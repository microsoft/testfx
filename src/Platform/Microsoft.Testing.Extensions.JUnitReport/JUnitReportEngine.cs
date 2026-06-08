// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.JUnitReport.Resources;
using Microsoft.Testing.Platform;
using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Configurations;
using Microsoft.Testing.Platform.Extensions.TestFramework;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.OutputDevice;
using Microsoft.Testing.Platform.Services;

namespace Microsoft.Testing.Extensions.JUnitReport;

internal sealed class JUnitReportEngine
{
    // Default cap on the rendered testpath property. Test trees can in theory be very
    // deep, and each level contributes a (capped) display name to the path, so we put
    // an additional ceiling on the rendered string to keep the XML output bounded.
    internal const int MaxTestPathLength = 64 * 1024;

    // Hard upper bound on parent-chain walks to defend against cycles or hostile
    // frameworks emitting self-referential parent UIDs.
    private const int MaxParentChainDepth = 1024;

    private const string TestPathSeparator = "/";

    private readonly IFileSystem _fileSystem;
    private readonly ITestApplicationModuleInfo _testApplicationModuleInfo;
    private readonly IEnvironment _environment;
    private readonly ICommandLineOptions _commandLineOptions;
    private readonly IConfiguration _configuration;
    private readonly IClock _clock;
    private readonly ITestFramework _testFramework;
    private readonly DateTimeOffset _testStartTime;
    private readonly int _exitCode;
    private readonly CancellationToken _cancellationToken;

    public JUnitReportEngine(
        IFileSystem fileSystem,
        ITestApplicationModuleInfo testApplicationModuleInfo,
        IEnvironment environment,
        ICommandLineOptions commandLineOptions,
        IConfiguration configuration,
        IClock clock,
        ITestFramework testFramework,
        DateTimeOffset testStartTime,
        int exitCode,
        CancellationToken cancellationToken)
    {
        _fileSystem = fileSystem;
        _testApplicationModuleInfo = testApplicationModuleInfo;
        _environment = environment;
        _commandLineOptions = commandLineOptions;
        _configuration = configuration;
        _clock = clock;
        _testFramework = testFramework;
        _testStartTime = testStartTime;
        _exitCode = exitCode;
        _cancellationToken = cancellationToken;
    }

    public Task<(string FileName, string? Warning)> GenerateReportAsync(
        CapturedTestResult[] results,
        IReadOnlyDictionary<string, TestResultCapture.ParentChainEntry> parentChain)
        => GenerateReportCoreAsync(results, parentChain, _clock.UtcNow);

    private async Task<(string FileName, string? Warning)> GenerateReportCoreAsync(
        CapturedTestResult[] results,
        IReadOnlyDictionary<string, TestResultCapture.ParentChainEntry> parentChain,
        DateTimeOffset finishTime)
    {
        _cancellationToken.ThrowIfCancellationRequested();

        bool fileNameExplicitlyProvided = _commandLineOptions.TryGetOptionArgumentList(
            JUnitReportGeneratorCommandLine.JUnitReportFileNameOptionName,
            out string[]? providedFileName);

        string fileName = fileNameExplicitlyProvided
            ? ResolveXmlFileName(GetProvidedFileName(providedFileName))
            : BuildDefaultFileName(finishTime);

        string outputDirectory = _configuration.GetTestResultDirectory();
        // Path.Combine short-circuits when the second argument is rooted, so an absolute
        // user-provided file name overrides the test results directory while validated
        // relative paths stay nested under it.
        string finalPath = Path.Combine(outputDirectory, fileName);
        string? finalDirectory = Path.GetDirectoryName(finalPath);
        if (!RoslynString.IsNullOrEmpty(finalDirectory))
        {
            _fileSystem.CreateDirectory(finalDirectory);
        }

        // Two-pass strategy: build all suites and resolve testcase-name collisions in
        // memory first, then stream the XML once. This keeps the writer logic linear
        // and lets us know testsuite-level aggregates (tests/failures/...) up front.
        SuiteSet suites = BuildSuites(results, parentChain, finishTime);

        return await WriteWithRetryAsync(finalPath, fileNameExplicitlyProvided, suites).ConfigureAwait(false);
    }

