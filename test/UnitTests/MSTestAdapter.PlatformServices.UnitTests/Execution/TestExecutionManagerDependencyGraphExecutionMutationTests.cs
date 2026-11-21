// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using AwesomeAssertions;

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Deployment;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Resources;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.TestableImplementations;

using Moq;

using TestFramework.ForTestingMSTest;

using FrameworkTestResult = Microsoft.VisualStudio.TestTools.UnitTesting.TestResult;

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Execution;

public sealed class TestExecutionManagerDependencyGraphExecutionMutationTests : TestContainer
{
    private const string WorkerCanceledFormat = "Parallel test worker canceled for source {0}";
    private const string MissingClassName = "Missing.Prerequisite.Class";

    // Only reached when the code under test is broken: a healthy run observes the cancellation within milliseconds.
    private static readonly TimeSpan CancellationObservationLimit = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan HangGuard = TimeSpan.FromSeconds(60);

    private readonly TestablePlatformServiceProvider _provider;
    private readonly RecordingMessageLogger _messageLogger = new();
    private readonly RecordingTestResultRecorder _recorder = new();
    private readonly List<(string Format, object?[] Args)> _infoCalls = [];

    public TestExecutionManagerDependencyGraphExecutionMutationTests()
    {
        _provider = new TestablePlatformServiceProvider();
        PlatformServiceProvider.Instance = _provider;

        _provider.MockFileOperations.Setup(fileOperations => fileOperations.LoadAssembly(It.IsAny<string>()))
            .Returns((string assemblyName) => Assembly.Load(new AssemblyName(Path.GetFileNameWithoutExtension(assemblyName))));
        _provider.MockTestSourceHost.Setup(host => host.CreateInstanceForType(It.IsAny<Type>(), It.IsAny<object[]>()))
            .Returns((Type type, object[] args) => Activator.CreateInstance(type, args));
        _provider.MockSettingsProvider.Setup(settingsProvider => settingsProvider.GetProperties(It.IsAny<string>()))
            .Returns(new Dictionary<string, object>());
        _provider.MockTraceLogger.Setup(logger => logger.Info(It.IsAny<string>(), It.IsAny<object?[]>()))
            .Callback((string format, object?[] args) => RecordInfo(format, args));
    }

    protected override void Dispose(bool disposing)
    {
        if (!IsDisposed)
        {
            base.Dispose(disposing);
            PlatformServiceProvider.Instance = null;
            MSTestSettings.Reset();
        }
    }

    public async Task RunTests_TestsDependingOnEachOther_ReportTheCycleOnceAndFailBothTests()
    {
        UnitTestElement first = CreateElement(typeof(FixtureA), "A");
        UnitTestElement second = CreateElement(typeof(FixtureB), "B");
        first.Dependencies = [new TestDependencyInfo(typeof(FixtureB).FullName, "B", proceedOnFailure: false)];
        second.Dependencies = [new TestDependencyInfo(typeof(FixtureA).FullName, "A", proceedOnFailure: false)];

        var manager = new TestExecutionManager(workerBody => workerBody());

        await RunAsync(manager, [first, second], CreateSequentialRunSettings(), new TestRunCancellationToken());

        string firstName = first.TestMethod.FullyQualifiedName;
        string secondName = second.TestMethod.FullyQualifiedName;
        string[] errors = [.. _messageLogger.Messages.Where(message => message.Level == MessageLevel.Error).Select(message => message.Message)];
        errors.Should().ContainSingle().Which.Should().BeOneOf(DescribeCycle(firstName, secondName), DescribeCycle(secondName, firstName));

        _recorder.Events.Should().Equal("start:A", "result:A:Failed", "start:B", "result:B:Failed");
        _recorder.Starts.Should().OnlyContain(start => start.SupportsExecutionActivityLease, "tests that are reported without running still participate in the execution activity lease");
        _recorder.Results.Should().OnlyContain(result => result.Failure is InvalidOperationException && result.Failure.Message == errors[0]);
    }

