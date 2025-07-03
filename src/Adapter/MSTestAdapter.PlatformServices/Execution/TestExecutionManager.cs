// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Extensions;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Helpers;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Adapter;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using ExecutionScope = Microsoft.VisualStudio.TestTools.UnitTesting.ExecutionScope;

namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution;

/// <summary>
/// Class responsible for execution of tests at assembly level and sending tests via framework handle.
/// </summary>
#if NET6_0_OR_GREATER
[Obsolete(TestTools.UnitTesting.FrameworkConstants.PublicTypeObsoleteMessage, DiagnosticId = "MSTESTOBS")]
#else
[Obsolete(TestTools.UnitTesting.FrameworkConstants.PublicTypeObsoleteMessage)]
#endif
public class TestExecutionManager
{
    private sealed class RemotingMessageLogger : MarshalByRefObject, IMessageLogger
    {
        private readonly IMessageLogger _realMessageLogger;

        public RemotingMessageLogger(IMessageLogger messageLogger)
            => _realMessageLogger = messageLogger;

        public void SendMessage(TestMessageLevel testMessageLevel, string message)
            => _realMessageLogger.SendMessage(testMessageLevel, message);
    }

    /// <summary>
    /// Dictionary for test run parameters.
    /// </summary>
    private readonly IDictionary<string, object> _sessionParameters;
    private readonly IEnvironment _environment;
    private readonly Func<Func<Task>, Task> _taskFactory;

    /// <summary>
    /// Specifies whether the test run is canceled or not.
    /// </summary>
    private TestRunCancellationToken? _testRunCancellationToken;

    /// <summary>
    /// Initializes a new instance of the <see cref="TestExecutionManager"/> class.
    /// </summary>
    public TestExecutionManager()
        : this(new EnvironmentWrapper())
    {
    }

    internal TestExecutionManager(IEnvironment environment, Func<Func<Task>, Task>? taskFactory = null)
    {
        _testMethodFilter = new TestMethodFilter();
        _sessionParameters = new Dictionary<string, object>();
        _environment = environment;
        _taskFactory = taskFactory ?? DefaultFactoryAsync;
    }

    private static Task DefaultFactoryAsync(Func<Task> taskGetter)
    {
        if (MSTestSettings.RunConfigurationSettings.ExecutionApartmentState == ApartmentState.STA
            && RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            TaskCompletionSource<int> tcs = new();
            Thread entryPointThread = new(() =>
            {
                try
                {
                    // This is best we can do to execute in STA thread.
                    Task task = taskGetter();
                    task.GetAwaiter().GetResult();
                    tcs.SetResult(0);
                }
                catch (Exception ex)
                {
                    tcs.SetException(ex);
                }
            });

            entryPointThread.SetApartmentState(ApartmentState.STA);
            entryPointThread.Start();
            return tcs.Task;
        }
        else
        {
            // NOTE: If you replace this with `return taskGetter()`, you will break parallel tests.
            return Task.Run(taskGetter);
        }
    }

    /// <summary>
    /// Gets or sets method filter for filtering tests.
    /// </summary>
    private readonly TestMethodFilter _testMethodFilter;

    /// <summary>
    /// Gets or sets a value indicating whether any test executed has failed.
    /// </summary>
    private bool _hasAnyTestFailed;

