// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Helpers;
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
    public void CtrlC_WhileCancellationAlreadyTriggeredExternally_TriggersForceExit()
    {
        var console = new CancelableConsole();
        var environment = new RecordingEnvironment();
        using var source = new CTRLPlusCCancellationTokenSource(console, logger: null, environment);

        // Simulate cancellation from another source (timeout, max-failed-tests, etc.).
        source.Cancel();

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

    private sealed class CancelableConsole : IConsole
    {
        public event ConsoleCancelEventHandler? CancelKeyPress;

        public int BufferHeight => int.MaxValue;

        public int BufferWidth => int.MaxValue;

        public int WindowHeight => int.MaxValue;

        public int WindowWidth => int.MaxValue;

        public bool IsOutputRedirected => false;

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
}
