// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using AwesomeAssertions;

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Execution;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Resources;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.TestableImplementations;

using TestFramework.ForTestingMSTest;

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Execution;

public sealed class TestMethodInfoLifecycleMutationTests : TestContainer
{
    public async Task RunTestCleanupMethodAsyncDoesNothingUntilTheTestContextSetupHasCompleted()
    {
        using var harness = new LifecycleHarness();
        harness.ClassInfo.TestCleanupMethod = harness.FixtureMethod(nameof(LifecycleFixture.Cleanup));
        object state = harness.CreateInvocationState(harness.Fixture, lease: null, testContextSetSuccessful: null);
        TestResult result = CreatePassedResult();

        bool cleanupExecuted = await harness.RunTestCleanupAsync(result, timeoutTokenSource: null, state);

        cleanupExecuted.Should().BeFalse(because: "the cleanup must wait until the outcome of the TestContext setup is known");
        harness.IsCleanupStarted(state).Should().BeFalse(because: "a deferred cleanup must not claim the cleanup");
        harness.Calls.Should().BeEmpty();
        result.Outcome.Should().Be(UnitTestOutcome.Passed);
    }

    public async Task RunTestCleanupMethodAsyncReturnsTrueAndRenewsTheCancelledTokenSourceWhenThereIsNothingToClean()
    {
        using var harness = new LifecycleHarness();
        object state = harness.CreateInvocationState(harness.Fixture, lease: null, testContextSetSuccessful: true);
        CancellationTokenSource cancelledSource = harness.Context.CancellationTokenSource;
        cancelledSource.Cancel();
        TestResult result = CreatePassedResult();

        bool cleanupExecuted = await harness.RunTestCleanupAsync(result, timeoutTokenSource: null, state);

        cleanupExecuted.Should().BeTrue(because: "renewing the cancelled token source is a cleanup step even without any cleanup method");
        harness.Context.CancellationTokenSource.Should().NotBeSameAs(cancelledSource);
        harness.Context.CancellationTokenSource.IsCancellationRequested.Should().BeFalse();
        cancelledSource.IsCancellationRequested.Should().BeTrue();
        harness.IsCleanupStarted(state).Should().BeTrue();
        harness.Calls.Should().BeEmpty();
        result.Outcome.Should().Be(UnitTestOutcome.Passed);
    }

    public async Task RunTestCleanupMethodAsyncDisposesTheLeaseWithoutRunningCleanupMethodsWhenTheTestContextSetupFailed()
    {
        using var harness = new LifecycleHarness();
        harness.ClassInfo.TestCleanupMethod = harness.FixtureMethod(nameof(LifecycleFixture.Cleanup));
        var lease = new RecordingLease(harness.Fixture, requiresCleanup: true);
        object state = harness.CreateInvocationState(harness.Fixture, lease, testContextSetSuccessful: false);
        TestResult result = CreatePassedResult();

        bool cleanupExecuted = await harness.RunTestCleanupAsync(result, timeoutTokenSource: null, state);

        cleanupExecuted.Should().BeTrue(because: "disposing the lease is the whole cleanup when the TestContext could not be set");
        lease.DisposeCount.Should().Be(1);
        harness.Calls.Should().BeEmpty(because: "the cleanup methods must not run when the TestContext setup failed");
        result.Outcome.Should().Be(UnitTestOutcome.Passed);
        result.TestFailureException.Should().BeNull();
    }

    public async Task RunTestCleanupMethodAsyncStopsAtTheFirstBaseCleanupMethodThatReportsAFailure()
    {
        using var harness = new LifecycleHarness();
        harness.ClassInfo.BaseTestCleanupMethodsQueue.Add(harness.FixtureMethod(nameof(LifecycleFixture.BaseCleanupA)));
        harness.ClassInfo.BaseTestCleanupMethodsQueue.Add(harness.FixtureMethod(nameof(LifecycleFixture.BaseCleanupB)));
        harness.Fixture.Behaviors[nameof(LifecycleFixture.BaseCleanupA)] = harness.Fixture.CancelContextAndThrow;
        object state = harness.CreateInvocationState(harness.Fixture, lease: null, testContextSetSuccessful: true);
        TestResult result = CreatePassedResult();

        bool cleanupExecuted = await harness.RunTestCleanupAsync(result, timeoutTokenSource: null, state);

        cleanupExecuted.Should().BeTrue();
        harness.Calls.Should().Equal(nameof(LifecycleFixture.BaseCleanupA));
        result.Outcome.Should().Be(UnitTestOutcome.Timeout);
        result.TestFailureException!.Message.Should().Be(
            CleanupFailureMessage(nameof(LifecycleFixture.BaseCleanupA), CancelledCleanupMessage(nameof(LifecycleFixture.BaseCleanupA))),
            because: "the failure of the first base cleanup method must not be replaced by the outcome of the next one");
    }

