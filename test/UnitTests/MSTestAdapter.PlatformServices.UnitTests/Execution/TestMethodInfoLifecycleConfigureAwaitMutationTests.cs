// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using AwesomeAssertions;

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using MSTestAdapter.PlatformServices.UnitTests.Helpers;

using TestFramework.ForTestingMSTest;

namespace MSTestAdapter.PlatformServices.UnitTests.Execution;

public sealed class TestMethodInfoLifecycleConfigureAwaitMutationTests : TestContainer
{
    private static readonly TimeSpan WaitTimeout = TimeSpan.FromSeconds(30);

    public async Task InvokeAsync_WithPendingGlobalTestInitialize_ResumesOnTheCapturedSynchronizationContext()
    {
        var probe = new ContinuationProbe();
        var globalInitializeGate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        GlobalInitializeFixture.Gate = globalInitializeGate;
        using var harness = new Harness(typeof(GlobalInitializeFixture), nameof(GlobalInitializeFixture.Run));
        harness.AssemblyInfo.GlobalTestInitializations.Add((typeof(GlobalInitializeFixture).GetMethod(nameof(GlobalInitializeFixture.GlobalInitialize))!, null));

        // The global initializer has no timeout, so it is the first pending operand and it suspends the whole invocation.
        Task<TestResult> runner = probe.Run(() => harness.MethodInfo.InvokeAsync(null));

        runner.IsCompleted.Should().BeFalse();

        globalInitializeGate.SetResult(true);
        await WaitFor(runner);

        (await runner).Outcome.Should().Be(UnitTestOutcome.Passed);
        probe.PostCount.Should().Be(1, "the pending global test initialization must resume on the test's captured synchronization context");
    }

    public async Task InvokeAsync_WithPendingBaseTestInitialize_ResumesOnTheCapturedSynchronizationContextBeforeAndAfterTestInitialize()
    {
        var probe = new ContinuationProbe();
        var baseInitializeGate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        BaseTestInitializeFixture.Gate = baseInitializeGate;
        using var harness = new Harness(typeof(BaseTestInitializeFixture), nameof(BaseTestInitializeFixture.Run));
        harness.ClassInfo.BaseTestInitializeMethodsQueue.Add(typeof(BaseTestInitializeFixture).GetMethod(nameof(BaseTestInitializeFixture.BaseInitialize))!);

        // There is no TestInitialize method, so the pending base TestInitialize method is the first awaited fixture.
        Task<TestResult> runner = probe.Run(() => harness.MethodInfo.InvokeAsync(null));

        runner.IsCompleted.Should().BeFalse();

        baseInitializeGate.SetResult(true);
        await WaitFor(runner);

        (await runner).Outcome.Should().Be(UnitTestOutcome.Passed);
        probe.PostCount.Should().Be(2, "the base initialization and subsequent test initialization must resume on the test's captured synchronization context");
    }

    public async Task InvokeAsync_WithPendingTestInitialize_OnlyResumesOnTheCapturedSynchronizationContextAfterTestInitialize()
    {
        var probe = new ContinuationProbe();
        var initializeGate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        TestInitializeFixture.Gate = initializeGate;
        using var harness = new Harness(typeof(TestInitializeFixture), nameof(TestInitializeFixture.Run));
        harness.ClassInfo.TestInitializeMethod = typeof(TestInitializeFixture).GetMethod(nameof(TestInitializeFixture.Initialize))!;

        // There is no base TestInitialize method, so the pending TestInitialize method is the first awaited fixture.
        Task<TestResult> runner = probe.Run(() => harness.MethodInfo.InvokeAsync(null));

        runner.IsCompleted.Should().BeFalse();

        initializeGate.SetResult(true);
        await WaitFor(runner);

        (await runner).Outcome.Should().Be(UnitTestOutcome.Passed);
        probe.PostCount.Should().Be(1, "the pending test initialization must resume on the test's captured synchronization context");
    }

    public async Task InvokeAsync_WithPendingBaseTestCleanup_ResumesOnTheCapturedSynchronizationContext()
    {
        var probe = new ContinuationProbe();
        var baseCleanupGate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        BaseTestCleanupFixture.Gate = baseCleanupGate;
        using var harness = new Harness(typeof(BaseTestCleanupFixture), nameof(BaseTestCleanupFixture.Run));
        harness.ClassInfo.BaseTestCleanupMethodsQueue.Add(typeof(BaseTestCleanupFixture).GetMethod(nameof(BaseTestCleanupFixture.BaseCleanup))!);

        // The test method completes synchronously, so the pending base TestCleanup method is the first pending operand.
        Task<TestResult> runner = probe.Run(() => harness.MethodInfo.InvokeAsync(null));

        runner.IsCompleted.Should().BeFalse();

        baseCleanupGate.SetResult(true);
        await WaitFor(runner);

        (await runner).Outcome.Should().Be(UnitTestOutcome.Passed);
        probe.PostCount.Should().Be(1, "the pending base cleanup must resume on the test's captured synchronization context");
    }

    public async Task InvokeAsync_WithPendingTestClassInstanceDisposal_ResumesOnTheCapturedSynchronizationContext()
    {
        var probe = new ContinuationProbe();
        var disposalGate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var harness = new Harness(typeof(LeaseFixture), nameof(LeaseFixture.Run));

        // The test method completes synchronously, so the pending disposal of the test class instance is the first pending operand.
        using (TestClassInstanceFactoryProvider.Push(new PendingDisposalFactory(disposalGate.Task)))
        {
            Task<TestResult> runner = probe.Run(() => harness.MethodInfo.InvokeAsync(null));

            runner.IsCompleted.Should().BeFalse();

            disposalGate.SetResult(true);
            await WaitFor(runner);

            (await runner).Outcome.Should().Be(UnitTestOutcome.Passed);
        }

        probe.PostCount.Should().Be(1, "disposing the test class instance must resume on the test's captured synchronization context");
    }

