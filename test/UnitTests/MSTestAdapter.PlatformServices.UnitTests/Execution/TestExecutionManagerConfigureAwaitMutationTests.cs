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

namespace MSTestAdapter.PlatformServices.UnitTests.Execution;

public sealed class TestExecutionManagerConfigureAwaitMutationTests : TestContainer
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

    public TestExecutionManagerConfigureAwaitMutationTests()
        => PlatformServiceProvider.Instance = new TestablePlatformServiceProvider();

    protected override void Dispose(bool disposing)
    {
        if (!IsDisposed)
        {
            base.Dispose(disposing);
            PlatformServiceProvider.Instance = null;
            MSTestSettings.Reset();
        }
    }

    public async Task RunTestsAsync_ForTests_NeverResumesOnTheCapturedSynchronizationContext()
    {
        var probe = new ContinuationProbe();
        var manager = new PendingExecutionManager();
        UnitTestElement[] tests = [new UnitTestElement(new TestMethod("M", "C", "A", displayName: null))];

        // The execution of the tests stays pending, so the await on it suspends while the probe's context is current.
        Task run = probe.Run(() => manager.RunTestsAsync(tests, new DeploymentContext(null, null), new Mock<IAdapterMessageLogger>().Object, new Mock<ITestResultRecorder>().Object, filterProvider: null, new TestRunCancellationToken()));
        run.IsCompleted.Should().BeFalse();
        manager.ExecuteCalls.Should().Be(1);
        manager.Gate.SetResult(true);
        await WaitFor(run);

        probe.PostCount.Should().Be(0, "running the tests must not capture the caller's synchronization context");
    }

    public async Task RunTestsAsync_ForSources_DoesNotResumeDiscoveryOnTheCapturedSynchronizationContext()
    {
        var probe = new ContinuationProbe();
        var discoverer = new PendingDiscoverer();
        var manager = new PendingExecutionManager { UnitTestDiscovererFactory = _ => discoverer };
        manager.Gate.SetResult(true);

        // The discovery of the source stays pending, so the await on it suspends while the probe's context is current.
        Task run = probe.Run(() => RunSourcesAsync(manager));
        run.IsCompleted.Should().BeFalse();
        discoverer.DiscoveredSources.Should().Equal("A.dll");
        manager.ExecuteCalls.Should().Be(0);
        discoverer.Gate.SetResult(true);
        await WaitFor(run);

        manager.ExecuteCalls.Should().Be(1);
        probe.PostCount.Should().Be(0, "discovering the source must not capture the caller's synchronization context");
    }

    public async Task RunTestsAsync_ForSources_DoesNotResumeExecutionOnTheCapturedSynchronizationContext()
    {
        var probe = new ContinuationProbe();
        var discoverer = new PendingDiscoverer();
        discoverer.Gate.SetResult(true);
        var manager = new PendingExecutionManager { UnitTestDiscovererFactory = _ => discoverer };

        // The discovery is already complete and the execution of the tests stays pending, so the await on it suspends
        // while the probe's context is current.
        Task run = probe.Run(() => RunSourcesAsync(manager));
        run.IsCompleted.Should().BeFalse();
        manager.ExecuteCalls.Should().Be(1);
        manager.Gate.SetResult(true);
        await WaitFor(run);

        probe.PostCount.Should().Be(0, "running the tests of the sources must not capture the caller's synchronization context");
    }

    private static Task RunSourcesAsync(TestExecutionManager manager)
        => manager.RunTestsAsync(["A.dll"], new DeploymentContext(null, null), new Mock<IAdapterMessageLogger>().Object, new Mock<ITestResultRecorder>().Object, filterProvider: null, new Mock<ITestSourceHandler>().Object, isMTP: false, new TestRunCancellationToken());

    private static async Task WaitFor(Task task)
    {
        Task completed = await Task.WhenAny(task, Task.Delay(Timeout));
        completed.Should().BeSameAs(task, "the awaited operation timed out");
        await task;
    }

    private sealed class PendingExecutionManager : TestExecutionManager
    {
        public TaskCompletionSource<bool> Gate { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public int ExecuteCalls { get; private set; }

        internal override Task ExecuteTestsAsync(IEnumerable<UnitTestElement> tests, DeploymentContext deploymentContext, IAdapterMessageLogger messageLogger, ITestResultRecorder testResultRecorder, ITestElementFilterProvider? filterProvider, bool isDeploymentDone)
        {
            ExecuteCalls++;
            return Gate.Task;
        }
    }

    private sealed class PendingDiscoverer : UnitTestDiscoverer
    {
        public PendingDiscoverer()
            : base(new Mock<ITestSourceHandler>().Object)
        {
        }

        public TaskCompletionSource<bool> Gate { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public List<string> DiscoveredSources { get; } = [];

        internal override Task DiscoverTestsInSourceAsync(string source, IAdapterMessageLogger logger, IUnitTestElementSink discoverySink, string? settingsXml, ITestElementFilterProvider? filterProvider, bool isMTP)
        {
            DiscoveredSources.Add(source);
            return Gate.Task;
        }
    }
}
