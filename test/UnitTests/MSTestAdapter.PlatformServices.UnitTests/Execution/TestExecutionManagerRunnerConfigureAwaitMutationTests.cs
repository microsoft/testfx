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

public sealed class TestExecutionManagerRunnerConfigureAwaitMutationTests : TestContainer
{
    private const string Settings =
        """
        <RunSettings>
          <RunConfiguration>
            <DisableAppDomain>True</DisableAppDomain>
          </RunConfiguration>
        </RunSettings>
        """;

    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

    public TestExecutionManagerRunnerConfigureAwaitMutationTests()
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

    public async Task SendTestResultsAsync_WithoutResults_NeverResumesOnTheCapturedSynchronizationContext()
    {
        var probe = new ContinuationProbe();
        var recorder = new GatedRecorder(PendingOperation.RecordEmptyResult);
        var manager = new TestExecutionManager();
        UnitTestElement test = CreateElement<PassingTestClass>(nameof(PassingTestClass.Pass));

        Task send = probe.Run(() => manager.SendTestResultsAsync(test, [], DateTimeOffset.Now, DateTimeOffset.Now, recorder));
        await AssertSuspendedAsync(send, recorder);
        recorder.Release();
        await WaitFor(send);

        probe.PostCount.Should().Be(0, "reporting an empty result must not capture the caller's synchronization context");
    }

    public async Task SendTestResultsAsync_WithResults_NeverResumesOnTheCapturedSynchronizationContext()
    {
        var probe = new ContinuationProbe();
        var recorder = new GatedRecorder(PendingOperation.RecordResult);
        var manager = new TestExecutionManager();
        UnitTestElement test = CreateElement<PassingTestClass>(nameof(PassingTestClass.Pass));
        FrameworkTestResult[] results = [new FrameworkTestResult { Outcome = UnitTestOutcome.Passed }];

        Task send = probe.Run(() => manager.SendTestResultsAsync(test, results, DateTimeOffset.Now, DateTimeOffset.Now, recorder));
        await AssertSuspendedAsync(send, recorder);
        recorder.Release();
        await WaitFor(send);

        probe.PostCount.Should().Be(0, "reporting a result must not capture the caller's synchronization context");
    }

    public async Task RunTestsAsync_WhenRecordingTheStartOfATestIsPending_NeverResumesOnTheCapturedSynchronizationContext()
    {
        var probe = new ContinuationProbe();
        var recorder = new GatedRecorder(PendingOperation.RecordStart);

        Task run = StartRun(probe, recorder, CreateElement<PassingTestClass>(nameof(PassingTestClass.Pass)));
        await AssertSuspendedAsync(run, recorder);
        recorder.Release();
        await WaitFor(run);

        probe.PostCount.Should().Be(0, "recording the start of a test must not capture the caller's synchronization context");
    }

    public async Task RunTestsAsync_WhenTheTestCleanupIsPending_NeverResumesOnTheCapturedSynchronizationContext()
    {
        var probe = new ContinuationProbe();
        var recorder = new GatedRecorder(PendingOperation.None);
        var gate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        PendingTestCleanupClass.Reset();
        PendingTestCleanupClass.Gate = gate;

        try
        {
            // The test initialize and the test method are intentionally awaited on the captured context (see TestMethodInfo.Execution.cs),
            // so the test cleanup is the deepest operation of a test run that must not capture it.
            Task run = StartRun(probe, recorder, CreateElement<PendingTestCleanupClass>(nameof(PendingTestCleanupClass.Run)));
            await AssertNotFaultedAsync(run);
            PendingTestCleanupClass.Started.Should().Be(1, "the run must reach the pending test cleanup");
            run.IsCompleted.Should().BeFalse("the run is expected to wait for the test cleanup");
            gate.SetResult(true);
            await WaitFor(run);

            probe.PostCount.Should().Be(0, "running a test must not capture the caller's synchronization context");
        }
        finally
        {
            PendingTestCleanupClass.Reset();
        }
    }

    public async Task RunTestsAsync_WhenRecordingTheResultOfATestIsPending_NeverResumesOnTheCapturedSynchronizationContext()
    {
        var probe = new ContinuationProbe();
        var recorder = new GatedRecorder(PendingOperation.RecordResult);

        Task run = StartRun(probe, recorder, CreateElement<PassingTestClass>(nameof(PassingTestClass.Pass)));
        await AssertSuspendedAsync(run, recorder);
        recorder.Release();
        await WaitFor(run);

        probe.PostCount.Should().Be(0, "recording the result of a test must not capture the caller's synchronization context");
    }

    public async Task RunTestsAsync_WhenRecordingTheStartOfASkippedDependentIsPending_NeverResumesOnTheCapturedSynchronizationContext()
    {
        var probe = new ContinuationProbe();
        var recorder = new GatedRecorder(PendingOperation.RecordStart, nameof(SkippedDependentClass.Dependent));

        Task run = StartRun(probe, recorder, CreateSkippedDependentElements());
        await AssertSuspendedAsync(run, recorder);
        recorder.Release();
        await WaitFor(run);

        probe.PostCount.Should().Be(0, "recording the start of a skipped dependent must not capture the caller's synchronization context");
    }

