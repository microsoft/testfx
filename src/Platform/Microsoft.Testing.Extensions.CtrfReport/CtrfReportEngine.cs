// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text.Json;

using Microsoft.Testing.Extensions.CtrfReport.Resources;
using Microsoft.Testing.Platform;
using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Configurations;
using Microsoft.Testing.Platform.Extensions.TestFramework;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.OutputDevice;
using Microsoft.Testing.Platform.Services;

namespace Microsoft.Testing.Extensions.CtrfReport;

internal sealed class CtrfReportEngine
{
    // CTRF spec: https://github.com/ctrf-io/ctrf
    private const string CtrfReportFormat = "CTRF";
    private const string CtrfSpecVersion = "0.0.0";

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

    public CtrfReportEngine(
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

    public Task<(string FileName, string? Warning)> GenerateReportAsync(CapturedTestResult[] results)
        => GenerateReportCoreAsync(results, _clock.UtcNow);

    private async Task<(string FileName, string? Warning)> GenerateReportCoreAsync(CapturedTestResult[] results, DateTimeOffset finishTime)
    {
        _cancellationToken.ThrowIfCancellationRequested();

        bool fileNameExplicitlyProvided = _commandLineOptions.TryGetOptionArgumentList(
            CtrfReportGeneratorCommandLine.CtrfReportFileNameOptionName,
            out string[]? providedFileName);

        string fileName = fileNameExplicitlyProvided
            ? ResolveJsonFileName(GetProvidedFileName(providedFileName))
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

        byte[] bytes = BuildCtrfJson(results, finishTime);

        return await WriteWithRetryAsync(finalPath, bytes, fileNameExplicitlyProvided).ConfigureAwait(false);
    }

    private static string GetProvidedFileName(string[]? providedFileName)
        => providedFileName is { Length: > 0 }
            ? providedFileName[0]
            : throw ApplicationStateGuard.Unreachable();

    private async Task<(string FileName, string? Warning)> WriteWithRetryAsync(string finalPath, byte[] bytes, bool fileNameExplicitlyProvided)
    {
        // Explicit file names: use FileMode.Create (overwrite). Default-generated file
        // names: use FileMode.CreateNew but retry with disambiguating suffixes when the
        // file already exists, so concurrent runs (or two runs within the same second
        // sharing the result directory) don't fail with IOException.
        if (fileNameExplicitlyProvided)
        {
            bool willOverwrite = _fileSystem.ExistFile(finalPath);
            await WriteAsync(finalPath, FileMode.Create, bytes).ConfigureAwait(false);
            return (
                finalPath,
                willOverwrite
                    ? string.Format(CultureInfo.InvariantCulture, ExtensionResources.CtrfReportFileExistsAndWillBeOverwritten, finalPath)
                    : null);
        }

        DateTimeOffset firstTry = _clock.UtcNow;
        string directory = Path.GetDirectoryName(finalPath) ?? string.Empty;
        string fileName = Path.GetFileName(finalPath);
        SplitCtrfExtension(fileName, out string baseName, out string extension);
        string candidate = finalPath;
        int attempt = 0;

        while (true)
        {
            _cancellationToken.ThrowIfCancellationRequested();

            try
            {
                await WriteAsync(candidate, FileMode.CreateNew, bytes).ConfigureAwait(false);
                return (candidate, null);
            }
            catch (IOException) when (_fileSystem.ExistFile(candidate))
            {
                // The IOException was caused by the file already existing. Try a
                // suffixed name. Any other IOException (disk full, permission, path
                // too long, etc.) is not caught here and will propagate to the caller.
                // Bound by both wall-clock (5s) and attempt count (1000) so we never
                // spin forever in pathological cases like a clock that doesn't advance.
                if (_clock.UtcNow - firstTry > TimeSpan.FromSeconds(5) || attempt >= 1_000)
                {
                    throw;
                }

                attempt++;
                candidate = Path.Combine(directory, $"{baseName}_{attempt}{extension}");
            }
        }
    }

    // Split a file name into base + extension while preserving the CTRF
    // double-extension convention (`*.ctrf.json`). The disambiguation suffix
    // must land before `.ctrf.json` so that downstream CTRF readers continue to
    // recognize the file by its conventional extension.
    private static void SplitCtrfExtension(string fileName, out string baseName, out string extension)
    {
        const string ctrfJsonSuffix = ".ctrf.json";
        if (fileName.EndsWith(ctrfJsonSuffix, StringComparison.OrdinalIgnoreCase) && fileName.Length > ctrfJsonSuffix.Length)
        {
            baseName = fileName.Substring(0, fileName.Length - ctrfJsonSuffix.Length);
            extension = fileName.Substring(fileName.Length - ctrfJsonSuffix.Length);
            return;
        }

        baseName = Path.GetFileNameWithoutExtension(fileName);
        extension = Path.GetExtension(fileName);
    }

    private async Task WriteAsync(string path, FileMode mode, byte[] bytes)
    {
        // Note that we need to dispose the IFileStream, not the inner stream.
        // IFileStream implementations will be responsible to dispose their inner stream.
        using IFileStream stream = _fileSystem.NewFileStream(path, mode);
#if NETCOREAPP
        await stream.Stream.WriteAsync(bytes.AsMemory(), _cancellationToken).ConfigureAwait(false);
#else
        await stream.Stream.WriteAsync(bytes, 0, bytes.Length, _cancellationToken).ConfigureAwait(false);
#endif
    }

    private string BuildDefaultFileName(DateTimeOffset finishTime)
    {
        string user = _environment.GetEnvironmentVariable("UserName")
            ?? _environment.GetEnvironmentVariable("USER")
            ?? "user";
        string moduleName = Path.GetFileNameWithoutExtension(_testApplicationModuleInfo.GetCurrentTestApplicationFullPath());
        string targetFrameworkMoniker = GetTargetFrameworkMoniker();
        string raw = $"{user}_{_environment.MachineName}_{moduleName}_{targetFrameworkMoniker}_{finishTime:yyyy-MM-dd_HH_mm_ss}.ctrf.json";
        return ReplaceInvalidFileNameChars(raw);
    }

    private string ResolveJsonFileName(string template)
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

    // CTRF `osPlatform` is the short Node-style platform identifier (e.g. "win32",
    // "linux", "darwin"). The full descriptive OS string belongs in `osVersion`.
    private static string GetCtrfOsPlatform()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return "win32";
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            return "linux";
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            return "darwin";
        }

#if NETCOREAPP
        if (RuntimeInformation.IsOSPlatform(OSPlatform.FreeBSD))
        {
            return "freebsd";
        }
#endif