    /// <summary>
    /// Runs the tests.
    /// </summary>
    /// <param name="tests">Tests to be run.</param>
    /// <param name="runContext">Context to use when executing the tests.</param>
    /// <param name="frameworkHandle">Handle to the framework to record results and to do framework operations.</param>
    /// <param name="runCancellationToken">Test run cancellation token.</param>
#if DEBUG
    [Obsolete("Use RunTestsAsync instead.")]
#endif
    public void RunTests(IEnumerable<TestCase> tests, IRunContext? runContext, IFrameworkHandle frameworkHandle, TestRunCancellationToken runCancellationToken)
    {
        DebugEx.Assert(tests != null, "tests");
        DebugEx.Assert(runContext != null, "runContext");
        DebugEx.Assert(frameworkHandle != null, "frameworkHandle");
        DebugEx.Assert(runCancellationToken != null, "runCancellationToken");

        _testRunCancellationToken = runCancellationToken;
        PlatformServiceProvider.Instance.TestRunCancellationToken = _testRunCancellationToken;

        bool isDeploymentDone = PlatformServiceProvider.Instance.TestDeployment.Deploy(tests, runContext, frameworkHandle);

        // Placing this after deployment since we need information post deployment that we pass in as properties.
        CacheSessionParameters(runContext, frameworkHandle);

        // Execute the tests
        ExecuteTestsAsync(tests, runContext, frameworkHandle, isDeploymentDone).GetAwaiter().GetResult();

        if (!_hasAnyTestFailed)
        {
            PlatformServiceProvider.Instance.TestDeployment.Cleanup();
        }
    }

    /// <summary>
    /// Runs the tests.
    /// </summary>
    /// <param name="tests">Tests to be run.</param>
    /// <param name="runContext">Context to use when executing the tests.</param>
    /// <param name="frameworkHandle">Handle to the framework to record results and to do framework operations.</param>
    /// <param name="runCancellationToken">Test run cancellation token.</param>
    internal async Task RunTestsAsync(IEnumerable<TestCase> tests, IRunContext? runContext, IFrameworkHandle frameworkHandle, TestRunCancellationToken runCancellationToken)
    {
        DebugEx.Assert(tests != null, "tests");
        DebugEx.Assert(runContext != null, "runContext");
        DebugEx.Assert(frameworkHandle != null, "frameworkHandle");
        DebugEx.Assert(runCancellationToken != null, "runCancellationToken");

        _testRunCancellationToken = runCancellationToken;
        PlatformServiceProvider.Instance.TestRunCancellationToken = _testRunCancellationToken;

        bool isDeploymentDone = PlatformServiceProvider.Instance.TestDeployment.Deploy(tests, runContext, frameworkHandle);

        // Placing this after deployment since we need information post deployment that we pass in as properties.
        CacheSessionParameters(runContext, frameworkHandle);

        // Execute the tests
        await ExecuteTestsAsync(tests, runContext, frameworkHandle, isDeploymentDone).ConfigureAwait(false);

        if (!_hasAnyTestFailed)
        {
            PlatformServiceProvider.Instance.TestDeployment.Cleanup();
        }
    }

    /// <summary>
    /// Runs the tests.
    /// </summary>
    /// <param name="sources">Tests to be run.</param>
    /// <param name="runContext">Context to use when executing the tests.</param>
    /// <param name="frameworkHandle">Handle to the framework to record results and to do framework operations.</param>
    /// <param name="cancellationToken">Test run cancellation token.</param>
#if DEBUG
    [Obsolete("Use RunTestsAsync instead.")]
#endif
    public void RunTests(IEnumerable<string> sources, IRunContext? runContext, IFrameworkHandle frameworkHandle, TestRunCancellationToken cancellationToken)
    {
        _testRunCancellationToken = cancellationToken;
        PlatformServiceProvider.Instance.TestRunCancellationToken = _testRunCancellationToken;

        var discoverySink = new TestCaseDiscoverySink();

        var tests = new List<TestCase>();

        // deploy everything first.
        foreach (string source in sources)
        {
            _testRunCancellationToken?.ThrowIfCancellationRequested();

            var logger = (IMessageLogger)frameworkHandle;

            // discover the tests
            GetUnitTestDiscoverer().DiscoverTestsInSource(source, logger, discoverySink, runContext);
            tests.AddRange(discoverySink.Tests);

            // Clear discoverSinksTests so that it just stores test for one source at one point of time
            discoverySink.Tests.Clear();
        }

        bool isDeploymentDone = PlatformServiceProvider.Instance.TestDeployment.Deploy(tests, runContext, frameworkHandle);

        // Placing this after deployment since we need information post deployment that we pass in as properties.
        CacheSessionParameters(runContext, frameworkHandle);

        // Run tests.
        ExecuteTestsAsync(tests, runContext, frameworkHandle, isDeploymentDone).GetAwaiter().GetResult();

        if (!_hasAnyTestFailed)
        {
            PlatformServiceProvider.Instance.TestDeployment.Cleanup();
        }
    }

