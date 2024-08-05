// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Net;
using System.Net.Sockets;
using System.Text;

using Microsoft.Testing.Platform.Capabilities;
using Microsoft.Testing.Platform.Capabilities.TestFramework;
using Microsoft.Testing.Platform.Extensions.TestFramework;
using Microsoft.Testing.Platform.Extensions.TestHost;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.ServerMode;
using Microsoft.Testing.Platform.Services;

namespace Microsoft.Testing.Platform.UnitTests;

[TestGroup]
public class ServerTests : TestBase
{
    public ServerTests(ITestExecutionContext testExecutionContext)
        : base(testExecutionContext)
    {
        if (IsHotReloadEnabled(new SystemEnvironment()))
        {
            throw new NotSupportedException("Tests of this class cannot work correctly under hot reload.");
        }
    }

    private static bool IsHotReloadEnabled(SystemEnvironment environment) => environment.GetEnvironmentVariable(EnvironmentVariableConstants.DOTNET_WATCH) == "1"
        || environment.GetEnvironmentVariable(EnvironmentVariableConstants.TESTINGPLATFORM_HOTRELOAD_ENABLED) == "1";

    public async Task ServerCanBeStartedAndAborted_TcpIp() => await RetryHelper.RetryAsync(
                async () =>
                {
                    using var server = TcpServer.Create();

                    TestApplicationHooks testApplicationHooks = new();
                    string[] args = ["--no-banner", "--server", "--client-host", "localhost", "--client-port", $"{server.Port}", "--internal-testingplatform-skipbuildercheck"];
                    ITestApplicationBuilder builder = await TestApplication.CreateBuilderAsync(args);
                    builder.TestHost.AddTestApplicationLifecycleCallbacks(_ => testApplicationHooks);
                    builder.RegisterTestFramework(_ => new TestFrameworkCapabilities(), (_, __) => new MockTestAdapter());
                    var testApplication = (TestApplication)await builder.BuildAsync();
                    testApplication.ServiceProvider.GetRequiredService<SystemConsole>().SuppressOutput();
                    Task<int> serverTask = testApplication.RunAsync();

                    await testApplicationHooks.WaitForBeforeRunAsync();
                    ITestApplicationCancellationTokenSource stopService = testApplication.ServiceProvider.GetTestApplicationCancellationTokenSource();

                    stopService.Cancel();
                    Assert.AreEqual(ExitCodes.TestSessionAborted, await serverTask);
                }, 3, TimeSpan.FromSeconds(10));

    public async Task ServerCanInitialize()
    {
        using var server = TcpServer.Create();

        string[] args = ["--no-banner", $"--server", "--client-port", $"{server.Port}", "--internal-testingplatform-skipbuildercheck"];
        TestApplicationHooks testApplicationHooks = new();
        ITestApplicationBuilder builder = await TestApplication.CreateBuilderAsync(args);
        builder.TestHost.AddTestApplicationLifecycleCallbacks(_ => testApplicationHooks);
        builder.RegisterTestFramework(_ => new TestFrameworkCapabilities(), (_, __) => new MockTestAdapter());
        var testApplication = (TestApplication)await builder.BuildAsync();
        testApplication.ServiceProvider.GetRequiredService<SystemConsole>().SuppressOutput();
        var serverTask = Task.Run(testApplication.RunAsync);

        using CancellationTokenSource timeout = new(TimeoutHelper.DefaultHangTimeSpanTimeout);
        using TcpClient client = await server.WaitForConnectionAsync(timeout.Token);
        using NetworkStream stream = client.GetStream();
        using StreamWriter writer = new(stream, Encoding.UTF8);
        TcpMessageHandler messageHandler = new(
                client,
                clientToServerStream: client.GetStream(),
                serverToClientStream: client.GetStream(),
                FormatterUtilities.CreateFormatter());

        const string initializeMessage = """
            {
                "jsonrpc": "2.0",
                "id": 1,
                "method": "initialize",
                "params": {
                    "processId": 32,
                    "clientInfo": { "name": "testingplatform-unittests", "version": "1.0.0" },
                    "capabilities": {
                        "testing": {
                            "debuggerProvider": true
                        }
                    }
                }
            }
            """;
        await WriteMessageAsync(
            writer,
            initializeMessage);

        // Wait for initialize response
        RpcMessage? msg = null;
        using CancellationTokenSource cancellationTokenSource = new(TimeSpan.FromSeconds(30));
        CancellationToken cancellationToken = cancellationTokenSource.Token;
        try
        {
            msg = await WaitForMessage(messageHandler, (RpcMessage? rpcMessage) => rpcMessage is ResponseMessage, "Wait initialize", cancellationToken);
        }
        catch (OperationCanceledException ex) when (ex.CancellationToken == cancellationToken)
        {
            // Try to observe if we had some exceptions
            await serverTask.TimeoutAfterAsync(TimeSpan.FromSeconds(30), cancellationToken);
        }

        Assert.IsNotNull(msg);

        InitializeResponseArgs resultJson = SerializerUtilities.Deserialize<InitializeResponseArgs>((IDictionary<string, object?>)((ResponseMessage)msg).Result!);

        InitializeResponseArgs expectedResponse = new(
                   1,
                   new ServerInfo("test-anywhere", "this is dynamic"),
                   new ServerCapabilities(new ServerTestingCapabilities(SupportsDiscovery: true, MultiRequestSupport: false, VSTestProviderSupport: false)));

        Assert.AreEqual(expectedResponse.Capabilities, resultJson.Capabilities);
        Assert.AreEqual(expectedResponse.ServerInfo.Name, resultJson.ServerInfo.Name);

        await WriteMessageAsync(writer, """{ "jsonrpc": "2.0", "method": "exit", "params": { } }""");

        int result = await serverTask;
        Assert.AreEqual(0, result);
    }

