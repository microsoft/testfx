// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Hosts;
using Microsoft.Testing.Platform.IPC;
using Microsoft.Testing.Platform.Logging;
using Microsoft.Testing.Platform.Services;

using Moq;

namespace Microsoft.Testing.Platform.UnitTests;

[TestClass]
[UnsupportedOSPlatform("browser")]
public sealed class TestHostControllerCancellationTests
{
    // Production closes the stalled control channel after one second. Allow loaded CI agents extra time
    // to schedule the blocking Dispose call and observe the resulting pipe disconnect.
    private static readonly TimeSpan DisposalTestTimeout = TimeSpan.FromSeconds(30);

    public TestContext TestContext { get; set; }

    [TestMethod]
    public async Task RequestCancellation_CancelsChildApplication()
    {
        Mock<ILoggerFactory> loggerFactory = CreateLoggerFactory();
        SystemEnvironment environment = new();
        using var server = new TestHostControllerCancellationServer(
            authorizedSecurityIdentities: null,
            environment,
            loggerFactory.Object,
            new SystemTask());
        server.Start();
        using CTRLPlusCCancellationTokenSource applicationCancellationTokenSource = new();
        using var listener = new TestHostControllerCancellationListener(
            server.PipeName,
            applicationCancellationTokenSource,
            environment,
            new NopLogger());

        await server.WaitForRequestAsync().TimeoutAfterAsync(TimeoutHelper.DefaultHangTimeSpanTimeout);
        server.RequestCancellation();

        Assert.IsTrue(
            applicationCancellationTokenSource.CancellationToken.WaitHandle.WaitOne(TimeoutHelper.DefaultHangTimeSpanTimeout),
            "The controller cancellation request was not propagated to the child application.");
        Assert.IsTrue(listener.WasCancellationRequestedByController);
    }

    [TestMethod]
    public async Task RequestCancellation_BeforeChildConnects_CancelsChildAfterConnection()
    {
        Mock<ILoggerFactory> loggerFactory = CreateLoggerFactory();
        SystemEnvironment environment = new();
        UnexpectedListenerFailureLogger listenerLogger = new();
        using var server = new TestHostControllerCancellationServer(
            authorizedSecurityIdentities: null,
            environment,
            loggerFactory.Object,
            new SystemTask());
        server.Start();
        server.RequestCancellation();
        using CTRLPlusCCancellationTokenSource applicationCancellationTokenSource = new();
        using var listener = new TestHostControllerCancellationListener(
            server.PipeName,
            applicationCancellationTokenSource,
            environment,
            listenerLogger);

        await WaitForOperationOrListenerFailureAsync(server.WaitForRequestAsync(), listenerLogger);

        TaskCompletionSource<bool> cancellationObserved = new(TaskCreationOptions.RunContinuationsAsynchronously);
        using CancellationTokenRegistration registration =
            applicationCancellationTokenSource.CancellationToken.Register(() => cancellationObserved.TrySetResult(true));
        await WaitForOperationOrListenerFailureAsync(cancellationObserved.Task, listenerLogger);

        Assert.IsTrue(listener.WasCancellationRequestedByController);
    }

    [TestMethod]
    public async Task ServerDisposal_WhenClientConnectsWithoutSendingRequest_DoesNotHang()
    {
        Mock<ILoggerFactory> loggerFactory = CreateLoggerFactory();
        SystemEnvironment environment = new();
        var server = new TestHostControllerCancellationServer(
            authorizedSecurityIdentities: null,
            environment,
            loggerFactory.Object,
            new SystemTask());
        server.Start();
        using var client = new System.IO.Pipes.NamedPipeClientStream(
            ".",
            server.PipeName,
            System.IO.Pipes.PipeDirection.InOut,
            System.IO.Pipes.PipeOptions.Asynchronous);
        await client.ConnectAsync(TestContext.CancellationToken);

        await DisposeServerAsync(server);

        byte[] responseBuffer = new byte[1];
        try
        {
            Task<int> readTask = client.ReadAsync(responseBuffer, 0, responseBuffer.Length, TestContext.CancellationToken);
            Task completedTask = await Task.WhenAny(readTask, Task.Delay(DisposalTestTimeout, TestContext.CancellationToken));
            Assert.AreSame(readTask, completedTask, "The client remained connected after server disposal.");
            int bytesRead = await readTask;
            Assert.AreEqual(0, bytesRead);
        }
        catch (IOException ex) when (ex.InnerException is System.Net.Sockets.SocketException
        {
            SocketErrorCode: System.Net.Sockets.SocketError.ConnectionReset,
        })
        {
            // Unix can report a forced close of an idle pipe as a connection reset instead of EOF.
        }
    }