        return "unknown";
    }

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
        // Keep the explicit file-name sanitization aligned with TRX report naming so
        // placeholders and cross-platform reserved characters produce compatible names.
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

    private byte[] BuildCtrfJson(CapturedTestResult[] results, DateTimeOffset finishTime)
    {
        // Collapse multiple captures sharing the same UID into a single CTRF test
        // entry. The CTRF spec models retries as nested `retryAttempts[]` records
        // and exposes `flaky: true` on the final passing row; emitting separate
        // top-level rows for retries would inflate `summary.tests` and double-count
        // outcomes.
        List<CollapsedTestResult> collapsed = CollapseAttempts(results);

        // Aggregate summary counts from the collapsed (final) outcomes only.
        int passed = 0;
        int failed = 0;
        int skipped = 0;
        int pending = 0;
        int other = 0;
        int flaky = 0;
        foreach (CollapsedTestResult c in collapsed)
        {
            switch (c.Final.Status)
            {
                case "passed": passed++; break;
                case "failed": failed++; break;
                case "skipped": skipped++; break;
                case "pending": pending++; break;
                default: other++; break;
            }

            if (c.IsFlaky)
            {
                flaky++;
            }
        }

        long startMs = _testStartTime.ToUnixTimeMilliseconds();
        long stopMs = finishTime.ToUnixTimeMilliseconds();
        long durationMs = Math.Max(0, stopMs - startMs);

        using var ms = new MemoryStream(capacity: 8 * 1024);
        // We deliberately use the default Default encoder rather than
        // UnsafeRelaxedJsonEscaping: CTRF documents routinely flow into web
        // dashboards that embed JSON into HTML/JS, and test names/messages are
        // attacker-controllable. The default safe encoder keeps `<`, `>`, `&`
        // escaped so a test display name like `<script>alert(1)</script>` can't
        // become an XSS vector in downstream consumers.
        var writerOptions = new JsonWriterOptions
        {
            Indented = true,
        };

        using (var writer = new Utf8JsonWriter(ms, writerOptions))
        {
            writer.WriteStartObject();

            writer.WriteString("reportFormat", CtrfReportFormat);
            // CTRF is still in pre-1.0; the upstream spec is at "0.0.0" today
            // (see https://github.com/ctrf-io/ctrf/blob/main/spec/ctrf.md).
            // Bump this constant whenever we update against a newer schema revision.
            writer.WriteString("specVersion", CtrfSpecVersion);
            writer.WriteString("reportId", Guid.NewGuid().ToString("D"));
            writer.WriteString("timestamp", finishTime.ToString("O", CultureInfo.InvariantCulture));
            writer.WriteString(
                "generatedBy",
                $"Microsoft.Testing.Extensions.CtrfReport@{ExtensionVersion.DefaultSemVer}");

            writer.WritePropertyName("results");
            writer.WriteStartObject();

            // results.tool
            writer.WritePropertyName("tool");
            writer.WriteStartObject();
            // CTRF spec requires `tool.name` to be a non-empty string. Fall back to
            // a sentinel rather than emitting an empty string (which would fail
            // strict schema validation by downstream CTRF consumers).
            string toolName = RoslynString.IsNullOrEmpty(_testFramework.DisplayName)
                ? "unknown"
                : _testFramework.DisplayName;
            writer.WriteString("name", toolName);
            if (!RoslynString.IsNullOrEmpty(_testFramework.Version))
            {
                writer.WriteString("version", _testFramework.Version);
            }

            writer.WritePropertyName("extra");
            writer.WriteStartObject();
            writer.WriteString("uid", _testFramework.Uid);
            writer.WriteEndObject();
            writer.WriteEndObject();

            // results.summary
            writer.WritePropertyName("summary");
            writer.WriteStartObject();
            writer.WriteNumber("tests", collapsed.Count);
            writer.WriteNumber("passed", passed);
            writer.WriteNumber("failed", failed);
            writer.WriteNumber("skipped", skipped);
            writer.WriteNumber("pending", pending);
            writer.WriteNumber("other", other);
            writer.WriteNumber("flaky", flaky);
            writer.WriteNumber("start", startMs);
            writer.WriteNumber("stop", stopMs);
            writer.WriteNumber("duration", durationMs);
            writer.WriteEndObject();

            // results.environment
            writer.WritePropertyName("environment");
            writer.WriteStartObject();
            string user = _environment.GetEnvironmentVariable("UserName")
                ?? _environment.GetEnvironmentVariable("USER")
                ?? string.Empty;
            // CTRF `osPlatform` expects a short identifier such as "win32", "linux" or
            // "darwin"; the full descriptive string belongs in `osVersion`.
            writer.WriteString("osPlatform", GetCtrfOsPlatform());
            writer.WriteString("osVersion", RuntimeInformation.OSDescription);
            // CTRF `extra` MUST be an object (schema enforces additionalProperties: false
            // on environment, with `extra` typed as object). We surface the test module
            // path and process exit code here rather than as top-level environment fields
            // because there is no first-class CTRF slot for them.
            writer.WritePropertyName("extra");
            writer.WriteStartObject();
            writer.WriteString("user", user);
            writer.WriteString("machine", _environment.MachineName);
            writer.WriteNumber("exitCode", _exitCode);
            writer.WriteString("testApplication", _testApplicationModuleInfo.GetCurrentTestApplicationFullPath());
            writer.WriteEndObject();
            writer.WriteEndObject();

            // results.tests
            writer.WritePropertyName("tests");
            writer.WriteStartArray();

            foreach (CollapsedTestResult c in collapsed)
            {
                WriteTest(writer, c);
            }

            writer.WriteEndArray();
            writer.WriteEndObject();

            writer.WriteEndObject();
        }

        return ms.ToArray();
    }

    private static void WriteTest(Utf8JsonWriter writer, CollapsedTestResult c)
    {
        CapturedTestResult r = c.Final;
        writer.WriteStartObject();

        // CTRF spec: tests[i].name MUST be a non-empty string. Fall back to UID
        // (also non-empty) when the framework didn't supply a display name.
        string name = RoslynString.IsNullOrEmpty(r.DisplayName) ? r.Uid : r.DisplayName;
        writer.WriteString("name", name);
        writer.WriteString("status", r.Status);
        writer.WriteNumber("duration", (long)Math.Max(0, r.Duration.TotalMilliseconds));

        if (r.StartTime is { } start)
        {
            writer.WriteNumber("start", start.ToUnixTimeMilliseconds());
        }

        if (r.EndTime is { } end)
        {
            writer.WriteNumber("stop", end.ToUnixTimeMilliseconds());
        }

        if (r.RawStatus is not null)
        {
            writer.WriteString("rawStatus", r.RawStatus);
        }

        // CTRF `suite` is an array of strings (minItems: 1) representing the test
        // hierarchy (e.g. ["MyNamespace", "MyClass"]).
        if (r.Namespace is not null || r.ClassName is not null)
        {
            writer.WritePropertyName("suite");
            writer.WriteStartArray();
            if (r.Namespace is not null)
            {
                writer.WriteStringValue(r.Namespace);
            }

            if (r.ClassName is not null)
            {
                writer.WriteStringValue(r.ClassName);
            }

            writer.WriteEndArray();
        }

        if (r.ErrorMessage is not null)
        {
            writer.WriteString("message", r.ErrorMessage);
        }

        if (r.StackTrace is not null)
        {
            writer.WriteString("trace", r.StackTrace);
        }

        if (r.FilePath is not null)
        {
            writer.WriteString("filePath", r.FilePath);
        }

        if (r.Line is { } lineNumber)
        {
            writer.WriteNumber("line", lineNumber);
        }

        if (c.PriorAttempts.Count > 0)
        {
            writer.WriteNumber("retries", c.PriorAttempts.Count);
            writer.WritePropertyName("retryAttempts");
            writer.WriteStartArray();
            for (int i = 0; i < c.PriorAttempts.Count; i++)
            {
                WriteRetryAttempt(writer, c.PriorAttempts[i], attemptNumber: i + 1);
            }

            writer.WriteEndArray();
        }

        if (c.IsFlaky)
        {
            writer.WriteBoolean("flaky", true);
        }

        WriteOutputLines(writer, "stdout", r.StandardOutput);
        WriteOutputLines(writer, "stderr", r.StandardError);

        // CTRF `labels` is reserved for user-controlled, classification-style
        // metadata (priority, severity, external IDs, etc.). We only emit the
        // traits collected from MTP TestMetadataProperty here. Synthetic
        // framework-generated metadata (method name, exception type, MTP UID)
        // lives in the per-test `extra` object instead so CTRF consumers can
        // filter/group by labels without seeing our internals.
        if (r.Traits is { Count: > 0 })
        {
            writer.WritePropertyName("labels");
            writer.WriteStartObject();
            foreach (KeyValuePair<string, string> trait in r.Traits)
            {
                writer.WriteString(trait.Key, trait.Value);
            }

            writer.WriteEndObject();
        }

        // CTRF `extra` (free-form object) — the CTRF spec doesn't define a
        // dedicated stable identifier so we surface the MTP UID here for
        // cross-tool correlation, alongside other framework metadata.
        writer.WritePropertyName("extra");
        writer.WriteStartObject();
        writer.WriteString("uid", r.Uid);
        if (r.MethodName is not null)
        {
            writer.WriteString("method", r.MethodName);
        }

        if (r.ExceptionType is not null)
        {
            writer.WriteString("exceptionType", r.ExceptionType);
        }

        writer.WriteEndObject();

        writer.WriteEndObject();
    }

    private static void WriteRetryAttempt(Utf8JsonWriter writer, CapturedTestResult attempt, int attemptNumber)
    {
        writer.WriteStartObject();
        writer.WriteNumber("attempt", attemptNumber);
        writer.WriteString("status", attempt.Status);
        writer.WriteNumber("duration", (long)Math.Max(0, attempt.Duration.TotalMilliseconds));
        if (attempt.StartTime is { } start)
        {
            writer.WriteNumber("start", start.ToUnixTimeMilliseconds());
        }

        if (attempt.EndTime is { } end)
        {
            writer.WriteNumber("stop", end.ToUnixTimeMilliseconds());
        }

        if (attempt.ErrorMessage is not null)
        {
            writer.WriteString("message", attempt.ErrorMessage);
        }

        if (attempt.StackTrace is not null)
        {
            writer.WriteString("trace", attempt.StackTrace);
        }

        if (attempt.Line is { } line)
        {
            writer.WriteNumber("line", line);
        }

        WriteOutputLines(writer, "stdout", attempt.StandardOutput);
        WriteOutputLines(writer, "stderr", attempt.StandardError);

        if (attempt.RawStatus is not null || attempt.ExceptionType is not null)
        {
            writer.WritePropertyName("extra");
            writer.WriteStartObject();
            if (attempt.RawStatus is not null)
            {
                writer.WriteString("rawStatus", attempt.RawStatus);
            }

            if (attempt.ExceptionType is not null)
            {
                writer.WriteString("exceptionType", attempt.ExceptionType);
            }

            writer.WriteEndObject();
        }

        writer.WriteEndObject();
    }

    // CTRF `stdout`/`stderr` are typed as an array of lines (each item is one line
    // of captured output). Splitting on LF (handling optional CR) preserves the
    // original line structure for consumers that present output per-line.
    private static void WriteOutputLines(Utf8JsonWriter writer, string propertyName, string? output)
    {
        if (output is null)
        {
            return;
        }

        writer.WritePropertyName(propertyName);
        writer.WriteStartArray();
        int start = 0;
        for (int i = 0; i < output.Length; i++)
        {
            if (output[i] == '\n')
            {
                int end = i;
                if (end > start && output[end - 1] == '\r')
                {
                    end--;
                }

                writer.WriteStringValue(output.AsSpan(start, end - start));
                start = i + 1;
            }
        }

        if (start < output.Length)
        {
            // Emit the trailing segment after the last LF (no trailing entry when
            // the input ends with LF — a trailing newline isn't an additional line).
            int end = output.Length;
            if (end > start && output[end - 1] == '\r')
            {
                end--;
            }

            writer.WriteStringValue(output.AsSpan(start, end - start));
        }

        writer.WriteEndArray();
    }

    private static List<CollapsedTestResult> CollapseAttempts(CapturedTestResult[] results)
    {
        // For each UID, group all captures in arrival order: the latest entry becomes the
        // final test record, earlier entries become `retryAttempts[]`. Preserves the
        // insertion order of first-seen UIDs in the output (stable across runs).
        var byUid = new Dictionary<string, int>(StringComparer.Ordinal);
        var collapsed = new List<CollapsedTestResult>(results.Length);
        foreach (CapturedTestResult r in results)
        {
            if (byUid.TryGetValue(r.Uid, out int existingIndex))
            {
                CollapsedTestResult existing = collapsed[existingIndex];
                existing.PriorAttempts.Add(existing.Final);
                collapsed[existingIndex] = existing with { Final = r };
            }
            else
            {
                byUid.Add(r.Uid, collapsed.Count);
                collapsed.Add(new CollapsedTestResult(r));
            }
        }

        return collapsed;
    }

    private readonly record struct CollapsedTestResult(CapturedTestResult Final)
    {
        public List<CapturedTestResult> PriorAttempts { get; } = [];

        // CTRF "flaky" is true iff the final status is "passed" AND at least one
        // previous attempt failed.
        public bool IsFlaky
        {
            get
            {
                if (Final.Status != "passed" || PriorAttempts.Count == 0)
                {
                    return false;
                }

                foreach (CapturedTestResult attempt in PriorAttempts)
                {
                    if (attempt.Status == "failed")
                    {
                        return true;
                    }
                }

                return false;
            }
        }
    }
}