    public async Task RunTestCleanupMethodAsyncAttributesCancellationToTheClassCleanupMethodThatCancelledTheTestContext()
    {
        using var harness = new LifecycleHarness();
        harness.ClassInfo.TestCleanupMethod = harness.FixtureMethod(nameof(LifecycleFixture.Cleanup));
        harness.AssemblyInfo.GlobalTestCleanups.Add((harness.FixtureMethod(nameof(LifecycleFixture.GlobalCleanup)), null));
        harness.Fixture.Behaviors[nameof(LifecycleFixture.Cleanup)] = harness.Fixture.CancelContext;
        object state = harness.CreateInvocationState(harness.Fixture, lease: null, testContextSetSuccessful: true);
        TestResult result = CreatePassedResult();

        bool cleanupExecuted = await harness.RunTestCleanupAsync(result, timeoutTokenSource: null, state);

        cleanupExecuted.Should().BeTrue();
        harness.Calls.Should().Equal(nameof(LifecycleFixture.Cleanup), nameof(LifecycleFixture.GlobalCleanup));
        result.Outcome.Should().Be(UnitTestOutcome.Timeout);
        result.TestFailureException!.Message.Should().Be(
            CleanupFailureMessage(nameof(LifecycleFixture.Cleanup), CancelledCleanupMessage(nameof(LifecycleFixture.Cleanup))));
        harness.Context.CancellationTokenSource.IsCancellationRequested.Should().BeFalse(because: "the global cleanups run with a renewed token source");
    }

    public async Task RunTestCleanupMethodAsyncAttributesCancellationToTheGlobalCleanupWhenOnlyTheLeaseCancelledTheTestContext()
    {
        using var harness = new LifecycleHarness();
        harness.AssemblyInfo.GlobalTestCleanups.Add((harness.FixtureMethod(nameof(LifecycleFixture.GlobalCleanup)), null));
        var lease = new RecordingLease(harness.Fixture, requiresCleanup: true) { OnDispose = harness.Fixture.CancelContext };
        object state = harness.CreateInvocationState(harness.Fixture, lease, testContextSetSuccessful: true);
        TestResult result = CreatePassedResult();

        bool cleanupExecuted = await harness.RunTestCleanupAsync(result, timeoutTokenSource: null, state);

        cleanupExecuted.Should().BeTrue();
        lease.DisposeCount.Should().Be(1);
        harness.Calls.Should().Equal(nameof(LifecycleFixture.GlobalCleanup));
        result.Outcome.Should().Be(UnitTestOutcome.Timeout);
        string globalCancelledMessage = string.Format(
            CultureInfo.InvariantCulture,
            Resource.GlobalTestCleanupWasCancelled,
            FixtureTypeName,
            nameof(LifecycleFixture.GlobalCleanup));
        result.TestFailureException!.Message.Should().Be(CleanupFailureMessage(nameof(LifecycleFixture.GlobalCleanup), globalCancelledMessage));
    }

