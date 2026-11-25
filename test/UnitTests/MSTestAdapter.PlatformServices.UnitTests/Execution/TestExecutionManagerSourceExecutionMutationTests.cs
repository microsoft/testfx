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

#if DEBUG && !NETFRAMEWORK
using Microsoft.Testing.TestInfrastructure;
#endif

using Moq;

using TestFramework.ForTestingMSTest;

using ExecutionScope = Microsoft.VisualStudio.TestTools.UnitTesting.ExecutionScope;
using FrameworkTestResult = Microsoft.VisualStudio.TestTools.UnitTesting.TestResult;

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Execution;

public sealed class TestExecutionManagerSourceExecutionMutationTests : TestContainer
{
    private const string MissingClassName = "Missing.Prerequisite.Class";
    private const string RunnerCreatedFormat = "Created unit-test runner {0}";
    private const string SourceExecutedFormat = "Executed tests belonging to source {0}";
    private const string ConcatWithOverwritesPrefix = "DictionaryHelper.ConcatWithOverwrites";

    private readonly HostSwitchingPlatformServiceProvider _provider;
    private readonly RecordingMessageLogger _messageLogger = new();
    private readonly RecordingTestResultRecorder _recorder = new();
    private readonly List<(string Format, object?[] Args)> _infoCalls = [];
    private readonly List<string> _propertySources = [];
    private Action<string>? _onGetProperties;
    private Action<string, object?[]>? _onInfo;

