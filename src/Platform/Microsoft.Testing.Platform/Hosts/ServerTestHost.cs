// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Net.Sockets;

using Microsoft.Testing.Internal.Framework;
using Microsoft.Testing.Platform.Capabilities.TestFramework;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Extensions.TestFramework;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Logging;
using Microsoft.Testing.Platform.Messages;
using Microsoft.Testing.Platform.Requests;
using Microsoft.Testing.Platform.ServerMode;
using Microsoft.Testing.Platform.Services;
using Microsoft.Testing.Platform.Telemetry;
using Microsoft.Testing.Platform.TestHost;

namespace Microsoft.Testing.Platform.Hosts;

internal sealed partial class ServerTestHost : CommonTestHost, IServerTestHost, IDisposable
{
    public const string ProtocolVersion = "1.0.0";
    private readonly Func<TestFrameworkBuilderData, Task<ITestFramework>> _buildTestFrameworkAsync;

    private readonly IMessageHandlerFactory _messageHandlerFactory;
    private readonly TestFrameworkManager _testFrameworkManager;
    private readonly ITestApplicationCancellationTokenSource _testApplicationCancellationTokenSource;
    private readonly TestHostManager _testSessionManager;
    private readonly ServerTelemetry _telemetryService;
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
    private int _serverToClientRequestId;
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
        _testApplicationCancellationTokenSource = serviceProvider.GetTestApplicationCancellationTokenSource();
        _testSessionManager = testSessionManager;
        _telemetryService = new ServerTelemetry(this);
        _clientToServerRequests = new();
        _serverToClientRequests = new();

        IAsyncMonitorFactory monitorFactory = ServiceProvider.GetAsyncMonitorFactory();
        _messageMonitor = monitorFactory.Create();

        _environment = ServiceProvider.GetEnvironment();
        _clock = ServiceProvider.GetClock();

        _logger = ServiceProvider.GetLoggerFactory().CreateLogger<ServerTestHost>();
        _messageHandlerStopPlusGlobalTokenSource = CancellationTokenSource.CreateLinkedTokenSource(_testApplicationCancellationTokenSource.CancellationToken, _stopMessageHandler.Token);