    internal async Task RunTestsAsync(IEnumerable<string> sources, IRunContext? runContext, IFrameworkHandle frameworkHandle, TestRunCancellationToken cancellationToken)
    {
        _testRunCancellationToken = cancellationToken;
        PlatformServiceProvider.Instance.TestRunCancellationToken = _testRunCancellationToken;

        var discoverySink = new TestCaseDiscoverySink();

        var tests = new List<TestCase>();

        // deploy everything first.
        foreach (string source in sources)
        {
            _testRunCancellationToken?.ThrowIfCancellationRequested();

            var logger = (IMessageLogger)frameworkHandle;

            // discover the tests
            GetUnitTestDiscoverer().DiscoverTestsInSource(source, logger, discoverySink, runContext);
            tests.AddRange(discoverySink.Tests);

            // Clear discoverSinksTests so that it just stores test for one source at one point of time
            discoverySink.Tests.Clear();
        }

        bool isDeploymentDone = PlatformServiceProvider.Instance.TestDeployment.Deploy(tests, runContext, frameworkHandle);

        // Placing this after deployment since we need information post deployment that we pass in as properties.
        CacheSessionParameters(runContext, frameworkHandle);

        // Run tests.
        await ExecuteTestsAsync(tests, runContext, frameworkHandle, isDeploymentDone).ConfigureAwait(false);

        if (!_hasAnyTestFailed)
        {
            PlatformServiceProvider.Instance.TestDeployment.Cleanup();
        }
    }

    /// <summary>
    /// Execute the parameter tests.
    /// </summary>
    /// <param name="tests">Tests to execute.</param>
    /// <param name="runContext">The run context.</param>
    /// <param name="frameworkHandle">Handle to record test start/end/results.</param>
    /// <param name="isDeploymentDone">Indicates if deployment is done.</param>
    internal virtual async Task ExecuteTestsAsync(IEnumerable<TestCase> tests, IRunContext? runContext, IFrameworkHandle frameworkHandle, bool isDeploymentDone)
    {
        var testsBySource = from test in tests
                            group test by test.Source into testGroup
                            select new { Source = testGroup.Key, Tests = testGroup };

        foreach (var group in testsBySource)
        {
            _testRunCancellationToken?.ThrowIfCancellationRequested();
            await ExecuteTestsInSourceAsync(group.Tests, runContext, frameworkHandle, group.Source, isDeploymentDone).ConfigureAwait(false);
        }
    }

    internal virtual UnitTestDiscoverer GetUnitTestDiscoverer() => new();

    internal void SendTestResults(TestCase test, TestTools.UnitTesting.TestResult[] unitTestResults, DateTimeOffset startTime, DateTimeOffset endTime,
        ITestExecutionRecorder testExecutionRecorder)
    {
        foreach (TestTools.UnitTesting.TestResult unitTestResult in unitTestResults)
        {
            _testRunCancellationToken?.ThrowIfCancellationRequested();

            if (test == null)
            {
                continue;
            }

            var testResult = unitTestResult.ToTestResult(test, startTime, endTime, _environment.MachineName, MSTestSettings.CurrentSettings);

            testExecutionRecorder.RecordEnd(test, testResult.Outcome);

            if (testResult.Outcome == TestOutcome.Failed)
            {
                PlatformServiceProvider.Instance.AdapterTraceLogger.LogInfo("MSTestExecutor:Test {0} failed. ErrorMessage:{1}, ErrorStackTrace:{2}.", testResult.TestCase.FullyQualifiedName, testResult.ErrorMessage, testResult.ErrorStackTrace);
                _hasAnyTestFailed = true;
            }

            try
            {
                if (testResult.Outcome != TestOutcome.NotFound
                    || !RuntimeContext.IsHotReloadEnabled)
                {
                    testExecutionRecorder.RecordResult(testResult);
                }
            }
            catch (TestCanceledException)
            {
                // Ignore this exception
            }
        }
    }

