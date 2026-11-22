// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#pragma warning disable SA1121 // Keep IntPtr for older target frameworks sharing this file.

using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;

using AwesomeAssertions;

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.SourceGeneration;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.TestableImplementations;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using Moq;

using TestFramework.ForTestingMSTest;

using ExecutionScope = Microsoft.VisualStudio.TestTools.UnitTesting.ExecutionScope;
using ITestMethod = Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface.ObjectModel.ITestMethod;

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Execution;

public sealed class TestExecutionManagerRunnerLoopMutationTests : TestContainer
{
    private const string Source = "loop-source";
    private const string OrderByName = "<OrderTestsByNameInClass>true</OrderTestsByNameInClass>";

    public async Task TestsAreRunInClassThenMethodNameOrderWhenOrderingByNameIsEnabled()
    {
        using var harness = new LoopHarness(OrderByName);
        UnitTestElement[] tests =
        [
            Managed<BetaFixture>(nameof(BetaFixture.Two)),
            Managed<AlphaFixture>(nameof(AlphaFixture.Two)),
            Managed<BetaFixture>(nameof(BetaFixture.One)),
            Managed<AlphaFixture>(nameof(AlphaFixture.One)),
        ];

        await harness.ExecuteAsync(tests, usesAppDomains: false, harness.CreateRunner(tests));

        harness.Recorder.Started.Should().Equal("AlphaFixture.One", "AlphaFixture.Two", "BetaFixture.One", "BetaFixture.Two");
    }

    public async Task TestsWithoutManagedNamesAreRunBeforeTheOnesWithManagedNamesWhenOrderingByName()
    {
        using var harness = new LoopHarness(OrderByName);
        UnitTestElement[] tests =
        [
            Managed<AlphaFixture>(nameof(AlphaFixture.Two)),
            WithoutManagedNames<BetaFixture>(nameof(BetaFixture.One)),
            Managed<AlphaFixture>(nameof(AlphaFixture.One)),
        ];

        await harness.ExecuteAsync(tests, usesAppDomains: false, harness.CreateRunner(tests));

        harness.Recorder.Started.Should().Equal("BetaFixture.One", "AlphaFixture.One", "AlphaFixture.Two");
    }

    public async Task TestsWithoutAUsableManagedMethodNameKeepTheirRelativeOrderWhenOrderingByName()
    {
        using var harness = new LoopHarness(OrderByName);
        UnitTestElement[] tests =
        [
            new(new TestMethod(" ", null, "First", typeof(AlphaFixture).FullName!, Source, "First", null)),
            WithoutManagedNames<AlphaFixture>("Second"),
        ];

        await harness.ExecuteAsync(tests, usesAppDomains: false, harness.CreateRunner(tests));

        harness.Recorder.Started.Should().Equal("AlphaFixture.First", "AlphaFixture.Second");
    }

    public async Task ExecutionActivityLeaseIsSupportedWhenAppDomainsAreNotUsed()
    {
        (await RunAndGetStartLeaseSupportAsync(usesAppDomains: false)).Should().BeTrue();
        (await RunAndGetStartLeaseSupportAsync(usesAppDomains: true)).Should().BeFalse();
    }

    public async Task TheRealMessageLoggerIsPassedToTheRunnerWhenAppDomainsAreNotUsed()
    {
        using var harness = new LoopHarness();
        UnitTestElement test = Managed<AlphaFixture>(nameof(AlphaFixture.One));
        IAdapterMessageLogger expected = harness.Logger.Object;

        await harness.ExecuteAsync([test], usesAppDomains: false, harness.CreateRunner(test));

        harness.Services.Loggers.Should().NotBeEmpty();
        harness.Services.Loggers.Should().OnlyContain(logger => ReferenceEquals(logger, expected));
    }

