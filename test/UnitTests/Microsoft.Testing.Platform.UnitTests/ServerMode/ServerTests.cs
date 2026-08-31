// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Net;
using System.Net.Sockets;

using Microsoft.Testing.Platform.Capabilities;
using Microsoft.Testing.Platform.Capabilities.TestFramework;
using Microsoft.Testing.Platform.Extensions.TestFramework;
using Microsoft.Testing.Platform.Extensions.TestHost;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Requests;
using Microsoft.Testing.Platform.ServerMode;
using Microsoft.Testing.Platform.Services;

namespace Microsoft.Testing.Platform.UnitTests;

[TestClass]
[UnsupportedOSPlatform("browser")]
public sealed class ServerTests
{
    public ServerTests()
    {
        if (IsHotReloadEnabled(new SystemEnvironment()))
        {
            throw new NotSupportedException("Tests of this class cannot work correctly under hot reload.");
        }
    }

    private static bool IsHotReloadEnabled(SystemEnvironment environment)
        => environment.GetEnvironmentVariable(EnvironmentVariableConstants.DOTNET_WATCH) == "1"
        || environment.GetEnvironmentVariable(EnvironmentVariableConstants.TESTINGPLATFORM_HOTRELOAD_ENABLED) == "1";

    [TestMethod]
    public async Task ServerCanBeStartedAndAborted_TcpIp()
    {
        using var server = TcpServer.Create();

        TestApplicationHooks testApplicationHooks = new();
        string[] args = ["--no-banner", "--server", "--client-host", "localhost", "--client-port", $"{server.Port}", "--internal-testingplatform-skipbuildercheck"];
        ITestApplicationBuilder builder = await TestApplication.CreateBuilderAsync(args);
        builder.TestHost.AddTestHostApplicationLifetime(_ => testApplicationHooks);
        builder.RegisterTestFramework(_ => new TestFrameworkCapabilities(), (_, __) => new MockTestAdapter());
        var testApplication = (TestApplication)await builder.BuildAsync();
        testApplication.ServiceProvider.GetRequiredService<SystemConsole>().SuppressOutput();
        Task<int> serverTask = testApplication.RunAsync();

        await testApplicationHooks.WaitForBeforeRunAsync();
        ITestApplicationCancellationTokenSource stopService = testApplication.ServiceProvider.GetTestApplicationCancellationTokenSource();

        stopService.Cancel();
        Assert.AreEqual((int)ExitCode.TestSessionAborted, await serverTask);
    }

    [TestMethod]
    public async Task ServerCanInitialize()
    {
        using var server = TcpServer.Create();

        string[] args = ["--no-banner", "--server", "--client-port", $"{server.Port}", "--internal-testingplatform-skipbuildercheck"];
        TestApplicationHooks testApplicationHooks = new();
        ITestApplicationBuilder builder = await TestApplication.CreateBuilderAsync(args);
        builder.TestHost.AddTestHostApplicationLifetime(_ => testApplicationHooks);
        builder.RegisterTestFramework(_ => new TestFrameworkCapabilities(), (_, __) => new MockTestAdapter());
        var testApplication = (TestApplication)await builder.BuildAsync();
        testApplication.ServiceProvider.GetRequiredService<SystemConsole>().SuppressOutput();
        Task<int> serverTask = Task.Run(testApplication.RunAsync);

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
            msg = await WaitForMessage(messageHandler, rpcMessage => rpcMessage is ResponseMessage, "Wait initialize", cancellationToken);
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
                   new ServerCapabilities(new ServerTestingCapabilities(SupportsDiscovery: true, MultiRequestSupport: false, VSTestProviderSupport: false, SupportsAttachments: true, MultiConnectionProvider: false)))
        {
            ProtocolVersion = JsonRpcProtocolVersions.Current,
        };

        Assert.AreEqual(expectedResponse.Capabilities, resultJson.Capabilities);
        Assert.AreEqual(expectedResponse.ServerInfo.Name, resultJson.ServerInfo.Name);
        Assert.AreEqual(JsonRpcProtocolVersions.Current, resultJson.ProtocolVersion);
        Assert.IsNotEmpty(resultJson.ServerInfo.Version);

        await WriteMessageAsync(writer, """{ "jsonrpc": "2.0", "method": "exit", "params": { } }""");

