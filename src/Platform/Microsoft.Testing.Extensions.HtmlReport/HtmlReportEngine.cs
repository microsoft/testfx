// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.HtmlReport.Resources;
using Microsoft.Testing.Platform;
using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Configurations;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Extensions.TestFramework;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Messages;
using Microsoft.Testing.Platform.Services;

namespace Microsoft.Testing.Extensions.HtmlReport;

internal sealed class HtmlReportEngine
{
    // Cap individual fields to keep the report HTML/JSON payload usable even on huge runs.
    // Truncation is surfaced in the UI.
    internal const int MaxStandardStreamLength = 32 * 1024;
    internal const int MaxStackTraceLength = 32 * 1024;
    internal const int MaxMessageLength = 16 * 1024;

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

    public async Task<(string FileName, string? Warning)> GenerateReportAsync(TestNodeUpdateMessage[] testNodes)
    {
        _cancellationToken.ThrowIfCancellationRequested();

        DateTimeOffset finishTime = _clock.UtcNow;
        bool fileNameExplicitlyProvided = _commandLineOptions.TryGetOptionArgumentList(
            HtmlReportGeneratorCommandLine.HtmlReportFileNameOptionName,
            out string[]? providedFileName);

        string fileName = fileNameExplicitlyProvided
            ? providedFileName![0]
            : BuildDefaultFileName(finishTime);

        string outputDirectory = _configuration.GetTestResultDirectory();
        string finalPath = Path.Combine(outputDirectory, fileName);
        bool willOverwrite = fileNameExplicitlyProvided && _fileSystem.ExistFile(finalPath);

        string template = LoadTemplate();
        string json = BuildJson(testNodes, finishTime);

        string html = template
            .Replace(GeneratorVersionPlaceholder, ExtensionVersion.DefaultSemVer)
            .Replace(DataPlaceholder, json);

        byte[] bytes = Encoding.UTF8.GetBytes(html);

        // Note that we need to dispose the IFileStream, not the inner stream.
        // IFileStream implementations will be responsible to dispose their inner stream.
        using IFileStream stream = _fileSystem.NewFileStream(finalPath, fileNameExplicitlyProvided ? FileMode.Create : FileMode.CreateNew);
#if NETCOREAPP
        await stream.Stream.WriteAsync(bytes.AsMemory(), _cancellationToken).ConfigureAwait(false);
#else
        await stream.Stream.WriteAsync(bytes, 0, bytes.Length, _cancellationToken).ConfigureAwait(false);
#endif

        string? warning = willOverwrite
            ? string.Format(CultureInfo.InvariantCulture, ExtensionResources.HtmlReportFileExistsAndWillBeOverwritten, finalPath)
            : null;

        return (finalPath, warning);
    }

