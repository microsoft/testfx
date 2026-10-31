// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Net.Sockets;

using Microsoft.Testing.Internal.Framework;
using Microsoft.Testing.Platform.Capabilities.TestFramework;
using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Extensions.OutputDevice;
using Microsoft.Testing.Platform.Extensions.TestFramework;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Hosts;
using Microsoft.Testing.Platform.Logging;
using Microsoft.Testing.Platform.Messages;
using Microsoft.Testing.Platform.OutputDevice;
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
        Assert.AreEqual("2.6.0-dev", fixture.Host.Version);
        Assert.AreEqual(0, fixture.FrameworkBuilds);
        fixture.Handler.Verify(h => h.ReadAsync(It.IsAny<CancellationToken>()), Times.Exactly(4));
        fixture.HandlerDisposal.Verify(d => d.Dispose(), Times.Once);
        Assert.IsTrue(fixture.Host.IsInitialized);
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
        private Task<int>? _run;
        private bool _failureObserved;

        public ServerFixture(CancellationToken token = default, bool fastFail = true)
        {
            Logger.Setup(l => l.LogAsync(It.IsAny<LogLevel>(), It.IsAny<string>(), It.IsAny<Exception?>(), It.IsAny<Func<string, Exception?, string>>())).Returns(Task.CompletedTask);
            Mock<ILoggerFactory> loggerFactory = new();
            loggerFactory.Setup(f => f.CreateLogger(It.IsAny<string>())).Returns(Logger.Object);
            Mock<IAsyncMonitorFactory> monitors = new();
            monitors.Setup(m => m.Create()).Returns(_monitor);
            Mock<IEnvironment> environment = new();
            environment.SetupGet(e => e.NewLine).Returns(Environment.NewLine);
            Mock<ITestApplicationCancellationTokenSource> application = new();
            application.SetupGet(a => a.CancellationToken).Returns(token);
            StopPoliciesService policies = new(application.Object) { ProcessRole = TestProcessRole.TestHost };
            Services.AddServices(
            [
                application.Object, loggerFactory.Object, monitors.Object, environment.Object, new SystemClock(),
                new UnhandledExceptionsPolicy(fastFail), new Mock<ICommandLineOptions>().Object, new SystemTask(), policies,
                Capabilities, new TestCoverageResult(), new Mock<ITelemetryCollector>().Object, new Mock<ITelemetryInformation>().Object,
            ]);
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