    [TestMethod]
    public async Task ServerDisposal_WhenNoClientConnects_DoesNotHang()
    {
        Mock<ILoggerFactory> loggerFactory = CreateLoggerFactory();
        SystemEnvironment environment = new();
        var server = new TestHostControllerCancellationServer(
            authorizedSecurityIdentities: null,
            environment,
            loggerFactory.Object,
            new SystemTask());
        server.Start();

        await DisposeServerAsync(server);

        using var lateClient = new System.IO.Pipes.NamedPipeClientStream(
            ".",
            server.PipeName,
            System.IO.Pipes.PipeDirection.InOut,
            System.IO.Pipes.PipeOptions.Asynchronous);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(TestContext.CancellationToken);
        timeout.CancelAfter(TimeSpan.FromMilliseconds(100));
        await Assert.ThrowsAsync<Exception>(() => lateClient.ConnectAsync(timeout.Token));
    }

    [TestMethod]
    public async Task ServerDisposal_WhenClientKeepsConnectionOpenAfterRequest_DoesNotHang()
    {
        Mock<ILoggerFactory> loggerFactory = CreateLoggerFactory();
        SystemEnvironment environment = new();
        var server = new TestHostControllerCancellationServer(
            authorizedSecurityIdentities: null,
            environment,
            loggerFactory.Object,
            new SystemTask());
        server.Start();
        using var client = new System.IO.Pipes.NamedPipeClientStream(
            ".",
            server.PipeName,
            System.IO.Pipes.PipeDirection.InOut,
            System.IO.Pipes.PipeOptions.Asynchronous);
        await client.ConnectAsync(TestContext.CancellationToken);
        byte[] requestFrame = [.. BitConverter.GetBytes(sizeof(int)), .. BitConverter.GetBytes(13)];
        await client.WriteAsync(requestFrame, 0, requestFrame.Length, TestContext.CancellationToken);
        await client.FlushAsync(TestContext.CancellationToken);
        await server.WaitForRequestAsync().TimeoutAfterAsync(TimeoutHelper.DefaultHangTimeSpanTimeout);

        await DisposeServerAsync(server);

        byte[] responseBuffer = new byte[64];
        int bytesRead;
        do
        {
            bytesRead = await client.ReadAsync(responseBuffer, 0, responseBuffer.Length, TestContext.CancellationToken);
        }
        while (bytesRead > 0);

        Assert.AreEqual(0, bytesRead);
    }

    [TestMethod]
    public async Task NormalServerDisposal_DoesNotCancelChildApplication()
    {
        for (int i = 0; i < 20; i++)
        {
            Mock<ILoggerFactory> loggerFactory = CreateLoggerFactory();
            SystemEnvironment environment = new();
            var server = new TestHostControllerCancellationServer(
                authorizedSecurityIdentities: null,
                environment,
                loggerFactory.Object,
                new SystemTask());
            server.Start();
            using CTRLPlusCCancellationTokenSource applicationCancellationTokenSource = new();
            using var listener = new TestHostControllerCancellationListener(
                server.PipeName,
                applicationCancellationTokenSource,
                environment,
                new NopLogger());

            await server.WaitForRequestAsync().TimeoutAfterAsync(TimeoutHelper.DefaultHangTimeSpanTimeout);
            server.Dispose();

            Assert.IsFalse(applicationCancellationTokenSource.CancellationToken.IsCancellationRequested);
            Assert.IsFalse(listener.WasCancellationRequestedByController);
        }
    }

