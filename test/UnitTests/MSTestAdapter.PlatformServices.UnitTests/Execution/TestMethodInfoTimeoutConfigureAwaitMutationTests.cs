// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Runtime.ExceptionServices;

using AwesomeAssertions;

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using MSTestAdapter.PlatformServices.UnitTests.Helpers;

using TestFramework.ForTestingMSTest;

namespace MSTestAdapter.PlatformServices.UnitTests.Execution;

public sealed class TestMethodInfoTimeoutConfigureAwaitMutationTests : TestContainer
{
    private static readonly TimeSpan WaitTimeout = TimeSpan.FromSeconds(30);

    public async Task InvokeAsync_WithCooperativeTimeoutAndPendingGlobalTestInitialize_NeverResumesOnTheCapturedSynchronizationContext()
    {
        var probe = new ContinuationProbe();
        var globalInitializeGate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        CooperativeTimeoutFixture.Gate = globalInitializeGate;
        using var harness = new Harness(typeof(CooperativeTimeoutFixture), nameof(CooperativeTimeoutFixture.Run), TimeoutInfo.FromTimeoutAttribute(new TimeoutAttribute(60_000) { CooperativeCancellation = true }));
        harness.AssemblyInfo.GlobalTestInitializations.Add((typeof(CooperativeTimeoutFixture).GetMethod(nameof(CooperativeTimeoutFixture.GlobalInitialize))!, null));

        // The global initializer has no timeout, so its pending task suspends the invocation inside the cooperative timeout wrappers.
        Task<TestResult> runner = probe.Run(() => harness.MethodInfo.InvokeAsync(null));

        runner.IsCompleted.Should().BeFalse();

        globalInitializeGate.SetResult(true);
        await WaitFor(runner);

        (await runner).Outcome.Should().Be(UnitTestOutcome.Passed);
        probe.PostCount.Should().Be(0, "the cooperative timeout wrappers must not capture the caller's synchronization context");
    }

    public async Task InvokeAsync_WithTimeoutAndCancelledTestWaitingForPendingTestCleanup_NeverResumesOnTheCapturedSynchronizationContext()
    {
        var context = new InlineSynchronizationContext();
        var cleanupGate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        CancelledTestFixture.CleanupGate = cleanupGate;
        using var runCancellation = new RunCancellationIsolation();
        using var harness = new Harness(typeof(CancelledTestFixture), nameof(CancelledTestFixture.Run), TimeoutInfo.FromTimeout(60_000));
        harness.ClassInfo.TestCleanupMethod = typeof(CancelledTestFixture).GetMethod(nameof(CancelledTestFixture.Cleanup))!;

        // The test cancels itself and the cleanup that its execution started never completes, so the monitor waits for the execution to end.
        Task<TestResult> runner = StartOnContextThread(context, () => harness.MethodInfo.InvokeAsync(null));

        runner.IsCompleted.Should().BeFalse();

        cleanupGate.SetResult(true);
        await WaitFor(runner);

        (await runner).Outcome.Should().Be(UnitTestOutcome.Timeout);
        context.PostCount.Should().Be(0, "neither the execution nor the monitor of a cancelled test may resume on the captured synchronization context");
    }

    public async Task InvokeAsync_WithTimeoutAndCancelledPendingTestWaitingForDispatchedTestCleanup_NeverResumesOnTheCapturedSynchronizationContext()
    {
        var context = new InlineSynchronizationContext();
        var bodyGate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var cleanupGate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        CancelledPendingTestFixture.BodyGate = bodyGate;
        CancelledPendingTestFixture.CleanupGate = cleanupGate;
        using var runCancellation = new RunCancellationIsolation();
        using var harness = new Harness(typeof(CancelledPendingTestFixture), nameof(CancelledPendingTestFixture.Run), TimeoutInfo.FromTimeout(60_000));
        harness.ClassInfo.TestCleanupMethod = typeof(CancelledPendingTestFixture).GetMethod(nameof(CancelledPendingTestFixture.Cleanup))!;

        // The test cancels itself and never completes, so the monitor dispatches the cleanup on the synchronization context and waits for it.
        Task<TestResult> runner = StartOnContextThread(context, () => harness.MethodInfo.InvokeAsync(null));

        runner.IsCompleted.Should().BeFalse();

        cleanupGate.SetResult(true);
        await WaitFor(runner);

        (await runner).Outcome.Should().Be(UnitTestOutcome.Timeout);
        context.PostCount.Should().Be(0, "neither the dispatched cleanup nor the monitor of a cancelled test may resume on the captured synchronization context");

        // Let the abandoned test unwind so that nothing outlives this test.
        bodyGate.SetResult(true);
        await WaitFor(context.ContinuationCompleted);
    }

