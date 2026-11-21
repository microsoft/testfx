// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using AwesomeAssertions;

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Deployment;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface;

using Moq;

using MSTestAdapter.PlatformServices.UnitTests.Helpers;

using TestFramework.ForTestingMSTest;

using FrameworkTestResult = Microsoft.VisualStudio.TestTools.UnitTesting.TestResult;

namespace MSTestAdapter.PlatformServices.UnitTests.Execution;

// The awaits of the chunk scheduler for a chunk without resource locks, and of the sequential phase, are reached by the dependent tests
// cases of TestExecutionManagerSourceExecutionConfigureAwaitMutationTests. The cases below reach the remaining awaits of the dependency
// graph execution: the reporting of the tests that are part of a dependency cycle, and a chunk that holds resource locks.
public sealed class TestExecutionManagerDependencyGraphExecutionConfigureAwaitMutationTests : TestContainer
{
    private const string SequentialSettings =
        """
        <RunSettings>
          <RunConfiguration>
            <DisableAppDomain>True</DisableAppDomain>
          </RunConfiguration>
        </RunSettings>
        """;

    private const string ParallelSettings =
        """
        <RunSettings>
          <RunConfiguration>
            <DisableAppDomain>True</DisableAppDomain>
          </RunConfiguration>
          <MSTest>
            <Parallelize>
              <Workers>2</Workers>
              <Scope>MethodLevel</Scope>
            </Parallelize>
          </MSTest>
        </RunSettings>
        """;

    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    public TestExecutionManagerDependencyGraphExecutionConfigureAwaitMutationTests()
        => PlatformServiceProvider.Instance = null;

    protected override void Dispose(bool disposing)
    {
        if (!IsDisposed)
        {
            base.Dispose(disposing);
            PlatformServiceProvider.Instance = null;
            MSTestSettings.Reset();
        }
    }

    public async Task RunTestsAsync_WhenRecordingTheStartOfATestInACycleIsPending_NeverResumesOnTheCapturedSynchronizationContext()
    {
        var probe = new ContinuationProbe();
        var recorder = new GatedRecorder(PendingOperation.RecordStart);

        Task run = StartRun(probe, recorder, SequentialSettings, CreateCycleElements());
        await AssertSuspendedAsync(run, recorder);
        recorder.Release();
        await WaitFor(run);

        probe.PostCount.Should().Be(0, "recording the start of a test in a dependency cycle must not capture the caller's synchronization context");
    }

    public async Task RunTestsAsync_WhenRecordingTheResultOfATestInACycleIsPending_NeverResumesOnTheCapturedSynchronizationContext()
    {
        var probe = new ContinuationProbe();
        var recorder = new GatedRecorder(PendingOperation.RecordResult);

        Task run = StartRun(probe, recorder, SequentialSettings, CreateCycleElements());
        await AssertSuspendedAsync(run, recorder);
        recorder.Release();
        await WaitFor(run);

        probe.PostCount.Should().Be(0, "recording the result of a test in a dependency cycle must not capture the caller's synchronization context");
    }

    public async Task RunTestsAsync_WithPrerequisiteHoldingResourceLocksRunningInParallel_NeverResumesOnTheCapturedSynchronizationContext()
    {
        var probe = new ContinuationProbe();
        var recorder = new GatedRecorder(PendingOperation.RecordStart, nameof(DependencyTestsClass.First));
        UnitTestElement first = CreateElement(nameof(DependencyTestsClass.First));
        first.ResourceLocks = [new ResourceLockInfo("R", ResourceAccessMode.ReadWrite)];
        UnitTestElement second = CreateElement(nameof(DependencyTestsClass.Second));
        second.Dependencies = [CreateDependency(nameof(DependencyTestsClass.First))];

        Task run = StartRun(probe, recorder, ParallelSettings, first, second);
        await AssertSuspendedAsync(run, recorder);
        recorder.Release();
        await WaitFor(run);

        probe.PostCount.Should().Be(0, "running a chunk that holds resource locks must not capture the caller's synchronization context");
    }

