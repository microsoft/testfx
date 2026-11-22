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

using ExecutionScope = Microsoft.VisualStudio.TestTools.UnitTesting.ExecutionScope;
using FrameworkTestResult = Microsoft.VisualStudio.TestTools.UnitTesting.TestResult;

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Execution;

public sealed class TestExecutionManagerParallelExecutionMutationTests : TestContainer
{
    private const string WorkerCanceledFormat = "Parallel test worker canceled for source {0}";

    // Only reached when the code under test is broken: a healthy run observes the cancellation within milliseconds.
    private static readonly TimeSpan CancellationObservationLimit = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan HangGuard = TimeSpan.FromSeconds(60);

    private readonly TestablePlatformServiceProvider _provider;
    private readonly RecordingMessageLogger _messageLogger = new();
    private readonly RecordingTestResultRecorder _recorder = new();
    private readonly List<(string Format, object?[] Args)> _infoCalls = [];
    private Action<string, object?[]>? _onInfo;

    public TestExecutionManagerParallelExecutionMutationTests()
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

    public async Task RunTests_CancelledRightAfterTheParallelizationBanner_DoesNotScheduleAnyWorker()
    {
        string source = Assembly.GetExecutingAssembly().Location;
        string banner = string.Format(CultureInfo.CurrentCulture, Resource.TestParallelizationBanner, source, 2, ExecutionScope.MethodLevel);
        var token = new TestRunCancellationToken();

        // The banner is the last thing reported before the parallel scheduler starts, so this cancels the run
        // after the source-level cancellation checks and right before the workers would be scheduled.
        _messageLogger.OnMessage = (level, message) =>
        {
            if (level == MessageLevel.Informational && message == banner)
            {
                token.Cancel();
            }
        };

        int scheduledWorkers = 0;
        var manager = new TestExecutionManager(workerBody =>
        {
            scheduledWorkers++;
            return workerBody();
        });

        Func<Task> run = () => RunAsync(manager, [CreateElement(typeof(FixtureA)), CreateElement(typeof(FixtureB))], CreateRunSettings(workers: 2, ExecutionScope.MethodLevel), token);

        await run.Should().ThrowAsync<OperationCanceledException>();

        scheduledWorkers.Should().Be(0, "a run canceled before the workers are scheduled must not start any worker");
        _recorder.Events.Should().BeEmpty();
    }

    public async Task RunTests_CancelledWhileAWorkerWaitsForAResourceLock_AbortsTheWait()
    {
        UnitTestElement first = CreateElement(typeof(FixtureA));
        UnitTestElement second = CreateElement(typeof(FixtureB));
        first.ResourceLocks = [new ResourceLockInfo("shared-resource", ResourceAccessMode.ReadWrite)];
        second.ResourceLocks = [new ResourceLockInfo("shared-resource", ResourceAccessMode.ReadWrite)];

        string source = Assembly.GetExecutingAssembly().Location;
        _provider.MockTraceLogger.SetupGet(logger => logger.IsInfoEnabled).Returns(true);

        // Only the worker waiting for the lock can report its cancellation while the first worker is held back.
        var waitingWorkerCanceled = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        _onInfo = (format, args) =>
        {
            if (IsWorkerCanceled(format, args, source))
            {
                waitingWorkerCanceled.TrySetResult(true);
            }
        };

        // The worker running the first chunk holds the lock until it is released here.
        var releaseLockHolder = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        _recorder.OnStart = _ => releaseLockHolder.Task;

        var token = new TestRunCancellationToken();
        int scheduledWorkers = 0;
        var manager = new TestExecutionManager(workerBody =>
        {
            scheduledWorkers++;
            return workerBody();
        });

        Task run = RunAsync(manager, [first, second], CreateRunSettings(workers: 2, ExecutionScope.ClassLevel), token);
        bool canceledWhileLockIsHeld;
        try
        {
            token.Cancel();
            await Task.WhenAny(waitingWorkerCanceled.Task, Task.Delay(CancellationObservationLimit));
            canceledWhileLockIsHeld = waitingWorkerCanceled.Task.IsCompleted;
        }
        finally
        {
            releaseLockHolder.TrySetResult(true);
        }

        await CompleteWithinGuardAsync(run);

        scheduledWorkers.Should().Be(2);
        canceledWhileLockIsHeld.Should().BeTrue("cancelling the run must abort a worker that is still waiting for a resource lock held by another worker");
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

        await RunAsync(manager, [CreateElement(typeof(FixtureTwoTests), "First"), CreateElement(typeof(FixtureTwoTests), "Second")], CreateRunSettings(workers: 1, ExecutionScope.ClassLevel), token);

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

        await RunAsync(manager, [CreateElement(typeof(FixtureTwoTests), "First"), CreateElement(typeof(FixtureTwoTests), "Second")], CreateRunSettings(workers: 1, ExecutionScope.ClassLevel), token);

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

    private static UnitTestElement CreateElement(Type type, string methodName = "Test")
    {
        MethodInfo methodInfo = type.GetMethod(methodName)!;
        return new UnitTestElement(new TestMethod(methodInfo.Name, hierarchyValues: null, methodInfo.Name, type.FullName!, Assembly.GetExecutingAssembly().Location, displayName: null, null));
    }

    private static bool IsWorkerCanceled(string format, object?[] args, string source)
        => format == WorkerCanceledFormat && args.Length == 1 && Equals(args[0], source);

    private static async Task CompleteWithinGuardAsync(Task run)
    {
        Task finished = await Task.WhenAny(run, Task.Delay(HangGuard));
        (finished == run).Should().BeTrue("the run must complete once the held worker is released");
        await run;
    }

    private Task RunAsync(TestExecutionManager manager, UnitTestElement[] tests, string runSettingsXml, TestRunCancellationToken token)
    {
        MSTestSettings.PopulateSettings(runSettingsXml, logger: null, configuration: null);
        return manager.RunTestsAsync(tests, new DeploymentContext(null, runSettingsXml), _messageLogger, _recorder, null, token);
    }

    private void RecordInfo(string format, object?[] args)
    {
        lock (_infoCalls)
        {
            _infoCalls.Add((format, args));
        }

        _onInfo?.Invoke(format, args);
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

    private sealed class RecordingMessageLogger : IAdapterMessageLogger
    {
        public Action<MessageLevel, string>? OnMessage { get; set; }

        public void SendMessage(MessageLevel level, string message) => OnMessage?.Invoke(level, message);
    }

    private sealed class RecordingTestResultRecorder : ITestResultRecorder
    {
        private readonly List<string> _events = [];

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

        public void PrepareResults(UnitTestElement test, FrameworkTestResult[] results)
        {
        }

        public Task RecordStartAsync(UnitTestElement test)
        {
            lock (_events)
            {
                _events.Add("start:" + test.TestMethod.Name);
            }

            return OnStart?.Invoke(test) ?? Task.CompletedTask;
        }

        public Task RecordEmptyResultAsync(UnitTestElement test) => Task.CompletedTask;

        public Task<bool> RecordResultAsync(UnitTestElement test, FrameworkTestResult result, DateTimeOffset startTime, DateTimeOffset endTime)
        {
            lock (_events)
            {
                _events.Add("result:" + test.TestMethod.Name + ":" + result.Outcome);
            }

            return Task.FromResult(false);
        }
    }

    private sealed class FixtureTestClassAttribute : TestClassAttribute;

    [FixtureTestClass]
    private sealed class FixtureA
    {
        [TestMethod]
        public void Test()
        {
        }
    }

    [FixtureTestClass]
    private sealed class FixtureB
    {
        [TestMethod]
        public void Test()
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
