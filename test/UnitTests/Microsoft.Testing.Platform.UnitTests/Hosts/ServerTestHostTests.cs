// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Concurrent;
using System.Net.Sockets;

using Microsoft.Testing.Internal.Framework;
using Microsoft.Testing.Platform.Capabilities.TestFramework;
using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Extensions.InvocationLifetime;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Extensions.OutputDevice;
using Microsoft.Testing.Platform.Extensions.TestFramework;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Hosts;
using Microsoft.Testing.Platform.Logging;
using Microsoft.Testing.Platform.Messages;
using Microsoft.Testing.Platform.OutputDevice;
using Microsoft.Testing.Platform.Requests;
using Microsoft.Testing.Platform.ServerMode;
using Microsoft.Testing.Platform.Services;
using Microsoft.Testing.Platform.Telemetry;
using Microsoft.Testing.Platform.TestHost;

using Moq;

namespace Microsoft.Testing.Platform.UnitTests;

[TestClass]
[UnsupportedOSPlatform("browser")]
public sealed class ServerTestHostTests
{
    private static readonly TimeSpan Limit = TimeSpan.FromSeconds(30);

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task RunAsync_ResponseAndErrorFramesDrainBeforeGracefulExit()
    {
        await using ServerFixture fixture = new();
        Assert.ThrowsExactly<InvalidOperationException>(fixture.Host.AssertInitialized);
        await Assert.ThrowsExactlyAsync<NotImplementedException>(fixture.Host.IsEnabledAsync);
        fixture.Incoming.Write(new ResponseMessage(17, "unsolicited") { StringId = "17" });
        fixture.Incoming.Write(new ErrorMessage(18, -32001, "client error", "details"));
        fixture.Incoming.Write(new NotificationMessage("not-initialized", null));
        fixture.Incoming.Write(new NotificationMessage(JsonRpcMethods.Exit, null));

        Assert.AreEqual(0, await fixture.StartAsync());

        Assert.AreEqual("ServerTestHost", fixture.Host.Uid);
        Assert.AreEqual("Server mode test host", fixture.Host.DisplayName);
        Assert.AreEqual("The 'ITestHost' implementation used when running server mode.", fixture.Host.Description);
        Assert.AreEqual(PlatformTestMetadata.Version, fixture.Host.Version);
        Assert.AreEqual(0, fixture.FrameworkBuilds);
        fixture.Handler.Verify(h => h.ReadAsync(It.IsAny<CancellationToken>()), Times.Exactly(4));
        fixture.HandlerDisposal.Verify(d => d.Dispose(), Times.Once);
        Assert.IsTrue(fixture.Host.IsInitialized);
    }

    [TestMethod]
    public async Task RunAsync_RunRequestsUseFreshInvocationHandlersAndRestoreEnvironmentBeforeDiscovery()
    {
#pragma warning disable TPEXP // Invocation lifetime handlers are experimental.
        await using ServerFixture fixture = new();
        List<string> events = [];
        int nextHandlerId = 0;
        InvocationLifetimeManager manager = fixture.InvocationLifetime;
        manager.AddInvocationLifetimeHandler(_ => new RecordingServerInvocationLifetimeHandler(
            "A", Interlocked.Increment(ref nextHandlerId), events.Add));
        manager.AddInvocationLifetimeHandler(_ => new RecordingServerInvocationLifetimeHandler(
            "B", Interlocked.Increment(ref nextHandlerId), events.Add));
        fixture.Execute = context =>
        {
            string value = fixture.Environment.Object.GetEnvironmentVariable("SERVER_HOOK_VALUE") ?? "<null>";
            events.Add(context.Request is RunTestExecutionRequest ? $"test:{value}" : $"discovery:{value}");
            context.Complete();
            return Task.CompletedTask;
        };

        Task<int> server = fixture.StartAsync();
        Assert.IsInstanceOfType<ResponseMessage>(await SendRequestAsync(
            fixture,
            1,
            JsonRpcMethods.Initialize,
            new InitializeRequestArgs(17, new Microsoft.Testing.Platform.ServerMode.ClientInfo("in-memory-client", "1.0"), new ClientCapabilities(false, true))));

        ResponseMessage firstRun = Assert.IsInstanceOfType<ResponseMessage>(await SendRequestAsync(
            fixture,
            2,
            JsonRpcMethods.TestingRunTests,
            new RunRequestArgs(Guid.NewGuid(), null, null)));
        Assert.IsInstanceOfType<RunResponseArgs>(firstRun.Result);
        events.Add($"restored:{fixture.Environment.Object.GetEnvironmentVariable("SERVER_HOOK_VALUE")}");

        ResponseMessage discovery = Assert.IsInstanceOfType<ResponseMessage>(await SendRequestAsync(
            fixture,
            3,
            JsonRpcMethods.TestingDiscoverTests,
            new DiscoverRequestArgs(Guid.NewGuid(), null, null)));
        Assert.IsInstanceOfType<DiscoverResponseArgs>(discovery.Result);

        ResponseMessage secondRun = Assert.IsInstanceOfType<ResponseMessage>(await SendRequestAsync(
            fixture,
            4,
            JsonRpcMethods.TestingRunTests,
            new RunRequestArgs(Guid.NewGuid(), null, null)));
        Assert.IsInstanceOfType<RunResponseArgs>(secondRun.Result);
        events.Add($"restored:{fixture.Environment.Object.GetEnvironmentVariable("SERVER_HOOK_VALUE")}");

        Assert.AreSequenceEqual(
            [
                "starting:A:1",
                "starting:B:2",
                "test:B-2",
                "completed:B:2",
                "completed:A:1",
                "restored:baseline",
                "discovery:baseline",
                "starting:A:3",
                "starting:B:4",
                "test:B-4",
                "completed:B:4",
                "completed:A:3",
                "restored:baseline",
            ],
            events);

        fixture.Incoming.Write(new NotificationMessage(JsonRpcMethods.Exit, null));
        Assert.AreEqual(0, await server.WaitAsync(Limit, TestContext.CancellationToken));
#pragma warning restore TPEXP
    }

