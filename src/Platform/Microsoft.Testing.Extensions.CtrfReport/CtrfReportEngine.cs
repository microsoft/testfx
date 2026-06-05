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
                if (_clock.UtcNow - firstTry > TimeSpan.FromSeconds(5))
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
        // Aggregate summary counts.
        int passed = 0;
        int failed = 0;
        int skipped = 0;
        int pending = 0;
        int other = 0;
        foreach (CapturedTestResult r in results)
        {
            switch (r.Status)
            {
                case "passed": passed++; break;
                case "failed": failed++; break;
                case "skipped": skipped++; break;
                case "pending": pending++; break;
                default: other++; break;
            }
        }

        long startMs = _testStartTime.ToUnixTimeMilliseconds();
        long stopMs = finishTime.ToUnixTimeMilliseconds();
        long durationMs = Math.Max(0, stopMs - startMs);

        using var ms = new MemoryStream(capacity: 8 * 1024);
        var writerOptions = new JsonWriterOptions
        {
            Indented = true,
            // CTRF documents are intended to be embedded in JSON/JS contexts.
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        };

        using (var writer = new Utf8JsonWriter(ms, writerOptions))
        {
            writer.WriteStartObject();

            writer.WriteString("reportFormat", CtrfReportFormat);
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
            writer.WriteString("name", _testFramework.DisplayName);
            writer.WriteString("version", _testFramework.Version);
            writer.WritePropertyName("extra");
            writer.WriteStartObject();
            writer.WriteString("uid", _testFramework.Uid);
            writer.WriteEndObject();
            writer.WriteEndObject();

            // results.summary
            writer.WritePropertyName("summary");
            writer.WriteStartObject();
            writer.WriteNumber("tests", results.Length);
            writer.WriteNumber("passed", passed);
            writer.WriteNumber("failed", failed);
            writer.WriteNumber("skipped", skipped);
            writer.WriteNumber("pending", pending);
            writer.WriteNumber("other", other);
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
            writer.WriteString(
                "extra",
                string.Format(CultureInfo.InvariantCulture, "user={0};machine={1};exitCode={2}", user, _environment.MachineName, _exitCode));
            writer.WriteString("osPlatform", RuntimeInformation.OSDescription);
            writer.WriteString("testEnvironment", _testApplicationModuleInfo.GetCurrentTestApplicationFullPath());
            writer.WriteEndObject();

            // results.tests
            writer.WritePropertyName("tests");
            writer.WriteStartArray();

            // First pass: count attempts per UID so we can annotate rows that share a UID
            // with retry/attempt metadata in `extra`. Multiple terminal results per UID
            // are intentional (parameterized rows, in-process retries) and must not be
            // dropped.
            Dictionary<string, int> countByUid = [];
            foreach (CapturedTestResult r in results)
            {
                countByUid[r.Uid] = countByUid.TryGetValue(r.Uid, out int existing) ? existing + 1 : 1;
            }

            Dictionary<string, int> emittedByUid = [];

            foreach (CapturedTestResult r in results)
            {
                int attemptOf = countByUid[r.Uid];
                int attemptIndex = emittedByUid.TryGetValue(r.Uid, out int alreadyEmitted) ? alreadyEmitted + 1 : 1;
                emittedByUid[r.Uid] = attemptIndex;

                writer.WriteStartObject();

                writer.WriteString("name", r.DisplayName);
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

                if (attemptOf > 1)
                {
                    writer.WriteNumber("retries", attemptOf - 1);
                }

                if (r.StandardOutput is not null)
                {
                    writer.WritePropertyName("stdout");
                    writer.WriteStartArray();
                    writer.WriteStringValue(r.StandardOutput);
                    writer.WriteEndArray();
                }

                if (r.StandardError is not null)
                {
                    writer.WritePropertyName("stderr");
                    writer.WriteStartArray();
                    writer.WriteStringValue(r.StandardError);
                    writer.WriteEndArray();
                }

                if (r.Traits is { Count: > 0 } || r.MethodName is not null
                    || r.ExceptionType is not null || attemptOf > 1)
                {
                    writer.WritePropertyName("labels");
                    writer.WriteStartObject();
                    if (r.Traits is { Count: > 0 })
                    {
                        foreach (KeyValuePair<string, string> trait in r.Traits)
                        {
                            writer.WriteString(trait.Key, trait.Value);
                        }
                    }

                    if (r.MethodName is not null)
                    {
                        writer.WriteString("method", r.MethodName);
                    }

                    if (r.ExceptionType is not null)
                    {
                        writer.WriteString("exceptionType", r.ExceptionType);
                    }

                    if (attemptOf > 1)
                    {
                        writer.WriteString("attemptIndex", attemptIndex.ToString(CultureInfo.InvariantCulture));
                        writer.WriteString("attemptOf", attemptOf.ToString(CultureInfo.InvariantCulture));
                    }

                    writer.WriteEndObject();
                }

                // CTRF `extra` (free-form) for the test UID — the CTRF spec doesn't
                // define a dedicated stable identifier so we surface the MTP UID here
                // for cross-tool correlation.
                writer.WritePropertyName("extra");
                writer.WriteStartObject();
                writer.WriteString("uid", r.Uid);
                writer.WriteEndObject();

                writer.WriteEndObject();
            }

            writer.WriteEndArray();
            writer.WriteEndObject();

            writer.WriteEndObject();
        }

        return ms.ToArray();
    }
}
