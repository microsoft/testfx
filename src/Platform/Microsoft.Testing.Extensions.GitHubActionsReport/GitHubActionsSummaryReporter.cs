// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.GitHubActionsReport.Resources;
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

    private int GetAttemptNumber()
        => int.TryParse(
            _environment.GetEnvironmentVariable(EnvironmentVariableConstants.TESTINGPLATFORM_DOTNETTEST_ATTEMPTNUMBER),
            NumberStyles.None,
            CultureInfo.InvariantCulture,
            out int attemptNumber)
            && attemptNumber > 0
                ? attemptNumber
                : 1;
}
