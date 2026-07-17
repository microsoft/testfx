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

[UnsupportedOSPlatform("browser")]
internal sealed class DotnetTestConnection : IPushOnlyProtocol, IDisposable
{
    private readonly CommandLineHandler _commandLineHandler;
    private readonly IEnvironment _environment;
    private readonly ITestApplicationModuleInfo _testApplicationModuleInfo;
    private readonly ITestApplicationCancellationTokenSource _cancellationTokenSource;
    private readonly ILogger _logger;

    private NamedPipeClient? _dotnetTestPipeClient;

    private NamedPipeClient? _serverControlPipeClient;
    private Task? _serverControlListenerTask;
    private CancellationTokenSource? _serverControlListenerCts;
    private string? _serverControlPipeName;
    private int _cancelRequested;

    public static string InstanceId { get; } = Guid.NewGuid().ToString("N");

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

    public bool IsServerMode => _dotnetTestPipeClient?.IsConnected == true;

    public Task<IPushOnlyProtocolConsumer> GetDataConsumerAsync()
        => Task.FromResult((IPushOnlyProtocolConsumer)new DotnetTestDataConsumer(this, _environment));

    public async Task AfterCommonServiceSetupAsync()
    {
        // If we are in server mode and the pipe name is provided
        // then, we need to connect to the pipe server.
        if (_commandLineHandler.HasDotnetTestServerOption() &&
            _commandLineHandler.TryGetOptionArgumentList(PlatformCommandLineProvider.DotNetTestPipeOptionKey, out string[]? arguments))
        {
            // The execution id is used to identify the test execution
            // We are storing it as an env var so that it can be read by the test host, test host controller and the test host orchestrator
            // If it already exists, we don't overwrite it
            if (RoslynString.IsNullOrEmpty(_environment.GetEnvironmentVariable(EnvironmentVariableConstants.TESTINGPLATFORM_DOTNETTEST_EXECUTIONID)))
            {
                _environment.SetEnvironmentVariable(EnvironmentVariableConstants.TESTINGPLATFORM_DOTNETTEST_EXECUTIONID, Guid.NewGuid().ToString("N"));
            }

            _dotnetTestPipeClient = new(arguments[0], _environment);
            _dotnetTestPipeClient.RegisterAllSerializers();

            await _dotnetTestPipeClient.ConnectAsync(_cancellationTokenSource.CancellationToken).ConfigureAwait(false);
        }
    }