        int result = await serverTask;
        Assert.AreEqual(0, result);
    }

    [TestMethod]
    public async Task ServerEnforcesLifecycleAndNegotiatesProtocolVersion()
    {
        using var server = TcpServer.Create();

        string[] args = ["--no-banner", "--server", "--client-port", $"{server.Port}", "--internal-testingplatform-skipbuildercheck"];
        ITestApplicationBuilder builder = await TestApplication.CreateBuilderAsync(args);
        builder.RegisterTestFramework(_ => new TestFrameworkCapabilities(), (_, __) => new MockTestAdapter
        {
            DiscoveryAction = context =>
            {
                context.Complete();
                return Task.CompletedTask;
            },
        });
        var testApplication = (TestApplication)await builder.BuildAsync();
        testApplication.ServiceProvider.GetRequiredService<SystemConsole>().SuppressOutput();
        Task<int> serverTask = Task.Run(testApplication.RunAsync);

        using CancellationTokenSource timeout = new(TimeoutHelper.DefaultHangTimeSpanTimeout);
        using TcpClient client = await server.WaitForConnectionAsync(timeout.Token);
        using NetworkStream stream = client.GetStream();
        using StreamWriter writer = new(stream, Encoding.UTF8);
        TcpMessageHandler messageHandler = new(
            client,
            clientToServerStream: client.GetStream(),
            serverToClientStream: client.GetStream(),
            FormatterUtilities.CreateFormatter());

        await WriteMessageAsync(
            writer,
            """
            {
                "jsonrpc": "2.0",
                "id": 1,
                "method": "testing/discoverTests",
                "params": {
                    "runId": "00000000-0000-0000-0000-000000000001"
                }
            }
            """);

        var beforeInitializeError = (ErrorMessage)(await WaitForMessage(
            messageHandler,
            rpcMessage => rpcMessage is ErrorMessage { Id: 1 },
            "Wait server-not-initialized error",
            timeout.Token))!;
        Assert.AreEqual(ErrorCodes.ServerNotInitialized, beforeInitializeError.ErrorCode);

        await WriteMessageAsync(
            writer,
            """
            {
                "jsonrpc": "2.0",
                "id": 2,
                "method": "initialize",
                "params": {
                    "processId": 32,
                    "clientInfo": { "name": "testingplatform-unittests", "version": "42.0.0" },
                    "capabilities": {
                        "testing": {
                            "debuggerProvider": false
                        }
                    },
                    "protocolVersions": [ "99.0.0" ]
                }
            }
            """);
        await WriteMessageAsync(
            writer,
            """
            {
                "jsonrpc": "2.0",
                "id": 20,
                "method": "testing/discoverTests",
                "params": {
                    "runId": "00000000-0000-0000-0000-000000000020"
                }
            }
            """);

        var incompatibleVersionError = (ErrorMessage)(await WaitForMessage(
            messageHandler,
            rpcMessage => rpcMessage is ErrorMessage { Id: 2 },
            "Wait incompatible protocol version error",
            timeout.Token))!;
        Assert.AreEqual(ErrorCodes.ProtocolVersionNotSupported, incompatibleVersionError.ErrorCode);

        const string initializeMessage = """
            {
                "jsonrpc": "2.0",
                "id": 2,
                "method": "initialize",
                "params": {
                    "processId": 32,
                    "clientInfo": { "name": "testingplatform-unittests", "version": "42.0.0" },
                    "capabilities": {
                        "testing": {
                            "debuggerProvider": false
                        }
                    },
                    "protocolVersions": [ "1.0.0" ]
                }
            }
            """;
        await WriteMessageAsync(writer, initializeMessage);

        ErrorMessage? queuedRequestError = null;
        ResponseMessage? initializeResponse = null;
        while (queuedRequestError is null || initializeResponse is null)
        {
            RpcMessage? message = await messageHandler.ReadAsync(timeout.Token);
            if (queuedRequestError is null && message is ErrorMessage { Id: 20 } error)
            {
                queuedRequestError = error;
            }

            if (initializeResponse is null && message is ResponseMessage { Id: 2 } response)
            {
                initializeResponse = response;
            }
        }

        Assert.AreEqual(ErrorCodes.ServerNotInitialized, queuedRequestError.ErrorCode);
        InitializeResponseArgs initializeResult = SerializerUtilities.Deserialize<InitializeResponseArgs>(
            (IDictionary<string, object?>)initializeResponse.Result!);
        Assert.AreEqual(JsonRpcProtocolVersions.Current, initializeResult.ProtocolVersion);
        Assert.IsNotEmpty(initializeResult.ServerInfo.Version);
        var responseResult = (IDictionary<string, object?>)initializeResponse.Result!;
        var responseCapabilities = (IDictionary<string, object?>)responseResult[JsonRpcStrings.Capabilities]!;
        var testingCapabilities = (IDictionary<string, object?>)responseCapabilities[JsonRpcStrings.Testing]!;
        Assert.IsFalse((bool)testingCapabilities[JsonRpcStrings.SupportsTestCoverageMessages]!);

        await WriteMessageAsync(
            writer,
            """
            {
                "jsonrpc": "2.0",
                "id": "4",
                "method": "testing/unknown",
                "params": {}
            }
            """);

        var methodNotFoundError = (ErrorMessage)(await WaitForMessage(
            messageHandler,
            rpcMessage => rpcMessage is ErrorMessage { Id: 4 },
            "Wait method-not-found error",
            timeout.Token))!;
        Assert.AreEqual(ErrorCodes.MethodNotFound, methodNotFoundError.ErrorCode);
        Assert.AreEqual("4", methodNotFoundError.StringId);

        await WriteMessageAsync(
            writer,
            """
            {
                "jsonrpc": "2.0",
                "id": 5,
                "method": "testing/runTests",
                "params": {
                    "runId": "00000000-0000-0000-0000-000000000005",
                    "filter": "/A(&B)"
                }
            }
            """);

        RpcMessage? failedRunCompletion = await WaitForMessage(
            messageHandler,
            IsTestUpdateCompletion,
            "Wait failed run completion",
            timeout.Token);
        Assert.IsNotNull(failedRunCompletion);

        var internalError = (ErrorMessage)(await WaitForMessage(
            messageHandler,
            rpcMessage => rpcMessage is ErrorMessage { Id: 5 },
            "Wait failed run error",
            timeout.Token))!;
        Assert.AreEqual(ErrorCodes.InternalError, internalError.ErrorCode);

        await WriteMessageAsync(writer, initializeMessage.Replace("\"id\": 2", "\"id\": 6"));
        var duplicateInitializeError = (ErrorMessage)(await WaitForMessage(
            messageHandler,
            rpcMessage => rpcMessage is ErrorMessage { Id: 6 },
            "Wait duplicate initialize error",
            timeout.Token))!;
        Assert.AreEqual(ErrorCodes.InvalidRequest, duplicateInitializeError.ErrorCode);

        await WriteMessageAsync(writer, """{ "jsonrpc": "2.0", "method": "exit", "params": { } }""");

        Assert.AreEqual(0, await serverTask);
    }

    [TestMethod]
    public async Task PipelinedRequestWaitsForInitializeResponse()
    {
        using var server = TcpServer.Create();

        string[] args = ["--no-banner", "--server", "--client-port", $"{server.Port}", "--internal-testingplatform-skipbuildercheck"];
        ITestApplicationBuilder builder = await TestApplication.CreateBuilderAsync(args);
        builder.RegisterTestFramework(_ => new TestFrameworkCapabilities(), (_, __) => new MockTestAdapter
        {
            DiscoveryAction = context =>
            {
                context.Complete();
                return Task.CompletedTask;
            },
        });
        var testApplication = (TestApplication)await builder.BuildAsync();
        testApplication.ServiceProvider.GetRequiredService<SystemConsole>().SuppressOutput();
        Task<int> serverTask = Task.Run(testApplication.RunAsync);

        using CancellationTokenSource timeout = new(TimeoutHelper.DefaultHangTimeSpanTimeout);
        using TcpClient client = await server.WaitForConnectionAsync(timeout.Token);
        using NetworkStream stream = client.GetStream();
        using StreamWriter writer = new(stream, Encoding.UTF8);
        TcpMessageHandler messageHandler = new(
            client,
            clientToServerStream: client.GetStream(),
            serverToClientStream: client.GetStream(),
            FormatterUtilities.CreateFormatter());

        await WriteMessageAsync(
            writer,
            """
            {
                "jsonrpc": "2.0",
                "id": 1,
                "method": "initialize",
                "params": {
                    "processId": 32,
                    "clientInfo": { "name": "testingplatform-unittests", "version": "1.0.0" },
                    "capabilities": {
                        "testing": {
                            "debuggerProvider": false
                        }
                    }
                }
            }
            """);
        await WriteMessageAsync(
            writer,
            """
            {
                "jsonrpc": "2.0",
                "id": 2,
                "method": "testing/discoverTests",
                "params": {
                    "runId": "00000000-0000-0000-0000-000000000002"
                }
            }
            """);

        _ = await WaitForMessage(
            messageHandler,
            rpcMessage => rpcMessage is ResponseMessage { Id: 1 },
            "Wait initialize response",
            timeout.Token);
        RpcMessage? discoveryResponse = await WaitForMessage(
            messageHandler,
            rpcMessage => rpcMessage is ResponseMessage { Id: 2 } or ErrorMessage { Id: 2 },
            "Wait pipelined discovery response",
            timeout.Token);
        Assert.IsInstanceOfType<ResponseMessage>(discoveryResponse);

        await WriteMessageAsync(writer, """{ "jsonrpc": "2.0", "method": "exit", "params": { } }""");
        Assert.AreEqual(0, await serverTask);
    }

    [TestMethod]
    public async Task PipelinedRequestCanBeCanceledWhileInitializationCompletes()
    {
        using var server = TcpServer.Create();
        using var testFrameworkCapabilities = new BlockingTestFrameworkCapabilities();
        int discoveryInvocationCount = 0;

        string[] args = ["--no-banner", "--server", "--client-port", $"{server.Port}", "--internal-testingplatform-skipbuildercheck"];
        ITestApplicationBuilder builder = await TestApplication.CreateBuilderAsync(args);
        builder.RegisterTestFramework(_ => testFrameworkCapabilities, (_, __) => new MockTestAdapter
        {
            DiscoveryAction = context =>
            {
                Interlocked.Increment(ref discoveryInvocationCount);
                return Task.Delay(Timeout.Infinite, context.CancellationToken);
            },
        });
        var testApplication = (TestApplication)await builder.BuildAsync();
        testApplication.ServiceProvider.GetRequiredService<SystemConsole>().SuppressOutput();
        Task<int> serverTask = Task.Run(testApplication.RunAsync);

        using CancellationTokenSource timeout = new(TimeoutHelper.DefaultHangTimeSpanTimeout);
        using TcpClient client = await server.WaitForConnectionAsync(timeout.Token);
        using NetworkStream stream = client.GetStream();
        using StreamWriter writer = new(stream, Encoding.UTF8);
        TcpMessageHandler messageHandler = new(
            client,
            clientToServerStream: client.GetStream(),
            serverToClientStream: client.GetStream(),
            FormatterUtilities.CreateFormatter());

        testFrameworkCapabilities.BlockNextAccess();
        try
        {
            await WriteMessageAsync(
                writer,
                """
                {
                    "jsonrpc": "2.0",
                    "id": 1,
                    "method": "initialize",
                    "params": {
                        "processId": 32,
                        "clientInfo": { "name": "testingplatform-unittests", "version": "1.0.0" },
                        "capabilities": {
                            "testing": {
                                "debuggerProvider": false
                            }
                        }
                    }
                }
                """);
            await testFrameworkCapabilities.WaitUntilBlockedAsync().WaitAsync(timeout.Token);
            await WriteMessageAsync(
                writer,
                """
                {
                    "jsonrpc": "2.0",
                    "id": 2,
                    "method": "testing/discoverTests",
                    "params": {
                        "runId": "00000000-0000-0000-0000-000000000002"
                    }
                }
                """);
            await WriteMessageAsync(
                writer,
                """
                {
                    "jsonrpc": "2.0",
                    "method": "$/cancelRequest",
                    "params": {
                        "id": 2
                    }
                }
                """);
            await WriteMessageAsync(
                writer,
                """
                {
                    "jsonrpc": "2.0",
                    "id": 3,
                    "method": "initialize",
                    "params": {
                        "processId": 32,
                        "clientInfo": { "name": "testingplatform-unittests", "version": "1.0.0" },
                        "capabilities": {
                            "testing": {
                                "debuggerProvider": false
                            }
                        }
                    }
                }
                """);

            var duplicateInitializeError = (ErrorMessage)(await WaitForMessage(
                messageHandler,
                rpcMessage => rpcMessage is ErrorMessage { Id: 3 },
                "Wait duplicate initialize error after cancellation",
                timeout.Token))!;
            Assert.AreEqual(ErrorCodes.InvalidRequest, duplicateInitializeError.ErrorCode);
        }
        finally
        {
            testFrameworkCapabilities.Release();
        }

        _ = await WaitForMessage(
            messageHandler,
            rpcMessage => rpcMessage is ResponseMessage { Id: 1 },
            "Wait initialize response",
            timeout.Token);
        _ = await WaitForMessage(
            messageHandler,
            IsTestUpdateCompletion,
            "Wait canceled discovery completion",
            timeout.Token);
        var cancellationError = (ErrorMessage)(await WaitForMessage(
            messageHandler,
            rpcMessage => rpcMessage is ErrorMessage { Id: 2 },
            "Wait canceled discovery error",
            timeout.Token))!;
        Assert.AreEqual(ErrorCodes.RequestCanceled, cancellationError.ErrorCode);
        Assert.AreEqual(0, Volatile.Read(ref discoveryInvocationCount));

        await WriteMessageAsync(writer, """{ "jsonrpc": "2.0", "method": "exit", "params": { } }""");
        Assert.AreEqual(0, await serverTask);
    }

    [TestMethod]
    public async Task SlowCancellationCallbackDoesNotBlockMessageReader()
    {
        using var server = TcpServer.Create();
        TaskCompletionSource<bool> discoveryStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource<bool> cancellationCallbackStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        using var releaseCancellationCallback = new ManualResetEventSlim(initialState: false);
        using CancellationTokenSource cancellationCallbackTimeout = new(TimeoutHelper.DefaultHangTimeSpanTimeout);

        string[] args = ["--no-banner", "--server", "--client-port", $"{server.Port}", "--internal-testingplatform-skipbuildercheck"];
        ITestApplicationBuilder builder = await TestApplication.CreateBuilderAsync(args);
        builder.RegisterTestFramework(_ => new TestFrameworkCapabilities(), (_, __) => new MockTestAdapter
        {
            DiscoveryAction = async context =>
            {
                var cancellationDelay = Task.Delay(Timeout.Infinite, context.CancellationToken);
                using CancellationTokenRegistration registration = context.CancellationToken.Register(() =>
                {
                    cancellationCallbackStarted.TrySetResult(true);
                    releaseCancellationCallback.Wait(cancellationCallbackTimeout.Token);
                });
                discoveryStarted.TrySetResult(true);
                await cancellationDelay;
            },
        });
        var testApplication = (TestApplication)await builder.BuildAsync();
        testApplication.ServiceProvider.GetRequiredService<SystemConsole>().SuppressOutput();
        Task<int> serverTask = Task.Run(testApplication.RunAsync);

        using CancellationTokenSource timeout = new(TimeoutHelper.DefaultHangTimeSpanTimeout);
        using TcpClient client = await server.WaitForConnectionAsync(timeout.Token);
        using NetworkStream stream = client.GetStream();
        using StreamWriter writer = new(stream, Encoding.UTF8);
        TcpMessageHandler messageHandler = new(
            client,
            clientToServerStream: client.GetStream(),
            serverToClientStream: client.GetStream(),
            FormatterUtilities.CreateFormatter());

        await WriteMessageAsync(
            writer,
            """
            {
                "jsonrpc": "2.0",
                "id": 1,
                "method": "initialize",
                "params": {
                    "processId": 32,
                    "clientInfo": { "name": "testingplatform-unittests", "version": "1.0.0" },
                    "capabilities": {
                        "testing": {
                            "debuggerProvider": false
                        }
                    }
                }
            }
            """);
        _ = await WaitForMessage(
            messageHandler,
            rpcMessage => rpcMessage is ResponseMessage { Id: 1 },
            "Wait initialize response",
            timeout.Token);

        try
        {
            await WriteMessageAsync(
                writer,
                """
                {
                    "jsonrpc": "2.0",
                    "id": 2,
                    "method": "testing/discoverTests",
                    "params": {
                        "runId": "00000000-0000-0000-0000-000000000002"
                    }
                }
                """);
            await discoveryStarted.Task.WaitAsync(timeout.Token);

            await WriteMessageAsync(
                writer,
                """
                {
                    "jsonrpc": "2.0",
                    "method": "$/cancelRequest",
                    "params": {
                        "id": 2
                    }
                }
                """);
            await cancellationCallbackStarted.Task.WaitAsync(timeout.Token);

            await WriteMessageAsync(
                writer,
                """
                {
                    "jsonrpc": "2.0",
                    "id": 3,
                    "method": "testing/unknown",
                    "params": {}
                }
                """);
            var methodNotFoundError = (ErrorMessage)(await WaitForMessage(
                messageHandler,
                rpcMessage => rpcMessage is ErrorMessage { Id: 3 },
                "Wait method-not-found response while cancellation callback is blocked",
                timeout.Token))!;
            Assert.AreEqual(ErrorCodes.MethodNotFound, methodNotFoundError.ErrorCode);
        }
        finally
        {
            releaseCancellationCallback.Set();
        }

        _ = await WaitForMessage(
            messageHandler,
            IsTestUpdateCompletion,
            "Wait canceled discovery completion",
            timeout.Token);
        var cancellationError = (ErrorMessage)(await WaitForMessage(
            messageHandler,
            rpcMessage => rpcMessage is ErrorMessage { Id: 2 },
            "Wait canceled discovery error",
            timeout.Token))!;
        Assert.AreEqual(ErrorCodes.RequestCanceled, cancellationError.ErrorCode);

        await WriteMessageAsync(writer, """{ "jsonrpc": "2.0", "method": "exit", "params": { } }""");
        Assert.AreEqual(0, await serverTask);
    }

    [TestMethod]
    public async Task NumericAndStringRequestIdsHaveIndependentCancellation()
    {
        using var server = TcpServer.Create();
        TaskCompletionSource<bool> bothRequestsStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource<bool> releaseNumericRequest = new(TaskCreationOptions.RunContinuationsAsynchronously);
        int startedRequestCount = 0;

        string[] args = ["--no-banner", "--server", "--client-port", $"{server.Port}", "--internal-testingplatform-skipbuildercheck"];
        ITestApplicationBuilder builder = await TestApplication.CreateBuilderAsync(args);
        builder.RegisterTestFramework(_ => new TestFrameworkCapabilities(), (_, __) => new MockTestAdapter
        {
            DiscoveryAction = async context =>
            {
                TreeNodeFilter filter = Assert.IsInstanceOfType<TreeNodeFilter>(
                    Assert.IsInstanceOfType<TestExecutionRequest>(context.Request).Filter);
                if (Interlocked.Increment(ref startedRequestCount) == 2)
                {
                    bothRequestsStarted.TrySetResult(true);
                }

                if (filter.Filter == "/string")
                {
                    await Task.Delay(Timeout.Infinite, context.CancellationToken);
                }
                else
                {
                    await releaseNumericRequest.Task;
                    context.Complete();
                }
            },
        });
        var testApplication = (TestApplication)await builder.BuildAsync();
        testApplication.ServiceProvider.GetRequiredService<SystemConsole>().SuppressOutput();
        Task<int> serverTask = Task.Run(testApplication.RunAsync);

        using CancellationTokenSource timeout = new(TimeoutHelper.DefaultHangTimeSpanTimeout);
        using TcpClient client = await server.WaitForConnectionAsync(timeout.Token);
        using NetworkStream stream = client.GetStream();
        using StreamWriter writer = new(stream, Encoding.UTF8);
        TcpMessageHandler messageHandler = new(
            client,
            clientToServerStream: client.GetStream(),
            serverToClientStream: client.GetStream(),
            FormatterUtilities.CreateFormatter());

        await WriteMessageAsync(
            writer,
            """
            {
                "jsonrpc": "2.0",
                "id": 1,
                "method": "initialize",
                "params": {
                    "processId": 32,
                    "clientInfo": { "name": "testingplatform-unittests", "version": "1.0.0" },
                    "capabilities": {
                        "testing": {
                            "debuggerProvider": false
                        }
                    }
                }
            }
            """);
        _ = await WaitForMessage(
            messageHandler,
            rpcMessage => rpcMessage is ResponseMessage { Id: 1 },
            "Wait initialize response",
            timeout.Token);

        await WriteMessageAsync(
            writer,
            """
            {
                "jsonrpc": "2.0",
                "id": 2,
                "method": "testing/discoverTests",
                "params": {
                    "runId": "00000000-0000-0000-0000-000000000002",
                    "filter": "/numeric"
                }
            }
            """);
        await WriteMessageAsync(
            writer,
            """
            {
                "jsonrpc": "2.0",
                "id": "2",
                "method": "testing/discoverTests",
                "params": {
                    "runId": "00000000-0000-0000-0000-000000000003",
                    "filter": "/string"
                }
            }
            """);
        await bothRequestsStarted.Task.TimeoutAfterAsync(TimeoutHelper.DefaultHangTimeSpanTimeout, timeout.Token);
        await WriteMessageAsync(
            writer,
            """
            {
                "jsonrpc": "2.0",
                "method": "$/cancelRequest",
                "params": {
                    "id": "2"
                }
            }
            """);

        var stringRequestError = (ErrorMessage)(await WaitForMessage(
            messageHandler,
            rpcMessage => rpcMessage is ErrorMessage { Id: 2, StringId: "2" },
            "Wait string request cancellation",
            timeout.Token))!;
        Assert.AreEqual(ErrorCodes.RequestCanceled, stringRequestError.ErrorCode);

        releaseNumericRequest.TrySetResult(true);
        var numericResponse = (ResponseMessage)(await WaitForMessage(
            messageHandler,
            rpcMessage => rpcMessage is ResponseMessage { Id: 2, StringId: null },
            "Wait numeric request response",
            timeout.Token))!;
        Assert.IsNull(numericResponse.StringId);

        await WriteMessageAsync(writer, """{ "jsonrpc": "2.0", "method": "exit", "params": { } }""");
        Assert.AreEqual(0, await serverTask);
    }

    [TestMethod]
    public async Task RunRequestWithEmptyTests_PreservesEmptyUidSelection()
    {
        using var server = TcpServer.Create();
        TaskCompletionSource<TestExecutionRequest> requestCaptured = new(TaskCreationOptions.RunContinuationsAsynchronously);

        string[] args = ["--no-banner", "--server", "--client-port", $"{server.Port}", "--internal-testingplatform-skipbuildercheck"];
        ITestApplicationBuilder builder = await TestApplication.CreateBuilderAsync(args);
        builder.RegisterTestFramework(_ => new TestFrameworkCapabilities(), (_, __) => new MockTestAdapter
        {
            DiscoveryAction = context =>
            {
                context.Complete();
                requestCaptured.TrySetResult((TestExecutionRequest)context.Request);
                return Task.CompletedTask;
            },
        });
        var testApplication = (TestApplication)await builder.BuildAsync();
        testApplication.ServiceProvider.GetRequiredService<SystemConsole>().SuppressOutput();
        Task<int> serverTask = Task.Run(testApplication.RunAsync);

        using CancellationTokenSource timeout = new(TimeoutHelper.DefaultHangTimeSpanTimeout);
        using TcpClient client = await server.WaitForConnectionAsync(timeout.Token);
        using NetworkStream stream = client.GetStream();
        using StreamWriter writer = new(stream, Encoding.UTF8);
        TcpMessageHandler messageHandler = new(
            client,
            clientToServerStream: client.GetStream(),
            serverToClientStream: client.GetStream(),
            FormatterUtilities.CreateFormatter());

        const string InitializeMessage = """
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
        await WriteMessageAsync(writer, InitializeMessage);
        _ = await WaitForMessage(
            messageHandler,
            rpcMessage => rpcMessage is ResponseMessage response && response.Id == 1,
            "Wait initialize",
            timeout.Token);

        const string RunTestsMessage = """
            {
                "jsonrpc": "2.0",
                "id": 2,
                "method": "testing/runTests",
                "params": {
                    "runId": "00000000-0000-0000-0000-000000000001",
                    "tests": []
                }
            }
            """;
        await WriteMessageAsync(writer, RunTestsMessage);
        await requestCaptured.Task.TimeoutAfterAsync(TimeoutHelper.DefaultHangTimeSpanTimeout);
        RunTestExecutionRequest runRequest = Assert.IsInstanceOfType<RunTestExecutionRequest>(await requestCaptured.Task);
        TestNodeUidListFilter uidFilter = Assert.IsInstanceOfType<TestNodeUidListFilter>(runRequest.Filter);
        Assert.IsEmpty(uidFilter.TestNodeUids);

        await WriteMessageAsync(writer, """{ "jsonrpc": "2.0", "method": "exit", "params": { } }""");

        Assert.AreEqual(0, await serverTask);
    }

    [TestMethod]
    public async Task DiscoveryRequestCanBeCanceled()
    {
        using var server = TcpServer.Create();

        TaskCompletionSource<bool> discoveryStartedTaskCompletionSource = new();
        TaskCompletionSource<bool> discoveryCanceledTaskCompletionSource = new();

        string[] args = ["--no-banner", "--server", "--client-port", $"{server.Port}", "--internal-testingplatform-skipbuildercheck"];
        ITestApplicationBuilder builder = await TestApplication.CreateBuilderAsync(args);
        builder.RegisterTestFramework(_ => new TestFrameworkCapabilities(), (_, __) => new MockTestAdapter
        {
            DiscoveryAction = async context =>
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
        Task<int> serverTask = Task.Run(testApplication.RunAsync);

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
        await WaitForMessage(messageHandler, rpcMessage => rpcMessage is ResponseMessage, "Wait initialize", cancellationTokenSource.Token);

        RpcMessage? msg;

        const string discoverTestsMessage = """
            {
                "jsonrpc": "2.0",
                "id": 2,
                "method": "testing/discoverTests",
                "params": {
                    "runId": "00000000-0000-0000-0000-000000000001"
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
        RpcMessage? completion = await WaitForMessage(
            messageHandler,
            IsTestUpdateCompletion,
            "Wait cancellation completion",
            cancellationTokenSource2.Token);
        Assert.IsNotNull(completion);

        msg = await WaitForMessage(messageHandler, rpcMessage => rpcMessage is ErrorMessage, "Wait cancelRequest", cancellationTokenSource2.Token);

        var error = (ErrorMessage)msg!;
        Assert.AreEqual(ErrorCodes.RequestCanceled, error.ErrorCode);

        await WriteMessageAsync(writer, """{ "jsonrpc": "2.0", "method": "exit", "params": { } }""");

        int result = await serverTask;
        Assert.AreEqual(0, result);
    }

    [TestMethod]
    public async Task GlobalCancellationDuringDiscovery_DoesNotHangShutdown()
    {
        using var server = TcpServer.Create();
        TaskCompletionSource<bool> discoveryStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);

        string[] args = ["--no-banner", "--server", "--client-port", $"{server.Port}", "--internal-testingplatform-skipbuildercheck"];
        ITestApplicationBuilder builder = await TestApplication.CreateBuilderAsync(args);
        builder.RegisterTestFramework(_ => new TestFrameworkCapabilities(), (_, __) => new MockTestAdapter
        {
            DiscoveryAction = async context =>
            {
                discoveryStarted.TrySetResult(true);
                await Task.Delay(Timeout.Infinite, context.CancellationToken);
            },
        });

        var testApplication = (TestApplication)await builder.BuildAsync();
        testApplication.ServiceProvider.GetRequiredService<SystemConsole>().SuppressOutput();
        Task<int> serverTask = Task.Run(testApplication.RunAsync);

        using CancellationTokenSource timeout = new(TimeoutHelper.DefaultHangTimeSpanTimeout);
        using TcpClient client = await server.WaitForConnectionAsync(timeout.Token);
        using NetworkStream stream = client.GetStream();
        using StreamWriter writer = new(stream, Encoding.UTF8);
        TcpMessageHandler messageHandler = new(
            client,
            clientToServerStream: client.GetStream(),
            serverToClientStream: client.GetStream(),
            FormatterUtilities.CreateFormatter());

        await WriteMessageAsync(
            writer,
            """
            {
                "jsonrpc": "2.0",
                "id": 1,
                "method": "initialize",
                "params": {
                    "processId": 32,
                    "clientInfo": { "name": "testingplatform-unittests", "version": "1.0.0" },
                    "capabilities": {
                        "testing": {
                            "debuggerProvider": false
                        }
                    }
                }
            }
            """);
        _ = await WaitForMessage(
            messageHandler,
            rpcMessage => rpcMessage is ResponseMessage { Id: 1 },
            "Wait initialize",
            timeout.Token);

        await WriteMessageAsync(
            writer,
            """
            {
                "jsonrpc": "2.0",
                "id": 2,
                "method": "testing/discoverTests",
                "params": {
                    "runId": "00000000-0000-0000-0000-000000000002"
                }
            }
            """);
        await discoveryStarted.Task.TimeoutAfterAsync(TimeoutHelper.DefaultHangTimeSpanTimeout, timeout.Token);

        testApplication.ServiceProvider.GetTestApplicationCancellationTokenSource().Cancel();

        await serverTask.TimeoutAfterAsync(TimeoutHelper.DefaultHangTimeSpanTimeout, timeout.Token);
        Assert.AreEqual((int)ExitCode.TestSessionAborted, await serverTask);
    }

    [TestMethod]
    public async Task DeadlineStateIsIsolatedBetweenServerRequests()
    {
        using var server = TcpServer.Create();
        List<ServerRequestState> requestStates = [];

        string[] args = ["--no-banner", "--server", "--client-port", $"{server.Port}", "--internal-testingplatform-skipbuildercheck"];
        ITestApplicationBuilder builder = await TestApplication.CreateBuilderAsync(args);
        builder.RegisterTestFramework(
            _ => new TestFrameworkCapabilities(new RecordingGracefulStopCapability()),
            (capabilities, serviceProvider) =>
            {
                IStopPoliciesService stopPoliciesService = serviceProvider.GetRequiredService<IStopPoliciesService>();
                RecordingGracefulStopCapability capability = Assert.IsInstanceOfType<RecordingGracefulStopCapability>(
                    capabilities.GetCapability<IGracefulStopTestExecutionCapability>());
                ServerRequestState state = new(
                    stopPoliciesService,
                    serviceProvider.GetTestApplicationProcessExitCode(),
                    capability);
                requestStates.Add(state);

                return new MockTestAdapter
                {
                    DiscoveryAction = async context =>
                    {
                        await stopPoliciesService.RegisterOnDeadlineCallbackAsync(
                            () => capability.TryStopTestExecutionAsync(CancellationToken.None));
                        context.Complete();
                    },
                };
            });

        var testApplication = (TestApplication)await builder.BuildAsync();
        testApplication.ServiceProvider.GetRequiredService<SystemConsole>().SuppressOutput();
        Task<int> serverTask = Task.Run(testApplication.RunAsync);

        using CancellationTokenSource timeout = new(TimeoutHelper.DefaultHangTimeSpanTimeout);
        using TcpClient client = await server.WaitForConnectionAsync(timeout.Token);
        using NetworkStream stream = client.GetStream();
        using StreamWriter writer = new(stream, Encoding.UTF8);
        TcpMessageHandler messageHandler = new(
            client,
            clientToServerStream: client.GetStream(),
            serverToClientStream: client.GetStream(),
            FormatterUtilities.CreateFormatter());

        const string InitializeMessage = """
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
        await WriteMessageAsync(writer, InitializeMessage);
        _ = await WaitForMessage(
            messageHandler,
            rpcMessage => rpcMessage is ResponseMessage response && response.Id == 1,
            "Wait initialize",
            timeout.Token);

        const string FirstDiscoverMessage = """
            {
                "jsonrpc": "2.0",
                "id": 2,
                "method": "testing/discoverTests",
                "params": {
                    "runId": "00000000-0000-0000-0000-000000000001"
                }
            }
            """;
        await WriteMessageAsync(writer, FirstDiscoverMessage);
        _ = await WaitForMessage(
            messageHandler,
            rpcMessage => rpcMessage is ResponseMessage response && response.Id == 2,
            "Wait first discovery",
            timeout.Token);

        const string SecondDiscoverMessage = """
            {
                "jsonrpc": "2.0",
                "id": 3,
                "method": "testing/discoverTests",
                "params": {
                    "runId": "00000000-0000-0000-0000-000000000002"
                }
            }
            """;
        await WriteMessageAsync(writer, SecondDiscoverMessage);
        _ = await WaitForMessage(
            messageHandler,
            rpcMessage => rpcMessage is ResponseMessage response && response.Id == 3,
            "Wait second discovery",
            timeout.Token);

        Assert.HasCount(2, requestStates);
        await requestStates[0].StopPoliciesService.ExecuteDeadlineCallbacksAsync();

        Assert.IsTrue(requestStates[0].StopPoliciesService.IsDeadlineTriggered);
        Assert.AreEqual(1, requestStates[0].GracefulStopCapability.StopCount);
        Assert.AreEqual((int)ExitCode.TestExecutionStoppedAtDeadline, requestStates[0].TestApplicationResult.GetProcessExitCode());
        Assert.IsFalse(requestStates[1].StopPoliciesService.IsDeadlineTriggered);
        Assert.AreEqual(0, requestStates[1].GracefulStopCapability.StopCount);
        Assert.AreEqual((int)ExitCode.ZeroTests, requestStates[1].TestApplicationResult.GetProcessExitCode());

        await WriteMessageAsync(writer, """{ "jsonrpc": "2.0", "method": "exit", "params": { } }""");

        Assert.AreEqual(0, await serverTask);
    }

    [DataRow(JsonRpcMethods.TestingDiscoverTests)]
    [DataRow(JsonRpcMethods.TestingRunTests)]
    [TestMethod]
    public async Task RequestWithInvalidRunId_ReturnsInvalidParamsError(string method)
    {
        using var server = TcpServer.Create();

        string[] args = ["--no-banner", "--server", "--client-port", $"{server.Port}", "--internal-testingplatform-skipbuildercheck"];
        ITestApplicationBuilder builder = await TestApplication.CreateBuilderAsync(args);
        builder.RegisterTestFramework(_ => new TestFrameworkCapabilities(), (_, __) => new MockTestAdapter());
        var testApplication = (TestApplication)await builder.BuildAsync();
        testApplication.ServiceProvider.GetRequiredService<SystemConsole>().SuppressOutput();
        Task<int> serverTask = Task.Run(testApplication.RunAsync);

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

        using CancellationTokenSource cancellationTokenSource = new(TimeoutHelper.DefaultHangTimeSpanTimeout);
        await WaitForMessage(messageHandler, rpcMessage => rpcMessage is ResponseMessage, "Wait initialize", cancellationTokenSource.Token);

        // Send a malformed request with an invalid runId.
        string malformedMessage = $$"""
            {
                "jsonrpc": "2.0",
                "id": 2,
                "method": "{{method}}",
                "params": {
                    "runId": "not-a-guid"
                }
            }
            """;
        await WriteMessageAsync(writer, malformedMessage);

        RpcMessage? msg = await WaitForMessage(messageHandler, rpcMessage => rpcMessage is ErrorMessage, "Wait invalid-runId error", cancellationTokenSource.Token);
        var error = (ErrorMessage)msg!;
        Assert.AreEqual(2, error.Id);
        Assert.AreEqual(ErrorCodes.InvalidParams, error.ErrorCode);

        // The server should still be alive and able to handle the exit notification after the rejection.
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

    private static bool IsTestUpdateCompletion(RpcMessage? rpcMessage)
        => rpcMessage is NotificationMessage notification
            && notification.Method == JsonRpcMethods.TestingTestUpdatesTests
            && notification.Params is IDictionary<string, object?> completionParams
            && completionParams.TryGetValue(JsonRpcStrings.Changes, out object? changes)
            && changes is null;

    private static async Task WriteMessageAsync(StreamWriter writer, string message)
    {
        await writer.WriteLineAsync($"Content-Length: {message.Length}");
        await writer.WriteLineAsync("Content-Type: application/testingplatform");
        await writer.WriteLineAsync();
        await writer.WriteAsync(message);
        await writer.FlushAsync();
    }

    private sealed class TestApplicationHooks : ITestHostApplicationLifetime, IDisposable
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

    private sealed class BlockingTestFrameworkCapabilities : ITestFrameworkCapabilities, IDisposable
    {
        private readonly TaskCompletionSource<bool> _blocked = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly ManualResetEventSlim _release = new(initialState: false);
        private int _blockNextAccess;

        public IReadOnlyCollection<ITestFrameworkCapability> Capabilities
        {
            get
            {
                if (Interlocked.Exchange(ref _blockNextAccess, 0) == 1)
                {
                    _blocked.TrySetResult(true);
                    _release.Wait();
                }

                return [];
            }
        }

        public void BlockNextAccess() => Volatile.Write(ref _blockNextAccess, 1);

        public Task WaitUntilBlockedAsync() => _blocked.Task;

        public void Release() => _release.Set();

        public void Dispose() => _release.Dispose();
    }

    private sealed record ServerRequestState(
        IStopPoliciesService StopPoliciesService,
        ITestApplicationProcessExitCode TestApplicationResult,
        RecordingGracefulStopCapability GracefulStopCapability);

    private sealed class RecordingGracefulStopCapability : IGracefulStopTestExecutionResultCapability
    {
        public int StopCount { get; private set; }

        public Task StopTestExecutionAsync(CancellationToken cancellationToken)
        {
            StopCount++;
            return Task.CompletedTask;
        }

        public Task<bool> TryStopTestExecutionAsync(CancellationToken cancellationToken)
        {
            StopCount++;
            return Task.FromResult(true);
        }
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
