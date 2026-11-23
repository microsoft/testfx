// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Concurrent;

using AwesomeAssertions;

#if DEBUG && !NETFRAMEWORK
using Microsoft.Testing.TestInfrastructure;
#endif

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Resources;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.TestableImplementations;

using Moq;

using TestFramework.ForTestingMSTest;

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Execution;

public sealed class TestMethodInfoExecutionTimeoutCleanupMutationTests : TestContainer
{
    private const string TestMethodName = "TestMethod";
    private const string TestCleanupMethodName = "TestCleanup";

    public async Task InvokeAsyncDoesNotStartTheTestOnAWorkerThatIsReleasedAfterTheTimeoutWasReported()
    {
        using var probe = new FixtureProbe();
        LateWorkerFixture.Probe = probe;
        using var harness = new CleanupHarness(typeof(LateWorkerFixture));
        Action? lateWorker = null;
        harness.Services.MockThreadOperations
            .Setup(operations => operations.Execute(It.IsAny<Action>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Returns<Action, int, CancellationToken>((action, _, _) =>
            {
                lateWorker = action;
                return false;
            });
#if DEBUG && !NETFRAMEWORK
        using var observer = new DiagnosticAssertionObserver(_ => { });
#endif

        TestResult result = await harness.Subject.InvokeAsync(null);

        // The worker only gets to run once the timeout was reported: it must find the invocation closed.
        lateWorker!();

        result.Outcome.Should().Be(UnitTestOutcome.Timeout);
        result.TestFailureException.Should().BeOfType<TestFailedException>().Which.Message.Should().Be(
            string.Format(CultureInfo.CurrentCulture, Resource.Execution_Test_Timeout, TestMethodName, 60_000));
        probe.InstanceCount.Should().Be(0, because: "a worker that starts after the timeout was reported must not run the test");
    }

    public async Task InvokeAsyncDoesNotWaitForARunningCleanupOnceTheTestRunWasCancelled()
    {
        using var probe = new FixtureProbe();
        BlockingCleanupFixture.Probe = probe;
        using var harness = new CleanupHarness(typeof(BlockingCleanupFixture));
        harness.Services.TestRunCancellationToken = new TestRunCancellationToken();
        var workerCompleted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        harness.Services.MockThreadOperations
            .Setup(operations => operations.Execute(It.IsAny<Action>(), It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .Returns<Action, int, CancellationToken>((action, timeout, token) => new ThreadOperations().Execute(
                () =>
                {
                    try
                    {
                        action();
                    }
                    finally
                    {
                        workerCompleted.TrySetResult(true);
                    }
                },
                timeout,
                token));

        try
        {
            // The cleanup cancels the run and then blocks: the cancelled run ends the wait for it, not the (distant) timeout.
            TestResult result = await WaitAsync(harness.Subject.InvokeAsync(null), guardSeconds: 10);

            result.Outcome.Should().Be(UnitTestOutcome.Timeout);
            result.TestFailureException.Should().BeOfType<TestFailedException>().Which.Message.Should().Be(
                string.Format(CultureInfo.CurrentCulture, Resource.Execution_Test_Cancelled, TestMethodName));
        }
        finally
        {
            probe.ReleaseTest.Set();
            await WaitAsync(workerCompleted.Task);
        }
    }

    public async Task InvokeAsyncWaitsForTheCleanupItDispatchedToTheSynchronizationContextOfACancelledTest()
    {
        using var probe = new FixtureProbe();
        SlowCleanupFixture.Probe = probe;
        using var harness = new CleanupHarness(typeof(SlowCleanupFixture));
        var runCancellation = new TestRunCancellationToken();
        harness.Services.TestRunCancellationToken = runCancellation;
        using var dispatcher = new DispatcherContext();
        Task<TestResult> invocation = dispatcher.InvokeAsync(() => harness.Subject.InvokeAsync(null));

        try
        {
            await WaitAsync(probe.TestStarted.Task);

            // The dispatcher is free while the test waits: it runs the cleanup, which outlives the dispatch window.
            runCancellation.Cancel();
            TestResult result = await WaitAsync(invocation);

            result.Outcome.Should().Be(UnitTestOutcome.Timeout);
            result.TestFailureException.Should().BeOfType<TestFailedException>().Which.Message.Should().Be(
                string.Format(CultureInfo.CurrentCulture, Resource.Execution_Test_Cancelled, TestMethodName));
            probe.CleanupCount.Should().Be(1);
            probe.CleanupFinished.Should().BeTrue(because: "the result is only reported once the cleanup dispatched to the synchronization context completed");
        }
        finally
        {
            probe.ReleaseTest.Set();
        }
    }

    public async Task InvokeAsyncLogsThatTheCleanupIsDeferredWhenTheSynchronizationContextStaysBusyAfterTheCancellation()
    {
        using var probe = new FixtureProbe();
        BusyDispatcherFixture.Probe = probe;
        using var harness = new CleanupHarness(typeof(BusyDispatcherFixture));
        harness.Services.MockTraceLogger.Setup(logger => logger.IsVerboseEnabled).Returns(true);
        var runCancellation = new TestRunCancellationToken();
        harness.Services.TestRunCancellationToken = runCancellation;
        using var dispatcher = new DispatcherContext();
        Task<TestResult> invocation = dispatcher.InvokeAsync(() => harness.Subject.InvokeAsync(null));

        try
        {
            await WaitAsync(probe.TestStarted.Task);

            // The test blocks the dispatcher: the cleanup cannot be dispatched and is left to the original invocation.
            runCancellation.Cancel();
            TestResult result = await WaitAsync(invocation);

            result.Outcome.Should().Be(UnitTestOutcome.Timeout);
            result.TestFailureException.Should().BeOfType<TestFailedException>().Which.Message.Should().Be(
                string.Format(CultureInfo.CurrentCulture, Resource.Execution_Test_Cancelled, TestMethodName));
            harness.Services.MockTraceLogger.Verify(
                logger => logger.Verbose(
                    "MSTest.TestMethodInfo: Cleanup for timed out test '{0}' is deferred because its synchronization context did not become available within {1}ms.",
                    TestMethodName,
                    1_000),
                Times.Once);
        }
        finally
        {
            probe.ReleaseTest.Set();
        }
    }

    public async Task InvokeAsyncPropagatesTheExceptionThatEscapedTheTestRunningOnTheSynchronizationContext()
    {
        using var harness = new CleanupHarness(typeof(DisposingFixture));
        using var dispatcher = new DispatcherContext();
#if DEBUG && !NETFRAMEWORK
        using var observer = new DiagnosticAssertionObserver(_ => { });
#endif

        // Disposing the cancellation source makes the handling of the test failure itself throw.
        Func<Task> invoke = () => dispatcher.InvokeAsync(() => harness.Subject.InvokeAsync(null));

        await invoke.Should().ThrowAsync<ObjectDisposedException>();
    }

    public async Task InvokeAsyncDoesNotRunTheTestQueuedOnTheSynchronizationContextOnceTheInvocationWasLeftByAnException()
    {
        using var probe = new FixtureProbe();
        QueuedWorkerFixture.Probe = probe;

        // Task.Delay rejects a negative timeout once the test is queued on the dispatcher: the invocation is left before the queued test started.
        using var harness = new CleanupHarness(typeof(QueuedWorkerFixture), timeout: -5);
        using var dispatcher = new DispatcherContext();
#if DEBUG && !NETFRAMEWORK
        using var observer = new DiagnosticAssertionObserver(_ => { });
#endif

        Func<Task> invoke = () => dispatcher.InvokeAsync(() => harness.Subject.InvokeAsync(null));

        await invoke.Should().ThrowAsync<ArgumentOutOfRangeException>();
        await dispatcher.DrainAsync();

        probe.InstanceCount.Should().Be(0, because: "the queued test must find the invocation closed instead of running after it was left");
    }

    private static async Task<TResult> WaitAsync<TResult>(Task<TResult> task, int guardSeconds = 30)
    {
        using var guard = new CancellationTokenSource(TimeSpan.FromSeconds(guardSeconds));
        Task completed = await Task.WhenAny(task, Task.Delay(Timeout.Infinite, guard.Token));
        completed.Should().BeSameAs(task, "the invocation is expected to complete within its guard time");

        return await task;
    }

    private sealed class CleanupHarness : IDisposable
    {
        private readonly IPlatformServiceProvider _previousProvider;

        public CleanupHarness(Type fixtureType, int timeout = 60_000)
        {
            _previousProvider = PlatformServiceProvider.Instance;
            Services = new TestablePlatformServiceProvider();
            Services.SetReflectionOperations(new ReflectionOperations());
            PlatformServiceProvider.Instance = Services;

            Context = new TestContextImplementation(null, fixtureType.FullName, new Dictionary<string, object?>(), null, null);
            var assemblyInfo = new TestAssemblyInfo(typeof(CleanupHarness).Assembly);
            var classInfo = new TestClassInfo(fixtureType, fixtureType.GetConstructor(Type.EmptyTypes), true, new TestClassAttribute(), assemblyInfo)
            {
                TestCleanupMethod = fixtureType.GetMethod(TestCleanupMethodName),
            };
            Subject = new TestMethodInfo(fixtureType.GetMethod(TestMethodName)!, classInfo)
            {
                TestContext = Context,
                TimeoutInfo = TimeoutInfo.FromTimeout(timeout),
            };
        }

        public TestablePlatformServiceProvider Services { get; }

        public TestContextImplementation Context { get; }

        public TestMethodInfo Subject { get; }

        public void Dispose()
            => PlatformServiceProvider.Instance = _previousProvider;
    }

    private sealed class DispatcherContext : SynchronizationContext, IDisposable
    {
        private readonly BlockingCollection<Action> _queue = [];
        private readonly Thread _thread;

        public DispatcherContext()
        {
            _thread = new Thread(() =>
            {
                SynchronizationContext.SetSynchronizationContext(this);
                foreach (Action work in _queue.GetConsumingEnumerable())
                {
                    work();
                }
            })
            {
                IsBackground = true,
            };
            _thread.Start();
        }

        public override void Post(SendOrPostCallback d, object? state)
        {
            try
            {
                _queue.TryAdd(() => d(state));
            }
            catch (InvalidOperationException)
            {
                // The dispatcher was disposed: the work still posted by a released invocation is dropped.
            }
        }

        public Task<TestResult> InvokeAsync(Func<Task<TestResult>> invoke)
        {
            var completion = new TaskCompletionSource<TestResult>(TaskCreationOptions.RunContinuationsAsynchronously);
            Post(
                _ => _ = invoke().ContinueWith(
                    task =>
                    {
                        if (task.IsFaulted)
                        {
                            completion.SetException(task.Exception!);
                        }
                        else if (task.IsCanceled)
                        {
                            completion.SetCanceled();
                        }
                        else
                        {
                            completion.SetResult(task.Result);
                        }
                    },
                    CancellationToken.None,
                    TaskContinuationOptions.None,
                    TaskScheduler.Default),
                null);

            return completion.Task;
        }

        public Task DrainAsync()
        {
            var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            Post(_ => completion.SetResult(true), null);

            return completion.Task;
        }

        public void Dispose()
        {
            _queue.CompleteAdding();
            if (!_thread.Join(TimeSpan.FromSeconds(5)))
            {
                // The thread is a background thread: it does not keep the test host alive.
            }

            _queue.Dispose();
        }
    }

    private sealed class FixtureProbe : IDisposable
    {
        private int _instanceCount;
        private int _cleanupCount;
        private int _cleanupFinished;

        public TaskCompletionSource<bool> TestStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public ManualResetEventSlim ReleaseTest { get; } = new();

        public int InstanceCount => Volatile.Read(ref _instanceCount);

        public int CleanupCount => Volatile.Read(ref _cleanupCount);

        public bool CleanupFinished => Volatile.Read(ref _cleanupFinished) != 0;

        public void InstanceCreated()
            => Interlocked.Increment(ref _instanceCount);

        public void CleanupStarted()
            => Interlocked.Increment(ref _cleanupCount);

        public void CleanupCompleted()
            => Volatile.Write(ref _cleanupFinished, 1);

        public void Dispose()
            => ReleaseTest.Dispose();
    }

    private sealed class LateWorkerFixture
    {
        public LateWorkerFixture()
            => Probe!.InstanceCreated();

        public static FixtureProbe? Probe { get; set; }

        public TestContext? TestContext { get; set; }

        public void TestMethod()
        {
        }
    }

    private sealed class QueuedWorkerFixture
    {
        public QueuedWorkerFixture()
            => Probe!.InstanceCreated();

        public static FixtureProbe? Probe { get; set; }

        public TestContext? TestContext { get; set; }

        public void TestMethod()
        {
        }
    }

    private sealed class BlockingCleanupFixture
    {
        public static FixtureProbe? Probe { get; set; }

        public TestContext? TestContext { get; set; }

        public void TestMethod()
        {
        }

        public void TestCleanup()
        {
            PlatformServiceProvider.Instance.TestRunCancellationToken!.Cancel();
            Probe!.ReleaseTest.Wait();
        }
    }

    private sealed class SlowCleanupFixture
    {
        public static FixtureProbe? Probe { get; set; }

        public TestContext? TestContext { get; set; }

        public async Task TestMethod()
        {
            Probe!.TestStarted.TrySetResult(true);
            await Task.Run(() => Probe.ReleaseTest.Wait());
        }

        public async Task TestCleanup()
        {
            Probe!.CleanupStarted();
            await Task.Delay(TimeSpan.FromSeconds(2));
            Probe.CleanupCompleted();
        }
    }

    private sealed class BusyDispatcherFixture
    {
        public static FixtureProbe? Probe { get; set; }

        public TestContext? TestContext { get; set; }

        public void TestMethod()
        {
            Probe!.TestStarted.TrySetResult(true);
            Probe.ReleaseTest.Wait();
        }

        public void TestCleanup()
            => Probe!.CleanupStarted();
    }

    private sealed class DisposingFixture
    {
        public TestContext? TestContext { get; set; }

        public void TestMethod()
        {
            TestContext!.CancellationTokenSource.Dispose();
            throw new InvalidOperationException("The test failed.");
        }
    }
}
