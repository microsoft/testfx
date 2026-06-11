// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Services;

namespace Microsoft.Testing.Platform.UnitTests;

[TestClass]
public sealed class CTRLPlusCCancellationTokenSourceTests
{
    [TestMethod]
    public void Initial_State_NeitherTokenIsCancelled()
    {
        using var source = new CTRLPlusCCancellationTokenSource(
            console: null,
            logger: null,
            gracePeriod: Timeout.InfiniteTimeSpan,
            abortTimeout: Timeout.InfiniteTimeSpan);

        Assert.IsFalse(source.CancellationToken.IsCancellationRequested);
        Assert.IsFalse(source.DrainingToken.IsCancellationRequested);
        Assert.IsFalse(source.AbortingToken.IsCancellationRequested);
    }

    [TestMethod]
    public void Cancel_OnlySignalsDrainingToken()
    {
        using var source = new CTRLPlusCCancellationTokenSource(
            console: null,
            logger: null,
            gracePeriod: Timeout.InfiniteTimeSpan,
            abortTimeout: Timeout.InfiniteTimeSpan);

        source.Cancel();

        Assert.IsTrue(source.DrainingToken.IsCancellationRequested);
        Assert.IsTrue(source.CancellationToken.IsCancellationRequested, "Legacy alias must follow DrainingToken.");
        Assert.IsFalse(source.AbortingToken.IsCancellationRequested);
    }

    [TestMethod]
    public void Abort_SignalsBothTokens()
    {
        using var source = new CTRLPlusCCancellationTokenSource(
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
        using var source = new CTRLPlusCCancellationTokenSource(
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
        using var source = new CTRLPlusCCancellationTokenSource(
            console: null,
            logger: null,
            gracePeriod: TimeSpan.FromMilliseconds(50),
            abortTimeout: Timeout.InfiniteTimeSpan);

        source.Cancel();
        Assert.IsTrue(source.DrainingToken.IsCancellationRequested);
        Assert.IsFalse(source.AbortingToken.IsCancellationRequested);

        await WaitForCancellationAsync(source.AbortingToken).ConfigureAwait(false);

        Assert.IsTrue(source.AbortingToken.IsCancellationRequested, "Aborting must trip after the grace period.");
    }

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task CancelAfter_EntersDrainingThenEscalatesToAborting()
    {
        using var source = new CTRLPlusCCancellationTokenSource(
            console: null,
            logger: null,
            gracePeriod: TimeSpan.FromMilliseconds(50),
            abortTimeout: Timeout.InfiniteTimeSpan);

        source.CancelAfter(TimeSpan.FromMilliseconds(10));

        await WaitForCancellationAsync(source.DrainingToken).ConfigureAwait(false);
        await WaitForCancellationAsync(source.AbortingToken).ConfigureAwait(false);

        Assert.IsTrue(source.DrainingToken.IsCancellationRequested);
        Assert.IsTrue(source.AbortingToken.IsCancellationRequested);
    }

    [TestMethod]
    public void ZeroGracePeriod_ImmediatelyEscalatesToAborting()
    {
        using var source = new CTRLPlusCCancellationTokenSource(
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
            using var source = new CTRLPlusCCancellationTokenSource(
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
        using var source = new CTRLPlusCCancellationTokenSource(
            console,
            logger: null,
            gracePeriod: Timeout.InfiniteTimeSpan,
            abortTimeout: Timeout.InfiniteTimeSpan);

        ConsoleCancelEventArgs first = CreateCancelKeyPressEventArgs();
        console.RaiseCancelKeyPress(first);
        Assert.IsTrue(first.Cancel);
        Assert.IsTrue(source.DrainingToken.IsCancellationRequested);
        Assert.IsFalse(source.AbortingToken.IsCancellationRequested);

        ConsoleCancelEventArgs second = CreateCancelKeyPressEventArgs();
        console.RaiseCancelKeyPress(second);
        Assert.IsTrue(second.Cancel);
        Assert.IsTrue(source.AbortingToken.IsCancellationRequested);

        ConsoleCancelEventArgs third = CreateCancelKeyPressEventArgs();
        console.RaiseCancelKeyPress(third);
        Assert.IsFalse(third.Cancel);
    }

    [TestMethod]
    public async Task AbortTimeout_FailFastMessageMentionsAbortTimeout()
    {
        var environment = new RecordingEnvironment(TestContext.CancellationToken);
        using var source = new CTRLPlusCCancellationTokenSource(
            console: null,
            logger: null,
            gracePeriod: Timeout.InfiniteTimeSpan,
            abortTimeout: TimeSpan.FromMilliseconds(10),
            environment: environment);

        source.Abort();

        string? message = await environment.WaitForFailFastAsync().ConfigureAwait(false);
        Assert.AreEqual("Test platform shutdown abort timeout exhausted.", message);
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
        Task completed = await Task.WhenAny(tcs.Task, timeoutTask).ConfigureAwait(false);

        Assert.AreSame(tcs.Task, completed, "Token must trip before the timeout.");
    }

    private static ConsoleCancelEventArgs CreateCancelKeyPressEventArgs()
    {
        ConstructorInfo constructor = typeof(ConsoleCancelEventArgs).GetConstructor(
            BindingFlags.Instance | BindingFlags.NonPublic,
            binder: null,
            [typeof(ConsoleSpecialKey)],
            modifiers: null)!;

        return (ConsoleCancelEventArgs)constructor.Invoke([ConsoleSpecialKey.ControlC]);
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

        public void RaiseCancelKeyPress(ConsoleCancelEventArgs args) => _cancelKeyPress?.Invoke(this, args);

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

        public void Write(char value) => throw new NotImplementedException();

        public void Clear() => throw new NotImplementedException();
    }

    private sealed class RecordingEnvironment(CancellationToken cancellationToken) : IEnvironment
    {
        private readonly TaskCompletionSource<string?> _failFastMessage = new(TaskCreationOptions.RunContinuationsAsynchronously);

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
            Task completed = await Task.WhenAny(_failFastMessage.Task, timeoutTask).ConfigureAwait(false);

            Assert.AreSame(_failFastMessage.Task, completed, "FailFast must be called before the timeout.");

            return await _failFastMessage.Task.ConfigureAwait(false);
        }

        public string[] GetCommandLineArgs() => throw new NotImplementedException();

        public string? GetEnvironmentVariable(string name) => throw new NotImplementedException();

        public IDictionary GetEnvironmentVariables() => throw new NotImplementedException();

        public string GetFolderPath(Environment.SpecialFolder folder, Environment.SpecialFolderOption option) => throw new NotImplementedException();

        public void FailFast(string? message, Exception? exception) => _failFastMessage.TrySetResult(message);

        public void FailFast(string? message) => _failFastMessage.TrySetResult(message);

        public void SetEnvironmentVariable(string variable, string? value) => throw new NotImplementedException();

        public void Exit(int exitCode) => throw new NotImplementedException();
    }
}