    private static bool MatchTestFilter(ITestCaseFilterExpression? filterExpression, TestCase test, TestMethodFilter testMethodFilter)
    {
        if (filterExpression != null
            && !filterExpression.MatchTestCase(test, p => testMethodFilter.PropertyValueProvider(test, p)))
        {
            // If this is a fixture test, return true. Fixture tests are not filtered out and are always available for the status.
            if (test.Traits.Any(t => t.Name == EngineConstants.FixturesTestTrait))
            {
                return true;
            }

            // Skip test if not fitting filter criteria.
            return false;
        }

        return true;
    }

    /// <summary>
    /// Execute the parameter tests present in parameter source.
    /// </summary>
    /// <param name="tests">Tests to execute.</param>
    /// <param name="runContext">The run context.</param>
    /// <param name="frameworkHandle">Handle to record test start/end/results.</param>
    /// <param name="source">The test container for the tests.</param>
    /// <param name="isDeploymentDone">Indicates if deployment is done.</param>
    private async Task ExecuteTestsInSourceAsync(IEnumerable<TestCase> tests, IRunContext? runContext, IFrameworkHandle frameworkHandle, string source, bool isDeploymentDone)
    {
        DebugEx.Assert(!StringEx.IsNullOrEmpty(source), "Source cannot be empty");

        if (isDeploymentDone)
        {
            source = Path.Combine(PlatformServiceProvider.Instance.TestDeployment.GetDeploymentDirectory()!, Path.GetFileName(source));
        }

        using MSTestAdapter.PlatformServices.Interface.ITestSourceHost isolationHost = PlatformServiceProvider.Instance.CreateTestSourceHost(source, runContext?.RunSettings, frameworkHandle);
        bool usesAppDomains = isolationHost is TestSourceHost { UsesAppDomain: true };
        PlatformServiceProvider.Instance.AdapterTraceLogger.LogInfo("Created unit-test runner {0}", source);

        // Default test set is filtered tests based on user provided filter criteria
        ITestCaseFilterExpression? filterExpression = _testMethodFilter.GetFilterExpression(runContext, frameworkHandle, out bool filterHasError);
        if (filterHasError)
        {
            // Bail out without processing everything else below.
            return;
        }

        // this is done so that appropriate values of test context properties are set at source level
        // and are merged with session level parameters
        IDictionary<string, object> sourceLevelParameters = PlatformServiceProvider.Instance.SettingsProvider.GetProperties(source);

        if (_sessionParameters is { Count: > 0 })
        {
            sourceLevelParameters = _sessionParameters.ConcatWithOverwrites(sourceLevelParameters);
        }

        _testRunCancellationToken?.ThrowIfCancellationRequested();

        TestAssemblySettingsProvider? sourceSettingsProvider = null;

        try
        {
            sourceSettingsProvider = isolationHost.CreateInstanceForType(
                typeof(TestAssemblySettingsProvider),
                null) as TestAssemblySettingsProvider;
        }
        catch (Exception ex)
        {
            PlatformServiceProvider.Instance.AdapterTraceLogger.LogInfo("Could not create TestAssemblySettingsProvider instance in child app-domain", ex);
        }

        TestAssemblySettings sourceSettings = (sourceSettingsProvider != null)
            ? sourceSettingsProvider.GetSettings(source)
            : new TestAssemblySettings();

        int parallelWorkers = sourceSettings.Workers;
        ExecutionScope parallelScope = sourceSettings.Scope;
        TestCase[] testsToRun = [.. tests.Where(t => MatchTestFilter(filterExpression, t, _testMethodFilter))];
        UnitTestElement[] unitTestElements = [.. testsToRun.Select(e => e.ToUnitTestElement(source))];
        // Create an instance of a type defined in adapter so that adapter gets loaded in the child app domain
        var testRunner = (UnitTestRunner)isolationHost.CreateInstanceForType(
            typeof(UnitTestRunner),
            [MSTestSettings.CurrentSettings, unitTestElements, (int)sourceSettings.ClassCleanupLifecycle])!;

        // Ensures that the cancellation token gets through AppDomain boundary.
        _testRunCancellationToken?.Register(static state => ((UnitTestRunner)state!).Cancel(), testRunner);

        if (MSTestSettings.CurrentSettings.ParallelizationWorkers.HasValue)
        {
            // The runsettings value takes precedence over an assembly level setting. Reset the level.
            parallelWorkers = MSTestSettings.CurrentSettings.ParallelizationWorkers.Value;
        }

        if (MSTestSettings.CurrentSettings.ParallelizationScope.HasValue)
        {
            // The runsettings value takes precedence over an assembly level setting. Reset the level.
            parallelScope = MSTestSettings.CurrentSettings.ParallelizationScope.Value;
        }

        if (!MSTestSettings.CurrentSettings.DisableParallelization && sourceSettings.CanParallelizeAssembly && parallelWorkers > 0)
        {
            // Parallelization is enabled. Let's do further classification for sets.
            frameworkHandle.SendMessage(
                TestMessageLevel.Informational,
                string.Format(CultureInfo.CurrentCulture, Resource.TestParallelizationBanner, source, parallelWorkers, parallelScope));

            // Create test sets for execution, we can execute them in parallel based on parallel settings
            // Parallel and not parallel sets.
            IEnumerable<IGrouping<bool, TestCase>> testSets = testsToRun.GroupBy(t => t.GetPropertyValue(EngineConstants.DoNotParallelizeProperty, false));

            IGrouping<bool, TestCase>? parallelizableTestSet = testSets.FirstOrDefault(g => !g.Key);
            IGrouping<bool, TestCase>? nonParallelizableTestSet = testSets.FirstOrDefault(g => g.Key);

            if (parallelizableTestSet != null)
            {
                ConcurrentQueue<IEnumerable<TestCase>>? queue = null;

                // Chunk the sets into further groups based on parallel level
                switch (parallelScope)
                {
                    case ExecutionScope.MethodLevel:
                        queue = new ConcurrentQueue<IEnumerable<TestCase>>(parallelizableTestSet.Select(t => new[] { t }));
                        break;

                    case ExecutionScope.ClassLevel:
                        queue = new ConcurrentQueue<IEnumerable<TestCase>>(parallelizableTestSet.GroupBy(t => t.GetPropertyValue(EngineConstants.TestClassNameProperty) as string));
                        break;
                }

                var tasks = new List<Task>();

                for (int i = 0; i < parallelWorkers; i++)
                {
                    _testRunCancellationToken?.ThrowIfCancellationRequested();

                    tasks.Add(_taskFactory(async () =>
                    {
                        try
                        {
                            while (!queue!.IsEmpty)
                            {
                                _testRunCancellationToken?.ThrowIfCancellationRequested();

                                if (queue.TryDequeue(out IEnumerable<TestCase>? testSet))
                                {
                                    await ExecuteTestsWithTestRunnerAsync(testSet, frameworkHandle, source, sourceLevelParameters, testRunner, usesAppDomains).ConfigureAwait(false);
                                }
                            }
                        }
                        catch (OperationCanceledException) when (_testRunCancellationToken?.Canceled == true)
                        {
                        }
                    }));
                }

                try
                {
                    await Task.WhenAll(tasks).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    string exceptionToString = ex.ToString();
                    PlatformServiceProvider.Instance.AdapterTraceLogger.LogError("Error occurred while executing tests in parallel{0}{1}", Environment.NewLine, exceptionToString);
                    frameworkHandle.SendMessage(TestMessageLevel.Error, exceptionToString);
                    throw;
                }
            }

            // Queue the non parallel set
            if (nonParallelizableTestSet != null)
            {
                await ExecuteTestsWithTestRunnerAsync(nonParallelizableTestSet, frameworkHandle, source, sourceLevelParameters, testRunner, usesAppDomains).ConfigureAwait(false);
            }
        }
        else
        {
            await ExecuteTestsWithTestRunnerAsync(testsToRun, frameworkHandle, source, sourceLevelParameters, testRunner, usesAppDomains).ConfigureAwait(false);
        }

        if (PlatformServiceProvider.Instance.IsGracefulStopRequested)
        {
            testRunner.ForceCleanup(sourceLevelParameters!, new RemotingMessageLogger(frameworkHandle));
        }

        PlatformServiceProvider.Instance.AdapterTraceLogger.LogInfo("Executed tests belonging to source {0}", source);
    }