    public async Task RunTestCleanupMethodAsyncReportsTheClassCleanupMethodWhenOnlyDisposingTheLeaseFails()
    {
        using var harness = new LifecycleHarness();
        harness.ClassInfo.TestCleanupMethod = harness.FixtureMethod(nameof(LifecycleFixture.Cleanup));
        var lease = new RecordingLease(harness.Fixture, requiresCleanup: true) { DisposeException = new InvalidOperationException("lease dispose failed") };
        object state = harness.CreateInvocationState(harness.Fixture, lease, testContextSetSuccessful: true);
        TestResult result = CreatePassedResult();

        bool cleanupExecuted = await harness.RunTestCleanupAsync(result, timeoutTokenSource: null, state);

        cleanupExecuted.Should().BeTrue();
        lease.DisposeCount.Should().Be(1);
        harness.Calls.Should().Equal(nameof(LifecycleFixture.Cleanup));
        result.Outcome.Should().Be(UnitTestOutcome.Failed);
        string expectedMessage = string.Format(
            CultureInfo.CurrentCulture,
            Resource.UTA_CleanupMethodThrows,
            FixtureTypeName,
            nameof(LifecycleFixture.Cleanup),
            "System.InvalidOperationException: lease dispose failed");
        result.TestFailureException!.Message.Should().Be(expectedMessage);
    }

    public async Task RunTestCleanupMethodAsyncReturnsTrueWithoutFailureWhenAllCleanupsSucceed()
    {
        using var harness = new LifecycleHarness();
        harness.ClassInfo.TestCleanupMethod = harness.FixtureMethod(nameof(LifecycleFixture.Cleanup));
        object state = harness.CreateInvocationState(harness.Fixture, lease: null, testContextSetSuccessful: true);
        TestResult result = CreatePassedResult();

        bool cleanupExecuted = await harness.RunTestCleanupAsync(result, timeoutTokenSource: null, state);

        cleanupExecuted.Should().BeTrue();
        harness.Calls.Should().Equal(nameof(LifecycleFixture.Cleanup));
        harness.IsCleanupStarted(state).Should().BeTrue();
        result.Outcome.Should().Be(UnitTestOutcome.Passed);
        result.TestFailureException.Should().BeNull();
    }

    public void HasCleanupsToInvokeIsOnlyTrueWhenAtLeastOneSourceOfCleanupExists()
    {
        var cases = new (string Name, Func<LifecycleHarness, ITestClassInstanceLease?> Configure, bool Expected)[]
        {
            ("nothing is configured", _ => null, false),
            (
                "a class cleanup method exists",
                harness =>
                {
                    harness.ClassInfo.TestCleanupMethod = harness.FixtureMethod(nameof(LifecycleFixture.Cleanup));
                    return null;
                },
                true),
            (
                "a base cleanup method exists",
                harness =>
                {
                    harness.ClassInfo.BaseTestCleanupMethodsQueue.Add(harness.FixtureMethod(nameof(LifecycleFixture.BaseCleanupA)));
                    return null;
                },
                true),
            (
                "a global cleanup method exists",
                harness =>
                {
                    harness.AssemblyInfo.GlobalTestCleanups.Add((harness.FixtureMethod(nameof(LifecycleFixture.GlobalCleanup)), null));
                    return null;
                },
                true),
            ("the lease requires cleanup", harness => new RecordingLease(harness.Fixture, requiresCleanup: true), true),
            ("the lease does not require cleanup", harness => new RecordingLease(harness.Fixture, requiresCleanup: false), false),
        };

        foreach ((string name, Func<LifecycleHarness, ITestClassInstanceLease?> configure, bool expected) in cases)
        {
            using var harness = new LifecycleHarness();
            ITestClassInstanceLease? lease = configure(harness);

            harness.HasCleanupsToInvoke(lease).Should().Be(expected, because: name);
        }
    }

    public async Task RunTestInitializeMethodAsyncStopsAtTheFirstBaseInitializeMethodThatReportsAFailure()
    {
        using var harness = new LifecycleHarness();
        harness.ClassInfo.BaseTestInitializeMethodsQueue.Add(harness.FixtureMethod(nameof(LifecycleFixture.BaseInitA)));
        harness.ClassInfo.BaseTestInitializeMethodsQueue.Add(harness.FixtureMethod(nameof(LifecycleFixture.BaseInitB)));
        harness.Fixture.Behaviors[nameof(LifecycleFixture.BaseInitB)] = harness.Fixture.CancelContextAndThrow;
        TestResult result = CreatePassedResult();

        bool initialized = await harness.RunTestInitializeAsync(harness.Fixture, result, timeoutTokenSource: null);

        initialized.Should().BeFalse();
        harness.Calls.Should().Equal(nameof(LifecycleFixture.BaseInitB));
        result.Outcome.Should().Be(UnitTestOutcome.Timeout);
        string expectedMessage = string.Format(
            CultureInfo.InvariantCulture,
            Resource.TestInitializeWasCancelled,
            FixtureTypeName,
            nameof(LifecycleFixture.BaseInitB));
        result.TestFailureException!.Message.Should().Be(
            expectedMessage,
            because: "the failure of the first initialize method must not be replaced by the outcome of the next one");
    }

