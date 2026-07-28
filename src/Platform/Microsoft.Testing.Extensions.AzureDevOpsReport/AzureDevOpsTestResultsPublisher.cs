// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.AzureDevOpsReport.Resources;
using Microsoft.Testing.Extensions.Reporting;
using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Configurations;
using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Extensions.OutputDevice;
using Microsoft.Testing.Platform.Extensions.TestHost;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Logging;
using Microsoft.Testing.Platform.OutputDevice;
using Microsoft.Testing.Platform.Services;

namespace Microsoft.Testing.Extensions.AzureDevOpsReport;

internal sealed partial class AzureDevOpsTestResultsPublisher : IDataConsumer, ITestSessionLifetimeHandler, IOutputDeviceDataProducer, IDisposable
{
    private readonly ICommandLineOptions _commandLineOptions;
    private readonly IConfiguration _configuration;
    private readonly IEnvironment _environment;
    private readonly IFileSystem _fileSystem;
    private readonly IOutputDevice _outputDevice;
    private readonly ITestApplicationModuleInfo _testApplicationModuleInfo;
    private readonly ITestApplicationProcessExitCode _testApplicationProcessExitCode;
    private readonly IAzureDevOpsTestResultsClient _client;
    private readonly ITask _task;
    private readonly IClock _clock;
    private readonly ILogger _logger;
    private readonly AzureDevOpsTestResultsPublisherOptions _options;
    // Mutate only while holding _flushSemaphore.
    private readonly Stack<AzureDevOpsTestCaseResultWithAttachments> _retryResults = new();
    private readonly ConcurrentQueue<AzureDevOpsTestCaseResultWithAttachments> _pendingResults = new();
    private readonly ConcurrentQueue<AzureDevOpsTestResultAttachment> _pendingRunAttachments = new();
    private readonly SemaphoreSlim _flushSemaphore = new(1, 1);

    private AzureDevOpsPublishConfiguration? _publishConfiguration;
    private AzureDevOpsRunIdCoordinator? _runIdCoordinator;
    private AzureDevOpsCoordinatedRun? _coordinatedRun;
    private DateTimeOffset _lastFlushTime;
    private CancellationTokenSource? _backgroundFlushCts;
    private Task? _backgroundFlushTask;
    private int _isDisposed;
    private int _failedAttachmentCount;

    private int? CurrentRunId { get; set; }

    public AzureDevOpsTestResultsPublisher(
        ICommandLineOptions commandLineOptions,
        IConfiguration configuration,
        IEnvironment environment,
        IFileSystem fileSystem,
        IOutputDevice outputDevice,
        ITestApplicationModuleInfo testApplicationModuleInfo,
        ITestApplicationProcessExitCode testApplicationProcessExitCode,
        IAzureDevOpsTestResultsClient client,
        ITask task,
        IClock clock,
        ILoggerFactory loggerFactory)
        : this(commandLineOptions, configuration, environment, fileSystem, outputDevice, testApplicationModuleInfo, testApplicationProcessExitCode, client, task, clock, loggerFactory.CreateLogger<AzureDevOpsTestResultsPublisher>(), AzureDevOpsTestResultsPublisherOptions.Default)
    {
    }

    internal AzureDevOpsTestResultsPublisher(
        ICommandLineOptions commandLineOptions,
        IConfiguration configuration,
        IEnvironment environment,
        IFileSystem fileSystem,
        IOutputDevice outputDevice,
        ITestApplicationModuleInfo testApplicationModuleInfo,
        ITestApplicationProcessExitCode testApplicationProcessExitCode,
        IAzureDevOpsTestResultsClient client,
        ITask task,
        IClock clock,
        ILogger logger,
        AzureDevOpsTestResultsPublisherOptions options)
    {
        _commandLineOptions = commandLineOptions;
        _configuration = configuration;
        _environment = environment;
        _fileSystem = fileSystem;
        _outputDevice = outputDevice;
        _testApplicationModuleInfo = testApplicationModuleInfo;
        _testApplicationProcessExitCode = testApplicationProcessExitCode;
        _client = client;
        _task = task;
        _clock = clock;
        _logger = logger;
        _options = options;
        _lastFlushTime = clock.UtcNow;
    }

    public Type[] DataTypesConsumed { get; } = [typeof(TestNodeUpdateMessage), typeof(SessionFileArtifact)];

    public string Uid => nameof(AzureDevOpsTestResultsPublisher);

    public string Version => ExtensionVersion.DefaultSemVer;

    public string DisplayName => AzureDevOpsResources.DisplayName;

    public string Description => AzureDevOpsResources.Description;

    internal int? RunId => CurrentRunId;

