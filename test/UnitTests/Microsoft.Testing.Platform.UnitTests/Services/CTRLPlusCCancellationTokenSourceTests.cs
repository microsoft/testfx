// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Logging;
using Microsoft.Testing.Platform.Services;

namespace Microsoft.Testing.Platform.UnitTests;

[TestClass]
public sealed class CTRLPlusCCancellationTokenSourceTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public void Initial_State_NeitherTokenIsCancelled()
    {
        using var source = CTRLPlusCCancellationTokenSource.CreateWithTimeouts(
            console: null,
            logger: null,
            gracePeriod: Timeout.InfiniteTimeSpan,
            abortTimeout: Timeout.InfiniteTimeSpan);

        Assert.IsFalse(source.CancellationToken.IsCancellationRequested);
        Assert.IsFalse(source.DrainingToken.IsCancellationRequested);
        Assert.IsFalse(source.AbortingToken.IsCancellationRequested);
        Assert.IsFalse(source.WasCancellationRequestedByConsole);
    }

    [TestMethod]
    public void Cancel_OnlySignalsDrainingToken()
    {
        using var source = CTRLPlusCCancellationTokenSource.CreateWithTimeouts(
            console: null,
            logger: null,
            gracePeriod: Timeout.InfiniteTimeSpan,
            abortTimeout: Timeout.InfiniteTimeSpan);

        source.Cancel();

        Assert.IsTrue(source.DrainingToken.IsCancellationRequested);
        Assert.IsTrue(source.CancellationToken.IsCancellationRequested, "Legacy alias must follow DrainingToken.");
        Assert.IsFalse(source.AbortingToken.IsCancellationRequested);
        Assert.IsFalse(source.WasCancellationRequestedByConsole);
    }

    [TestMethod]
    public void Abort_SignalsBothTokens()
    {
        using var source = CTRLPlusCCancellationTokenSource.CreateWithTimeouts(
            console: null,
            logger: null,
            gracePeriod: Timeout.InfiniteTimeSpan,
            abortTimeout: Timeout.InfiniteTimeSpan);

        source.Abort();

        Assert.IsTrue(source.DrainingToken.IsCancellationRequested);
        Assert.IsTrue(source.AbortingToken.IsCancellationRequested);
    }

    [TestMethod]
    public void Cancel_IsIdempotent()
    {
        using var source = CTRLPlusCCancellationTokenSource.CreateWithTimeouts(
            console: null,
            logger: null,
            gracePeriod: Timeout.InfiniteTimeSpan,
            abortTimeout: Timeout.InfiniteTimeSpan);

        source.Cancel();
        source.Cancel();
        source.Cancel();

        Assert.IsTrue(source.DrainingToken.IsCancellationRequested);
        Assert.IsFalse(source.AbortingToken.IsCancellationRequested);
    }

    [TestMethod]
    public async Task GracePeriodElapse_EscalatesToAborting()
    {
        using var source = CTRLPlusCCancellationTokenSource.CreateWithTimeouts(
            console: null,
            logger: null,
            gracePeriod: TimeSpan.FromMilliseconds(50),
            abortTimeout: Timeout.InfiniteTimeSpan);

        source.Cancel();
        Assert.IsTrue(source.DrainingToken.IsCancellationRequested);

        await WaitForCancellationAsync(source.AbortingToken);

        Assert.IsTrue(source.AbortingToken.IsCancellationRequested, "Aborting must trip after the grace period.");
    }

    [TestMethod]
    public async Task CancelAfter_EntersDrainingThenEscalatesToAborting()
    {
        using var source = CTRLPlusCCancellationTokenSource.CreateWithTimeouts(
            console: null,
            logger: null,
            gracePeriod: TimeSpan.FromMilliseconds(50),
            abortTimeout: Timeout.InfiniteTimeSpan);

        source.CancelAfter(TimeSpan.FromMilliseconds(10));

        await WaitForCancellationAsync(source.DrainingToken);
        await WaitForCancellationAsync(source.AbortingToken);

        Assert.IsTrue(source.DrainingToken.IsCancellationRequested);
        Assert.IsTrue(source.AbortingToken.IsCancellationRequested);
    }

    [TestMethod]
    public void ZeroGracePeriod_ImmediatelyEscalatesToAborting()
    {
        using var source = CTRLPlusCCancellationTokenSource.CreateWithTimeouts(
            console: null,
            logger: null,
            gracePeriod: TimeSpan.Zero,
            abortTimeout: Timeout.InfiniteTimeSpan);

        source.Cancel();

        Assert.IsTrue(source.DrainingToken.IsCancellationRequested);
        Assert.IsTrue(source.AbortingToken.IsCancellationRequested);
    }

    [TestMethod]
    public void Dispose_UnsubscribesFromConsoleCancelKeyPress()
    {
        var console = new TrackingConsole();
        {
            using var source = CTRLPlusCCancellationTokenSource.CreateWithTimeouts(
                console,
                logger: null,
                gracePeriod: Timeout.InfiniteTimeSpan,
                abortTimeout: Timeout.InfiniteTimeSpan);

            Assert.AreEqual(1, console.CancelKeyPressSubscriberCount);
            GC.KeepAlive(source);
        }

        Assert.AreEqual(0, console.CancelKeyPressSubscriberCount);
    }

    [TestMethod]
    public void CtrlC_Handler_TransitionsThroughExpectedPhasesAndCancelValues()
    {
        var console = new TrackingConsole();
        using var source = CTRLPlusCCancellationTokenSource.CreateWithTimeouts(
            console,
            logger: null,
            gracePeriod: Timeout.InfiniteTimeSpan,
            abortTimeout: Timeout.InfiniteTimeSpan);

        ConsoleCancelEventArgs first = console.FireCancelKeyPress();
        Assert.IsTrue(first.Cancel);
        Assert.IsTrue(source.DrainingToken.IsCancellationRequested);
        Assert.IsFalse(source.AbortingToken.IsCancellationRequested);
        Assert.IsTrue(source.WasCancellationRequestedByConsole);

        ConsoleCancelEventArgs second = console.FireCancelKeyPress();
        Assert.IsTrue(second.Cancel);
        Assert.IsTrue(source.AbortingToken.IsCancellationRequested);

        ConsoleCancelEventArgs third = console.FireCancelKeyPress();
        Assert.IsFalse(third.Cancel);
    }

    [TestMethod]
    public async Task AbortTimeout_FailFastMessageMentionsAbortTimeout()
    {
        var environment = new RecordingEnvironment(TestContext.CancellationToken);
        using var source = CTRLPlusCCancellationTokenSource.CreateWithTimeouts(
            console: null,
            logger: null,
            gracePeriod: Timeout.InfiniteTimeSpan,
            abortTimeout: TimeSpan.FromMilliseconds(10),
            environment: environment);

        source.Abort();

        string? message = await environment.WaitForFailFastAsync();
        Assert.AreEqual("Test platform shutdown abort timeout exhausted.", message);
    }

    [TestMethod]
    public async Task AbortTimeout_InvokesForceExitActionBeforeTerminatingHost()
    {
        var environment = new RecordingEnvironment(TestContext.CancellationToken);
        using var source = CTRLPlusCCancellationTokenSource.CreateWithTimeouts(
            console: null,
            logger: null,
            gracePeriod: Timeout.InfiniteTimeSpan,
            abortTimeout: TimeSpan.FromMilliseconds(10),
            environment: environment);
        bool forceExitActionInvoked = false;
        using IDisposable registration = source.RegisterForceExitAction(() =>
        {
            Assert.IsFalse(environment.FailFastCalled, "The child must be terminated before the controller.");
            forceExitActionInvoked = true;
        });

        source.Abort();
        await environment.WaitForFailFastAsync();

        Assert.IsTrue(forceExitActionInvoked);
    }

    [TestMethod]
    public void SecondCtrlC_SignalsAbortingWithoutInvokingForceExitAction()
    {
        var console = new TrackingConsole();
        var environment = new RecordingEnvironment(TestContext.CancellationToken);
        using var source = CTRLPlusCCancellationTokenSource.CreateWithTimeouts(
            console, logger: null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan, environment);
        bool forceExitActionInvoked = false;
        using IDisposable registration = source.RegisterForceExitAction(() => forceExitActionInvoked = true);

        console.FireCancelKeyPress();
        ConsoleCancelEventArgs second = console.FireCancelKeyPress();

        Assert.IsTrue(second.Cancel);
        Assert.IsTrue(source.DrainingToken.IsCancellationRequested);
        Assert.IsTrue(source.AbortingToken.IsCancellationRequested);
        Assert.IsFalse(forceExitActionInvoked);
        Assert.IsFalse(environment.FailFastCalled);
        Assert.AreEqual(0, environment.ExitCallCount);
    }

    [TestMethod]
    public void DisposedForceExitRegistration_DoesNotInvokeAction()
    {
        var console = new TrackingConsole();
        using var source = CTRLPlusCCancellationTokenSource.CreateWithTimeouts(
            console, logger: null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        bool forceExitActionInvoked = false;
        source.RegisterForceExitAction(() => forceExitActionInvoked = true).Dispose();

        console.FireCancelKeyPress();
        console.FireCancelKeyPress();
        ConsoleCancelEventArgs third = console.FireCancelKeyPress();

        Assert.IsFalse(forceExitActionInvoked);
        Assert.IsFalse(third.Cancel);
    }

    [TestMethod]
    public void CtrlC_AfterExternalCancel_StillRequiresTwoPressesToAbort()
    {
        var console = new TrackingConsole();
        using var source = CTRLPlusCCancellationTokenSource.CreateWithTimeouts(
            console, logger: null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);

        source.Cancel();
        Assert.IsFalse(source.WasCancellationRequestedByConsole);

        ConsoleCancelEventArgs first = console.FireCancelKeyPress();
        Assert.IsTrue(first.Cancel);
        Assert.IsFalse(source.AbortingToken.IsCancellationRequested);

        ConsoleCancelEventArgs second = console.FireCancelKeyPress();
        Assert.IsTrue(second.Cancel);
        Assert.IsTrue(source.AbortingToken.IsCancellationRequested);
    }

    [TestMethod]
    public void RepeatedCtrlC_InvokesForceExitActionOnlyOnce()
    {
        var console = new TrackingConsole();
        using var source = CTRLPlusCCancellationTokenSource.CreateWithTimeouts(
            console, logger: null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        int forceExitActionInvocations = 0;
        using IDisposable registration = source.RegisterForceExitAction(() => forceExitActionInvocations++);

        console.FireCancelKeyPress();
        console.FireCancelKeyPress();
        ConsoleCancelEventArgs third = console.FireCancelKeyPress();
        ConsoleCancelEventArgs fourth = console.FireCancelKeyPress();

        Assert.AreEqual(1, forceExitActionInvocations);
        Assert.IsFalse(third.Cancel);
        Assert.IsFalse(fourth.Cancel);
    }

    [TestMethod]
    public void FirstCtrlC_WhenCancelCallbackThrows_LogsWarningAndSuppressesException()
    {
        var console = new TrackingConsole();
        var logger = new RecordingLogger();
        using var source = CTRLPlusCCancellationTokenSource.CreateWithTimeouts(
            console, logger, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        using CancellationTokenRegistration registration = source.DrainingToken.Register(() => throw new InvalidOperationException("boom"));

        console.FireCancelKeyPress();

        Assert.IsTrue(source.DrainingToken.IsCancellationRequested);
        Assert.IsFalse(source.AbortingToken.IsCancellationRequested);
        Assert.AreEqual(1, logger.WarningCount);
        Assert.IsNotNull(logger.LastWarning);
        Assert.Contains("Exception during shutdown (Draining)", logger.LastWarning);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void ExternalCancellation_WhenCallbackThrows_LogsWarningAndSuppressesException(bool abort)
    {
        var logger = new RecordingLogger();
        using var source = CTRLPlusCCancellationTokenSource.CreateWithTimeouts(
            console: null, logger, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        CancellationToken token = abort ? source.AbortingToken : source.DrainingToken;
        using CancellationTokenRegistration registration = token.Register(() => throw new InvalidOperationException("boom"));

        if (abort)
        {
            source.Abort();
        }
        else
        {
            source.Cancel();
        }

        Assert.IsTrue(token.IsCancellationRequested);
        Assert.AreEqual(1, logger.WarningCount);
        Assert.IsNotNull(logger.LastWarning);
        Assert.Contains(abort ? "Exception during shutdown (Aborting)" : "Exception during shutdown (Draining)", logger.LastWarning);
    }

    [TestMethod]
    public void ConcurrentCtrlC_SignalsEachPhaseOnlyOnce()
    {
        var console = new TrackingConsole();
        using var source = CTRLPlusCCancellationTokenSource.CreateWithTimeouts(
            console, logger: null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        int drainingCallbacks = 0;
        int abortingCallbacks = 0;
        int forceExitActions = 0;
        using CancellationTokenRegistration drainingRegistration = source.DrainingToken.Register(() => Interlocked.Increment(ref drainingCallbacks));
        using CancellationTokenRegistration abortingRegistration = source.AbortingToken.Register(() => Interlocked.Increment(ref abortingCallbacks));
        using IDisposable forceExitRegistration = source.RegisterForceExitAction(() => Interlocked.Increment(ref forceExitActions));

        Parallel.For(0, 16, _ => console.FireCancelKeyPress());

        Assert.IsTrue(source.DrainingToken.IsCancellationRequested);
        Assert.IsTrue(source.AbortingToken.IsCancellationRequested);
        Assert.AreEqual(1, drainingCallbacks);
        Assert.AreEqual(1, abortingCallbacks);
        Assert.AreEqual(1, forceExitActions);
    }

    [TestMethod]
    public void Constructor_WithNullConsole_DoesNotCrash_AndCancelStillWorks()
    {
        var environment = new RecordingEnvironment(TestContext.CancellationToken);
        using var source = new CTRLPlusCCancellationTokenSource(console: null, logger: null, environment);

        source.Cancel();

        Assert.IsTrue(source.CancellationToken.IsCancellationRequested);
        Assert.IsFalse(source.WasCancellationRequestedByConsole);
        Assert.IsFalse(environment.FailFastCalled);
        Assert.AreEqual(0, environment.ExitCallCount);
    }

    [TestMethod]
    public void ThirdCtrlC_ForceActionFailureWarnsAndStillLetsRuntimeTerminate()
    {
        var console = new TrackingConsole();
        var logger = new RecordingLogger();
        using var source = CTRLPlusCCancellationTokenSource.CreateWithTimeouts(
            console, logger, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);
        int invoked = 0;
        using IDisposable registration = source.RegisterForceExitAction(() =>
        {
            invoked++;
            throw new InvalidOperationException("force action failed");
        });

        console.FireCancelKeyPress();
        console.FireCancelKeyPress();
        ConsoleCancelEventArgs third = console.FireCancelKeyPress();
        ConsoleCancelEventArgs fourth = console.FireCancelKeyPress();

        Assert.AreEqual(1, invoked);
        Assert.IsFalse(third.Cancel);
        Assert.IsFalse(fourth.Cancel);
        Assert.AreEqual(1, logger.WarningCount);
        Assert.IsNotNull(logger.LastWarning);
        Assert.StartsWith("Exception while force-terminating the active test host:\n", logger.LastWarning);
        Assert.Contains("System.InvalidOperationException: force action failed", logger.LastWarning);
    }

    [TestMethod]
    public void CancelAfter_DisabledTimerDoesNotCancelAndDisposeIsIdempotent()
    {
        var console = new TrackingConsole();
        var environment = new RecordingEnvironment(TestContext.CancellationToken);
        var source = new CTRLPlusCCancellationTokenSource(console, logger: null, environment);

        source.CancelAfter(Timeout.InfiniteTimeSpan);
        Assert.IsFalse(source.CancellationToken.IsCancellationRequested);
        source.Cancel();
        Assert.IsTrue(source.CancellationToken.IsCancellationRequested);
        source.Dispose();
        source.Dispose();

        Assert.AreEqual(0, console.CancelKeyPressSubscriberCount);
        console.FireCancelKeyPress();
        Assert.AreEqual(0, environment.ExitCallCount);
        Assert.IsFalse(environment.FailFastCalled);
    }

    [TestMethod]
    [DataRow(false, DisplayName = "Queued abort callback after disposal")]
    [DataRow(true, DisplayName = "Disposal during abort callback logging")]
    public void QueuedAbortTimeout_WhenSourceIsDisposed_DoesNotTerminate(bool disposeDuringLogging)
    {
        var environment = new RecordingEnvironment(TestContext.CancellationToken);
        var logger = new RecordingLogger();
        using var source = CTRLPlusCCancellationTokenSource.CreateWithTimeouts(
            console: null, logger, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan, environment);
        int forceExitActions = 0;
        using IDisposable registration = source.RegisterForceExitAction(() => forceExitActions++);
        if (disposeDuringLogging)
        {
            logger.OnWarning = source.Dispose;
        }
        else
        {
            source.Dispose();
        }

        // Invoke the timer's callback target directly so the disposal ordering is deterministic.
        MethodInfo? callback = typeof(CTRLPlusCCancellationTokenSource).GetMethod("ForceTerminate", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(callback);
        callback.Invoke(source, parameters: null);

        Assert.AreEqual(0, forceExitActions);
        Assert.IsFalse(environment.FailFastCalled);
    }

    [TestMethod]
    public void Dispose_FromDrainingCallback_DoesNotEscalateAfterDisposal()
    {
        using var source = CTRLPlusCCancellationTokenSource.CreateWithTimeouts(
            console: null, logger: null, TimeSpan.Zero, Timeout.InfiniteTimeSpan);
        CancellationToken draining = source.DrainingToken;
        CancellationToken aborting = source.AbortingToken;
        using CancellationTokenRegistration registration = draining.Register(source.Dispose);

        source.Cancel();

        Assert.IsTrue(draining.IsCancellationRequested);
        Assert.IsFalse(aborting.IsCancellationRequested);
    }

    [TestMethod]
    [DataRow(false, DisplayName = "Queued abort callback after shutdown completion")]
    [DataRow(true, DisplayName = "Shutdown completes during abort callback logging")]
    public void QueuedAbortTimeout_WhenShutdownCompleted_DoesNotTerminate(bool completeDuringLogging)
    {
        var console = new TrackingConsole();
        var environment = new RecordingEnvironment(TestContext.CancellationToken);
        var logger = new RecordingLogger();
        using var source = CTRLPlusCCancellationTokenSource.CreateWithTimeouts(
            console, logger, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan, environment);
        int forceExitActions = 0;
        using IDisposable registration = source.RegisterForceExitAction(() => forceExitActions++);
        if (completeDuringLogging)
        {
            logger.OnWarning = source.CompleteShutdown;
        }
        else
        {
            source.CompleteShutdown();
        }

        MethodInfo? callback = typeof(CTRLPlusCCancellationTokenSource).GetMethod("ForceTerminate", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(callback);
        callback.Invoke(source, parameters: null);
        source.CompleteShutdown();
        source.Abort();
        console.FireCancelKeyPress();

        Assert.IsFalse(source.DrainingToken.IsCancellationRequested);
        Assert.IsFalse(source.AbortingToken.IsCancellationRequested);
        Assert.IsFalse(source.WasCancellationRequestedByConsole);
        Assert.AreEqual(0, console.CancelKeyPressSubscriberCount);
        Assert.AreEqual(0, forceExitActions);
        Assert.IsFalse(environment.FailFastCalled);
    }

    [TestMethod]
    public void CompleteShutdown_FromDrainingCallback_DoesNotEscalateOrDisposeTokens()
    {
        using var source = CTRLPlusCCancellationTokenSource.CreateWithTimeouts(
            console: null, logger: null, TimeSpan.Zero, Timeout.InfiniteTimeSpan);
        using CancellationTokenRegistration registration = source.DrainingToken.Register(source.CompleteShutdown);

        source.Cancel();

        Assert.IsTrue(source.CancellationToken.IsCancellationRequested);
        Assert.IsTrue(source.DrainingToken.IsCancellationRequested);
        Assert.IsFalse(source.AbortingToken.IsCancellationRequested);
    }

    private async Task WaitForCancellationAsync(CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return;
        }

        var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        using CancellationTokenRegistration registration = cancellationToken.Register(() => tcs.TrySetResult(true));

        var timeoutTask = Task.Delay(TimeSpan.FromSeconds(5), TestContext.CancellationToken);
        Task completed = await Task.WhenAny(tcs.Task, timeoutTask);

        Assert.AreSame(tcs.Task, completed, "Token must trip before the timeout.");
    }

    private sealed class TrackingConsole : IConsole
    {
        private ConsoleCancelEventHandler? _cancelKeyPress;

        public event ConsoleCancelEventHandler? CancelKeyPress
        {
            add => _cancelKeyPress += value;
            remove => _cancelKeyPress -= value;
        }

        public int CancelKeyPressSubscriberCount => _cancelKeyPress?.GetInvocationList().Length ?? 0;

        public ConsoleCancelEventArgs FireCancelKeyPress()
        {
            ConstructorInfo? constructor = typeof(ConsoleCancelEventArgs).GetConstructor(
                BindingFlags.Instance | BindingFlags.NonPublic,
                binder: null,
                types: [typeof(ConsoleSpecialKey)],
                modifiers: null);
            Assert.IsNotNull(constructor);
            var args = (ConsoleCancelEventArgs)constructor.Invoke([ConsoleSpecialKey.ControlC]);
            _cancelKeyPress?.Invoke(this, args);
            return args;
        }

        public int BufferHeight => throw new NotImplementedException();

        public int BufferWidth => throw new NotImplementedException();

        public int WindowHeight => throw new NotImplementedException();

        public int WindowWidth => throw new NotImplementedException();

        public bool IsOutputRedirected => throw new NotImplementedException();

        public void SetForegroundColor(ConsoleColor color) => throw new NotImplementedException();

        public ConsoleColor GetForegroundColor() => throw new NotImplementedException();

        public void WriteLine() => throw new NotImplementedException();

        public void WriteLine(string? value) => throw new NotImplementedException();

        public void Write(string? value) => throw new NotImplementedException();

        public void Write(StringBuilder value) => throw new NotImplementedException();

        public void Write(char value) => throw new NotImplementedException();

        public void Clear() => throw new NotImplementedException();
    }

    private sealed class RecordingEnvironment(CancellationToken cancellationToken) : IEnvironment
    {
        private readonly TaskCompletionSource<string?> _failFastMessage = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public bool FailFastCalled => _failFastMessage.Task.IsCompleted;

        public int ExitCallCount { get; private set; }

        public string CommandLine => throw new NotImplementedException();

        public string MachineName => throw new NotImplementedException();

        public string NewLine => Environment.NewLine;

        public int ProcessId => throw new NotImplementedException();

        public string OsVersion => throw new NotImplementedException();

#if NETCOREAPP
        public string? ProcessPath => throw new NotImplementedException();
#endif

        public async Task<string?> WaitForFailFastAsync()
        {
            var timeoutTask = Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
            Task completed = await Task.WhenAny(_failFastMessage.Task, timeoutTask);

            Assert.AreSame(_failFastMessage.Task, completed, "FailFast must be called before the timeout.");

            return await _failFastMessage.Task;
        }

        public string[] GetCommandLineArgs() => throw new NotImplementedException();

        public string? GetEnvironmentVariable(string name) => throw new NotImplementedException();

        public IDictionary GetEnvironmentVariables() => throw new NotImplementedException();

        public string GetFolderPath(Environment.SpecialFolder folder, Environment.SpecialFolderOption option) => throw new NotImplementedException();

        public void FailFast(string? message, Exception? exception) => _failFastMessage.TrySetResult(message);

        public void FailFast(string? message) => _failFastMessage.TrySetResult(message);

        public void SetEnvironmentVariable(string variable, string? value) => throw new NotImplementedException();

        public void Exit(int exitCode) => ExitCallCount++;
    }

    private sealed class RecordingLogger : ILogger
    {
        private int _warningCount;

        public int WarningCount => _warningCount;

        public string? LastWarning { get; private set; }

        public Action? OnWarning { get; set; }

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (logLevel == LogLevel.Warning)
            {
                Interlocked.Increment(ref _warningCount);
                LastWarning = formatter(state, exception);
                OnWarning?.Invoke();
            }
        }

        public Task LogAsync<TState>(LogLevel logLevel, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            Log(logLevel, state, exception, formatter);
            return Task.CompletedTask;
        }
    }
}