        // If we don't want to crash on unhandled exceptions, handle them differently
        if (!ServiceProvider.GetUnhandledExceptionsPolicy().FastFailOnFailure)
        {
            AppDomain.CurrentDomain.UnhandledException += OnCurrentDomainUnhandledException;
            TaskScheduler.UnobservedTaskException += OnTaskSchedulerUnobservedTaskException;
        }
    }

    public bool IsInitialized => _messageHandler is not null;

    protected override bool RunTestApplicationLifeCycleCallbacks => true;

    private void OnCurrentDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
        => _logger.LogWarning($"[ServerTestHost.OnCurrentDomainUnhandledException] {e.ExceptionObject}{_environment.NewLine}IsTerminating: {e.IsTerminating}");

    private void OnTaskSchedulerUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
    {
        e.SetObserved();
        _logger.LogWarning($"[ServerTestHost.OnTaskSchedulerUnobservedTaskException] Unhandled exception: {e.Exception}");
    }

    [MemberNotNull(nameof(_messageHandler))]
    public void AssertInitialized()
    {
        if (_messageHandler is null)
        {
            throw new InvalidOperationException();
        }
    }

    protected override async Task<int> InternalRunAsync()
    {
        try
        {
            await _logger.LogDebugAsync("Starting server mode");
            _messageHandler = await _messageHandlerFactory.CreateMessageHandlerAsync(_testApplicationCancellationTokenSource.CancellationToken);

            // Initialize the ServerLoggerForwarderProvider, it can be null if diagnostic is disabled.
            ServerLoggerForwarderProvider? serviceLoggerForwarder = ServiceProvider.GetService<ServerLoggerForwarderProvider>();
            if (serviceLoggerForwarder is not null)
            {
                await serviceLoggerForwarder.InitializeAsync(this);
            }

            await HandleMessagesAsync();

            (_messageHandler as IDisposable)?.Dispose();
        }
        catch (OperationCanceledException ex) when (ex.CancellationToken == _testApplicationCancellationTokenSource.CancellationToken)
        {
        }
        catch (SocketException ex) when (ex.SocketErrorCode == SocketError.OperationAborted && _testApplicationCancellationTokenSource.CancellationToken.IsCancellationRequested)
        {
        }
        catch (ObjectDisposedException) when (_testApplicationCancellationTokenSource.CancellationToken.IsCancellationRequested)
        {
        }
        finally
        {
            // Cleanup all services but special one because in the per-call mode we needed to keep them alive for reuse
            await DisposeServiceProviderAsync(ServiceProvider);
        }

        // If the global cancellation is called together with the server closing one the server exited gracefully.
        return !_testApplicationCancellationTokenSource.CancellationToken.IsCancellationRequested && _serverClosingTokenSource.IsCancellationRequested
            ? ExitCodes.Success
            : ExitCodes.TestSessionAborted;
    }

    /// <summary>
    /// The main server loop.
    /// It receives messages from the client and then runs a corresponding handler.
    /// </summary>
    private async Task HandleMessagesAsync()
    {
        AssertInitialized();

        CancellationToken messageHandlerStopPlusGlobalToken = _messageHandlerStopPlusGlobalTokenSource.Token;
        while (!_messageHandlerStopPlusGlobalTokenSource.IsCancellationRequested)
        {
            try
            {
                RpcMessage? message = await _messageHandler.ReadAsync(messageHandlerStopPlusGlobalToken);

                // In case of issue on underneath handler we expect a null rpc message to signal that we should close
                // because we're no more able to process things.
                if (message is null)
                {
                    return;
                }

                // Signal that we have to handle this request
                _requestCounter.AddCount();

                if (message is NotificationMessage { Method: JsonRpcMethods.Exit })
                {
                    // Signal only one time
                    if (!_serverClosingTokenSource.IsCancellationRequested)
                    {
                        await _logger.LogDebugAsync("Server requested to shutdown");
                        await _serverClosingTokenSource.CancelAsync();
                    }

                    // Signal the exit call
                    _requestCounter.Signal();

                    // If there're no in-flight request we can close the server
                    if (_clientToServerRequests.IsEmpty)
                    {
                        await _stopMessageHandler.CancelAsync();
                    }

                    continue;
                }

                // Note: Handle the requests and notifications asynchronously, so that
                // we can keep reading further messages.
                // For instance we should be able to handle a cancellation request
                // while a discovery request is being handled.
                switch (message)
                {
                    case RequestMessage request:
                        // This task is recorded inside the _clientToServerRequests
                        _ = HandleRequestAsync(request, _serverClosingTokenSource.Token);
                        break;

                    case NotificationMessage notification:
                        // This task is recorded inside the _clientToServerRequests
                        _ = HandleNotificationAsync(notification, _serverClosingTokenSource.Token);
                        break;
                    case ResponseMessage response:
                        CompleteRequest(ref _serverToClientRequests, response.Id, completion => completion.TrySetResult(response));
                        break;

                    case ErrorMessage error:
                        RemoteInvocationException exception = new();
                        CompleteRequest(ref _serverToClientRequests, error.Id, completion => completion.TrySetException(exception));
                        break;
                    default:
                        break;
                }
            }
            catch (OperationCanceledException ex) when (ex.CancellationToken == messageHandlerStopPlusGlobalToken)
            {
                // We're shutting down the reader
                continue;
            }
        }

        // subtract the default count
        _requestCounter.Signal();

        // Wait to drain all in-flight requests HandleRequestCoreAsync/CompleteRequest
        await _requestCounter.WaitAsync(TimeoutHelper.DefaultHangTimeSpanTimeout, CancellationToken.None);
    }

    private async Task HandleNotificationAsync(NotificationMessage message, CancellationToken serverClosing)
    {
        // We need to guarantee that all notification received before the "exit" are handled.
        // We check it before to "enqueue" the task that handle it
        if (serverClosing.IsCancellationRequested)
        {
            try
            {
                // We're closing we don't handle the "new notification"
                return;
            }
            finally
            {
                // Signal the notification
                _requestCounter.Signal();
            }
        }

        // Note: Yield, so that the main message reading loop can continue.
        await Task.Yield();

        try
        {
            switch (message.Method, message.Params)
            {
                case (JsonRpcMethods.CancelRequest, CancelRequestArgs args):
                    if (_clientToServerRequests.TryGetValue(args.CancelRequestId, out RpcInvocationState? rpcState))
                    {
                        Exception? cancellationException = rpcState.CancelRequest();
                        if (cancellationException is null)
                        {
                            await _logger.LogWarningAsync($"Exception during the cancellation of request id '{args.CancelRequestId}'");
                        }
                    }

                    break;
            }
        }
        finally
        {
            // Signal the notification
            _requestCounter.Signal();
        }
    }

    private async Task HandleRequestAsync(RequestMessage request, CancellationToken serverClosing)
    {
        // We're closing so we don't handle anymore any requests
        if (serverClosing.IsCancellationRequested)
        {
            try
            {
                await SendErrorAsync(reqId: request.Id, errorCode: ErrorCodes.InvalidRequest, message: "Server is closing", data: null, _testApplicationCancellationTokenSource.CancellationToken);
            }
            finally
            {
                // Signal the notification
                _requestCounter.Signal();
            }
        }
        else
        {
            // We enqueue the request before to "unlink" the current thread so we're sure that we
            // correctly handle the completion also after the "exit"
            RpcInvocationState rpcState = new();
            _clientToServerRequests.TryAdd(request.Id, rpcState);

            // Note: Yield, so that the main message reading loop can continue.
            await Task.Yield();

            try
            {
                object response = await HandleRequestCoreAsync(request, rpcState);
                await SendResponseAsync(reqId: request.Id, result: response, _testApplicationCancellationTokenSource.CancellationToken);
                CompleteRequest(ref _clientToServerRequests, request.Id, completion => completion.TrySetResult(response));
            }
            catch (OperationCanceledException e)
            {
                // We don't return the stack of the exception if we're canceling the single request because it's expected and it's not an exception.
                (string errorMessage, int errorCode) =
                    rpcState.CancellationToken.IsCancellationRequested
                    ? (string.Empty, ErrorCodes.RequestCanceled)
                    : (e.ToString(), ErrorCodes.RequestCanceled);

                await SendErrorAsync(reqId: request.Id, errorCode: errorCode, message: errorMessage, data: null, _testApplicationCancellationTokenSource.CancellationToken);
                CompleteRequest(ref _clientToServerRequests, request.Id, completion => completion.TrySetCanceled());
            }
            catch (Exception e)
            {
                await SendErrorAsync(reqId: request.Id, errorCode: 0, message: e.ToString(), data: null, _testApplicationCancellationTokenSource.CancellationToken);
                CompleteRequest(ref _clientToServerRequests, request.Id, completion => completion.SetException(e));
            }
        }
    }

    private void CompleteRequest(
        ref ConcurrentDictionary<int, RpcInvocationState> rpcStates,
        int reqId,
        Action<TaskCompletionSource<object>> completion)
    {
        try
        {
            if (rpcStates.TryRemove(reqId, out RpcInvocationState? completedInvocation))
            {
                completion(completedInvocation.CompletionSource);
                completedInvocation.Dispose();
            }

            // If we don't have anymore rpc call to handle and "exit" was called we stop the reader and
            // we go to wait to drain the send to the clients.
            if (rpcStates.IsEmpty && _serverClosingTokenSource.IsCancellationRequested)
            {
                _stopMessageHandler.Cancel();
            }
        }
        finally
        {
            // We handled the request
            _requestCounter.Signal();
        }
    }

    private async Task<object> HandleRequestCoreAsync(RequestMessage message, RpcInvocationState rpcInvocationState)
    {
        var perRequestServiceProvider = (ServiceProvider)ServiceProvider.Clone();

        // Add custom linked ITestApplicationCooperativeLifetimeService cancellation token source
        perRequestServiceProvider.AddService(new PerRequestTestSessionContext(
            rpcInvocationState.CancellationToken,
            _testApplicationCancellationTokenSource.CancellationToken));

        perRequestServiceProvider.AddService(new TestHostTestFrameworkInvoker(perRequestServiceProvider));

        AssertInitialized();

        await _logger.LogDebugAsync($"Received {message.Method} request");

        switch (message.Method, message.Params)
        {
            case (JsonRpcMethods.Initialize, InitializeRequestArgs args):
                {
                    _client = new(args.ClientInfo.Name, args.ClientInfo.Version);
                    _clientInfoService = new ClientInfoService(args.ClientInfo.Name, args.ClientInfo.Version);
                    await _logger.LogDebugAsync($"Connection established with '{_client.Id}', protocol version {_client.Version}");

                    INamedFeatureCapability? namedFeatureCapability = ServiceProvider.GetTestFrameworkCapabilities().GetCapability<INamedFeatureCapability>();
                    return new InitializeResponseArgs(
                        ProcessId: ServiceProvider.GetProcessHandler().GetCurrentProcess().Id,
                        ServerInfo: new ServerInfo("test-anywhere", Version: ProtocolVersion),
                        Capabilities: new ServerCapabilities(
                            new ServerTestingCapabilities(
                                SupportsDiscovery: true,
                                // Current implementation of testing platform and VS doesn't allow multi-request.
                                MultiRequestSupport: false,
                                VSTestProviderSupport: namedFeatureCapability?.IsSupported(JsonRpcStrings.VSTestProviderSupport) == true,
                                SupportsAttachments: true,
                                MultiConnectionProvider: false)));
                }

            case (JsonRpcMethods.TestingDiscoverTests, DiscoverRequestArgs args):
                {
                    return await ExecuteRequestAsync(args, JsonRpcMethods.TestingDiscoverTests, perRequestServiceProvider);
                }

            case (JsonRpcMethods.TestingRunTests, RunRequestArgs args):
                {
                    return await ExecuteRequestAsync(args, JsonRpcMethods.TestingRunTests, perRequestServiceProvider);
                }

            default:
                throw new NotImplementedException();
        }
    }

    private async Task<ResponseArgsBase> ExecuteRequestAsync(RequestArgsBase args, string method, ServiceProvider perRequestServiceProvider)
    {
        DateTimeOffset requestStart = _clock.UtcNow;
        ITestSessionContext perRequestTestSessionContext = perRequestServiceProvider.GetTestSessionContext();

        // Verify request cancellation, above the chain the exception will be
        // catch and propagated as correct json rpc error
        perRequestTestSessionContext.CancellationToken.ThrowIfCancellationRequested();

        ICollection<TestNode>? testNodes = args.TestNodes;
        ITestExecutionFilter executionFilter = await _testSessionManager.BuildFilterAsync(ServiceProvider, testNodes);

        ServerTestExecutionRequestFactory requestFactory = new(session =>
            method == JsonRpcMethods.TestingRunTests
                ? new RunTestExecutionRequest(session, executionFilter)
                : method == JsonRpcMethods.TestingDiscoverTests
                    ? new DiscoverTestExecutionRequest(session, executionFilter)
                    : throw new NotImplementedException($"Request not implemented '{method}'"));

        // Build the per request objects
        TestHostTestFrameworkInvoker invoker = new(perRequestServiceProvider);
        PerRequestServerDataConsumer testNodeUpdateProcessor = new(perRequestServiceProvider, this, args.RunId, perRequestServiceProvider.GetTask());

        // Add the client info service to the per request service provider
        RoslynDebug.Assert(_clientInfoService is not null, "Request should only have been called after initialization");
        perRequestServiceProvider.TryAddService(_clientInfoService);

        DateTimeOffset adapterLoadStart = _clock.UtcNow;

        // Build the per request adapter
        ITestFramework perRequestTestFramework = await _buildTestFrameworkAsync(new(
            perRequestServiceProvider,
            requestFactory,
            invoker,
            new ServerModePerCallOutputDevice(),
            [testNodeUpdateProcessor],
            _testFrameworkManager,
            _testSessionManager,
            new MessageBusProxy(),
            method == JsonRpcMethods.TestingDiscoverTests,
            true));

        DateTimeOffset adapterLoadStop = _clock.UtcNow;

        // Reset the stopwatch for the execution
        DateTimeOffset requestExecuteStart = _clock.UtcNow;
        DateTimeOffset? requestExecuteStop = null;
        try
        {
            RoslynDebug.Assert(_client is not null, "Request should only have been called after initialization");

            // Execute the request
            await ExecuteRequestAsync(
                perRequestServiceProvider.GetPlatformOutputDevice(),
                perRequestServiceProvider.GetTestSessionContext(),
                perRequestServiceProvider,
                perRequestServiceProvider.GetBaseMessageBus(),
                perRequestTestFramework,
                _client);

            // Check if there was a test adapter testSession failure
            ITestApplicationProcessExitCode testApplicationResult = perRequestServiceProvider.GetTestApplicationProcessExitCode();
            if (testApplicationResult.HasTestAdapterTestSessionFailure)
            {
                throw new InvalidOperationException($"TestAdapter testSession failure occurred, '{testApplicationResult.TestAdapterTestSessionFailureErrorMessage}'");
            }

            // Verify request cancellation, above the chain the exception will be
            // catch and propagated as correct json rpc error
            perRequestTestSessionContext.CancellationToken.ThrowIfCancellationRequested();
            await SendTestUpdateCompleteAsync(args.RunId);
            requestExecuteStop = _clock.UtcNow;
        }
        finally
        {
            requestExecuteStop ??= _clock.UtcNow;

            // Cleanup all services
            // We skip all services that are "cloned" per call because are reused and will be disposed on shutdown.
            await DisposeServiceProviderAsync(perRequestServiceProvider, obj => !ServiceProvider.Services.Contains(obj));

            // We need to dispose this service manually because the shared DisposeServiceProviderAsync skip some special service like the ITestApplicationCooperativeLifetimeService
            // that needs to be disposed at process exits.
            // Here we have one crafted for per-call and we won't invoke the stopping events on it in the same way as the global one.
            ((PerRequestTestSessionContext)perRequestTestSessionContext).Dispose();
        }

        DateTimeOffset requestStop = _clock.UtcNow;

        RoslynDebug.Assert(requestExecuteStop != null);

        Dictionary<string, object> metadata = method == JsonRpcMethods.TestingRunTests
            ? GetRunMetrics(
                (RunRequestArgs)args,
                requestStart, requestStop,
                adapterLoadStart, adapterLoadStop,
                requestExecuteStart, (DateTimeOffset)requestExecuteStop,
                testNodeUpdateProcessor.GetTestNodeStatistics())
            : method == JsonRpcMethods.TestingDiscoverTests
                ? GetDiscoveryMetrics(
                    (DiscoverRequestArgs)args,
                    requestStart, requestStop,
                    adapterLoadStart, adapterLoadStop,
                    requestExecuteStart, (DateTimeOffset)requestExecuteStop,
                    testNodeUpdateProcessor.GetTestNodeStatistics().TotalDiscoveredTests)
                : throw new NotImplementedException($"Request not implemented '{method}'");

        await _telemetryService.LogEventAsync(TelemetryEvents.TestsRunEventName, metadata);

        return method == JsonRpcMethods.TestingRunTests
            ? new RunResponseArgs(testNodeUpdateProcessor.Artifacts.ToArray())
            : method == JsonRpcMethods.TestingDiscoverTests
                ? (ResponseArgsBase)new DiscoverResponseArgs()
                : throw new NotImplementedException($"Request not implemented '{method}'");
    }

    internal static Dictionary<string, object> GetDiscoveryMetrics(
        DiscoverRequestArgs args,
        DateTimeOffset requestStart, DateTimeOffset requestStop,
        DateTimeOffset adapterLoadStart, DateTimeOffset adapterLoadStop,
        DateTimeOffset requestExecuteStart, DateTimeOffset requestExecuteStop,
        long totalTestsDiscovered) => new()
        {
            { TelemetryProperties.RequestProperties.TotalDiscoveredTestsPropertyName, totalTestsDiscovered },
            { TelemetryProperties.RequestProperties.RequestStart, requestStart },
            { TelemetryProperties.RequestProperties.RequestStop, requestStop },
            { TelemetryProperties.RequestProperties.AdapterLoadStart, adapterLoadStart },
            { TelemetryProperties.RequestProperties.AdapterLoadStop, adapterLoadStop },
            { TelemetryProperties.RequestProperties.RequestExecuteStart, requestExecuteStart },
            { TelemetryProperties.RequestProperties.RequestExecuteStop, requestExecuteStop },
            { TelemetryProperties.RequestProperties.IsFilterEnabledPropertyName, (args.TestNodes is not null || args.GraphFilter is not null).AsTelemetryBool() },
        };

    internal static Dictionary<string, object> GetRunMetrics(
        RunRequestArgs args,
        DateTimeOffset requestStart, DateTimeOffset requestStop,
        DateTimeOffset adapterLoadStart, DateTimeOffset adapterLoadStop,
        DateTimeOffset requestExecuteStart, DateTimeOffset requestExecuteStop,
        TestNodeStatistics statistics) => new()
        {
            { TelemetryProperties.RequestProperties.TotalPassedTestsPropertyName, statistics.TotalPassedTests },
            { TelemetryProperties.RequestProperties.TotalFailedTestsPropertyName, statistics.TotalFailedTests },
            { TelemetryProperties.RequestProperties.TotalPassedRetriesPropertyName, statistics.TotalPassedRetries },
            { TelemetryProperties.RequestProperties.TotalFailedRetriesPropertyName, statistics.TotalFailedRetries },
            { TelemetryProperties.RequestProperties.RequestStart, requestStart },
            { TelemetryProperties.RequestProperties.RequestStop, requestStop },
            { TelemetryProperties.RequestProperties.AdapterLoadStart, adapterLoadStart },
            { TelemetryProperties.RequestProperties.AdapterLoadStop, adapterLoadStop },
            { TelemetryProperties.RequestProperties.RequestExecuteStart, requestExecuteStart },
            { TelemetryProperties.RequestProperties.RequestExecuteStop, requestExecuteStop },
            { TelemetryProperties.RequestProperties.IsFilterEnabledPropertyName, (args.TestNodes is not null || args.GraphFilter is not null).AsTelemetryBool() },
        };

    private async Task SendErrorAsync(int reqId, int errorCode, string message, object? data, CancellationToken cancellationToken)
    {
        AssertInitialized();
        ErrorMessage error = new(reqId, errorCode, message, data);

        using (await _messageMonitor.LockAsync(cancellationToken))
        {
            await _messageHandler.WriteRequestAsync(error, cancellationToken);
        }
    }

    private async Task SendResponseAsync(int reqId, object result, CancellationToken cancellationToken)
    {
        AssertInitialized();
        ResponseMessage response = new(reqId, result);

        using (await _messageMonitor.LockAsync(cancellationToken))
        {
            await _messageHandler.WriteRequestAsync(response, cancellationToken);
        }
    }

    private async Task SendMessageAsync(string method, object? @params, CancellationToken cancellationToken, bool checkServerExit = false, bool rethrowException = true)
    {
        if (checkServerExit && _messageHandlerStopPlusGlobalTokenSource.IsCancellationRequested)
        {
            return;
        }

        _requestCounter.AddCount();
        try
        {
            NotificationMessage notification = new(method, @params);

            using (await _messageMonitor.LockAsync(cancellationToken))
            {
                AssertInitialized();
                await _messageHandler.WriteRequestAsync(notification, cancellationToken);
            }
        }
        catch
        {
            if (rethrowException)
            {
                throw;
            }
        }
        finally
        {
            _requestCounter.Signal();
        }
    }

    public void Dispose()
    {
        // Note: The lifetime of the _reader/_writer should be currently handled by the RunAsync()
        // We could consider creating a stateful engine that has the lifetime == server connection UP.
    }

    internal async Task SendTestUpdateCompleteAsync(Guid runId)
        => await SendTestUpdateAsync(new TestNodeStateChangedEventArgs(runId, Changes: null));

    public async Task SendTestUpdateAsync(TestNodeStateChangedEventArgs update)
        => await SendMessageAsync(
            method: JsonRpcMethods.TestingTestUpdatesTests,
            @params: update,
            _testApplicationCancellationTokenSource.CancellationToken);

    public async Task SendTelemetryEventUpdateAsync(TelemetryEventArgs args)
        => await SendMessageAsync(
            method: JsonRpcMethods.TelemetryUpdate,
            @params: args,
            _testApplicationCancellationTokenSource.CancellationToken);

    public async Task SendClientLaunchDebuggerAsync(ProcessInfoArgs args)
       => await SendRequestAsync(
           method: JsonRpcMethods.ClientLaunchDebugger,
           @params: args,
           _testApplicationCancellationTokenSource.CancellationToken);

    public async Task SendClientAttachDebuggerAsync(AttachDebuggerInfoArgs args)
       => await SendRequestAsync(
           method: JsonRpcMethods.ClientAttachDebugger,
           @params: args,
           _testApplicationCancellationTokenSource.CancellationToken);

    private async Task SendRequestAsync(string method, object @params, CancellationToken cancellationToken)
    {
        AssertInitialized();
        int requestId = Interlocked.Increment(ref _serverToClientRequestId);
        RequestMessage request = new(requestId, method, @params);
        RpcInvocationState invocationState = new();

        _serverToClientRequests.TryAdd(requestId, invocationState);

        // Add the request to the counter
        _requestCounter.AddCount();
        await _messageHandler.WriteRequestAsync(request, cancellationToken);

        using (cancellationToken.Register(() => _ = SendMessageAsync(
            JsonRpcMethods.CancelRequest,
            new CancelRequestArgs(requestId),
            cancellationToken)))
        {
            await invocationState.CompletionSource.Task;
        }
    }

    public async Task PushDataAsync(IData value)
    {
        switch (value)
        {
            case ServerLogMessage logMessage:
                await SendMessageAsync(
                    method: JsonRpcMethods.ClientLog,
                    @params: new LogEventArgs(logMessage),
                    _testApplicationCancellationTokenSource.CancellationToken,

                    // We could receive some log messages after the exit, a real sample is if telemetry provider is too slow and we log a warning.
                    checkServerExit: true,
                    rethrowException: false);
                break;
        }
    }

    private sealed class RpcInvocationState : IDisposable
    {
        private readonly Lock _cancellationTokenSourceLock = new();
        private readonly CancellationTokenSource _cancellationTokenSource = new();
        private volatile bool _isDisposed;

        /// <remarks>
        /// For outbound requests, this is populated with the response from the client.
        /// For inbound requests, this is set when the invoked request is completed
        /// in <see cref="HandleRequestAsync(RequestMessage, CancellationToken)"/>.
        /// </remarks>
        public TaskCompletionSource<object> CompletionSource { get; } = new TaskCompletionSource<object>();

        // We don't expose directly the source because we need to synchronize the complete/cancel
        public CancellationToken CancellationToken => _cancellationTokenSource.Token;

        public AggregateException? CancelRequest()
        {
            if (!_isDisposed)
            {
                lock (_cancellationTokenSourceLock)
                {
                    if (!_isDisposed)
                    {
                        try
                        {
                            _cancellationTokenSource.Cancel();
                        }

                        // We don't want to crash the server if cancellation fails due to improper usage of token.
                        // We report it to the caller for logging purposes.
                        catch (AggregateException ex)
                        {
                            return ex;
                        }
                    }
                }
            }

            return null;
        }

        public void Dispose()
        {
            lock (_cancellationTokenSourceLock)
            {
                if (!_isDisposed)
                {
                    _cancellationTokenSource.Dispose();
                    _isDisposed = true;
                }
            }
        }
    }
}