    private string BuildDefaultFileName(DateTimeOffset finishTime)
    {
        string user = _environment.GetEnvironmentVariable("UserName")
            ?? _environment.GetEnvironmentVariable("USER")
            ?? "user";
        string raw = $"{user}_{_environment.MachineName}_{finishTime:yyyy-MM-dd_HH_mm_ss}.html";
        return ReplaceInvalidFileNameChars(raw);
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

    private string BuildJson(TestNodeUpdateMessage[] testNodes, DateTimeOffset finishTime)
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
        Dictionary<string, int> countByUid = new(testNodes.Length);
        foreach (TestNodeUpdateMessage n in testNodes)
        {
            string uid = n.TestNode.Uid.Value;
            countByUid[uid] = countByUid.TryGetValue(uid, out int existing) ? existing + 1 : 1;
        }

        Dictionary<string, int> emittedByUid = new(countByUid.Count);

        var sb = new StringBuilder(8 * 1024);
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
        foreach (TestNodeUpdateMessage update in testNodes)
        {
            if (!first)
            {
                sb.Append(',');
            }

            first = false;

            TestNode node = update.TestNode;
            TestNodeStateProperty state = node.Properties.Single<TestNodeStateProperty>();
            string outcome = ClassifyOutcome(state, ref passed, ref failed, ref skipped, ref timedout, ref errored);

            TimingProperty? timing = node.Properties.SingleOrDefault<TimingProperty>();
            TimeSpan duration = timing?.GlobalTiming.Duration ?? TimeSpan.Zero;
            totalDuration += duration;

            (string? className, string? methodName) = GetClassAndMethodName(node);

            string? errorMessage = state.Explanation;
            string? stackTrace = null;
            string? exceptionType = null;
            Exception? exception = state switch
            {
                FailedTestNodeStateProperty f => f.Exception,
                ErrorTestNodeStateProperty e => e.Exception,
                TimeoutTestNodeStateProperty t => t.Exception,
                _ => null,
            };

            if (exception is not null)
            {
                errorMessage ??= exception.Message;
                stackTrace = exception.StackTrace;
                exceptionType = exception.GetType().FullName;
            }

            string? stdout = node.Properties.SingleOrDefault<StandardOutputProperty>()?.StandardOutput;
            string? stderr = node.Properties.SingleOrDefault<StandardErrorProperty>()?.StandardError;

            string uid = node.Uid.Value;
            int attemptOf = countByUid[uid];
            int attemptIndex = emittedByUid.TryGetValue(uid, out int alreadyEmitted) ? alreadyEmitted + 1 : 1;
            emittedByUid[uid] = attemptIndex;

            sb.Append('{');
            AppendStringPair(sb, "uid", uid);
            sb.Append(',');
            AppendStringPair(sb, "displayName", node.DisplayName);
            sb.Append(',');
            AppendStringPair(sb, "outcome", outcome);
            sb.Append(',');
            AppendNumberPair(sb, "durationMs", duration.TotalMilliseconds.ToString("F3", CultureInfo.InvariantCulture));

            if (attemptOf > 1)
            {
                sb.Append(',');
                AppendNumberPair(sb, "attemptIndex", attemptIndex.ToString(CultureInfo.InvariantCulture));
                sb.Append(',');
                AppendNumberPair(sb, "attemptOf", attemptOf.ToString(CultureInfo.InvariantCulture));
            }

            if (timing is not null)
            {
                sb.Append(',');
                AppendStringPair(sb, "startTime", timing.GlobalTiming.StartTime.ToString("O", CultureInfo.InvariantCulture));
                sb.Append(',');
                AppendStringPair(sb, "endTime", timing.GlobalTiming.EndTime.ToString("O", CultureInfo.InvariantCulture));
            }

            if (className is not null)
            {
                sb.Append(',');
                AppendStringPair(sb, "className", className);
            }

            if (methodName is not null)
            {
                sb.Append(',');
                AppendStringPair(sb, "methodName", methodName);
            }

            // Traits / categories: every TestMetadataProperty becomes a {key, value} entry.
            // VSTest categories appear as TestMetadataProperty(category, "") and arbitrary
            // traits appear as TestMetadataProperty(key, value).
            bool firstTrait = true;
            foreach (TestMetadataProperty meta in node.Properties.OfType<TestMetadataProperty>())
            {
                if (firstTrait)
                {
                    sb.Append(',');
                    AppendKey(sb, "traits");
                    sb.Append('[');
                    firstTrait = false;
                }
                else
                {
                    sb.Append(',');
                }

                sb.Append('{');
                AppendStringPair(sb, "key", meta.Key);
                sb.Append(',');
                AppendStringPair(sb, "value", meta.Value);
                sb.Append('}');
            }

            if (!firstTrait)
            {
                sb.Append(']');
            }

            if (errorMessage is not null)
            {
                sb.Append(',');
                AppendTruncatedStringPair(sb, "errorMessage", errorMessage, MaxMessageLength);
            }

            if (exceptionType is not null)
            {
                sb.Append(',');
                AppendStringPair(sb, "exceptionType", exceptionType);
            }

            if (stackTrace is not null)
            {
                sb.Append(',');
                AppendTruncatedStringPair(sb, "stackTrace", stackTrace, MaxStackTraceLength);
            }

            if (stdout is not null)
            {
                sb.Append(',');
                AppendTruncatedStringPair(sb, "standardOutput", stdout, MaxStandardStreamLength);
            }

            if (stderr is not null)
            {
                sb.Append(',');
                AppendTruncatedStringPair(sb, "standardError", stderr, MaxStandardStreamLength);
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

    private static string ClassifyOutcome(
        TestNodeStateProperty state,
        ref int passed,
        ref int failed,
        ref int skipped,
        ref int timedout,
        ref int errored)
    {
        switch (state)
        {
            case PassedTestNodeStateProperty:
                passed++;
                return "passed";
            case SkippedTestNodeStateProperty:
                skipped++;
                return "skipped";
            case TimeoutTestNodeStateProperty:
                timedout++;
                return "timedOut";
            case ErrorTestNodeStateProperty:
                errored++;
                return "errored";
            case FailedTestNodeStateProperty:
                failed++;
                return "failed";
            default:
                if (Array.IndexOf(TestNodePropertiesCategories.WellKnownTestNodeTestRunOutcomeFailedProperties, state.GetType()) >= 0)
                {
                    failed++;
                    return "failed";
                }

                throw ApplicationStateGuard.Unreachable();
        }
    }

    private static (string? ClassName, string? MethodName) GetClassAndMethodName(TestNode node)
    {
        TestMethodIdentifierProperty? identifier = node.Properties.SingleOrDefault<TestMethodIdentifierProperty>();
        if (identifier is null)
        {
            return (null, null);
        }

        string className = RoslynString.IsNullOrEmpty(identifier.Namespace)
            ? identifier.TypeName
            : $"{identifier.Namespace}.{identifier.TypeName}";

        return (className, identifier.MethodName);
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

    private static void AppendTruncatedStringPair(StringBuilder sb, string key, string value, int maxLength)
    {
        AppendKey(sb, key);
        if (value.Length <= maxLength)
        {
            AppendString(sb, value);
        }
        else
        {
            string truncated = value.Substring(0, maxLength) + $"\n…[truncated, original length: {value.Length}]";
            AppendString(sb, truncated);
        }
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