    [TestMethod]
    public async Task RunAsync_RunAndDiscoveryWaitForAnActiveInvocation()
    {
#pragma warning disable TPEXP // Invocation lifetime handlers are experimental.
        await using ServerFixture fixture = new();
        ConcurrentQueue<string> events = new();
        TaskCompletionSource<bool> setupStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource<bool> releaseSetup = new(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.ReleaseBlockedExecution = () => releaseSetup.TrySetResult(true);
        int nextHandlerId = 0;
        InvocationLifetimeManager manager = fixture.InvocationLifetime;
        manager.AddInvocationLifetimeHandler(_ =>
        {
            int id = Interlocked.Increment(ref nextHandlerId);
            return new RecordingServerInvocationLifetimeHandler(
                "A",
                id,
                events.Enqueue,
                setupStarted: id == 1 ? setupStarted : null,
                releaseSetup: id == 1 ? releaseSetup : null);
        });
        manager.AddInvocationLifetimeHandler(_ => new RecordingServerInvocationLifetimeHandler(
            "B", Interlocked.Increment(ref nextHandlerId), events.Enqueue));
        fixture.Execute = context =>
        {
            string value = fixture.Environment.Object.GetEnvironmentVariable("SERVER_HOOK_VALUE") ?? "<null>";
            events.Enqueue(context.Request is RunTestExecutionRequest ? $"test:{value}" : $"discovery:{value}");
            context.Complete();
            return Task.CompletedTask;
        };

        TaskCompletionSource<bool> competingRequestsReceived = new(TaskCreationOptions.RunContinuationsAsynchronously);
        int runRequestsReceived = 0;
        int discoveryReceived = 0;
        fixture.Logger.Setup(l => l.LogAsync(LogLevel.Debug, It.IsAny<string>(), null, LoggingExtensions.Formatter))
            .Returns<LogLevel, string, Exception?, Func<string, Exception?, string>>((_, message, _, _) =>
            {
                if (message == "Received testing/runTests request"
                    && Interlocked.Increment(ref runRequestsReceived) == 2)
                {
                    if (Volatile.Read(ref discoveryReceived) != 0)
                    {
                        competingRequestsReceived.TrySetResult(true);
                    }
                }
                else if (message == "Received testing/discoverTests request")
                {
                    Volatile.Write(ref discoveryReceived, 1);
                    if (Volatile.Read(ref runRequestsReceived) == 2)
                    {
                        competingRequestsReceived.TrySetResult(true);
                    }
                }

                return Task.CompletedTask;
            });

        Task<int> server = fixture.StartAsync();
        Assert.IsInstanceOfType<ResponseMessage>(await SendRequestAsync(
            fixture,
            1,
            JsonRpcMethods.Initialize,
            new InitializeRequestArgs(17, new Microsoft.Testing.Platform.ServerMode.ClientInfo("in-memory-client", "1.0"), new ClientCapabilities(false, true))));

        fixture.Incoming.Write(new RequestMessage(2, JsonRpcMethods.TestingRunTests, new RunRequestArgs(Guid.NewGuid(), null, null)));
        await setupStarted.Task.WaitAsync(Limit, TestContext.CancellationToken);

        fixture.Incoming.Write(new RequestMessage(3, JsonRpcMethods.TestingDiscoverTests, new DiscoverRequestArgs(Guid.NewGuid(), null, null)));
        fixture.Incoming.Write(new RequestMessage(4, JsonRpcMethods.TestingRunTests, new RunRequestArgs(Guid.NewGuid(), null, null)));
        await competingRequestsReceived.Task.WaitAsync(Limit, TestContext.CancellationToken);

        Assert.AreSequenceEqual(["starting:A:1"], events.ToArray());
        releaseSetup.TrySetResult(true);

        Dictionary<int, RpcMessage> responses = await ReadResponsesAsync(fixture, 2, 3, 4);
        Assert.IsInstanceOfType<ResponseMessage>(responses[2]);
        Assert.IsInstanceOfType<ResponseMessage>(responses[3]);
        Assert.IsInstanceOfType<ResponseMessage>(responses[4]);
        string[] observed = events.ToArray();
        string[] discoveryBeforeRun =
        [
            "starting:A:1",
            "starting:B:2",
            "test:B-2",
            "completed:B:2",
            "completed:A:1",
            "discovery:baseline",
            "starting:A:3",
            "starting:B:4",
            "test:B-4",
            "completed:B:4",
            "completed:A:3",
        ];
        string[] runBeforeDiscovery =
        [
            "starting:A:1",
            "starting:B:2",
            "test:B-2",
            "completed:B:2",
            "completed:A:1",
            "starting:A:3",
            "starting:B:4",
            "test:B-4",
            "completed:B:4",
            "completed:A:3",
            "discovery:baseline",
        ];
        Assert.IsTrue(
            observed.SequenceEqual(discoveryBeforeRun) || observed.SequenceEqual(runBeforeDiscovery),
            string.Join(Environment.NewLine, observed));

        fixture.Incoming.Write(new NotificationMessage(JsonRpcMethods.Exit, null));
        Assert.AreEqual(0, await server.WaitAsync(Limit, TestContext.CancellationToken));
#pragma warning restore TPEXP
    }

    [TestMethod]
    public async Task RunAsync_NoInvocationHandlersPreservesRequestExecution()
    {
        await using ServerFixture fixture = new();
        List<string> events = [];
        fixture.Execute = context =>
        {
            string value = fixture.Environment.Object.GetEnvironmentVariable("SERVER_HOOK_VALUE") ?? "<null>";
            events.Add(context.Request is RunTestExecutionRequest ? "run" : $"discovery:{value}");
            context.Complete();
            return Task.CompletedTask;
        };

        Task<int> server = fixture.StartAsync();
        Assert.IsInstanceOfType<ResponseMessage>(await SendRequestAsync(
            fixture,
            1,
            JsonRpcMethods.Initialize,
            new InitializeRequestArgs(17, new Microsoft.Testing.Platform.ServerMode.ClientInfo("in-memory-client", "1.0"), new ClientCapabilities(false, true))));
        ResponseMessage response = Assert.IsInstanceOfType<ResponseMessage>(await SendRequestAsync(
            fixture,
            2,
            JsonRpcMethods.TestingRunTests,
            new RunRequestArgs(Guid.NewGuid(), null, null)));

        Assert.IsInstanceOfType<RunResponseArgs>(response.Result);
        Assert.AreSequenceEqual(["run"], events);
        Assert.AreEqual(1, fixture.FrameworkBuilds);

        fixture.Incoming.Write(new NotificationMessage(JsonRpcMethods.Exit, null));
        Assert.AreEqual(0, await server.WaitAsync(Limit, TestContext.CancellationToken));
    }

    [TestMethod]
    [DataRow("setup")]
    [DataRow("cleanup")]
    public async Task RunAsync_HandlerFailureReturnsRequestErrorAndServerRemainsUsable(string failurePhase)
    {
#pragma warning disable TPEXP // Invocation lifetime handlers are experimental.
        await using ServerFixture fixture = new();
        List<string> events = [];
        int nextHandlerId = 0;
        InvocationLifetimeManager manager = fixture.InvocationLifetime;
        manager.AddInvocationLifetimeHandler(_ => new RecordingServerInvocationLifetimeHandler(
            "A", Interlocked.Increment(ref nextHandlerId), events.Add));
        manager.AddInvocationLifetimeHandler(_ => new RecordingServerInvocationLifetimeHandler(
            "B",
            Interlocked.Increment(ref nextHandlerId),
            events.Add,
            failOnSetup: failurePhase == "setup",
            failOnCleanup: failurePhase == "cleanup"));
        fixture.Execute = context =>
        {
            events.Add($"test:{fixture.Environment.Object.GetEnvironmentVariable("SERVER_HOOK_VALUE")}");
            context.Complete();
            return Task.CompletedTask;
        };

        Task<int> server = fixture.StartAsync();
        Assert.IsInstanceOfType<ResponseMessage>(await SendRequestAsync(
            fixture,
            1,
            JsonRpcMethods.Initialize,
            new InitializeRequestArgs(17, new Microsoft.Testing.Platform.ServerMode.ClientInfo("in-memory-client", "1.0"), new ClientCapabilities(false, true))));

        ErrorMessage failure = Assert.IsInstanceOfType<ErrorMessage>(await SendRequestAsync(
            fixture,
            2,
            JsonRpcMethods.TestingRunTests,
            new RunRequestArgs(Guid.NewGuid(), null, null)));
        Assert.AreEqual(ErrorCodes.InternalError, failure.ErrorCode);
        Assert.AreEqual("baseline", fixture.Environment.Object.GetEnvironmentVariable("SERVER_HOOK_VALUE"));

        ResponseMessage recovery = Assert.IsInstanceOfType<ResponseMessage>(await SendRequestAsync(
            fixture,
            3,
            JsonRpcMethods.TestingRunTests,
            new RunRequestArgs(Guid.NewGuid(), null, null)));
        Assert.IsInstanceOfType<RunResponseArgs>(recovery.Result);
        string[] expected = failurePhase == "setup"
            ? ["starting:A:1", "starting:B:2", "completed:B:2", "completed:A:1", "starting:A:3", "starting:B:4", "test:B-4", "completed:B:4", "completed:A:3"]
            : ["starting:A:1", "starting:B:2", "test:B-2", "completed:B:2", "completed:A:1", "starting:A:3", "starting:B:4", "test:B-4", "completed:B:4", "completed:A:3"];
        Assert.AreSequenceEqual(expected, events);
        Assert.AreEqual("baseline", fixture.Environment.Object.GetEnvironmentVariable("SERVER_HOOK_VALUE"));

        fixture.Incoming.Write(new NotificationMessage(JsonRpcMethods.Exit, null));
        Assert.AreEqual(0, await server.WaitAsync(Limit, TestContext.CancellationToken));
#pragma warning restore TPEXP
    }

    [TestMethod]
    public async Task RunAsync_CanceledSetupReturnsRequestErrorAndServerRemainsUsable()
    {
#pragma warning disable TPEXP // Invocation lifetime handlers are experimental.
        await using ServerFixture fixture = new();
        List<string> events = [];
        TaskCompletionSource<bool> setupStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        int nextHandlerId = 0;
        InvocationLifetimeManager manager = fixture.InvocationLifetime;
        manager.AddInvocationLifetimeHandler(_ => new RecordingServerInvocationLifetimeHandler(
            "A",
            Interlocked.Increment(ref nextHandlerId),
            events.Add,
            setupStarted: setupStarted));
        manager.AddInvocationLifetimeHandler(_ => new RecordingServerInvocationLifetimeHandler(
            "B", Interlocked.Increment(ref nextHandlerId), events.Add));
        fixture.Execute = context =>
        {
            string value = fixture.Environment.Object.GetEnvironmentVariable("SERVER_HOOK_VALUE") ?? "<null>";
            events.Add(context.Request is RunTestExecutionRequest ? "run" : $"discovery:{value}");
            context.Complete();
            return Task.CompletedTask;
        };

        Task<int> server = fixture.StartAsync();
        Assert.IsInstanceOfType<ResponseMessage>(await SendRequestAsync(
            fixture,
            1,
            JsonRpcMethods.Initialize,
            new InitializeRequestArgs(17, new Microsoft.Testing.Platform.ServerMode.ClientInfo("in-memory-client", "1.0"), new ClientCapabilities(false, true))));

        using var cancelSender = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);
        Task cancelRequest = CancelWhenSetupStartsAsync(fixture, setupStarted, 2, cancelSender.Token);
        RpcMessage canceledResult = await SendRequestAsync(
            fixture,
            2,
            JsonRpcMethods.TestingRunTests,
            new RunRequestArgs(Guid.NewGuid(), null, null));
        cancelSender.Cancel();
        try
        {
            await cancelRequest;
        }
        catch (OperationCanceledException) when (cancelSender.IsCancellationRequested)
        {
            // If setup was never entered, the assertion below reports the missing cancellation behavior.
        }

        ErrorMessage canceled = Assert.IsInstanceOfType<ErrorMessage>(canceledResult);
        Assert.AreEqual(ErrorCodes.RequestCanceled, canceled.ErrorCode);

        ResponseMessage discovery = Assert.IsInstanceOfType<ResponseMessage>(await SendRequestAsync(
            fixture,
            3,
            JsonRpcMethods.TestingDiscoverTests,
            new DiscoverRequestArgs(Guid.NewGuid(), null, null)));
        Assert.IsInstanceOfType<DiscoverResponseArgs>(discovery.Result);
        Assert.AreSequenceEqual(["starting:A:1", "completed:A:1", "discovery:baseline"], events);
        Assert.AreEqual("baseline", fixture.Environment.Object.GetEnvironmentVariable("SERVER_HOOK_VALUE"));

        fixture.Incoming.Write(new NotificationMessage(JsonRpcMethods.Exit, null));
        Assert.AreEqual(0, await server.WaitAsync(Limit, TestContext.CancellationToken));
#pragma warning restore TPEXP
    }