    public async Task RunTests_CancelledRightAfterTheCycleIsReported_DoesNotReportTheBrokenTests()
    {
        UnitTestElement first = CreateElement(typeof(FixtureA), "A");
        UnitTestElement second = CreateElement(typeof(FixtureB), "B");
        first.Dependencies = [new TestDependencyInfo(typeof(FixtureB).FullName, "B", proceedOnFailure: false)];
        second.Dependencies = [new TestDependencyInfo(typeof(FixtureA).FullName, "A", proceedOnFailure: false)];

        var token = new TestRunCancellationToken();
        _messageLogger.OnMessage = (level, _) =>
        {
            if (level == MessageLevel.Error)
            {
                token.Cancel();
            }
        };

        var manager = new TestExecutionManager(workerBody => workerBody());

        Func<Task> run = () => RunAsync(manager, [first, second], CreateSequentialRunSettings(), token);

        await run.Should().ThrowAsync<OperationCanceledException>();

        _recorder.Events.Should().BeEmpty("a run canceled before the broken tests are reported must not report them");
    }

    public async Task RunTests_CancelledRightAfterTheDependencyWarning_DoesNotScheduleAnyWorker()
    {
        UnitTestElement first = CreateElement(typeof(FixtureA), "A");
        UnitTestElement second = CreateElement(typeof(FixtureB), "B");
        first.Dependencies = [new TestDependencyInfo(MissingClassName, null, proceedOnFailure: false)];

        var token = new TestRunCancellationToken();
        _messageLogger.OnMessage = (level, _) =>
        {
            if (level == MessageLevel.Warning)
            {
                token.Cancel();
            }
        };

        var tracker = new WorkerTracker(expectedWorkers: 2);
        var manager = new TestExecutionManager(tracker.Schedule);

        Func<Task> run = () => RunAsync(manager, [first, second], CreateRunSettings(workers: 2, ExecutionScope.ClassLevel), token);

        await run.Should().ThrowAsync<OperationCanceledException>();

        tracker.Scheduled.Should().Be(0, "a run canceled before the chunks are scheduled must not start any worker");
        _recorder.Events.Should().BeEmpty();
    }

    public async Task RunTests_ChunkDependingOnALaterChunk_RunsThePrerequisiteFirst()
    {
        UnitTestElement dependent = CreateElement(typeof(FixtureB), "B");
        UnitTestElement prerequisite = CreateElement(typeof(FixtureA), "A");
        dependent.Dependencies = [new TestDependencyInfo(typeof(FixtureA).FullName, "A", proceedOnFailure: false)];

        var manager = new TestExecutionManager(workerBody => workerBody());

        await RunAsync(manager, [dependent, prerequisite], CreateRunSettings(workers: 2, ExecutionScope.ClassLevel), new TestRunCancellationToken());

        _recorder.Events.Should().Equal("start:A", "result:A:Passed", "start:B", "result:B:Passed");
    }

    public async Task RunTests_MoreWorkersThanChunks_SchedulesOneWorkerPerChunk()
    {
        UnitTestElement[] tests = CreateIndependentElements();
        var tracker = new WorkerTracker(expectedWorkers: 3);
        var manager = new TestExecutionManager(tracker.Schedule);

        await RunAsync(manager, tests, CreateRunSettings(workers: 5, ExecutionScope.ClassLevel), new TestRunCancellationToken());

        tracker.Scheduled.Should().Be(3);
    }

    public async Task RunTests_FewerWorkersThanChunks_SchedulesOneWorkerPerConfiguredWorker()
    {
        UnitTestElement[] tests = CreateIndependentElements();
        var tracker = new WorkerTracker(expectedWorkers: 2);
        var manager = new TestExecutionManager(tracker.Schedule);

        await RunAsync(manager, tests, CreateRunSettings(workers: 2, ExecutionScope.ClassLevel), new TestRunCancellationToken());

        tracker.Scheduled.Should().Be(2);
    }

    public async Task RunTests_WorkerWithNothingReadyToRun_WaitsUntilAChunkBecomesReady()
    {
        TaskCompletionSource<bool> gate = HoldTestWhileStarting("A");
        var tracker = new WorkerTracker(expectedWorkers: 2);
        var manager = new TestExecutionManager(tracker.Schedule);

        Task run = RunAsync(manager, CreateTwoDependentsOfA(), CreateRunSettings(workers: 2, ExecutionScope.ClassLevel), new TestRunCancellationToken());
        bool idleWorkerIsWaiting;
        try
        {
            await ObserveWithinLimitAsync(tracker.AllScheduled);
            idleWorkerIsWaiting = !tracker[1].IsCompleted;
        }
        finally
        {
            gate.TrySetResult(true);
        }

        await CompleteWithinGuardAsync(run);

        idleWorkerIsWaiting.Should().BeTrue("the second worker has no ready chunk while the only runnable one is still executing, so it must wait instead of leaving");
        string[] events = [.. _recorder.Events];
        events.Take(2).Should().Equal("start:A", "result:A:Passed");
        events.Skip(2).Should().BeEquivalentTo("start:B", "result:B:Passed", "start:C", "result:C:Passed");
    }

