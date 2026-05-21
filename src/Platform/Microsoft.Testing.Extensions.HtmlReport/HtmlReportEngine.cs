// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.HtmlReport.Resources;
using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Configurations;
using Microsoft.Testing.Platform.Extensions.TestFramework;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.OutputDevice;
using Microsoft.Testing.Platform.Services;

namespace Microsoft.Testing.Extensions.HtmlReport;

internal sealed class HtmlReportEngine
{
    private const string TemplateResourceName = "Microsoft.Testing.Extensions.HtmlReport.Templates.report-template.html";
    private const string DataPlaceholder = "/*__MTP_DATA__*/null";
    private const string GeneratorVersionPlaceholder = "__MTP_GENERATOR_VERSION__";

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

    public HtmlReportEngine(
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
            HtmlReportGeneratorCommandLine.HtmlReportFileNameOptionName,
            out string[]? providedFileName);

        string fileName = fileNameExplicitlyProvided
            ? providedFileName![0]
            : BuildDefaultFileName(finishTime);

        string outputDirectory = _configuration.GetTestResultDirectory();
        string finalPath = Path.Combine(outputDirectory, fileName);

        string template = LoadTemplate();
        string json = BuildJson(results, finishTime);

        string html = template
            .Replace(GeneratorVersionPlaceholder, ExtensionVersion.DefaultSemVer)
            .Replace(DataPlaceholder, json);

        byte[] bytes = Encoding.UTF8.GetBytes(html);

