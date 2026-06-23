// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.HtmlReport.Resources;
using Microsoft.Testing.Platform;
using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Configurations;
using Microsoft.Testing.Platform.Extensions.TestFramework;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Services;

namespace Microsoft.Testing.Extensions.HtmlReport;

internal sealed class HtmlReportEngine : ReportEngineBase
{
    private const string TemplateResourceName = "Microsoft.Testing.Extensions.HtmlReport.Templates.report-template.html";
    private const string DataPlaceholder = "/*__MTP_DATA__*/null";
    private const string GeneratorVersionPlaceholder = "__MTP_GENERATOR_VERSION__";

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
        : base(
            fileSystem,
            testApplicationModuleInfo,
            environment,
            commandLineOptions,
            configuration,
            clock,
            testFramework,
            testStartTime,
            exitCode,
            cancellationToken)
    {
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
            ? ResolveReportFileName(GetProvidedFileName(providedFileName))
            : BuildDefaultFileName("html");

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

        string template = LoadTemplate();
        string json = BuildJson(results, finishTime);

        string html = template
            .Replace(GeneratorVersionPlaceholder, ExtensionVersion.DefaultSemVer)
            .Replace(DataPlaceholder, json);

        byte[] bytes = Encoding.UTF8.GetBytes(html);

        return await WriteAsync(finalPath, bytes).ConfigureAwait(false);
    }

    private async Task<(string FileName, string? Warning)> WriteAsync(string finalPath, byte[] bytes)
    {
        // Always overwrite (FileMode.Create), regardless of whether the file name was explicitly
        // provided or generated from the default <asm>_<tfm>_<arch>.html shape. Emit a warning
        // when overwriting so users have a single, predictable rule to reason about.
        bool willOverwrite = _fileSystem.ExistFile(finalPath);
        await WriteFileAsync(finalPath, bytes).ConfigureAwait(false);
        return (
            finalPath,
            willOverwrite
                ? string.Format(CultureInfo.InvariantCulture, ExtensionResources.HtmlReportFileExistsAndWillBeOverwritten, finalPath)
                : null);
    }

    private async Task WriteFileAsync(string path, byte[] bytes)
    {
        // Note that we need to dispose the IFileStream, not the inner stream.
        // IFileStream implementations will be responsible to dispose their inner stream.
        using IFileStream stream = _fileSystem.NewFileStream(path, FileMode.Create);
#if NETCOREAPP
        await stream.Stream.WriteAsync(bytes.AsMemory(), _cancellationToken).ConfigureAwait(false);
#else
        await stream.Stream.WriteAsync(bytes, 0, bytes.Length, _cancellationToken).ConfigureAwait(false);
#endif
    }

#pragma warning disable IDE0051 // Accessed by unit tests through reflection.
    private static string ReplaceInvalidFileNameChars(string fileName)
        => ReportFileNameSanitizer.ReplaceInvalidFileNameChars(fileName);
#pragma warning restore IDE0051

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