    public async Task InvokeInitializeMethodAsyncAppliesTheConfiguredInitializeTimeout()
    {
        using var harness = new LifecycleHarness();
        MethodInfo initialize = harness.FixtureMethod(nameof(LifecycleFixture.Init));
        harness.ClassInfo.TestInitializeMethodTimeoutMilliseconds[initialize] = CreateImmediateCooperativeTimeout();

        TestFailedException? failure = await harness.InvokeInitializeAsync(initialize, harness.Fixture, timeoutTokenSource: null);

        failure.Should().NotBeNull();
        failure!.Outcome.Should().Be(UnitTestOutcome.Timeout);
        string expectedMessage = string.Format(
            CultureInfo.InvariantCulture,
            Resource.TestInitializeTimedOut,
            FixtureTypeName,
            nameof(LifecycleFixture.Init),
            0);
        failure.Message.Should().Be(expectedMessage);
        harness.Calls.Should().BeEmpty(because: "a zero timeout expires before the initialize method can start");
    }

    public async Task InvokeCleanupMethodAsyncAppliesTheConfiguredCleanupTimeout()
    {
        using var harness = new LifecycleHarness();
        MethodInfo cleanup = harness.FixtureMethod(nameof(LifecycleFixture.Cleanup));
        harness.ClassInfo.TestCleanupMethodTimeoutMilliseconds[cleanup] = CreateImmediateCooperativeTimeout();

        TestFailedException? failure = await harness.InvokeCleanupAsync(cleanup, harness.Fixture, timeoutTokenSource: null);

        failure.Should().NotBeNull();
        failure!.Outcome.Should().Be(UnitTestOutcome.Timeout);
        string expectedMessage = string.Format(
            CultureInfo.InvariantCulture,
            Resource.TestCleanupTimedOut,
            FixtureTypeName,
            nameof(LifecycleFixture.Cleanup),
            0);
        failure.Message.Should().Be(expectedMessage);
        harness.Calls.Should().BeEmpty(because: "a zero timeout expires before the cleanup method can start");
    }

    public async Task InvokeInitializeMethodAsyncRecordsAnActivityWithoutFailureWhenTheMethodSucceeds()
    {
        using var harness = new LifecycleHarness();
        harness.InstallActivityFactory();

        TestFailedException? failure = await harness.InvokeInitializeAsync(harness.FixtureMethod(nameof(LifecycleFixture.Init)), harness.Fixture, timeoutTokenSource: null);

        failure.Should().BeNull();
        harness.Calls.Should().Equal(nameof(LifecycleFixture.Init));
        harness.Activities.Should().HaveCount(1);
        AssertFixtureActivity(harness.Activities[0], "MSTest.TestInitialize", "test_initialize");
        harness.Activities[0].Exceptions.Should().BeEmpty();
    }

    public async Task InvokeInitializeMethodAsyncRecordsTheFailureOnTheActivityWhenTheMethodIsCancelled()
    {
        using var harness = new LifecycleHarness();
        harness.InstallActivityFactory();
        harness.Context.CancellationTokenSource.Cancel();

        TestFailedException? failure = await harness.InvokeInitializeAsync(harness.FixtureMethod(nameof(LifecycleFixture.Init)), harness.Fixture, timeoutTokenSource: null);

        failure.Should().NotBeNull();
        harness.Calls.Should().BeEmpty();
        harness.Activities.Should().HaveCount(1);
        AssertFixtureActivity(harness.Activities[0], "MSTest.TestInitialize", "test_initialize");
        harness.Activities[0].Exceptions.Should().HaveCount(1);
        harness.Activities[0].Exceptions[0].Should().BeSameAs(failure);
    }