    private async Task ExecuteTestsWithTestRunnerAsync(
        IEnumerable<TestCase> tests,
        ITestExecutionRecorder testExecutionRecorder,
        string source,
        IDictionary<string, object> sourceLevelParameters,
        UnitTestRunner testRunner,
        bool usesAppDomains)
    {
        bool hasAnyRunnableTests = false;
        var fixtureTests = new List<TestCase>();

        IEnumerable<TestCase> orderedTests = MSTestSettings.CurrentSettings.OrderTestsByNameInClass
            ? tests.OrderBy(t => t.GetManagedType()).ThenBy(t => t.GetManagedMethod())
            : tests;

        // If testRunner is in a different AppDomain, we cannot pass the testExecutionRecorder directly.
        // Instead, we pass a proxy (remoting object) that is marshallable by ref.
        IMessageLogger remotingMessageLogger = usesAppDomains
            ? new RemotingMessageLogger(testExecutionRecorder)
            : testExecutionRecorder;

        foreach (TestCase currentTest in orderedTests)
        {
            _testRunCancellationToken?.ThrowIfCancellationRequested();
            if (PlatformServiceProvider.Instance.IsGracefulStopRequested)
            {
                break;
            }

            // If it is a fixture test, add it to the list of fixture tests and do not execute it.
            // It is executed by test itself.
            if (currentTest.Traits.Any(t => t.Name == EngineConstants.FixturesTestTrait))
            {
                fixtureTests.Add(currentTest);
                continue;
            }

            hasAnyRunnableTests = true;
            var unitTestElement = currentTest.ToUnitTestElement(source);

            testExecutionRecorder.RecordStart(currentTest);

            DateTimeOffset startTime = DateTimeOffset.Now;

            PlatformServiceProvider.Instance.AdapterTraceLogger.LogInfo("Executing test {0}", unitTestElement.TestMethod.Name);

            // Run single test passing test context properties to it.
            IDictionary<TestProperty, object?> tcmProperties = TcmTestPropertiesProvider.GetTcmProperties(currentTest);
            Dictionary<string, object?> testContextProperties = GetTestContextProperties(tcmProperties, sourceLevelParameters);

            TestTools.UnitTesting.TestResult[] unitTestResult;
            if (usesAppDomains || Thread.CurrentThread.GetApartmentState() == ApartmentState.STA)
            {
#pragma warning disable VSTHRD103 // Call async methods when in an async method - We cannot do right now because we are crossing app domains.
                // TODO: When app domains support is dropped, we can finally always be calling the async version.
                // In addition to app domains, if we are STA thread (e.g, because runsettings setting ExecutionApartmentState to STA), we want to preserve that.
                // If we await, we could end up in a thread pool thread, which is not what we want.
                // Alternatively, if we want to use RunSingleTestAsync for the case of STA, we should have:
                // 1. A custom single threaded synchronization context that keeps us in STA.
                // 2. Use ConfigureAwait(true).
                unitTestResult = testRunner.RunSingleTest(unitTestElement.TestMethod, testContextProperties, remotingMessageLogger);
#pragma warning restore VSTHRD103 // Call async methods when in an async method
            }
            else
            {
                unitTestResult = await testRunner.RunSingleTestAsync(unitTestElement.TestMethod, testContextProperties, remotingMessageLogger).ConfigureAwait(false);
            }

            PlatformServiceProvider.Instance.AdapterTraceLogger.LogInfo("Executed test {0}", unitTestElement.TestMethod.Name);

            DateTimeOffset endTime = DateTimeOffset.Now;

            SendTestResults(currentTest, unitTestResult, startTime, endTime, testExecutionRecorder);
        }

        // Once all tests have been executed, update the status of fixture tests.
        foreach (TestCase currentTest in fixtureTests)
        {
            _testRunCancellationToken?.ThrowIfCancellationRequested();

            testExecutionRecorder.RecordStart(currentTest);

            // If there were only fixture tests, send an inconclusive result.
            if (!hasAnyRunnableTests)
            {
                var result = new TestTools.UnitTesting.TestResult
                {
                    Outcome = TestTools.UnitTesting.UnitTestOutcome.Inconclusive,
                };

                SendTestResults(currentTest, [result], DateTimeOffset.Now, DateTimeOffset.Now, testExecutionRecorder);
                continue;
            }

            Trait trait = currentTest.Traits.First(t => t.Name == EngineConstants.FixturesTestTrait);
            var unitTestElement = currentTest.ToUnitTestElement(source);
            FixtureTestResult fixtureTestResult = testRunner.GetFixtureTestResult(unitTestElement.TestMethod, trait.Value);

            if (fixtureTestResult.IsExecuted)
            {
                var result = new TestTools.UnitTesting.TestResult
                {
                    Outcome = fixtureTestResult.Outcome,
                };
                SendTestResults(currentTest, [result], DateTimeOffset.Now, DateTimeOffset.Now, testExecutionRecorder);
            }
        }
    }