    public async Task InvokeAsync_WithPendingTestClassInstanceDisposalAfterTestContextFailure_NeverResumesOnTheCapturedSynchronizationContext()
    {
        var probe = new ContinuationProbe();
        var disposalGate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var harness = new Harness(typeof(ThrowingTestContextFixture), nameof(ThrowingTestContextFixture.Run));

        // Setting the TestContext fails, so the test does not run and the pending disposal is the first pending operand.
        using (TestClassInstanceFactoryProvider.Push(new PendingDisposalFactory(disposalGate.Task)))
        {
            Task<TestResult> runner = probe.Run(() => harness.MethodInfo.InvokeAsync(null));

            runner.IsCompleted.Should().BeFalse();

            disposalGate.SetResult(true);
            await WaitFor(runner);

            (await runner).Outcome.Should().Be(UnitTestOutcome.Failed);
        }

        probe.PostCount.Should().Be(0, "the disposal of the test class instance must not capture the caller's synchronization context");
    }

#if NET6_0_OR_GREATER
    public async Task InvokeAsync_WithPendingAsyncDisposableTestClass_ResumesOnTheCapturedSynchronizationContextAtBothSteps()
    {
        var probe = new ContinuationProbe();
        var disposalGate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        AsyncDisposableFixture.Gate = disposalGate;
        using var harness = new Harness(typeof(AsyncDisposableFixture), nameof(AsyncDisposableFixture.Run));

        // The default lease awaits the pending DisposeAsync of the test class instance.
        Task<TestResult> runner = probe.Run(() => harness.MethodInfo.InvokeAsync(null));

        runner.IsCompleted.Should().BeFalse();

        disposalGate.SetResult(true);
        await WaitFor(runner);

        (await runner).Outcome.Should().Be(UnitTestOutcome.Passed);
        probe.PostCount.Should().Be(2, "asynchronous disposal and its enclosing cleanup must resume on the test's captured synchronization context");
    }
#endif

    private static async Task WaitFor(Task task)
    {
        Task completed = await Task.WhenAny(task, Task.Delay(WaitTimeout));
        completed.Should().BeSameAs(task, "the awaited operation timed out");
        await task;
    }

    private sealed class Harness : IDisposable
    {
        private readonly TestContextImplementation _testContext;
        private readonly IDisposable _testContextScope;

        public Harness(Type fixtureType, string testMethodName)
        {
            AssemblyInfo = new TestAssemblyInfo(fixtureType.Assembly);
            var testMethod = new TestMethod("dummyTestName", fixtureType.FullName!, "dummyAssemblyName", displayName: null);
            _testContext = new TestContextImplementation(testMethod, null, new Dictionary<string, object?>(), null, null);
            _testContextScope = TestContextImplementation.SetCurrentTestContext(_testContext);
            ClassInfo = new TestClassInfo(fixtureType, fixtureType.GetConstructor(Type.EmptyTypes), true, new TestClassAttribute(), AssemblyInfo);
            MethodInfo = new TestMethodInfo(fixtureType.GetMethod(testMethodName)!, ClassInfo)
            {
                TestContext = _testContext,
                TimeoutInfo = TimeoutInfo.FromTimeout(0),
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

    private sealed class PendingDisposalFactory(Task disposal) : ITestClassInstanceFactory
    {
        public ITestClassInstanceLease CreateInstance(Type testClassType, TestContext testContext)
            => new PendingDisposalLease(Activator.CreateInstance(testClassType)!, disposal);
    }

    private sealed class PendingDisposalLease(object instance, Task disposal) : ITestClassInstanceLease
    {
        public object Instance { get; } = instance;

        public bool RequiresCleanup => true;

        public Task DisposeAsync()
            => disposal;
    }

    private sealed class GlobalInitializeFixture
    {
        public static TaskCompletionSource<bool>? Gate { get; set; }

        public static Task GlobalInitialize(TestContext testContext)
            => Gate!.Task;

        public void Run()
        {
        }
    }

    private sealed class BaseTestInitializeFixture
    {
        public static TaskCompletionSource<bool>? Gate { get; set; }

        public Task BaseInitialize()
            => Gate!.Task;

        public void Run()
        {
        }
    }

    private sealed class TestInitializeFixture
    {
        public static TaskCompletionSource<bool>? Gate { get; set; }

        public Task Initialize()
            => Gate!.Task;

        public void Run()
        {
        }
    }

    private sealed class BaseTestCleanupFixture
    {
        public static TaskCompletionSource<bool>? Gate { get; set; }

        public Task BaseCleanup()
            => Gate!.Task;

        public void Run()
        {
        }
    }

    private sealed class LeaseFixture
    {
        public void Run()
        {
        }
    }

    private sealed class ThrowingTestContextFixture
    {
        public TestContext? TestContext
        {
            get => null;
            set
            {
                _ = value;
                throw new InvalidOperationException("The test context cannot be set.");
            }
        }

        public void Run()
        {
        }
    }

#if NET6_0_OR_GREATER
    private sealed class AsyncDisposableFixture : IAsyncDisposable
    {
        public static TaskCompletionSource<bool>? Gate { get; set; }

        public ValueTask DisposeAsync()
            => new(Gate!.Task);

        public void Run()
        {
        }
    }
#endif
}
