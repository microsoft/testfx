// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.GitHubActionsReport.Resources;
using Microsoft.Testing.Platform;
using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Extensions.OutputDevice;
using Microsoft.Testing.Platform.Extensions.TestHost;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Logging;
using Microsoft.Testing.Platform.OutputDevice;
using Microsoft.Testing.Platform.Services;

namespace Microsoft.Testing.Extensions.GitHubActionsReport;

/// <summary>
/// Writes a markdown roll-up of the test run (totals, failures, slowest tests) to the file pointed to by
/// the <c>GITHUB_STEP_SUMMARY</c> environment variable. GitHub renders that file on the workflow run's
/// summary page. See
/// <see href="https://docs.github.com/en/actions/using-workflows/workflow-commands-for-github-actions#adding-a-job-summary"/>.
/// </summary>
internal sealed class GitHubActionsSummaryReporter :
    IDataConsumer,
    ITestSessionLifetimeHandler,
    IOutputDeviceDataProducer
{
    private const string StepSummaryEnvironmentVariable = "GITHUB_STEP_SUMMARY";
    private const int MaxFailures = 20;
    private const int MaxSlowestTests = 10;

    // GITHUB_STEP_SUMMARY is a single shared file that every test-host process appends to. Under a
    // concurrent multi-assembly `dotnet test` run, contention is resolved by an exclusive-append retry loop
    // (see AppendStepSummaryWithRetryAsync). Twenty attempts at 50 ms bound the wait to ~1s, which is ample
    // to serialize the tiny per-assembly writes while still failing fast (into a best-effort warning) on a
    // genuinely unwritable path.
    private const int StepSummaryMaxWriteAttempts = 20;
    private static readonly TimeSpan StepSummaryRetryDelay = TimeSpan.FromMilliseconds(50);

    private readonly IEnvironment _environment;
    private readonly IFileSystem _fileSystem;
    private readonly IOutputDevice _outputDevice;
    private readonly ITestApplicationModuleInfo _testApplicationModuleInfo;
    private readonly ITestApplicationProcessExitCode _testApplicationProcessExitCode;
    private readonly ILogger _logger;
    private readonly Lazy<string> _targetFrameworkMoniker;
    private readonly bool _isEnabled;

#if NET9_0_OR_GREATER
    private readonly System.Threading.Lock _stateLock = new();
#else
    private readonly object _stateLock = new();
#endif
#pragma warning disable IDE0028 // Collection initialization can be simplified - target-typed `new` cannot pass the comparer in the same syntactic form expected.
    private readonly Dictionary<string, TestRecord> _records = new Dictionary<string, TestRecord>(StringComparer.Ordinal);
#pragma warning restore IDE0028

    public GitHubActionsSummaryReporter(
        ICommandLineOptions commandLineOptions,
        IEnvironment environment,
        IFileSystem fileSystem,
        IOutputDevice outputDevice,
        ITestApplicationModuleInfo testApplicationModuleInfo,
        ITestApplicationProcessExitCode testApplicationProcessExitCode,
        ILoggerFactory loggerFactory)
    {
        _environment = environment;
        _fileSystem = fileSystem;
        _outputDevice = outputDevice;
        _testApplicationModuleInfo = testApplicationModuleInfo;
        _testApplicationProcessExitCode = testApplicationProcessExitCode;
        _logger = loggerFactory.CreateLogger<GitHubActionsSummaryReporter>();
        _targetFrameworkMoniker = new(TargetFrameworkMonikerHelper.GetTargetFrameworkMonikerIncludingPlatform);
        _isEnabled = GitHubActionsFeature.IsEnabled(commandLineOptions, environment, GitHubActionsCommandLineOptions.GitHubActionsStepSummary);
    }

    public Type[] DataTypesConsumed { get; } = [typeof(TestNodeUpdateMessage)];

    public string Uid => nameof(GitHubActionsSummaryReporter);

    public string Version => ExtensionVersion.DefaultSemVer;

    public string DisplayName => GitHubActionsResources.DisplayName;

    public string Description => GitHubActionsResources.Description;

    public Task<bool> IsEnabledAsync() => Task.FromResult(_isEnabled);

    public Task OnTestSessionStartingAsync(ITestSessionContext testSessionContext)
    {
        lock (_stateLock)
        {
            _records.Clear();
        }

        return Task.CompletedTask;
    }

    public Task ConsumeAsync(IDataProducer dataProducer, IData value, CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!_isEnabled || value is not TestNodeUpdateMessage update)
            {
                return Task.CompletedTask;
            }

            TestNodeStateProperty? state = update.TestNode.Properties.FirstOrDefault<TestNodeStateProperty>();
            TerminalKind kind = SummaryReporterHelpers.GetTerminalKind(state);
            if (kind == TerminalKind.NotTerminal)
            {
                return Task.CompletedTask;
            }

            string uid = update.TestNode.Uid;
            string displayName = update.TestNode.DisplayName;

            // Resolve the stable, fully-qualified name the same way the annotation and slow-test reporters do
            // (preferring TestMethodIdentifierProperty) so a given test renders identically across all three surfaces.
            string fullyQualifiedName = TestNodeIdentity.GetTestName(update.TestNode);

            TimingProperty? timing = null;
            PropertyBag.PropertyBagEnumerator enumerator = update.TestNode.Properties.GetStructEnumerator();
            while (enumerator.MoveNext())
            {
                if (enumerator.Current is TimingProperty t)
                {
                    timing = t;
                    break;
                }
            }

            TimeSpan duration = timing?.GlobalTiming.Duration ?? TimeSpan.Zero;

            lock (_stateLock)
            {
                _records[uid] = new TestRecord(displayName, fullyQualifiedName, kind, duration);
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogUnexpectedException(nameof(ConsumeAsync), ex);
        }

        return Task.CompletedTask;
    }

    public async Task OnTestSessionFinishingAsync(ITestSessionContext testSessionContext)
    {
        try
        {
            testSessionContext.CancellationToken.ThrowIfCancellationRequested();

            if (!_isEnabled)
            {
                return;
            }

            string? path = _environment.GetEnvironmentVariable(StepSummaryEnvironmentVariable);
            if (RoslynString.IsNullOrWhiteSpace(path))
            {
                // Outside a GitHub Actions step (or when summaries are unsupported) there is nowhere to
                // write. Stay quiet apart from a low-noise trace so local/dev runs don't get a warning.
                if (_logger.IsEnabled(LogLevel.Trace))
                {
                    _logger.LogTrace($"'{StepSummaryEnvironmentVariable}' is not set; skipping job summary.");
                }

                return;
            }

            List<TestRecord> snapshot;
            lock (_stateLock)
            {
                snapshot = [.. _records.Values];
            }

            string markdown = BuildMarkdown(snapshot, _testApplicationModuleInfo.TryGetAssemblyName() ?? "unknown assembly name", _targetFrameworkMoniker.Value, _testApplicationProcessExitCode.GetProcessExitCode());

            try
            {
                await AppendStepSummaryWithRetryAsync(_fileSystem, path!, markdown, StepSummaryMaxWriteAttempts, StepSummaryRetryDelay, testSessionContext.CancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                string warning = string.Format(CultureInfo.InvariantCulture, GitHubActionsResources.StepSummaryWriteFailedWarning, path, ex.Message);
                if (_logger.IsEnabled(LogLevel.Warning))
                {
                    _logger.LogWarning(warning);
                }

                await _outputDevice.DisplayAsync(this, new WarningMessageOutputDeviceData(warning), testSessionContext.CancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogUnexpectedException(nameof(OnTestSessionFinishingAsync), ex);
        }
    }

    /// <summary>
    /// Appends <paramref name="content"/> to the shared <c>GITHUB_STEP_SUMMARY</c> file in a way that is safe
    /// when multiple test-host processes (one per assembly / target framework in a <c>dotnet test</c> run) write
    /// concurrently.
    /// </summary>
    /// <remarks>
    /// <see cref="FileMode.Append"/> only seeks to the end of the file once, at open time, and performs no
    /// atomic OS-level append. Opening with <see cref="FileShare.ReadWrite"/> would therefore let two processes
    /// position at the same offset and interleave or overwrite each other's section. We instead open with
    /// <see cref="FileShare.Read"/> — which denies other writers — so at most one process appends at a time, and
    /// retry on the resulting sharing violation (an <see cref="IOException"/>) until the holder releases the file.
    /// Each write is a single small section, so contention clears almost immediately; the bounded attempt count
    /// still lets a genuinely unlockable file surface as the caller's best-effort warning rather than looping
    /// forever.
    /// <para>
    /// Retries are scoped to <em>acquiring</em> the exclusive append handle only. Once the handle is acquired the
    /// process appends alone, so contention can no longer occur; a failure that happens <em>during</em> the write
    /// (e.g. disk full) may already have appended a partial section, and retrying would re-append the full section
    /// on top of it and corrupt the summary. Such a mid-write failure is therefore propagated straight to the
    /// caller's best-effort warning path instead of being retried.
    /// </para>
    /// </remarks>
    internal static /* for testing */ async Task AppendStepSummaryWithRetryAsync(
        IFileSystem fileSystem,
        string path,
        string content,
        int maxAttempts,
        TimeSpan retryDelay,
        CancellationToken cancellationToken)
    {
        for (int attempt = 1; ; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            IFileStream stream;
            try
            {
                stream = fileSystem.NewFileStream(path, FileMode.Append, FileAccess.Write, FileShare.Read);
            }
            catch (IOException) when (attempt < maxAttempts)
            {
                // Another test-host process currently holds the summary file open for writing. Back off briefly
                // and retry so this assembly's section is appended intact once the holder releases the file.
                await Task.Delay(retryDelay, cancellationToken).ConfigureAwait(false);
                continue;
            }

            // The exclusive append handle is acquired: from here on we append alone, so any failure is a genuine
            // write error (not contention) and must not be retried — a partial append followed by a full re-append
            // would corrupt the summary. Let it propagate to the caller's best-effort warning path.
            using (stream)
            using (var writer = new StreamWriter(stream.Stream, new UTF8Encoding(false)))
            {
                await writer.WriteAsync(content).ConfigureAwait(false);
            }

            return;
        }
    }

    internal static /* for testing */ string BuildMarkdown(IReadOnlyList<TestRecord> records, string assemblyName, string targetFrameworkMoniker, int exitCode)
    {
        int total = records.Count;
        int passed = 0;
        int failed = 0;
        int skipped = 0;
        TimeSpan totalDuration = TimeSpan.Zero;
        var failures = new List<TestRecord>();

        foreach (TestRecord record in records)
        {
            totalDuration += record.Duration;
            switch (record.Kind)
            {
                case TerminalKind.Passed:
                    passed++;
                    break;
                case TerminalKind.Failed:
                    failed++;
                    if (failures.Count < MaxFailures)
                    {
                        failures.Add(record);
                    }

                    break;
                case TerminalKind.Skipped:
                    skipped++;
                    break;
            }
        }

        // Reflect the process verdict, not just the failed-test count: a run can end in failure with zero failed
        // tests (e.g. zero tests discovered or a --minimum-expected-tests violation), which must not show ✅.
        bool runFailed = failed > 0 || GitHubActionsExitCode.IndicatesFailure(exitCode);
        string statusIcon = runFailed ? "❌" : "✅";

        var builder = new StringBuilder();
        builder.Append("## ").Append(statusIcon).Append(" Test Run Summary — ").Append(assemblyName).Append(" (").Append(targetFrameworkMoniker).Append(")\n\n");
        builder.Append("| Total | Passed | Failed | Skipped | Duration |\n");
        builder.Append("|---:|---:|---:|---:|---:|\n");
        builder.Append("| ").Append(total.ToString(CultureInfo.InvariantCulture))
            .Append(" | ").Append(passed.ToString(CultureInfo.InvariantCulture))
            .Append(" | ").Append(failed.ToString(CultureInfo.InvariantCulture))
            .Append(" | ").Append(skipped.ToString(CultureInfo.InvariantCulture))
            .Append(" | ").Append(FormatDuration(totalDuration)).Append(" |\n\n");

        // Surface a non-test-result failure that this reporter can observe once the session has finished
        // (zero tests, --minimum-expected-tests, --maximum-failed-tests, test-adapter session failure) as a
        // GitHub alert callout. Plain pass / at-least-one-failed outcomes are already conveyed by the totals
        // table and the failures section, so no callout is added for them.
        if (!GitHubActionsExitCode.IsTestResultOutcome(exitCode))
        {
            string calloutText = string.Format(
                CultureInfo.InvariantCulture,
                GitHubActionsResources.ExitCodeCallout,
                exitCode.ToString(CultureInfo.InvariantCulture),
                GitHubActionsExitCode.GetName(exitCode),
                GitHubActionsExitCode.GetReason(exitCode));
            builder.Append("> [!WARNING]\n> ").Append(EscapeInlineCode(calloutText)).Append("\n\n");
        }

        if (failures.Count > 0)
        {
            builder.Append("### ❌ Failures (").Append(failed.ToString(CultureInfo.InvariantCulture)).Append(")\n\n");
            foreach (TestRecord failure in failures)
            {
                builder.Append("- `").Append(EscapeInlineCode(failure.FullyQualifiedName)).Append("`\n");
            }

            builder.Append('\n');
        }

        IEnumerable<TestRecord> slowest = records
            .Where(static r => r.Duration > TimeSpan.Zero)
            .OrderByDescending(static r => r.Duration)
            .Take(MaxSlowestTests);

        bool slowestEmitted = false;
        foreach (TestRecord record in slowest)
        {
            if (!slowestEmitted)
            {
                builder.Append("### ⏱ Slowest tests\n\n");
                slowestEmitted = true;
            }

            builder.Append("- `").Append(EscapeInlineCode(record.FullyQualifiedName)).Append("` — ").Append(FormatDuration(record.Duration)).Append('\n');
        }

        if (slowestEmitted)
        {
            builder.Append('\n');
        }

        return builder.ToString();
    }

    private static string FormatDuration(TimeSpan duration)
        => SummaryReporterHelpers.FormatDuration(duration, "{0}m {1:00}s", "{0}h {1:00}m {2:00}s");

    private static string EscapeInlineCode(string value)
        => RoslynString.IsNullOrEmpty(value) ? value : value.Replace("`", "'").Replace("\r", string.Empty).Replace("\n", " ");
}
