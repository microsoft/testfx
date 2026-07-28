// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Net.Sockets;

using Microsoft.Testing.Internal.Framework;
using Microsoft.Testing.Platform.Extensions.OutputDevice;
using Microsoft.Testing.Platform.Extensions.TestFramework;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Logging;
using Microsoft.Testing.Platform.OutputDevice;
using Microsoft.Testing.Platform.Resources;
using Microsoft.Testing.Platform.ServerMode;
using Microsoft.Testing.Platform.Services;
using Microsoft.Testing.Platform.Telemetry;
using Microsoft.Testing.Platform.TestHost;

namespace Microsoft.Testing.Platform.Hosts;

[StackTraceHidden]
internal sealed partial class ServerTestHost : CommonHost, IServerTestHost, IDisposable, IOutputDeviceDataProducer
{
    // The value is the build-time version stamp (e.g. "2.4.0-dev" locally vs "2.4.0-ci" on CI), so it is an
    // implementation detail rather than a stable API surface and must not be tracked by the internal API analyzers.
#pragma warning disable RS0051 // Add internal types and members to the declared API
    public const string ProtocolVersion = PlatformVersion.Version;
#pragma warning restore RS0051 // Add internal types and members to the declared API
    private readonly Func<TestFrameworkBuilderData, Task<ITestFramework>> _buildTestFrameworkAsync;

    private readonly IMessageHandlerFactory _messageHandlerFactory;
    private readonly TestFrameworkManager _testFrameworkManager;
    private readonly TestHostManager _testSessionManager;
    private readonly IAsyncMonitor _messageMonitor;
    private readonly IEnvironment _environment;
    private readonly ILogger<ServerTestHost> _logger;
    private readonly CancellationTokenSource _messageHandlerStopPlusGlobalTokenSource;
    private readonly CancellationTokenSource _serverClosingTokenSource = new();
    private readonly CancellationTokenSource _stopMessageHandler = new();

    // We start by one so we can wait all other requests
    private readonly CountdownEvent _requestCounter = new(1);
    private readonly IClock _clock;

    // In-flight requests from the client to the server.
    // The client can cancel these requests at any time.
    // When the server completes the handler it will complete the backing RpcRequest.
    private ConcurrentDictionary<int, RpcInvocationState> _clientToServerRequests;

    // In-flight requests from the server to the client.
    // Whenever a client responds with a result or an error, the requests
    // get completed.
    private ConcurrentDictionary<int, RpcInvocationState> _serverToClientRequests;
    private IMessageHandler? _messageHandler;
    private TestHost.ClientInfo? _client;
    private IClientInfo? _clientInfoService;

    public ServerTestHost(
        ServiceProvider serviceProvider,
        Func<TestFrameworkBuilderData, Task<ITestFramework>> buildTestFrameworkAsync,
        IMessageHandlerFactory messageHandlerFactory,
        TestFrameworkManager testFrameworkManager,
        TestHostManager testSessionManager)
        : base(serviceProvider)
    {
        _buildTestFrameworkAsync = buildTestFrameworkAsync;
        _messageHandlerFactory = messageHandlerFactory;
        _testFrameworkManager = testFrameworkManager;
        _testSessionManager = testSessionManager;
        _clientToServerRequests = new();
        _serverToClientRequests = new();

        IAsyncMonitorFactory monitorFactory = ServiceProvider.GetAsyncMonitorFactory();
        _messageMonitor = monitorFactory.Create();

        _environment = ServiceProvider.GetEnvironment();
        _clock = ServiceProvider.GetClock();

        _logger = ServiceProvider.GetLoggerFactory().CreateLogger<ServerTestHost>();
        _messageHandlerStopPlusGlobalTokenSource = CancellationTokenSource.CreateLinkedTokenSource(serviceProvider.GetTestApplicationCancellationTokenSource().CancellationToken, _stopMessageHandler.Token);

        // If we don't want to crash on unhandled exceptions, handle them differently
        if (!ServiceProvider.GetUnhandledExceptionsPolicy().FastFailOnFailure)
        {
            AppDomain.CurrentDomain.UnhandledException += OnCurrentDomainUnhandledException;
            TaskScheduler.UnobservedTaskException += OnTaskSchedulerUnobservedTaskException;
        }
    }

    public bool IsInitialized => _messageHandler is not null;

    protected override bool RunTestApplicationLifeCycleCallbacks => true;

    public string Uid => nameof(ServerTestHost);

