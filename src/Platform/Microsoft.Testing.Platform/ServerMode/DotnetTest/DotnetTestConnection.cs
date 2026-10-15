// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Extensions.CommandLine;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.IPC;
using Microsoft.Testing.Platform.IPC.Models;
using Microsoft.Testing.Platform.IPC.Serializers;
using Microsoft.Testing.Platform.Logging;
using Microsoft.Testing.Platform.ServerMode;
using Microsoft.Testing.Platform.Services;
using Microsoft.Testing.Platform.Tools;

namespace Microsoft.Testing.Platform;

internal sealed class DotnetTestConnection : IPushOnlyProtocol, IDisposable
{
    private const int MaximumLocalArtifactTransferChunkBytes = 1024 * 1024;
    private const int MaximumLocalArtifactTransferFileCount = 10_000;
    private static readonly TimeSpan ServerControlShutdownTimeout = TimeSpan.FromSeconds(5);

    private readonly CommandLineHandler _commandLineHandler;
    private readonly IEnvironment _environment;
    private readonly ITestApplicationModuleInfo _testApplicationModuleInfo;
    private readonly ITestApplicationCancellationTokenSource _cancellationTokenSource;
    private readonly ILogger _logger;
#if NET9_0_OR_GREATER
    private readonly Lock _artifactTransferLock = new();
#else
    private readonly object _artifactTransferLock = new();
#endif

    private IClient? _transportClient;
    private DotnetTestTransportKind _transportKind;

    private NamedPipeClient? _serverControlPipeClient;
    private DotnetTestHttpClient? _serverControlHttpClient;
    private Task? _serverControlListenerTask;
    private CancellationTokenSource? _serverControlListenerCts;
    private string? _serverControlPipeName;
    private string? _serverControlTransport;
    private int _cancelRequested;
    private int _fileArtifactTransferMaxChunkBytes;
    private long _fileArtifactTransferMaxFileBytes;
    private long _fileArtifactTransferMaxModuleBytes;
    private long _reservedArtifactTransferBytes;
    private int _fileArtifactTransferMaxFileCount;
    private int _reservedArtifactTransferFileCount;
    private int _artifactTransferUnsupportedWarningEmitted;

    public static string InstanceId { get; } = Guid.NewGuid().ToString("N");

    string IPushOnlyProtocol.InstanceId => InstanceId;

    public DotnetTestConnection(CommandLineHandler commandLineHandler, IEnvironment environment, ITestApplicationModuleInfo testApplicationModuleInfo, ITestApplicationCancellationTokenSource cancellationTokenSource)
        : this(commandLineHandler, environment, testApplicationModuleInfo, cancellationTokenSource, new NopLogger())
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="DotnetTestConnection"/> class with a logger for low-noise
    /// diagnostics of the control-pipe connect/listen/cancel/exit paths.
    /// </summary>
    public DotnetTestConnection(CommandLineHandler commandLineHandler, IEnvironment environment, ITestApplicationModuleInfo testApplicationModuleInfo, ITestApplicationCancellationTokenSource cancellationTokenSource, ILogger logger)
    {
        _commandLineHandler = commandLineHandler;
        _environment = environment;
        _testApplicationModuleInfo = testApplicationModuleInfo;
        _cancellationTokenSource = cancellationTokenSource;
        _logger = logger;
    }

    public bool IsServerMode => _transportClient?.IsConnected == true;

    public Task<IPushOnlyProtocolConsumer> GetDataConsumerAsync()
        => Task.FromResult((IPushOnlyProtocolConsumer)new DotnetTestDataConsumer(this, _environment));

    public async Task AfterCommonServiceSetupAsync()
    {
        if (_commandLineHandler.HasDotnetTestServerOption()
            && _commandLineHandler.TryGetDotnetTestTransport(out DotnetTestTransportKind transport))
        {
            // The execution id is used to identify the test execution
            // We are storing it as an env var so that it can be read by the test host, test host controller and the test host orchestrator
            // If it already exists, we don't overwrite it
            if (RoslynString.IsNullOrEmpty(_environment.GetEnvironmentVariable(EnvironmentVariableConstants.TESTINGPLATFORM_DOTNETTEST_EXECUTIONID)))
            {
                _environment.SetEnvironmentVariable(EnvironmentVariableConstants.TESTINGPLATFORM_DOTNETTEST_EXECUTIONID, Guid.NewGuid().ToString("N"));
            }

            _transportKind = transport;
            _transportClient = transport switch
            {
                DotnetTestTransportKind.Http => CreateHttpClient(),
                DotnetTestTransportKind.NamedPipe when !OperatingSystem.IsBrowser() && !OperatingSystem.IsWasi() => CreateNamedPipeClient(),
                DotnetTestTransportKind.NamedPipe => throw new PlatformNotSupportedException("The dotnet test named-pipe transport is not supported on WebAssembly."),
                _ => throw ApplicationStateGuard.Unreachable(),
            };

            ((NamedPipeBase)_transportClient).RegisterAllSerializers();

            await _transportClient.ConnectAsync(_cancellationTokenSource.CancellationToken).ConfigureAwait(false);
        }
    }

