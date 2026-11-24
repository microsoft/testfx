// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using AwesomeAssertions;

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.TestableImplementations;

using Moq;

using TestFramework.ForTestingMSTest;

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Execution;

public sealed class TestMethodRunnerExecutionMutationTests : TestContainer
{
    // Only bounds a wait that would otherwise hang forever if the runner never completes the awaited task.
    private static readonly TimeSpan HangGuard = TimeSpan.FromSeconds(3);

    private readonly TestablePlatformServiceProvider _testablePlatformServiceProvider;
    private readonly MethodInfo _methodInfo;
    private readonly TestMethod _testMethod;
    private readonly TestContextImplementation _testContextImplementation;
    private readonly TestClassInfo _testClassInfo;

    public TestMethodRunnerExecutionMutationTests()
    {
        _testablePlatformServiceProvider = new TestablePlatformServiceProvider();
        _testablePlatformServiceProvider.SetupMockReflectionOperations();
        PlatformServiceProvider.Instance = _testablePlatformServiceProvider;

        _methodInfo = typeof(ExecutionSubject).GetMethod(nameof(ExecutionSubject.Method))!;
        _testMethod = new TestMethod(nameof(ExecutionSubject.Method), typeof(ExecutionSubject).FullName!, "dummyAssemblyName", displayName: null);
        _testContextImplementation = new TestContextImplementation(_testMethod, null, new Dictionary<string, object?>(), null, null);
        _testClassInfo = new TestClassInfo(
            typeof(ExecutionSubject),
            typeof(ExecutionSubject).GetConstructor([])!,
            isParameterlessConstructor: true,
            new TestClassAttribute(),
            new TestAssemblyInfo(typeof(ExecutionSubject).Assembly));
    }

    public async Task UnsupportedDeployment_FailsTheTestAndNotifiesTheContextOfTheFailureTwice()
    {
        _testablePlatformServiceProvider.MockTestDeployment
            .Setup(d => d.GetUnsupportedDeploymentMessage(It.IsAny<MethodInfo>(), It.IsAny<Type>()))
            .Returns("unsupported deployment");
        var reportedOutcomes = new List<UnitTestOutcome>();
        var testContext = new Mock<ITestContext>();
        testContext.SetupGet(c => c.Context).Returns(_testContextImplementation);
        testContext.Setup(c => c.SetOutcome(It.IsAny<UnitTestOutcome>())).Callback<UnitTestOutcome>(reportedOutcomes.Add);
        bool executed = false;
        TestMethodRunner runner = CreateRunner(
            _ =>
            {
                executed = true;
                return Passed();
            },
            testContext.Object);

        TestResult[] results = await runner.RunTestMethodAsync();

        executed.Should().BeFalse();
        results.Should().ContainSingle();
        results[0].Outcome.Should().Be(UnitTestOutcome.Failed);
        results[0].DisplayName.Should().Be(nameof(ExecutionSubject.Method));
        results[0].TestFailureException.Should().BeOfType<TestFailedException>().Which.Message.Should().Be("unsupported deployment");

        // Once when the deployment check fails and once more with the aggregated outcome of the run.
        reportedOutcomes.Should().Equal(UnitTestOutcome.Failed, UnitTestOutcome.Failed);
    }

    public async Task ExecuteTest_WhenClassAndAssemblyCapturedExecutionContexts_RunsOnTheClassContext()
    {
        var state = new AsyncLocal<string?>();
        _testClassInfo.Parent.ExecutionContext = CaptureWith(state, "assembly");
        _testClassInfo.ExecutionContext = CaptureWith(state, "class");
        string? observed = null;
        TestMethodRunner runner = CreateRunner(_ =>
        {
            observed = state.Value;
            return Passed();
        });

        TestResult[] results = await runner.RunTestMethodAsync();

        observed.Should().Be("class");
        results.Should().ContainSingle().Which.Outcome.Should().Be(UnitTestOutcome.Passed);
    }