    public async Task RunTests_CancelledWhileAWorkerWaitsForAReadyChunk_StopsThatWorker()
    {
        TaskCompletionSource<bool> gate = HoldTestWhileStarting("A");
        var tracker = new WorkerTracker(expectedWorkers: 2);
        var manager = new TestExecutionManager(tracker.Schedule);
        var token = new TestRunCancellationToken();

        Task run = RunAsync(manager, CreateTwoDependentsOfA(), CreateRunSettings(workers: 2, ExecutionScope.ClassLevel), token);
        bool idleWorkerStopped;
        try
        {
            await ObserveWithinLimitAsync(tracker.AllScheduled);
            Task idleWorker = tracker[1];
            token.Cancel();
            await ObserveWithinLimitAsync(idleWorker);
            idleWorkerStopped = idleWorker.IsCompleted;
        }
        finally
        {
            gate.TrySetResult(true);
        }

        await CompleteWithinGuardAsync(run);

        idleWorkerStopped.Should().BeTrue("cancelling the run must wake a worker that is waiting for a chunk to become ready");
    }

    public async Task RunTests_CancelledWhileAWorkerWaitsForAResourceLock_StopsThatWorker()
    {
        UnitTestElement first = CreateElement(typeof(FixtureA), "A");
        UnitTestElement second = CreateElement(typeof(FixtureB), "B");
        first.Dependencies = [new TestDependencyInfo(MissingClassName, null, proceedOnFailure: false)];
        first.ResourceLocks = [new ResourceLockInfo("shared-resource", ResourceAccessMode.ReadWrite)];
        second.ResourceLocks = [new ResourceLockInfo("shared-resource", ResourceAccessMode.ReadWrite)];

        TaskCompletionSource<bool> gate = HoldTestWhileStarting("A");
        var tracker = new WorkerTracker(expectedWorkers: 2);
        var manager = new TestExecutionManager(tracker.Schedule);
        var token = new TestRunCancellationToken();

        Task run = RunAsync(manager, [first, second], CreateRunSettings(workers: 2, ExecutionScope.ClassLevel), token);
        bool lockWaiterStopped;
        try
        {
            await ObserveWithinLimitAsync(tracker.AllScheduled);
            Task lockWaiter = tracker[1];
            token.Cancel();
            await ObserveWithinLimitAsync(lockWaiter);
            lockWaiterStopped = lockWaiter.IsCompleted;
        }
        finally
        {
            gate.TrySetResult(true);
        }

        await CompleteWithinGuardAsync(run);

        lockWaiterStopped.Should().BeTrue("cancelling the run must abort a worker that is still waiting for a resource lock held by another worker");
    }

    public async Task RunTests_WorkerCanceledWithInfoLoggingEnabled_LogsItOnce()
    {
        string source = Assembly.GetExecutingAssembly().Location;
        _provider.MockTraceLogger.SetupGet(logger => logger.IsInfoEnabled).Returns(true);
        var token = new TestRunCancellationToken();
        _recorder.OnStart = _ =>
        {
            token.Cancel();
            return Task.CompletedTask;
        };

        var manager = new TestExecutionManager(workerBody => workerBody());

        await RunAsync(manager, CreateTestsOfOneClassWithAMissingPrerequisite(), CreateRunSettings(workers: 1, ExecutionScope.ClassLevel), token);

        token.Canceled.Should().BeTrue();
        CountWorkerCanceledInfoCalls(source).Should().Be(1);
        InfoCallCount(WorkerCanceledFormat).Should().Be(1, "the message must be logged with its own format and nothing else");
    }