    private static string GetProvidedFileName(string[]? providedFileName)
        => providedFileName is { Length: > 0 }
            ? providedFileName[0]
            : throw ApplicationStateGuard.Unreachable();

    private async Task<(string FileName, string? Warning)> WriteWithRetryAsync(
        string finalPath,
        bool fileNameExplicitlyProvided,
        SuiteSet suites)
    {
        // Stream-to-temp-then-rename: write to a unique "<final>.<random>.tmp" in the
        // same directory and atomically move it into place at the end. The random suffix
        // prevents concurrent runs that happen to produce the same default file name
        // (same second / same results dir) from clobbering each other's tmp file.
        string tempPath = finalPath + "." + Path.GetRandomFileName() + ".tmp";
        await WriteXmlAsync(tempPath, suites).ConfigureAwait(false);

        // Explicit file names: overwrite. Default-generated file names: keep the user's
        // existing artifacts intact by disambiguating with "_N" suffixes when needed.
        if (fileNameExplicitlyProvided)
        {
            bool willOverwrite = _fileSystem.ExistFile(finalPath);
            _fileSystem.MoveFile(tempPath, finalPath, overwrite: true);
            return (
                finalPath,
                willOverwrite
                    ? string.Format(CultureInfo.InvariantCulture, ExtensionResources.JUnitReportFileExistsAndWillBeOverwritten, finalPath)
                    : null);
        }

        DateTimeOffset firstTry = _clock.UtcNow;
        string directory = Path.GetDirectoryName(finalPath) ?? string.Empty;
        string baseName = Path.GetFileNameWithoutExtension(finalPath);
        string extension = Path.GetExtension(finalPath);
        string candidate = finalPath;
        int attempt = 0;

        while (true)
        {
            _cancellationToken.ThrowIfCancellationRequested();

            if (!_fileSystem.ExistFile(candidate))
            {
                try
                {
                    _fileSystem.MoveFile(tempPath, candidate, overwrite: false);
                    return (candidate, null);
                }
                catch (IOException) when (_fileSystem.ExistFile(candidate))
                {
                    // A concurrent run won the race for this candidate name. Fall through
                    // to the disambiguation loop. Other IOExceptions (disk full, ACL,
                    // path too long, ...) intentionally propagate to the caller.
                }
            }

            if (_clock.UtcNow - firstTry > TimeSpan.FromSeconds(5))
            {
                // Last-ditch: keep the data by leaving the .tmp file in place and bubble
                // up so the user still has the artifact rather than losing it silently.
                throw new IOException(
                    string.Format(
                        CultureInfo.InvariantCulture,
                        "Unable to generate JUnit report at '{0}' after retrying for 5 seconds; intermediate file kept at '{1}'.",
                        finalPath,
                        tempPath));
            }

            attempt++;
            candidate = Path.Combine(directory, $"{baseName}_{attempt}{extension}");
        }
    }

