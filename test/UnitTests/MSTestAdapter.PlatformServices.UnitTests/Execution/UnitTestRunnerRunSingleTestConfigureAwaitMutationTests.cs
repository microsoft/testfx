// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Reflection.Emit;

using AwesomeAssertions;

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.SourceGeneration;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.TestableImplementations;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using MSTestAdapter.PlatformServices.UnitTests.Helpers;

using TestFramework.ForTestingMSTest;

namespace MSTestAdapter.PlatformServices.UnitTests.Execution;

public sealed class UnitTestRunnerRunSingleTestConfigureAwaitMutationTests : TestContainer
{
    private const string Source = "configure-await-probe-source";
    private const string FilteredSource = "configure-await-filtered-source";
    private const string AssemblyInitializeMethod = nameof(LifecycleProbe.AssemblyInitialize);
    private const string AssemblyCleanupMethod = nameof(LifecycleProbe.AssemblyCleanup);
    private const string ClassInitializeMethod = nameof(LifecycleProbe.ClassInitialize);
    private const string ClassCleanupMethod = nameof(LifecycleProbe.ClassCleanup);
    private const string FirstTestMethod = nameof(LifecycleProbe.FirstTest);
    private const string SecondTestMethod = nameof(LifecycleProbe.SecondTest);

    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    private readonly Dictionary<string, object?> _testRunParameters = [];
    private readonly Dictionary<string, Func<int, Task>> _behaviors = [];
    private readonly Dictionary<string, int> _calls = [];
    private readonly TestablePlatformServiceProvider _testablePlatformServiceProvider;

    public UnitTestRunnerRunSingleTestConfigureAwaitMutationTests()
    {
        _testablePlatformServiceProvider = new TestablePlatformServiceProvider();
        PlatformServiceProvider.Instance = _testablePlatformServiceProvider;
    }

    protected override void Dispose(bool disposing)
    {
        if (!IsDisposed)
        {
            base.Dispose(disposing);
            AssertionFailureSettings.CaptureDiagnosticsOnFailure = null;
            PlatformServiceProvider.Instance = null;
        }
    }

    public async Task RunSingleTestAsync_WithPendingAssemblyInitialize_NeverResumesOnTheCapturedSynchronizationContext()
    {
        RegisterProbe();
        TaskCompletionSource<bool> gate = Pend(AssemblyInitializeMethod);
        UnitTestElement element = CreateElement(FirstTestMethod);
        UnitTestRunner runner = CreateRunner(element);

        TestResult[] results = await RunProbedAsync(
            () => runner.RunSingleTestAsync(element, _testRunParameters, null!),
            gate,
            AssemblyInitializeMethod,
            expectedCalls: 1,
            "a pending assembly initialization must not capture the caller's synchronization context");

        results.Should().ContainSingle().Which.Outcome.Should().Be(UnitTestOutcome.Passed);
    }

    public async Task RunSingleTestAsync_WithPendingClassInitialize_NeverResumesOnTheCapturedSynchronizationContext()
    {
        RegisterProbe();
        TaskCompletionSource<bool> gate = Pend(ClassInitializeMethod);
        UnitTestElement element = CreateElement(FirstTestMethod);
        UnitTestRunner runner = CreateRunner(element);

        TestResult[] results = await RunProbedAsync(
            () => runner.RunSingleTestAsync(element, _testRunParameters, null!),
            gate,
            ClassInitializeMethod,
            expectedCalls: 1,
            "a pending class initialization must not capture the caller's synchronization context");

        results.Should().ContainSingle().Which.Outcome.Should().Be(UnitTestOutcome.Passed);
    }

    public async Task RunSingleTestAsync_WithPendingTestMethod_NeverResumesOnTheCapturedSynchronizationContext()
    {
        RegisterProbe();
        TaskCompletionSource<bool> gate = Pend(FirstTestMethod);
        UnitTestElement element = CreateElement(FirstTestMethod);
        UnitTestRunner runner = CreateRunner(element);

        TestResult[] results = await RunProbedAsync(
            () => runner.RunSingleTestAsync(element, _testRunParameters, null!),
            gate,
            FirstTestMethod,
            expectedCalls: 1,
            "a pending test method must not capture the caller's synchronization context");

        results.Should().ContainSingle().Which.Outcome.Should().Be(UnitTestOutcome.Passed);
    }