    public async Task RunTests_WorkerCanceledWithInfoLoggingDisabled_DoesNotLogIt()
    {
        string source = Assembly.GetExecutingAssembly().Location;
        _provider.MockTraceLogger.SetupGet(logger => logger.IsInfoEnabled).Returns(false);
        var token = new TestRunCancellationToken();
        _recorder.OnStart = _ =>
        {
            token.Cancel();
            return Task.CompletedTask;
        };

        var manager = new TestExecutionManager(workerBody => workerBody());

        await RunAsync(manager, CreateTestsOfOneClassWithAMissingPrerequisite(), CreateRunSettings(workers: 1, ExecutionScope.ClassLevel), token);

        token.Canceled.Should().BeTrue();
        _recorder.Events.Should().Contain("start:First");
        CountWorkerCanceledInfoCalls(source).Should().Be(0);
        InfoCallCount(WorkerCanceledFormat).Should().Be(0);
    }

    private static string CreateRunSettings(int workers, ExecutionScope scope)
        => $$"""
            <RunSettings>
              <RunConfiguration>
                <DisableAppDomain>True</DisableAppDomain>
              </RunConfiguration>
              <MSTest>
                <Parallelize>
                  <Workers>{{workers}}</Workers>
                  <Scope>{{scope}}</Scope>
                </Parallelize>
              </MSTest>
            </RunSettings>
            """;

    private static string CreateSequentialRunSettings()
        => """
            <RunSettings>
              <RunConfiguration>
                <DisableAppDomain>True</DisableAppDomain>
              </RunConfiguration>
            </RunSettings>
            """;

    private static UnitTestElement CreateElement(Type type, string methodName)
    {
        MethodInfo methodInfo = type.GetMethod(methodName)!;
        return new UnitTestElement(new TestMethod(methodInfo.Name, hierarchyValues: null, methodInfo.Name, type.FullName!, Assembly.GetExecutingAssembly().Location, displayName: null, null));
    }

    // A, B and C live in different classes; A also declares a dependency on a missing class so that the dependency graph is built.
    private static UnitTestElement[] CreateIndependentElements()
    {
        UnitTestElement a = CreateElement(typeof(FixtureA), "A");
        a.Dependencies = [new TestDependencyInfo(MissingClassName, null, proceedOnFailure: false)];
        return [a, CreateElement(typeof(FixtureB), "B"), CreateElement(typeof(FixtureC), "C")];
    }

    private static UnitTestElement[] CreateTwoDependentsOfA()
    {
        UnitTestElement a = CreateElement(typeof(FixtureA), "A");
        UnitTestElement b = CreateElement(typeof(FixtureB), "B");
        UnitTestElement c = CreateElement(typeof(FixtureC), "C");
        b.Dependencies = [new TestDependencyInfo(typeof(FixtureA).FullName, "A", proceedOnFailure: false)];
        c.Dependencies = [new TestDependencyInfo(typeof(FixtureA).FullName, "A", proceedOnFailure: false)];
        return [a, b, c];
    }

    private static UnitTestElement[] CreateTestsOfOneClassWithAMissingPrerequisite()
    {
        UnitTestElement first = CreateElement(typeof(FixtureTwoTests), "First");
        first.Dependencies = [new TestDependencyInfo(MissingClassName, null, proceedOnFailure: false)];
        return [first, CreateElement(typeof(FixtureTwoTests), "Second")];
    }

    private static string DescribeCycle(string root, string other)
        => string.Format(CultureInfo.CurrentCulture, Resource.DependsOnCycle, $"{root} > {other} > {root}");

    private static bool IsWorkerCanceled(string format, object?[] args, string source)
        => format == WorkerCanceledFormat && args.Length == 1 && Equals(args[0], source);

    private static Task ObserveWithinLimitAsync(Task task)
        => Task.WhenAny(task, Task.Delay(CancellationObservationLimit));

    private static async Task CompleteWithinGuardAsync(Task run)
    {
        Task finished = await Task.WhenAny(run, Task.Delay(HangGuard));
        (finished == run).Should().BeTrue("the run must complete once the held test is released");
        await run;
    }

    private Task RunAsync(TestExecutionManager manager, UnitTestElement[] tests, string runSettingsXml, TestRunCancellationToken token)
    {
        MSTestSettings.PopulateSettings(runSettingsXml, logger: null, configuration: null);
        return manager.RunTestsAsync(tests, new DeploymentContext(null, runSettingsXml), _messageLogger, _recorder, null, token);
    }

    // The recorder reports the start of the named test with a task that only completes once the returned gate is opened.
    private TaskCompletionSource<bool> HoldTestWhileStarting(string methodName)
    {
        var gate = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        _recorder.OnStart = test => test.TestMethod.Name == methodName ? gate.Task : Task.CompletedTask;
        return gate;
    }

