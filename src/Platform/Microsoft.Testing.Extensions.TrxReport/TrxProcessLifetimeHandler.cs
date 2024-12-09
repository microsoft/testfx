// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Globalization;

using Microsoft.Testing.Extensions.TestReports.Resources;
using Microsoft.Testing.Extensions.TrxReport.Abstractions.Serializers;
using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Configurations;
using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Extensions.TestFramework;
using Microsoft.Testing.Platform.Extensions.TestHost;
using Microsoft.Testing.Platform.Extensions.TestHostControllers;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.IPC;
using Microsoft.Testing.Platform.IPC.Models;
using Microsoft.Testing.Platform.IPC.Serializers;
using Microsoft.Testing.Platform.Logging;
using Microsoft.Testing.Platform.Messages;
using Microsoft.Testing.Platform.Services;
using Microsoft.Testing.Platform.TestHost;

namespace Microsoft.Testing.Extensions.TrxReport.Abstractions;

internal sealed class TrxProcessLifetimeHandler :
    ITestHostProcessLifetimeHandler,
    IDataConsumer,
    IDataProducer,
#if NETCOREAPP
    IAsyncDisposable
#else
    IDisposable
#endif
{
    private readonly ICommandLineOptions _commandLineOptions;
    private readonly IEnvironment _environment;
    private readonly IMessageBus _messageBus;
    private readonly ITestApplicationModuleInfo _testApplicationModuleInfo;
    private readonly IConfiguration _configuration;
    private readonly IClock _clock;
    private readonly ITask _task;
    private readonly ILogger<TrxProcessLifetimeHandler> _logger;
    private readonly PipeNameDescription _pipeNameDescription;
    private readonly Dictionary<IDataProducer, List<FileArtifact>> _fileArtifacts = new();
    private readonly DateTimeOffset _startTime;

    private NamedPipeServer? _singleConnectionNamedPipeServer;
    private Task? _waitConnectionTask;
    private ReportFileNameRequest? _fileNameRequest;
    private TestAdapterInformationRequest? _testAdapterInformationRequest;

    public TrxProcessLifetimeHandler(
        ICommandLineOptions commandLineOptions,
        IEnvironment environment,
        ILoggerFactory loggerFactory,
        IMessageBus messageBus,
        ITestApplicationModuleInfo testApplicationModuleInfo,
        IConfiguration configuration,
        IClock clock,
        ITask task,
        PipeNameDescription pipeNameDescription)
    {
        _commandLineOptions = commandLineOptions;
        _environment = environment;
        _messageBus = messageBus;
        _testApplicationModuleInfo = testApplicationModuleInfo;
        _configuration = configuration;
        _clock = clock;
        _task = task;
        _pipeNameDescription = pipeNameDescription;
        _logger = loggerFactory.CreateLogger<TrxProcessLifetimeHandler>();
        _startTime = _clock.UtcNow;
    }

    public string Uid => nameof(TrxProcessLifetimeHandler);

    public string Version => AppVersion.DefaultSemVer;

    public string DisplayName => string.Empty;

    public string Description => string.Empty;

    public Type[] DataTypesConsumed => [typeof(FileArtifact)];

    public Type[] DataTypesProduced => [typeof(FileArtifact)];

    public Task<bool> IsEnabledAsync()
#pragma warning disable SA1114 // Parameter list should follow declaration
        => Task.FromResult(
           // TrxReportGenerator is enabled only when trx report is enabled
           _commandLineOptions.IsOptionSet(TrxReportGeneratorCommandLine.TrxReportOptionName)
           // TestController is not used when we run in server mode
           && !_commandLineOptions.IsOptionSet(PlatformCommandLineProvider.ServerOptionKey)
           // If crash dump is not enabled we run trx in-process only
           && _commandLineOptions.IsOptionSet(CrashDumpCommandLineOptions.CrashDumpOptionName));
#pragma warning restore SA1114 // Parameter list should follow declaration

    public Task BeforeTestHostProcessStartAsync(CancellationToken cancellation)
    {
        _waitConnectionTask = _task.Run(
            async () =>
            {
                _singleConnectionNamedPipeServer = new(_pipeNameDescription, CallbackAsync, _environment, _logger, _task, cancellation);
                _singleConnectionNamedPipeServer.RegisterSerializer(new ReportFileNameRequestSerializer(), typeof(ReportFileNameRequest));
                _singleConnectionNamedPipeServer.RegisterSerializer(new TestAdapterInformationRequestSerializer(), typeof(TestAdapterInformationRequest));
                _singleConnectionNamedPipeServer.RegisterSerializer(new VoidResponseSerializer(), typeof(VoidResponse));
                await _singleConnectionNamedPipeServer.WaitConnectionAsync(cancellation).TimeoutAfterAsync(TimeoutHelper.DefaultHangTimeSpanTimeout, cancellation);
            }, cancellation);

        return Task.CompletedTask;
    }

    public async Task OnTestHostProcessStartedAsync(ITestHostProcessInformation testHostProcessInformation, CancellationToken cancellation)
    {
        if (_waitConnectionTask is null)
        {
            throw new InvalidOperationException(ExtensionResources.TrxReportGeneratorBeforeTestHostProcessStartAsyncNotCalled);
        }

        await _waitConnectionTask.TimeoutAfterAsync(TimeoutHelper.DefaultHangTimeSpanTimeout, cancellation);
    }

    public Task ConsumeAsync(IDataProducer dataProducer, IData value, CancellationToken cancellationToken)
    {
        if (!_fileArtifacts.TryGetValue(dataProducer, out List<FileArtifact>? fileArtifacts))
        {
            fileArtifacts = [];
            _fileArtifacts.Add(dataProducer, fileArtifacts);
        }

        fileArtifacts.Add((FileArtifact)value);
        return Task.CompletedTask;
    }

    public async Task OnTestHostProcessExitedAsync(ITestHostProcessInformation testHostProcessInformation, CancellationToken cancellation)
    {
        if (cancellation.IsCancellationRequested)
        {
            return;
        }

        Dictionary<IExtension, List<SessionFileArtifact>> artifacts = new();

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

        // We create a trx with only files in case of test host process crash.
        if (!testHostProcessInformation.HasExitedGracefully)
        {
            TrxReportEngine trxReportGeneratorEngine = new(_testApplicationModuleInfo, _environment, _commandLineOptions, _configuration,
                _clock, [], 0, 0, 0, 0,
                artifacts,
                new Dictionary<TestNodeUid, List<SessionFileArtifact>>(),
                adapterSupportTrxCapability: null,
                new TestAdapterInfo(_testAdapterInformationRequest!.TestAdapterId, _testAdapterInformationRequest.TestAdapterVersion),
                _startTime,
                testHostProcessInformation.ExitCode,
                cancellation);

            await _messageBus.PublishAsync(
                this,
                new FileArtifact(
                    new FileInfo(await trxReportGeneratorEngine.GenerateReportAsync(
                        isTestHostCrashed: true,
                        testHostCrashInfo: $"Test host process pid: {testHostProcessInformation.PID} crashed.")),
                    ExtensionResources.TrxReportArtifactDisplayName,
                    ExtensionResources.TrxReportArtifactDescription));
            return;
        }

        if (_fileNameRequest is null)
        {
            throw ApplicationStateGuard.Unreachable();
        }

        var trxFile = new FileInfo(_fileNameRequest.FileName);

        // Add attachments to the trx.
        if (_fileArtifacts.Count > 0)
        {
            TrxReportEngine trxReportGeneratorEngine = new(_testApplicationModuleInfo, _environment, _commandLineOptions, _configuration,
               _clock, [], 0, 0, 0, 0,
               artifacts,
               new Dictionary<TestNodeUid, List<SessionFileArtifact>>(),
               false,
               new TestAdapterInfo(_testAdapterInformationRequest!.TestAdapterId, _testAdapterInformationRequest.TestAdapterVersion),
               _startTime,
               testHostProcessInformation.ExitCode,
               cancellation);

            await trxReportGeneratorEngine.AddArtifactsAsync(trxFile, artifacts);
        }

        await _messageBus.PublishAsync(this, new FileArtifact(trxFile, ExtensionResources.TrxReportArtifactDisplayName, ExtensionResources.TrxReportArtifactDescription));
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
        else
        {
            throw new ArgumentOutOfRangeException(string.Format(CultureInfo.InvariantCulture, ExtensionResources.UnsupportedRequestTypeErrorMessage, request.GetType().FullName));
        }
    }

#if NETCOREAPP
    public async ValueTask DisposeAsync()
    {
        await DisposeHelper.DisposeAsync(_singleConnectionNamedPipeServer);

        // Dispose the pipe descriptor after the server to ensure the pipe is closed.
        _pipeNameDescription?.Dispose();
    }
#else
    public void Dispose()
    {
        _singleConnectionNamedPipeServer?.Dispose();

        // Dispose the pipe descriptor after the server to ensure the pipe is closed.
        _pipeNameDescription?.Dispose();
    }
#endif

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