    public async Task RunSingleTestAsync_WithPendingRetryAttempt_NeverResumesOnTheCapturedSynchronizationContext()
    {
        RegisterProbe(new PassthroughRetryAttribute());
        TaskCompletionSource<bool> gate = CreateGate();
        _behaviors[FirstTestMethod] = call => call == 1
            ? Task.FromException(new InvalidOperationException("the first attempt fails"))
            : gate.Task;
        UnitTestElement element = CreateElement(FirstTestMethod);
        UnitTestRunner runner = CreateRunner(element);

        TestResult[] results = await RunProbedAsync(
            () => runner.RunSingleTestAsync(element, _testRunParameters, null!),
            gate,
            FirstTestMethod,
            expectedCalls: 2,
            "a pending retry attempt must not capture the caller's synchronization context");

        results.Select(result => result.Outcome).Should().Equal(UnitTestOutcome.Failed, UnitTestOutcome.Passed);
    }

    public async Task RunSingleTestAsync_WithPendingClassCleanup_NeverResumesOnTheCapturedSynchronizationContext()
    {
        RegisterProbe();
        TaskCompletionSource<bool> gate = Pend(ClassCleanupMethod);
        UnitTestElement element = CreateElement(FirstTestMethod);
        UnitTestRunner runner = CreateRunner(element);

        TestResult[] results = await RunProbedAsync(
            () => runner.RunSingleTestAsync(element, _testRunParameters, null!),
            gate,
            ClassCleanupMethod,
            expectedCalls: 1,
            "a pending class cleanup must not capture the caller's synchronization context");

        results.Should().ContainSingle().Which.Outcome.Should().Be(UnitTestOutcome.Passed);
    }

    public async Task RunSingleTestAsync_WithPendingAssemblyCleanup_NeverResumesOnTheCapturedSynchronizationContext()
    {
        RegisterProbe();
        TaskCompletionSource<bool> gate = Pend(AssemblyCleanupMethod);
        UnitTestElement element = CreateElement(FirstTestMethod);
        UnitTestRunner runner = CreateRunner(element);

        TestResult[] results = await RunProbedAsync(
            () => runner.RunSingleTestAsync(element, _testRunParameters, null!),
            gate,
            AssemblyCleanupMethod,
            expectedCalls: 1,
            "a pending assembly cleanup must not capture the caller's synchronization context");

        results.Should().ContainSingle().Which.Outcome.Should().Be(UnitTestOutcome.Passed);
    }

    public async Task NotifyTestNotRunAsync_WithPendingClassCleanup_NeverResumesOnTheCapturedSynchronizationContext()
    {
        RegisterProbe();
        UnitTestElement first = CreateElement(FirstTestMethod);
        UnitTestElement second = CreateElement(SecondTestMethod);
        UnitTestRunner runner = CreateRunner(first, second);
        await RunToCompletionAsync(runner, first);
        TaskCompletionSource<bool> gate = Pend(ClassCleanupMethod);

        TestResult[] results = await RunProbedAsync(
            () => runner.NotifyTestNotRunAsync(second, _testRunParameters, _testRunParameters, null!),
            gate,
            ClassCleanupMethod,
            expectedCalls: 1,
            "a pending class cleanup owed by a test that did not run must not capture the caller's synchronization context");

        results.Should().BeEmpty();
    }

    public async Task NotifyTestNotRunAsync_WithPendingAssemblyCleanup_NeverResumesOnTheCapturedSynchronizationContext()
    {
        RegisterProbe();
        UnitTestElement first = CreateElement(FirstTestMethod);
        UnitTestElement second = CreateElement(SecondTestMethod);
        UnitTestRunner runner = CreateRunner(first, second);
        await RunToCompletionAsync(runner, first);
        TaskCompletionSource<bool> gate = Pend(AssemblyCleanupMethod);

        TestResult[] results = await RunProbedAsync(
            () => runner.NotifyTestNotRunAsync(second, _testRunParameters, _testRunParameters, null!),
            gate,
            AssemblyCleanupMethod,
            expectedCalls: 1,
            "a pending assembly cleanup owed by a test that did not run must not capture the caller's synchronization context");

        results.Should().BeEmpty();
    }

