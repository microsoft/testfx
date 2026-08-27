// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.GitHubActionsReport.Resources;
using Microsoft.Testing.Platform;
using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Configurations;
using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Extensions.OutputDevice;
using Microsoft.Testing.Platform.Extensions.TestHost;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Logging;
using Microsoft.Testing.Platform.Messages;
using Microsoft.Testing.Platform.OutputDevice;
using Microsoft.Testing.Platform.Services;

namespace Microsoft.Testing.Extensions.GitHubActionsReport;

/// <summary>
/// Writes a markdown roll-up of the test run (totals, failures, slowest tests) to the file pointed to by
/// the <c>GITHUB_STEP_SUMMARY</c> environment variable. GitHub renders that file on the workflow run's
/// summary page. See
/// <see href="https://docs.github.com/en/actions/using-workflows/workflow-commands-for-github-actions#adding-a-job-summary"/>.
/// </summary>
internal sealed partial class GitHubActionsSummaryReporter :
    IDataConsumer,
    IDataProducer,
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

    private readonly IConfiguration _configuration;
    private readonly IEnvironment _environment;
    private readonly IFileSystem _fileSystem;
    private readonly IMessageBus _messageBus;
    private readonly IOutputDevice _outputDevice;
    private readonly ITestApplicationModuleInfo _testApplicationModuleInfo;
    private readonly ITestApplicationProcessExitCode _testApplicationProcessExitCode;
    private readonly ITestCoverageResult _testCoverageResult;
    private readonly ILogger _logger;
    private readonly IGitHubActionsHistoryService _historyService;
    private readonly Lazy<string> _targetFrameworkMoniker;
    private readonly bool _isEnabled;
    private readonly bool _isSummaryEnabled;
    private readonly bool _writeOnFailureOnly;
    private readonly GitHubActionsStepSummarySections _sections;
    private readonly bool _includeFailureDetails;
    private readonly Func<bool> _shouldDeferToArtifactPostProcessing;

#if NET9_0_OR_GREATER
    private readonly System.Threading.Lock _stateLock = new();
#else
    private readonly object _stateLock = new();
#endif
    private readonly List<(string Uid, string Key, TestRecord Record)> _records = [];
#pragma warning disable IDE0028 // Collection expressions cannot pass the required comparer.
    private readonly Dictionary<string, int> _finalRowCountsByUid = new(StringComparer.Ordinal);
    private readonly Dictionary<string, List<int>> _flakyRecordIndicesByUid = new(StringComparer.Ordinal);
    private readonly HashSet<string> _inProcessFailedTests = new(StringComparer.Ordinal);
    private readonly HashSet<string> _notRecoveredTests = new(StringComparer.Ordinal);

    /// <summary>
    /// The raw inputs for each failed test's diagnostics, held until session end.
    /// </summary>
    /// <remarks>
    /// Only what rendering actually needs is kept: the explanation, the exception (already allocated by the test
    /// framework), and the declared source location read out of the node's property bag. The <see cref="TestNode"/>
    /// itself is deliberately not retained — its property bag can carry unbounded captured standard output and
    /// error, so holding one per failure would retain a run's entire output to render at most
    /// <see cref="MaxFailures"/> of them.
    /// <para>
    /// The map is also bounded to <see cref="MaxFailures"/> entries, ordered the same way the snapshot is, so a
    /// run with thousands of failures retains only the diagnostics that will actually be rendered.
    /// </para>
    /// </remarks>
    private readonly Dictionary<string, PendingFailure> _pendingFailures = new Dictionary<string, PendingFailure>(StringComparer.Ordinal);
