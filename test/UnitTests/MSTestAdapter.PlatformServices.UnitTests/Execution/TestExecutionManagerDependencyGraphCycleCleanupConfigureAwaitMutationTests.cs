// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using AwesomeAssertions;

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Deployment;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.TestableImplementations;

using Moq;

using MSTestAdapter.PlatformServices.UnitTests.Helpers;

using TestFramework.ForTestingMSTest;

using FrameworkTestResult = Microsoft.VisualStudio.TestTools.UnitTesting.TestResult;

namespace MSTestAdapter.PlatformServices.UnitTests.Execution;

public sealed class TestExecutionManagerDependencyGraphCycleCleanupConfigureAwaitMutationTests : TestContainer
{
    private const string Source = "cycle-cleanup-source";

    private const string Settings =
        """
        <RunSettings>
          <RunConfiguration>
            <DisableAppDomain>True</DisableAppDomain>
          </RunConfiguration>
        </RunSettings>
        """;

    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(10);

    private readonly TestablePlatformServiceProvider _services = new();

    public TestExecutionManagerDependencyGraphCycleCleanupConfigureAwaitMutationTests()
    {
        PlatformServiceProvider.Instance = _services;
        _services.MockFileOperations.Setup(value => value.LoadAssembly(Source)).Returns(typeof(PendingClassCleanupClass).Assembly);
        _services.MockSettingsProvider.Setup(value => value.GetProperties(Source)).Returns(new Dictionary<string, object>());
    }

    protected override void Dispose(bool disposing)
    {
        if (!IsDisposed)
        {
            base.Dispose(disposing);
            PendingClassCleanupClass.Reset();
            PlatformServiceProvider.Instance = null;
            MSTestSettings.Reset();
        }
    }

    public async Task RunTestsAsync_WhenNotifyingThatATestInADependencyCycleDidNotRunIsPending_NeverResumesOnTheCapturedSynchronizationContext()
    {
        var probe = new ContinuationProbe();
        var gate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        PendingClassCleanupClass.Reset();
        PendingClassCleanupClass.ClassCleanupGate = gate;

        try
        {
            IAdapterMessageLogger logger = new Mock<IAdapterMessageLogger>().Object;
            MSTestSettings.PopulateSettings(Settings, logger, null);
            UnitTestElement executed = CreateElement(nameof(PendingClassCleanupClass.Executed));
            UnitTestElement inCycleA = CreateElement(nameof(PendingClassCleanupClass.InCycleA));
            inCycleA.Dependencies = [CreateDependency(nameof(PendingClassCleanupClass.InCycleB))];
            UnitTestElement inCycleB = CreateElement(nameof(PendingClassCleanupClass.InCycleB));
            inCycleB.Dependencies = [CreateDependency(nameof(PendingClassCleanupClass.InCycleA))];

            // A run executes its source with a runner that has not run any test yet, so the class cleanup is never pending when the tests of a cycle are notified.
            // The runner injected here already executed a test of the class: the last test of the cycle is then the one that runs the class cleanup.
            var runner = new UnitTestRunner(MSTestSettings.CurrentSettings, [executed, inCycleA, inCycleB]);
            FrameworkTestResult[] executedResults = await runner.RunSingleTestAsync(executed, new Dictionary<string, object?>(), logger);
            executedResults.Should().ContainSingle().Which.Outcome.Should().Be(UnitTestOutcome.Passed);
            _services.MockTestSourceHost.Setup(value => value.CreateInstanceForType(It.IsAny<Type>(), It.IsAny<object?[]?>()))
                .Returns<Type, object?[]?>((type, arguments) => type == typeof(TestAssemblySettingsProvider)
                    ? new TestAssemblySettingsProvider()
                    : type == typeof(UnitTestRunner)
                        ? runner
                        : throw new InvalidOperationException("Unexpected isolation activation: " + type));

            var recorder = new Mock<ITestResultRecorder>();
            recorder.Setup(value => value.RecordStartAsync(It.IsAny<UnitTestElement>())).Returns(Task.CompletedTask);
            recorder.Setup(value => value.RecordEmptyResultAsync(It.IsAny<UnitTestElement>())).Returns(Task.CompletedTask);
            recorder.Setup(value => value.RecordResultAsync(It.IsAny<UnitTestElement>(), It.IsAny<FrameworkTestResult>(), It.IsAny<DateTimeOffset>(), It.IsAny<DateTimeOffset>())).ReturnsAsync(false);

            // The run is started on the probe's thread, so that the awaits of the scheduler execute while its context is current.
            // The inline task factory keeps the whole run on that thread until an operation is genuinely pending.
            UnitTestElement[] cycle = [inCycleA, inCycleB];
            var manager = new TestExecutionManager(task => task());
            Task run = probe.Run(() => manager.RunTestsAsync(cycle, new DeploymentContext(null, Settings), logger, recorder.Object, filterProvider: null, new TestRunCancellationToken()));
            await AssertNotFaultedAsync(run);
            PendingClassCleanupClass.ClassCleanupStarted.Should().Be(1, "the run must reach the pending class cleanup");
            run.IsCompleted.Should().BeFalse("the run is expected to wait for the class cleanup");
            gate.SetResult(true);
            await WaitFor(run);

            probe.PostCount.Should().Be(0, "notifying that a test of a dependency cycle did not run must not capture the caller's synchronization context");
        }
        finally
        {
            PendingClassCleanupClass.Reset();
        }
    }

    private static UnitTestElement CreateElement(string methodName)
        => new(new TestMethod(methodName, hierarchyValues: null, methodName, typeof(PendingClassCleanupClass).FullName!, Source, displayName: null, null));

    private static TestDependencyInfo CreateDependency(string methodName)
        => new(typeof(PendingClassCleanupClass).FullName, methodName, proceedOnFailure: false);

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

    [DummyTestClass]
    private sealed class PendingClassCleanupClass
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
        public void Executed()
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
