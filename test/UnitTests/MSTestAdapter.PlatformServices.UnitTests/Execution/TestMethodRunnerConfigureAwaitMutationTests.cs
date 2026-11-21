// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using AwesomeAssertions;

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.TestableImplementations;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using MSTestAdapter.PlatformServices.UnitTests.Helpers;

using TestFramework.ForTestingMSTest;

namespace MSTestAdapter.PlatformServices.UnitTests.Execution;

public sealed class TestMethodRunnerConfigureAwaitMutationTests : TestContainer
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    private readonly MethodInfo _methodInfo;
    private readonly MethodInfo _parameterizedMethodInfo;
    private readonly TestMethod _testMethod;
    private readonly TestContextImplementation _testContext;
    private readonly TestClassInfo _testClassInfo;
    private readonly TestablePlatformServiceProvider _testablePlatformServiceProvider;

    public TestMethodRunnerConfigureAwaitMutationTests()
    {
        _methodInfo = typeof(DummyTestClass).GetMethod(nameof(DummyTestClass.DummyTestMethod))!;
        _parameterizedMethodInfo = typeof(DummyTestClass).GetMethod(nameof(DummyTestClass.DummyParameterizedTestMethod))!;
        _testMethod = CreateTestMethod();
        _testContext = CreateTestContext(_testMethod);
        _testClassInfo = new TestClassInfo(
            typeof(DummyTestClass),
            typeof(DummyTestClass).GetConstructor([])!,
            isParameterlessConstructor: true,
            new TestClassAttribute(),
            new TestAssemblyInfo(typeof(DummyTestClass).Assembly));

        _testablePlatformServiceProvider = new TestablePlatformServiceProvider();
        _testablePlatformServiceProvider.SetupMockReflectionOperations();
        PlatformServiceProvider.Instance = _testablePlatformServiceProvider;
    }

    protected override void Dispose(bool disposing)
    {
        if (!IsDisposed)
        {
            base.Dispose(disposing);
            PlatformServiceProvider.Instance = null;
        }
    }

    public async Task ExecuteAsync_ForPlainTestMethod_NeverResumesOnTheCapturedSynchronizationContext()
    {
        var executor = new GatedTestMethodAttribute();
        var runner = new TestMethodRunner(CreateTestMethodInfo(_methodInfo, executor), _testMethod, _testContext);

        TestResult[] results = await ExecuteProbedAsync(runner, executor, "the plain test method path must not capture the caller's synchronization context");

        results.Should().ContainSingle().Which.Outcome.Should().Be(UnitTestOutcome.Passed);
    }

    public async Task ExecuteAsync_WithExecutionActivityLease_NeverResumesOnTheCapturedSynchronizationContext()
    {
        var executor = new GatedTestMethodAttribute();
        var lease = new PassThroughLease();
        var runner = new TestMethodRunner(CreateTestMethodInfo(_methodInfo, executor), _testMethod, _testContext, lease);

        TestResult[] results = await ExecuteProbedAsync(runner, executor, "the leased test method path must not capture the caller's synchronization context");

        results.Should().ContainSingle().Which.Outcome.Should().Be(UnitTestOutcome.Passed);
        lease.Calls.Should().Be(1);
    }

    public async Task ExecuteAsync_WithCapturedExecutionContext_NeverResumesOnTheCapturedSynchronizationContext()
    {
        _testClassInfo.ExecutionContext = ExecutionContext.Capture();
        var executor = new GatedTestMethodAttribute();
        var runner = new TestMethodRunner(CreateTestMethodInfo(_methodInfo, executor), _testMethod, _testContext);

        TestResult[] results = await ExecuteProbedAsync(runner, executor, "the captured execution context path must not capture the caller's synchronization context");

        results.Should().ContainSingle().Which.Outcome.Should().Be(UnitTestOutcome.Passed);
    }

    public async Task ExecuteAsync_WithCapturedExecutionContextAndExecutionActivityLease_NeverResumesOnTheCapturedSynchronizationContext()
    {
        _testClassInfo.ExecutionContext = ExecutionContext.Capture();
        var executor = new GatedTestMethodAttribute();
        var lease = new PassThroughLease();
        var runner = new TestMethodRunner(CreateTestMethodInfo(_methodInfo, executor), _testMethod, _testContext, lease);

        TestResult[] results = await ExecuteProbedAsync(runner, executor, "the leased captured execution context path must not capture the caller's synchronization context");

        results.Should().ContainSingle().Which.Outcome.Should().Be(UnitTestOutcome.Passed);
        lease.Calls.Should().Be(1);
    }

    public async Task ExecuteAsync_WithDataDiscoveredDuringDiscovery_NeverResumesOnTheCapturedSynchronizationContext()
    {
        TestMethod testMethod = CreateTestMethod();
        testMethod.DataType = DynamicDataType.ITestDataSource;
        testMethod.ActualData = [42];
        var executor = new GatedTestMethodAttribute();
        var runner = new TestMethodRunner(CreateTestMethodInfo(_parameterizedMethodInfo, executor), testMethod, CreateTestContext(testMethod));

        TestResult[] results = await ExecuteProbedAsync(runner, executor, "the discovered data path must not capture the caller's synchronization context");

        results.Should().ContainSingle().Which.Outcome.Should().Be(UnitTestOutcome.Passed);
    }

    public async Task ExecuteAsync_WithDataSourceAttribute_NeverResumesOnTheCapturedSynchronizationContext()
    {
        _testablePlatformServiceProvider.MockReflectionOperations
            .Setup(reflection => reflection.GetCustomAttributes(_methodInfo))
            .Returns(new Attribute[] { new DataSourceAttribute("DummyConnectionString", "DummyTableName") });
        var executor = new GatedTestMethodAttribute();
        TestMethodInfo info = CreateTestMethodInfo(_methodInfo, executor);
        _testablePlatformServiceProvider.MockTestDataSource
            .Setup(dataSource => dataSource.GetData(info, _testContext))
            .Returns([1]);
        var runner = new TestMethodRunner(info, _testMethod, _testContext);

        TestResult[] results = await ExecuteProbedAsync(runner, executor, "the DataSource attribute path must not capture the caller's synchronization context");

        results.Should().ContainSingle().Which.Outcome.Should().Be(UnitTestOutcome.Passed);
    }

    public async Task ExecuteAsync_WithDataRowAttribute_NeverResumesOnTheCapturedSynchronizationContext()
    {
        _testablePlatformServiceProvider.MockReflectionOperations
            .Setup(reflection => reflection.GetCustomAttributes(_parameterizedMethodInfo))
            .Returns(new Attribute[] { new DataRowAttribute(1) });
        var executor = new GatedTestMethodAttribute();
        var runner = new TestMethodRunner(CreateTestMethodInfo(_parameterizedMethodInfo, executor), _testMethod, _testContext);

        TestResult[] results = await ExecuteProbedAsync(runner, executor, "the folded data-driven path must not capture the caller's synchronization context");

        results.Should().ContainSingle().Which.Outcome.Should().Be(UnitTestOutcome.Passed);
    }

    public async Task ExecuteAsync_WithDuplicateDataSourceAttributes_NeverResumesOnTheCapturedSynchronizationContext()
    {
        _testablePlatformServiceProvider.MockReflectionOperations
            .Setup(reflection => reflection.GetCustomAttributes(_methodInfo))
            .Returns(new Attribute[] { new DataSourceAttribute("first", "table"), new DataSourceAttribute("second", "table") });
        var executor = new GatedTestMethodAttribute();
        var runner = new TestMethodRunner(CreateTestMethodInfo(_methodInfo, executor), _testMethod, _testContext);

        TestResult[] results = await ExecuteProbedAsync(runner, executor, "the duplicate DataSource fallback path must not capture the caller's synchronization context");

        results.Should().ContainSingle().Which.Outcome.Should().Be(UnitTestOutcome.Passed);
    }

    public async Task RunTestMethodAsync_WithDataDiscoveredDuringDiscovery_DoesNotUnwrapTestDataRowsAgain()
    {
        TestMethod testMethod = CreateTestMethod();
        var row = new TestDataRow<int>(5) { IgnoreMessage = "ignored by the data row" };
        testMethod.DataType = DynamicDataType.ITestDataSource;
        testMethod.ActualData = [row];
        var executor = new GatedTestMethodAttribute();
        executor.Release();
        TestMethodInfo info = CreateTestMethodInfo(_parameterizedMethodInfo, executor);
        var runner = new TestMethodRunner(info, testMethod, CreateTestContext(testMethod));

        TestResult[] results = await runner.RunTestMethodAsync();

        executor.Calls.Should().Be(1, "data that was already handled during discovery must be passed to the test method as is");
        results.Should().ContainSingle().Which.Outcome.Should().Be(UnitTestOutcome.Passed);
        info.Arguments.Should().ContainSingle().Which.Should().BeSameAs(row);
    }

    private static TestMethod CreateTestMethod()
        => new("dummyTestName", "dummyClassName", "dummyAssemblyName", displayName: null);

    private static TestContextImplementation CreateTestContext(TestMethod testMethod)
        => new(testMethod, null, new Dictionary<string, object?>(), null, null);

    private static async Task<TestResult[]> ExecuteProbedAsync(TestMethodRunner runner, GatedTestMethodAttribute executor, string because)
    {
        var probe = new ContinuationProbe();

        // The test method is gated, so every await up to the executor suspends while the probe context is current.
        Task<TestResult[]> run = probe.Run(() => runner.ExecuteAsync(string.Empty, string.Empty, string.Empty, string.Empty));

        run.IsCompleted.Should().BeFalse();
        executor.Calls.Should().Be(1, "the pending test method must be the operation the runner is awaiting");

        executor.Release();
        TestResult[] results = await WaitFor(run);

        probe.PostCount.Should().Be(0, because);
        return results;
    }

    private static async Task<T> WaitFor<T>(Task<T> task)
    {
        Task completed = await Task.WhenAny(task, Task.Delay(Timeout));
        completed.Should().BeSameAs(task, "the awaited operation timed out");
        return await task;
    }

    private TestMethodInfo CreateTestMethodInfo(MethodInfo methodInfo, TestMethodAttribute executor)
        => new(methodInfo, _testClassInfo)
        {
            TimeoutInfo = TimeoutInfo.FromTimeout(200),
            Executor = executor,
        };

    private sealed class GatedTestMethodAttribute : TestMethodAttribute
    {
        private readonly TaskCompletionSource<TestResult[]> _gate = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public int Calls { get; private set; }

        public void Release()
            => _gate.SetResult([new TestResult { Outcome = UnitTestOutcome.Passed }]);

        public override Task<TestResult[]> ExecuteAsync(ITestMethod testMethod)
        {
            Calls++;
            return _gate.Task;
        }
    }

    private sealed class PassThroughLease : ITestExecutionActivityLease
    {
        public int Calls { get; private set; }

        public Task<T> RunAsync<T>(Func<Task<T>> callback)
        {
            Calls++;
            return callback();
        }
    }

    private sealed class DummyTestClass
    {
        public void DummyTestMethod()
        {
        }

        public void DummyParameterizedTestMethod(object? value)
        {
        }
    }
}