    private async Task WriteXmlAsync(string tempPath, SuiteSet suites)
    {
        var settings = new XmlWriterSettings
        {
            Indent = true,
            Encoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
            CloseOutput = false,
            Async = true,
            CheckCharacters = false,
        };

        // FileMode.Create creates a fresh tmp file. The temp path is unique per run
        // (Path.GetRandomFileName), so we never collide with a sibling process's tmp.
        using IFileStream stream = _fileSystem.NewFileStream(tempPath, FileMode.Create);

#if NETCOREAPP
#pragma warning disable CA2007 // ConfigureAwait — extension code runs on its own threadpool task, no UI sync context
        await using var writer = XmlWriter.Create(stream.Stream, settings);
#pragma warning restore CA2007
#else
        using var writer = XmlWriter.Create(stream.Stream, settings);
#endif

        await writer.WriteStartDocumentAsync().ConfigureAwait(false);
        await writer.WriteStartElementAsync(prefix: null, localName: "testsuites", ns: null).ConfigureAwait(false);
        WriteAttribute(writer, "name", suites.Name);
        WriteAttribute(writer, "tests", suites.TotalTests.ToString(CultureInfo.InvariantCulture));
        WriteAttribute(writer, "failures", suites.TotalFailures.ToString(CultureInfo.InvariantCulture));
        WriteAttribute(writer, "errors", suites.TotalErrors.ToString(CultureInfo.InvariantCulture));
        WriteAttribute(writer, "skipped", suites.TotalSkipped.ToString(CultureInfo.InvariantCulture));
        WriteAttribute(writer, "time", FormatSeconds(suites.TotalDuration));
        WriteAttribute(writer, "timestamp", FormatTimestamp(suites.Timestamp));

        int suiteId = 0;
        foreach (Suite suite in suites.Suites)
        {
            _cancellationToken.ThrowIfCancellationRequested();
            await WriteSuiteAsync(writer, suite, suiteId++).ConfigureAwait(false);
        }

        await writer.WriteEndElementAsync().ConfigureAwait(false);
        await writer.WriteEndDocumentAsync().ConfigureAwait(false);
        await writer.FlushAsync().ConfigureAwait(false);
    }

    private async Task WriteSuiteAsync(XmlWriter writer, Suite suite, int suiteId)
    {
        await writer.WriteStartElementAsync(prefix: null, localName: "testsuite", ns: null).ConfigureAwait(false);
        WriteAttribute(writer, "name", suite.Name);
        WriteAttribute(writer, "tests", suite.Tests.Count.ToString(CultureInfo.InvariantCulture));
        WriteAttribute(writer, "failures", suite.Failures.ToString(CultureInfo.InvariantCulture));
        WriteAttribute(writer, "errors", suite.Errors.ToString(CultureInfo.InvariantCulture));
        WriteAttribute(writer, "skipped", suite.Skipped.ToString(CultureInfo.InvariantCulture));
        WriteAttribute(writer, "time", FormatSeconds(suite.TotalDuration));
        WriteAttribute(writer, "timestamp", FormatTimestamp(suite.Timestamp));
        WriteAttribute(writer, "hostname", _environment.MachineName);
        WriteAttribute(writer, "id", suiteId.ToString(CultureInfo.InvariantCulture));

        await WriteSuitePropertiesAsync(writer).ConfigureAwait(false);

        foreach (TestCase tc in suite.Tests)
        {
            _cancellationToken.ThrowIfCancellationRequested();
            await WriteTestCaseAsync(writer, tc).ConfigureAwait(false);
        }

        await writer.WriteEndElementAsync().ConfigureAwait(false);
    }

    private async Task WriteSuitePropertiesAsync(XmlWriter writer)
    {
        // Emit framework / process metadata as testsuite-level <properties> so the data
        // is preserved in the JUnit XML without bloating each <testcase>.
        await writer.WriteStartElementAsync(null, "properties", null).ConfigureAwait(false);
        await WritePropertyAsync(writer, "test-framework", _testFramework.DisplayName).ConfigureAwait(false);
        if (!RoslynString.IsNullOrEmpty(_testFramework.Version))
        {
            await WritePropertyAsync(writer, "test-framework-version", _testFramework.Version).ConfigureAwait(false);
        }

        if (!RoslynString.IsNullOrEmpty(_testFramework.Uid))
        {
            await WritePropertyAsync(writer, "test-framework-uid", _testFramework.Uid).ConfigureAwait(false);
        }

        await WritePropertyAsync(writer, "exit-code", _exitCode.ToString(CultureInfo.InvariantCulture)).ConfigureAwait(false);
        await writer.WriteEndElementAsync().ConfigureAwait(false);
    }