    public async Task ExecuteTest_WhenOnlyTheAssemblyCapturedAnExecutionContext_RunsOnTheAssemblyContext()
    {
        var state = new AsyncLocal<string?>();
        _testClassInfo.Parent.ExecutionContext = CaptureWith(state, "assembly");
        string? observed = null;
        TestMethodRunner runner = CreateRunner(_ =>
        {
            observed = state.Value;
            return Passed();
        });

        TestResult[] results = await runner.RunTestMethodAsync();

        observed.Should().Be("assembly");
        results.Should().ContainSingle().Which.Outcome.Should().Be(UnitTestOutcome.Passed);
    }

    public async Task ExecuteTest_WithoutCapturedExecutionContext_RunsInsideTheActivityLease()
    {
        var lease = new FakeExecutionActivityLease();
        bool enteredDuringExecution = false;
        TestMethodRunner runner = CreateRunner(
            _ =>
            {
                enteredDuringExecution = lease.IsEntered;
                return Passed();
            },
            lease: lease);

        TestResult[] results = await runner.RunTestMethodAsync();

        enteredDuringExecution.Should().BeTrue();
        lease.EnterCount.Should().Be(1);
        lease.ExitCount.Should().Be(1);
        results.Should().ContainSingle().Which.Outcome.Should().Be(UnitTestOutcome.Passed);
    }

    public async Task ExecuteTest_WhenTheExecutorThrowsOnACapturedExecutionContext_ReportsAFailedResultInsteadOfHanging()
    {
        _testClassInfo.ExecutionContext = ExecutionContext.Capture();
        var thrown = new InvalidOperationException("boom");
        TestMethodRunner runner = CreateRunner(_ => throw thrown);

        Task<TestResult[]> run = runner.RunTestMethodAsync();
        Task completed = await Task.WhenAny(run, Task.Delay(HangGuard));

        completed.Should().BeSameAs(run);
        TestResult[] results = await run;
        results.Should().ContainSingle();
        Exception? failure = results[0].TestFailureException;
        failure.Should().BeOfType<InvalidOperationException>();
        failure!.InnerException.Should().BeSameAs(thrown);
        failure.Message.Should().Contain(typeof(DelegateExecutor).FullName).And.Contain("boom");
    }

    protected override void Dispose(bool disposing)
    {
        if (!IsDisposed)
        {
            base.Dispose(disposing);
            PlatformServiceProvider.Instance = null;
        }
    }

    private static Task<TestResult[]> Passed()
        => Task.FromResult<TestResult[]>([new TestResult { Outcome = UnitTestOutcome.Passed }]);

    private static ExecutionContext CaptureWith(AsyncLocal<string?> state, string value)
    {
        state.Value = value;
        try
        {
            return ExecutionContext.Capture()!;
        }
        finally
        {
            state.Value = null;
        }
    }

    private TestMethodRunner CreateRunner(Func<ITestMethod, Task<TestResult[]>> execute, ITestContext? testContext = null, ITestExecutionActivityLease? lease = null)
    {
        var testMethodInfo = new TestMethodInfo(_methodInfo, _testClassInfo) { Executor = new DelegateExecutor(execute) };

        return new TestMethodRunner(testMethodInfo, _testMethod, testContext ?? _testContextImplementation, lease);
    }

    public sealed class ExecutionSubject
    {
        public void Method()
        {
        }
    }

    private sealed class DelegateExecutor(Func<ITestMethod, Task<TestResult[]>> execute) : TestMethodAttribute
    {
        public override Task<TestResult[]> ExecuteAsync(ITestMethod testMethod) => execute(testMethod);
    }

    private sealed class FakeExecutionActivityLease : ITestExecutionActivityLease
    {
        public bool IsEntered { get; private set; }

        public int EnterCount { get; private set; }

        public int ExitCount { get; private set; }

        public async Task<T> RunAsync<T>(Func<Task<T>> callback)
        {
            EnterCount++;
            IsEntered = true;
            try
            {
                return await callback();
            }
            finally
            {
                ExitCount++;
                IsEntered = false;
            }
        }
    }
}