    [UnsupportedOSPlatform("browser")]
    private NamedPipeClient CreateNamedPipeClient()
    {
        ApplicationStateGuard.Ensure(_commandLineHandler.TryGetOptionArgumentList(PlatformCommandLineProvider.DotNetTestPipeOptionKey, out string[]? arguments));
        return new NamedPipeClient(arguments[0], _environment);
    }

    private DotnetTestHttpClient CreateHttpClient()
    {
        ApplicationStateGuard.Ensure(_commandLineHandler.TryGetOptionArgumentList(PlatformCommandLineProvider.DotNetTestHttpEndpointOptionKey, out string[]? endpointArguments));
        ApplicationStateGuard.Ensure(_commandLineHandler.TryGetOptionArgumentList(PlatformCommandLineProvider.DotNetTestHttpTokenOptionKey, out string[]? tokenArguments));
        return new DotnetTestHttpClient(new Uri(endpointArguments[0], UriKind.Absolute), tokenArguments[0]);
    }

    public async Task HelpInvokedAsync()
    {
        IClient transportClient = _transportClient
            ?? throw new InvalidOperationException("The dotnet test transport client is not connected.");

        List<CommandLineOptionMessage> commandLineHelpOptions = [];
        foreach (ICommandLineOptionsProvider commandLineOptionProvider in _commandLineHandler.CommandLineOptionsProviders)
        {
            if (commandLineOptionProvider is IToolCommandLineOptionsProvider)
            {
                continue;
            }

            foreach (CommandLineOption commandLineOption in commandLineOptionProvider.GetCommandLineOptions())
            {
                commandLineHelpOptions.Add(new CommandLineOptionMessage(
                    commandLineOption.Name,
                    commandLineOption.Description,
                    commandLineOption.IsHidden,
                    commandLineOption.IsBuiltIn,
                    commandLineOptionProvider.Uid,
                    commandLineOption.Arity.Min,
                    commandLineOption.Arity.Max));
            }
        }

        await transportClient.RequestReplyAsync<CommandLineOptionMessages, VoidResponse>(new CommandLineOptionMessages(_testApplicationModuleInfo.GetCurrentTestApplicationFullPath(), [.. commandLineHelpOptions.OrderBy(option => option.Name)]), _cancellationTokenSource.CancellationToken).ConfigureAwait(false);
    }

    public bool IsIDE { get; private set; }

    // True once the handshake negotiated protocol version 1.2.0 or later, which is when the SDK is
    // able to receive AzureDevOpsLogMessage forwards. The host gates forwarding on this so an older
    // SDK (1.0.0/1.1.0) never receives an unknown message id.
    public bool IsLogForwardingSupported { get; private set; }

    // True once the handshake negotiated protocol version 1.3.0 or later, which is when the SDK is able to
    // receive generic DisplayMessage forwards (warning/error host diagnostics). The host gates forwarding on this
    // so an older SDK (<= 1.2.0) never receives an unknown message id.
    public bool IsDisplayMessageForwardingSupported { get; private set; }

    // True once the SDK advertised a reverse "server control" pipe name in its handshake reply. When set, the
    // test host opens a NamedPipeClient to that pipe and parks a long-poll so the SDK can push a
    // ServerControlMessage (e.g. CancelSession) at any time. The feature is gated on the presence of the handshake
    // property (a capability), not on the negotiated version string, so an older SDK simply never enables it.
    public bool IsServerControlChannelSupported { get; private set; }

    private bool IsFileArtifactTransferSupported { get; set; }

    public bool IsRequiredArtifactPostProcessingSupported { get; private set; }

