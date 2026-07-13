// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.TrxReport.Abstractions.Serializers;
using Microsoft.Testing.Extensions.TrxReport.Abstractions.Streaming;
using Microsoft.Testing.Extensions.TrxReport.Resources;
using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Configurations;
using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Extensions.OutputDevice;
using Microsoft.Testing.Platform.Extensions.TestFramework;
using Microsoft.Testing.Platform.Extensions.TestHostControllers;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.IPC;
using Microsoft.Testing.Platform.IPC.Models;
using Microsoft.Testing.Platform.IPC.Serializers;
using Microsoft.Testing.Platform.Logging;
using Microsoft.Testing.Platform.Messages;
using Microsoft.Testing.Platform.OutputDevice;
using Microsoft.Testing.Platform.Services;
using Microsoft.Testing.Platform.TestHost;

namespace Microsoft.Testing.Extensions.TrxReport.Abstractions;

internal sealed class TrxProcessLifetimeHandler :
    ITestHostProcessLifetimeHandler,
    IDataConsumer,
    IDataProducer,
    IOutputDeviceDataProducer,
#if NETCOREAPP
    IAsyncDisposable,
#endif
    IDisposable
{
    private readonly ICommandLineOptions _commandLineOptions;
    private readonly IEnvironment _environment;
    private readonly IMessageBus _messageBus;
    private readonly IFileSystem _fileSystem;
    private readonly ITestApplicationModuleInfo _testApplicationModuleInfo;
    private readonly IConfiguration _configuration;
    private readonly IClock _clock;
    private readonly ITask _task;
    private readonly IOutputDevice _outputDevice;
    private readonly ILogger<TrxProcessLifetimeHandler> _logger;
    private readonly PipeNameDescription _pipeNameDescription;
    private readonly Dictionary<IDataProducer, List<FileArtifact>> _fileArtifacts = [];
    private readonly DateTimeOffset _startTime;

    private NamedPipeServer? _singleConnectionNamedPipeServer;
    private Task? _waitConnectionTask;
    private ReportFileNameRequest? _fileNameRequest;
    private TestAdapterInformationRequest? _testAdapterInformationRequest;
    private TrxStreamLocationRequest? _streamLocationRequest;

    public TrxProcessLifetimeHandler(
        ICommandLineOptions commandLineOptions,
        IEnvironment environment,
        ILoggerFactory loggerFactory,
        IMessageBus messageBus,
        IFileSystem fileSystem,
        ITestApplicationModuleInfo testApplicationModuleInfo,
        IConfiguration configuration,
        IClock clock,
        ITask task,
        IOutputDevice outputDevice,
        PipeNameDescription pipeNameDescription)
    {
        _commandLineOptions = commandLineOptions;
        _environment = environment;
        _messageBus = messageBus;
        _fileSystem = fileSystem;
        _testApplicationModuleInfo = testApplicationModuleInfo;
        _configuration = configuration;
        _clock = clock;
        _task = task;
        _outputDevice = outputDevice;
        _pipeNameDescription = pipeNameDescription;
        _logger = loggerFactory.CreateLogger<TrxProcessLifetimeHandler>();
        _startTime = _clock.UtcNow;
    }

    public string Uid => nameof(TrxProcessLifetimeHandler);

    public string Version => ExtensionVersion.DefaultSemVer;

    public string DisplayName => string.Empty;

    public string Description => string.Empty;

    public Type[] DataTypesConsumed => [typeof(FileArtifact)];

    public Type[] DataTypesProduced => [typeof(FileArtifact)];

    public Task<bool> IsEnabledAsync()
#pragma warning disable SA1114 // Parameter list should follow declaration
        => Task.FromResult(
           // TrxReportGenerator is enabled only when trx report is enabled
           _commandLineOptions.IsOptionSet(TrxReportGeneratorCommandLine.TrxReportOptionName)
           // If crash dump is not enabled we run trx in-process only
           && TrxModeHelpers.ShouldUseOutOfProcessTrxGeneration(_commandLineOptions));
#pragma warning restore SA1114 // Parameter list should follow declaration

    public Task BeforeTestHostProcessStartAsync(CancellationToken cancellationToken)
    {
        // IsEnabledAsync will only return true if we are out of process.
        // If we are not out of process, then we are disabled. Hence, this won't be called.
        // The extra check is to let the platform compatibility analyzer know that we are not running in browser.
        if (!TrxModeHelpers.ShouldUseOutOfProcessTrxGeneration(_commandLineOptions))
        {
            throw ApplicationStateGuard.Unreachable();
        }

        // Note: Inlining this method produces a false positive warning for platform compatibility.
        BeforeTestHostProcessStartCore(cancellationToken);

        return Task.CompletedTask;
    }

    [UnsupportedOSPlatform("BROWSER")]
    private void BeforeTestHostProcessStartCore(CancellationToken cancellationToken)
        => _waitConnectionTask = _task.Run(
            async () =>
            {
                _singleConnectionNamedPipeServer = new(_pipeNameDescription, CallbackAsync, _environment, _logger, _task, cancellationToken);
                _singleConnectionNamedPipeServer.RegisterSerializer(new ReportFileNameRequestSerializer(), typeof(ReportFileNameRequest));
                _singleConnectionNamedPipeServer.RegisterSerializer(new TestAdapterInformationRequestSerializer(), typeof(TestAdapterInformationRequest));
                _singleConnectionNamedPipeServer.RegisterSerializer(new TrxStreamLocationRequestSerializer(), typeof(TrxStreamLocationRequest));
                _singleConnectionNamedPipeServer.RegisterSerializer(new VoidResponseSerializer(), typeof(VoidResponse));
                await _singleConnectionNamedPipeServer.WaitConnectionAsync(cancellationToken).TimeoutAfterAsync(TimeoutHelper.DefaultHangTimeSpanTimeout, cancellationToken).ConfigureAwait(false);
            }, cancellationToken);

    public async Task OnTestHostProcessStartedAsync(ITestHostProcessInformation testHostProcessInformation, CancellationToken cancellationToken)
    {
        if (_waitConnectionTask is null)
        {
            throw new InvalidOperationException(ExtensionResources.TrxReportGeneratorBeforeTestHostProcessStartAsyncNotCalled);
        }

        await _waitConnectionTask.TimeoutAfterAsync(TimeoutHelper.DefaultHangTimeSpanTimeout, cancellationToken).ConfigureAwait(false);
    }

    public Task ConsumeAsync(IDataProducer dataProducer, IData value, CancellationToken cancellationToken)
    {
        // This is only run in TestHostController.
        // We group artifacts by producer.
        // The scenario is:
        // 1. On graceful exit, TestHost will already have written the TRX file.
        // 2. The TRX written by TestHost does not include artifacts published in TestHostController.
        // We address this by tracking those artifacts in TestHostController and then modifying the TRX file
        // written by TestHost so that it also includes those artifacts.
        if (!_fileArtifacts.TryGetValue(dataProducer, out List<FileArtifact>? fileArtifacts))
        {
            fileArtifacts = [];
            _fileArtifacts.Add(dataProducer, fileArtifacts);
        }

        fileArtifacts.Add((FileArtifact)value);
        return Task.CompletedTask;
    }

    public async Task OnTestHostProcessExitedAsync(ITestHostProcessInformation testHostProcessInformation, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        Dictionary<IExtension, List<SessionFileArtifact>> artifacts = [];

        foreach (KeyValuePair<IDataProducer, List<FileArtifact>> prodArtifacts in _fileArtifacts)
        {
            var perProducerArtifact = new List<SessionFileArtifact>(prodArtifacts.Value.Count);
            var extensionInfo = new ExtensionInfo(prodArtifacts.Key.Uid, prodArtifacts.Key.Version, prodArtifacts.Key.DisplayName, prodArtifacts.Key.Description);
            foreach (FileArtifact fileArtifact in prodArtifacts.Value)
            {
                perProducerArtifact.Add(new SessionFileArtifact(new SessionUid(Guid.Empty.ToString()), fileArtifact.FileInfo, fileArtifact.DisplayName, fileArtifact.Description));
            }

            artifacts.Add(extensionInfo, perProducerArtifact);
        }

        // If _fileNameRequest is null, that means that the TestHost crashed before it wrote the TRX file.
        if (_fileNameRequest is null)
        {
            // Crash recovery: if the test host had time to provision the streaming sidecar and notify
            // us of its location, attempt to read the durably-written records back so the TRX is
            // partial-but-useful instead of empty. Failures here must NEVER prevent us from emitting
            // the (at least empty) crash TRX, so we trap and log everything.
            IReadOnlyList<TrxTestResult> recoveredResults = await TryRecoverStreamingResultsAsync(cancellationToken).ConfigureAwait(false);

            var trxReportGeneratorEngine = new TrxReportEngine(
                _fileSystem,
                _testApplicationModuleInfo,
                _environment,
                _commandLineOptions,
                _configuration,
                _clock,
                artifacts,
                new TestAdapterInfo(_testAdapterInformationRequest!.TestAdapterId, _testAdapterInformationRequest.TestAdapterVersion),
                _startTime,
#if NETCOREAPP
                testHostProcessInformation.ExitCode,
                cancellationToken);
#else
                testHostProcessInformation.ExitCode);
#endif

            (string fileName, string? warning) = await trxReportGeneratorEngine.GenerateReportAsync(
                recoveredResults,
                isTestHostCrashed: true,
                testHostCrashInfo: $"Test host process pid: {testHostProcessInformation.PID} crashed.").ConfigureAwait(false);
            if (warning is not null)
            {
                await _outputDevice.DisplayAsync(this, new WarningMessageOutputDeviceData(warning), cancellationToken).ConfigureAwait(false);
            }

            // Tell the user how many records survived the crash. Without this they have to grep the TRX
            // (or the controller logs) to know whether recovery did anything useful.
            string recoverySummary = recoveredResults.Count == 0
                ? "Test host crashed and no test results could be recovered from the TRX streaming sidecar; the TRX is empty."
                : $"Test host crashed; recovered {recoveredResults.Count} test result(s) from the TRX streaming sidecar (additional results that were in flight at crash time may be missing).";
            await _outputDevice.DisplayAsync(this, new WarningMessageOutputDeviceData(recoverySummary), cancellationToken).ConfigureAwait(false);

            await _messageBus.PublishAsync(
                this,
                new FileArtifact(
                    new FileInfo(fileName),
                    ExtensionResources.TrxReportArtifactDisplayName,
                    ExtensionResources.TrxReportArtifactDescription,
                    TrxReportEngine.TrxArtifactKind)).ConfigureAwait(false);

            TryDeleteStreamingSidecar();
            return;
        }

        // Tracked by https://github.com/microsoft/testfx/issues/8086:
        // If the current TRX file is indicating a success status while
        // testHostProcessInformation.ExitCode indicates non-success, then that must be
        // a crash after TRX was written.
        // In that case, we should update the TRX file to indicate non-success.
        var trxFile = new FileInfo(_fileNameRequest.FileName);

        // Add attachments to the trx.
        if (_fileArtifacts.Count > 0)
        {
            var trxReportGeneratorEngine = new TrxReportEngine(
                _fileSystem,
                _testApplicationModuleInfo,
                _environment,
                _commandLineOptions,
                _configuration,
                _clock,
                artifacts,
                new TestAdapterInfo(_testAdapterInformationRequest!.TestAdapterId, _testAdapterInformationRequest.TestAdapterVersion),
                _startTime,
#if NETCOREAPP
                testHostProcessInformation.ExitCode,
                cancellationToken);
#else
                testHostProcessInformation.ExitCode);
#endif

            await trxReportGeneratorEngine.AddArtifactsAsync(trxFile, artifacts).ConfigureAwait(false);
        }

        await _messageBus.PublishAsync(this, new FileArtifact(trxFile, ExtensionResources.TrxReportArtifactDisplayName, ExtensionResources.TrxReportArtifactDescription, TrxReportEngine.TrxArtifactKind)).ConfigureAwait(false);

        // Best-effort orphan cleanup. On the happy path the test host normally deletes its own
        // sidecar in TrxReportGenerator.GenerateReportAndCleanupAsync, but if its CompleteAsync timed
        // out it intentionally leaves the file behind for recovery. Once the controller has the final
        // TRX in hand, the sidecar is no longer useful — sweep it so repeated CI runs don't accumulate
        // stale files in the test results directory.
        TryDeleteStreamingSidecar();
    }

    private Task<IResponse> CallbackAsync(IRequest request)
    {
        if (request is ReportFileNameRequest report)
        {
            _fileNameRequest = report;
            return Task.FromResult<IResponse>(VoidResponse.CachedInstance);
        }
        else if (request is TestAdapterInformationRequest testAdapterInformationRequest)
        {
            _testAdapterInformationRequest = testAdapterInformationRequest;
            return Task.FromResult<IResponse>(VoidResponse.CachedInstance);
        }
        else if (request is TrxStreamLocationRequest streamLocationRequest)
        {
            _streamLocationRequest = streamLocationRequest;
            return Task.FromResult<IResponse>(VoidResponse.CachedInstance);
        }
        else
        {
            throw new ArgumentOutOfRangeException(string.Format(CultureInfo.InvariantCulture, ExtensionResources.UnsupportedRequestTypeErrorMessage, request.GetType().FullName));
        }
    }

    private async Task<IReadOnlyList<TrxTestResult>> TryRecoverStreamingResultsAsync(CancellationToken cancellationToken)
    {
        if (_streamLocationRequest is null)
        {
            return [];
        }

        string filePath = _streamLocationRequest.FilePath;
        if (!_fileSystem.ExistFile(filePath))
        {
            return [];
        }

        var results = new List<TrxTestResult>();
        try
        {
            // FileShare.ReadWrite so AV scanners or transient writer-side handles do not block recovery.
            using IFileStream stream = _fileSystem.NewFileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);

            // Iterate manually so a failure mid-stream still salvages the records we already pulled.
            // ReadAll is designed to stop cleanly on truncated/corrupt tails (it does not throw) but
            // a UTF-8 decode error inside an otherwise-valid record would surface as an exception
            // — without this loop we'd lose the entire prefix.
            using IEnumerator<TrxTestResult> enumerator = TrxTestResultSerializer.ReadAll(stream.Stream, _logger).GetEnumerator();
            while (true)
            {
                try
                {
                    if (!enumerator.MoveNext())
                    {
                        break;
                    }
                }
                catch (Exception ex)
                {
                    await _logger.LogErrorAsync(
                        $"Encountered an error while recovering TRX streaming records; salvaged {results.Count} record(s) before stopping.",
                        ex).ConfigureAwait(false);
                    break;
                }

                results.Add(enumerator.Current);
            }

            await _logger.LogInformationAsync($"Recovered {results.Count} test result(s) from TRX streaming sidecar after test host crash.").ConfigureAwait(false);
            return results;
        }
        catch (Exception ex)
        {
            await _logger.LogErrorAsync($"Failed to open TRX streaming sidecar '{filePath}' for recovery. Salvaged {results.Count} record(s) before failure.", ex).ConfigureAwait(false);
            return results;
        }
    }

    private void TryDeleteStreamingSidecar()
    {
        if (_streamLocationRequest is null)
        {
            return;
        }

        try
        {
            if (_fileSystem.ExistFile(_streamLocationRequest.FilePath))
            {
                _fileSystem.DeleteFile(_streamLocationRequest.FilePath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug($"Failed to delete TRX streaming sidecar '{_streamLocationRequest.FilePath}': {ex.Message}");
        }
    }

#if NETCOREAPP
    public async ValueTask DisposeAsync()
        => await DisposeHelper.DisposeAsync(_singleConnectionNamedPipeServer).ConfigureAwait(false);
#endif

    public void Dispose()
    {
        if (TrxModeHelpers.ShouldUseOutOfProcessTrxGeneration(_commandLineOptions))
        {
            _singleConnectionNamedPipeServer?.Dispose();
        }
    }

    private sealed class ExtensionInfo : IExtension
    {
        public ExtensionInfo(string id, string semVer, string displayName, string description)
        {
            Uid = id;
            Version = semVer;
            DisplayName = displayName;
            Description = description;
        }

        public string Uid { get; }

        public string Version { get; }

        public string DisplayName { get; }

        public string Description { get; }

        public Task<bool> IsEnabledAsync() => throw new NotImplementedException();
    }

    private sealed class TestAdapterInfo : ITestFramework
    {
        public TestAdapterInfo(string id, string semVer)
        {
            Uid = id;
            Version = semVer;
        }

        public string Uid { get; }

        public string Version { get; }

        public string DisplayName => throw new NotImplementedException();

        public string Description => throw new NotImplementedException();

        public Task<bool> IsEnabledAsync() => throw new NotImplementedException();

        public Task<CloseTestSessionResult> CloseTestSessionAsync(CloseTestSessionContext context) => throw new NotImplementedException();

        public Task<CreateTestSessionResult> CreateTestSessionAsync(CreateTestSessionContext context) => throw new NotImplementedException();

        public Task ExecuteRequestAsync(ExecuteRequestContext context) => throw new NotImplementedException();
    }
}