    public async Task RunTestsAsync_WhenNotifyingThatASkippedDependentDidNotRunIsPending_NeverResumesOnTheCapturedSynchronizationContext()
    {
        var probe = new ContinuationProbe();
        var recorder = new GatedRecorder(PendingOperation.None);
        var gate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        SkippedDependentClass.Reset();
        SkippedDependentClass.ClassCleanupGate = gate;

        try
        {
            // The skipped dependent is the last test of its class, so notifying that it did not run executes the class cleanup.
            Task run = StartRun(probe, recorder, CreateSkippedDependentElements());
            await AssertNotFaultedAsync(run);
            SkippedDependentClass.ClassCleanupStarted.Should().Be(1, "the run must reach the pending class cleanup");
            run.IsCompleted.Should().BeFalse("the run is expected to wait for the class cleanup");
            gate.SetResult(true);
            await WaitFor(run);

            probe.PostCount.Should().Be(0, "notifying that a test did not run must not capture the caller's synchronization context");
        }
        finally
        {
            SkippedDependentClass.Reset();
        }
    }

    public async Task RunTestsAsync_WhenRecordingTheResultOfASkippedDependentIsPending_NeverResumesOnTheCapturedSynchronizationContext()
    {
        var probe = new ContinuationProbe();
        var recorder = new GatedRecorder(PendingOperation.RecordResult, nameof(SkippedDependentClass.Dependent));

        Task run = StartRun(probe, recorder, CreateSkippedDependentElements());
        await AssertSuspendedAsync(run, recorder);
        recorder.Release();
        await WaitFor(run);

        probe.PostCount.Should().Be(0, "recording the result of a skipped dependent must not capture the caller's synchronization context");
    }

    private static UnitTestElement CreateElement<T>(string methodName)
        => new(new TestMethod(methodName, hierarchyValues: null, methodName, typeof(T).FullName!, Assembly.GetExecutingAssembly().Location, displayName: null, null));

    private static UnitTestElement[] CreateSkippedDependentElements()
    {
        UnitTestElement prerequisite = CreateElement<SkippedDependentClass>(nameof(SkippedDependentClass.Prereq));
        UnitTestElement dependent = CreateElement<SkippedDependentClass>(nameof(SkippedDependentClass.Dependent));
        dependent.Dependencies = [new TestDependencyInfo(typeof(SkippedDependentClass).FullName, nameof(SkippedDependentClass.Prereq), proceedOnFailure: false)];
        return [prerequisite, dependent];
    }

    // The tests are started on the probe's thread, so that the awaits of the runner execute while its context is current.
    // The inline task factory keeps the whole run on that thread until an operation is genuinely pending.
    private static Task StartRun(ContinuationProbe probe, ITestResultRecorder recorder, params UnitTestElement[] tests)
    {
        IAdapterMessageLogger logger = new Mock<IAdapterMessageLogger>().Object;
        MSTestSettings.PopulateSettings(Settings, logger, null);
        var manager = new TestExecutionManager(task => task());
        return probe.Run(() => manager.RunTestsAsync(tests, new DeploymentContext(null, Settings), logger, recorder, filterProvider: null, new TestRunCancellationToken()));
    }

    private static async Task AssertSuspendedAsync(Task call, GatedRecorder recorder)
    {
        await AssertNotFaultedAsync(call);
        call.IsCompleted.Should().BeFalse("the call is expected to wait for the pending recorder operation");
        recorder.Reached.Should().BeTrue("the call must reach the pending recorder operation");
    }

    private static async Task AssertNotFaultedAsync(Task call)
    {
        if (call.IsFaulted)
        {
            await call;
        }
    }

    private static async Task WaitFor(Task task)
    {
        Task completed = await Task.WhenAny(task, Task.Delay(Timeout));
        completed.Should().BeSameAs(task, "the awaited operation timed out");
        await task;
    }

    private enum PendingOperation
    {
        None,
        RecordStart,
        RecordEmptyResult,
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
            => ShouldPend(PendingOperation.RecordEmptyResult, testElement) ? _gate.Task : Task.CompletedTask;

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
    private sealed class PassingTestClass
    {
        [TestMethod]
        public void Pass()
        {
        }
    }

    [DummyTestClass]
    private sealed class PendingTestCleanupClass
    {
        private static int s_started;

        public static TaskCompletionSource<bool>? Gate { get; set; }

        public static int Started => Volatile.Read(ref s_started);

        public static void Reset()
        {
            Gate = null;
            Volatile.Write(ref s_started, 0);
        }

        [TestCleanup]
        public Task Cleanup()
        {
            Interlocked.Increment(ref s_started);
            return Gate?.Task ?? Task.CompletedTask;
        }

        [TestMethod]
        public void Run()
        {
        }
    }

    [DummyTestClass]
    private sealed class SkippedDependentClass
    {
        private static int s_classCleanupStarted;

        public static TaskCompletionSource<bool>? ClassCleanupGate { get; set; }

        public static int ClassCleanupStarted => Volatile.Read(ref s_classCleanupStarted);

        public static void Reset()
        {
            ClassCleanupGate = null;
            Volatile.Write(ref s_classCleanupStarted, 0);
        }

        [ClassCleanup]
        public static Task ClassCleanup()
        {
            Interlocked.Increment(ref s_classCleanupStarted);
            return ClassCleanupGate?.Task ?? Task.CompletedTask;
        }

        [TestMethod]
        public void Prereq() => throw new InvalidOperationException("The prerequisite fails on purpose.");

        [TestMethod]
        public void Dependent()
        {
        }
    }

    private sealed class DummyTestClassAttribute : TestClassAttribute;
}