    private static async Task WriteTestCaseAsync(XmlWriter writer, TestCase tc)
    {
        await writer.WriteStartElementAsync(prefix: null, localName: "testcase", ns: null).ConfigureAwait(false);
        WriteAttribute(writer, "name", tc.Name);
        WriteAttribute(writer, "classname", tc.ClassName);
        WriteAttribute(writer, "time", FormatSeconds(tc.Result.Duration));

        // JUnit normative child order: <properties> first, then <skipped>/<error>/
        // <failure>, then <system-out>/<system-err>. Surefire/Jenkins parsers depend
        // on this order; do not reorder.
        await WritePropertiesAsync(writer, tc).ConfigureAwait(false);

        switch (tc.Result.Outcome)
        {
            case "skipped":
                await writer.WriteStartElementAsync(null, "skipped", null).ConfigureAwait(false);
                if (!RoslynString.IsNullOrEmpty(tc.Result.ErrorMessage))
                {
                    WriteAttribute(writer, "message", tc.Result.ErrorMessage!);
                }

                await writer.WriteEndElementAsync().ConfigureAwait(false);
                break;

            case "failed":
                await WriteFailureOrErrorAsync(writer, "failure", tc.Result).ConfigureAwait(false);
                break;

            case "errored":
            case "timedOut":
            case "cancelled":
                await WriteFailureOrErrorAsync(writer, "error", tc.Result).ConfigureAwait(false);
                break;

            case "passed":
            default:
                break;
        }

        if (!RoslynString.IsNullOrEmpty(tc.Result.StandardOutput))
        {
            await writer.WriteStartElementAsync(null, "system-out", null).ConfigureAwait(false);
            await writer.WriteStringAsync(XmlSafeText(tc.Result.StandardOutput)).ConfigureAwait(false);
            await writer.WriteEndElementAsync().ConfigureAwait(false);
        }

        if (!RoslynString.IsNullOrEmpty(tc.Result.StandardError))
        {
            await writer.WriteStartElementAsync(null, "system-err", null).ConfigureAwait(false);
            await writer.WriteStringAsync(XmlSafeText(tc.Result.StandardError)).ConfigureAwait(false);
            await writer.WriteEndElementAsync().ConfigureAwait(false);
        }

        await writer.WriteEndElementAsync().ConfigureAwait(false);
    }

    private static async Task WriteFailureOrErrorAsync(XmlWriter writer, string elementName, CapturedTestResult result)
    {
        await writer.WriteStartElementAsync(null, elementName, null).ConfigureAwait(false);
        if (!RoslynString.IsNullOrEmpty(result.ErrorMessage))
        {
            WriteAttribute(writer, "message", result.ErrorMessage!);
        }

        if (!RoslynString.IsNullOrEmpty(result.ExceptionType))
        {
            WriteAttribute(writer, "type", result.ExceptionType!);
        }

        if (!RoslynString.IsNullOrEmpty(result.StackTrace))
        {
            await writer.WriteStringAsync(XmlSafeText(result.StackTrace)).ConfigureAwait(false);
        }

        await writer.WriteEndElementAsync().ConfigureAwait(false);
    }

    private static async Task WritePropertiesAsync(XmlWriter writer, TestCase tc)
    {
        bool hasTraits = tc.Result.Traits is { Count: > 0 };
        bool hasTestPath = !RoslynString.IsNullOrEmpty(tc.TestPath);
        bool isDuplicate = tc.DuplicateIndex > 0;

        if (!hasTraits && !hasTestPath && !isDuplicate && RoslynString.IsNullOrEmpty(tc.Result.Uid))
        {
            return;
        }

        await writer.WriteStartElementAsync(null, "properties", null).ConfigureAwait(false);

        if (!RoslynString.IsNullOrEmpty(tc.Result.Uid))
        {
            await WritePropertyAsync(writer, "uid", tc.Result.Uid).ConfigureAwait(false);
        }

        if (hasTestPath)
        {
            await WritePropertyAsync(writer, "testpath", tc.TestPath!).ConfigureAwait(false);
        }

        if (isDuplicate)
        {
            await WritePropertyAsync(writer, "original-name", tc.OriginalName!).ConfigureAwait(false);
            await WritePropertyAsync(writer, "attempt-index", tc.DuplicateIndex.ToString(CultureInfo.InvariantCulture)).ConfigureAwait(false);
            await WritePropertyAsync(writer, "attempt-of", tc.DuplicateOf.ToString(CultureInfo.InvariantCulture)).ConfigureAwait(false);
        }

        if (hasTraits)
        {
            foreach (KeyValuePair<string, string> trait in tc.Result.Traits!)
            {
                // Prefix with "trait." so trait keys cannot collide with reserved
                // property names like "uid" / "testpath" and consumers can filter
                // trait properties from intrinsic ones.
                await WritePropertyAsync(writer, $"trait.{trait.Key}", trait.Value).ConfigureAwait(false);
            }
        }

        await writer.WriteEndElementAsync().ConfigureAwait(false);
    }

