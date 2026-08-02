// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions.TestHostOrchestrator;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Hosts;
using Microsoft.Testing.Platform.Logging;
using Microsoft.Testing.Platform.Services;
using Microsoft.Testing.Platform.TestHostOrchestrator;

namespace Microsoft.Testing.Platform.UnitTests;

/// <summary>
/// Covers the orchestrator host's application-lifetime contract: whatever <c>BeforeRunAsync</c> acquired
/// must be released on every exit path, including cancellation and failure. See
/// <see href="https://github.com/microsoft/testfx/issues/10360"/> — an Azure DevOps test run created there
/// and never closed stays "InProgress" and never appears in the build's Tests tab.
/// </summary>
[TestClass]
public sealed class TestHostOrchestratorHostTests
{
    [TestMethod]
    public async Task RunAsync_OrchestratorSucceeds_CallsAfterRunWithTheOrchestratorExitCode()
    {
        RecordingLifetime lifetime = new();
        TestHostOrchestratorHost host = CreateHost(new RecordingOrchestrator(exitCode: 7), lifetime, out _);

        int exitCode = await host.RunAsync();

        Assert.AreEqual(7, exitCode);
        Assert.AreEqual(1, lifetime.BeforeRunCount);
        Assert.AreEqual(1, lifetime.AfterRunCount);
        Assert.AreEqual(7, lifetime.LastExitCode);
        Assert.AreEqual(1, lifetime.DisposeCount);
    }

    [TestMethod]
    public async Task RunAsync_OrchestratorCanceled_StillCallsAfterRunAndDisposes()
    {
        RecordingLifetime lifetime = new();
        TestHostOrchestratorHost host = CreateHost(
            new RecordingOrchestrator(onOrchestrate: cancellationTokenSource =>
            {
                cancellationTokenSource.Cancel();
                throw new OperationCanceledException();
            }),
            lifetime,
            out _);

        int exitCode = await host.RunAsync();

        Assert.AreEqual((int)ExitCode.TestSessionAborted, exitCode);
        Assert.AreEqual(1, lifetime.BeforeRunCount);
        Assert.AreEqual(1, lifetime.AfterRunCount);
        Assert.AreEqual((int)ExitCode.TestSessionAborted, lifetime.LastExitCode);
        Assert.AreEqual(1, lifetime.DisposeCount);
    }

    [TestMethod]
    public async Task RunAsync_OrchestratorThrows_CallsAfterRunAndDisposesThenRethrows()
    {
        RecordingLifetime lifetime = new();
        TestHostOrchestratorHost host = CreateHost(
            new RecordingOrchestrator(onOrchestrate: _ => throw new InvalidOperationException("orchestrator exploded")),
            lifetime,
            out _);

        InvalidOperationException exception = await Assert.ThrowsExactlyAsync<InvalidOperationException>(host.RunAsync);

        Assert.AreEqual("orchestrator exploded", exception.Message);
        Assert.AreEqual(1, lifetime.BeforeRunCount);
        Assert.AreEqual(1, lifetime.AfterRunCount);
        Assert.AreEqual((int)ExitCode.GenericFailure, lifetime.LastExitCode);
        Assert.AreEqual(1, lifetime.DisposeCount);
    }

    [TestMethod]
    public async Task RunAsync_LifetimeBeforeRunThrows_StillCallsAfterRunOnEveryLifetime()
    {
        // A lifetime whose BeforeRunAsync faulted, and one whose BeforeRunAsync never ran, both still get
        // AfterRunAsync so neither can strand a resource it may have partially acquired.
        RecordingLifetime faulting = new(beforeRunException: new InvalidOperationException("before-run exploded"));
        RecordingLifetime never = new();
        TestHostOrchestratorHost host = CreateHost(new RecordingOrchestrator(exitCode: 0), [faulting, never], out _);

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(host.RunAsync);

        Assert.AreEqual(1, faulting.BeforeRunCount);
        Assert.AreEqual(1, faulting.AfterRunCount);
        Assert.AreEqual((int)ExitCode.GenericFailure, faulting.LastExitCode);
        Assert.AreEqual(1, faulting.DisposeCount);
        Assert.AreEqual(0, never.BeforeRunCount);
        Assert.AreEqual(1, never.AfterRunCount);
        Assert.AreEqual((int)ExitCode.GenericFailure, never.LastExitCode);
        Assert.AreEqual(1, never.DisposeCount);
    }