    public async Task InvokeCleanupMethodAsyncRecordsAnActivityWithoutFailureWhenTheMethodSucceeds()
    {
        using var harness = new LifecycleHarness();
        harness.InstallActivityFactory();

        TestFailedException? failure = await harness.InvokeCleanupAsync(harness.FixtureMethod(nameof(LifecycleFixture.Cleanup)), harness.Fixture, timeoutTokenSource: null);

        failure.Should().BeNull();
        harness.Calls.Should().Equal(nameof(LifecycleFixture.Cleanup));
        harness.Activities.Should().HaveCount(1);
        AssertFixtureActivity(harness.Activities[0], "MSTest.TestCleanup", "test_cleanup");
        harness.Activities[0].Exceptions.Should().BeEmpty();
    }

    public async Task InvokeCleanupMethodAsyncRecordsTheFailureOnTheActivityWhenTheMethodIsCancelled()
    {
        using var harness = new LifecycleHarness();
        harness.InstallActivityFactory();
        harness.Context.CancellationTokenSource.Cancel();

        TestFailedException? failure = await harness.InvokeCleanupAsync(harness.FixtureMethod(nameof(LifecycleFixture.Cleanup)), harness.Fixture, timeoutTokenSource: null);

        failure.Should().NotBeNull();
        harness.Calls.Should().BeEmpty();
        harness.Activities.Should().HaveCount(1);
        AssertFixtureActivity(harness.Activities[0], "MSTest.TestCleanup", "test_cleanup");
        harness.Activities[0].Exceptions.Should().HaveCount(1);
        harness.Activities[0].Exceptions[0].Should().BeSameAs(failure);
    }

    public void CaptureExecutionContextAfterFixtureIfNeededKeepsThePreviousContextWhenNothingCanBeCaptured()
    {
        using var harness = new LifecycleHarness();
        var previous = ExecutionContext.Capture();
        previous.Should().NotBeNull();
        harness.SetExecutionContext(previous);

        harness.InvokeCaptureExecutionContextWithSuppressedFlow(timeoutInfo: null);

        harness.GetExecutionContext().Should().BeSameAs(previous, because: "a flow-suppressed capture returns null and must not erase the previously captured context");
    }

    private static TestResult CreatePassedResult() => new() { Outcome = UnitTestOutcome.Passed };

    private static TimeoutInfo CreateImmediateCooperativeTimeout()
        => TimeoutInfo.FromTimeoutAttribute(new TimeoutAttribute(0) { CooperativeCancellation = true });

    private static string FixtureTypeName => typeof(LifecycleFixture).FullName!;

    private static string CancelledCleanupMessage(string methodName)
        => string.Format(CultureInfo.InvariantCulture, Resource.TestCleanupWasCancelled, FixtureTypeName, methodName);

    private static string CleanupFailureMessage(string methodName, string innerMessage)
        => string.Format(
            CultureInfo.CurrentCulture,
            Resource.UTA_CleanupMethodThrows,
            FixtureTypeName,
            methodName,
            $"{typeof(TestFailedException).FullName}: {innerMessage}");

    private static void AssertFixtureActivity(RecordedActivity activity, string expectedName, string expectedFixtureKind)
    {
        activity.Name.Should().Be(expectedName);
        string[] expectedTags =
        [
            $"test.fixture.kind={expectedFixtureKind}",
            $"test.suite.name={FixtureTypeName}",
            $"test.assembly.name={typeof(LifecycleFixture).Assembly.GetName().Name}",
        ];
        activity.Tags.Select(tag => $"{tag.Key}={tag.Value}").Should().Equal(expectedTags);
        activity.IsDisposed.Should().BeTrue(because: "the activity is scoped to the fixture invocation");
    }