    private static UnitTestElement CreateElement(string methodName)
        => new(new TestMethod(methodName, hierarchyValues: null, methodName, typeof(DependencyTestsClass).FullName!, Assembly.GetExecutingAssembly().Location, displayName: null, null));

    private static TestDependencyInfo CreateDependency(string methodName)
        => new(typeof(DependencyTestsClass).FullName, methodName, proceedOnFailure: false);

    private static UnitTestElement[] CreateCycleElements()
    {
        UnitTestElement inCycleA = CreateElement(nameof(DependencyTestsClass.InCycleA));
        inCycleA.Dependencies = [CreateDependency(nameof(DependencyTestsClass.InCycleB))];
        UnitTestElement inCycleB = CreateElement(nameof(DependencyTestsClass.InCycleB));
        inCycleB.Dependencies = [CreateDependency(nameof(DependencyTestsClass.InCycleA))];
        return [inCycleA, inCycleB];
    }

    // The tests are started on the probe's thread, so that the awaits of the scheduler execute while its context is current.
    // The inline task factory keeps the whole run on that thread until an operation is genuinely pending.
    private static Task StartRun(ContinuationProbe probe, ITestResultRecorder recorder, string settings, params UnitTestElement[] tests)
    {
        IAdapterMessageLogger logger = new Mock<IAdapterMessageLogger>().Object;
        MSTestSettings.PopulateSettings(settings, logger, null);
        var manager = new TestExecutionManager(task => task());
        return probe.Run(() => manager.RunTestsAsync(tests, new DeploymentContext(null, settings), logger, recorder, filterProvider: null, new TestRunCancellationToken()));
    }

    private static async Task AssertSuspendedAsync(Task run, GatedRecorder recorder)
    {
        if (run.IsFaulted)
        {
            await run;
        }

        run.IsCompleted.Should().BeFalse("the run is expected to wait for the pending recorder operation");
        recorder.Reached.Should().BeTrue("the run must reach the pending recorder operation");
    }

    private static async Task WaitFor(Task task)
    {
        Task completed = await Task.WhenAny(task, Task.Delay(Timeout));
        completed.Should().BeSameAs(task, "the awaited operation timed out");
        await task;
    }

    private enum PendingOperation
    {
        RecordStart,
        RecordResult,
    }

    private sealed class GatedRecorder : ITestResultRecorder
    {
        private readonly TaskCompletionSource<bool> _gate = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly PendingOperation _pendingOperation;
        private readonly string? _pendingTestName;
        private int _reached;

        public GatedRecorder(PendingOperation pendingOperation, string? pendingTestName = null)
        {
            _pendingOperation = pendingOperation;
            _pendingTestName = pendingTestName;
        }

        public bool Reached => Volatile.Read(ref _reached) == 1;

        public void Release() => _gate.TrySetResult(false);

        public void PrepareResults(UnitTestElement testElement, FrameworkTestResult[] results)
        {
        }

        public Task RecordStartAsync(UnitTestElement testElement)
            => ShouldPend(PendingOperation.RecordStart, testElement) ? _gate.Task : Task.CompletedTask;

        public Task RecordEmptyResultAsync(UnitTestElement testElement)
            => Task.CompletedTask;

        public Task<bool> RecordResultAsync(UnitTestElement testElement, FrameworkTestResult result, DateTimeOffset startTime, DateTimeOffset endTime)
            => ShouldPend(PendingOperation.RecordResult, testElement) ? _gate.Task : Task.FromResult(false);

        private bool ShouldPend(PendingOperation operation, UnitTestElement testElement)
        {
            if (operation != _pendingOperation || (_pendingTestName is not null && testElement.TestMethod.Name != _pendingTestName))
            {
                return false;
            }

            Volatile.Write(ref _reached, 1);
            return true;
        }
    }

    [DummyTestClass]
    private sealed class DependencyTestsClass
    {
        [TestMethod]
        public void First()
        {
        }

        [TestMethod]
        public void Second()
        {
        }

        [TestMethod]
        public void InCycleA()
        {
        }

        [TestMethod]
        public void InCycleB()
        {
        }
    }

    private sealed class DummyTestClassAttribute : TestClassAttribute;
}