    public async Task<bool> IsCompatibleProtocolAsync(string hostType, IReadOnlyDictionary<byte, string>? additionalHandshakeProperties = null)
    {
        IClient transportClient = _transportClient
            ?? throw new InvalidOperationException("The dotnet test transport client is not connected.");

        string supportedProtocolVersions = ProtocolConstants.SupportedVersions;
        Dictionary<byte, string> properties = new()
        {
            { HandshakeMessagePropertyNames.PID, _environment.ProcessId.ToString(CultureInfo.InvariantCulture) },
            { HandshakeMessagePropertyNames.Architecture, RuntimeInformation.ProcessArchitecture.ToString() },
            { HandshakeMessagePropertyNames.Framework, RuntimeInformation.FrameworkDescription },
            { HandshakeMessagePropertyNames.OS, RuntimeInformation.OSDescription },
            { HandshakeMessagePropertyNames.SupportedProtocolVersions, supportedProtocolVersions },
            { HandshakeMessagePropertyNames.HostType, hostType },
            { HandshakeMessagePropertyNames.ModulePath, _testApplicationModuleInfo?.GetCurrentTestApplicationFullPath() ?? string.Empty },
            { HandshakeMessagePropertyNames.ExecutionId,  _environment.GetEnvironmentVariable(EnvironmentVariableConstants.TESTINGPLATFORM_DOTNETTEST_EXECUTIONID) ?? string.Empty },
            { HandshakeMessagePropertyNames.InstanceId, InstanceId },
            { HandshakeMessagePropertyNames.ExecutionMode, GetExecutionMode() },
        };

        if (hostType is HandshakeMessageHostTypes.TestHost or HandshakeMessageHostTypes.ServerTestHost)
        {
            properties.Add(HandshakeMessagePropertyNames.AttemptNumber, GetAttemptNumber());
        }

        if (additionalHandshakeProperties is not null)
        {
            foreach (KeyValuePair<byte, string> property in additionalHandshakeProperties)
            {
                properties[property.Key] = property.Value;
            }
        }

        HandshakeMessage handshakeMessage = new(properties);

        HandshakeMessage response = await transportClient.RequestReplyAsync<HandshakeMessage, HandshakeMessage>(handshakeMessage, _cancellationTokenSource.CancellationToken).ConfigureAwait(false);

        IsIDE = response.Properties?.TryGetValue(HandshakeMessagePropertyNames.IsIDE, out string? isIDEValue) == true &&
            bool.TryParse(isIDEValue, out bool isIDE) &&
            isIDE;
        IsRequiredArtifactPostProcessingSupported =
            response.Properties?.TryGetValue(HandshakeMessagePropertyNames.RequiredPostProcessingSupported, out string? requiredPostProcessingValue) is true
            && bool.TryParse(requiredPostProcessingValue, out bool requiredPostProcessingSupported)
            && requiredPostProcessingSupported;

        if (_transportKind == DotnetTestTransportKind.NamedPipe
            && response.Properties?.TryGetValue(HandshakeMessagePropertyNames.ServerControlPipeName, out string? serverControlPipeName) is true &&
            !RoslynString.IsNullOrEmpty(serverControlPipeName))
        {
            _serverControlPipeName = serverControlPipeName;
            IsServerControlChannelSupported = true;
        }
        else if (_transportKind == DotnetTestTransportKind.Http
            && response.Properties?.TryGetValue(HandshakeMessagePropertyNames.ServerControlTransport, out string? serverControlTransport) is true
            && serverControlTransport == ServerControlTransports.Http)
        {
            _serverControlTransport = serverControlTransport;
            IsServerControlChannelSupported = true;
        }

        int advertisedMaxChunkBytes = 0;
        IsFileArtifactTransferSupported =
            _transportKind == DotnetTestTransportKind.Http
            && response.Properties?.TryGetValue(HandshakeMessagePropertyNames.FileArtifactTransfer, out string? artifactTransfer) is true
            && artifactTransfer == FileArtifactTransferCapabilities.ChunkedV1
            && TryReadPositiveInt32(response, HandshakeMessagePropertyNames.FileArtifactTransferMaxChunkBytes, out advertisedMaxChunkBytes)
            && TryReadPositiveInt64(response, HandshakeMessagePropertyNames.FileArtifactTransferMaxFileBytes, out _fileArtifactTransferMaxFileBytes)
            && TryReadPositiveInt64(response, HandshakeMessagePropertyNames.FileArtifactTransferMaxModuleBytes, out _fileArtifactTransferMaxModuleBytes);
        if (IsFileArtifactTransferSupported)
        {
            _fileArtifactTransferMaxChunkBytes = Math.Min(
                advertisedMaxChunkBytes,
                MaximumLocalArtifactTransferChunkBytes);
            _fileArtifactTransferMaxFileCount =
                TryReadPositiveInt32(
                    response,
                    HandshakeMessagePropertyNames.FileArtifactTransferMaxFileCount,
                    out int advertisedMaxFileCount)
                    ? Math.Min(advertisedMaxFileCount, MaximumLocalArtifactTransferFileCount)
                    : MaximumLocalArtifactTransferFileCount;
        }

        if (response.Properties?.TryGetValue(HandshakeMessagePropertyNames.SupportedProtocolVersions, out string? protocolVersion) is true)
        {
            bool isCompatible = IsVersionCompatible(protocolVersion, supportedProtocolVersions);
            bool versionParsed = Version.TryParse(protocolVersion, out Version? negotiatedVersion);
            IsLogForwardingSupported = isCompatible && versionParsed && negotiatedVersion >= new Version(1, 2, 0);
            IsDisplayMessageForwardingSupported = isCompatible && versionParsed && negotiatedVersion >= new Version(1, 3, 0);
            return isCompatible;
        }

        return false;
    }

    private string GetExecutionMode()
        => _commandLineHandler.ParseResult.HasTool
            ? HandshakeMessageExecutionModes.Tool
            : _commandLineHandler.IsHelpInvoked()
            ? HandshakeMessageExecutionModes.Help
            : _commandLineHandler.IsOptionSet(PlatformCommandLineProvider.DiscoverTestsOptionKey)
                ? HandshakeMessageExecutionModes.Discover
                : HandshakeMessageExecutionModes.Run;