    private sealed class LifecycleHarness : IDisposable
    {
        private static readonly Type InvocationStateType = typeof(TestMethodInfo).GetNestedType("TestInvocationState", BindingFlags.NonPublic)!;
        private static readonly MethodInfo RunTestCleanupMethod = GetPrivateMethod("RunTestCleanupMethodAsync");
        private static readonly MethodInfo RunTestInitializeMethod = GetPrivateMethod("RunTestInitializeMethodAsync");
        private static readonly MethodInfo InvokeInitializeMethod = GetPrivateMethod("InvokeInitializeMethodAsync");
        private static readonly MethodInfo InvokeCleanupMethod = GetPrivateMethod("InvokeCleanupMethodAsync");
        private static readonly MethodInfo HasCleanupsToInvokeMethod = GetPrivateMethod("HasCleanupsToInvoke");
        private static readonly MethodInfo CaptureExecutionContextMethod = GetPrivateMethod("CaptureExecutionContextAfterFixtureIfNeeded");
        private static readonly FieldInfo ExecutionContextField = typeof(TestMethodInfo).GetField("_executionContext", BindingFlags.NonPublic | BindingFlags.Instance)!;

        private readonly IPlatformServiceProvider _previousProvider;

        public LifecycleHarness()
        {
            _previousProvider = PlatformServiceProvider.Instance;
            var services = new TestablePlatformServiceProvider();
            services.SetReflectionOperations(new ReflectionOperations());
            PlatformServiceProvider.Instance = services;

            Calls = [];
            Context = new TestContextImplementation(null, typeof(LifecycleFixture).FullName, new Dictionary<string, object?> { ["calls"] = Calls }, null, null);
            AssemblyInfo = new TestAssemblyInfo(typeof(LifecycleFixture).Assembly);
            ClassInfo = new TestClassInfo(typeof(LifecycleFixture), typeof(LifecycleFixture).GetConstructor(Type.EmptyTypes), true, new TestClassAttribute(), AssemblyInfo);
            Subject = new TestMethodInfo(typeof(LifecycleFixture).GetMethod(nameof(LifecycleFixture.TestMethod))!, ClassInfo) { TestContext = Context };
            Fixture = new LifecycleFixture { Calls = Calls, Context = Context };
        }

        public List<string> Calls { get; }

        public List<RecordedActivity> Activities { get; } = [];

        public TestContextImplementation Context { get; }

        public TestAssemblyInfo AssemblyInfo { get; }

        public TestClassInfo ClassInfo { get; }

        public TestMethodInfo Subject { get; }

        public LifecycleFixture Fixture { get; }

        public MethodInfo FixtureMethod(string name) => typeof(LifecycleFixture).GetMethod(name)!;

        public void InstallActivityFactory()
            => MSTestInstrumentation.SetActivityFactory((name, tags) =>
            {
                var activity = new RecordedActivity(name, tags?.ToList() ?? []);
                Activities.Add(activity);
                return activity;
            });

        public object CreateInvocationState(object? classInstance, ITestClassInstanceLease? lease, bool? testContextSetSuccessful)
        {
            object state = Activator.CreateInstance(InvocationStateType, nonPublic: true)!;
            if (classInstance is not null)
            {
                InvocationStateType.GetMethod("PublishActivation")!.Invoke(state, [classInstance, lease]);
            }

            if (testContextSetSuccessful is { } successful)
            {
                InvocationStateType.GetMethod("CompleteTestContextSetup")!.Invoke(state, [successful]);
            }

            return state;
        }

        public bool IsCleanupStarted(object state)
            => (bool)InvocationStateType.GetProperty("IsCleanupStarted")!.GetValue(state)!;

        public async Task<bool> RunTestCleanupAsync(TestResult result, CancellationTokenSource? timeoutTokenSource, object invocationState)
            => await (SynchronizationContextPreservingTask<bool>)RunTestCleanupMethod.Invoke(Subject, [result, timeoutTokenSource, invocationState])!;

        public async Task<bool> RunTestInitializeAsync(object? classInstance, TestResult result, CancellationTokenSource? timeoutTokenSource)
        {
            object?[] arguments = RunTestInitializeMethod.GetParameters().Length switch
            {
                3 => [classInstance, result, timeoutTokenSource],
                4 => [classInstance, result, timeoutTokenSource, CreateInvocationState(classInstance, lease: null, testContextSetSuccessful: true)],
                _ => throw new InvalidOperationException("Unexpected RunTestInitializeMethodAsync signature."),
            };

            return await (SynchronizationContextPreservingTask<bool>)RunTestInitializeMethod.Invoke(Subject, arguments)!;
        }