    public async Task RunSingleTestAsync_WithFilteredOutTestAndPendingClassCleanup_NeverResumesOnTheCapturedSynchronizationContext()
    {
        RegisterProbe();
        RegisterDropFilterAssembly();
        UnitTestElement first = CreateElement(FirstTestMethod);
        UnitTestElement filteredOut = CreateElement(SecondTestMethod, FilteredSource);
        UnitTestRunner runner = CreateRunner(first, filteredOut);
        await RunToCompletionAsync(runner, first);
        TaskCompletionSource<bool> gate = Pend(ClassCleanupMethod);

        TestResult[] results = await RunProbedAsync(
            () => runner.RunSingleTestAsync(filteredOut, _testRunParameters, null!),
            gate,
            ClassCleanupMethod,
            expectedCalls: 1,
            "a pending class cleanup owed by a filtered-out test must not capture the caller's synchronization context");

        results.Should().BeEmpty();
    }

    private static UnitTestElement CreateElement(string methodName, string source = Source)
        => new(new TestMethod(methodName, null, methodName, typeof(LifecycleProbe).FullName!, source, null, null));

    private static UnitTestRunner CreateRunner(params UnitTestElement[] elements)
        => new(new MSTestSettings(), elements);

    private static TaskCompletionSource<bool> CreateGate()
        => new(TaskCreationOptions.RunContinuationsAsynchronously);

    private static async Task<T> WaitFor<T>(Task<T> task)
    {
        Task completed = await Task.WhenAny(task, Task.Delay(Timeout));
        completed.Should().BeSameAs(task, "the awaited operation timed out");
        return await task;
    }

    private Task Invoke(string method)
    {
        int call = _calls.TryGetValue(method, out int previous) ? previous + 1 : 1;
        _calls[method] = call;
        return _behaviors.TryGetValue(method, out Func<int, Task>? behavior) ? behavior(call) : Task.CompletedTask;
    }

    private TaskCompletionSource<bool> Pend(string method)
    {
        TaskCompletionSource<bool> gate = CreateGate();
        _behaviors[method] = _ => gate.Task;
        return gate;
    }

    private int CallCount(string method)
        => _calls.TryGetValue(method, out int count) ? count : 0;

    private async Task RunToCompletionAsync(UnitTestRunner runner, UnitTestElement element)
    {
        TestResult[] results = await runner.RunSingleTestAsync(element, _testRunParameters, null!);

        results.Should().ContainSingle().Which.Outcome.Should().Be(UnitTestOutcome.Passed);
    }

    private async Task<TestResult[]> RunProbedAsync(Func<Task<TestResult[]>> start, TaskCompletionSource<bool> gate, string pendingMethod, int expectedCalls, string because)
    {
        var probe = new ContinuationProbe();
        Task<TestResult[]> run = probe.Run(start);

        run.IsCompleted.Should().BeFalse($"{pendingMethod} is pending, so the run must still be suspended on it");
        CallCount(pendingMethod).Should().Be(expectedCalls, $"the run must be suspended on the expected {pendingMethod} invocation");

        gate.SetResult(true);
        TestResult[] results = await WaitFor(run);

        probe.PostCount.Should().Be(0, because);
        return results;
    }