    /// <summary>
    /// Get test context properties.
    /// </summary>
    /// <param name="tcmProperties">Tcm properties.</param>
    /// <param name="sourceLevelParameters">Source level parameters.</param>
    /// <returns>Test context properties.</returns>
    private static Dictionary<string, object?> GetTestContextProperties(
        IDictionary<TestProperty, object?> tcmProperties,
        IDictionary<string, object> sourceLevelParameters)
    {
        // This dictionary will have *at least* 8 entries. Those are the sourceLevelParameters
        // which were originally calculated from TestDeployment.GetDeploymentInformation.
        var testContextProperties = new Dictionary<string, object?>(capacity: 8);

        // Add tcm properties.
        foreach ((TestProperty key, object? value) in tcmProperties)
        {
            testContextProperties[key.Id] = value;
        }

        // Add source level parameters.
        foreach ((string key, object value) in sourceLevelParameters)
        {
            testContextProperties[key] = value;
        }

        return testContextProperties;
    }

    [SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes", Justification = "Requirement is to handle errors in user specified run parameters")]
    private void CacheSessionParameters(IRunContext? runContext, ITestExecutionRecorder testExecutionRecorder)
    {
        if (StringEx.IsNullOrEmpty(runContext?.RunSettings?.SettingsXml))
        {
            return;
        }

        try
        {
            Dictionary<string, object>? testRunParameters = RunSettingsUtilities.GetTestRunParameters(runContext.RunSettings.SettingsXml);
            if (testRunParameters != null)
            {
                // Clear sessionParameters to prevent key collisions of test run parameters in case
                // "Keep Test Execution Engine Alive" is selected in VS.
                _sessionParameters.Clear();
                foreach (KeyValuePair<string, object> kvp in testRunParameters)
                {
                    _sessionParameters.Add(kvp);
                }
            }
        }
        catch (Exception ex)
        {
            testExecutionRecorder.SendMessage(TestMessageLevel.Error, ex.Message);
        }
    }
}