    public string Version => PlatformVersion.Version;

    public string DisplayName => PlatformResources.ServerTestHostDisplayName;

    public string Description => PlatformResources.ServerTestHostDescription;

    private void OnCurrentDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        const string Prefix = "[ServerTestHost.OnCurrentDomainUnhandledException]";
        var exception = e.ExceptionObject as Exception;

        // Log the exception via the structured exception parameter so sinks can capture it
        // independently of the message text. Output device still gets the human-readable form.
        if (exception is not null)
        {
            _logger.Log(LogLevel.Warning, $"{Prefix} IsTerminating: {e.IsTerminating}", exception, LoggingExtensions.Formatter);
        }
        else
        {
            _logger.LogWarning($"{Prefix} {e.ExceptionObject}{_environment.NewLine}IsTerminating: {e.IsTerminating}");
        }

        // Looks like nothing in this message to really be localized?
        // All are class names, method names, property names, and placeholders. So none is localizable?
        ServiceProvider.GetOutputDevice().DisplayAsync(
            this,
            new WarningMessageOutputDeviceData(
                $"{Prefix} {e.ExceptionObject}{_environment.NewLine}IsTerminating: {e.IsTerminating}"), CancellationToken.None)
            .GetAwaiter().GetResult();
    }

    private void OnTaskSchedulerUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        e.SetObserved();
        _logger.Log(LogLevel.Warning, "[ServerTestHost.OnTaskSchedulerUnobservedTaskException] Unhandled task exception", e.Exception, LoggingExtensions.Formatter);

        ServiceProvider.GetOutputDevice().DisplayAsync(this, new WarningMessageOutputDeviceData(string.Format(CultureInfo.InvariantCulture, PlatformResources.UnobservedTaskExceptionWarningMessage, e.Exception.ToString())), CancellationToken.None)
            .GetAwaiter().GetResult();
    }

    [MemberNotNull(nameof(_messageHandler))]
    public void AssertInitialized()
    {
        if (_messageHandler is null)
        {
            throw new InvalidOperationException();
        }
    }

    protected override async Task<int> InternalRunAsync(CancellationToken cancellationToken)
    {
        using IPlatformActivity? activity = ServiceProvider.GetPlatformOTelService()?.StartActivity("ServerTestHost");

        await _logger.LogDebugAsync("Starting server mode").ConfigureAwait(false);

        try
        {
            _messageHandler = await _messageHandlerFactory.CreateMessageHandlerAsync(cancellationToken).ConfigureAwait(false);

            await HandleMessagesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when
            // When the cancellation token fires during TCP connect or message handling, several
            // exception types can surface depending on the exact timing:
            (cancellationToken.IsCancellationRequested
            // the standard cancellation path.
            && (ex is OperationCanceledException
                // the TcpClient/stream was disposed while an async operation was in flight.
                or ObjectDisposedException
                // TOCTOU race in the runtime: ConnectAsync completed successfully at the OS level,
                // but the cancellation callback (registered via CancellationToken.UnsafeRegister
                // in SocketAsyncEventArgs.ProcessIOCPResult) called CancelIoEx, tearing down
                // the socket's connected state before GetStream() could read it.
                or InvalidOperationException
                // the pending overlapped I/O was cancelled by CancelIoEx (Windows) from the
                // cancellation callback, completing with SocketError.OperationAborted.
                or SocketException { SocketErrorCode: SocketError.OperationAborted }))
        {
        }
        finally
        {
            (_messageHandler as IDisposable)?.Dispose();

            // Cleanup all services but special one because in the per-call mode we needed to keep them alive for reuse
            await DisposeServiceProviderAsync(ServiceProvider).ConfigureAwait(false);
        }

        // If the global cancellation is called together with the server closing one the server exited gracefully.
        return !cancellationToken.IsCancellationRequested && _serverClosingTokenSource.IsCancellationRequested
            ? (int)ExitCode.Success
            : (int)ExitCode.TestSessionAborted;
    }

    public void Dispose()
    {
        // Note: The lifetime of the _reader/_writer should be currently handled by the RunAsync()
        // We could consider creating a stateful engine that has the lifetime == server connection UP.
        if (!ServiceProvider.GetUnhandledExceptionsPolicy().FastFailOnFailure)
        {
            AppDomain.CurrentDomain.UnhandledException -= OnCurrentDomainUnhandledException;
            TaskScheduler.UnobservedTaskException -= OnTaskSchedulerUnobservedTaskException;
        }
    }
}