        public async Task<TestFailedException?> InvokeInitializeAsync(MethodInfo method, object? classInstance, CancellationTokenSource? timeoutTokenSource)
            => await (SynchronizationContextPreservingTask<TestFailedException?>)InvokeInitializeMethod.Invoke(Subject, [method, classInstance, timeoutTokenSource])!;

        public async Task<TestFailedException?> InvokeCleanupAsync(MethodInfo method, object? classInstance, CancellationTokenSource? timeoutTokenSource)
            => await (SynchronizationContextPreservingTask<TestFailedException?>)InvokeCleanupMethod.Invoke(Subject, [method, classInstance, timeoutTokenSource])!;

        public bool HasCleanupsToInvoke(ITestClassInstanceLease? lease)
            => (bool)HasCleanupsToInvokeMethod.Invoke(Subject, [lease])!;

        public object? GetExecutionContext() => ExecutionContextField.GetValue(Subject);

        public void SetExecutionContext(object? executionContext) => ExecutionContextField.SetValue(Subject, executionContext);

        public void InvokeCaptureExecutionContextWithSuppressedFlow(TimeoutInfo? timeoutInfo)
        {
            using (ExecutionContext.SuppressFlow())
            {
                CaptureExecutionContextMethod.Invoke(Subject, [timeoutInfo]);
            }
        }

        public void Dispose()
        {
            MSTestInstrumentation.SetActivityFactory(null);
            PlatformServiceProvider.Instance = _previousProvider;
        }

        private static MethodInfo GetPrivateMethod(string name)
            => typeof(TestMethodInfo).GetMethod(name, BindingFlags.NonPublic | BindingFlags.Instance)!;
    }

    private sealed class LifecycleFixture
    {
        public List<string> Calls { get; set; } = [];

        public TestContextImplementation? Context { get; set; }

        public Dictionary<string, Action> Behaviors { get; } = [];

        public static void GlobalCleanup(TestContext context)
            => ((List<string>)context.Properties["calls"]!).Add(nameof(GlobalCleanup));

        public void Init() => Run(nameof(Init));

        public void BaseInitA() => Run(nameof(BaseInitA));

        public void BaseInitB() => Run(nameof(BaseInitB));

        public void Cleanup() => Run(nameof(Cleanup));

        public void BaseCleanupA() => Run(nameof(BaseCleanupA));

        public void BaseCleanupB() => Run(nameof(BaseCleanupB));

        public void TestMethod() => Run(nameof(TestMethod));

        public void CancelContext() => Context!.CancellationTokenSource.Cancel();

        public void CancelContextAndThrow()
        {
            Context!.CancellationTokenSource.Cancel();
            throw new OperationCanceledException(Context.CancellationTokenSource.Token);
        }

        private void Run(string name)
        {
            Calls.Add(name);
            if (Behaviors.TryGetValue(name, out Action? behavior))
            {
                behavior();
            }
        }
    }

    private sealed class RecordingLease : ITestClassInstanceLease
    {
        public RecordingLease(object instance, bool requiresCleanup)
        {
            Instance = instance;
            RequiresCleanup = requiresCleanup;
        }

        public bool RequiresCleanup { get; }

        public object Instance { get; }

        public int DisposeCount { get; private set; }

        public Exception? DisposeException { get; set; }

        public Action? OnDispose { get; set; }

        public Task DisposeAsync()
        {
            DisposeCount++;
            OnDispose?.Invoke();
            return DisposeException is null ? Task.CompletedTask : Task.FromException(DisposeException);
        }
    }

    private sealed class RecordedActivity : IMSTestActivity
    {
        public RecordedActivity(string name, List<KeyValuePair<string, object?>> tags)
        {
            Name = name;
            Tags = tags;
        }

        public string Name { get; }

        public List<KeyValuePair<string, object?>> Tags { get; }

        public List<Exception?> Exceptions { get; } = [];

        public bool IsDisposed { get; private set; }

        public void SetTag(string key, object? value)
        {
        }

        public void SetFailed(string? description)
        {
        }

        public void RecordException(Exception exception) => Exceptions.Add(exception);

        public void Dispose() => IsDisposed = true;
    }
}
