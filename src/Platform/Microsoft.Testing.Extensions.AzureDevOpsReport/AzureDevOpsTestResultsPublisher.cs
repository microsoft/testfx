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
            await WarnAsync(warning ?? AzureDevOpsResources.AzureDevOpsLivePublishingMissingConfiguration, testSessionContext.CancellationToken).ConfigureAwait(false);
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
            await WarnAsync($"{AzureDevOpsResources.AzureDevOpsLivePublishingCreateRunFailed} {ex.Message}", testSessionContext.CancellationToken).ConfigureAwait(false);
            _publishConfiguration = null;
            _coordinatedRun = null;
            CurrentRunId = null;
        }
    }

    private async Task WarnAsync(string message, CancellationToken cancellationToken)
    {
        _logger.LogWarning(message);

        try
        {
            await _outputDevice.DisplayAsync(this, new WarningMessageOutputDeviceData(message), cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // The session is tearing down; the warning is already in the log file.
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

                    await _runIdCoordinator.RenewLeaseAsync(_coordinatedRun, cancellationToken).ConfigureAwait(false);
                    _pendingResults.Enqueue(testCaseResult);
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
            _logger.LogWarning($"{AzureDevOpsResources.AzureDevOpsLivePublishingRunAttachmentFailed} {ex.Message}");
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
            _logger.LogWarning($"{AzureDevOpsResources.AzureDevOpsLivePublishingCompleteRunFailed} {ex.Message}");
        }
    }
}