    public async Task TheMessageLoggerIsWrappedForRemotingWhenAppDomainsAreUsed()
    {
        using var harness = new LoopHarness();
        UnitTestElement test = Managed<AlphaFixture>(nameof(AlphaFixture.One));

        await harness.ExecuteAsync([test], usesAppDomains: true, harness.CreateRunner(test));

        IAdapterMessageLogger remotingLogger = harness.Services.Loggers.Distinct().Should().ContainSingle().Subject;
        remotingLogger.GetType().Name.Should().Be("RemotingMessageLogger");
        remotingLogger.SendMessage(MessageLevel.Warning, "forwarded through remoting");
        harness.Logger.Verify(logger => logger.SendMessage(MessageLevel.Warning, "forwarded through remoting"), Times.Once);
    }

    public async Task CancellationIsObservedBeforeAnyTestIsStarted()
    {
        using var harness = new LoopHarness();
        var cancellationToken = new TestRunCancellationToken();
        cancellationToken.Cancel();
        harness.SetCancellationToken(cancellationToken);
        UnitTestElement test = Managed<AlphaFixture>(nameof(AlphaFixture.One));

        Func<Task> act = () => harness.ExecuteAsync([test], usesAppDomains: false, harness.CreateRunner(test));

        await act.Should().ThrowAsync<OperationCanceledException>();
        harness.Recorder.Events.Should().BeEmpty();
    }

    public async Task NoTestIsStartedWhenAGracefulStopWasAlreadyRequested()
    {
        using var harness = new LoopHarness();
        harness.Services.IsGracefulStopRequested = true;
        UnitTestElement[] tests = [Managed<AlphaFixture>(nameof(AlphaFixture.One)), Managed<AlphaFixture>(nameof(AlphaFixture.Two))];

        await harness.ExecuteAsync(tests, usesAppDomains: false, harness.CreateRunner(tests));

        harness.Recorder.Events.Should().BeEmpty();
    }

    public async Task StartAndEndOfEachTestAreLoggedWhenInfoLoggingIsEnabled()
    {
        using var harness = new LoopHarness();
        harness.Services.MockTraceLogger.SetupGet(logger => logger.IsInfoEnabled).Returns(true);
        UnitTestElement test = Managed<AlphaFixture>(nameof(AlphaFixture.One));

        await harness.ExecuteAsync([test], usesAppDomains: false, harness.CreateRunner(test));

        harness.Services.MockTraceLogger.Verify(logger => logger.Info("Executing test {0}", "One"), Times.Once);
        harness.Services.MockTraceLogger.Verify(logger => logger.Info("Executed test {0}", "One"), Times.Once);
    }

    public async Task NothingIsLoggedWhenInfoLoggingIsDisabled()
    {
        using var harness = new LoopHarness();
        UnitTestElement test = Managed<AlphaFixture>(nameof(AlphaFixture.One));

        await harness.ExecuteAsync([test], usesAppDomains: false, harness.CreateRunner(test));

        harness.Services.MockTraceLogger.Verify(logger => logger.Info("Executing test {0}", It.IsAny<object?[]>()), Times.Never);
        harness.Services.MockTraceLogger.Verify(logger => logger.Info("Executed test {0}", It.IsAny<object?[]>()), Times.Never);
    }

    public async Task ASkippedDependentIsReportedAsStartedAndIgnoredAndIsNotRunForItsOwnDependents()
    {
        using var harness = new LoopHarness();
        UnitTestElement prerequisite = WithoutManagedNames<AlphaFixture>("Prerequisite");
        UnitTestElement dependent = WithoutManagedNames<AlphaFixture>("Dependent", new TestDependencyInfo(null, "Prerequisite", proceedOnFailure: false));
        UnitTestElement observer = WithoutManagedNames<AlphaFixture>("Observer", new TestDependencyInfo(null, "Dependent", proceedOnFailure: false));
        UnitTestElement[] tests = [prerequisite, dependent, observer];
        TestDependencyCoordinator coordinator = CreateCoordinator(tests);
        coordinator.RecordOutcome(prerequisite, passed: false);
        coordinator.RecordOutcome(dependent, passed: true);

        await harness.ExecuteAsync([dependent], usesAppDomains: false, harness.CreateRunner(tests), coordinator);

        harness.Recorder.Kinds.Should().Equal("RecordStart", "PrepareResults", "RecordResult:Ignored");
        harness.Recorder.Events[0].SupportsLease.Should().BeTrue();
        coordinator.ShouldSkip(observer, out string? reason).Should().BeTrue();
        reason.Should().Contain("Dependent");
    }