    private static async Task WritePropertyAsync(XmlWriter writer, string name, string value)
    {
        await writer.WriteStartElementAsync(null, "property", null).ConfigureAwait(false);
        WriteAttribute(writer, "name", name);
        WriteAttribute(writer, "value", value);
        await writer.WriteEndElementAsync().ConfigureAwait(false);
    }

    private static void WriteAttribute(XmlWriter writer, string name, string value)
        => writer.WriteAttributeString(name, XmlSafeText(value));

    // Sanitizes a string so it can be safely written by XmlWriter. XML 1.0 forbids
    // most control chars, lone surrogates, and 0xFFFE/0xFFFF; CheckCharacters is
    // disabled on the writer for throughput so all sanitization happens here. Invalid
    // chars are replaced with U+FFFD (REPLACEMENT CHARACTER) to preserve byte-length
    // intuition rather than silently shifting offsets.
    internal static string XmlSafeText(string? value)
    {
        if (RoslynString.IsNullOrEmpty(value))
        {
            return string.Empty;
        }

        StringBuilder? sb = null;
        for (int i = 0; i < value.Length; i++)
        {
            char c = value[i];
            if (char.IsHighSurrogate(c) && i + 1 < value.Length && char.IsLowSurrogate(value[i + 1]))
            {
                int cp = char.ConvertToUtf32(c, value[i + 1]);
                if (cp is >= 0x10000 and <= 0x10FFFF)
                {
                    sb?.Append(c).Append(value[i + 1]);
                    i++;
                    continue;
                }

                sb ??= NewBuilder(value, i);
                sb.Append('\uFFFD');
                i++;
                continue;
            }

            if (IsXmlChar(c))
            {
                sb?.Append(c);
                continue;
            }

            sb ??= NewBuilder(value, i);
            sb.Append('\uFFFD');
        }

        return sb is null ? value : sb.ToString();

        static StringBuilder NewBuilder(string original, int copyUpTo)
        {
            var b = new StringBuilder(original.Length);
            if (copyUpTo > 0)
            {
                b.Append(original, 0, copyUpTo);
            }

            return b;
        }
    }

    private static bool IsXmlChar(char c)
        => c is '\t' or '\n' or '\r' or (>= '\u0020' and <= '\uD7FF') or (>= '\uE000' and <= '\uFFFD');

    private static string FormatSeconds(TimeSpan duration)
        => duration.TotalSeconds.ToString("0.000", CultureInfo.InvariantCulture);

    private static string FormatTimestamp(DateTimeOffset timestamp)
        => timestamp.UtcDateTime.ToString("yyyy-MM-ddTHH:mm:ss.fff", CultureInfo.InvariantCulture);

