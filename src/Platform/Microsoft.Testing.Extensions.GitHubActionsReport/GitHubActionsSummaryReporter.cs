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
    private readonly ILogger _logger;
    private readonly Lazy<string> _targetFrameworkMoniker;
    private readonly bool _isEnabled;
    private readonly bool _includeFailureDetails;
    private readonly Func<bool> _shouldDeferToArtifactPostProcessing;

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
        IConfiguration configuration,
        IEnvironment environment,
        IFileSystem fileSystem,
        IMessageBus messageBus,
        IOutputDevice outputDevice,
        ITestApplicationModuleInfo testApplicationModuleInfo,
        ITestApplicationProcessExitCode testApplicationProcessExitCode,
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
        _logger = loggerFactory.CreateLogger<GitHubActionsSummaryReporter>();
        _targetFrameworkMoniker = new(TargetFrameworkMonikerHelper.GetTargetFrameworkMonikerIncludingPlatform);
        _isEnabled = GitHubActionsFeature.IsEnabled(commandLineOptions, environment, GitHubActionsCommandLineOptions.GitHubActionsStepSummary);
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

            TestFailureDetails? failureDetails = kind == TerminalKind.Failed && _includeFailureDetails
                ? CaptureFailureDetails(update.TestNode, state)
                : null;

            lock (_stateLock)
            {
                _records[uid] = new TestRecord(displayName, fullyQualifiedName, kind, duration, failureDetails);
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

            string assemblyName = _testApplicationModuleInfo.TryGetAssemblyName() ?? "unknown assembly name";
            int exitCode = _testApplicationProcessExitCode.GetProcessExitCode();
            if (_shouldDeferToArtifactPostProcessing()
                && _configuration.GetTestResultDirectory() is { } resultsDirectory
                && !RoslynString.IsNullOrWhiteSpace(resultsDirectory))
            {
                CiRunSummaryModule module = CreateModule(snapshot, assemblyName, testSessionContext);
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

            // The 1 MiB cap applies to the whole GITHUB_STEP_SUMMARY file, which every test project in the job
            // appends to, so this reporter degrades in two stages as the shared file fills up:
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
            if (!await TryAppendRenderedSummaryAsync(writer, snapshot, assemblyName, exitCode, testSessionContext).ConfigureAwait(false))
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
    /// Renders this project's section against the file length observed under the writer's lock, and writes it in
    /// the same transaction. Best-effort: a failure to write the summary must not fail the test run, so it
    /// surfaces as a warning.
    /// </summary>
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
                        : BuildMarkdown(snapshot, assemblyName, _targetFrameworkMoniker.Value, exitCode, _includeFailureDetails, budget);

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
        ITestSessionContext testSessionContext)
        => CiRunSummaryAggregation.CreateModule(
            records,
            assemblyName,
            _testApplicationModuleInfo.GetCurrentTestApplicationFullPath(),
            _targetFrameworkMoniker.Value,
            RuntimeInformation.ProcessArchitecture.ToString().ToLowerInvariant(),
            _environment.GetEnvironmentVariable(EnvironmentVariableConstants.TESTINGPLATFORM_DOTNETTEST_EXECUTIONID),
            testSessionContext.SessionUid.Value,
            GetAttemptNumber(),
            _testApplicationProcessExitCode.GetProcessExitCode());

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

    private TestFailureDetails? CaptureFailureDetails(TestNode testNode, TestNodeStateProperty? state)
    {
        (string? Explanation, Exception? Exception)? failure = TryGetFailureInfo(state);

        if (failure is null)
        {
            return null;
        }

        Exception? exception = failure.Value.Exception;
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
        string? explanation = RoslynString.IsNullOrWhiteSpace(failure.Value.Explanation)
            ? exception?.Message
            : failure.Value.Explanation;

        return new TestFailureDetails(
            GitHubActionsFailureDetails.Clip(explanation, GitHubActionsFailureDetails.MaxMessageLength, GitHubActionsFailureDetails.MaxMessageRows),
            exception?.GetType().FullName,
            GitHubActionsFailureDetails.Clip(exception?.StackTrace, GitHubActionsFailureDetails.MaxStackTraceLength, GitHubActionsFailureDetails.MaxStackTraceRows),
            location?.RelativeNormalizedPath,
            location?.LineNumber ?? 0);
    }
}