    public void TheTestRunnerIsInvokedSynchronouslyWhenAppDomainsAreUsed()
    {
        (RecordedEvent passed, List<bool> observations) = RunYieldingTest(usesAppDomains: true, apartmentState: ApartmentState.Unknown);

        passed.OnPoolThread.Should().BeFalse();
        observations.Should().Equal(true);
    }

    public void TheTestRunnerIsInvokedSynchronouslyOnAnStaThread()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return;
        }

        (RecordedEvent passed, List<bool> observations) = RunYieldingTest(usesAppDomains: false, apartmentState: ApartmentState.STA);

        passed.OnPoolThread.Should().BeFalse();
        observations.Should().Equal(true);
    }

    public void TheCleanupOfASkippedDependentIsRunSynchronouslyWhenAppDomainsAreUsed()
    {
        (RecordedEvent ignored, List<bool> observations) = RunSkippedDependent(usesAppDomains: true, gated: false, apartmentState: ApartmentState.Unknown);

        ignored.OnPoolThread.Should().BeFalse();
        observations.Should().Equal(true);
    }

    public void TheCleanupOfASkippedDependentIsRunSynchronouslyOnAnStaThread()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return;
        }

        (RecordedEvent ignored, List<bool> observations) = RunSkippedDependent(usesAppDomains: false, gated: false, apartmentState: ApartmentState.STA);

        ignored.OnPoolThread.Should().BeFalse();
        observations.Should().Equal(true);
    }

    public void TheCleanupOfASkippedDependentIsRunAsynchronouslyWhenAppDomainsAreNotUsed()
    {
        (RecordedEvent ignored, List<bool> observations) = RunSkippedDependent(usesAppDomains: false, gated: true, apartmentState: ApartmentState.Unknown);

        ignored.OnPoolThread.Should().BeTrue();
        observations.Should().Equal(true);
    }

    private static async Task<bool> RunAndGetStartLeaseSupportAsync(bool usesAppDomains)
    {
        using var harness = new LoopHarness();
        UnitTestElement test = Managed<AlphaFixture>(nameof(AlphaFixture.One));

        await harness.ExecuteAsync([test], usesAppDomains, harness.CreateRunner(test));

        return harness.Recorder.Events.Single(recorded => recorded.Kind == "RecordStart").SupportsLease;
    }

    private static (RecordedEvent Passed, List<bool> PoolThreadObservations) RunYieldingTest(bool usesAppDomains, ApartmentState apartmentState)
    {
        using var harness = new LoopHarness();
        var probe = new Probe();
        harness.SourceLevelParameters["probe"] = probe;
        UnitTestElement test = Managed<YieldingFixture>(nameof(YieldingFixture.Yielding));
        UnitTestRunner runner = harness.CreateRunner(test);

        RunOnDedicatedThread(() => harness.ExecuteAsync([test], usesAppDomains, runner), probe, apartmentState);

        return (harness.Recorder.Events.Single(recorded => recorded.Kind == "RecordResult:Passed"), probe.PoolThreadObservations);
    }

    private static (RecordedEvent Ignored, List<bool> PoolThreadObservations) RunSkippedDependent(bool usesAppDomains, bool gated, ApartmentState apartmentState)
    {
        using var harness = new LoopHarness();
        TaskCompletionSource<bool>? gate = gated ? new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously) : null;
        var probe = new Probe(gate);
        harness.SourceLevelParameters["probe"] = probe;
        UnitTestElement prerequisite = Managed<CleanupFixture>(nameof(CleanupFixture.Prerequisite));
        UnitTestElement dependent = Managed<CleanupFixture>(nameof(CleanupFixture.Dependent), new TestDependencyInfo(null, nameof(CleanupFixture.Prerequisite), proceedOnFailure: false));
        TestDependencyCoordinator coordinator = CreateCoordinator(prerequisite, dependent);
        UnitTestRunner runner = harness.CreateRunner(prerequisite, dependent);

        RunOnDedicatedThread(
            async () =>
            {
                harness.RunSynchronously(runner, prerequisite);
                await harness.ExecuteAsync([dependent], usesAppDomains, runner, coordinator);
            },
            probe,
            apartmentState);

        return (harness.Recorder.Events.Single(recorded => recorded.Kind == "RecordResult:Ignored"), probe.PoolThreadObservations);
    }

    [SuppressMessage("Interoperability", "CA1416:Validate platform compatibility", Justification = "The STA apartment is only requested by the tests that first check they run on Windows.")]
    private static void RunOnDedicatedThread(Func<Task> work, Probe probe, ApartmentState apartmentState)
    {
        ExceptionDispatchInfo? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                if (probe.Gate is not null)
                {
                    // A thread that blocks on the work while it is parked on the gate would deadlock: the context lets it
                    // through so that a regression is reported as a failed assertion instead of a hang.
                    SynchronizationContext.SetSynchronizationContext(new GateReleasingContext(probe, Environment.CurrentManagedThreadId));
                }

                Task pending = work();

                // Released only once the work returned an incomplete task: every await of the chain is registered by then.
                probe.Gate?.TrySetResult(true);
                pending.GetAwaiter().GetResult();
            }
            catch (Exception exception)
            {
                failure = ExceptionDispatchInfo.Capture(exception);
            }
        });

        if (apartmentState != ApartmentState.Unknown)
        {
            thread.SetApartmentState(apartmentState);
        }

        thread.Start();
        thread.Join();
        failure?.Throw();
    }

    private static UnitTestElement Managed<T>(string method, params TestDependencyInfo[] dependencies)
        => new(new TestMethod(method, null, method, typeof(T).FullName!, Source, method, null)
        {
            MethodInfo = typeof(T).GetMethod(method),
        })
        {
            Dependencies = dependencies.Length == 0 ? null : dependencies,
        };

    private static UnitTestElement WithoutManagedNames<T>(string method, params TestDependencyInfo[] dependencies)
        => new(new TestMethod(method, typeof(T).FullName!, Source, displayName: null))
        {
            Dependencies = dependencies.Length == 0 ? null : dependencies,
        };

    private static TestDependencyCoordinator CreateCoordinator(params UnitTestElement[] tests)
    {
        TestDependencyGraph graph = TestDependencyGraph.Build(tests, ExecutionScope.MethodLevel, parallelizationEnabled: true)
            ?? throw new InvalidOperationException("The tests do not declare any dependency.");
        return new TestDependencyCoordinator(graph);
    }

    private static string Describe(UnitTestElement test)
    {
        string className = test.TestMethod.FullClassName;
        return className.Substring(className.LastIndexOf('+') + 1) + "." + test.TestMethod.Name;
    }

    private sealed class LoopHarness : IDisposable
    {
        private static readonly MethodInfo ExecuteMethod = typeof(TestExecutionManager).GetMethod("ExecuteTestsWithTestRunnerAsync", BindingFlags.NonPublic | BindingFlags.Instance)!;
        private static readonly FieldInfo CancellationTokenField = typeof(TestExecutionManager).GetField("_testRunCancellationToken", BindingFlags.NonPublic | BindingFlags.Instance)!;
        private static readonly FieldInfo RecorderField = typeof(TestExecutionManager).GetField("_testResultRecorder", BindingFlags.NonPublic | BindingFlags.Instance)!;

        private readonly IPlatformServiceProvider _previous;

        internal LoopHarness(string extraMSTestSettings = "")
        {
            _previous = PlatformServiceProvider.Instance;
            Services = new CapturingProvider();
            PlatformServiceProvider.Instance = Services;
            MSTestSettings.PopulateSettings(
                $"""
                <RunSettings>
                  <RunConfiguration>
                    <ExecutionThreadApartmentState>MTA</ExecutionThreadApartmentState>
                  </RunConfiguration>
                  <MSTest>
                    <CaptureTraceOutput>None</CaptureTraceOutput>
                    {extraMSTestSettings}
                  </MSTest>
                </RunSettings>
                """,
                logger: null,
                configuration: null);
            Services.SetReflectionOperations(new SourceGeneratedReflectionOperations(new SourceGeneratedReflectionDataProvider
            {
                Assembly = typeof(AlphaFixture).Assembly,
                AssemblyName = typeof(AlphaFixture).Assembly.GetName().Name!,
                Types = [typeof(AlphaFixture), typeof(BetaFixture), typeof(YieldingFixture), typeof(CleanupFixture)],
                AssemblyAttributes = [],
            }));
            Services.MockFileOperations.Setup(value => value.LoadAssembly(It.IsAny<string>())).Returns(typeof(AlphaFixture).Assembly);
            Manager = new TestExecutionManager();
            RecorderField.SetValue(Manager, Recorder);
        }

        internal CapturingProvider Services { get; }

        internal TestExecutionManager Manager { get; }

        internal Mock<IAdapterMessageLogger> Logger { get; } = new();

        internal RecordingRecorder Recorder { get; } = new();

        internal Dictionary<string, object> SourceLevelParameters { get; } = [];

        internal UnitTestRunner CreateRunner(params UnitTestElement[] elements) => new(MSTestSettings.CurrentSettings, elements);

        internal Task ExecuteAsync(UnitTestElement[] tests, bool usesAppDomains, UnitTestRunner runner, TestDependencyCoordinator? coordinator = null)
            => (Task)ExecuteMethod.Invoke(Manager, [tests, Logger.Object, Source, SourceLevelParameters, runner, usesAppDomains, coordinator])!;

        internal void RunSynchronously(UnitTestRunner runner, UnitTestElement test)
        {
            Dictionary<string, object?> lifecycleContextProperties = [];
            foreach (KeyValuePair<string, object> pair in SourceLevelParameters)
            {
                lifecycleContextProperties[pair.Key] = pair.Value;
            }

            runner.RunSingleTest(test, new Dictionary<string, object?>(), lifecycleContextProperties, Logger.Object);
        }

        internal void SetCancellationToken(TestRunCancellationToken cancellationToken) => CancellationTokenField.SetValue(Manager, cancellationToken);

        public void Dispose()
        {
            PlatformServiceProvider.Instance = _previous;
            MSTestSettings.Reset();
            AssertionFailureSettings.CaptureDiagnosticsOnFailure = null;
        }
    }

    private sealed class CapturingProvider : TestablePlatformServiceProvider, IPlatformServiceProvider
    {
        internal List<IAdapterMessageLogger> Loggers { get; } = [];

        ITestContext IPlatformServiceProvider.GetTestContext(ITestMethod? testMethod, string? testClassFullName, IDictionary<string, object?> properties, IAdapterMessageLogger messageLogger, UnitTestOutcome outcome)
        {
            Loggers.Add(messageLogger);
            return GetTestContext(testMethod, testClassFullName, properties, messageLogger, outcome);
        }
    }

    private sealed class RecordingRecorder : ITestResultRecorder
    {
        internal List<RecordedEvent> Events { get; } = [];

        internal string[] Kinds => [.. Events.Select(recorded => recorded.Kind)];

        internal string[] Started => [.. Events.Where(recorded => recorded.Kind == "RecordStart").Select(recorded => recorded.Test)];

        public void PrepareResults(UnitTestElement test, TestResult[] results) => Add("PrepareResults", test);

        public Task RecordStartAsync(UnitTestElement test)
        {
            Add("RecordStart", test);
            return Task.CompletedTask;
        }

        public Task RecordEmptyResultAsync(UnitTestElement test)
        {
            Add("RecordEmptyResult", test);
            return Task.CompletedTask;
        }

        public Task<bool> RecordResultAsync(UnitTestElement test, TestResult result, DateTimeOffset startTime, DateTimeOffset endTime)
        {
            Add("RecordResult:" + result.Outcome, test);
            return Task.FromResult(false);
        }

        private void Add(string kind, UnitTestElement test)
            => Events.Add(new RecordedEvent(kind, Describe(test), Thread.CurrentThread.IsThreadPoolThread, test.SupportsExecutionActivityLease));
    }

    private sealed class RecordedEvent
    {
        internal RecordedEvent(string kind, string test, bool onPoolThread, bool supportsLease)
        {
            Kind = kind;
            Test = test;
            OnPoolThread = onPoolThread;
            SupportsLease = supportsLease;
        }

        internal string Kind { get; }

        internal string Test { get; }

        internal bool OnPoolThread { get; }

        internal bool SupportsLease { get; }
    }

    private sealed class Probe
    {
        internal Probe(TaskCompletionSource<bool>? gate = null) => Gate = gate;

        internal TaskCompletionSource<bool>? Gate { get; }

        internal List<bool> PoolThreadObservations { get; } = [];

        internal bool IsParkedOnGate { get; private set; }

        internal async Task PauseAsync()
        {
            if (Gate is not null)
            {
                IsParkedOnGate = true;
                await Gate.Task.ConfigureAwait(false);
            }
            else
            {
                await Task.Yield();
                await Task.Yield();
                await Task.Yield();
            }

            PoolThreadObservations.Add(Thread.CurrentThread.IsThreadPoolThread);
        }
    }

    private sealed class GateReleasingContext : SynchronizationContext
    {
        private readonly Probe _probe;
        private readonly int _threadId;

        internal GateReleasingContext(Probe probe, int threadId)
        {
            _probe = probe;
            _threadId = threadId;
            SetWaitNotificationRequired();
        }

        public override int Wait(IntPtr[] waitHandles, bool waitAll, int millisecondsTimeout)
        {
            // Only the thread that owns the context, and only once the cleanup is parked: earlier blocking waits must not open the gate.
            if (_probe.IsParkedOnGate && Environment.CurrentManagedThreadId == _threadId)
            {
                _probe.Gate!.TrySetResult(true);
            }

            return base.Wait(waitHandles, waitAll, millisecondsTimeout);
        }
    }

    [TestClass]
    public sealed class AlphaFixture
    {
        [TestMethod]
        public void One()
        {
        }

        [TestMethod]
        public void Two()
        {
        }
    }

    [TestClass]
    public sealed class BetaFixture
    {
        [TestMethod]
        public void One()
        {
        }

        [TestMethod]
        public void Two()
        {
        }
    }

    [TestClass]
    public sealed class YieldingFixture
    {
        public TestContext TestContext { get; set; } = null!;

        [TestMethod]
        public Task Yielding() => ((Probe)TestContext.Properties["probe"]!).PauseAsync();
    }

    [TestClass]
    public sealed class CleanupFixture
    {
        [ClassCleanup]
        public static Task ClassCleanup(TestContext context) => ((Probe)context.Properties["probe"]!).PauseAsync();

        [TestMethod]
        public void Prerequisite()
        {
        }

        [TestMethod]
        public void Dependent()
        {
        }
    }
}