    private SuiteSet BuildSuites(
        CapturedTestResult[] results,
        IReadOnlyDictionary<string, TestResultCapture.ParentChainEntry> parentChain,
        DateTimeOffset finishTime)
    {
        string moduleName = Path.GetFileNameWithoutExtension(_testApplicationModuleInfo.GetCurrentTestApplicationFullPath());

        // Preserve assembly order and group testcases by classname. The fallback
        // bucket uses the immediate parent's display name (so MTP-native tests fan
        // out into multiple suites rather than one giant "__unknown__"), falling
        // back to the module name when even that is missing.
        var orderedKeys = new List<string>();
        var suiteBuckets = new Dictionary<string, List<TestCase>>(StringComparer.Ordinal);
        var nameCounts = new Dictionary<string, Dictionary<string, int>>(StringComparer.Ordinal);

        foreach (CapturedTestResult result in results)
        {
            string suiteName = ResolveSuiteName(result, parentChain, moduleName);

            if (!suiteBuckets.TryGetValue(suiteName, out List<TestCase>? bucket))
            {
                bucket = [];
                suiteBuckets[suiteName] = bucket;
                orderedKeys.Add(suiteName);
#pragma warning disable IDE0028 // Collection initialization cannot be simplified — Dictionary requires StringComparer.Ordinal
                nameCounts.Add(suiteName, new Dictionary<string, int>(StringComparer.Ordinal));
#pragma warning restore IDE0028
            }

            string baseName = result.MethodName ?? result.DisplayName;
            string testPath = BuildTestPath(result, parentChain);

            bucket.Add(new TestCase
            {
                ClassName = suiteName,
                Name = baseName,
                OriginalName = baseName,
                TestPath = testPath,
                Result = result,
                DuplicateIndex = 0,
                DuplicateOf = 0,
            });
        }

        // Second mini-pass: detect duplicate (classname, name) pairs and disambiguate
        // them with " [attempt N]" suffixes plus original-name / attempt-index /
        // attempt-of properties. We never drop rows: parameterized tests, theory
        // data, and intentional retries all need to survive into the JUnit report.
        foreach (string suiteKey in orderedKeys)
        {
            List<TestCase> bucket = suiteBuckets[suiteKey];
            Dictionary<string, int> counts = nameCounts[suiteKey];
            foreach (TestCase tc in bucket)
            {
                counts[tc.OriginalName] = counts.TryGetValue(tc.OriginalName, out int total) ? total + 1 : 1;
            }

            var seen = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (TestCase tc in bucket)
            {
                int total = counts[tc.OriginalName];
                if (total <= 1)
                {
                    continue;
                }

                seen.TryGetValue(tc.OriginalName, out int index);
                index++;
                seen[tc.OriginalName] = index;
                tc.DuplicateIndex = index;
                tc.DuplicateOf = total;
                tc.Name = $"{tc.OriginalName} [attempt {index.ToString(CultureInfo.InvariantCulture)}]";
            }
        }

        var suites = new List<Suite>(orderedKeys.Count);
        long totalTests = 0;
        long totalFailures = 0;
        long totalErrors = 0;
        long totalSkipped = 0;
        TimeSpan totalDuration = TimeSpan.Zero;

        foreach (string suiteKey in orderedKeys)
        {
            List<TestCase> bucket = suiteBuckets[suiteKey];
            int failures = 0;
            int errors = 0;
            int skipped = 0;
            TimeSpan duration = TimeSpan.Zero;
            DateTimeOffset suiteStart = finishTime;
            bool sawStart = false;

            foreach (TestCase tc in bucket)
            {
                duration += tc.Result.Duration;
                switch (tc.Result.Outcome)
                {
                    case "failed":
                        failures++;
                        break;
                    case "errored":
                    case "timedOut":
                    case "cancelled":
                        errors++;
                        break;
                    case "skipped":
                        skipped++;
                        break;
                }

                if (tc.Result.StartTime is DateTimeOffset st && (!sawStart || st < suiteStart))
                {
                    suiteStart = st;
                    sawStart = true;
                }
            }

            suites.Add(new Suite
            {
                Name = suiteKey,
                Tests = bucket,
                Failures = failures,
                Errors = errors,
                Skipped = skipped,
                TotalDuration = duration,
                Timestamp = sawStart ? suiteStart : _testStartTime,
            });

            totalTests += bucket.Count;
            totalFailures += failures;
            totalErrors += errors;
            totalSkipped += skipped;
            totalDuration += duration;
        }

        return new SuiteSet
        {
            Name = moduleName,
            Suites = suites,
            TotalTests = totalTests,
            TotalFailures = totalFailures,
            TotalErrors = totalErrors,
            TotalSkipped = totalSkipped,
            TotalDuration = totalDuration,
            Timestamp = _testStartTime,
        };
    }