    // The lifecycle metadata is served by a source-generated reflection provider that routes every invocation through
    // Invoke, so each fixture (and the test body) can be made to stay pending.
    private void RegisterProbe(params Attribute[] firstTestAttributes)
    {
        Type type = typeof(LifecycleProbe);
        Dictionary<MethodInfo, Func<object?, object?[]?, object?>> invokers = type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
            .ToDictionary<MethodInfo, MethodInfo, Func<object?, object?[]?, object?>>(
                method => method,
                method => (_, _) => Invoke(method.Name));
        _testablePlatformServiceProvider.SetReflectionOperations(new SourceGeneratedReflectionOperations(
            new SourceGeneratedReflectionDataProvider
            {
                Assembly = type.Assembly,
                AssemblyName = type.Assembly.GetName().Name!,
                Types = [type],
                TypesByName = new Dictionary<string, Type> { [type.FullName!] = type },
                AssemblyAttributes = [],
                TypeAttributes = new Dictionary<Type, Attribute[]>
                {
                    [type] = [new TestClassAttribute()],
                },
                TypeMethodAttributes = new Dictionary<MethodInfo, Attribute[]>
                {
                    [type.GetMethod(FirstTestMethod)!] = [new ScriptedTestMethodAttribute(() => Invoke(FirstTestMethod)), .. firstTestAttributes],
                    [type.GetMethod(SecondTestMethod)!] = [new ScriptedTestMethodAttribute(() => Invoke(SecondTestMethod))],
                    [type.GetMethod(AssemblyInitializeMethod)!] = [new AssemblyInitializeAttribute()],
                    [type.GetMethod(AssemblyCleanupMethod)!] = [new AssemblyCleanupAttribute()],
                    [type.GetMethod(ClassInitializeMethod)!] = [new ClassInitializeAttribute()],
                    [type.GetMethod(ClassCleanupMethod)!] = [new ClassCleanupAttribute()],
                },
                TypeMethodInvokers = invokers,
                TypeConstructorsInvoker = new Dictionary<Type, SourceGeneratedReflectionDataProvider.ConstructorInvoker[]>
                {
                    [type] =
                    [
                        new()
                        {
                            Invoker = _ => new LifecycleProbe(),
                        },
                    ],
                },
            }));
        _testablePlatformServiceProvider.MockFileOperations.Setup(value => value.LoadAssembly(Source)).Returns(type.Assembly);
    }

    // Lifecycle attributes live only in the metadata registered by RegisterProbe. Putting real
    // AssemblyInitialize/Cleanup attributes here would pollute other runner tests
    // that intentionally inspect this unit-test assembly with ordinary reflection.
    private sealed class LifecycleProbe
    {
        public static void AssemblyInitialize(TestContext context)
        {
        }

        public static void AssemblyCleanup()
        {
        }

        public static void ClassInitialize(TestContext context)
        {
        }

        public static void ClassCleanup()
        {
        }

        public void FirstTest()
        {
        }

        public void SecondTest()
        {
        }
    }

    // TestMethodInfo awaits the test initialization and the test body with ConfigureAwait(true) on purpose (UI thread affinity),
    // so a pending test body legitimately resumes on the captured synchronization context. This attribute stands in for the
    // body, which keeps the awaits probed here limited to the ones owned by the runner.
    private sealed class ScriptedTestMethodAttribute : TestMethodAttribute
    {
        private readonly Func<Task> _execute;

        public ScriptedTestMethodAttribute(Func<Task> execute)
            => _execute = execute;

        public override async Task<TestResult[]> ExecuteAsync(ITestMethod testMethod)
        {
            try
            {
                await _execute().ConfigureAwait(false);
            }
            catch (InvalidOperationException ex)
            {
                return [new TestResult { Outcome = UnitTestOutcome.Failed, TestFailureException = ex }];
            }

            return [new TestResult { Outcome = UnitTestOutcome.Passed }];
        }
    }

#pragma warning disable MSTESTEXP // The retry and programmatic test-filter APIs are experimental; exercised here under test.
    private void RegisterDropFilterAssembly()
    {
        AssemblyName name = new("ConfigureAwaitDropFilter" + Guid.NewGuid().ToString("N"));
#if NETFRAMEWORK
        AssemblyBuilder assembly = AppDomain.CurrentDomain.DefineDynamicAssembly(name, AssemblyBuilderAccess.Run);
#else
        var assembly = AssemblyBuilder.DefineDynamicAssembly(name, AssemblyBuilderAccess.Run);
#endif
        ConstructorInfo constructor = typeof(TestFilterProviderAttribute).GetConstructor([typeof(Type)])!;
        assembly.SetCustomAttribute(new CustomAttributeBuilder(constructor, [typeof(DropFilter)]));
        _testablePlatformServiceProvider.MockFileOperations.Setup(value => value.LoadAssembly(FilteredSource)).Returns(assembly);
    }

    [AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
    private sealed class PassthroughRetryAttribute : RetryBaseAttribute
    {
        protected internal override async Task<RetryResult> ExecuteAsync(RetryContext retryContext)
        {
            var result = new RetryResult();
            result.AddResult(await retryContext.ExecuteTaskGetter().ConfigureAwait(false));
            return result;
        }
    }

    public sealed class DropFilter : ITestFilter
    {
        public TestFilterResult Filter(TestFilterContext context) => TestFilterResult.Drop;
    }
#pragma warning restore MSTESTEXP
}