        return await WriteWithRetryAsync(finalPath, bytes, fileNameExplicitlyProvided).ConfigureAwait(false);
    }

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
                    ? string.Format(CultureInfo.InvariantCulture, ExtensionResources.HtmlReportFileExistsAndWillBeOverwritten, finalPath)
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
        string raw = $"{user}_{_environment.MachineName}_{moduleName}_{targetFrameworkMoniker}_{finishTime:yyyy-MM-dd_HH_mm_ss}.html";
        return ReplaceInvalidFileNameChars(raw);
    }

    private static string GetTargetFrameworkMoniker()
    {
        string? entryAssemblyTargetFramework = TargetFrameworkParser.GetShortTargetFramework(
            Assembly.GetEntryAssembly()?.GetCustomAttribute<TargetFrameworkAttribute>()?.FrameworkDisplayName);
        string? runtimeTargetFramework = TargetFrameworkParser.GetShortTargetFramework(RuntimeInformation.FrameworkDescription);

        return entryAssemblyTargetFramework ?? runtimeTargetFramework ?? "unknown";
    }

    private static string ReplaceInvalidFileNameChars(string fileName)
    {
        var sb = new StringBuilder(fileName.Length);
        char[] invalid = Path.GetInvalidFileNameChars();
        foreach (char c in fileName)
        {
            sb.Append(Array.IndexOf(invalid, c) >= 0 ? '_' : c);
        }

        return sb.ToString();
    }

    private static string LoadTemplate()
    {
        Assembly assembly = typeof(HtmlReportEngine).Assembly;
        using Stream stream = assembly.GetManifestResourceStream(TemplateResourceName)
            ?? throw ApplicationStateGuard.Unreachable();
        using var reader = new StreamReader(stream, Encoding.UTF8);
        return reader.ReadToEnd();
    }

    private string BuildJson(CapturedTestResult[] results, DateTimeOffset finishTime)
    {
        int passed = 0;
        int failed = 0;
        int skipped = 0;
        int timedout = 0;
        int errored = 0;
        TimeSpan totalDuration = TimeSpan.Zero;

        // First pass: count how many entries each UID is going to produce so we can
        // annotate rows that share a UID with "attemptIndex"/"attemptOf". This lets the
        // UI surface frameworks that emit multiple terminal results for the same UID
        // (parameterized rows, in-process retries, broken UID generators, etc.) without
        // silently dropping any data.
        Dictionary<string, int> countByUid = [];
        foreach (CapturedTestResult r in results)
        {
            countByUid[r.Uid] = countByUid.TryGetValue(r.Uid, out int existing) ? existing + 1 : 1;
        }

        Dictionary<string, int> emittedByUid = [];

        StringBuilder sb = new(8 * 1024);
        sb.Append('{');
        AppendStringPair(sb, "schemaVersion", "1");
        sb.Append(',');
        AppendStringPair(sb, "generator", "Microsoft.Testing.Extensions.HtmlReport");
        sb.Append(',');
        AppendStringPair(sb, "generatorVersion", ExtensionVersion.DefaultSemVer);
        sb.Append(',');
        AppendStringPair(sb, "testApplication", _testApplicationModuleInfo.GetCurrentTestApplicationFullPath());
        sb.Append(',');
        AppendStringPair(sb, "machineName", _environment.MachineName);
        sb.Append(',');
        AppendStringPair(sb, "userName", _environment.GetEnvironmentVariable("UserName") ?? _environment.GetEnvironmentVariable("USER") ?? string.Empty);
        sb.Append(',');
        AppendStringPair(sb, "framework", _testFramework.DisplayName);
        sb.Append(',');
        AppendStringPair(sb, "frameworkUid", _testFramework.Uid);
        sb.Append(',');
        AppendStringPair(sb, "frameworkVersion", _testFramework.Version);
        sb.Append(',');
        AppendStringPair(sb, "startTime", _testStartTime.ToString("O", CultureInfo.InvariantCulture));
        sb.Append(',');
        AppendStringPair(sb, "endTime", finishTime.ToString("O", CultureInfo.InvariantCulture));
        sb.Append(',');
        AppendNumberPair(sb, "exitCode", _exitCode.ToString(CultureInfo.InvariantCulture));
        sb.Append(',');
        AppendKey(sb, "tests");
        sb.Append('[');

        bool first = true;
        for (int i = 0; i < results.Length; i++)
        {
            CapturedTestResult r = results[i];
            if (!first)
            {
                sb.Append(',');
            }

            first = false;

            CountOutcome(r.Outcome, ref passed, ref failed, ref skipped, ref timedout, ref errored);
            totalDuration += r.Duration;

            int attemptOf = countByUid[r.Uid];
            int attemptIndex = emittedByUid.TryGetValue(r.Uid, out int alreadyEmitted) ? alreadyEmitted + 1 : 1;
            emittedByUid[r.Uid] = attemptIndex;

            sb.Append('{');
            AppendNumberPair(sb, "rowKey", i.ToString(CultureInfo.InvariantCulture));
            sb.Append(',');
            AppendStringPair(sb, "uid", r.Uid);
            sb.Append(',');
            AppendStringPair(sb, "displayName", r.DisplayName);
            sb.Append(',');
            AppendStringPair(sb, "outcome", r.Outcome);
            sb.Append(',');
            AppendNumberPair(sb, "durationMs", r.Duration.TotalMilliseconds.ToString("F3", CultureInfo.InvariantCulture));

            if (attemptOf > 1)
            {
                sb.Append(',');
                AppendNumberPair(sb, "attemptIndex", attemptIndex.ToString(CultureInfo.InvariantCulture));
                sb.Append(',');
                AppendNumberPair(sb, "attemptOf", attemptOf.ToString(CultureInfo.InvariantCulture));
            }

            if (r.StartTime is { } startTime)
            {
                sb.Append(',');
                AppendStringPair(sb, "startTime", startTime.ToString("O", CultureInfo.InvariantCulture));
            }

            if (r.EndTime is { } endTime)
            {
                sb.Append(',');
                AppendStringPair(sb, "endTime", endTime.ToString("O", CultureInfo.InvariantCulture));
            }

            if (r.ClassName is not null)
            {
                sb.Append(',');
                AppendStringPair(sb, "className", r.ClassName);
            }

            if (r.MethodName is not null)
            {
                sb.Append(',');
                AppendStringPair(sb, "methodName", r.MethodName);
            }

            if (r.Traits is { Count: > 0 })
            {
                sb.Append(',');
                AppendKey(sb, "traits");
                sb.Append('[');
                for (int t = 0; t < r.Traits.Count; t++)
                {
                    if (t > 0)
                    {
                        sb.Append(',');
                    }

                    KeyValuePair<string, string> trait = r.Traits[t];
                    sb.Append('{');
                    AppendStringPair(sb, "key", trait.Key);
                    sb.Append(',');
                    AppendStringPair(sb, "value", trait.Value);
                    sb.Append('}');
                }

                sb.Append(']');
            }

            if (r.ErrorMessage is not null)
            {
                sb.Append(',');
                AppendStringPair(sb, "errorMessage", r.ErrorMessage);
            }

            if (r.ExceptionType is not null)
            {
                sb.Append(',');
                AppendStringPair(sb, "exceptionType", r.ExceptionType);
            }

            if (r.StackTrace is not null)
            {
                sb.Append(',');
                AppendStringPair(sb, "stackTrace", r.StackTrace);
            }

            if (r.StandardOutput is not null)
            {
                sb.Append(',');
                AppendStringPair(sb, "standardOutput", r.StandardOutput);
            }

            if (r.StandardError is not null)
            {
                sb.Append(',');
                AppendStringPair(sb, "standardError", r.StandardError);
            }

            sb.Append('}');
        }

        sb.Append("],");

        AppendKey(sb, "summary");
        sb.Append('{');
        AppendNumberPair(sb, "total", (passed + failed + skipped + timedout + errored).ToString(CultureInfo.InvariantCulture));
        sb.Append(',');
        AppendNumberPair(sb, "passed", passed.ToString(CultureInfo.InvariantCulture));
        sb.Append(',');
        AppendNumberPair(sb, "failed", failed.ToString(CultureInfo.InvariantCulture));
        sb.Append(',');
        AppendNumberPair(sb, "skipped", skipped.ToString(CultureInfo.InvariantCulture));
        sb.Append(',');
        AppendNumberPair(sb, "timedOut", timedout.ToString(CultureInfo.InvariantCulture));
        sb.Append(',');
        AppendNumberPair(sb, "errored", errored.ToString(CultureInfo.InvariantCulture));
        sb.Append(',');
        AppendNumberPair(sb, "totalDurationMs", totalDuration.TotalMilliseconds.ToString("F3", CultureInfo.InvariantCulture));
        sb.Append('}');

        sb.Append('}');

        return sb.ToString();
    }

    private static void CountOutcome(string outcome, ref int passed, ref int failed, ref int skipped, ref int timedout, ref int errored)
    {
        switch (outcome)
        {
            case "passed": passed++; break;
            case "failed": failed++; break;
            case "skipped": skipped++; break;
            case "timedOut": timedout++; break;
            case "errored": errored++; break;
            default: throw ApplicationStateGuard.Unreachable();
        }
    }

    // ------------------------------------------------------------------
    // Minimal HTML-safe JSON writer.
    // We can't use System.Text.Json because the extension targets
    // netstandard2.0 (where it isn't part of the platform's reference set).
    // The output is a plain JSON document but every character that could
    // close the embedding <script type="application/json"> element or
    // confuse line-based parsers is escaped to its \\uXXXX form. This makes
    // it safe to inline in HTML and resilient against any test-controlled
    // content (display names, stack traces, stdout/stderr).
    // ------------------------------------------------------------------
    private static void AppendKey(StringBuilder sb, string key)
    {
        AppendString(sb, key);
        sb.Append(':');
    }

    private static void AppendStringPair(StringBuilder sb, string key, string value)
    {
        AppendKey(sb, key);
        AppendString(sb, value);
    }

    private static void AppendNumberPair(StringBuilder sb, string key, string number)
    {
        AppendKey(sb, key);
        sb.Append(number);
    }

    private static void AppendString(StringBuilder sb, string value)
    {
        sb.Append('"');
        foreach (char c in value)
        {
            switch (c)
            {
                case '"':
                    sb.Append("\\\"");
                    break;
                case '\\':
                    sb.Append("\\\\");
                    break;
                case '\b':
                    sb.Append("\\b");
                    break;
                case '\f':
                    sb.Append("\\f");
                    break;
                case '\n':
                    sb.Append("\\n");
                    break;
                case '\r':
                    sb.Append("\\r");
                    break;
                case '\t':
                    sb.Append("\\t");
                    break;
                case '<':
                case '>':
                case '&':
                case '\'':
                case '\u2028':
                case '\u2029':
                    AppendUnicodeEscape(sb, c);
                    break;
                default:
                    if (c < 0x20)
                    {
                        AppendUnicodeEscape(sb, c);
                    }
                    else
                    {
                        sb.Append(c);
                    }

                    break;
            }
        }

        sb.Append('"');
    }

    private static void AppendUnicodeEscape(StringBuilder sb, char c)
    {
        sb.Append("\\u");
        sb.Append(((int)c).ToString("X4", CultureInfo.InvariantCulture));
    }
}