    public void Dispose()
    {
        // Dispose must be idempotent: the platform can dispose an extension more than once during
        // teardown (e.g. once when the test host tears down its services and once again during
        // process shutdown). Without this guard the Cancel below throws ObjectDisposedException,
        // which surfaces as an unhandled exception and fails the run even though every test passed.
        if (Interlocked.Exchange(ref _isDisposed, 1) != 0)
        {
            return;
        }

        // Signal the background flush loop to stop. The loop is already awaited in
        // OnTestSessionFinishingAsync; this Cancel is a safety net for cases where the
        // session lifecycle methods are not called (e.g. early disposal in tests).
#pragma warning disable VSTHRD103 // CancelAsync is only available on .NET 8+; synchronous cancel is acceptable in Dispose.
        _backgroundFlushCts?.Cancel();
#pragma warning restore VSTHRD103
        _backgroundFlushCts?.Dispose();
        _flushSemaphore.Dispose();
    }

    // The extension stays enabled whenever the option is set so that a missing or invalid Azure DevOps
    // configuration can be reported to the user in OnTestSessionStartingAsync. Silently disabling the
    // extension here would leave users with no test run and no explanation for why.
    public Task<bool> IsEnabledAsync()
        => Task.FromResult(_commandLineOptions.IsOptionSet(AzureDevOpsCommandLineOptions.PublishAzureDevOpsTestResultsOptionName));

