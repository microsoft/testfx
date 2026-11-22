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

public sealed class TestMethodInfoConfigureAwaitMutationTests : TestContainer
{
    private static readonly TimeSpan WaitTimeout = TimeSpan.FromSeconds(10);

    public async Task InvokeAsync_WithExecutionContextAndPendingTestMethod_NeverResumesOnTheCapturedSynchronizationContext()
    {
        var probe = new ContinuationProbe();
        var testMethodGate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        ExecutionContextFixture.Gate = testMethodGate;
        using var harness = new Harness(typeof(ExecutionContextFixture), nameof(ExecutionContextFixture.Run));

        // A global initializer with a non-cooperative timeout makes the test run on the execution context captured after it.
        // The caller blocks while the initializer runs on the thread pool, so nothing is pending before the test method itself.
        harness.AssemblyInfo.GlobalTestInitializations.Add((typeof(ExecutionContextFixture).GetMethod(nameof(ExecutionContextFixture.GlobalInitialize))!, TimeoutInfo.FromTimeout(60_000)));

        // The test method has no timeout, so the pending test method suspends the invocation, the execution context callback and the test run.
        Task<TestResult> runner = probe.Run(() => harness.MethodInfo.InvokeAsync(null));

        runner.IsCompleted.Should().BeFalse();

        testMethodGate.SetResult(true);
        await WaitFor(runner);

        (await runner).Outcome.Should().Be(UnitTestOutcome.Passed);
        probe.PostCount.Should().Be(0, "the test invocation must not capture the caller's synchronization context");
    }

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
            var classInfo = new TestClassInfo(fixtureType, fixtureType.GetConstructor(Type.EmptyTypes), true, new TestClassAttribute(), AssemblyInfo);
            MethodInfo = new TestMethodInfo(fixtureType.GetMethod(testMethodName)!, classInfo)
            {
                TestContext = _testContext,
                TimeoutInfo = TimeoutInfo.FromTimeout(0),
                Executor = new TestMethodAttribute(),
            };
        }

        public TestAssemblyInfo AssemblyInfo { get; }

        public TestMethodInfo MethodInfo { get; }

        public void Dispose()
        {
            _testContextScope.Dispose();
            _testContext.Dispose();
        }
    }

    private sealed class ExecutionContextFixture
    {
        public static TaskCompletionSource<bool>? Gate { get; set; }

        public static Task GlobalInitialize(TestContext testContext)
            => Task.CompletedTask;

        public Task Run()
            => Gate!.Task;
    }
}
