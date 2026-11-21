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

public sealed class TestExecutionManagerSourceExecutionConfigureAwaitMutationTests : TestContainer
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

    public TestExecutionManagerSourceExecutionConfigureAwaitMutationTests()
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

    public async Task RunTestsAsync_WithTestsRunningSequentially_NeverResumesOnTheCapturedSynchronizationContext()
    {
        var probe = new ContinuationProbe();
        var recorder = new GatedRecorder(nameof(OrderedTestsClass.First));

        Task run = StartRun(probe, recorder, SequentialSettings, CreateIndependentElements());
        await AssertSuspendedAsync(run, recorder);
        recorder.Release();
        await WaitFor(run);

        probe.PostCount.Should().Be(0, "running the tests of a source one after the other must not capture the caller's synchronization context");
    }

    public async Task RunTestsAsync_WithDependentTestsRunningSequentially_NeverResumesOnTheCapturedSynchronizationContext()
    {
        var probe = new ContinuationProbe();
        var recorder = new GatedRecorder(nameof(OrderedTestsClass.First));

        Task run = StartRun(probe, recorder, SequentialSettings, CreateDependentElements());
        await AssertSuspendedAsync(run, recorder);
        recorder.Release();
        await WaitFor(run);

        probe.PostCount.Should().Be(0, "running dependent tests one after the other must not capture the caller's synchronization context");
    }

    public async Task RunTestsAsync_WithTestsRunningInParallel_NeverResumesOnTheCapturedSynchronizationContext()
    {
        var probe = new ContinuationProbe();
        var recorder = new GatedRecorder(nameof(OrderedTestsClass.First));

        Task run = StartRun(probe, recorder, ParallelSettings, CreateIndependentElements());
        await AssertSuspendedAsync(run, recorder);
        recorder.Release();
        await WaitFor(run);

        probe.PostCount.Should().Be(0, "running the tests of a source in parallel must not capture the caller's synchronization context");
    }

    public async Task RunTestsAsync_WithDependentTestsRunningInParallel_NeverResumesOnTheCapturedSynchronizationContext()
    {
        var probe = new ContinuationProbe();
        var recorder = new GatedRecorder(nameof(OrderedTestsClass.First));

        Task run = StartRun(probe, recorder, ParallelSettings, CreateDependentElements());
        await AssertSuspendedAsync(run, recorder);
        recorder.Release();
        await WaitFor(run);

        probe.PostCount.Should().Be(0, "running dependent tests in parallel must not capture the caller's synchronization context");
    }

    private static UnitTestElement CreateElement(string methodName)
        => new(new TestMethod(methodName, hierarchyValues: null, methodName, typeof(OrderedTestsClass).FullName!, Assembly.GetExecutingAssembly().Location, displayName: null, null));

    private static UnitTestElement[] CreateIndependentElements()
        => [CreateElement(nameof(OrderedTestsClass.First)), CreateElement(nameof(OrderedTestsClass.Second))];

    private static UnitTestElement[] CreateDependentElements()
    {
        UnitTestElement first = CreateElement(nameof(OrderedTestsClass.First));
        UnitTestElement second = CreateElement(nameof(OrderedTestsClass.Second));
        second.Dependencies = [new TestDependencyInfo(typeof(OrderedTestsClass).FullName, nameof(OrderedTestsClass.First), proceedOnFailure: false)];
        return [first, second];
    }

    // The tests are started on the probe's thread, so that the awaits of the source execution run while its context is current.
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

        run.IsCompleted.Should().BeFalse("the run is expected to wait for the recording of the start of the first test");
        recorder.Reached.Should().BeTrue("the run must reach the recording of the start of the first test");
    }

    private static async Task WaitFor(Task task)
    {
        Task completed = await Task.WhenAny(task, Task.Delay(Timeout));
        completed.Should().BeSameAs(task, "the awaited operation timed out");
        await task;
    }

    private sealed class GatedRecorder : ITestResultRecorder
    {
        private readonly TaskCompletionSource<bool> _gate = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly string _pendingTestName;
        private int _reached;

        public GatedRecorder(string pendingTestName)
            => _pendingTestName = pendingTestName;

        public bool Reached => Volatile.Read(ref _reached) == 1;

        public void Release() => _gate.TrySetResult(false);

        public void PrepareResults(UnitTestElement testElement, FrameworkTestResult[] results)
        {
        }

        public Task RecordStartAsync(UnitTestElement testElement)
        {
            if (testElement.TestMethod.Name != _pendingTestName)
            {
                return Task.CompletedTask;
            }

            Volatile.Write(ref _reached, 1);
            return _gate.Task;
        }

        public Task RecordEmptyResultAsync(UnitTestElement testElement)
            => Task.CompletedTask;

        public Task<bool> RecordResultAsync(UnitTestElement testElement, FrameworkTestResult result, DateTimeOffset startTime, DateTimeOffset endTime)
            => Task.FromResult(false);
    }

    [DummyTestClass]
    private sealed class OrderedTestsClass
    {
        [TestMethod]
        public void First()
        {
        }

        [TestMethod]
        public void Second()
        {
        }
    }

    private sealed class DummyTestClassAttribute : TestClassAttribute;
}