    private static Task DisposeServerAsync(TestHostControllerCancellationServer server)
        // Synchronous disposal waits for asynchronous pipe continuations. A pooled worker can starve those
        // continuations on .NET Framework when the rest of the method-parallel suite occupies the pool.
        => Task.Factory.StartNew(server.Dispose, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default)
            .TimeoutAfterAsync(DisposalTestTimeout);

    private static Mock<ILoggerFactory> CreateLoggerFactory()
    {
        Mock<ILoggerFactory> loggerFactory = new();
        loggerFactory.Setup(x => x.CreateLogger(It.IsAny<string>())).Returns(new NopLogger());
        return loggerFactory;
    }

    private async Task WaitForOperationOrListenerFailureAsync(Task operation, UnexpectedListenerFailureLogger listenerLogger)
    {
        Task<Task> completionTask = Task.WhenAny(operation, listenerLogger.Failure);
        await completionTask.TimeoutAfterAsync(TimeoutHelper.DefaultHangTimeSpanTimeout, TestContext.CancellationToken);

        if (await completionTask == listenerLogger.Failure)
        {
            Assert.Fail(await listenerLogger.Failure);
        }

        await operation;
    }

    private sealed class UnexpectedListenerFailureLogger : ILogger
    {
        private readonly TaskCompletionSource<string> _failure = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task<string> Failure => _failure.Task;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            => _failure.TrySetResult(formatter(state, exception));

        public Task LogAsync<TState>(LogLevel logLevel, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            _failure.TrySetResult(formatter(state, exception));
            return Task.CompletedTask;
        }
    }

    [TestMethod]
    public async Task CancellationListener_DisposalBeforeConnectionIsIdempotentAndNonCanceling()
    {
        using CTRLPlusCCancellationTokenSource application = new();
        using TestHostControllerCancellationListener listener = new(
            "owned-missing-controller-" + Guid.NewGuid().ToString("N"),
            application, new SystemEnvironment(), new NopLogger());
        Assert.IsFalse(listener.ShouldReportCompletionAfterCancellation);

        await DisposeListenerJoinedAsync(listener);
        Action disposeAgain = listener.Dispose;
        disposeAgain();

        Assert.IsFalse(application.CancellationToken.IsCancellationRequested);
        Assert.IsFalse(listener.WasCancellationRequestedByController);
        Assert.IsFalse(listener.ShouldReportCompletionAfterCancellation);
    }

    [TestMethod]
    public async Task CancellationListener_PeerEofLogsDiagnosticWithoutCancelingApplication()
    {
        string name = NamedPipeServer.GetPipeName("listener-eof-" + Guid.NewGuid().ToString("N")).Name;
        using System.IO.Pipes.NamedPipeServerStream peer = new(
            name, System.IO.Pipes.PipeDirection.InOut, 1, System.IO.Pipes.PipeTransmissionMode.Byte, System.IO.Pipes.PipeOptions.Asynchronous);
        TaskCompletionSource<bool> received = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource<bool> close = new(TaskCreationOptions.RunContinuationsAsynchronously);
        var peerTask = Task.Run(
            async () =>
            {
                await peer.WaitForConnectionAsync(TestContext.CancellationToken);
                byte[] request = new byte[8];
                int offset = 0;
                while (offset < request.Length)
                {
                    int count = await peer.ReadAsync(request, offset, request.Length - offset, TestContext.CancellationToken);
                    Assert.IsGreaterThan(0, count);
                    offset += count;
                }

                byte[] expected = [.. BitConverter.GetBytes(4), .. BitConverter.GetBytes(13)];
                Assert.AreSequenceEqual(expected, request);
                received.TrySetResult(true);
                await close.Task;
                peer.Dispose();
            },
            TestContext.CancellationToken);
        using CTRLPlusCCancellationTokenSource application = new();
        Mock<IEnvironment> environment = new();
        UnexpectedListenerFailureLogger logger = new();
        using TestHostControllerCancellationListener listener = new(name, application, environment.Object, logger);
        try
        {
            await received.Task.TimeoutAfterAsync(DisposalTestTimeout);
            Assert.IsFalse(listener.ShouldReportCompletionAfterCancellation);
            close.TrySetResult(true);

            await logger.Failure.TimeoutAfterAsync(DisposalTestTimeout);
            string diagnostic = await logger.Failure;

            Assert.StartsWith("Test host controller cancellation channel stopped unexpectedly: System.IO.IOException:", diagnostic);
            Assert.IsFalse(application.CancellationToken.IsCancellationRequested);
            Assert.IsFalse(listener.WasCancellationRequestedByController);
            Assert.IsFalse(listener.ShouldReportCompletionAfterCancellation);
            environment.Verify(e => e.Exit(It.IsAny<int>()), Times.Never);
            await DisposeListenerJoinedAsync(listener);
            await DisposeListenerJoinedAsync(listener);
        }
        finally
        {
            close.TrySetResult(true);
            await peerTask.TimeoutAfterAsync(DisposalTestTimeout);
        }
    }