    private async Task<RpcMessage> SendRequestAsync(ServerFixture fixture, int id, string method, object args)
    {
        fixture.Incoming.Write(new RequestMessage(id, method, args));
        while (true)
        {
            RpcMessage message = await fixture.Written.ReadAsync().WaitAsync(Limit, TestContext.CancellationToken);
            if ((message is ResponseMessage { Id: var responseId } && responseId == id)
                || (message is ErrorMessage { Id: var errorId } && errorId == id))
            {
                return message;
            }
        }
    }

    private async Task<Dictionary<int, RpcMessage>> ReadResponsesAsync(ServerFixture fixture, params int[] ids)
    {
        Dictionary<int, RpcMessage> responses = [];
        while (responses.Count < ids.Length)
        {
            RpcMessage message = await fixture.Written.ReadAsync().WaitAsync(Limit, TestContext.CancellationToken);
            int? id = message switch
            {
                ResponseMessage response => response.Id,
                ErrorMessage error => error.Id,
                _ => null,
            };
            if (id is int value && ids.Contains(value))
            {
                responses[value] = message;
            }
        }

        return responses;
    }

    private static async Task CancelWhenSetupStartsAsync(
        ServerFixture fixture,
        TaskCompletionSource<bool> setupStarted,
        int requestId,
        CancellationToken cancellationToken)
    {
        await setupStarted.Task.WaitAsync(cancellationToken);
        fixture.Incoming.Write(new NotificationMessage(JsonRpcMethods.CancelRequest, new CancelRequestArgs(requestId)));
    }

#pragma warning disable TPEXP // Invocation lifetime handlers are experimental.
    private sealed class RecordingServerInvocationLifetimeHandler(
        string name,
        int id,
        Action<string> record,
        bool failOnSetup = false,
        bool failOnCleanup = false,
        TaskCompletionSource<bool>? setupStarted = null,
        TaskCompletionSource<bool>? releaseSetup = null) : IInvocationLifetimeHandler
    {
        public string Uid => "ServerHandler" + name;

        public string Version => "1.0";

        public string DisplayName => Uid;

        public string Description => Uid;

        public Task<bool> IsEnabledAsync() => Task.FromResult(true);

        public async Task OnInvocationStartingAsync(InvocationStartingContext context, CancellationToken cancellationToken)
        {
            record($"starting:{name}:{id}");
            context.SetEnvironmentVariable("SERVER_HOOK_VALUE", $"{name}-{id}");
            if (setupStarted is not null)
            {
                setupStarted.TrySetResult(true);
                await (releaseSetup is null
                    ? Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken)
                    : releaseSetup.Task.WaitAsync(cancellationToken));
            }

            if (failOnSetup && id == 2)
            {
                throw new InvalidOperationException("setup failed");
            }
        }

