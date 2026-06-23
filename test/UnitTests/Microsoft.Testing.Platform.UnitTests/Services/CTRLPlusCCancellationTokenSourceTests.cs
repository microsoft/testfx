// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Logging;
using Microsoft.Testing.Platform.Services;

namespace Microsoft.Testing.Platform.UnitTests;

[TestClass]
public sealed class CTRLPlusCCancellationTokenSourceTests
{
    [TestMethod]
    public void FirstCtrlC_CancelsToken_AndDoesNotExitProcess()
    {
        var console = new CancelableConsole();
        var environment = new RecordingEnvironment();
        using var source = new CTRLPlusCCancellationTokenSource(console, logger: null, environment);

        console.FireCancelKeyPress();

        Assert.IsTrue(source.CancellationToken.IsCancellationRequested);
        Assert.IsNull(environment.ExitCode, "Environment.Exit must not be called on the first Ctrl+C press.");
    }

    [TestMethod]
    public void SecondCtrlC_TriggersForceExit_WithTestSessionAbortedExitCode()
    {
        var console = new CancelableConsole();
        var environment = new RecordingEnvironment();
        using var source = new CTRLPlusCCancellationTokenSource(console, logger: null, environment);

        console.FireCancelKeyPress();
        console.FireCancelKeyPress();

        Assert.IsTrue(source.CancellationToken.IsCancellationRequested);
        Assert.AreEqual((int)ExitCode.TestSessionAborted, environment.ExitCode);
    }

    [TestMethod]
    public void CtrlC_AfterExternalCancel_DoesNotForceExit_ButSecondCtrlCDoes()
    {
        var console = new CancelableConsole();
        var environment = new RecordingEnvironment();
        using var source = new CTRLPlusCCancellationTokenSource(console, logger: null, environment);

        // Simulate cancellation from another source (timeout, max-failed-tests, etc.).
        source.Cancel();

        // The user has not yet seen the "Press Ctrl+C again to force exit." hint, so the first
        // user Ctrl+C must not force-exit — it should be treated as the first cooperative press.
        console.FireCancelKeyPress();
        Assert.IsNull(environment.ExitCode, "First user Ctrl+C must not force-exit even when cancellation was already requested externally.");

        // The second user Ctrl+C should then force-exit.
        console.FireCancelKeyPress();
        Assert.AreEqual((int)ExitCode.TestSessionAborted, environment.ExitCode);
    }

    [TestMethod]
    public void RepeatedCtrlC_AfterForceExit_DoesNotCallExitAgain()
    {
        var console = new CancelableConsole();
        var environment = new RecordingEnvironment();
        using var source = new CTRLPlusCCancellationTokenSource(console, logger: null, environment);

        console.FireCancelKeyPress();
        console.FireCancelKeyPress();
        console.FireCancelKeyPress();
        console.FireCancelKeyPress();

        Assert.AreEqual(1, environment.ExitCallCount);
        Assert.AreEqual((int)ExitCode.TestSessionAborted, environment.ExitCode);
    }

    [TestMethod]
    public void FirstCtrlC_WhenCancelCallbackThrows_LogsWarningAndSuppressesException()
    {
        var console = new CancelableConsole();
        var environment = new RecordingEnvironment();
        var logger = new RecordingLogger();
        using var source = new CTRLPlusCCancellationTokenSource(console, logger, environment);

        // Registering a throwing callback on the token causes CancellationTokenSource.Cancel()
        // to throw AggregateException, exercising the catch/log path in OnConsoleCancelKeyPressed.
        source.CancellationToken.Register(() => throw new InvalidOperationException("boom"));

        console.FireCancelKeyPress();

        Assert.IsTrue(source.CancellationToken.IsCancellationRequested);
        Assert.IsNull(environment.ExitCode, "First Ctrl+C must not force-exit even when the cancel callback throws.");
        Assert.AreEqual(1, logger.WarningCount, "The AggregateException must be logged as a warning.");
        Assert.IsNotNull(logger.LastWarning);
        Assert.Contains("CTRLPlusCCancellationTokenSource cancel", logger.LastWarning!);
    }

    [TestMethod]
    public void ConcurrentFirstCtrlC_OnlyTransitionsStateOnce_AndAllPressesCombinedNeverExitMoreThanOnce()
    {
        var console = new CancelableConsole();
        var environment = new RecordingEnvironment();
        using var source = new CTRLPlusCCancellationTokenSource(console, logger: null, environment);

        // Registering a callback on the token lets us count how many times the underlying
        // CancellationTokenSource.Cancel() was actually invoked. The state machine guarantees
        // only the first thread that wins the StateIdle -> StateCancelling transition calls
        // Cancel(), so the callback must fire exactly once even under heavy contention.
        int cancelCallbackInvocations = 0;
        source.CancellationToken.Register(() => Interlocked.Increment(ref cancelCallbackInvocations));

        Parallel.For(0, 16, _ => console.FireCancelKeyPress());

        Assert.IsTrue(source.CancellationToken.IsCancellationRequested);
        Assert.AreEqual(1, cancelCallbackInvocations, "Only the thread that wins the state transition must call Cancel().");
        Assert.AreEqual(1, environment.ExitCallCount, "Across many concurrent presses Exit() must be called at most once.");
    }