    public async Task HelpInvokedAsync()
    {
        RoslynDebug.Assert(_dotnetTestPipeClient is not null);

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
                    commandLineOption.IsBuiltIn));
            }
        }

        await _dotnetTestPipeClient.RequestReplyAsync<CommandLineOptionMessages, VoidResponse>(new CommandLineOptionMessages(_testApplicationModuleInfo.GetCurrentTestApplicationFullPath(), [.. commandLineHelpOptions.OrderBy(option => option.Name)]), _cancellationTokenSource.CancellationToken).ConfigureAwait(false);
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

    public async Task<bool> IsCompatibleProtocolAsync(string hostType, IReadOnlyDictionary<byte, string>? additionalHandshakeProperties = null)
    {
        RoslynDebug.Assert(_dotnetTestPipeClient is not null);

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

        HandshakeMessage response = await _dotnetTestPipeClient.RequestReplyAsync<HandshakeMessage, HandshakeMessage>(handshakeMessage, _cancellationTokenSource.CancellationToken).ConfigureAwait(false);

        IsIDE = response.Properties?.TryGetValue(HandshakeMessagePropertyNames.IsIDE, out string? isIDEValue) == true &&
            bool.TryParse(isIDEValue, out bool isIDE) &&
            isIDE;

        if (response.Properties?.TryGetValue(HandshakeMessagePropertyNames.ServerControlPipeName, out string? serverControlPipeName) is true &&
            !RoslynString.IsNullOrEmpty(serverControlPipeName))
        {
            _serverControlPipeName = serverControlPipeName;
            IsServerControlChannelSupported = true;
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
        => _commandLineHandler.IsHelpInvoked()
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
        NamedPipeClient dotnetTestPipeClient = _dotnetTestPipeClient
            ?? throw new InvalidOperationException("The dotnet test pipe client is not connected.");

        switch (message)
        {
            case DiscoveredTestMessages discoveredTestMessages:
                await dotnetTestPipeClient.RequestReplyAsync<DiscoveredTestMessages, VoidResponse>(discoveredTestMessages, _cancellationTokenSource.CancellationToken).ConfigureAwait(false);
                break;

            case TestResultMessages testResultMessages:
                await dotnetTestPipeClient.RequestReplyAsync<TestResultMessages, VoidResponse>(testResultMessages, _cancellationTokenSource.CancellationToken).ConfigureAwait(false);
                break;

            case FileArtifactMessages fileArtifactMessages:
                await dotnetTestPipeClient.RequestReplyAsync<FileArtifactMessages, VoidResponse>(fileArtifactMessages, _cancellationTokenSource.CancellationToken).ConfigureAwait(false);
                break;

            case TestSessionEvent testSessionEvent:
                await dotnetTestPipeClient.RequestReplyAsync<TestSessionEvent, VoidResponse>(testSessionEvent, _cancellationTokenSource.CancellationToken).ConfigureAwait(false);
                break;

            case AzureDevOpsLogMessage azureDevOpsLogMessage:
                await dotnetTestPipeClient.RequestReplyAsync<AzureDevOpsLogMessage, VoidResponse>(azureDevOpsLogMessage, _cancellationTokenSource.CancellationToken).ConfigureAwait(false);
                break;

            case DisplayMessage displayMessage:
                await dotnetTestPipeClient.RequestReplyAsync<DisplayMessage, VoidResponse>(displayMessage, _cancellationTokenSource.CancellationToken).ConfigureAwait(false);
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
        if (!IsServerControlChannelSupported || RoslynString.IsNullOrEmpty(_serverControlPipeName) || _serverControlPipeClient is not null)
        {
            return Task.CompletedTask;
        }

        _serverControlListenerCts = CancellationTokenSource.CreateLinkedTokenSource(_cancellationTokenSource.CancellationToken);

        // exitProcessOnConnectionLoss: false - a dropped control pipe must not kill the test host; the listener
        // turns it into a cooperative cancel instead.
        var controlClient = new NamedPipeClient(_serverControlPipeName, _environment, exitProcessOnConnectionLoss: false);
        controlClient.RegisterAllSerializers();
        _serverControlPipeClient = controlClient;

        // Connect AND listen on a background task: we deliberately do not await the connect on the run path so a
        // slow/absent control server can never delay (or fail) test execution start.
        _serverControlListenerTask = Task.Run(
            () => ConnectAndListenForServerControlAsync(controlClient, onCancelSessionRequestedAsync, _serverControlListenerCts.Token));
        return Task.CompletedTask;
    }

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

        await TryLogBoundedAsync(LogLevel.Debug, $"Connected to the server control pipe '{_serverControlPipeName}'.").ConfigureAwait(false);

        await ListenForServerControlAsync(controlClient, onCancelSessionRequestedAsync, cancellationToken).ConfigureAwait(false);
    }

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
        _serverControlPipeClient?.Dispose();

        if (_serverControlListenerTask is { } listenerTask)
        {
            // Bounded wait so a stuck listener can never hang the exit path on any target framework.
            Task completed = await Task.WhenAny(listenerTask, Task.Delay(TimeSpan.FromSeconds(5))).ConfigureAwait(false);
            if (completed != listenerTask)
            {
                await TryLogBoundedAsync(LogLevel.Debug, $"Server control pipe '{_serverControlPipeName}' listener did not finish within the 5s bounded wait during exit; continuing without waiting further.").ConfigureAwait(false);
            }
        }
    }

    public void Dispose()
    {
        _serverControlListenerCts?.Cancel();

        // Disposing the pipe client forces a parked read to abort even where token cancellation alone would not.
        _serverControlPipeClient?.Dispose();

        if (_serverControlListenerTask is { } listenerTask)
        {
            try
            {
                // Bounded wait: the cancel + dispose above unblock the parked read quickly. Never hang exit.
                listenerTask.Wait(TimeSpan.FromSeconds(5));
            }
            catch (Exception)
            {
                // Best-effort shutdown. Logging providers have already been disposed by this phase; diagnostics
                // belong in OnExitAsync, which runs while logging is still available.
            }
        }

        _serverControlListenerCts?.Dispose();
        _dotnetTestPipeClient?.Dispose();
    }

    private async Task TryLogAsync(LogLevel logLevel, string message)
    {
        try
        {
            await _logger.LogAsync(logLevel, message, null, LoggingExtensions.Formatter).ConfigureAwait(false);
        }
        catch (Exception)
        {
            // Control-channel diagnostics must never alter connection, cancellation, or shutdown behavior.
        }
    }

    private async Task TryLogBoundedAsync(LogLevel logLevel, string message)
    {
        // Isolate synchronous provider work, then drain briefly while logging is still alive. This keeps the exit
        // bound finite and avoids racing service-provider disposal for the diagnostic this path exists to emit.
        var loggingTask = Task.Run(() => TryLogAsync(logLevel, message));
        await Task.WhenAny(loggingTask, Task.Delay(TimeSpan.FromSeconds(1))).ConfigureAwait(false);
    }
}