        public Task OnInvocationCompletedAsync(InvocationCompletedContext context, CancellationToken cancellationToken)
        {
            record($"completed:{name}:{id}");
            return failOnCleanup && id == 2
                ? throw new InvalidOperationException("cleanup failed")
                : Task.CompletedTask;
        }
    }
#pragma warning restore TPEXP

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task RunAsync_NullIdErrorsAreDiagnosedWithoutLeakingRequestCounts(bool failingLogger)
    {
        await using ServerFixture fixture = new();
        TaskCompletionSource<bool> errorsLogged = new(TaskCreationOptions.RunContinuationsAsynchronously);
        int diagnostics = 0;
        fixture.Logger.Setup(l => l.LogAsync(LogLevel.Debug, It.IsAny<string>(), null, LoggingExtensions.Formatter))
            .Returns<LogLevel, string, Exception?, Func<string, Exception?, string>>((_, message, _, _) =>
            {
                if (message == "Received an uncorrelated JSON-RPC error (code -32700): JSON parse error")
                {
                    if (Interlocked.Increment(ref diagnostics) == 2)
                    {
                        errorsLogged.TrySetResult(true);
                    }

                    return failingLogger ? Task.FromException(new IOException("diagnostic sink failed")) : Task.CompletedTask;
                }

                return Task.CompletedTask;
            });

        Task<int> run = fixture.StartAsync();
        fixture.Incoming.Write(new ErrorMessage(null, ErrorCodes.ParseError, "JSON parse error", null));
        fixture.Incoming.Write(new ErrorMessage(null, ErrorCodes.ParseError, "JSON parse error", null));
        fixture.Incoming.Write(new RequestMessage(
            0, JsonRpcMethods.Initialize,
            new InitializeRequestArgs(17, new Microsoft.Testing.Platform.ServerMode.ClientInfo("in-memory-client", "1.0"), new ClientCapabilities(false, true))));

        ResponseMessage initialized = Assert.IsInstanceOfType<ResponseMessage>(await fixture.Written.ReadAsync().WaitAsync(Limit, TestContext.CancellationToken));
        Assert.AreEqual(0, initialized.Id);
        fixture.Incoming.Write(new NotificationMessage(JsonRpcMethods.Exit, null));

        Assert.AreEqual(0, await run.WaitAsync(Limit, TestContext.CancellationToken));
        await errorsLogged.Task.WaitAsync(Limit, TestContext.CancellationToken);
        Assert.AreEqual(2, Volatile.Read(ref diagnostics));
        fixture.Handler.Verify(h => h.WriteRequestAsync(It.IsAny<RpcMessage>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task Forwarding_BestEffortTransportErrorsAreLoggedAndRequiredUpdatesPropagate(bool canceledWrite)
    {
        await using ServerFixture fixture = new();
        Task<int> run = fixture.StartAsync();
        await fixture.Reading.Task.TimeoutAfterAsync(Limit);
        Exception expected = canceledWrite ? new OperationCanceledException("transport canceled") : new IOException("transport failed");
        RpcMessage? sent = null;
        fixture.Handler.Setup(h => h.WriteRequestAsync(It.IsAny<RpcMessage>(), It.IsAny<CancellationToken>()))
            .Callback<RpcMessage, CancellationToken>((message, _) => sent = message)
            .Returns(Task.FromException(expected));
        TaskCompletionSource<(LogLevel Level, string Message)> suppressed = new(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.Logger.Setup(l => l.LogAsync(It.IsAny<LogLevel>(), It.IsAny<string>(), null, LoggingExtensions.Formatter))
            .Returns<LogLevel, string, Exception?, Func<string, Exception?, string>>((level, message, _, _) =>
            {
                if (message.StartsWith("Suppressed ", StringComparison.Ordinal))
                {
                    suppressed.TrySetResult((level, message));
                    return Task.FromException(new IOException("diagnostic sink failed"));
                }

                return Task.CompletedTask;
            });

        await fixture.Host.PushDataAsync(new ServerLogMessage(LogLevel.Warning, "payload"), CancellationToken.None);
        await suppressed.Task.TimeoutAfterAsync(Limit);
        (LogLevel level, string diagnostic) = await suppressed.Task;
        Assert.AreEqual(canceledWrite ? LogLevel.Trace : LogLevel.Debug, level);
        Assert.StartsWith(canceledWrite ? "Suppressed cancellation while sending 'client/log': " : "Suppressed failure while sending 'client/log': ", diagnostic);
        NotificationMessage notification = Assert.IsInstanceOfType<NotificationMessage>(sent);
        Assert.AreEqual("client/log", notification.Method);
        LogEventArgs log = Assert.IsInstanceOfType<LogEventArgs>(notification.Params);
        Assert.AreEqual("payload", log.LogMessage.Message);
        Assert.AreEqual(LogLevel.Warning, log.LogMessage.Level);
        Guid runId = new("11111111-2222-3333-4444-555555555555");
        Exception propagated = await Assert.ThrowsAsync<Exception>(() => fixture.Host.SendTestUpdateCompleteAsync(runId, CancellationToken.None));
        Assert.AreSame(expected, propagated);
        NotificationMessage complete = Assert.IsInstanceOfType<NotificationMessage>(sent);
        Assert.AreEqual("testing/testUpdates/tests", complete.Method);
        TestNodeStateChangedEventArgs update = Assert.IsInstanceOfType<TestNodeStateChangedEventArgs>(complete.Params);
        Assert.AreEqual(runId, update.RunId);
        Assert.IsNull(update.Changes);
        fixture.Incoming.Write(new NotificationMessage(JsonRpcMethods.Exit, null));
        Assert.AreEqual(0, await run);
        int writes = fixture.Handler.Invocations.Count(invocation => invocation.Method.Name == nameof(IMessageHandler.WriteRequestAsync));

        await fixture.Host.PushDataAsync(new ServerLogMessage(LogLevel.Error, "after exit"), CancellationToken.None);

        Assert.AreEqual(writes, fixture.Handler.Invocations.Count(invocation => invocation.Method.Name == nameof(IMessageHandler.WriteRequestAsync)));
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(1)]
    [DataRow(2)]
    [DataRow(3)]
    public async Task RunAsync_CanceledTransportInitializationIsCooperative(int kind)
    {
        using CancellationTokenSource cancellation = new();
        await using ServerFixture fixture = new(cancellation.Token);
        Exception failure = kind switch
        {
            0 => new OperationCanceledException(cancellation.Token),
            1 => new ObjectDisposedException("transport"),
            2 => new InvalidOperationException("transport not connected"),
            _ => new SocketException((int)SocketError.OperationAborted),
        };
        fixture.Factory.Setup(f => f.CreateMessageHandlerAsync(cancellation.Token)).ThrowsAsync(failure);
        cancellation.Cancel();

        Assert.AreEqual(3, await fixture.StartAsync());
        Assert.AreEqual(0, fixture.FrameworkBuilds);
        fixture.Handler.Verify(h => h.ReadAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [TestMethod]
    public async Task RunAsync_CanceledTransportDoesNotSuppressAccessDenied()
    {
        using CancellationTokenSource cancellation = new();
        await using ServerFixture fixture = new(cancellation.Token);
        SocketException expected = new((int)SocketError.AccessDenied);
        fixture.Factory.Setup(f => f.CreateMessageHandlerAsync(cancellation.Token)).ThrowsAsync(expected);
        cancellation.Cancel();

        SocketException actual = await Assert.ThrowsExactlyAsync<SocketException>(fixture.StartAsync);

        Assert.AreSame(expected, actual);
        fixture.MarkObservedFailure();
        Assert.AreEqual(0, fixture.FrameworkBuilds);
    }

    [TestMethod]
    public async Task Cancellation_ThrowingRequestCallbackWarnsAndCompletesRequestAsCanceled()
    {
        await using ServerFixture fixture = new();
        TaskCompletionSource<bool> executing = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource<bool> finishExecution = new(TaskCreationOptions.RunContinuationsAsynchronously);
        fixture.ReleaseBlockedExecution = () => finishExecution.TrySetResult(true);
        int cancellationCallbacks = 0;
        fixture.Execute = async context =>
        {
            using CancellationTokenRegistration registration = context.CancellationToken.Register(() =>
            {
                Interlocked.Increment(ref cancellationCallbacks);
                throw new InvalidOperationException("cancellation callback sentinel");
            });
            executing.TrySetResult(true);
            await finishExecution.Task;
            context.Complete();
        };
        Task<int> run = fixture.StartAsync();
        fixture.Incoming.Write(new RequestMessage(
            1, JsonRpcMethods.Initialize,
            new InitializeRequestArgs(17, new Microsoft.Testing.Platform.ServerMode.ClientInfo("in-memory-client", "1.0"), new ClientCapabilities(false, true))));
        ResponseMessage initialized = Assert.IsInstanceOfType<ResponseMessage>(await fixture.Written.ReadAsync().WaitAsync(Limit, TestContext.CancellationToken));
        Assert.AreEqual(1, initialized.Id);
        InitializeResponseArgs initializedResult = Assert.IsInstanceOfType<InitializeResponseArgs>(initialized.Result);
        Assert.AreEqual("1.0.0", initializedResult.ProtocolVersion);
        Assert.IsFalse(initializedResult.Capabilities.TestingCapabilities.VSTestProviderSupport);
        Assert.IsFalse(fixture.Capabilities.HasCapability<INamedFeatureCapability>());
        Mock<INamedFeatureCapability> named = new();
        Assert.IsTrue(new TestFrameworkCapabilities(named.Object).HasCapability<INamedFeatureCapability>());
        fixture.Incoming.Write(new RequestMessage(
            17, JsonRpcMethods.TestingDiscoverTests,
            new DiscoverRequestArgs(new Guid("11111111-2222-3333-4444-555555555555"), null, null)));
        await executing.Task.TimeoutAfterAsync(Limit);
        fixture.Incoming.Write(new NotificationMessage(JsonRpcMethods.CancelRequest, new CancelRequestArgs(17)));
        fixture.Incoming.Write(new NotificationMessage(JsonRpcMethods.CancelRequest, new CancelRequestArgs(17)));
        await fixture.CancellationWarning.Task.TimeoutAfterAsync(Limit);
        Assert.AreEqual("Exception during the cancellation of request id '17'", await fixture.CancellationWarning.Task);
        Assert.AreEqual(1, cancellationCallbacks);
        finishExecution.SetResult(true);
        RpcMessage first = await fixture.Written.ReadAsync().WaitAsync(Limit, TestContext.CancellationToken);
        RpcMessage second = await fixture.Written.ReadAsync().WaitAsync(Limit, TestContext.CancellationToken);
        ErrorMessage canceled = Assert.IsInstanceOfType<ErrorMessage>(second);

        Assert.IsInstanceOfType<NotificationMessage>(first);
        Assert.AreEqual(17, canceled.Id);
        Assert.AreEqual(ErrorCodes.RequestCanceled, canceled.ErrorCode);
        Assert.AreEqual(string.Empty, canceled.Message);
        fixture.Incoming.Write(new NotificationMessage(JsonRpcMethods.Exit, null));
        Assert.AreEqual(0, await run);
        Assert.AreEqual(1, fixture.FrameworkBuilds);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task ServerUnhandledEvents_RealChildPreservesWarningAndMarksTasksObserved(bool taskException)
    {
        SystemWrappersTests.ChildResult child = await SystemWrappersTests.RunChildAsync(taskException ? "server-handler-task" : "server-handler-thread");
        string exception = taskException
            ? new AggregateException(
                "A Task's exception(s) were not observed either by Waiting on the Task or accessing its Exception property. As a result, the unobserved exception was rethrown by the finalizer thread.",
                new SystemWrapperChildProbe.ProbeException()).ToString()
            : "PROBE_EXCEPTION";
        string prefix = taskException
            ? "[ServerTestHost.OnTaskSchedulerUnobservedTaskException]"
            : "[ServerTestHost.OnCurrentDomainUnhandledException]";
        string state = prefix + (taskException ? " Unhandled task exception" : " IsTerminating: True");
        string output = prefix + (taskException ? " Unhandled exception: " + exception : " " + exception + Environment.NewLine + "IsTerminating: True");
        string[] records = child.Output.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Select(line => line.TrimStart('\uFEFF'))
            .Where(line => line.StartsWith("PROBE|", StringComparison.Ordinal))
            .Select(line => Encoding.UTF8.GetString(Convert.FromBase64String(line["PROBE|".Length..])))
            .ToArray();
        string[] expected = taskException
            ? ["server-log-Warning:" + state + Environment.NewLine + "------Exception detail------" + Environment.NewLine + exception, "server-output:" + output, "observed:True"]
            : ["server-log-Warning:" + state + Environment.NewLine + "------Exception detail------" + Environment.NewLine + exception, "server-output:" + output];

        Assert.AreSequenceEqual(expected, records);
        if (taskException)
        {
            Assert.AreEqual(0, child.ExitCode);
        }
        else
        {
            Assert.AreNotEqual(0, child.ExitCode);
        }
    }

    internal static ServerTestHost CreateUnhandledEventChild(Action<string> record)
    {
        ServerFixture fixture = new(fastFail: false);
        fixture.Logger.Setup(l => l.Log(It.IsAny<LogLevel>(), It.IsAny<string>(), It.IsAny<Exception?>(), It.IsAny<Func<string, Exception?, string>>()))
            .Callback<LogLevel, string, Exception?, Func<string, Exception?, string>>((level, message, exception, formatter) =>
                record("server-log-" + level + ":" + formatter(message, exception)));
        fixture.Output.Setup(o => o.DisplayAsync(It.IsAny<IOutputDeviceDataProducer>(), It.IsAny<IOutputDeviceData>(), It.IsAny<CancellationToken>()))
            .Callback<IOutputDeviceDataProducer, IOutputDeviceData, CancellationToken>((_, data, _) =>
                record("server-output:" + Assert.IsInstanceOfType<WarningMessageOutputDeviceData>(data).Message))
            .Returns(Task.CompletedTask);
        return fixture.Host;
    }

    internal static async Task WithRunningHostAsync(Func<ServerTestHost, Func<Task<RpcMessage>>, Task> action)
    {
        await using ServerFixture fixture = new();
        Task<int> run = fixture.StartAsync();
        await fixture.Reading.Task.TimeoutAfterAsync(Limit);
        await action(fixture.Host, () => fixture.Written.ReadAsync().WaitAsync(Limit));
        fixture.Incoming.Write(new NotificationMessage(JsonRpcMethods.Exit, null));
        Assert.AreEqual(0, await run.WaitAsync(Limit));
    }

    private sealed class ServerFixture : IAsyncDisposable
    {
        private readonly SystemAsyncMonitor _monitor = new();
        private readonly Dictionary<string, string?> _environmentVariables = new(StringComparer.Ordinal)
        {
            ["SERVER_HOOK_VALUE"] = "baseline",
        };

        private Task<int>? _run;
        private bool _failureObserved;

        public ServerFixture(CancellationToken token = default, bool fastFail = true)
        {
            Logger.Setup(l => l.LogAsync(It.IsAny<LogLevel>(), It.IsAny<string>(), It.IsAny<Exception?>(), It.IsAny<Func<string, Exception?, string>>())).Returns(Task.CompletedTask);
            Mock<ILoggerFactory> loggerFactory = new();
            loggerFactory.Setup(f => f.CreateLogger(It.IsAny<string>())).Returns(Logger.Object);
            Mock<IAsyncMonitorFactory> monitors = new();
            monitors.Setup(m => m.Create()).Returns(_monitor);
            Environment.SetupGet(e => e.NewLine).Returns(System.Environment.NewLine);
            Environment.SetupGet(e => e.ProcessId).Returns(1234);
            Environment.Setup(e => e.GetEnvironmentVariable(It.IsAny<string>()))
                .Returns<string>(name => _environmentVariables.TryGetValue(name, out string? value) ? value : null);
            Environment.Setup(e => e.GetEnvironmentVariables())
                .Returns(() => new System.Collections.Hashtable(_environmentVariables));
            Environment.Setup(e => e.SetEnvironmentVariable(It.IsAny<string>(), It.IsAny<string?>()))
                .Callback<string, string?>((name, value) =>
                {
                    if (value is null)
                    {
                        _environmentVariables.Remove(name);
                    }
                    else
                    {
                        _environmentVariables[name] = value;
                    }
                });
            Mock<ITestApplicationCancellationTokenSource> application = new();
            application.SetupGet(a => a.CancellationToken).Returns(token);
            StopPoliciesService policies = new(application.Object) { ProcessRole = TestProcessRole.TestHost };
            Services.AddServices(
            [
                application.Object, loggerFactory.Object, monitors.Object, Environment.Object, new SystemClock(),
                new UnhandledExceptionsPolicy(fastFail), new Mock<ICommandLineOptions>().Object, new SystemTask(), policies,
                Capabilities, new TestCoverageResult(), new Mock<ITelemetryCollector>().Object, new Mock<ITelemetryInformation>().Object,
            ]);
            Services.AddService(InvocationLifetime);
            Output.Setup(o => o.DisplayAsync(It.IsAny<IOutputDeviceDataProducer>(), It.IsAny<IOutputDeviceData>(), It.IsAny<CancellationToken>()))
                .Callback<IOutputDeviceDataProducer, IOutputDeviceData, CancellationToken>((_, data, _) =>
                {
                    if (data is WarningMessageOutputDeviceData warning)
                    {
                        CancellationWarning.TrySetResult(warning.Message);
                    }
                })
                .Returns(Task.CompletedTask);
            Services.AddService(new ProxyOutputDevice(Output.Object, null));
            HandlerDisposal = Handler.As<IDisposable>();
            Handler.Setup(h => h.ReadAsync(It.IsAny<CancellationToken>())).Returns<CancellationToken>(cancellationToken =>
            {
                Reading.TrySetResult(true);
                return Incoming.ReadAsync().WaitAsync(cancellationToken);
            });
            Handler.Setup(h => h.WriteRequestAsync(It.IsAny<RpcMessage>(), It.IsAny<CancellationToken>()))
                .Callback<RpcMessage, CancellationToken>((message, _) => Written.Write(message)).Returns(Task.CompletedTask);
            Factory.Setup(f => f.CreateMessageHandlerAsync(token)).ReturnsAsync(Handler.Object);
            Host = new(
                Services, BuildFrameworkAsync, Factory.Object,
                new TestFrameworkManager((_, _) => throw new NotSupportedException(), _ => Capabilities),
                new TestHostManager());
        }

        public ServiceProvider Services { get; } = new();

        public Mock<ILogger> Logger { get; } = new();

        public Mock<IPlatformOutputDevice> Output { get; } = new();

        public Mock<IEnvironment> Environment { get; } = new();

        public InvocationLifetimeManager InvocationLifetime { get; } = new();

        public Mock<IMessageHandler> Handler { get; } = new();

        public Mock<IDisposable> HandlerDisposal { get; }

        public Mock<IMessageHandlerFactory> Factory { get; } = new();

        public AsyncQueue<RpcMessage?> Incoming { get; } = new();

        public AsyncQueue<RpcMessage> Written { get; } = new();

        public TaskCompletionSource<bool> Reading { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource<string> CancellationWarning { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TestFrameworkCapabilities Capabilities { get; } = new();

        public ServerTestHost Host { get; }

        public int FrameworkBuilds { get; private set; }

        public Func<ExecuteRequestContext, Task>? Execute { get; set; }

        public Action? ReleaseBlockedExecution { get; set; }

        public Task<int> StartAsync() => _run = Host.RunAsync();

        public void MarkObservedFailure() => _failureObserved = true;

        public async ValueTask DisposeAsync()
        {
            try
            {
                ReleaseBlockedExecution?.Invoke();
                if (_run is not null && !_failureObserved)
                {
                    Incoming.Write(new NotificationMessage(JsonRpcMethods.Exit, null));
                    await _run.TimeoutAfterAsync(Limit);
                }
            }
            finally
            {
                Host.Dispose();
                _monitor.Dispose();
            }
        }

        private Task<ITestFramework> BuildFrameworkAsync(TestFrameworkBuilderData data)
        {
            FrameworkBuilds++;
            data.ServiceProvider.AddService(data.TestExecutionRequestFactory);
            Mock<BaseMessageBus> bus = new();
            bus.Setup(b => b.DrainDataAsync()).Returns(Task.CompletedTask);
            bus.Setup(b => b.DisableAsync()).Returns(Task.CompletedTask);
            bus.SetupGet(b => b.DataConsumerServices).Returns([]);
            bus.SetupGet(b => b.ConsumersStillRunning).Returns([]);
            data.ServiceProvider.AddService(bus.Object);
            Mock<ITestFramework> framework = new();
            framework.SetupGet(f => f.Uid).Returns("in-memory-framework");
            framework.SetupGet(f => f.Version).Returns("1.0");
            framework.Setup(f => f.CreateTestSessionAsync(It.IsAny<CreateTestSessionContext>())).ReturnsAsync(new CreateTestSessionResult { IsSuccess = true });
            framework.Setup(f => f.CloseTestSessionAsync(It.IsAny<CloseTestSessionContext>())).ReturnsAsync(new CloseTestSessionResult { IsSuccess = true });
            framework.Setup(f => f.ExecuteRequestAsync(It.IsAny<ExecuteRequestContext>())).Returns<ExecuteRequestContext>(context =>
            {
                if (Execute is not null)
                {
                    return Execute(context);
                }

                context.Complete();
                return Task.CompletedTask;
            });
            return Task.FromResult(framework.Object);
        }
    }

    private sealed class AsyncQueue<T>
    {
#if NET9_0_OR_GREATER
        private readonly Lock _sync = new();
#else
        private readonly object _sync = new();
#endif
        private readonly Queue<T> _items = new();
        private readonly Queue<TaskCompletionSource<T>> _waiters = new();

        public Task<T> ReadAsync()
        {
            lock (_sync)
            {
                if (_items.Count > 0)
                {
                    return Task.FromResult(_items.Dequeue());
                }

                TaskCompletionSource<T> waiter = new(TaskCreationOptions.RunContinuationsAsynchronously);
                _waiters.Enqueue(waiter);
                return waiter.Task;
            }
        }

        public void Write(T item)
        {
            TaskCompletionSource<T>? waiter = null;
            lock (_sync)
            {
                if (_waiters.Count > 0)
                {
                    waiter = _waiters.Dequeue();
                }
                else
                {
                    _items.Enqueue(item);
                }
            }

            waiter?.SetResult(item);
        }
    }
}