    private static async Task WaitFor(Task task)
    {
        Task completed = await Task.WhenAny(task, Task.Delay(WaitTimeout));
        completed.Should().BeSameAs(task, "the awaited operation timed out");
        await task;
    }

    private static Task<TestResult> StartOnContextThread(InlineSynchronizationContext context, Func<Task<TestResult>> start)
    {
        Task<TestResult>? started = null;
        ExceptionDispatchInfo? failure = null;
        var thread = new Thread(() =>
        {
            SynchronizationContext.SetSynchronizationContext(context);
            try
            {
                started = start();
            }
            catch (Exception ex)
            {
                failure = ExceptionDispatchInfo.Capture(ex);
            }
        })
        {
            IsBackground = true,
        };

        thread.Start();
        thread.Join(WaitTimeout).Should().BeTrue("the invocation must suspend instead of blocking the context thread");
        failure?.Throw();

        return started!;
    }

    private sealed class Harness : IDisposable
    {
        private readonly TestContextImplementation _testContext;
        private readonly IDisposable _testContextScope;

        public Harness(Type fixtureType, string testMethodName, TimeoutInfo timeoutInfo)
        {
            AssemblyInfo = new TestAssemblyInfo(fixtureType.Assembly);
            var testMethod = new TestMethod("dummyTestName", fixtureType.FullName!, "dummyAssemblyName", displayName: null);
            _testContext = new TestContextImplementation(testMethod, null, new Dictionary<string, object?>(), null, null);
            _testContextScope = TestContextImplementation.SetCurrentTestContext(_testContext);
            ClassInfo = new TestClassInfo(fixtureType, fixtureType.GetConstructor(Type.EmptyTypes), true, new TestClassAttribute(), AssemblyInfo);
            MethodInfo = new TestMethodInfo(fixtureType.GetMethod(testMethodName)!, ClassInfo)
            {
                TestContext = _testContext,
                TimeoutInfo = timeoutInfo,
                Executor = new TestMethodAttribute(),
            };
        }

        public TestAssemblyInfo AssemblyInfo { get; }

        public TestClassInfo ClassInfo { get; }

        public TestMethodInfo MethodInfo { get; }

        public void Dispose()
        {
            _testContextScope.Dispose();
            _testContext.Dispose();
        }
    }

    // Runs every posted continuation inline, like a synchronization context whose thread is always available.
    private sealed class InlineSynchronizationContext : SynchronizationContext
    {
        private readonly TaskCompletionSource<bool> _continuationCompleted = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _postCount;

        public int PostCount => Volatile.Read(ref _postCount);

        public Task ContinuationCompleted => _continuationCompleted.Task;

        public override void Post(SendOrPostCallback d, object? state)
        {
            // The task scheduler of the context posts the tasks it schedules, only the continuations are counted.
            if (state is Task)
            {
                RunInline(d, state);
                return;
            }

            Interlocked.Increment(ref _postCount);
            try
            {
                RunInline(d, state);
            }
            finally
            {
                _continuationCompleted.TrySetResult(true);
            }
        }

        public override SynchronizationContext CreateCopy()
            => this;

        private void RunInline(SendOrPostCallback d, object? state)
        {
            SynchronizationContext? previous = Current;
            SetSynchronizationContext(this);
            try
            {
                d(state);
            }
            finally
            {
                SetSynchronizationContext(previous);
            }
        }
    }

    // The test run cancellation token is shared by the whole process: a cancelled one would change how the monitor waits.
    private sealed class RunCancellationIsolation : IDisposable
    {
        private readonly TestRunCancellationToken? _previous;

        public RunCancellationIsolation()
        {
            _previous = PlatformServiceProvider.Instance.TestRunCancellationToken;
            PlatformServiceProvider.Instance.TestRunCancellationToken = null;
        }

        public void Dispose()
            => PlatformServiceProvider.Instance.TestRunCancellationToken = _previous;
    }

    private sealed class CooperativeTimeoutFixture
    {
        public static TaskCompletionSource<bool>? Gate { get; set; }

        public static Task GlobalInitialize(TestContext testContext)
            => Gate!.Task;

        public void Run()
        {
        }
    }

    private sealed class CancelledTestFixture
    {
        public static TaskCompletionSource<bool>? CleanupGate { get; set; }

        public TestContext? TestContext { get; set; }

        public void Run()
            => TestContext!.CancellationTokenSource.Cancel();

        public Task Cleanup()
            => CleanupGate!.Task;
    }

    private sealed class CancelledPendingTestFixture
    {
        public static TaskCompletionSource<bool>? BodyGate { get; set; }

        public static TaskCompletionSource<bool>? CleanupGate { get; set; }

        public TestContext? TestContext { get; set; }

        public async Task Run()
        {
            TestContext!.CancellationTokenSource.Cancel();
            await BodyGate!.Task;
        }

        public Task Cleanup()
            => CleanupGate!.Task;
    }
}