    [TestMethod]
    public void Constructor_WithNullConsole_DoesNotCrash_AndCancelStillWorks()
    {
        var environment = new RecordingEnvironment();
        using var source = new CTRLPlusCCancellationTokenSource(console: null, logger: null, environment);

        source.Cancel();

        Assert.IsTrue(source.CancellationToken.IsCancellationRequested);
        Assert.IsNull(environment.ExitCode, "Without a console there is no Ctrl+C handler and Exit must never be called.");
    }

    [TestMethod]
    public void Dispose_UnsubscribesCancelKeyPressHandler()
    {
        var console = new CancelableConsole();
        var environment = new RecordingEnvironment();
        var source = new CTRLPlusCCancellationTokenSource(console, logger: null, environment);

        source.Dispose();

        // After disposal the handler must be detached so a late Ctrl+C cannot touch the disposed
        // CancellationTokenSource (which would throw ObjectDisposedException).
        Assert.IsFalse(console.HasCancelKeyPressSubscribers);

        // Firing the event after dispose must be a no-op.
        console.FireCancelKeyPress();
        Assert.IsNull(environment.ExitCode);
    }

    private sealed class CancelableConsole : IConsole
    {
        public event ConsoleCancelEventHandler? CancelKeyPress;

        public int BufferHeight => int.MaxValue;

        public int BufferWidth => int.MaxValue;

        public int WindowHeight => int.MaxValue;

        public int WindowWidth => int.MaxValue;

        public bool IsOutputRedirected => false;

        public bool HasCancelKeyPressSubscribers => CancelKeyPress is not null;

        public void FireCancelKeyPress()
        {
            ConsoleCancelEventHandler? handler = CancelKeyPress;
            handler?.Invoke(this, CreateConsoleCancelEventArgs());
        }

        public void Clear() => throw new NotImplementedException();

        public ConsoleColor GetForegroundColor() => ConsoleColor.White;

        public void SetForegroundColor(ConsoleColor color)
        {
            // do nothing
        }

        public void Write(string? value)
        {
            // do nothing
        }

        public void Write(char value)
        {
            // do nothing
        }

        public void WriteLine()
        {
            // do nothing
        }

        public void WriteLine(string? value)
        {
            // do nothing
        }

        // ConsoleCancelEventArgs has no public constructor; use reflection to instantiate it
        // for the purposes of the test.
        private static ConsoleCancelEventArgs CreateConsoleCancelEventArgs()
        {
            ConstructorInfo? constructor = typeof(ConsoleCancelEventArgs).GetConstructor(
                BindingFlags.Instance | BindingFlags.NonPublic,
                binder: null,
                types: [typeof(ConsoleSpecialKey)],
                modifiers: null);

            Assert.IsNotNull(constructor, "Failed to locate internal ConsoleCancelEventArgs constructor.");
            return (ConsoleCancelEventArgs)constructor.Invoke([ConsoleSpecialKey.ControlC]);
        }
    }

    private sealed class RecordingEnvironment : IEnvironment
    {
        public int? ExitCode { get; private set; }

        public int ExitCallCount { get; private set; }

        public string CommandLine => string.Empty;

        public string MachineName => string.Empty;

        public string NewLine => Environment.NewLine;

        public int ProcessId => 0;

        public string OsVersion => string.Empty;

#if NETCOREAPP
        public string? ProcessPath => null;
#endif

        public string[] GetCommandLineArgs() => [];

        public string? GetEnvironmentVariable(string name) => null;

        public IDictionary GetEnvironmentVariables() => new Dictionary<string, string>();

        public string GetFolderPath(Environment.SpecialFolder folder, Environment.SpecialFolderOption option) => string.Empty;

        public void FailFast(string? message, Exception? exception)
        {
            // do nothing
        }

        public void FailFast(string? message)
        {
            // do nothing
        }

        public void SetEnvironmentVariable(string variable, string? value)
        {
            // do nothing
        }

        public void Exit(int exitCode)
        {
            ExitCode = exitCode;
            ExitCallCount++;

            // The real implementation never returns; ours does, so subsequent presses still
            // observe ExitCallCount accurately for the test.
        }
    }

    private sealed class RecordingLogger : ILogger
    {
        private int _warningCount;

        public int WarningCount => _warningCount;

        public string? LastWarning { get; private set; }

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (logLevel == LogLevel.Warning)
            {
                Interlocked.Increment(ref _warningCount);
                LastWarning = formatter(state, exception);
            }
        }

        public Task LogAsync<TState>(LogLevel logLevel, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            Log(logLevel, state, exception, formatter);
            return Task.CompletedTask;
        }
    }
}