    private static string ResolveSuiteName(
        CapturedTestResult result,
        IReadOnlyDictionary<string, TestResultCapture.ParentChainEntry> parentChain,
        string moduleName)
        => !RoslynString.IsNullOrEmpty(result.ClassName)
            ? result.ClassName!
            : result.ParentRawUid is string parentUid
                && parentChain.TryGetValue(parentUid, out TestResultCapture.ParentChainEntry parent)
                && !RoslynString.IsNullOrEmpty(parent.DisplayName)
                ? parent.DisplayName
                : moduleName;

    private static string BuildTestPath(
        CapturedTestResult result,
        IReadOnlyDictionary<string, TestResultCapture.ParentChainEntry> parentChain)
    {
        // Per RFC 016, testpath is the "/"-joined display names from the root down
        // to and including this node (e.g. "Root/Container/Subcontainer/MyTest").
        // The leaf is therefore always the test's own display name; the parent
        // chain (if any) is prepended in root-first order.
        var segments = new List<string>();

        if (result.ParentRawUid is not null)
        {
            string? current = result.ParentRawUid;
            int depth = 0;
            var visited = new HashSet<string>(StringComparer.Ordinal);
            while (current is not null && depth < MaxParentChainDepth && visited.Add(current))
            {
                if (!parentChain.TryGetValue(current, out TestResultCapture.ParentChainEntry entry))
                {
                    // Parent UID present but missing from the chain (truncated capture
                    // window, framework bug, ...). Stop walking; the leaf below still
                    // gives a usable, non-empty path.
                    break;
                }

                segments.Add(entry.DisplayName);
                current = entry.ParentRawUid;
                depth++;
            }

            segments.Reverse();
        }

        // Always include the test's own display name as the leaf so root-level
        // tests still get a non-empty testpath.
        segments.Add(result.DisplayName);

        var sb = new StringBuilder();

        // Compute the full untruncated testpath length up front so the truncation
        // marker reports the real original length (not the partially-built buffer
        // length at the moment we exceeded the cap, which omits remaining segments).
        int totalLength = 0;
        for (int i = 0; i < segments.Count; i++)
        {
            if (i > 0)
            {
                totalLength += TestPathSeparator.Length;
            }

            totalLength += segments[i].Length;
        }

        for (int i = 0; i < segments.Count; i++)
        {
            if (i > 0)
            {
                sb.Append(TestPathSeparator);
            }

            sb.Append(segments[i]);
            if (sb.Length > MaxTestPathLength)
            {
                int cut = MaxTestPathLength;

                // Don't split a surrogate pair when truncating: drop the high surrogate too.
                if (cut > 0 && char.IsHighSurrogate(sb[cut - 1]))
                {
                    cut--;
                }

                sb.Length = cut;
                sb.Append("\n…[truncated, original length: ").Append(totalLength.ToString(CultureInfo.InvariantCulture)).Append(']');
                break;
            }
        }

        return sb.ToString();
    }

    private string BuildDefaultFileName(DateTimeOffset finishTime)
    {
        string user = _environment.GetEnvironmentVariable("UserName")
            ?? _environment.GetEnvironmentVariable("USER")
            ?? "user";
        string moduleName = Path.GetFileNameWithoutExtension(_testApplicationModuleInfo.GetCurrentTestApplicationFullPath());
        string targetFrameworkMoniker = GetTargetFrameworkMoniker();
        string raw = $"{user}_{_environment.MachineName}_{moduleName}_{targetFrameworkMoniker}_{finishTime:yyyy-MM-dd_HH_mm_ss}.xml";
        return ReplaceInvalidFileNameChars(raw);
    }