    public async Task DiscoveryRequestCanBeCanceled()
    {
        using var server = TcpServer.Create();

        TaskCompletionSource<bool> discoveryStartedTaskCompletionSource = new();
        TaskCompletionSource<bool> discoveryCanceledTaskCompletionSource = new();

        string[] args = ["--no-banner", $"--server", "--client-port", $"{server.Port}", "--internal-testingplatform-skipbuildercheck"];
        ITestApplicationBuilder builder = await TestApplication.CreateBuilderAsync(args);
        builder.RegisterTestFramework(_ => new TestFrameworkCapabilities(), (_, __) => new MockTestAdapter
        {
            DiscoveryAction = async (ExecuteRequestContext context) =>
            {
                using (context.CancellationToken.Register(() => discoveryCanceledTaskCompletionSource.SetResult(true)))
                {
                    discoveryStartedTaskCompletionSource.TrySetResult(true);
                    await discoveryCanceledTaskCompletionSource.Task;
                }
            },
        });
        var testApplication = (TestApplication)await builder.BuildAsync();
        testApplication.ServiceProvider.GetRequiredService<SystemConsole>().SuppressOutput();
        var serverTask = Task.Run(testApplication.RunAsync);

        using CancellationTokenSource timeout = new(TimeoutHelper.DefaultHangTimeSpanTimeout);
        using TcpClient client = await server.WaitForConnectionAsync(timeout.Token);
        using NetworkStream stream = client.GetStream();
        using StreamWriter writer = new(stream, Encoding.UTF8);
        TcpMessageHandler messageHandler = new(
                client,
                clientToServerStream: client.GetStream(),
                serverToClientStream: client.GetStream(),
                FormatterUtilities.CreateFormatter());

        const string initializeMessage = """
            {
                "jsonrpc": "2.0",
                "id": 1,
                "method": "initialize",
                "params": {
                    "processId": 32,
                    "clientInfo": { "name": "testingplatform-unittests", "version": "1.0.0" },
                    "capabilities": {
                        "testing": {
                            "debuggerProvider": true
                        }
                    }
                }
            }
            """;
        await WriteMessageAsync(writer, initializeMessage);

        // Wait for initialize response
        using CancellationTokenSource cancellationTokenSource = new(TimeoutHelper.DefaultHangTimeSpanTimeout);
        await WaitForMessage(messageHandler, (RpcMessage? rpcMessage) => rpcMessage is ResponseMessage, "Wait initialize", cancellationTokenSource.Token);

        RpcMessage? msg;

        const string discoverTestsMessage = """
            {
                "jsonrpc": "2.0",
                "id": 2,
                "method": "testing/discoverTests",
                "params": {
                    "runId": "Run1"
                }
            }
            """;
        await WriteMessageAsync(writer, discoverTestsMessage);

        // Note: Wait for the adapter to start the discovery.
        await discoveryStartedTaskCompletionSource.Task;

        const string cancelRequestMessage = """
            {
                "jsonrpc": "2.0",
                "method": "$/cancelRequest",
                "params": { "id": 2 }
            }
            """;
        await WriteMessageAsync(writer, cancelRequestMessage);

        using CancellationTokenSource cancellationTokenSource2 = new(TimeoutHelper.DefaultHangTimeSpanTimeout);
        msg = await WaitForMessage(messageHandler, (RpcMessage? rpcMessage) => rpcMessage is ErrorMessage, "Wait cancelRequest", cancellationTokenSource.Token);

        var error = (ErrorMessage)msg!;
        Assert.AreEqual(ErrorCodes.RequestCanceled, error.ErrorCode);

        await WriteMessageAsync(writer, """{ "jsonrpc": "2.0", "method": "exit", "params": { } }""");

        int result = await serverTask;
        Assert.AreEqual(0, result);
    }