    private void RecordInfo(string format, object?[] args)
    {
        lock (_infoCalls)
        {
            _infoCalls.Add((format, args));
        }
    }

    private int CountWorkerCanceledInfoCalls(string source)
    {
        lock (_infoCalls)
        {
            return _infoCalls.Count(call => IsWorkerCanceled(call.Format, call.Args, source));
        }
    }

    private int InfoCallCount(string format)
    {
        lock (_infoCalls)
        {
            return _infoCalls.Count(call => call.Format == format);
        }
    }

    private sealed class WorkerTracker(int expectedWorkers)
    {
        private readonly List<Task> _workers = [];
        private readonly TaskCompletionSource<bool> _allScheduled = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task AllScheduled => _allScheduled.Task;

        public int Scheduled
        {
            get
            {
                lock (_workers)
                {
                    return _workers.Count;
                }
            }
        }

        public Task this[int index]
        {
            get
            {
                lock (_workers)
                {
                    return _workers[index];
                }
            }
        }

        // Runs the worker inline, like a test host that schedules workers on the calling thread, and remembers it.
        public Task Schedule(Func<Task> workerBody)
        {
            Task worker = workerBody();
            lock (_workers)
            {
                _workers.Add(worker);
                if (_workers.Count == expectedWorkers)
                {
                    _allScheduled.TrySetResult(true);
                }
            }

            return worker;
        }
    }

    private sealed class RecordingMessageLogger : IAdapterMessageLogger
    {
        private readonly List<(MessageLevel Level, string Message)> _messages = [];

        public Action<MessageLevel, string>? OnMessage { get; set; }

        public IReadOnlyList<(MessageLevel Level, string Message)> Messages
        {
            get
            {
                lock (_messages)
                {
                    return [.. _messages];
                }
            }
        }

        public void SendMessage(MessageLevel level, string message)
        {
            lock (_messages)
            {
                _messages.Add((level, message));
            }

            OnMessage?.Invoke(level, message);
        }
    }

    private sealed class RecordingTestResultRecorder : ITestResultRecorder
    {
        private readonly List<string> _events = [];
        private readonly List<(string Test, bool SupportsExecutionActivityLease)> _starts = [];
        private readonly List<(string Test, Exception? Failure)> _results = [];

        public Func<UnitTestElement, Task>? OnStart { get; set; }

        public IReadOnlyList<string> Events
        {
            get
            {
                lock (_events)
                {
                    return [.. _events];
                }
            }
        }

        public IReadOnlyList<(string Test, bool SupportsExecutionActivityLease)> Starts
        {
            get
            {
                lock (_events)
                {
                    return [.. _starts];
                }
            }
        }

        public IReadOnlyList<(string Test, Exception? Failure)> Results
        {
            get
            {
                lock (_events)
                {
                    return [.. _results];
                }
            }
        }

        public void PrepareResults(UnitTestElement test, FrameworkTestResult[] results)
        {
        }

        public Task RecordStartAsync(UnitTestElement test)
        {
            lock (_events)
            {
                _events.Add("start:" + test.TestMethod.Name);
                _starts.Add((test.TestMethod.Name, test.SupportsExecutionActivityLease));
            }

            return OnStart?.Invoke(test) ?? Task.CompletedTask;
        }

        public Task RecordEmptyResultAsync(UnitTestElement test) => Task.CompletedTask;

        public Task<bool> RecordResultAsync(UnitTestElement test, FrameworkTestResult result, DateTimeOffset startTime, DateTimeOffset endTime)
        {
            lock (_events)
            {
                _events.Add("result:" + test.TestMethod.Name + ":" + result.Outcome);
                _results.Add((test.TestMethod.Name, result.TestFailureException));
            }

            return Task.FromResult(false);
        }
    }

    private sealed class FixtureTestClassAttribute : TestClassAttribute;

    [FixtureTestClass]
    private sealed class FixtureA
    {
        [TestMethod]
        public void A()
        {
        }
    }

    [FixtureTestClass]
    private sealed class FixtureB
    {
        [TestMethod]
        public void B()
        {
        }
    }

    [FixtureTestClass]
    private sealed class FixtureC
    {
        [TestMethod]
        public void C()
        {
        }
    }

    [FixtureTestClass]
    private sealed class FixtureTwoTests
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
}