#pragma warning restore IDE0028

    public GitHubActionsSummaryReporter(
        ICommandLineOptions commandLineOptions,
        IConfiguration configuration,
        IEnvironment environment,
        IFileSystem fileSystem,
        IMessageBus messageBus,
        IOutputDevice outputDevice,
        ITestApplicationModuleInfo testApplicationModuleInfo,
        ITestApplicationProcessExitCode testApplicationProcessExitCode,
        ITestCoverageResult testCoverageResult,
        ILoggerFactory loggerFactory,
        Func<bool> shouldDeferToArtifactPostProcessing,
        IGitHubActionsHistoryService? historyService = null)
    {
        _configuration = configuration;
        _environment = environment;
        _fileSystem = fileSystem;
        _messageBus = messageBus;
        _outputDevice = outputDevice;
        _testApplicationModuleInfo = testApplicationModuleInfo;
        _testApplicationProcessExitCode = testApplicationProcessExitCode;
        _testCoverageResult = testCoverageResult;
        _logger = loggerFactory.CreateLogger<GitHubActionsSummaryReporter>();
        _historyService = historyService ?? DisabledGitHubActionsHistoryService.Instance;
        _targetFrameworkMoniker = new(TargetFrameworkMonikerHelper.GetTargetFrameworkMonikerIncludingPlatform);
        _isSummaryEnabled = GitHubActionsFeature.IsEnabled(commandLineOptions, environment, GitHubActionsCommandLineOptions.GitHubActionsStepSummary);
        _isEnabled = _isSummaryEnabled || _historyService.IsEnabled;
        _writeOnFailureOnly = GitHubActionsFeature.IsStepSummaryOnFailureOnly(commandLineOptions);
        _sections = GitHubActionsStepSummarySectionsParser.GetSections(commandLineOptions);
        _includeFailureDetails = GitHubActionsFeature.IsKnobEnabled(commandLineOptions, GitHubActionsCommandLineOptions.GitHubActionsFailureDetails);
        _shouldDeferToArtifactPostProcessing = shouldDeferToArtifactPostProcessing;
    }

    public Type[] DataTypesConsumed { get; } = [typeof(TestNodeUpdateMessage)];

    public Type[] DataTypesProduced { get; } = [typeof(SessionFileArtifact)];

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
            _finalRowCountsByUid.Clear();
            _flakyRecordIndicesByUid.Clear();
            _inProcessFailedTests.Clear();
            _notRecoveredTests.Clear();
            _pendingFailures.Clear();
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
            RetryAttemptProperty? retryAttempt = update.TestNode.Properties.SingleOrDefault<RetryAttemptProperty>();

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

            // Capture only what is cheap to hold and small: the explanation, the exception reference, and the
            // declared source location read out of the property bag now — the node itself is not retained, since
            // its bag can carry a run's entire captured output. Formatting Exception.StackTrace, walking it for a
            // location and clipping the result is the expensive part, and at most MaxFailures of these are ever
            // rendered, so that work is deferred to session end and done only for the failures selected.
            (string? Explanation, Exception? Exception)? pendingFailure = kind == TerminalKind.Failed && _includeFailureDetails
                ? TryGetFailureInfo(state)
                : null;

            lock (_stateLock)
            {
                if (retryAttempt is { IsSuperseded: true })
                {
                    if (kind == TerminalKind.Failed)
                    {
                        _inProcessFailedTests.Add(uid);
                    }

                    return Task.CompletedTask;
                }

                if (kind is TerminalKind.Failed or TerminalKind.Skipped)
                {
                    // Keep this sticky for the session: folded data-driven rows can share a uid and arrive in
                    // either order, so a later passing row must not turn a mixed-outcome uid into a recovery.
                    _notRecoveredTests.Add(uid);
                    ClearFlakyRecords(uid);
                }

                bool isFlaky = kind == TerminalKind.Passed
                    && !_notRecoveredTests.Contains(uid)
                    && (_inProcessFailedTests.Contains(uid) || GetAttemptNumber() > 1);
                int finalRowCount = _finalRowCountsByUid.TryGetValue(uid, out int existingFinalRowCount)
                    ? existingFinalRowCount + 1
                    : 1;
                _finalRowCountsByUid[uid] = finalRowCount;
                string recordKey = $"{uid}\0{finalRowCount.ToString(CultureInfo.InvariantCulture)}";
                _records.Add((uid, recordKey, new TestRecord(displayName, fullyQualifiedName, kind, duration, isFlaky)));
                if (isFlaky)
                {
                    if (!_flakyRecordIndicesByUid.TryGetValue(uid, out List<int>? indices))
                    {
                        indices = [];
                        _flakyRecordIndicesByUid.Add(uid, indices);
                    }

                    indices.Add(_records.Count - 1);
                }

                if (finalRowCount > 1)
                {
                    // Multiple final rows share one UID, so the protocol cannot identify which row recovered.
                    // Keep every row and its outcome, but fail closed instead of attributing flakiness to all of them.
                    ClearFlakyRecords(uid);
                }

                PendingFailure? failure = null;
                if (pendingFailure is { } info)
                {
                    TestFileLocationProperty? declared = update.TestNode.Properties.FirstOrDefault<TestFileLocationProperty>();
                    failure = new PendingFailure(
                        fullyQualifiedName,
                        displayName,
                        info.Explanation,
                        info.Exception,
                        declared?.FilePath,
                        declared?.LineSpan.Start.Line ?? 0);
                }

                ApplyPendingFailure(_pendingFailures, recordKey, failure, MaxFailures);
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

    private void ClearFlakyRecords(string uid)
    {
        if (!_flakyRecordIndicesByUid.TryGetValue(uid, out List<int>? indices))
        {
            return;
        }

        _flakyRecordIndicesByUid.Remove(uid);
        foreach (int index in indices)
        {
            (string existingUid, string key, TestRecord record) = _records[index];
            _records[index] = (existingUid, key, new TestRecord(
                record.DisplayName,
                record.FullyQualifiedName,
                record.Kind,
                record.Duration,
                isFlaky: false,
                record.Failure));
        }
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

            SummarySnapshot snapshot = BuildSnapshot();
            string assemblyName = _testApplicationModuleInfo.TryGetAssemblyName() ?? "unknown assembly name";
            int exitCode = _testApplicationProcessExitCode.GetProcessExitCode();
            CiCoverageSummaryData coverage = CiCoverageSummary.Create(_testCoverageResult, testSessionContext.SessionUid);
            CiRunSummaryModule module = CreateModule(snapshot, assemblyName, testSessionContext, coverage);
            if (_shouldDeferToArtifactPostProcessing()
                && _configuration.GetTestResultDirectory() is { } resultsDirectory
                && !RoslynString.IsNullOrWhiteSpace(resultsDirectory))
            {
                string fragmentPath = await CiRunSummaryAggregation.WriteFragmentAsync(
                    resultsDirectory,
                    GitHubActionsSummaryArtifactPostProcessor.Provider,
                    GitHubActionsSummaryArtifactPostProcessor.ProviderSlug,
                    module).ConfigureAwait(false);
                await _messageBus.PublishAsync(
                    this,
                    new SessionFileArtifact(
                        testSessionContext.SessionUid,
                        new FileInfo(fragmentPath),
                        GitHubActionsResources.DisplayName,
                        GitHubActionsResources.Description,
                        GitHubActionsSummaryArtifactPostProcessor.FragmentArtifactKind)).ConfigureAwait(false);
                return;
            }

            await _historyService.WriteAsync([module], testSessionContext.CancellationToken).ConfigureAwait(false);
            if (!_isSummaryEnabled)
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

            if (_writeOnFailureOnly
                && !snapshot.Records.Any(static record => record.Kind == TerminalKind.Failed)
                && !GitHubActionsExitCode.IndicatesFailure(exitCode))
            {
                return;
            }

            // The 1 MiB cap applies to the whole GITHUB_STEP_SUMMARY file, which every test project in the job
            // appends to, so this reporter degrades in stages as the shared file fills up:
            //
            //   below 40%  full section, failures expanded into collapsible diagnostics
            //   40% - 60%  full section, but failures listed as name and duration only
            //   above 60%  one-line verdict for the whole project
            //
            // Shedding the diagnostics first is what keeps the report useful for longest: the list of which tests
            // failed is a line per failure, while the diagnostics behind it are kilobytes.
            //
            // Both the decision and the write happen inside the writer's lock. Deciding beforehand would let every
            // project that finishes at the same moment observe the same file length and each render a full
            // section, so the absolute cap would admit the first few and refuse the rest outright — turning a
            // report where every project degrades gracefully into one where the first get everything and the rest
            // get nothing.
            var writer = new StepSummaryWriter(_fileSystem, path!, _logger, StepSummaryMaxWriteAttempts, StepSummaryRetryDelay);
            if (!await TryAppendRenderedSummaryAsync(writer, snapshot.Records, assemblyName, exitCode, coverage, testSessionContext).ConfigureAwait(false))
            {
                await ReportSectionDroppedAsync(writer, testSessionContext).ConfigureAwait(false);
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
    /// What rendering a failure's diagnostics needs, without retaining the test node it came from.
    /// </summary>
    internal readonly struct PendingFailure
    {
        internal PendingFailure(string fullyQualifiedName, string displayName, string? explanation, Exception? exception, string? declaredFilePath, int declaredLine)
        {
            FullyQualifiedName = fullyQualifiedName;
            DisplayName = displayName;
            Explanation = explanation;
            Exception = exception;
            DeclaredFilePath = declaredFilePath;
            DeclaredLine = declaredLine;
        }

        internal string FullyQualifiedName { get; }

        internal string DisplayName { get; }

        internal string? Explanation { get; }

        internal Exception? Exception { get; }

        internal string? DeclaredFilePath { get; }

        internal int DeclaredLine { get; }
    }

    /// <summary>
    /// Orders failures the way the rendered summary does, so "the ones that will be shown" is decidable before
    /// the session ends.
    /// </summary>
    /// <remarks>
    /// The uid is the final tie-break, and it is what makes the order total. Distinct tests can share both names —
    /// duplicate parameterized cases most obviously — and without it their relative order comes from dictionary
    /// enumeration in one caller and an unstable sort in the other. Retention and rendering could then disagree
    /// about which of the tied failures falls inside the first <see cref="MaxFailures"/>, so one would be shown
    /// stripped of its diagnostics while the other held diagnostics that are never rendered.
    /// </remarks>
    private static int CompareForRendering(
        string leftUid,
        string leftFullyQualifiedName,
        string leftDisplayName,
        string rightUid,
        string rightFullyQualifiedName,
        string rightDisplayName)
    {
        int result = StringComparer.Ordinal.Compare(leftFullyQualifiedName, rightFullyQualifiedName);
        if (result != 0)
        {
            return result;
        }

        result = StringComparer.Ordinal.Compare(leftDisplayName, rightDisplayName);
        return result != 0 ? result : StringComparer.Ordinal.Compare(leftUid, rightUid);
    }

    /// <summary>
    /// Records the diagnostics for a test's latest terminal state, keeping retention bounded to
    /// <paramref name="keep"/> entries.
    /// </summary>
    /// <remarks>
    /// A <see langword="null"/> <paramref name="failure"/> clears whatever was held for the UID. In-process retries
    /// reuse the same UID, so a test that failed and then recovered has to give its retained slot back — leaving it
    /// would let a passing test evict the diagnostics of one that is still failing, and that failure would then
    /// render as a bare line with nothing explaining why its details are missing.
    /// </remarks>
    internal static /* for testing */ void ApplyPendingFailure(
        Dictionary<string, PendingFailure> pending,
        string uid,
        PendingFailure? failure,
        int keep)
    {
        if (failure is { } value)
        {
            pending[uid] = value;
            TrimToRenderedFailures(pending, keep);
        }
        else
        {
            pending.Remove(uid);
        }
    }

    /// <summary>
    /// Drops the entries that sort last once more than <paramref name="keep"/> are held, so retention stays
    /// bounded however many tests fail.
    /// </summary>
    internal static /* for testing */ void TrimToRenderedFailures(Dictionary<string, PendingFailure> pending, int keep)
    {
        while (pending.Count > keep)
        {
            string? worstKey = null;
            PendingFailure worst = default;
            foreach (KeyValuePair<string, PendingFailure> candidate in pending)
            {
                if (worstKey is null
                    || CompareForRendering(candidate.Key, candidate.Value.FullyQualifiedName, candidate.Value.DisplayName, worstKey, worst.FullyQualifiedName, worst.DisplayName) > 0)
                {
                    worstKey = candidate.Key;
                    worst = candidate.Value;
                }
            }

            pending.Remove(worstKey!);
        }
    }

    /// <summary>
    /// Takes the recorded tests in a deterministic order and attaches diagnostics to the failures that will
    /// actually be rendered.
    /// </summary>
    /// <remarks>
    /// The order matters twice. It makes which failures get expanded reproducible — dictionary order is arbitrary,
    /// so without it a run could expand a different twenty failures each time — and it makes the direct path's
    /// "first <see cref="MaxFailures"/> encountered" agree with the aggregated path's "first
    /// <see cref="MaxFailures"/> by name", so exactly the failures that need diagnostics are the ones that pay
    /// for them.
    /// </remarks>
    private SummarySnapshot BuildSnapshot()
    {
        List<(string Uid, string Key, TestRecord Record)> entries;
        Dictionary<string, PendingFailure> pending;
        lock (_stateLock)
        {
            entries = [.. _records];
            pending = new Dictionary<string, PendingFailure>(_pendingFailures, StringComparer.Ordinal);
        }

        entries.Sort(static (left, right)
            => CompareForRendering(
                left.Key,
                left.Record.FullyQualifiedName,
                left.Record.DisplayName,
                right.Key,
                right.Record.FullyQualifiedName,
                right.Record.DisplayName));

        var snapshot = new List<TestRecord>(entries.Count);
        List<CiRunSummaryHistoryTest>? historyTests = _historyService.IsEnabled
            ? new(Math.Min(entries.Count, GitHubActionsHistoryStore.MaxTotalSamples))
            : null;
        int expanded = 0;
        foreach ((string Uid, string Key, TestRecord Record) entry in entries)
        {
            TestRecord record = entry.Record;
            if (record.Kind == TerminalKind.Failed
                && expanded < MaxFailures
                && pending.TryGetValue(entry.Key, out PendingFailure failure))
            {
                expanded++;
                TestFailureDetails? failureDetails = CaptureFailureDetails(failure);
                if (failureDetails is not null
                    && _historyService.TryGetStats(
                        entry.Uid,
                        record.FullyQualifiedName,
                        record.DisplayName,
                        out GitHubActionsHistoryStats historyStats)
                    && (historyStats.TotalCount > 0 || historyStats.DurationSampleCount > 0))
                {
                    failureDetails = new TestFailureDetails(
                        GitHubActionsAnnotationReporter.FormatHistoryContext(
                            failureDetails.Message ?? GitHubActionsResources.NoFailureMessageFallback,
                            historyStats,
                            _historyService.HistoryWindowInDays),
                        failureDetails.ExceptionType,
                        failureDetails.StackTrace,
                        failureDetails.FilePath,
                        failureDetails.LineNumber);
                }

                record = new TestRecord(
                    record.DisplayName,
                    record.FullyQualifiedName,
                    record.Kind,
                    record.Duration,
                    record.IsFlaky,
                    failureDetails);
            }

            snapshot.Add(record);
            if (historyTests?.Count < GitHubActionsHistoryStore.MaxTotalSamples)
            {
                historyTests.Add(new CiRunSummaryHistoryTest
                {
                    TestId = entry.Uid,
                    DisplayName = record.DisplayName,
                    FullyQualifiedName = record.FullyQualifiedName,
                    Outcome = record.Kind switch
                    {
                        TerminalKind.Passed => GitHubActionsHistoryOutcome.Passed,
                        TerminalKind.Failed => GitHubActionsHistoryOutcome.Failed,
                        TerminalKind.Skipped => GitHubActionsHistoryOutcome.Skipped,
                        _ => throw new InvalidOperationException($"Unexpected terminal kind '{record.Kind}'."),
                    },
                    DurationTicks = record.Duration.Ticks,
                    IsFlaky = record.IsFlaky,
                });
            }
        }

        return new SummarySnapshot(snapshot, historyTests ?? []);
    }

    /// <returns>
    /// <see langword="false"/> only when the writer refused the content because it would have taken the file past
    /// GitHub's cap. A write that failed for any other reason has already been reported and returns
    /// <see langword="true"/>, so the caller does not warn about it twice.
    /// </returns>
    private async Task<bool> TryAppendRenderedSummaryAsync(
        StepSummaryWriter writer,
        IReadOnlyList<TestRecord> snapshot,
        string assemblyName,
        int exitCode,
        CiCoverageSummaryData coverage,
        ITestSessionContext testSessionContext)
    {
        try
        {
            return await writer.AppendRenderedStepSummarySectionAsync(
                currentLength =>
                {
                    var budget = SummaryBudget.ForProject(currentLength);
                    bool condense = budget.Stage is SummaryStage.Condensed or SummaryStage.Unlisted;
                    string markdown = condense
                        ? BuildMinimalMarkdown(snapshot, assemblyName, _targetFrameworkMoniker.Value, exitCode)
                        : BuildMarkdown(snapshot, assemblyName, _targetFrameworkMoniker.Value, exitCode, coverage, _sections, _includeFailureDetails, budget);

                    // The note is only warranted once whole project sections start disappearing. Dropping the
                    // expanded diagnostics leaves every project and every failing test still named, which is a
                    // shortened report rather than an incomplete one, and does not need a warning at the top.
                    return (markdown, condense);
                },

                // The project count the note quotes is read from the summary file when the note is written, so it
                // is passed as a factory rather than a string: only the writer holds the file exclusively, and
                // only it can count without racing a sibling project.
                BuildTruncationNotice,
                testSessionContext.CancellationToken,
                GitHubActionsFailureDetails.EffectiveStepSummaryLimit).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            await ReportWriteFailureAsync(writer.Path, ex, testSessionContext).ConfigureAwait(false);
            return true;
        }
    }

    /// <summary>
    /// Warns that this project's section did not fit in the shared summary, and makes sure the note explaining
    /// that the report is incomplete is present.
    /// </summary>
    private async Task ReportSectionDroppedAsync(StepSummaryWriter writer, ITestSessionContext testSessionContext)
    {
        string overflowWarning = string.Format(
            CultureInfo.InvariantCulture,
            GitHubActionsResources.StepSummaryLimitExceededWarning,
            (writer.GetSummaryLength() ?? 0).ToString(CultureInfo.InvariantCulture),
            GitHubActionsFailureDetails.EffectiveStepSummaryLimit.ToString(CultureInfo.InvariantCulture));

        if (_logger.IsEnabled(LogLevel.Warning))
        {
            _logger.LogWarning(overflowWarning);
        }

        await _outputDevice.DisplayAsync(this, new WarningMessageOutputDeviceData(overflowWarning), testSessionContext.CancellationToken).ConfigureAwait(false);

        // This project's section is dropped entirely, which is exactly what the note at the top of the summary
        // describes, so make sure it is there even if this project was not condensed. The note is a few hundred
        // bytes and dropping the section frees far more room than it takes; if even that does not fit, the writer
        // refuses it and the summary is genuinely full, where silence is what keeps the rest rendered.
        await TryAppendNoticeOnlyAsync(writer, testSessionContext).ConfigureAwait(false);
    }

    /// <summary>
    /// Writes the top-of-file note on its own, for when this project's section was dropped entirely.
    /// </summary>
    private async Task TryAppendNoticeOnlyAsync(StepSummaryWriter writer, ITestSessionContext testSessionContext)
    {
        try
        {
            await writer.AppendStepSummaryWithLeadingNoticeAsync(
                string.Empty,
                BuildTruncationNotice,
                testSessionContext.CancellationToken,
                GitHubActionsFailureDetails.EffectiveStepSummaryLimit).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            await ReportWriteFailureAsync(writer.Path, ex, testSessionContext).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Surfaces a failed summary write as a warning: failing to write the summary must not fail the test run.
    /// </summary>
    private async Task ReportWriteFailureAsync(string path, Exception ex, ITestSessionContext testSessionContext)
    {
        string warning = string.Format(CultureInfo.InvariantCulture, GitHubActionsResources.StepSummaryWriteFailedWarning, path, ex.Message);
        if (_logger.IsEnabled(LogLevel.Warning))
        {
            _logger.LogWarning(warning);
        }

        await _outputDevice.DisplayAsync(this, new WarningMessageOutputDeviceData(warning), testSessionContext.CancellationToken).ConfigureAwait(false);
    }

    private CiRunSummaryModule CreateModule(
        SummarySnapshot snapshot,
        string assemblyName,
        ITestSessionContext testSessionContext,
        CiCoverageSummaryData coverage)
    {
        CiRunSummaryModule module = CiRunSummaryAggregation.CreateModule(
            snapshot.Records,
            assemblyName,
            _testApplicationModuleInfo.GetCurrentTestApplicationFullPath(),
            _targetFrameworkMoniker.Value,
            RuntimeInformation.ProcessArchitecture.ToString().ToLowerInvariant(),
            _environment.GetEnvironmentVariable(EnvironmentVariableConstants.TESTINGPLATFORM_DOTNETTEST_EXECUTIONID),
            testSessionContext.SessionUid.Value,
            GetAttemptNumber(),
            _testApplicationProcessExitCode.GetProcessExitCode(),
            coverage: coverage,
            writeOnFailureOnly: _writeOnFailureOnly);
        module.HistoryTests = [.. snapshot.HistoryTests];
        module.GitHubActionsStepSummaryEnabled = _isSummaryEnabled;
        module.GitHubActionsHistoryPath = _historyService.HistoryPath;
        module.GitHubActionsHistoryWindowInDays = _historyService.IsEnabled
            ? _historyService.HistoryWindowInDays
            : 0;
        module.GitHubActionsStepSummarySections = GitHubActionsStepSummarySectionsParser.ToPersistedValues(_sections);
        return module;
    }

    private sealed class SummarySnapshot(
        IReadOnlyList<TestRecord> records,
        IReadOnlyList<CiRunSummaryHistoryTest> historyTests)
    {
        public IReadOnlyList<TestRecord> Records { get; } = records;

        public IReadOnlyList<CiRunSummaryHistoryTest> HistoryTests { get; } = historyTests;
    }

    private int GetAttemptNumber()
        => int.TryParse(
            _environment.GetEnvironmentVariable(EnvironmentVariableConstants.TESTINGPLATFORM_DOTNETTEST_ATTEMPTNUMBER),
            NumberStyles.None,
            CultureInfo.InvariantCulture,
            out int attemptNumber)
            && attemptNumber > 0
                ? attemptNumber
                : 1;

    /// <summary>
    /// Extracts the explanation and exception a failing state carries, or <see langword="null"/> for a state
    /// that is not a failure. Separate from <see cref="CaptureFailureDetails"/> so each state shape can be
    /// covered without standing up a whole reporter.
    /// </summary>
    internal static /* for testing */ (string? Explanation, Exception? Exception)? TryGetFailureInfo(TestNodeStateProperty? state)
        => state switch
        {
            FailedTestNodeStateProperty failed => (failed.Explanation, failed.Exception),
            ErrorTestNodeStateProperty error => (error.Explanation, error.Exception),
            TimeoutTestNodeStateProperty timeout => (timeout.Explanation, timeout.Exception),
#pragma warning disable CS0618, MTP0001 // Type or member is obsolete
            CancelledTestNodeStateProperty cancelled => (cancelled.Explanation, cancelled.Exception),
#pragma warning restore CS0618, MTP0001 // Type or member is obsolete
            _ => null,
        };

    /// <summary>
    /// Captures the diagnostics of a failing test — explanation/exception message, exception type, stack trace and
    /// the source location — so the job summary can expand the failure beyond its name.
    /// </summary>
    /// <remarks>
    /// The location is resolved the same way <see cref="GitHubActionsAnnotationReporter"/> resolves it: prefer the
    /// exception's call site (it pinpoints the failing statement) and fall back to the location the test framework
    /// reported for the test itself, so frameworks without a usable stack trace still get a location. Values are
    /// clipped here rather than at render time so an enormous stack trace never reaches the aggregation fragment
    /// written to disk.
    /// </remarks>
    private TestFailureDetails? CaptureFailureDetails(PendingFailure failure)
    {
        Exception? exception = failure.Exception;
        string repoRoot = GitHubActionsRepositoryRoot.Resolve(_environment) ?? string.Empty;
        (string RelativeNormalizedPath, int LineNumber)? stackLocation = StackTraceSourceLocationResolver.TryResolve(
            exception?.StackTrace,
            repoRoot,
            _fileSystem,
            _logger,
            StackTraceSourceLocationResolver.SkipAssertionFramesForCurrentRuntime);
        GitHubActionsSourceLocation? location = stackLocation is { } resolved
            ? new GitHubActionsSourceLocation(resolved.RelativeNormalizedPath, resolved.LineNumber)
            : ResolveDeclaredLocation(failure, repoRoot);

        // Fall back on whitespace, not just null: Clip treats a whitespace-only value as absent, so an empty
        // explanation would otherwise discard a perfectly good exception message.
        string? explanation = RoslynString.IsNullOrWhiteSpace(failure.Explanation)
            ? exception?.Message
            : failure.Explanation;

        return new TestFailureDetails(
            GitHubActionsFailureDetails.Clip(explanation, GitHubActionsFailureDetails.MaxMessageLength, GitHubActionsFailureDetails.MaxMessageRows),
            exception?.GetType().FullName,
            GitHubActionsFailureDetails.Clip(exception?.StackTrace, GitHubActionsFailureDetails.MaxStackTraceLength, GitHubActionsFailureDetails.MaxStackTraceRows),
            location?.RelativeNormalizedPath,
            location?.LineNumber ?? 0);
    }

    /// <summary>
    /// Resolves the location the test framework declared for the test, from the file and line read out of its
    /// property bag at capture time. Mirrors <see cref="GitHubActionsAnnotationReporter.TryResolveDeclaredLocation"/>,
    /// but works from the two small values kept rather than from the node, which is not retained.
    /// </summary>
    private GitHubActionsSourceLocation? ResolveDeclaredLocation(PendingFailure failure, string repoRoot)
    {
        if (RoslynString.IsNullOrWhiteSpace(failure.DeclaredFilePath))
        {
            return null;
        }

        string? relativeNormalizedPath = StackTraceSourceLocationResolver.TryMakeWorkspaceRelative(failure.DeclaredFilePath!, repoRoot, _fileSystem);

        // A framework that knows the file but not the line reports a sentinel (-1) or 0; GitHub accepts a
        // 'file'-only annotation, so drop the line rather than emitting an invalid one.
        return relativeNormalizedPath is null
            ? null
            : new GitHubActionsSourceLocation(relativeNormalizedPath, failure.DeclaredLine > 0 ? failure.DeclaredLine : 0);
    }
}