    [TestMethod]
    public async Task CancellationListener_CallbackAndLoggerFailuresPreserveControllerReason()
    {
        Mock<ILogger> sourceLogger = new();
        AggregateException loggingFailure = new("source logging sentinel", new IOException("provider sentinel"));
        sourceLogger.Setup(l => l.Log(LogLevel.Warning, It.IsAny<string>(), null, LoggingExtensions.Formatter))
            .Throws(loggingFailure);
        using CTRLPlusCCancellationTokenSource application = new(logger: sourceLogger.Object);
        using CancellationTokenRegistration callback = application.CancellationToken.Register(
            () => throw new InvalidOperationException("cancellation callback sentinel"));
        TaskCompletionSource<string> warned = new(TaskCreationOptions.RunContinuationsAsynchronously);
        Mock<ILogger> listenerLogger = new();
        listenerLogger.Setup(l => l.Log(LogLevel.Warning, It.IsAny<string>(), null, LoggingExtensions.Formatter))
            .Callback<LogLevel, string, Exception?, Func<string, Exception?, string>>((_, text, _, _) => warned.TrySetResult(text));
        using TestHostControllerCancellationServer server = new(
            null, new SystemEnvironment(), CreateLoggerFactory().Object, new SystemTask());
        server.Start();
        using TestHostControllerCancellationListener listener = new(
            server.PipeName, application, new SystemEnvironment(), listenerLogger.Object);
        await server.WaitForRequestAsync().TimeoutAfterAsync(DisposalTestTimeout);
        Assert.IsFalse(listener.ShouldReportCompletionAfterCancellation);

        server.RequestCancellation();
        await warned.Task.TimeoutAfterAsync(DisposalTestTimeout);
        string warning = await warned.Task;

        Assert.StartsWith("Exception while propagating test host controller cancellation:\nSystem.AggregateException:", warning);
        Assert.Contains("source logging sentinel", warning);
        Assert.Contains("provider sentinel", warning);
        Assert.IsTrue(application.CancellationToken.IsCancellationRequested);
        Assert.IsTrue(listener.WasCancellationRequestedByController);
        Assert.IsTrue(listener.ShouldReportCompletionAfterCancellation);
        sourceLogger.Verify(l => l.Log(LogLevel.Warning, It.IsAny<string>(), null, LoggingExtensions.Formatter), Times.Once);
        listenerLogger.Verify(l => l.Log(LogLevel.Warning, It.IsAny<string>(), null, LoggingExtensions.Formatter), Times.Once);
        await DisposeListenerJoinedAsync(listener);
    }

    private static Task DisposeListenerJoinedAsync(TestHostControllerCancellationListener listener)
#if NETCOREAPP
        => listener.DisposeAsync().AsTask().TimeoutAfterAsync(DisposalTestTimeout);
#else
        => Task.Factory.StartNew(listener.Dispose, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default)
            .TimeoutAfterAsync(DisposalTestTimeout);
#endif
}