    private static async Task<RpcMessage?> WaitForMessage(TcpMessageHandler messageHandler, Func<RpcMessage?, bool> rpcMessageFilter, string label, CancellationToken cancellationToken)
    {
        while (true)
        {
            RpcMessage? rpcMessage;
            try
            {
                rpcMessage = await messageHandler.ReadAsync(cancellationToken);
            }
            catch (OperationCanceledException ex)
            {
                throw new OperationCanceledException($"Label: {label}", ex);
            }

            if (rpcMessageFilter(rpcMessage))
            {
                return rpcMessage;
            }
        }
    }

    private static async Task WriteMessageAsync(StreamWriter writer, string message)
    {
        await writer.WriteLineAsync($"Content-Length: {message.Length}");
        await writer.WriteLineAsync($"Content-Type: application/testingplatform");
        await writer.WriteLineAsync();
        await writer.WriteAsync(message);
        await writer.FlushAsync();
    }

    private sealed class TestApplicationHooks : ITestApplicationLifecycleCallbacks, IDisposable
    {
        private readonly SemaphoreSlim _waitForBeforeRunAsync = new(0, 1);

        public string Uid => nameof(TestApplicationHooks);

        public string Version => "1.0.0";

        public string DisplayName => string.Empty;

        public string Description => string.Empty;

        public Task<bool> IsEnabledAsync() => Task.FromResult(true);

        public Task WaitForBeforeRunAsync() => _waitForBeforeRunAsync.WaitAsync();

        public Task AfterRunAsync(int returnValue, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task BeforeRunAsync(CancellationToken cancellationToken)
        {
            _waitForBeforeRunAsync.Release();
            return Task.CompletedTask;
        }

        public void Dispose() => _waitForBeforeRunAsync.Dispose();
    }

    private sealed class MockTestAdapter : ITestFramework
    {
        public Func<ExecuteRequestContext, Task>? DiscoveryAction { get; set; }

        public ICapability[] Capabilities => [];

        public string Uid => nameof(MockTestAdapter);

        public string Version => "1.0.0";

        public string DisplayName => nameof(MockTestAdapter);

        public string Description => string.Empty;

        public Task<bool> IsEnabledAsync() => Task.FromResult(true);

        public Task<CreateTestSessionResult> CreateTestSessionAsync(CreateTestSessionContext context) => Task.FromResult(new CreateTestSessionResult { IsSuccess = true });

        public Task<CloseTestSessionResult> CloseTestSessionAsync(CloseTestSessionContext context) => Task.FromResult(new CloseTestSessionResult { IsSuccess = true });

        public Task ExecuteRequestAsync(ExecuteRequestContext context) => DiscoveryAction is not null ? DiscoveryAction(context) : Task.CompletedTask;
    }

    private sealed class TcpServer : IDisposable
    {
        public TcpServer(TcpListener listener) => Listener = listener;

        private TcpListener Listener { get; }

        public int Port => EndPoint.Port;

        private IPEndPoint EndPoint => (IPEndPoint)Listener.LocalEndpoint;

        public async Task<TcpClient> WaitForConnectionAsync(CancellationToken cancellationToken)
        {
#if NETCOREAPP
#pragma warning disable IDE0022 // Use expression body for method | False positive because of the #if
            return await Listener.AcceptTcpClientAsync(cancellationToken);
#pragma warning restore IDE0022 // Use expression body for method
#else
            using (cancellationToken.Register(Listener.Stop))
            {
                return await Listener.AcceptTcpClientAsync();
            }
#endif
        }

        internal static TcpServer Create()
        {
            IPEndPoint endPoint = new(IPAddress.Loopback, port: 0);
            TcpListener listener = new(endPoint);
            listener.Start();

            return new(listener);
        }

        public void Dispose() => Listener.Stop();
    }
}