    [TestMethod]
    public async Task RunAsync_OrchestratorThrows_OneLifetimeFailingDoesNotBlockTheNext()
    {
        RecordingLifetime failing = new(afterRunException: new InvalidOperationException("cleanup exploded"));
        RecordingLifetime healthy = new();
        TestHostOrchestratorHost host = CreateHost(
            new RecordingOrchestrator(onOrchestrate: _ => throw new InvalidOperationException("orchestrator exploded")),
            [failing, healthy],
            out _);

        // The orchestrator's failure is what surfaces, not the cleanup failure.
        InvalidOperationException exception = await Assert.ThrowsExactlyAsync<InvalidOperationException>(host.RunAsync);

        Assert.AreEqual("orchestrator exploded", exception.Message);
        Assert.AreEqual(1, failing.BeforeRunCount);
        Assert.AreEqual(1, failing.AfterRunCount);
        Assert.AreEqual((int)ExitCode.GenericFailure, failing.LastExitCode);
        Assert.AreEqual(1, failing.DisposeCount);
        Assert.AreEqual(1, healthy.BeforeRunCount);
        Assert.AreEqual(1, healthy.AfterRunCount);
        Assert.AreEqual((int)ExitCode.GenericFailure, healthy.LastExitCode);
        Assert.AreEqual(1, healthy.DisposeCount);
    }

    [TestMethod]
    public async Task RunAsync_OrchestratorCanceled_LifetimeAfterRunFailureIsSwallowed()
    {
        RecordingLifetime lifetime = new(afterRunException: new InvalidOperationException("cleanup exploded"));
        TestHostOrchestratorHost host = CreateHost(
            new RecordingOrchestrator(onOrchestrate: cancellationTokenSource =>
            {
                cancellationTokenSource.Cancel();
                throw new OperationCanceledException();
            }),
            lifetime,
            out _);

        int exitCode = await host.RunAsync();

        Assert.AreEqual((int)ExitCode.TestSessionAborted, exitCode);
        Assert.AreEqual(1, lifetime.BeforeRunCount);
        Assert.AreEqual(1, lifetime.AfterRunCount);
        Assert.AreEqual((int)ExitCode.TestSessionAborted, lifetime.LastExitCode);
        Assert.AreEqual(1, lifetime.DisposeCount);
    }

    [TestMethod]
    public async Task RunAsync_OrchestratorSucceeds_LifetimeIsNotInvokedTwice()
    {
        RecordingLifetime lifetime = new();
        TestHostOrchestratorHost host = CreateHost(new RecordingOrchestrator(exitCode: 0), lifetime, out _);

        await host.RunAsync();

        Assert.AreEqual(1, lifetime.BeforeRunCount);
        Assert.AreEqual(1, lifetime.AfterRunCount);
        Assert.AreEqual(0, lifetime.LastExitCode);
        Assert.AreEqual(1, lifetime.DisposeCount);
    }

    [TestMethod]
    public async Task RunAsync_LoggerThrows_StillCleansUpEveryLifetime()
    {
        // Diagnostic logging in the cleanup loop is best-effort: the aggregate logger propagates provider
        // exceptions, so a broken log provider must not skip disposal or the remaining lifetimes - the very
        // resources this loop exists to release.
        RecordingLifetime failing = new(afterRunException: new InvalidOperationException("cleanup exploded"));
        RecordingLifetime healthy = new();
        TestHostOrchestratorHost host = CreateHost(
            new RecordingOrchestrator(onOrchestrate: _ => throw new InvalidOperationException("orchestrator exploded")),
            [failing, healthy],
            out _,
            new ThrowingLoggerFactory());

        InvalidOperationException exception = await Assert.ThrowsExactlyAsync<InvalidOperationException>(host.RunAsync);

        Assert.AreEqual("orchestrator exploded", exception.Message);
        Assert.AreEqual(1, failing.BeforeRunCount);
        Assert.AreEqual(1, failing.AfterRunCount);
        Assert.AreEqual((int)ExitCode.GenericFailure, failing.LastExitCode);
        Assert.AreEqual(1, failing.DisposeCount);
        Assert.AreEqual(1, healthy.BeforeRunCount);
        Assert.AreEqual(1, healthy.AfterRunCount);
        Assert.AreEqual((int)ExitCode.GenericFailure, healthy.LastExitCode);
        Assert.AreEqual(1, healthy.DisposeCount);
    }

    private static TestHostOrchestratorHost CreateHost(
        RecordingOrchestrator orchestrator,
        RecordingLifetime lifetime,
        out FakeApplicationCancellationTokenSource cancellationTokenSource)
        => CreateHost(orchestrator, [lifetime], out cancellationTokenSource);

    private static TestHostOrchestratorHost CreateHost(
        RecordingOrchestrator orchestrator,
        RecordingLifetime[] lifetimes,
        out FakeApplicationCancellationTokenSource cancellationTokenSource,
        ILoggerFactory? loggerFactory = null)
    {
        ServiceProvider serviceProvider = new();
        cancellationTokenSource = new FakeApplicationCancellationTokenSource();
        orchestrator.CancellationTokenSource = cancellationTokenSource;
        serviceProvider.AddService(cancellationTokenSource);
        serviceProvider.AddService(loggerFactory ?? new NopLoggerFactory());
        serviceProvider.AddServices([.. lifetimes]);

        return new TestHostOrchestratorHost(new TestHostOrchestratorConfiguration([orchestrator]), serviceProvider);
    }