    public async Task OnTestSessionStartingAsync(ITestSessionContext testSessionContext)
    {
        if (!TryCreatePublishConfiguration(out AzureDevOpsPublishConfiguration? publishConfiguration, out string? warning))
        {
            await WarnAsync(warning, testSessionContext.CancellationToken).ConfigureAwait(false);
            return;
        }

        _publishConfiguration = publishConfiguration;
        _runIdCoordinator = new AzureDevOpsRunIdCoordinator(_fileSystem, _task, _clock, _environment, _logger, _options);

        try
        {
            _coordinatedRun = await _runIdCoordinator.AcquireRunAsync(
                publishConfiguration,
                cancellationToken => _client.CreateTestRunAsync(publishConfiguration, cancellationToken),
                testSessionContext.CancellationToken).ConfigureAwait(false);
            CurrentRunId = _coordinatedRun.RunId;

            // Start a background loop that flushes pending results on the time-based interval even
            // when no new TestNodeUpdateMessages arrive (e.g. at the tail end of a slow test run).
            _backgroundFlushCts = new CancellationTokenSource();
            _backgroundFlushTask = Task.Run(() => BackgroundFlushLoopAsync(_backgroundFlushCts.Token));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Reset state before reporting so a failure to display the warning can never leave the
            // publisher half-initialized.
            _publishConfiguration = null;
            _coordinatedRun = null;
            CurrentRunId = null;
            await WarnAsync($"{AzureDevOpsResources.AzureDevOpsLivePublishingCreateRunFailed} {ex.Message}", testSessionContext.CancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Reports a live-publishing problem both to the diagnostic log and to the output device, so that
    /// it is visible in CI logs (as an Azure DevOps warning) instead of only in an opt-in log file.
    /// </summary>
    /// <remarks>
    /// Never throws: this is a best-effort diagnostic invoked from error-recovery paths and from
    /// session teardown, where propagating would turn a warning into a failed run.
    /// </remarks>
    private async Task WarnAsync(string message, CancellationToken cancellationToken)
    {
        _logger.LogWarning(message);

        try
        {
            await _outputDevice.DisplayAsync(this, new WarningMessageOutputDeviceData(message), cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            // The message is already in the diagnostic log; losing the console copy is preferable to
            // failing the test run from inside a diagnostic helper.
            _logger.LogWarning($"{AzureDevOpsResources.AzureDevOpsLivePublishingWarningDisplayFailed} {ex.Message}");
        }
    }

    public async Task ConsumeAsync(IDataProducer dataProducer, IData value, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (_publishConfiguration is null || _runIdCoordinator is null || _coordinatedRun is null || CurrentRunId is null)
        {
            return;
        }

        try
        {
            switch (value)
            {
                case TestNodeUpdateMessage testNodeUpdateMessage:
                    AzureDevOpsTestCaseResultWithAttachments? testCaseResult = CreateTestCaseResult(testNodeUpdateMessage.TestNode, _publishConfiguration.AutomatedTestStorage);
                    if (testCaseResult is null)
                    {
                        return;
                    }

                    // Enqueue before renewing the lease: RenewLeaseAsync does file I/O that can throw
                    // (e.g. a sharing violation while another process reads the lease). If it threw
                    // first, the result would be dropped without ever reaching a queue, so the
                    // end-of-session "results dropped" count could not see it.
                    _pendingResults.Enqueue(testCaseResult);
                    await _runIdCoordinator.RenewLeaseAsync(_coordinatedRun, cancellationToken).ConfigureAwait(false);
                    await FlushPendingResultsAsync(force: false, cancellationToken).ConfigureAwait(false);
                    break;

                case SessionFileArtifact sessionFileArtifact when TryCreateRunAttachment(sessionFileArtifact) is { } runAttachment:
                    _pendingRunAttachments.Enqueue(runAttachment);
                    break;
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning($"{AzureDevOpsResources.AzureDevOpsLivePublishingPublishResultsFailed} {ex.Message}");
        }
    }

    public async Task OnTestSessionFinishingAsync(ITestSessionContext testSessionContext)
    {
        if (_publishConfiguration is null || _coordinatedRun is null || CurrentRunId is null || _runIdCoordinator is null)
        {
            return;
        }

        // Stop the background flush loop before doing the session-end forced flush so there is no
        // concurrent flush in flight when we drain the last batch.
        if (_backgroundFlushCts is not null && _backgroundFlushTask is not null)
        {
#pragma warning disable VSTHRD103 // CancelAsync is only available on .NET 8+; multi-target sync cancel is acceptable here.
            _backgroundFlushCts.Cancel();
#pragma warning restore VSTHRD103
            try
            {
                await _backgroundFlushTask.ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Unexpected failure in the background flush loop; the loop already logs per-flush warnings.
                _logger.LogWarning($"{AzureDevOpsResources.AzureDevOpsLivePublishingPublishResultsFailed} {ex.Message}");
            }
            catch
            {
                // Cancellation — expected and fine.
            }
        }

        try
        {
            await _runIdCoordinator.RenewLeaseAsync(_coordinatedRun, testSessionContext.CancellationToken).ConfigureAwait(false);
            await FlushPendingResultsAsync(force: true, testSessionContext.CancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Best-effort flush: session was canceled; finalization still runs below with a fresh token.
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning($"{AzureDevOpsResources.AzureDevOpsLivePublishingPublishResultsFailed} {ex.Message}");
        }

        // The session-end flush is the last chance to publish: nothing drains the retry queue after
        // this point, so anything still pending is permanently lost. Report it once here rather than
        // per failed batch (which the background loop would repeat every flush interval).
        // Use CancellationToken.None so the warning still surfaces when the session was canceled.
        int unpublishedResultCount = _retryResults.Count + _pendingResults.Count;
        if (unpublishedResultCount > 0)
        {
            await WarnAsync(
                string.Format(CultureInfo.InvariantCulture, AzureDevOpsResources.AzureDevOpsLivePublishingResultsDropped, unpublishedResultCount),
                CancellationToken.None).ConfigureAwait(false);
        }

        try
        {
            await UploadPendingRunAttachmentsAsync(testSessionContext.CancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Best-effort: don't fail finalization if the session was canceled mid-upload.
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Defensive: individual upload failures are already handled (and counted) per attachment,
            // so nothing is expected here. Swallow anyway because the platform does not guard lifetime
            // handlers, so throwing would fail an otherwise successful run.
            await WarnAsync($"{AzureDevOpsResources.AzureDevOpsLivePublishingRunAttachmentFailed} {ex.Message}", CancellationToken.None).ConfigureAwait(false);
        }

        // Attachment failures are swallowed per attachment so that one bad file cannot abort the drain.
        // Report them once here, otherwise coverage files and dumps go missing with no explanation.
        int failedAttachmentCount = Volatile.Read(ref _failedAttachmentCount);
        if (failedAttachmentCount > 0)
        {
            await WarnAsync(
                string.Format(CultureInfo.InvariantCulture, AzureDevOpsResources.AzureDevOpsLivePublishingAttachmentsDropped, failedAttachmentCount),
                CancellationToken.None).ConfigureAwait(false);
        }

        // Azure DevOps test runs use "Aborted" specifically for cancellation or session-level
        // infrastructure failures. Individual failing tests should still mark the run as
        // "Completed" — only treat process exit codes other than Success/AtLeastOneTestFailed as
        // an abort signal (e.g. TestSessionAborted, TestHostProcessExitedNonGracefully,
        // TestAdapterTestSessionFailure, MinimumExpectedTestsPolicyViolation, etc.).
        int exitCode = _testApplicationProcessExitCode.GetProcessExitCode();
        bool exitCodeIsTestResult = exitCode is (int)ExitCode.Success or (int)ExitCode.AtLeastOneTestFailed;
        string finalState = testSessionContext.CancellationToken.IsCancellationRequested
                || _testApplicationProcessExitCode.HasTestAdapterTestSessionFailure
                || !exitCodeIsTestResult
            ? AzureDevOpsLivePublishingConstants.AbortedTestRunState
            : AzureDevOpsLivePublishingConstants.CompletedTestRunState;

        try
        {
            // Use a fresh, non-canceled token so finalization (marking the run Aborted/Completed)
            // succeeds even when the test session itself has been canceled.
            using var cleanupCts = new CancellationTokenSource(_options.CoordinationFinalizeTimeout + TimeSpan.FromSeconds(60));
            await _runIdCoordinator.FinalizeRunAsync(
                _coordinatedRun,
                cancellationToken => _client.UpdateTestRunStateAsync(_publishConfiguration, CurrentRunId.Value, finalState, cancellationToken),
                cleanupCts.Token).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // A run left in "InProgress" is not surfaced in the build's Tests tab, so the user would
            // otherwise see an empty tab with no explanation even though every result was uploaded.
            await WarnAsync(
                $"{AzureDevOpsResources.AzureDevOpsLivePublishingCompleteRunFailed} {ex.Message} {AzureDevOpsResources.AzureDevOpsLivePublishingRunLeftInProgress}",
                CancellationToken.None).ConfigureAwait(false);
        }
    }
}