    private string ResolveXmlFileName(string template)
    {
        string processName = Path.GetFileNameWithoutExtension(_testApplicationModuleInfo.GetCurrentTestApplicationFullPath());
        string processId = _environment.ProcessId.ToString(CultureInfo.InvariantCulture);
        Dictionary<string, string> replacements = ArtifactNamingHelper.GetStandardReplacements(processName, processId, _clock.UtcNow);
        string resolved = ArtifactNamingHelper.ResolveTemplate(template, replacements);
        string directoryPart = Path.GetDirectoryName(resolved) ?? string.Empty;
        string sanitizedFileName = ReplaceInvalidFileNameChars(Path.GetFileName(resolved));
        return directoryPart.Length == 0
            ? sanitizedFileName
            : Path.Combine(directoryPart, sanitizedFileName);
    }

    private static string GetTargetFrameworkMoniker()
        => TargetFrameworkParser.GetShortTargetFramework(
            Assembly.GetEntryAssembly()?.GetCustomAttribute<TargetFrameworkAttribute>()?.FrameworkDisplayName)
            ?? TargetFrameworkParser.GetShortTargetFramework(RuntimeInformation.FrameworkDescription)
            ?? "unknown";

    private static string ReplaceInvalidFileNameChars(string fileName)
    {
        var sb = new StringBuilder(fileName.Length);
        foreach (char c in fileName)
        {
            sb.Append(IsInvalidFileNameChar(c) ? '_' : c);
        }

        string replaced = sb.ToString().TrimEnd();
        if (IsReservedFileName(replaced))
        {
            replaced = '_' + replaced;
        }

        return replaced;
    }

    private static bool IsInvalidFileNameChar(char c)
        // Keep the explicit file-name sanitization aligned with TRX / HtmlReport naming
        // so placeholders and cross-platform reserved characters produce compatible names.
        => c is < ' ' or '"' or '<' or '>' or '|' or ':' or '*' or '?' or '\\' or '/' or '@' or '(' or ')' or '^' or ' ';

    private static bool IsReservedFileName(string fileName)
    {
        string bareName = fileName;
        int dot = bareName.IndexOf('.');
        if (dot >= 0)
        {
            bareName = bareName.Substring(0, dot);
        }

        return bareName.Equals("CON", StringComparison.OrdinalIgnoreCase)
            || bareName.Equals("PRN", StringComparison.OrdinalIgnoreCase)
            || bareName.Equals("AUX", StringComparison.OrdinalIgnoreCase)
            || bareName.Equals("NUL", StringComparison.OrdinalIgnoreCase)
            || bareName.Equals("CLOCK$", StringComparison.OrdinalIgnoreCase)
            || IsReservedNameWithNumber(bareName, "COM")
            || IsReservedNameWithNumber(bareName, "LPT");

        static bool IsReservedNameWithNumber(string bareName, string prefix)
            => bareName.Length == 4
                && bareName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
                && bareName[3] is >= '1' and <= '9';
    }

    private sealed class SuiteSet
    {
        public required string Name { get; init; }

        public required IReadOnlyList<Suite> Suites { get; init; }

        public required long TotalTests { get; init; }

        public required long TotalFailures { get; init; }

        public required long TotalErrors { get; init; }

        public required long TotalSkipped { get; init; }

        public required TimeSpan TotalDuration { get; init; }

        public required DateTimeOffset Timestamp { get; init; }
    }

    private sealed class Suite
    {
        public required string Name { get; init; }

        public required IReadOnlyList<TestCase> Tests { get; init; }

        public required int Failures { get; init; }

        public required int Errors { get; init; }

        public required int Skipped { get; init; }

        public required TimeSpan TotalDuration { get; init; }

        public required DateTimeOffset Timestamp { get; init; }
    }

    private sealed class TestCase
    {
        public required string ClassName { get; init; }

        public required string Name { get; set; }

        public required string OriginalName { get; init; }

        public required string TestPath { get; init; }

        public required CapturedTestResult Result { get; init; }

        public required int DuplicateIndex { get; set; }

        public required int DuplicateOf { get; set; }
    }
}