    private string GetAttemptNumber()
    {
        string? value = _environment.GetEnvironmentVariable(EnvironmentVariableConstants.TESTINGPLATFORM_DOTNETTEST_ATTEMPTNUMBER);
        return RoslynString.IsNullOrEmpty(value)
            ? "1"
            : int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out int attemptNumber) && attemptNumber >= 1
                ? attemptNumber.ToString(CultureInfo.InvariantCulture)
                : throw new InvalidOperationException($"Environment variable '{EnvironmentVariableConstants.TESTINGPLATFORM_DOTNETTEST_ATTEMPTNUMBER}' must contain a positive integer.");
    }

    public static bool IsVersionCompatible(string protocolVersion, string supportedProtocolVersions) => supportedProtocolVersions.Split(';').Contains(protocolVersion);

    public async Task SendMessageAsync(IRequest message)
    {
        IClient transportClient = _transportClient
            ?? throw new InvalidOperationException("The dotnet test transport client is not connected.");

        switch (message)
        {
            case DiscoveredTestMessages discoveredTestMessages:
                await transportClient.RequestReplyAsync<DiscoveredTestMessages, VoidResponse>(discoveredTestMessages, _cancellationTokenSource.CancellationToken).ConfigureAwait(false);
                break;

            case TestResultMessages testResultMessages:
                await transportClient.RequestReplyAsync<TestResultMessages, VoidResponse>(testResultMessages, _cancellationTokenSource.CancellationToken).ConfigureAwait(false);
                break;

            case FileArtifactMessages fileArtifactMessages:
                FileArtifactMessages? messageToSend = await PrepareFileArtifactsAsync(
                    transportClient,
                    fileArtifactMessages,
                    _cancellationTokenSource.CancellationToken).ConfigureAwait(false);
                if (messageToSend is not null)
                {
                    await transportClient.RequestReplyAsync<FileArtifactMessages, VoidResponse>(messageToSend, _cancellationTokenSource.CancellationToken).ConfigureAwait(false);
                }

                break;

            case TestSessionEvent testSessionEvent:
                await transportClient.RequestReplyAsync<TestSessionEvent, VoidResponse>(testSessionEvent, _cancellationTokenSource.CancellationToken).ConfigureAwait(false);
                break;

            case AzureDevOpsLogMessage azureDevOpsLogMessage:
                await transportClient.RequestReplyAsync<AzureDevOpsLogMessage, VoidResponse>(azureDevOpsLogMessage, _cancellationTokenSource.CancellationToken).ConfigureAwait(false);
                break;

            case DisplayMessage displayMessage:
                await transportClient.RequestReplyAsync<DisplayMessage, VoidResponse>(displayMessage, _cancellationTokenSource.CancellationToken).ConfigureAwait(false);
                break;
        }
    }

    /// <summary>
    /// Opens the reverse "server control" pipe the SDK advertised during the handshake and parks a long-poll
    /// <see cref="WaitForServerControlRequest"/> on it. The SDK completes that request with a
    /// <see cref="ServerControlMessage"/> whenever it wants to signal the test host (today only
    /// <see cref="ServerControlKinds.CancelSession"/>). On cancel - or when the control pipe drops, which means
    /// the host went away - <paramref name="onCancelSessionRequestedAsync"/> is invoked exactly once so the caller
    /// can stop the run cooperatively (preferring a graceful stop so trx/artifacts are still produced).
    /// </summary>
    /// <param name="onCancelSessionRequestedAsync">
    /// The reaction to a server-initiated cancel. It receives the test application cancellation token.
    /// </param>
    /// <remarks>
    /// Both the connect and the long-poll run entirely on a background task, so test start is never blocked on
    /// this auxiliary channel. This is best-effort: if the control pipe cannot be established the test run
    /// continues unaffected (the feature simply stays off). Callers should only invoke this after a successful
    /// handshake and when <see cref="IsServerControlChannelSupported"/> is <see langword="true"/>. It is invoked
    /// once per connection on the awaited run path (before the test run starts), so the control-channel fields
    /// below are written here and only read later during exit/dispose - the single-threaded lifecycle makes the
    /// plain fields safe without extra synchronization.
    /// </remarks>
    public Task StartServerControlChannelAsync(Func<CancellationToken, Task> onCancelSessionRequestedAsync)
    {
        if (!IsServerControlChannelSupported
            || _serverControlListenerTask is not null)
        {
            return Task.CompletedTask;
        }

        _serverControlListenerCts = CancellationTokenSource.CreateLinkedTokenSource(_cancellationTokenSource.CancellationToken);

        if (_transportKind == DotnetTestTransportKind.Http
            && _serverControlTransport == ServerControlTransports.Http)
        {
            DotnetTestHttpClient controlClient = CreateHttpClient();
            controlClient.RegisterAllSerializers();
            _serverControlHttpClient = controlClient;
            _serverControlListenerTask = Task.Run(
                () => ConnectAndListenForHttpServerControlAsync(
                    controlClient,
                    onCancelSessionRequestedAsync,
                    _serverControlListenerCts.Token));
        }
        else if (_transportKind == DotnetTestTransportKind.NamedPipe
            && !OperatingSystem.IsBrowser()
            && !OperatingSystem.IsWasi()
            && !RoslynString.IsNullOrEmpty(_serverControlPipeName))
        {
            // exitProcessOnConnectionLoss: false - a dropped control pipe must not kill the test host; the listener
            // turns it into a cooperative cancel instead.
            var controlClient = new NamedPipeClient(_serverControlPipeName, _environment, exitProcessOnConnectionLoss: false);
            controlClient.RegisterAllSerializers();
            _serverControlPipeClient = controlClient;

            // Connect AND listen on a background task: we deliberately do not await the connect on the run path so a
            // slow/absent control server can never delay (or fail) test execution start.
#pragma warning disable CA1416 // The runtime guards above exclude browser before the named-pipe operation is scheduled.
            _serverControlListenerTask = Task.Run(
                () => ConnectAndListenForServerControlAsync(controlClient, onCancelSessionRequestedAsync, _serverControlListenerCts.Token));
#pragma warning restore CA1416
        }

        return Task.CompletedTask;
    }

    private async Task ConnectAndListenForHttpServerControlAsync(
        DotnetTestHttpClient controlClient,
        Func<CancellationToken, Task> onCancelSessionRequestedAsync,
        CancellationToken cancellationToken)
    {
        try
        {
            await controlClient.ConnectAsync(cancellationToken).ConfigureAwait(false);
            while (!cancellationToken.IsCancellationRequested)
            {
                IResponse response = await controlClient.RequestReplyAsync<WaitForServerControlRequest, IResponse>(
                    WaitForServerControlRequest.CachedInstance,
                    cancellationToken).ConfigureAwait(false);

                if (response is ServerControlMessage { Kind: ServerControlKinds.CancelSession })
                {
                    await RequestCancelOnceAsync(onCancelSessionRequestedAsync).ConfigureAwait(false);
                    return;
                }

                if (response is ServerControlMessage { Kind: ServerControlKinds.CloseChannel })
                {
                    return;
                }

                if (response is VoidResponse)
                {
                    // The SDK bounds each long poll and returns VoidResponse when no command is pending.
                    // Yield before parking the next request so a broken peer cannot create a tight loop.
                    await Task.Delay(TimeSpan.FromMilliseconds(50), cancellationToken).ConfigureAwait(false);
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            await TryLogAsync(LogLevel.Trace, "The HTTP server-control listener stopped: shutdown requested.").ConfigureAwait(false);
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            await RequestCancelOnceAsync(onCancelSessionRequestedAsync).ConfigureAwait(false);
            await TryLogAsync(LogLevel.Debug, $"The HTTP server-control listener failed while the session was live; ensuring cooperative cancellation: {ex}").ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            await TryLogAsync(LogLevel.Trace, $"The HTTP server-control listener observed a shutdown race: {ex}").ConfigureAwait(false);
        }
    }

    [UnsupportedOSPlatform("browser")]
    private async Task ConnectAndListenForServerControlAsync(NamedPipeClient controlClient, Func<CancellationToken, Task> onCancelSessionRequestedAsync, CancellationToken cancellationToken)
    {
        try
        {
            // Bound the connect so a misbehaving SDK that advertises a pipe it never listens on cannot leave this
            // task parked forever holding resources.
            using var connectCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            connectCts.CancelAfter(TimeSpan.FromSeconds(30));
            await controlClient.ConnectAsync(connectCts.Token).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            // Best-effort: failing to establish the control channel degrades to "no server-initiated cancel"
            // rather than affecting the test run.
            await TryLogAsync(LogLevel.Debug, $"Failed to connect to the server control pipe '{_serverControlPipeName}'; the server-initiated cancel feature will stay disabled for this run: {ex}").ConfigureAwait(false);
            return;
        }

        await TryLogAsync(LogLevel.Debug, $"Connected to the server control pipe '{_serverControlPipeName}'.").ConfigureAwait(false);

        await ListenForServerControlAsync(controlClient, onCancelSessionRequestedAsync, cancellationToken).ConfigureAwait(false);
    }

    [UnsupportedOSPlatform("browser")]
    private async Task ListenForServerControlAsync(NamedPipeClient controlClient, Func<CancellationToken, Task> onCancelSessionRequestedAsync, CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                ServerControlMessage message = await controlClient.RequestReplyAsync<WaitForServerControlRequest, ServerControlMessage>(
                    WaitForServerControlRequest.CachedInstance, cancellationToken).ConfigureAwait(false);

                if (message.Kind == ServerControlKinds.CancelSession)
                {
                    await RequestCancelOnceAsync(onCancelSessionRequestedAsync).ConfigureAwait(false);
                    return;
                }

                // Unknown control kind (forward-compat): ignore it and keep parking for the next signal.
            }
        }
        catch (OperationCanceledException)
        {
            // We are shutting down (dispose or app cancellation) - nothing to do. Expected shutdown, so this stays
            // at Trace to avoid noise.
            await TryLogAsync(LogLevel.Trace, $"Server control pipe '{_serverControlPipeName}' listener stopped: shutdown requested.").ConfigureAwait(false);
        }
        catch (Exception ex) when (!cancellationToken.IsCancellationRequested)
        {
            // The control pipe dropped while the session was still live => the host went away. Treat it as a
            // cooperative cancel so we still try to wind down and report whatever completed. NOTE: this makes it a
            // protocol requirement that the SDK keep the control pipe open until the data session ends - an early
            // close for any reason is interpreted here as a cancel.
            await RequestCancelOnceAsync(onCancelSessionRequestedAsync).ConfigureAwait(false);
            await TryLogAsync(LogLevel.Debug, $"Server control pipe '{_serverControlPipeName}' listener failed while the session was live; ensuring cooperative cancellation: {ex}").ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            // Cancellation raced with a pipe error (e.g. the stream was disposed during teardown); ignore. This is
            // an expected shutdown race, so it stays at Trace.
            await TryLogAsync(LogLevel.Trace, $"Server control pipe '{_serverControlPipeName}' listener observed a race between shutdown and a pipe error during teardown: {ex}").ConfigureAwait(false);
        }
    }

    private async Task RequestCancelOnceAsync(Func<CancellationToken, Task> onCancelSessionRequestedAsync)
    {
        if (Interlocked.Exchange(ref _cancelRequested, 1) != 0)
        {
            return;
        }

        await onCancelSessionRequestedAsync(_cancellationTokenSource.CancellationToken).ConfigureAwait(false);
    }

    public async Task OnExitAsync()
    {
#if NET
        if (_serverControlListenerCts is { } cts)
        {
            await cts.CancelAsync().ConfigureAwait(false);
        }
#else
#pragma warning disable VSTHRD103 // CancellationTokenSource.CancelAsync is not available on this target framework.
        _serverControlListenerCts?.Cancel();
#pragma warning restore VSTHRD103
#endif

        // Cancelling the token is enough to abort an in-flight named-pipe read on modern .NET, but on .NET
        // Framework cancellation of an already-parked overlapped read is not reliable - disposing the stream is
        // what forces it to unblock. We cancelled first (above) so the listener treats the resulting failure as
        // shutdown rather than "host gone => cancel".
        if (!OperatingSystem.IsBrowser())
        {
            _serverControlPipeClient?.Dispose();
        }

        _serverControlHttpClient?.Dispose();

        if (_serverControlListenerTask is { } listenerTask)
        {
            // Bounded wait so a stuck listener can never hang the exit path on any target framework.
            Task completed = await Task.WhenAny(listenerTask, Task.Delay(ServerControlShutdownTimeout)).ConfigureAwait(false);
            if (completed != listenerTask)
            {
                await TryLogAsync(LogLevel.Debug, "The server-control listener did not finish within the 5s bounded wait during exit; continuing without waiting further.").ConfigureAwait(false);
            }
        }
    }

    public void Dispose()
    {
        _serverControlListenerCts?.Cancel();

        // Disposing the pipe client forces a parked read to abort even where token cancellation alone would not.
        if (!OperatingSystem.IsBrowser())
        {
            _serverControlPipeClient?.Dispose();
        }

        _serverControlHttpClient?.Dispose();

        if (_serverControlListenerTask is { } listenerTask)
        {
            try
            {
                // Bounded wait: the cancel + dispose above unblock the parked read quickly. Never hang exit.
                listenerTask.Wait(ServerControlShutdownTimeout);
            }
            catch (Exception)
            {
                // Best-effort shutdown. Logging providers have already been disposed by this phase; diagnostics
                // belong in OnExitAsync, which runs while logging is still available.
            }
        }

        _serverControlListenerCts?.Dispose();
        _transportClient?.Dispose();
    }

    private async Task<FileArtifactMessages?> PrepareFileArtifactsAsync(
        IClient transportClient,
        FileArtifactMessages fileArtifactMessages,
        CancellationToken cancellationToken)
    {
        if (_transportKind != DotnetTestTransportKind.Http)
        {
            return fileArtifactMessages;
        }

        if (!IsFileArtifactTransferSupported)
        {
            if (Interlocked.Exchange(ref _artifactTransferUnsupportedWarningEmitted, 1) == 0)
            {
                await ReportArtifactTransferWarningAsync(
                    transportClient,
                    "The dotnet test HTTP peer does not support remote test-host artifact export. Test-host-local artifact paths will not be reported as host artifacts.",
                    cancellationToken).ConfigureAwait(false);
            }

            return null;
        }

        List<FileArtifactMessage> exportedArtifacts = [];
        foreach (FileArtifactMessage artifact in fileArtifactMessages.FileArtifacts)
        {
            string fullPath = await ExportArtifactPathAsync(
                transportClient,
                fileArtifactMessages.ExecutionId,
                fileArtifactMessages.InstanceId,
                artifact.FullPath,
                cancellationToken).ConfigureAwait(false);

            string[]? inputArtifactPaths = null;
            if (artifact.InputArtifactPaths is { Length: > 0 })
            {
                inputArtifactPaths = new string[artifact.InputArtifactPaths.Length];
                for (int i = 0; i < artifact.InputArtifactPaths.Length; i++)
                {
                    string exportedInput = await ExportArtifactPathAsync(
                        transportClient,
                        fileArtifactMessages.ExecutionId,
                        fileArtifactMessages.InstanceId,
                        artifact.InputArtifactPaths[i],
                        cancellationToken).ConfigureAwait(false);
                    inputArtifactPaths[i] = exportedInput;
                }
            }

            exportedArtifacts.Add(artifact with
            {
                FullPath = fullPath,
                InputArtifactPaths = inputArtifactPaths,
            });
        }

        return exportedArtifacts.Count == 0
            ? null
            : fileArtifactMessages with { FileArtifacts = [.. exportedArtifacts] };
    }

    private Task<string> ExportArtifactPathAsync(
        IClient transportClient,
        string? executionId,
        string? instanceId,
        string? path,
        CancellationToken cancellationToken)
        => RoslynString.IsNullOrEmpty(path)
            ? FailArtifactTransferAsync(
                transportClient,
                "A test-host artifact cannot be exported because its path is empty.",
                cancellationToken)
            : ExportArtifactPathCoreAsync(
                transportClient,
                executionId,
                instanceId,
                path,
                cancellationToken);

    private async Task<string> ExportArtifactPathCoreAsync(
        IClient transportClient,
        string? executionId,
        string? instanceId,
        string path,
        CancellationToken cancellationToken)
    {
        try
        {
            var file = new FileInfo(path);
            if (!file.Exists)
            {
                return await FailArtifactTransferAsync(
                    transportClient,
                    $"Test-host artifact '{path}' could not be exported because it does not exist.",
                    cancellationToken).ConfigureAwait(false);
            }

            long fileLength = file.Length;
            if (fileLength > _fileArtifactTransferMaxFileBytes)
            {
                return await FailArtifactTransferAsync(
                    transportClient,
                    $"Test-host artifact '{path}' is {fileLength} bytes, exceeding the peer's {_fileArtifactTransferMaxFileBytes}-byte per-file export limit.",
                    cancellationToken).ConfigureAwait(false);
            }

            bool reserved = false;
            bool fileCountLimitReached = false;
            lock (_artifactTransferLock)
            {
                fileCountLimitReached = _reservedArtifactTransferFileCount >= _fileArtifactTransferMaxFileCount;
                if (!fileCountLimitReached
                    && _reservedArtifactTransferBytes <= _fileArtifactTransferMaxModuleBytes - fileLength)
                {
                    _reservedArtifactTransferBytes += fileLength;
                    _reservedArtifactTransferFileCount++;
                    reserved = true;
                }
            }

            if (!reserved)
            {
                return await FailArtifactTransferAsync(
                    transportClient,
                    fileCountLimitReached
                        ? $"Test-host artifact '{path}' was not exported because the peer's {_fileArtifactTransferMaxFileCount}-file per-module export limit was reached."
                        : $"Test-host artifact '{path}' was not exported because the peer's {_fileArtifactTransferMaxModuleBytes}-byte per-module export limit was reached.",
                    cancellationToken).ConfigureAwait(false);
            }

            bool committed = false;
            try
            {
                string transferId = Guid.NewGuid().ToString("N");
                long offset = 0;
                using FileStream stream = file.OpenRead();
                do
                {
                    int chunkLength = (int)Math.Min(_fileArtifactTransferMaxChunkBytes, fileLength - offset);
                    byte[] data = new byte[chunkLength];
                    int bytesRead = 0;
                    while (bytesRead < data.Length)
                    {
                        int read = await stream.ReadAsync(
                            data,
                            bytesRead,
                            data.Length - bytesRead,
                            cancellationToken).ConfigureAwait(false);
                        if (read == 0)
                        {
                            throw new EndOfStreamException($"Test-host artifact '{path}' ended before its declared length.");
                        }

                        bytesRead += read;
                    }

                    bool isFinal = offset + bytesRead == fileLength;
                    var request = new FileArtifactTransferRequest(
                        executionId,
                        instanceId,
                        transferId,
                        file.Name,
                        fileLength,
                        offset,
                        data,
                        isFinal);
                    FileArtifactTransferResponse response =
                        await transportClient.RequestReplyAsync<FileArtifactTransferRequest, FileArtifactTransferResponse>(
                            request,
                            cancellationToken).ConfigureAwait(false);
                    if (!RoslynString.IsNullOrEmpty(response.Error))
                    {
                        return await FailArtifactTransferAsync(
                            transportClient,
                            $"Test-host artifact '{path}' was rejected by the dotnet test peer: {response.Error}",
                            cancellationToken).ConfigureAwait(false);
                    }

                    long expectedNextOffset = offset + bytesRead;
                    if (response.NextOffset != expectedNextOffset)
                    {
                        return await FailArtifactTransferAsync(
                            transportClient,
                            $"Test-host artifact '{path}' export stopped because the peer acknowledged offset {response.NextOffset} instead of {expectedNextOffset}.",
                            cancellationToken).ConfigureAwait(false);
                    }

                    offset = expectedNextOffset;
                    if (isFinal)
                    {
                        if (RoslynString.IsNullOrEmpty(response.FullPath))
                        {
                            return await FailArtifactTransferAsync(
                                transportClient,
                                $"Test-host artifact '{path}' export completed without a committed host path.",
                                cancellationToken).ConfigureAwait(false);
                        }

                        committed = true;
                        return response.FullPath;
                    }
                }
                while (offset < fileLength);
            }
            finally
            {
                if (!committed)
                {
                    lock (_artifactTransferLock)
                    {
                        _reservedArtifactTransferBytes -= fileLength;
                        _reservedArtifactTransferFileCount--;
                    }
                }
            }

            return await FailArtifactTransferAsync(
                transportClient,
                $"Test-host artifact '{path}' export ended without a final acknowledgment.",
                cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (ArtifactTransferException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw await CreateArtifactTransferExceptionAsync(
                transportClient,
                $"Test-host artifact '{path}' could not be exported: {ex.Message}",
                cancellationToken,
                ex).ConfigureAwait(false);
        }
    }

    private async Task<string> FailArtifactTransferAsync(
        IClient transportClient,
        string message,
        CancellationToken cancellationToken)
        => throw await CreateArtifactTransferExceptionAsync(
            transportClient,
            message,
            cancellationToken,
            innerException: null).ConfigureAwait(false);

    private async Task<ArtifactTransferException> CreateArtifactTransferExceptionAsync(
        IClient transportClient,
        string message,
        CancellationToken cancellationToken,
        Exception? innerException)
    {
        await TryLogAsync(LogLevel.Error, message).ConfigureAwait(false);
        if (IsDisplayMessageForwardingSupported)
        {
            await transportClient.RequestReplyAsync<DisplayMessage, VoidResponse>(
                new DisplayMessage(
                    _environment.GetEnvironmentVariable(EnvironmentVariableConstants.TESTINGPLATFORM_DOTNETTEST_EXECUTIONID),
                    InstanceId,
                    DisplayMessageLevels.Error,
                    message),
                cancellationToken).ConfigureAwait(false);
        }

        return new ArtifactTransferException(message, innerException);
    }

    private async Task ReportArtifactTransferWarningAsync(
        IClient transportClient,
        string message,
        CancellationToken cancellationToken)
    {
        await TryLogAsync(LogLevel.Warning, message).ConfigureAwait(false);
        if (IsDisplayMessageForwardingSupported)
        {
            await transportClient.RequestReplyAsync<DisplayMessage, VoidResponse>(
                new DisplayMessage(
                    _environment.GetEnvironmentVariable(EnvironmentVariableConstants.TESTINGPLATFORM_DOTNETTEST_EXECUTIONID),
                    InstanceId,
                    DisplayMessageLevels.Warning,
                    message),
                cancellationToken).ConfigureAwait(false);
        }
    }

    private static bool TryReadPositiveInt32(HandshakeMessage response, byte propertyName, out int value)
    {
        value = 0;
        return response.Properties?.TryGetValue(propertyName, out string? text) is true
            && int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out value)
            && value > 0;
    }

    private static bool TryReadPositiveInt64(HandshakeMessage response, byte propertyName, out long value)
    {
        value = 0;
        return response.Properties?.TryGetValue(propertyName, out string? text) is true
            && long.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out value)
            && value > 0;
    }

    private sealed class ArtifactTransferException : Exception
    {
        public ArtifactTransferException(string message, Exception? innerException)
            : base(message, innerException)
        {
        }
    }

    private async Task TryLogAsync(LogLevel logLevel, string message)
    {
        var loggingTask = Task.Run(async () =>
        {
            try
            {
                await _logger.LogAsync(logLevel, message, null, LoggingExtensions.Formatter).ConfigureAwait(false);
            }
            catch (Exception)
            {
                // Control-channel diagnostics must never alter connection, cancellation, or shutdown behavior.
            }
        });

        // Isolate synchronous provider work and bound providers that return a task which never completes.
        await Task.WhenAny(loggingTask, Task.Delay(TimeSpan.FromSeconds(1))).ConfigureAwait(false);
    }
}
