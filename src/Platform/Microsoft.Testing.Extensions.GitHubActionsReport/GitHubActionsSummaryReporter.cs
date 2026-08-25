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
    private readonly Lazy<string> _targetFrameworkMoniker;
    private readonly bool _isEnabled;
    private readonly bool _writeOnFailureOnly;
    private readonly GitHubActionsStepSummarySections _sections;
    private readonly bool _includeFailureDetails;
    private readonly Func<bool> _shouldDeferToArtifactPostProcessing;

#if NET9_0_OR_GREATER
    private readonly System.Threading.Lock _stateLock = new();
#else
    private readonly object _stateLock = new();
#endif
#pragma warning disable IDE0028 // Collection initialization can be simplified - target-typed `new` cannot pass the comparer in the same syntactic form expected.
    private readonly Dictionary<string, TestRecord> _records = new Dictionary<string, TestRecord>(StringComparer.Ordinal);

    /// <summary>
    /// The raw inputs for each failed test's diagnostics, held until session end.
    /// </summary>
    /// <remarks>
    /// Only references already allocated by the test framework are kept here. Turning them into rendered
    /// diagnostics — formatting <see cref="Exception.StackTrace"/>, walking it for a source location, and
    /// clipping the result — costs far more than it looks, and only <see cref="MaxFailures"/> of them are ever
    /// rendered. A run with thousands of failures would otherwise pay that cost, and retain tens of megabytes of
    /// clipped text, for diagnostics it immediately discards.
    /// </remarks>
    private readonly Dictionary<string, (string? Explanation, Exception? Exception, TestNode TestNode)> _pendingFailures
        = new Dictionary<string, (string?, Exception?, TestNode)>(StringComparer.Ordinal);
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
        Func<bool> shouldDeferToArtifactPostProcessing)
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
        _targetFrameworkMoniker = new(TargetFrameworkMonikerHelper.GetTargetFrameworkMonikerIncludingPlatform);
        _isEnabled = GitHubActionsFeature.IsEnabled(commandLineOptions, environment, GitHubActionsCommandLineOptions.GitHubActionsStepSummary);
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

            // Capture only what is cheap to hold: the explanation and the exception reference, both of which
            // already exist. Formatting Exception.StackTrace, resolving its source location and clipping the
            // result is the expensive part, and at most MaxFailures of these are ever rendered — so that work is
            // deferred to session end and done only for the failures actually selected.
            (string? Explanation, Exception? Exception)? pendingFailure = kind == TerminalKind.Failed && _includeFailureDetails
                ? TryGetFailureInfo(state)
                : null;

            lock (_stateLock)
            {
                _records[uid] = new TestRecord(displayName, fullyQualifiedName, kind, duration);
                if (pendingFailure is { } failure)
                {
                    _pendingFailures[uid] = (failure.Explanation, failure.Exception, update.TestNode);
                }
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

            List<TestRecord> snapshot = BuildSnapshot();

            string assemblyName = _testApplicationModuleInfo.TryGetAssemblyName() ?? "unknown assembly name";
            int exitCode = _testApplicationProcessExitCode.GetProcessExitCode();
            CiCoverageSummaryData coverage = CiCoverageSummary.Create(_testCoverageResult, testSessionContext.SessionUid);
            if (_shouldDeferToArtifactPostProcessing()
                && _configuration.GetTestResultDirectory() is { } resultsDirectory
                && !RoslynString.IsNullOrWhiteSpace(resultsDirectory))
            {
                CiRunSummaryModule module = CreateModule(snapshot, assemblyName, testSessionContext, coverage);
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

            if (_writeOnFailureOnly
                && !snapshot.Any(static record => record.Kind == TerminalKind.Failed)
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
            if (!await TryAppendRenderedSummaryAsync(writer, snapshot, assemblyName, exitCode, coverage, testSessionContext).ConfigureAwait(false))
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
    private List<TestRecord> BuildSnapshot()
    {
        List<KeyValuePair<string, TestRecord>> entries;
        Dictionary<string, (string? Explanation, Exception? Exception, TestNode TestNode)> pending;
        lock (_stateLock)
        {
            entries = [.. _records];
            pending = new Dictionary<string, (string?, Exception?, TestNode)>(_pendingFailures, StringComparer.Ordinal);
        }

        entries.Sort(static (left, right) =>
        {
            int result = StringComparer.Ordinal.Compare(left.Value.FullyQualifiedName, right.Value.FullyQualifiedName);
            return result != 0 ? result : StringComparer.Ordinal.Compare(left.Value.DisplayName, right.Value.DisplayName);
        });

        var snapshot = new List<TestRecord>(entries.Count);
        int expanded = 0;
        foreach (KeyValuePair<string, TestRecord> entry in entries)
        {
            TestRecord record = entry.Value;
            if (record.Kind == TerminalKind.Failed
                && expanded < MaxFailures
                && pending.TryGetValue(entry.Key, out (string? Explanation, Exception? Exception, TestNode TestNode) failure))
            {
                expanded++;
                record = new TestRecord(
                    record.DisplayName,
                    record.FullyQualifiedName,
                    record.Kind,
                    record.Duration,
                    CaptureFailureDetails(failure.TestNode, failure.Explanation, failure.Exception));
            }

            snapshot.Add(record);
        }

        return snapshot;
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
        IReadOnlyList<TestRecord> records,
        string assemblyName,
        ITestSessionContext testSessionContext,
        CiCoverageSummaryData coverage)
    {
        CiRunSummaryModule module = CiRunSummaryAggregation.CreateModule(
            records,
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
        module.GitHubActionsStepSummarySections = GitHubActionsStepSummarySectionsParser.ToPersistedValues(_sections);
        return module;
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
    private TestFailureDetails? CaptureFailureDetails(TestNode testNode, string? explanationInput, Exception? exception)
    {
        string repoRoot = GitHubActionsRepositoryRoot.Resolve(_environment) ?? string.Empty;
        (string RelativeNormalizedPath, int LineNumber)? stackLocation = StackTraceSourceLocationResolver.TryResolve(
            exception?.StackTrace,
            repoRoot,
            _fileSystem,
            _logger,
            StackTraceSourceLocationResolver.SkipAssertionFramesForCurrentRuntime);
        GitHubActionsSourceLocation? location = stackLocation is { } resolved
            ? new GitHubActionsSourceLocation(resolved.RelativeNormalizedPath, resolved.LineNumber)
            : GitHubActionsAnnotationReporter.TryResolveDeclaredLocation(testNode, repoRoot, _fileSystem);

        // Fall back on whitespace, not just null: Clip treats a whitespace-only value as absent, so an empty
        // explanation would otherwise discard a perfectly good exception message.
        string? explanation = RoslynString.IsNullOrWhiteSpace(explanationInput)
            ? exception?.Message
            : explanationInput;

        return new TestFailureDetails(
            GitHubActionsFailureDetails.Clip(explanation, GitHubActionsFailureDetails.MaxMessageLength, GitHubActionsFailureDetails.MaxMessageRows),
            exception?.GetType().FullName,
            GitHubActionsFailureDetails.Clip(exception?.StackTrace, GitHubActionsFailureDetails.MaxStackTraceLength, GitHubActionsFailureDetails.MaxStackTraceRows),
            location?.RelativeNormalizedPath,
            location?.LineNumber ?? 0);
    }
}
