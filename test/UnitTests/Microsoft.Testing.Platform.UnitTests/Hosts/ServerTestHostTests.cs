// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Internal.Framework;
using Microsoft.Testing.Platform.Capabilities.TestFramework;
using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Extensions.Messages;
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

    private sealed class ServerFixture : IAsyncDisposable
    {
        private readonly SystemAsyncMonitor _monitor = new();
        private Task<int>? _run;

        public ServerFixture()
        {
            Logger.Setup(l => l.LogAsync(It.IsAny<LogLevel>(), It.IsAny<string>(), It.IsAny<Exception?>(), It.IsAny<Func<string, Exception?, string>>())).Returns(Task.CompletedTask);
            Mock<ILoggerFactory> loggerFactory = new();
            loggerFactory.Setup(f => f.CreateLogger(It.IsAny<string>())).Returns(Logger.Object);
            Mock<IAsyncMonitorFactory> monitors = new();
            monitors.Setup(m => m.Create()).Returns(_monitor);
            Mock<IEnvironment> environment = new();
            environment.SetupGet(e => e.NewLine).Returns(Environment.NewLine);
            Mock<ITestApplicationCancellationTokenSource> application = new();
            application.SetupGet(a => a.CancellationToken).Returns(CancellationToken.None);
            StopPoliciesService policies = new(application.Object) { ProcessRole = TestProcessRole.TestHost };
            Services.AddServices(
            [
                application.Object, loggerFactory.Object, monitors.Object, environment.Object, new SystemClock(),
                new UnhandledExceptionsPolicy(true), new Mock<ICommandLineOptions>().Object, new SystemTask(), policies,
                Capabilities, new TestCoverageResult(), new Mock<ITelemetryCollector>().Object, new Mock<ITelemetryInformation>().Object,
            ]);
            Services.AddService(new ProxyOutputDevice(new Mock<IPlatformOutputDevice>().Object, null));
            Handler.As<IDisposable>();
            Handler.Setup(h => h.ReadAsync(It.IsAny<CancellationToken>())).Returns<CancellationToken>(cancellationToken =>
                Incoming.ReadAsync().WaitAsync(cancellationToken));
            Handler.Setup(h => h.WriteRequestAsync(It.IsAny<RpcMessage>(), It.IsAny<CancellationToken>()))
                .Callback<RpcMessage, CancellationToken>((message, _) => Written.Write(message)).Returns(Task.CompletedTask);
            Mock<IMessageHandlerFactory> factory = new();
            factory.Setup(f => f.CreateMessageHandlerAsync(CancellationToken.None)).ReturnsAsync(Handler.Object);
            Host = new(
                Services, BuildFrameworkAsync, factory.Object,
                new TestFrameworkManager((_, _) => throw new NotSupportedException(), _ => Capabilities),
                new TestHostManager());
        }

        public ServiceProvider Services { get; } = new();

        public Mock<ILogger> Logger { get; } = new();

        public Mock<IMessageHandler> Handler { get; } = new();

        public AsyncQueue<RpcMessage?> Incoming { get; } = new();

        public AsyncQueue<RpcMessage> Written { get; } = new();

        public TestFrameworkCapabilities Capabilities { get; } = new();

        public ServerTestHost Host { get; }

        public Task<int> StartAsync() => _run = Host.RunAsync();

        public async ValueTask DisposeAsync()
        {
            try
            {
                if (_run is not null)
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

        private static Task<ITestFramework> BuildFrameworkAsync(TestFrameworkBuilderData data)
        {
            data.ServiceProvider.AddService(data.TestExecutionRequestFactory);
            Mock<BaseMessageBus> bus = new();
            bus.Setup(b => b.DrainDataAsync()).Returns(Task.CompletedTask);
            bus.Setup(b => b.DisableAsync()).Returns(Task.CompletedTask);
            bus.SetupGet(b => b.DataConsumerServices).Returns([]);
            data.ServiceProvider.AddService(bus.Object);
            Mock<ITestFramework> framework = new();
            framework.SetupGet(f => f.Uid).Returns("in-memory-framework");
            framework.SetupGet(f => f.Version).Returns("1.0");
            framework.Setup(f => f.CreateTestSessionAsync(It.IsAny<CreateTestSessionContext>())).ReturnsAsync(new CreateTestSessionResult { IsSuccess = true });
            framework.Setup(f => f.CloseTestSessionAsync(It.IsAny<CloseTestSessionContext>())).ReturnsAsync(new CloseTestSessionResult { IsSuccess = true });
            framework.Setup(f => f.ExecuteRequestAsync(It.IsAny<ExecuteRequestContext>())).Returns<ExecuteRequestContext>(context =>
            {
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
