// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.TrxReport.Abstractions.Serializers;
using Microsoft.Testing.Extensions.TrxReport.Abstractions.Streaming;
using Microsoft.Testing.Extensions.TrxReport.Resources;
using Microsoft.Testing.Platform.Capabilities.TestFramework;
using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Configurations;
using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Extensions.OutputDevice;
using Microsoft.Testing.Platform.Extensions.TestFramework;
using Microsoft.Testing.Platform.Extensions.TestHost;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.IPC.Models;
using Microsoft.Testing.Platform.Logging;
using Microsoft.Testing.Platform.Messages;
using Microsoft.Testing.Platform.OutputDevice;
using Microsoft.Testing.Platform.Services;

namespace Microsoft.Testing.Extensions.TrxReport.Abstractions;

internal sealed class TrxReportGenerator :
    IDataConsumer,
    ITestSessionLifetimeHandler,
    IDataProducer,
    IOutputDeviceDataProducer,
    IDisposable
{
    private readonly IConfiguration _configuration;
    private readonly ICommandLineOptions _commandLineOptionsService;
    private readonly IFileSystem _fileSystem;
    private readonly ITestApplicationModuleInfo _testApplicationModuleInfo;
    private readonly IMessageBus _messageBus;
    private readonly IClock _clock;
    private readonly IEnvironment _environment;
    private readonly IOutputDevice _outputDisplay;
    private readonly ITestFramework _testFramework;
    private readonly ITestFrameworkCapabilities _testFrameworkCapabilities;
    private readonly ITestApplicationProcessExitCode _testApplicationProcessExitCode;
    private readonly ITask _task;
    private readonly TrxTestApplicationLifecycleCallbacks? _trxTestApplicationLifecycleCallbacks;
    private readonly ILogger<TrxReportGenerator> _logger;
    private readonly Dictionary<IExtension, List<SessionFileArtifact>> _artifactsByExtension = [];
    private readonly bool _isEnabled;
    private TrxResultStreamingStore? _streamingStore;
    private bool _crashRecoveryUnavailable;
    private int _truncatedFieldCount;

    private DateTimeOffset? _testStartTime;
    private bool _adapterSupportTrxCapability;

    public TrxReportGenerator(
        IConfiguration configuration,
        ICommandLineOptions commandLineOptionsService,
        IFileSystem fileSystem,
        ITestApplicationModuleInfo testApplicationModuleInfo,
        IMessageBus messageBus,
        IClock clock,
        IEnvironment environment,
        IOutputDevice outputDisplay,
        ITestFramework testFramework,
        ITestFrameworkCapabilities testFrameworkCapabilities,
        ITestApplicationProcessExitCode testApplicationProcessExitCode,
        ITask task,
        // Can be null in case of server mode
        TrxTestApplicationLifecycleCallbacks? trxTestApplicationLifecycleCallbacks,
        ILogger<TrxReportGenerator> logger)
    {
        _configuration = configuration;
        _commandLineOptionsService = commandLineOptionsService;
        _fileSystem = fileSystem;
        _testApplicationModuleInfo = testApplicationModuleInfo;
        _messageBus = messageBus;
        _clock = clock;
        _environment = environment;
        _outputDisplay = outputDisplay;
        _testFramework = testFramework;
        _testFrameworkCapabilities = testFrameworkCapabilities;
        _testApplicationProcessExitCode = testApplicationProcessExitCode;
        _task = task;
        _trxTestApplicationLifecycleCallbacks = trxTestApplicationLifecycleCallbacks;
        _logger = logger;
        _isEnabled = commandLineOptionsService.IsOptionSet(TrxReportGeneratorCommandLine.TrxReportOptionName);
    }

    public Type[] DataTypesConsumed { get; } =
    [
        typeof(TestNodeUpdateMessage),
        typeof(SessionFileArtifact)
    ];

    public Type[] DataTypesProduced { get; } = [typeof(SessionFileArtifact)];

    /// <inheritdoc />
    public string Uid => nameof(TrxReportGenerator);

    /// <inheritdoc />
    public string Version => ExtensionVersion.DefaultSemVer;

    /// <inheritdoc />
    public string DisplayName { get; } = ExtensionResources.TrxReportGeneratorDisplayName;

    /// <inheritdoc />
    public string Description { get; } = ExtensionResources.TrxReportGeneratorDescription;

    /// <inheritdoc />
    public Task<bool> IsEnabledAsync() => Task.FromResult(_isEnabled);

    public async Task ConsumeAsync(IDataProducer dataProducer, IData value, CancellationToken cancellationToken)
    {
        // This is only run in TestHost, and not TestHostController.
        cancellationToken.ThrowIfCancellationRequested();

        switch (value)
        {
            case TestNodeUpdateMessage nodeChangedMessage:
                TestNodeStateProperty? nodeState = nodeChangedMessage.TestNode.Properties.SingleOrDefault<TestNodeStateProperty>();
                if (nodeState is null or DiscoveredTestNodeStateProperty or InProgressTestNodeStateProperty)
                {
                    return;
                }

                await EnsureStreamingStoreCreatedAsync(cancellationToken).ConfigureAwait(false);
                (TrxTestResult extracted, bool wasTruncated) = TrxTestResultExtractor.Extract(nodeChangedMessage);
                if (wasTruncated)
                {
                    Interlocked.Increment(ref _truncatedFieldCount);
                }

                Volatile.Read(ref _streamingStore)!.Enqueue(extracted);

                break;

            case SessionFileArtifact fileArtifact:
                if (!_artifactsByExtension.TryGetValue(dataProducer, out List<SessionFileArtifact>? sessionFileArtifacts))
                {
                    sessionFileArtifacts = [fileArtifact];
                    _artifactsByExtension[dataProducer] = sessionFileArtifacts;
                }
                else
                {
                    sessionFileArtifacts.Add(fileArtifact);
                }

                break;
        }
    }

    public async Task OnTestSessionStartingAsync(ITestSessionContext testSessionContext)
    {
        // This is only run in TestHost, and not TestHostController.
        CancellationToken cancellationToken = testSessionContext.CancellationToken;
        cancellationToken.ThrowIfCancellationRequested();

        bool shouldUseOutOfProcessTrxGeneration = TrxModeHelpers.ShouldUseOutOfProcessTrxGeneration(_commandLineOptionsService);
        if (_logger.IsEnabled(LogLevel.Debug))
        {
            await _logger.LogDebugAsync($"""
CrashDumpCommandLineOptions.CrashDumpOptionName: {_commandLineOptionsService.IsOptionSet(CrashDumpCommandLineOptions.CrashDumpOptionName)}
TrxReportGeneratorCommandLine.IsTrxReportEnabled: {_commandLineOptionsService.IsOptionSet(TrxReportGeneratorCommandLine.TrxReportOptionName)}
shouldUseOutOfProcessTrxGeneration: {shouldUseOutOfProcessTrxGeneration}
""").ConfigureAwait(false);
        }

        if (shouldUseOutOfProcessTrxGeneration)
        {
            ApplicationStateGuard.Ensure(_trxTestApplicationLifecycleCallbacks is not null);
            ApplicationStateGuard.Ensure(_trxTestApplicationLifecycleCallbacks.NamedPipeClient is not null);

            // This tells the TestHostController process the info of the registered ITestFramework.
            // The TestHostController needs that info to create the TrxReportEngine so that the info are written to the TRX file.
            // Note: TestHostController cannot retrieve ITestFramework from service provider which is why we need the extra complexity here.
            // It would be worth investigating whether TestHostController can/should have access to ITestFramework to simplify this.
            await _trxTestApplicationLifecycleCallbacks.NamedPipeClient.RequestReplyAsync<TestAdapterInformationRequest, VoidResponse>(new TestAdapterInformationRequest(_testFramework.Uid, _testFramework.Version), cancellationToken)
                .TimeoutAfterAsync(TimeoutHelper.DefaultHangTimeSpanTimeout, cancellationToken).ConfigureAwait(false);
        }

        ITrxReportCapability? trxCapability = _testFrameworkCapabilities.GetCapability<ITrxReportCapability>();
        if (trxCapability is not null && trxCapability.IsSupported)
        {
            _adapterSupportTrxCapability = true;
            trxCapability.Enable();
        }

        _testStartTime = _clock.UtcNow;
    }

    public async Task OnTestSessionFinishingAsync(ITestSessionContext testSessionContext)
    {
        // This is only run in TestHost, and not TestHostController.
        // The current implementation tries to keep the TRX generation logic always in-process.
        // However, in case of a crash, the TestHostController takes over this responsibility (when running in out-of-proc mode).
        CancellationToken cancellationToken = testSessionContext.CancellationToken;
        cancellationToken.ThrowIfCancellationRequested();

        if (!_adapterSupportTrxCapability)
        {
            await _outputDisplay.DisplayAsync(this, new WarningMessageOutputDeviceData(string.Format(CultureInfo.InvariantCulture, ExtensionResources.TrxReportFrameworkDoesNotSupportTrxReportCapability, _testFramework.DisplayName, _testFramework.Uid)), testSessionContext.CancellationToken).ConfigureAwait(false);
        }

        ApplicationStateGuard.Ensure(_testStartTime is not null);

        int exitCode = _testApplicationProcessExitCode.GetProcessExitCode();
        var trxReportGeneratorEngine = new TrxReportEngine(
            _fileSystem,
            _testApplicationModuleInfo,
            _environment,
            _commandLineOptionsService,
            _configuration,
            _clock,
            _artifactsByExtension,
            _testFramework,
            _testStartTime.Value,
#if NETCOREAPP
            exitCode,
            cancellationToken);
#else
            exitCode);
#endif

        (string reportFileName, string? warning) = await GenerateReportAndCleanupAsync(trxReportGeneratorEngine, cancellationToken).ConfigureAwait(false);
        if (warning is not null)
        {
            await _outputDisplay.DisplayAsync(this, new WarningMessageOutputDeviceData(warning), testSessionContext.CancellationToken).ConfigureAwait(false);
        }

        if (_crashRecoveryUnavailable)
        {
            // Surface this on the output device too — a single Warning log line at first-result time is
            // easy to miss, and users only learn the consequence (empty TRX after a crash) much later.
            await _outputDisplay.DisplayAsync(
                this,
                new WarningMessageOutputDeviceData("TRX streaming store could not notify the test host controller of its location; if the test host crashes during this run, the TRX will not contain partial results."),
                testSessionContext.CancellationToken).ConfigureAwait(false);
        }

        if (Volatile.Read(ref _truncatedFieldCount) > 0)
        {
            // Tell the user (not just the log) that captured text has been truncated. This is a
            // user-observable behavior change vs. the legacy in-memory TRX path so it must be visible.
            await _outputDisplay.DisplayAsync(
                this,
                new WarningMessageOutputDeviceData($"TRX streaming store truncated captured text fields (stdout / stderr / exception messages / stack traces) on {Volatile.Read(ref _truncatedFieldCount)} test result(s) because individual fields exceeded the per-field size cap. The truncated regions are marked '[truncated by TRX streaming store]' in the TRX."),
                testSessionContext.CancellationToken).ConfigureAwait(false);
        }

        // TRX can run in two modes. In-process or out-of-process.
        // If we are already running with the in-process mode, we publish the SessionFileArtifact to the message bus directly.
        // If we are running with out-of-process mode, we communicate via pipe to the TestHostController and send the ReportFileNameRequest.
        if (!TrxModeHelpers.ShouldUseOutOfProcessTrxGeneration(_commandLineOptionsService))
        {
            await _messageBus.PublishAsync(this, new SessionFileArtifact(testSessionContext.SessionUid, new FileInfo(reportFileName), ExtensionResources.TrxReportArtifactDisplayName, ExtensionResources.TrxReportArtifactDescription, "microsoft.testing.trx")).ConfigureAwait(false);
        }
        else
        {
            // The TestHostController will receive the TRX file name.
            // Then, it will **modify** it and add any additional artifacts that were produced by TestHostController.
            ApplicationStateGuard.Ensure(_trxTestApplicationLifecycleCallbacks is not null);
            ApplicationStateGuard.Ensure(_trxTestApplicationLifecycleCallbacks.NamedPipeClient is not null);
            await _trxTestApplicationLifecycleCallbacks.NamedPipeClient.RequestReplyAsync<ReportFileNameRequest, VoidResponse>(new ReportFileNameRequest(reportFileName), cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task EnsureStreamingStoreCreatedAsync(CancellationToken cancellationToken)
    {
        // Safe non-atomic check-then-create: AsyncConsumerDataProcessor serializes ConsumeAsync calls
        // per IDataConsumer instance, so this method is only invoked from one task at a time.
        // Use Volatile.Read so a publish in this method is observable to OnTestSessionFinishingAsync,
        // which runs on a different task once the message bus drain completes.
        if (Volatile.Read(ref _streamingStore) is not null)
        {
            return;
        }

        // First test result observed: provision the sidecar lazily so test runs that don't enable TRX or
        // produce no results don't pay the file-creation cost.
        string fileName = $"trx-stream-{Guid.NewGuid():N}.bin";
        string filePath = Path.Combine(_configuration.GetTestResultDirectory(), fileName);
        var store = new TrxResultStreamingStore(filePath, _fileSystem, _task, _logger);

        // In out-of-process mode, tell the controller where the sidecar lives BEFORE we publish the
        // store and start enqueuing into it. If the IPC is cancelled we tear the store down so we don't
        // leak the writer thread + file handle when OnTestSessionFinishingAsync never runs (which is
        // exactly what cancellation triggers in the platform).
        if (TrxModeHelpers.ShouldUseOutOfProcessTrxGeneration(_commandLineOptionsService))
        {
            ApplicationStateGuard.Ensure(_trxTestApplicationLifecycleCallbacks is not null);
            ApplicationStateGuard.Ensure(_trxTestApplicationLifecycleCallbacks.NamedPipeClient is not null);

            try
            {
                await _trxTestApplicationLifecycleCallbacks.NamedPipeClient.RequestReplyAsync<TrxStreamLocationRequest, VoidResponse>(
                    new TrxStreamLocationRequest(filePath), cancellationToken)
                    .TimeoutAfterAsync(TimeoutHelper.DefaultHangTimeSpanTimeout, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                store.Dispose();
                throw;
            }
            catch (Exception ex)
            {
                // IPC failure is best-effort — keep the streaming store running so the in-process happy
                // path still records to disk and produces a complete TRX. Crash recovery will be
                // unavailable for this run; we set a flag so OnTestSessionFinishingAsync can surface it
                // on the output device (logs alone are easy to miss).
                _crashRecoveryUnavailable = true;
                await _logger.LogWarningAsync($"Failed to notify the test host controller of the TRX streaming store location; crash recovery will be unavailable. Reason: {ex.Message}").ConfigureAwait(false);
            }
        }

        Volatile.Write(ref _streamingStore, store);
    }

    private async Task<(string FileName, string? Warning)> GenerateReportAndCleanupAsync(TrxReportEngine engine, CancellationToken cancellationToken)
    {
        TrxResultStreamingStore? store = Volatile.Read(ref _streamingStore);
        IReadOnlyList<TrxTestResult> testResults;
        if (store is null)
        {
            testResults = [];
        }
        else
        {
            await store.CompleteAsync(cancellationToken).ConfigureAwait(false);
            testResults = store.ReadAll();
            int dropped = store.DroppedCount;
            if (dropped > 0)
            {
                await _logger.LogWarningAsync($"TRX streaming store dropped {dropped} record(s) during the session; the generated TRX will be incomplete.").ConfigureAwait(false);
            }
        }

        try
        {
            return await engine.GenerateReportAsync(testResults).ConfigureAwait(false);
        }
        finally
        {
            // Dispose first so the writer thread releases the file handle, then delete. With FileShare.Read
            // the file cannot be deleted on Windows while the writer still owns it, so a swapped order
            // would silently leak the sidecar after a CompleteAsync timeout.
            store?.Dispose();

            // If completion timed out the writer may still hold records that didn't make it into the TRX
            // we just generated. Keep the file around for out-of-band recovery (and so a follow-up run
            // doesn't claim the TRX is complete when it isn't). The orphan file is harmless: a fresh run
            // creates a new GUID-named sidecar, and the next clean shutdown will delete it.
            if (store is not null && !store.CompletionTimedOut)
            {
                store.TryDelete();
            }

            Volatile.Write(ref _streamingStore, null);
        }
    }

    public void Dispose()
    {
        // Defensive cleanup: GenerateReportAndCleanupAsync is the primary cleanup path.
        // Dispose runs only if the host shuts down before OnTestSessionFinishingAsync completes
        // (e.g. cancellation) — at that point we just need to release the writer thread and file handle.
        TrxResultStreamingStore? store = Interlocked.Exchange(ref _streamingStore, null);
        store?.Dispose();
    }
}
