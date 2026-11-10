// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Concurrent;
using System.IO.Pipes;

using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Extensions.TestHostControllers;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Hosts;
using Microsoft.Testing.Platform.IPC.Models;
using Microsoft.Testing.Platform.Logging;
using Microsoft.Testing.Platform.Messages;
using Microsoft.Testing.Platform.OutputDevice;
using Microsoft.Testing.Platform.Services;
using Microsoft.Testing.Platform.Telemetry;
using Microsoft.Testing.Platform.TestHostControllers;

using Moq;

namespace Microsoft.Testing.Platform.UnitTests.Hosts;

[TestClass]
[UnsupportedOSPlatform("browser")]
public sealed class TestHostControllersTestHostFinalizationTests : IDisposable
{
    private readonly Mock<IProcess> _process = new();
    private readonly Mock<ILogger> _logger = new();
    private readonly Mock<ITestHostProcessLifetimeHandler> _lifetimeHandler = new();
    private readonly TestHostControllersTestHost _host;

    public TestHostControllersTestHostFinalizationTests()
    {
        _process.SetupGet(x => x.HasExited).Returns(true);
        _process.SetupGet(x => x.ExitCode).Returns(0);
        _logger.Setup(x => x.IsEnabled(It.IsAny<LogLevel>())).Returns(true);
        _logger
            .Setup(x => x.LogAsync(It.IsAny<LogLevel>(), It.IsAny<string>(), null, LoggingExtensions.Formatter))
            .Returns(Task.CompletedTask);
        _lifetimeHandler
            .Setup(x => x.OnTestHostProcessExitedAsync(It.IsAny<ITestHostProcessInformation>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        Mock<ILoggerFactory> loggerFactory = new();
        loggerFactory.Setup(x => x.CreateLogger(It.IsAny<string>())).Returns(_logger.Object);
        Mock<BaseMessageBus> messageBus = new();
        messageBus.Setup(x => x.DrainDataAsync()).Returns(Task.CompletedTask);
        messageBus.Setup(x => x.DisableAsync()).Returns(Task.CompletedTask);
        MessageBusProxy messageBusProxy = new();
        messageBusProxy.SetBuiltMessageBus(messageBus.Object);
        IEnvironment environment = Mock.Of<IEnvironment>();
        ServiceProvider serviceProvider = new();
        serviceProvider.AddServices([messageBusProxy, new TestCoverageResult(), Mock.Of<ICommandLineOptions>(), environment]);
        _host = new(
            new([], [_lifetimeHandler.Object], [], testHostLauncher: null, requireProcessRestart: false),
            serviceProvider,
            passiveNode: null,
            environment,
            loggerFactory.Object,
            Mock.Of<IClock>());
    }

    [TestMethod]
    public async Task FinalizeTestHostProcessAsync_PidUpdatedDuringProcessSnapshot_UsesLatestPid()
    {
        await ReportPidAsync(101);
        Task duplicatePidRequest = Task.CompletedTask;
        _process.SetupGet(x => x.HasExited).Returns(() =>
        {
            duplicatePidRequest = ReportPidAsync(202);
            return true;
        });

        (_, TestHostProcessInformation processInformation, _) = await FinalizeTestHostProcessAsync();
        await duplicatePidRequest;

        Assert.AreEqual(202, processInformation.PID);
    }

    [TestMethod]
    public async Task FinalizeTestHostProcessAsync_PidUpdatedByExitHandler_DiagnosticUsesLatestPid()
    {
        await ReportPidAsync(101);
        _lifetimeHandler
            .Setup(x => x.OnTestHostProcessExitedAsync(It.IsAny<ITestHostProcessInformation>(), It.IsAny<CancellationToken>()))
            .Returns(() => ReportPidAsync(303));
        string? warning = null;
        _logger
            .Setup(x => x.LogAsync(LogLevel.Warning, It.IsAny<string>(), null, LoggingExtensions.Formatter))
            .Callback<LogLevel, string, Exception?, Func<string, Exception?, string>>(
                (_, message, _, _) => warning = message)
            .Returns(Task.CompletedTask);

        (_, TestHostProcessInformation processInformation, _) = await FinalizeTestHostProcessAsync();

        Assert.AreEqual(101, processInformation.PID);
        const string ExpectedWarning = """
            Test host did not exit gracefully.
              OS exit code: '0'
              OS exit code authoritative: 'True'
              IPC-reported exit code: '<not received>'
              TestHostCompletedRequest received: 'False'
              PID: '303'
              CancellationRequested: 'False'
            """;
        Assert.AreEqual(ExpectedWarning.Replace("\r\n", "\n"), warning?.Replace("\r\n", "\n"));
    }

    public void Dispose() => _host.Dispose();

    private Task<(int ExitCode, TestHostProcessInformation ProcessInformation, string? ExtensionInformation)> FinalizeTestHostProcessAsync()
    {
        MethodInfo method = typeof(TestHostControllersTestHost).GetMethod(
            "FinalizeTestHostProcessAsync",
            BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException("Could not find TestHostControllersTestHost.FinalizeTestHostProcessAsync.");
        return (Task<(int ExitCode, TestHostProcessInformation ProcessInformation, string? ExtensionInformation)>?)method.Invoke(
            _host,
            [
                _process.Object,
                new ProxyOutputDevice(new NopPlatformOutputDevice(), serverModeOutputDevice: null),
                new TelemetryInformation(isEnabled: false, version: "test"),
                Stopwatch.StartNew(),
                CancellationToken.None,
            ])
            ?? throw new InvalidOperationException("TestHostControllersTestHost.FinalizeTestHostProcessAsync returned null.");
    }

    private Task ReportPidAsync(int pid)
    {
        MethodInfo method = typeof(TestHostControllersTestHost).GetMethod(
            "HandleRequestAsync",
            BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException("Could not find TestHostControllersTestHost.HandleRequestAsync.");
        // IPC models are source-linked into tests, so use the request type from the host assembly.
        Type requestType = typeof(TestHostControllersTestHost).Assembly.GetType(typeof(TestHostProcessPIDRequest).FullName!)
            ?? throw new InvalidOperationException("Could not find the host's TestHostProcessPIDRequest type.");
        object request = Activator.CreateInstance(requestType, [pid])
            ?? throw new InvalidOperationException("Could not create TestHostProcessPIDRequest.");
        return (Task?)method.Invoke(_host, [request])
            ?? throw new InvalidOperationException("TestHostControllersTestHost.HandleRequestAsync returned null.");
    }

    [TestMethod]
    [DataRow("exit", false)]
    [DataRow("output", false)]
    [DataRow("completion", false)]
    [DataRow("output", true)]
    public Task ControllerFinalization_CancellationAbandonsOnlyActiveCallbacksAndReportsOnce(string stage, bool warningThrows)
        => RunBoundedControllerFinalizationAsync(stage, warningThrows);

    [TestMethod]
    [DataRow("lifetime-dispose")]
    [DataRow("provider-dispose")]
    [DataRow("service-dispose")]
    public Task ControllerFinalization_ExpiredBudgetSkipsRemainingOwnedServices(string stage)
        => RunBoundedControllerFinalizationAsync(stage, warningThrows: false);

    private static async Task RunBoundedControllerFinalizationAsync(string stage, bool warningThrows)
    {
        ControllerHostFixture fixture = new();
        TaskCompletionSource<CancellationToken> entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource<bool> release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        using ManualResetEventSlim releaseDisposal = new(false);
        TaskCompletionSource<bool> callbackFinished = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource<bool> abortEntered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource<bool> abortRelease = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource<string> warning = new(TaskCreationOptions.RunContinuationsAsynchronously);
        int warningCount = 0;
        CancellationToken lastFinalizationToken = default;
        bool disposing = stage.EndsWith("-dispose", StringComparison.Ordinal);
        fixture.Environment.Setup(e => e.GetEnvironmentVariable(
                EnvironmentVariableConstants.TESTINGPLATFORM_TESTHOSTCONTROLLER_FINALIZATION_TIMEOUT_SECONDS))
            .Returns("0.001");
        fixture.LoggerFactory.Setup(f => f.CreateLogger(It.IsAny<string>()))
            .Returns(new FinalizationWarningLogger(text =>
            {
                Interlocked.Increment(ref warningCount);
                warning.TrySetResult(text);
                if (warningThrows)
                {
                    throw new InvalidOperationException("owned warning provider sentinel");
                }
            }));
        var policies = Mock.Get(fixture.Services.GetRequiredService<IStopPoliciesService>());
        policies.Setup(p => p.ExecuteAbortCallbacksAsync()).Returns(() =>
        {
            abortEntered.TrySetResult(true);
            return abortRelease.Task;
        });
        fixture.Completion.Setup(c => c.OnRunCompletedAsync(
                It.IsAny<int>(), It.IsAny<IReadOnlyList<SessionFileArtifact>>(), It.IsAny<CancellationToken>()))
            .Callback<int, IReadOnlyList<SessionFileArtifact>, CancellationToken>((_, _, token) => lastFinalizationToken = token)
            .Returns(Task.CompletedTask);
        if (stage == "exit")
        {
            fixture.Handler.Setup(h => h.OnTestHostProcessExitedAsync(It.IsAny<ITestHostProcessInformation>(), It.IsAny<CancellationToken>()))
                .Returns<ITestHostProcessInformation, CancellationToken>((_, token) => HoldAsync(token));
        }
        else if (stage == "output")
        {
            fixture.Output.Setup(o => o.DisplayAfterSessionEndRunAsync(It.IsAny<CancellationToken>())).Returns<CancellationToken>(HoldAsync);
        }
        else if (stage == "completion")
        {
            fixture.Completion.Setup(c => c.OnRunCompletedAsync(
                    It.IsAny<int>(), It.IsAny<IReadOnlyList<SessionFileArtifact>>(), It.IsAny<CancellationToken>()))
                .Returns<int, IReadOnlyList<SessionFileArtifact>, CancellationToken>((_, _, token) => HoldAsync(token));
        }
        else if (stage == "lifetime-dispose")
        {
            fixture.Handler.As<IDisposable>().Setup(d => d.Dispose()).Callback(HoldDisposal);
        }
        else if (stage == "provider-dispose")
        {
            fixture.Provider.As<IDisposable>().Setup(d => d.Dispose()).Callback(HoldDisposal);
        }

        Mock<IDataConsumer> idleConsumer = new();
        Mock<IDisposable> idleDisposable = idleConsumer.As<IDisposable>();
        idleConsumer.SetupGet(c => c.Uid).Returns("owned-idle-consumer");
        idleConsumer.Setup(c => c.IsEnabledAsync()).ReturnsAsync(true);
        idleConsumer.SetupGet(c => c.DataTypesConsumed).Returns([typeof(FileArtifact)]);
        idleConsumer.Setup(c => c.ConsumeAsync(It.IsAny<IDataProducer>(), It.IsAny<IData>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        Mock<IDisposable> ownedService = new();
        if (stage == "service-dispose")
        {
            ownedService.Setup(s => s.Dispose()).Callback(HoldDisposal);
        }

        fixture.Services.AddService(ownedService.Object);
        TestHostControllerConfiguration configuration = new(
            [fixture.Provider.Object], [fixture.Handler.Object], [idleConsumer.Object], null, requireProcessRestart: true)
        {
            RunCompletionHandlers = [fixture.Completion.Object],
        };
        using TestHostControllersTestHost host = new(
            configuration, fixture.Services, null, fixture.Environment.Object, fixture.LoggerFactory.Object, fixture.Services.GetClock());
        Task<int>? run = null;
        try
        {
            run = host.RunAsync();
            Task<Task> first = Task.WhenAny(entered.Task, run);
            await first.TimeoutAfterAsync(ControllerHostFixture.OperationLimit);
            if (await first == run)
            {
                await run;
                Assert.Fail("Controller completed before the selected callback was entered.");
            }

            await entered.Task.TimeoutAfterAsync(ControllerHostFixture.OperationLimit);
            CancellationToken finalizationToken = await entered.Task;
            Assert.IsFalse(finalizationToken.IsCancellationRequested);
            TaskCompletionSource<bool> cancellationObserved = new(TaskCreationOptions.RunContinuationsAsynchronously);
            using CancellationTokenRegistration registration = finalizationToken.Register(() => cancellationObserved.TrySetResult(true));

            fixture.Cancellation.Cancel();
            await cancellationObserved.Task.TimeoutAfterAsync(ControllerHostFixture.OperationLimit);
            await run.TimeoutAfterAsync(ControllerHostFixture.OperationLimit);
            Assert.AreEqual(3, await run);

            Assert.IsFalse(callbackFinished.Task.IsCompleted, "RunAsync must not wait for a callback that ignored the canceled cleanup token.");
            idleDisposable.Verify(d => d.Dispose(), stage == "service-dispose" ? Times.Once() : Times.Never());
            if (stage != "service-dispose")
            {
                ownedService.Verify(d => d.Dispose(), Times.Never);
            }

            if (!disposing)
            {
                if (stage == "completion")
                {
                    // This callback follows the last abort-join point in the real source order.
                    policies.Verify(p => p.ExecuteAbortCallbacksAsync(), Times.Never);
                }
                else
                {
                    await abortEntered.Task.TimeoutAfterAsync(ControllerHostFixture.OperationLimit);
                    policies.Verify(p => p.ExecuteAbortCallbacksAsync(), Times.Once);
                }

                await warning.Task.TimeoutAfterAsync(ControllerHostFixture.OperationLimit);
                Assert.AreEqual("Test host controller extension finalization exceeded the 00:00:00.0010000 cleanup timeout.", await warning.Task);
                Assert.AreEqual(1, Volatile.Read(ref warningCount));
                fixture.Completion.Verify(
                    c => c.OnRunCompletedAsync(It.IsAny<int>(), It.IsAny<IReadOnlyList<SessionFileArtifact>>(), It.IsAny<CancellationToken>()),
                    stage == "completion" ? Times.Once() : Times.Never());
            }

            if (stage == "exit")
            {
                fixture.Handler.As<IDisposable>().Verify(d => d.Dispose(), Times.Never);
            }
        }
        finally
        {
            release.TrySetResult(true);
            releaseDisposal.Set();
            abortRelease.TrySetResult(true);
            if (entered.Task.IsCompleted)
            {
                await callbackFinished.Task.TimeoutAfterAsync(ControllerHostFixture.OperationLimit);
            }

            if (run is not null)
            {
                await run.TimeoutAfterAsync(ControllerHostFixture.OperationLimit);
            }

            if (stage == "exit")
            {
                // This earlier timeout intentionally abandons an undisabled bus; its test owner still joins it.
                await fixture.Bus.DisableAsync().TimeoutAfterAsync(ControllerHostFixture.OperationLimit);
            }

            await fixture.DisposeAsync();
        }

        async Task HoldAsync(CancellationToken token)
        {
            entered.TrySetResult(token);
            try
            {
                await release.Task;
            }
            finally
            {
                callbackFinished.TrySetResult(true);
            }
        }

        void HoldDisposal()
        {
            entered.TrySetResult(lastFinalizationToken);
            try
            {
                releaseDisposal.Wait(CancellationToken.None);
            }
            finally
            {
                callbackFinished.TrySetResult(true);
            }
        }
    }

    private sealed class FinalizationWarningLogger(Action<string> warn) : ILogger
    {
        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            string text = formatter(state, exception);
            if (logLevel == LogLevel.Warning && text.StartsWith("Test host controller extension finalization exceeded", StringComparison.Ordinal))
            {
                warn(text);
            }
        }

        public Task LogAsync<TState>(LogLevel logLevel, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            Log(logLevel, state, exception, formatter);
            return Task.CompletedTask;
        }
    }

    [TestMethod]
    public async Task ControllerIpc_UnsupportedRequestReachesFailFastAndRethrowsOriginal()
    {
        ControllerHostFixture fixture = new();
        TaskCompletionSource<bool> started = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource<bool> exited = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource<bool> failed = new(TaskCreationOptions.RunContinuationsAsynchronously);
        ConcurrentQueue<(string Message, Exception Error)> diagnostics = new();
        bool hasExited = false;
        int exitSignaled = 0;
        Task? peer = null;
        fixture.Process.SetupGet(p => p.HasExited).Returns(() => Volatile.Read(ref hasExited));
        fixture.Process.Setup(p => p.WaitForExitAsync(It.IsAny<CancellationToken>())).Returns(exited.Task);
        fixture.Handler.Setup(h => h.OnTestHostProcessStartedAsync(It.IsAny<ITestHostProcessInformation>(), It.IsAny<CancellationToken>()))
            .Callback(() => started.TrySetResult(true))
            .Returns(Task.CompletedTask);
        fixture.Environment.Setup(e => e.FailFast(It.IsAny<string>(), It.IsAny<Exception>()))
            .Callback<string, Exception>((message, error) =>
            {
                diagnostics.Enqueue((message, error));
                failed.TrySetResult(true);
                if (Interlocked.Exchange(ref exitSignaled, 1) == 0)
                {
                    Volatile.Write(ref hasExited, true);
                    exited.TrySetResult(true);
                    fixture.Process.Raise(p => p.Exited += null!, EventArgs.Empty);
                }
            });
        fixture.ProcessHandler.Setup(p => p.Start(It.IsAny<ProcessStartInfo>()))
            .Callback<ProcessStartInfo>(info =>
                peer = ExchangeUnsupportedRequestAsync(info.EnvironmentVariables[EnvironmentVariableConstants.TESTINGPLATFORM_TESTHOSTCONTROLLER_PIPENAME + "_42"]!, started.Task))
            .Returns(fixture.Process.Object);
        Task<int>? run = null;
        try
        {
            run = fixture.RunAsync();
            await failed.Task.TimeoutAfterAsync(ControllerHostFixture.OperationLimit);
            await run.TimeoutAfterAsync(ControllerHostFixture.OperationLimit);

            Assert.AreEqual(7, await run);
            (string routerMessage, Exception routerError) = Assert.ContainsSingle(diagnostics.Where(d =>
                d.Message.StartsWith("[TestHostControllersTestHost] Unhandled exception:\n", StringComparison.Ordinal)));
            NotSupportedException unsupported = Assert.IsInstanceOfType<NotSupportedException>(routerError);
            Assert.AreEqual("Request 'Microsoft.Testing.Platform.IPC.Models.WaitForServerControlRequest' not supported", unsupported.Message);
            Assert.StartsWith("[TestHostControllersTestHost] Unhandled exception:\nSystem.NotSupportedException:", routerMessage);
            Assert.AreSame(
                unsupported,
                Assert.ContainsSingle(diagnostics.Where(d => d.Message.StartsWith("[NamedPipeServer] Unhandled exception:", StringComparison.Ordinal))).Error);
            Assert.HasCount(2, diagnostics);
            Assert.IsNotNull(peer);
            await peer.TimeoutAfterAsync(ControllerHostFixture.OperationLimit);
        }
        finally
        {
            exited.TrySetResult(true);
            if (run is not null)
            {
                await run.TimeoutAfterAsync(ControllerHostFixture.OperationLimit);
            }

            if (peer is not null)
            {
                await peer.TimeoutAfterAsync(ControllerHostFixture.OperationLimit);
            }

            await fixture.DisposeAsync();
        }
    }

    private static async Task ExchangeUnsupportedRequestAsync(string name, Task started)
    {
        using NamedPipeClientStream stream = new(".", name, PipeDirection.InOut, PipeOptions.Asynchronous);
        await stream.ConnectAsync(CancellationToken.None);
        byte[] pid = [.. BitConverter.GetBytes(8), .. BitConverter.GetBytes(2), .. BitConverter.GetBytes(606)];
        await stream.WriteAsync(pid, 0, pid.Length, CancellationToken.None);
        await stream.FlushAsync(CancellationToken.None);
        byte[] acknowledgement = new byte[8];
        int offset = 0;
        while (offset < acknowledgement.Length)
        {
            int count = await stream.ReadAsync(acknowledgement, offset, acknowledgement.Length - offset, CancellationToken.None);
            Assert.IsGreaterThan(0, count);
            offset += count;
        }

        byte[] expected = [.. BitConverter.GetBytes(4), .. BitConverter.GetBytes(0)];
        Assert.AreSequenceEqual(expected, acknowledgement);
        await started;
        byte[] unsupported = [.. BitConverter.GetBytes(4), .. BitConverter.GetBytes(13)];
        await stream.WriteAsync(unsupported, 0, unsupported.Length, CancellationToken.None);
        await stream.FlushAsync(CancellationToken.None);
        try
        {
            Assert.AreEqual(0, await stream.ReadAsync(new byte[1], 0, 1, CancellationToken.None));
        }
        catch (IOException error) when (error.InnerException is System.Net.Sockets.SocketException
        {
            SocketErrorCode: System.Net.Sockets.SocketError.ConnectionReset,
        })
        {
            // Unix may expose a closed failed protocol stream as a reset rather than a zero-byte read.
        }
    }
}