    private sealed class RecordingOrchestrator : ITestHostExecutionOrchestrator
    {
        private readonly Action<FakeApplicationCancellationTokenSource>? _onOrchestrate;

        public RecordingOrchestrator(int exitCode = 0, Action<FakeApplicationCancellationTokenSource>? onOrchestrate = null)
        {
            ExitCode = exitCode;
            _onOrchestrate = onOrchestrate;
        }

        public int ExitCode { get; }

        public FakeApplicationCancellationTokenSource CancellationTokenSource { get; set; } = new();

        public string Uid => nameof(RecordingOrchestrator);

        public string Version => "1.0.0";

        public string DisplayName => nameof(RecordingOrchestrator);

        public string Description => nameof(RecordingOrchestrator);

        public Task<bool> IsEnabledAsync() => Task.FromResult(true);

        public Task<int> OrchestrateTestHostExecutionAsync(CancellationToken cancellationToken)
        {
            _onOrchestrate?.Invoke(CancellationTokenSource);
            return Task.FromResult(ExitCode);
        }
    }

    private sealed class RecordingLifetime : ITestHostOrchestratorApplicationLifetime, IDisposable
    {
        private static int s_instanceCount;
        private readonly Exception? _beforeRunException;
        private readonly Exception? _afterRunException;

        public RecordingLifetime(Exception? beforeRunException = null, Exception? afterRunException = null)
        {
            _beforeRunException = beforeRunException;
            _afterRunException = afterRunException;
            Uid = $"{nameof(RecordingLifetime)}-{Interlocked.Increment(ref s_instanceCount)}";
        }

        public int BeforeRunCount { get; private set; }

        public int AfterRunCount { get; private set; }

        public int DisposeCount { get; private set; }

        public int? LastExitCode { get; private set; }

        public string Uid { get; }

        public string Version => "1.0.0";

        public string DisplayName => Uid;

        public string Description => Uid;

        public Task<bool> IsEnabledAsync() => Task.FromResult(true);

        public Task BeforeRunAsync(CancellationToken cancellationToken)
        {
            BeforeRunCount++;
            return _beforeRunException is null ? Task.CompletedTask : throw _beforeRunException;
        }

        public Task AfterRunAsync(int exitCode, CancellationToken cancellationToken)
        {
            AfterRunCount++;
            LastExitCode = exitCode;
            return _afterRunException is null ? Task.CompletedTask : throw _afterRunException;
        }

        public void Dispose() => DisposeCount++;
    }

    private sealed class FakeApplicationCancellationTokenSource : ITestApplicationCancellationTokenSource, IDisposable
    {
        private readonly CancellationTokenSource _cancellationTokenSource = new();

        public CancellationToken CancellationToken => _cancellationTokenSource.Token;

#pragma warning disable VSTHRD103 // CancelAsync is only available on .NET 8+; this project also targets .NET Framework.
        public void Cancel() => _cancellationTokenSource.Cancel();
#pragma warning restore VSTHRD103

        public void Dispose() => _cancellationTokenSource.Dispose();
    }

    private sealed class NopLoggerFactory : ILoggerFactory
    {
        public ILogger CreateLogger(string categoryName) => new NopLogger();

        private sealed class NopLogger : ILogger
        {
            public bool IsEnabled(LogLevel logLevel) => false;

            public void Log<TState>(LogLevel logLevel, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            {
            }

            public Task LogAsync<TState>(LogLevel logLevel, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
                => Task.CompletedTask;
        }
    }

    /// <summary>
    /// A logger whose provider throws for the levels the cleanup loop uses, mirroring the aggregate
    /// logger's behaviour of propagating provider exceptions to the caller. Other levels pass through so
    /// the host's own startup logging, which is outside the cleanup path, still works.
    /// </summary>
    private sealed class ThrowingLoggerFactory : ILoggerFactory
    {
        public ILogger CreateLogger(string categoryName) => new ThrowingLogger();

        private sealed class ThrowingLogger : ILogger
        {
            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
                => ThrowIfCleanupLevel(logLevel);

            public Task LogAsync<TState>(LogLevel logLevel, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            {
                ThrowIfCleanupLevel(logLevel);
                return Task.CompletedTask;
            }

            private static void ThrowIfCleanupLevel(LogLevel logLevel)
            {
                if (logLevel is LogLevel.Warning or LogLevel.Debug)
                {
                    throw new InvalidOperationException("log provider exploded");
                }
            }
        }
    }
}