    public TestExecutionManagerSourceExecutionMutationTests()
    {
        _provider = new HostSwitchingPlatformServiceProvider();
        PlatformServiceProvider.Instance = _provider;

        _provider.MockFileOperations.Setup(fileOperations => fileOperations.LoadAssembly(It.IsAny<string>()))
            .Returns(Assembly.GetExecutingAssembly());
        _provider.MockTestSourceHost.Setup(host => host.CreateInstanceForType(It.IsAny<Type>(), It.IsAny<object[]>()))
            .Returns((Type type, object[] args) => Activator.CreateInstance(type, args));
        _provider.MockSettingsProvider.Setup(settingsProvider => settingsProvider.GetProperties(It.IsAny<string>()))
            .Returns((string source) =>
            {
                lock (_propertySources)
                {
                    _propertySources.Add(source);
                }

                _onGetProperties?.Invoke(source);
                return new Dictionary<string, object>();
            });
        _provider.MockTraceLogger.Setup(logger => logger.Info(It.IsAny<string>(), It.IsAny<object?[]>()))
            .Callback((string format, object?[] args) =>
            {
                RecordInfo(format, args);
                _onInfo?.Invoke(format, args);
            });
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

    public async Task RunTests_RandomizedOrder_ShufflesTheOrderInWhichSourcesAreExecuted()
    {
        string[] sources = [.. Enumerable.Range(0, 5).Select(index => CreateSourcePath($"shuffle{index}"))];
        int seed = FindSeedThatReordersSources(sources);
        UnitTestElement[] tests = [.. sources.Select(source => CreateElement(typeof(FixtureA), "A", source, "A"))];

        await RunAsync(CreateManager(), tests, CreateRandomizedRunSettings(seed), new TestRunCancellationToken());

        _propertySources.Should().Equal(ShuffleLikeProduction(sources, seed));
        _recorder.Events.Should().HaveCount(2 * sources.Length);
    }

    public async Task ExecuteTests_PartitionedRun_ValidatesDeclaredDependenciesAgainstTheUnpartitionedTests()
    {
        string source = CreateSourcePath("partitioned");
        UnitTestElement dependent = CreateElement(typeof(FixtureA), "A", source, "A");
        UnitTestElement prerequisite = CreateElement(typeof(FixtureB), "B", source, "B");
        PopulateDeclaredDependency(dependent, prerequisite);
        TestExecutionManager manager = CreateManager();
        typeof(TestExecutionManager).GetField("_unpartitionedTests", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(manager, new List<UnitTestElement> { dependent, prerequisite });

        await manager.ExecuteTestsAsync([prerequisite], new DeploymentContext(null, null), _messageLogger, _recorder, null, isDeploymentDone: false);

        _messageLogger.Messages.Where(message => message.Level == MessageLevel.Warning).Should().BeEmpty();
        _recorder.Events.Should().Equal("start:B", "result:B:Passed");
    }

    public async Task RunTests_CancelledAfterTheFirstSource_DoesNotStartTheNextSource()
    {
        string sourceA = CreateSourcePath("cancel-a");
        string sourceB = CreateSourcePath("cancel-b");
        _provider.MockTraceLogger.SetupGet(logger => logger.IsInfoEnabled).Returns(true);
        var token = new TestRunCancellationToken();
        _onInfo = (format, args) =>
        {
            if (format == SourceExecutedFormat && Equals(args[0], sourceA))
            {
                token.Cancel();
            }
        };

        Func<Task> act = () => RunAsync(
            CreateManager(),
            [CreateElement(typeof(FixtureA), "A", sourceA, "A"), CreateElement(typeof(FixtureB), "B", sourceB, "B")],
            CreateSequentialRunSettings(),
            token);

        await act.Should().ThrowAsync<OperationCanceledException>();
        _propertySources.Should().Equal(sourceA);
        InfoCallCount(RunnerCreatedFormat).Should().Be(1);
        _recorder.Events.Should().Equal("start:A", "result:A:Passed");
    }

#if DEBUG && !NETFRAMEWORK
    public async Task ExecuteTests_SourceIsEmpty_FailsTheSourceAssertion()
    {
        var assertions = new List<string?>();
        using var observer = new DiagnosticAssertionObserver(assertions.Add);
        UnitTestElement test = CreateElement(typeof(FixtureA), "A", CreateSourcePath("empty"), "A");
        var testWithoutSource = new UnitTestElement(test.TestMethod.CloneWithSource(string.Empty));

        await CreateManager().ExecuteTestsAsync([testWithoutSource], new DeploymentContext(null, null), _messageLogger, _recorder, null, isDeploymentDone: false);

        assertions.Should().Contain("Source cannot be empty");
    }
#endif

    public async Task RunTests_RealTestSourceHostWithoutAppDomains_RunsWithExecutionActivityLeases()
    {
        _provider.UseRealTestSourceHost = true;
        var factory = new CountingTestClassInstanceFactory();
        using IDisposable factoryScope = TestClassInstanceFactoryProvider.Push(factory);

        await RunAsync(CreateManager(), [CreateElement(typeof(FixtureA), "A", CreateSourcePath("real-host"), "A")], CreateSequentialRunSettings(), new TestRunCancellationToken());

        _recorder.Events.Should().Equal("start:A", "result:A:Passed");
        _recorder.Starts.Should().ContainSingle().Which.SupportsExecutionActivityLease.Should().BeTrue();
        factory.CreatedInstances.Should().BeGreaterThan(0);
    }

    public async Task RunTests_ClassInstanceFactoryInstalledWithoutAppDomains_RunsTheTests()
    {
        var factory = new CountingTestClassInstanceFactory();
        using IDisposable factoryScope = TestClassInstanceFactoryProvider.Push(factory);

        await RunAsync(CreateManager(), [CreateElement(typeof(FixtureA), "A", CreateSourcePath("factory"), "A")], CreateSequentialRunSettings(), new TestRunCancellationToken());

        _recorder.Events.Should().Equal("start:A", "result:A:Passed");
        factory.CreatedInstances.Should().BeGreaterThan(0);
    }

    public async Task RunTests_InfoLoggingEnabled_LogsTheRunnerCreationAndTheSourceExecution()
    {
        string source = CreateSourcePath("logging-enabled");
        _provider.MockTraceLogger.SetupGet(logger => logger.IsInfoEnabled).Returns(true);

        await RunAsync(CreateManager(), [CreateElement(typeof(FixtureA), "A", source, "A")], CreateSequentialRunSettings(), new TestRunCancellationToken());

        _recorder.Events.Should().Equal("start:A", "result:A:Passed");
        InfoFirstArguments(RunnerCreatedFormat).Should().Equal(source);
        InfoFirstArguments(SourceExecutedFormat).Should().Equal(source);
    }

    public async Task RunTests_InfoLoggingDisabled_DoesNotLogTheRunnerCreationNorTheSourceExecution()
    {
        _provider.MockTraceLogger.SetupGet(logger => logger.IsInfoEnabled).Returns(false);

        await RunAsync(CreateManager(), [CreateElement(typeof(FixtureA), "A", CreateSourcePath("logging-disabled"), "A")], CreateSequentialRunSettings(), new TestRunCancellationToken());

        _recorder.Events.Should().Equal("start:A", "result:A:Passed");
        InfoCallCount(RunnerCreatedFormat).Should().Be(0);
        InfoCallCount(SourceExecutedFormat).Should().Be(0);
    }

    public async Task RunTests_WithoutSessionParameters_DoesNotMergeThemWithTheSourceProperties()
    {
        _provider.MockTraceLogger.SetupGet(logger => logger.IsInfoEnabled).Returns(true);

        await RunAsync(CreateManager(), [CreateElement(typeof(FixtureA), "A", CreateSourcePath("without-parameters"), "A")], CreateSequentialRunSettings(), new TestRunCancellationToken());

        _recorder.Events.Should().Equal("start:A", "result:A:Passed");
        InfoCallCountStartingWith(ConcatWithOverwritesPrefix).Should().Be(0);
    }

    public async Task RunTests_WithSessionParameters_MergesThemWithTheSourceProperties()
    {
        _provider.MockTraceLogger.SetupGet(logger => logger.IsInfoEnabled).Returns(true);

        await RunAsync(CreateManager(), [CreateElement(typeof(FixtureA), "A", CreateSourcePath("with-parameters"), "A")], CreateTestRunParametersRunSettings(), new TestRunCancellationToken());

        _recorder.Events.Should().Equal("start:A", "result:A:Passed");
        InfoCallCountStartingWith(ConcatWithOverwritesPrefix).Should().BeGreaterThan(0);
    }

    public async Task RunTests_CancelledWhileReadingTheSourceProperties_DoesNotCreateTheAssemblySettingsProvider()
    {
        var token = new TestRunCancellationToken();
        _onGetProperties = _ => token.Cancel();

        Func<Task> act = () => RunAsync(
            CreateManager(),
            [CreateElement(typeof(FixtureA), "A", CreateSourcePath("cancel-properties"), "A")],
            CreateSequentialRunSettings(),
            token);

        await act.Should().ThrowAsync<OperationCanceledException>();
        _provider.MockTestSourceHost.Verify(host => host.CreateInstanceForType(typeof(TestAssemblySettingsProvider), It.IsAny<object[]>()), Times.Never);
        _recorder.Events.Should().BeEmpty();
    }

    public async Task RunTests_CancelledDuringTheRun_CancelsTheUnitTestRunnerOfTheSource()
    {
        _provider.MockTraceLogger.SetupGet(logger => logger.IsInfoEnabled).Returns(true);
        var runToken = new TestRunCancellationToken();
        var runnerToken = new TestRunCancellationToken();
        _onInfo = (format, _) =>
        {
            if (format == SourceExecutedFormat)
            {
                // The runner cancels the token of the platform service provider; swapping it makes the forwarding observable.
                _provider.TestRunCancellationToken = runnerToken;
                runToken.Cancel();
            }
        };

        await RunAsync(CreateManager(), [CreateElement(typeof(FixtureA), "A", CreateSourcePath("cancel-runner"), "A")], CreateSequentialRunSettings(), runToken);

        runToken.Canceled.Should().BeTrue();
        runnerToken.Canceled.Should().BeTrue();
    }

    public async Task RunTests_ParallelScopeInRunSettings_OverridesTheScopeOfTheAssembly()
    {
        string source = CreateSourcePath("scope");

        await RunAsync(CreateManager(), [CreateElement(typeof(FixtureA), "A", source, "A")], CreateParallelRunSettings(workers: 2, ExecutionScope.MethodLevel), new TestRunCancellationToken());

        _recorder.Events.Should().Equal("start:A", "result:A:Passed");
        _messageLogger.Messages.Should().Contain((MessageLevel.Informational, DescribeParallelization(source, workers: 2, ExecutionScope.MethodLevel)));
    }

    public async Task RunTests_ParallelizationDisabledInRunSettings_DoesNotParallelizeTheSource()
    {
        string source = CreateSourcePath("disabled-parallelization");

        await RunAsync(CreateManager(), [CreateElement(typeof(FixtureA), "A", source, "A")], CreateParallelizationDisabledRunSettings(), new TestRunCancellationToken());

        _recorder.Events.Should().Equal("start:A", "result:A:Passed");
        _messageLogger.Messages.Should().NotContain((MessageLevel.Informational, DescribeParallelization(source, workers: 2, ExecutionScope.ClassLevel)));
    }

    public async Task RunTests_OrderedByName_SortsTheTestsByManagedTypeNameFirst()
    {
        string source = CreateSourcePath("ordered-types");
        UnitTestElement third = CreateElement(typeof(ClassR), "Third", source, "Third");
        third.Dependencies = [new TestDependencyInfo(MissingClassName, null, proceedOnFailure: false)];

        await RunAsync(
            CreateManager(),
            [CreateElement(typeof(ClassQ), "Alpha", source, "Alpha"), CreateElement(typeof(ClassP), "Zulu", source, "Zulu"), third],
            CreateOrderedRunSettings(),
            new TestRunCancellationToken());

        StartedTests().Should().Equal("Zulu", "Alpha", "Third");
    }

    public async Task RunTests_OrderedByNameWithoutManagedMethodNames_KeepsTheSuppliedOrder()
    {
        string source = CreateSourcePath("ordered-unmanaged");
        UnitTestElement third = CreateElement(typeof(ClassR), "Third", source, managedMethodName: null);
        third.Dependencies = [new TestDependencyInfo(MissingClassName, null, proceedOnFailure: false)];

        await RunAsync(
            CreateManager(),
            [CreateElement(typeof(ClassQ), "Alpha", source, managedMethodName: null), CreateElement(typeof(ClassP), "Zulu", source, managedMethodName: null), third],
            CreateOrderedRunSettings(),
            new TestRunCancellationToken());

        StartedTests().Should().Equal("Alpha", "Zulu", "Third");
    }

    public async Task RunTests_OrderedByNameWithBlankManagedMethodName_SortsItLikeAMissingOne()
    {
        string source = CreateSourcePath("ordered-blank");
        UnitTestElement second = CreateElement(typeof(FixtureTwoTests), "Second", source, managedMethodName: null);
        second.Dependencies = [new TestDependencyInfo(MissingClassName, null, proceedOnFailure: false)];

        await RunAsync(
            CreateManager(),
            [CreateElement(typeof(FixtureTwoTests), "First", source, managedMethodName: " "), second],
            CreateOrderedRunSettings(),
            new TestRunCancellationToken());

        StartedTests().Should().Equal("First", "Second");
    }

    public async Task RunTests_GracefulStopRequested_ForcesTheClassCleanupOfTheSource()
    {
        string source = CreateSourcePath("graceful-stop");
        ForceCleanupFixture.CleanupCount = 0;
        _recorder.OnStart = test =>
        {
            if (test.TestMethod.Name == "First")
            {
                _provider.IsGracefulStopRequested = true;
            }

            return Task.CompletedTask;
        };

        await RunAsync(
            CreateManager(),
            [CreateElement(typeof(ForceCleanupFixture), "First", source, "First"), CreateElement(typeof(ForceCleanupFixture), "Second", source, "Second")],
            CreateSequentialRunSettings(),
            new TestRunCancellationToken());

        _recorder.Events.Should().Equal("start:First", "result:First:Passed");
        ForceCleanupFixture.CleanupCount.Should().Be(1);
    }

    public async Task RunTests_PreloadEnabled_PreloadsReferencedAssembliesBeforeTheFirstTestStarts()
    {
        bool preloadedWhenFirstTestStarted = false;
        _recorder.OnStart = _ =>
        {
            preloadedWhenFirstTestStarted = _messageLogger.Messages.Any(IsPreloadSummary);
            return Task.CompletedTask;
        };

        await RunAsync(CreateManager(), [CreateElement(typeof(FixtureA), "A", CreateSourcePath("preload-enabled"), "A")], CreatePreloadRunSettings("true"), new TestRunCancellationToken());

        preloadedWhenFirstTestStarted.Should().BeTrue();
        _messageLogger.Messages.Count(IsPreloadSummary).Should().Be(1);
        _recorder.Events.Should().Equal("start:A", "result:A:Passed");
    }

    public async Task RunTests_PreloadNotConfigured_DoesNotPreloadReferencedAssemblies()
    {
        await RunAsync(CreateManager(), [CreateElement(typeof(FixtureA), "A", CreateSourcePath("preload-default"), "A")], CreateSequentialRunSettings(), new TestRunCancellationToken());

        _messageLogger.Messages.Count(IsPreloadSummary).Should().Be(0);
        _recorder.Events.Should().Equal("start:A", "result:A:Passed");
    }

    public async Task RunTests_PreloadExplicitlyDisabled_DoesNotPreloadReferencedAssemblies()
    {
        await RunAsync(CreateManager(), [CreateElement(typeof(FixtureA), "A", CreateSourcePath("preload-disabled"), "A")], CreatePreloadRunSettings("false"), new TestRunCancellationToken());

        _messageLogger.Messages.Count(IsPreloadSummary).Should().Be(0);
        _recorder.Events.Should().Equal("start:A", "result:A:Passed");
    }

    private static bool IsPreloadSummary((MessageLevel Level, string Message) entry)
        => entry.Level == MessageLevel.Informational
            && entry.Message.StartsWith(Resource.PreloadReferencedAssembliesSummary.Substring(0, Resource.PreloadReferencedAssembliesSummary.IndexOf("{0}", StringComparison.Ordinal)), StringComparison.Ordinal);

    private static string CreatePreloadRunSettings(string value)
        => $$"""
            <RunSettings>
              <RunConfiguration>
                <DisableAppDomain>True</DisableAppDomain>
              </RunConfiguration>
              <MSTest>
                <PreloadReferencedAssemblies>{{value}}</PreloadReferencedAssemblies>
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

    private static string CreateParallelRunSettings(int workers, ExecutionScope scope)
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

    private static string CreateParallelizationDisabledRunSettings()
        => """
            <RunSettings>
              <RunConfiguration>
                <DisableAppDomain>True</DisableAppDomain>
                <DisableParallelization>true</DisableParallelization>
              </RunConfiguration>
              <MSTest>
                <Parallelize>
                  <Workers>2</Workers>
                  <Scope>ClassLevel</Scope>
                </Parallelize>
              </MSTest>
            </RunSettings>
            """;

    private static string CreateOrderedRunSettings()
        => """
            <RunSettings>
              <RunConfiguration>
                <DisableAppDomain>True</DisableAppDomain>
              </RunConfiguration>
              <MSTest>
                <OrderTestsByNameInClass>true</OrderTestsByNameInClass>
              </MSTest>
            </RunSettings>
            """;

    private static string CreateRandomizedRunSettings(int seed)
        => $$"""
            <RunSettings>
              <RunConfiguration>
                <DisableAppDomain>True</DisableAppDomain>
              </RunConfiguration>
              <MSTest>
                <RandomizeTestOrder>true</RandomizeTestOrder>
                <RandomTestOrderSeed>{{seed}}</RandomTestOrderSeed>
              </MSTest>
            </RunSettings>
            """;

    private static string CreateTestRunParametersRunSettings()
        => """
            <RunSettings>
              <RunConfiguration>
                <DisableAppDomain>True</DisableAppDomain>
              </RunConfiguration>
              <TestRunParameters>
                <Parameter name="p1" value="v1" />
              </TestRunParameters>
            </RunSettings>
            """;

    private static string CreateSourcePath(string name) => Path.Combine(Environment.CurrentDirectory, $"p04-{name}.dll");

    private static TestExecutionManager CreateManager() => new(workerBody => workerBody());

    private static UnitTestElement CreateElement(Type type, string methodName, string source, string? managedMethodName)
    {
        MethodInfo methodInfo = type.GetMethod(methodName)!;
        return new UnitTestElement(new TestMethod(managedMethodName, hierarchyValues: null, methodInfo.Name, type.FullName!, source, displayName: null, null));
    }

    private static string DescribeParallelization(string source, int workers, ExecutionScope scope)
        => string.Format(CultureInfo.CurrentCulture, Resource.TestParallelizationBanner, source, workers, scope);

    // Mirrors the Fisher-Yates shuffle that the production code applies to the sources.
    private static string[] ShuffleLikeProduction(string[] items, int seed)
    {
        string[] shuffled = [.. items];
        var random = new Random(seed);
        for (int i = shuffled.Length - 1; i > 0; i--)
        {
            int j = random.Next(i + 1);
            (shuffled[i], shuffled[j]) = (shuffled[j], shuffled[i]);
        }

        return shuffled;
    }

    private static int FindSeedThatReordersSources(string[] sources)
    {
        for (int seed = 1; seed < 1000; seed++)
        {
            if (!ShuffleLikeProduction(sources, seed).SequenceEqual(sources))
            {
                return seed;
            }
        }

        throw new InvalidOperationException("No seed reorders the sources.");
    }

    private static void PopulateDeclaredDependency(UnitTestElement dependent, UnitTestElement prerequisite)
    {
        var values = new Dictionary<string, string>
        {
            ["mstest"] = "configured",
            ["mstest:execution:dependencies:nodes:0:test"] = dependent.TestMethod.FullyQualifiedName,
            ["mstest:execution:dependencies:nodes:0:dependsOn:0"] = prerequisite.TestMethod.FullyQualifiedName,
        };
        var configuration = new Mock<IConfiguration>();
        configuration.Setup(setting => setting[It.IsAny<string>()])
            .Returns((string key) => values.TryGetValue(key, out string? value) ? value : null);
        MSTestSettings.PopulateSettings(null, logger: null, configuration.Object);
    }

    private Task RunAsync(TestExecutionManager manager, UnitTestElement[] tests, string runSettingsXml, TestRunCancellationToken token)
    {
        MSTestSettings.PopulateSettings(runSettingsXml, logger: null, configuration: null);
        return manager.RunTestsAsync(tests, new DeploymentContext(null, runSettingsXml), _messageLogger, _recorder, null, token);
    }

    private string[] StartedTests() => [.. _recorder.Starts.Select(start => start.Test)];

    private void RecordInfo(string format, object?[] args)
    {
        lock (_infoCalls)
        {
            _infoCalls.Add((format, args));
        }
    }

    private int InfoCallCount(string format)
    {
        lock (_infoCalls)
        {
            return _infoCalls.Count(call => call.Format == format);
        }
    }

    private int InfoCallCountStartingWith(string prefix)
    {
        lock (_infoCalls)
        {
            return _infoCalls.Count(call => call.Format.StartsWith(prefix, StringComparison.Ordinal));
        }
    }

    private string?[] InfoFirstArguments(string format)
    {
        lock (_infoCalls)
        {
            return [.. _infoCalls.Where(call => call.Format == format).Select(call => call.Args[0]?.ToString())];
        }
    }

    private sealed class HostSwitchingPlatformServiceProvider : TestablePlatformServiceProvider, IPlatformServiceProvider
    {
        public bool UseRealTestSourceHost { get; set; }

        public new ITestSourceHost CreateTestSourceHost(string source, string? settingsXml)
            => UseRealTestSourceHost ? new TestSourceHost(source, settingsXml) : MockTestSourceHost.Object;
    }

    private sealed class CountingTestClassInstanceFactory : ITestClassInstanceFactory
    {
        private int _createdInstances;

        public int CreatedInstances => Volatile.Read(ref _createdInstances);

        public ITestClassInstanceLease CreateInstance(Type testClassType, TestContext testContext)
        {
            Interlocked.Increment(ref _createdInstances);
            return new InstanceLease(Activator.CreateInstance(testClassType)!);
        }
    }

    private sealed class InstanceLease(object instance) : ITestClassInstanceLease
    {
        public object Instance { get; } = instance;

        public bool RequiresCleanup => false;

        public Task DisposeAsync() => Task.CompletedTask;
    }

    private sealed class RecordingMessageLogger : IAdapterMessageLogger
    {
        private readonly List<(MessageLevel Level, string Message)> _messages = [];

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
        }
    }

    private sealed class RecordingTestResultRecorder : ITestResultRecorder
    {
        private readonly List<string> _events = [];
        private readonly List<(string Test, bool SupportsExecutionActivityLease)> _starts = [];

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

    [FixtureTestClass]
    private sealed class ClassP
    {
        [TestMethod]
        public void Zulu()
        {
        }
    }

    [FixtureTestClass]
    private sealed class ClassQ
    {
        [TestMethod]
        public void Alpha()
        {
        }
    }

    [FixtureTestClass]
    private sealed class ClassR
    {
        [TestMethod]
        public void Third()
        {
        }
    }

    [FixtureTestClass]
    private sealed class ForceCleanupFixture
    {
        public static int CleanupCount { get; set; }

        [ClassCleanup]
        public static void Cleanup() => CleanupCount++;

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
