// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Reflection;

using AwesomeAssertions;

using Microsoft.Testing.TestInfrastructure;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Deployment;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.TestableImplementations;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using Moq;

using TestFramework.ForTestingMSTest;

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Execution;

public sealed class TestExecutionManagerRunnerEntryPointMutationTests : TestContainer
{
    public async Task DefaultWorkerFactoryQueuesTheWorkToTheThreadPoolWhenTheHostApartmentIsMta()
    {
        using var harness = new EntryPointHarness();
        MethodInfo factoryMethod = typeof(TestExecutionManager).GetMethod("DefaultFactoryAsync", BindingFlags.NonPublic | BindingFlags.Static)!;
        var factory = (Func<Func<Task>, Task>)Delegate.CreateDelegate(typeof(Func<Func<Task>, Task>), factoryMethod);
        bool? ranOnThreadPoolThread = null;
        Func<Task> work = () =>
        {
            ranOnThreadPoolThread = Thread.CurrentThread.IsThreadPoolThread;
            return Task.CompletedTask;
        };

        // The caller owns a dedicated thread: running the work inline or on a new STA thread is then distinguishable from queuing it to the thread pool.
        await Task.Factory.StartNew(() => factory(work), CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default).Unwrap();

        ranOnThreadPoolThread.Should().BeTrue();
    }

#if DEBUG && !NETFRAMEWORK
    public async Task RunTestsForTestsReportsEachViolatedPreconditionInOrder()
    {
        using var harness = new EntryPointHarness();
        var assertions = new List<string?>();
        using var observer = new DiagnosticAssertionObserver(assertions.Add);

        Func<Task> run = () => harness.Manager.RunTestsAsync(null!, null!, null!, null!, null, null!);

        await run.Should().ThrowAsync<NullReferenceException>();
        assertions.Should().Equal("tests", "deploymentContext", "messageLogger", "runCancellationToken");
    }
#endif

    public async Task RunTestsForSourcesDoesNotTrackUnpartitionedTestsWithoutAPartition()
    {
        using var harness = new EntryPointHarness();

        await harness.RunSourcesAsync([]);

        ReadField(harness.Manager, "_unpartitionedTests").Should().BeNull();
    }

    public async Task RunTestsForSourcesTracksAnEmptyUnpartitionedTestListWhenAPartitionIsSet()
    {
        using var harness = new EntryPointHarness();
        harness.Manager.Partition = new TestPartition(1, 1);

        await harness.RunSourcesAsync([]);

        ReadField(harness.Manager, "_unpartitionedTests").Should().BeOfType<List<UnitTestElement>>().Which.Should().BeEmpty();
    }

    public async Task RunTestsForSourcesCachesTheTestRunParametersOfTheRunSettings()
    {
        using var harness = new EntryPointHarness();

        await harness.RunSourcesAsync(
            [],
            """
            <RunSettings>
              <TestRunParameters>
                <Parameter name="p" value="v" />
              </TestRunParameters>
            </RunSettings>
            """);

        var sessionParameters = (IDictionary<string, object>)ReadField(harness.Manager, "_sessionParameters")!;
        sessionParameters.Should().Equal(new Dictionary<string, object> { ["p"] = "v" });
    }

    public async Task RunTestsForSourcesCleansUpTheDeploymentWhenNoTestFailed()
    {
        using var harness = new EntryPointHarness();

        await harness.RunSourcesAsync([]);

        harness.Services.MockTestDeployment.Verify(deployment => deployment.Cleanup(), Times.Once);
    }

    public async Task RunTestsForSourcesStopsBeforeDiscoveringAnySourceWhenTheRunIsCanceled()
    {
        using var harness = new EntryPointHarness();
        var discoverer = new RecordingDiscoverer();
        harness.Manager.UnitTestDiscovererFactory = _ => discoverer;
        var cancellation = new TestRunCancellationToken();
        cancellation.Cancel();

        Func<Task> run = () => harness.RunSourcesAsync(["canceled-source"], cancellationToken: cancellation);

        await run.Should().ThrowAsync<OperationCanceledException>();
        discoverer.Sources.Should().BeEmpty();
    }

    public void TheDiscovererIsCreatedByTheConfiguredFactoryFromTheTestSourceHandler()
    {
        using var harness = new EntryPointHarness();
        ITestSourceHandler handler = Mock.Of<ITestSourceHandler>();
        ITestSourceHandler? receivedHandler = null;
        var discoverer = new RecordingDiscoverer();
        harness.Manager.UnitTestDiscovererFactory = testSourceHandler =>
        {
            receivedHandler = testSourceHandler;
            return discoverer;
        };

        UnitTestDiscoverer actual = harness.Manager.GetUnitTestDiscoverer(handler);

        actual.Should().BeSameAs(discoverer);
        receivedHandler.Should().BeSameAs(handler);
    }

    private static object? ReadField(TestExecutionManager manager, string name)
        => typeof(TestExecutionManager).GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(manager);

    private sealed class EntryPointHarness : IDisposable
    {
        private readonly IPlatformServiceProvider _previous;

        internal EntryPointHarness()
        {
            _previous = PlatformServiceProvider.Instance;
            Services = new TestablePlatformServiceProvider();
            PlatformServiceProvider.Instance = Services;
            MSTestSettings.PopulateSettings(
                """
                <RunSettings>
                  <RunConfiguration>
                    <ExecutionThreadApartmentState>MTA</ExecutionThreadApartmentState>
                  </RunConfiguration>
                  <MSTest>
                    <CaptureTraceOutput>None</CaptureTraceOutput>
                  </MSTest>
                </RunSettings>
                """,
                logger: null,
                configuration: null);
            Manager = new TestExecutionManager();
        }

        internal TestablePlatformServiceProvider Services { get; }

        internal TestExecutionManager Manager { get; }

        internal Task RunSourcesAsync(IEnumerable<string> sources, string? runSettingsXml = null, TestRunCancellationToken? cancellationToken = null)
            => Manager.RunTestsAsync(
                sources,
                new DeploymentContext(null, runSettingsXml),
                Mock.Of<IAdapterMessageLogger>(),
                Mock.Of<ITestResultRecorder>(),
                filterProvider: null,
                testSourceHandler: Mock.Of<ITestSourceHandler>(),
                isMTP: false,
                cancellationToken: cancellationToken ?? new TestRunCancellationToken());

        public void Dispose()
        {
            PlatformServiceProvider.Instance = _previous;
            MSTestSettings.Reset();
        }
    }

    private sealed class RecordingDiscoverer() : UnitTestDiscoverer(Mock.Of<ITestSourceHandler>())
    {
        internal List<string> Sources { get; } = [];

        internal override Task DiscoverTestsInSourceAsync(
            string source,
            IAdapterMessageLogger logger,
            IUnitTestElementSink discoverySink,
            string? settingsXml,
            ITestElementFilterProvider? filterProvider,
            bool isMTP)
        {
            Sources.Add(source);
            return Task.CompletedTask;
        }
    }
}
