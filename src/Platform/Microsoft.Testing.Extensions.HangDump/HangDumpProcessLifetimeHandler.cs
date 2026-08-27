// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.Diagnostics.Resources;
using Microsoft.Testing.Extensions.HangDump.Serializers;
using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Configurations;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Extensions.OutputDevice;
using Microsoft.Testing.Platform.Extensions.TestHostControllers;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.IPC;
using Microsoft.Testing.Platform.IPC.Models;
using Microsoft.Testing.Platform.IPC.Serializers;
using Microsoft.Testing.Platform.Logging;
using Microsoft.Testing.Platform.Messages;
using Microsoft.Testing.Platform.OutputDevice;

namespace Microsoft.Testing.Extensions.Diagnostics;

[UnsupportedOSPlatform("browser")]
[UnsupportedOSPlatform("ios")]
[UnsupportedOSPlatform("tvos")]
[UnsupportedOSPlatform("wasi")]
internal sealed partial class HangDumpProcessLifetimeHandler : ITestHostProcessLifetimeHandler, IOutputDeviceDataProducer, IDataProducer,
#if NETCOREAPP
    IAsyncDisposable,
#endif
    IDisposable
{
    private readonly IMessageBus _messageBus;
    private readonly OutputDeviceWriter _outputDisplay;
    private readonly ICommandLineOptions _commandLineOptions;
    private readonly ITask _task;
    private readonly IEnvironment _environment;
    private readonly IConfiguration _configuration;
    private readonly IProcessHandler _processHandler;
    private readonly IClock _clock;
    private readonly PipeNameDescription _pipeNameDescription;
    private readonly bool _traceEnabled;
    private readonly ILogger<HangDumpProcessLifetimeHandler> _logger;
    private readonly ManualResetEventSlim _waitConsumerPipeName = new(false);
    private readonly List<string> _dumpFiles = [];

    private TimeSpan? _activityTimerValue;
    private Timer? _activityTimer;
    private Task? _waitConnectionTask;
    private Task? _activityIndicatorTask;
    private NamedPipeServer? _singleConnectionNamedPipeServer;
    private string _dumpType = "Full";
    private string? _dumpFileNamePattern;
    private ITestHostProcessInformation? _testHostProcessInformation;
    private NamedPipeClient? _namedPipeClient;

    public HangDumpProcessLifetimeHandler(
        PipeNameDescription pipeNameDescription,
        IMessageBus messageBus,
        IOutputDevice outputDevice,
        ICommandLineOptions commandLineOptions,
        ITask task,
        IEnvironment environment,
        ILoggerFactory loggerFactory,
        IConfiguration configuration,
        IProcessHandler processHandler,
        IClock clock)
    {
        _logger = loggerFactory.CreateLogger<HangDumpProcessLifetimeHandler>();
        _traceEnabled = _logger.IsEnabled(LogLevel.Trace);
        _pipeNameDescription = pipeNameDescription;
        _messageBus = messageBus;
        _outputDisplay = new OutputDeviceWriter(outputDevice, this);
        _commandLineOptions = commandLineOptions;
        _task = task;
        _environment = environment;
        _configuration = configuration;
        _processHandler = processHandler;
        _clock = clock;
    }

    public string Uid => nameof(HangDumpProcessLifetimeHandler);

    public string Version => ExtensionVersion.DefaultSemVer;

    public string DisplayName => ExtensionResources.HangDumpExtensionDisplayName;

    public string Description => ExtensionResources.HangDumpExtensionDescription;

    public Type[] DataTypesProduced => [typeof(FileArtifact)];

    public Task<bool> IsEnabledAsync() => Task.FromResult(HangDumpOptions.IsEnabled(_commandLineOptions));

    public async Task BeforeTestHostProcessStartAsync(CancellationToken cancellationToken)
    {
        _activityTimerValue = _commandLineOptions.TryGetOptionArgumentList(HangDumpCommandLineProvider.HangDumpTimeoutOptionName, out string[]? timeout)
            ? TimeSpanParser.Parse(timeout[0])
            : TimeSpan.FromMinutes(30);

        if (_commandLineOptions.TryGetOptionArgumentList(HangDumpCommandLineProvider.HangDumpTypeOptionName, out string[]? dumpType))
        {
            _dumpType = dumpType[0];
        }
        else if (_commandLineOptions.TryGetOptionArgumentList(HangDumpCommandLineProvider.HangDumpTypeIfSupportedOptionName, out string[]? dumpTypeIfSupported))
        {
            // The "-if-supported" variant accepts the full set of dump types regardless of TFM
            // (see HangDumpCommandLineProvider.ValidateOptionArgumentsAsync). When the user
            // requests a value that the current runtime cannot honor, fall back to the closest
            // supported value (see MapToSupportedDumpType) and emit a single informational
            // message so the CI log makes the substitution visible without breaking the run.
            string requested = dumpTypeIfSupported[0];
            _dumpType = HangDumpCommandLineProvider.MapToSupportedDumpType(requested);
            if (!string.Equals(_dumpType, requested, StringComparison.OrdinalIgnoreCase))
            {
                await _outputDisplay.DisplayAsync(
                    new FormattedTextOutputDeviceData(string.Format(CultureInfo.InvariantCulture, ExtensionResources.HangDumpTypeIfSupportedFallbackInfoMessage, requested, _dumpType)),
                    cancellationToken).ConfigureAwait(false);
            }
        }

        if (_commandLineOptions.TryGetOptionArgumentList(HangDumpCommandLineProvider.HangDumpFileNameOptionName, out string[]? fileName))
        {
            _dumpFileNamePattern = fileName[0];
        }

        await _logger.LogInformationAsync($"Hang dump timeout setup {_activityTimerValue}.").ConfigureAwait(false);

        _singleConnectionNamedPipeServer = new(_pipeNameDescription, CallbackAsync, _environment, _logger, _task, cancellationToken);
        _singleConnectionNamedPipeServer.RegisterSerializer(new VoidResponseSerializer(), typeof(VoidResponse));
        _singleConnectionNamedPipeServer.RegisterSerializer(new ConsumerPipeNameRequestSerializer(), typeof(ConsumerPipeNameRequest));
        _singleConnectionNamedPipeServer.RegisterSerializer(new ActivitySignalRequestSerializer(), typeof(ActivitySignalRequest));

        _waitConnectionTask = _task.Run(
            async () =>
            {
                await _logger.LogDebugAsync($"Waiting for connection to {_singleConnectionNamedPipeServer.PipeName.Name}").ConfigureAwait(false);
                await _singleConnectionNamedPipeServer.WaitConnectionAsync(cancellationToken).TimeoutAfterAsync(TimeoutHelper.DefaultHangTimeSpanTimeout, cancellationToken).ConfigureAwait(false);
            }, cancellationToken);
    }

    private async Task<IResponse> CallbackAsync(IRequest request)
    {
        if (request is ConsumerPipeNameRequest consumerPipeNameRequest)
        {
            await _logger.LogDebugAsync($"Consumer pipe name received '{consumerPipeNameRequest.PipeName}'").ConfigureAwait(false);
            _namedPipeClient = new NamedPipeClient(consumerPipeNameRequest.PipeName, _environment);
            _namedPipeClient.RegisterSerializer(new GetInProgressTestsResponseSerializer(), typeof(GetInProgressTestsResponse));
            _namedPipeClient.RegisterSerializer(new GetInProgressTestsRequestSerializer(), typeof(GetInProgressTestsRequest));
            _namedPipeClient.RegisterSerializer(new VoidResponseSerializer(), typeof(VoidResponse));
            _waitConsumerPipeName.Set();
            return VoidResponse.CachedInstance;
        }
        else if (request is ActivitySignalRequest)
        {
            if (_traceEnabled)
            {
                _logger.LogTrace($"Activity signal received by the test host '{_clock.UtcNow}'");
            }

            _activityTimer?.Change(_activityTimerValue!.Value, TimeSpan.FromMilliseconds(-1));
            return VoidResponse.CachedInstance;
        }
        else
        {
            throw new ArgumentOutOfRangeException(string.Format(CultureInfo.InvariantCulture, ExtensionResources.HangDumpUnsupportedRequestTypeErrorMessage, request));
        }
    }

    public async Task OnTestHostProcessStartedAsync(ITestHostProcessInformation testHostProcessInformation, CancellationToken cancellationToken)
    {
        ApplicationStateGuard.Ensure(_waitConnectionTask is not null);
        ApplicationStateGuard.Ensure(_singleConnectionNamedPipeServer is not null);

        _testHostProcessInformation = testHostProcessInformation;

        await _logger.LogDebugAsync($"Wait for test host connection to the server pipe '{_singleConnectionNamedPipeServer.PipeName.Name}'").ConfigureAwait(false);
        await _waitConnectionTask.TimeoutAfterAsync(TimeoutHelper.DefaultHangTimeSpanTimeout).ConfigureAwait(false);
        using CancellationTokenSource timeout = new(TimeoutHelper.DefaultHangTimeSpanTimeout);
        using var linkedCancellationToken = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);
        _waitConsumerPipeName.Wait(linkedCancellationToken.Token);
        ApplicationStateGuard.Ensure(_namedPipeClient is not null);
        await _namedPipeClient.ConnectAsync(cancellationToken).TimeoutAfterAsync(TimeoutHelper.DefaultHangTimeSpanTimeout).ConfigureAwait(false);
        await _logger.LogDebugAsync($"Connected to the test host server pipe '{_namedPipeClient.PipeName}'").ConfigureAwait(false);

        _activityTimer = new Timer(
            _ => _activityIndicatorTask = TakeDumpOfTreeAsync(cancellationToken),
            null,
            _activityTimerValue!.Value,
            TimeSpan.FromMilliseconds(-1));
    }

    public async Task OnTestHostProcessExitedAsync(ITestHostProcessInformation testHostProcessInformation, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (_activityTimer is not null)
        {
#if NETCOREAPP
            await _activityTimer.DisposeAsync().ConfigureAwait(false);
#else
            _activityTimer.Dispose();
#endif
        }

        if (!testHostProcessInformation.HasExitedGracefully)
        {
            _logger.LogDebug($"Testhost didn't exit gracefully '{testHostProcessInformation.ExitCode}')");
        }

        foreach (string dumpFile in _dumpFiles)
        {
            await _messageBus.PublishAsync(this, new FileArtifact(new FileInfo(dumpFile), ExtensionResources.HangDumpArtifactDisplayName, ExtensionResources.HangDumpArtifactDescription)).ConfigureAwait(false);
        }
    }

    public void Dispose()
    {
        if (_activityIndicatorTask is not null)
        {
            bool waitResult;
            try
            {
                waitResult = _activityIndicatorTask.Wait(TimeoutHelper.DefaultHangTimeSpanTimeout);
            }
            catch (Exception e)
            {
                _outputDisplay.DisplayAsync(new ErrorMessageOutputDeviceData(string.Format(CultureInfo.InvariantCulture, ExtensionResources.HangDumpFailed, e.ToString(), GetDiskInfo())), CancellationToken.None).GetAwaiter().GetResult();
                throw;
            }

            if (!waitResult)
            {
                throw new InvalidOperationException($"_activityIndicatorTask didn't exit in {TimeoutHelper.DefaultHangTimeSpanTimeout} seconds");
            }
        }

        _namedPipeClient?.Dispose();
        _waitConsumerPipeName.Dispose();
        _singleConnectionNamedPipeServer?.Dispose();
    }

#if NETCOREAPP
    public async ValueTask DisposeAsync()
    {
        if (_activityIndicatorTask is not null)
        {
            try
            {
                await _activityIndicatorTask.TimeoutAfterAsync(TimeoutHelper.DefaultHangTimeSpanTimeout).ConfigureAwait(false);
            }
            catch (Exception e)
            {
                await _outputDisplay.DisplayAsync(new ErrorMessageOutputDeviceData(string.Format(CultureInfo.InvariantCulture, ExtensionResources.HangDumpFailed, e.ToString(), GetDiskInfo())), CancellationToken.None).ConfigureAwait(false);
                throw;
            }
        }

        _namedPipeClient?.Dispose();
        _waitConsumerPipeName.Dispose();
        _singleConnectionNamedPipeServer?.Dispose();
    }
#endif
}
