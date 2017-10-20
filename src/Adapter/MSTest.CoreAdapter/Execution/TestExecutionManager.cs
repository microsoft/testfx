// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Diagnostics.CodeAnalysis;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Extensions;
    using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Helpers;
    using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;
    using Microsoft.VisualStudio.TestPlatform.ObjectModel;
    using Microsoft.VisualStudio.TestPlatform.ObjectModel.Adapter;
    using Microsoft.VisualStudio.TestPlatform.ObjectModel.Logging;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// Class responsible for execution of tests at assembly level and sending tests via framework handle
    /// </summary>
    public class TestExecutionManager
    {
        /// <summary>
        /// Specifies whether the test run is canceled or not
        /// </summary>
        private TestRunCancellationToken cancellationToken;

        /// <summary>
        /// Dictionary for test run parameters
        /// </summary>
        private IDictionary<string, object> sessionParameters;

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Performance", "CA1810:InitializeReferenceTypeStaticFieldsInline", Justification = "Need to over-write the keys in dictionary.")]
        public TestExecutionManager()
        {
            this.TestMethodFilter = new TestMethodFilter();
            this.sessionParameters = new Dictionary<string, object>();
        }

        /// <summary>
        /// Gets or sets method filter for filtering tests
        /// </summary>
        private TestMethodFilter TestMethodFilter { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether any test executed has failed.
        /// </summary>
        private bool HasAnyTestFailed { get; set; }

        /// <summary>
        /// Runs the tests.
        /// </summary>
        /// <param name="tests">Tests to be run.</param>
        /// <param name="runContext">Context to use when executing the tests.</param>
        /// <param name="frameworkHandle">Handle to the framework to record results and to do framework operations.</param>
        /// <param name="runCancellationToken">Test run cancellation tokenn</param>
        public void RunTests(IEnumerable<TestCase> tests, IRunContext runContext, IFrameworkHandle frameworkHandle, TestRunCancellationToken runCancellationToken)
        {
            Debug.Assert(tests != null, "tests");
            Debug.Assert(runContext != null, "runContext");
            Debug.Assert(frameworkHandle != null, "frameworkHandle");
            Debug.Assert(runCancellationToken != null, "runCancellationToken");

            this.cancellationToken = runCancellationToken;

            var isDeploymentDone = PlatformServiceProvider.Instance.TestDeployment.Deploy(tests, runContext, frameworkHandle);

            // Placing this after deployment since we need information post deployment that we pass in as properties.
            this.CacheSessionParameters(runContext, frameworkHandle);

            // Execute the tests
            this.ExecuteTests(tests, runContext, frameworkHandle, isDeploymentDone);

            if (!this.HasAnyTestFailed)
            {
                PlatformServiceProvider.Instance.TestDeployment.Cleanup();
            }
        }

        public void RunTests(IEnumerable<string> sources, IRunContext runContext, IFrameworkHandle frameworkHandle, TestRunCancellationToken cancellationToken)
        {
            this.cancellationToken = cancellationToken;

            var discoverySink = new TestCaseDiscoverySink();

            var tests = new List<TestCase>();

            // deploy everything first.
            foreach (var source in sources)
            {
                if (this.cancellationToken.Canceled)
                {
                    break;
                }

                var logger = (IMessageLogger)frameworkHandle;

                // discover the tests
                this.GetUnitTestDiscoverer().DiscoverTestsInSource(source, logger, discoverySink, runContext);
                tests.AddRange(discoverySink.Tests);

                // Clear discoverSinksTests so that it just stores test for one source at one point of time
                discoverySink.Tests.Clear();
            }

            bool isDeploymentDone = PlatformServiceProvider.Instance.TestDeployment.Deploy(tests, runContext, frameworkHandle);

            // Placing this after deployment since we need information post deployment that we pass in as properties.
            this.CacheSessionParameters(runContext, frameworkHandle);

            // Run tests.
            this.ExecuteTests(tests, runContext, frameworkHandle, isDeploymentDone);

            if (!this.HasAnyTestFailed)
            {
                PlatformServiceProvider.Instance.TestDeployment.Cleanup();
            }
        }

        /// <summary>
        /// Execute the parameter tests
        /// </summary>
        /// <param name="tests">Tests to execute.</param>
        /// <param name="runContext">The run context.</param>
        /// <param name="frameworkHandle">Handle to record test start/end/results.</param>
        /// <param name="isDeploymentDone">Indicates if deployment is done.</param>
        internal virtual void ExecuteTests(IEnumerable<TestCase> tests, IRunContext runContext, IFrameworkHandle frameworkHandle, bool isDeploymentDone)
        {
            var testsBySource = from test in tests
                                group test by test.Source into testGroup
                                select new { Source = testGroup.Key, Tests = testGroup };

            foreach (var group in testsBySource)
            {
                this.ExecuteTestsInSource(group.Tests, runContext, frameworkHandle, group.Source, isDeploymentDone);
            }
        }

        internal virtual UnitTestDiscoverer GetUnitTestDiscoverer()
        {
            return new UnitTestDiscoverer();
        }

        internal void SendTestResults(TestCase test, UnitTestResult[] unitTestResults, DateTimeOffset startTime, DateTimeOffset endTime, ITestExecutionRecorder testExecutionRecorder)
        {
            if (!(unitTestResults?.Length > 0))
            {
                return;
            }

            foreach (var unitTestResult in unitTestResults)
            {
                if (test == null)
                {
                    continue;
                }

                var testResult = unitTestResult.ToTestResult(test, startTime, endTime, MSTestSettings.CurrentSettings.MapInconclusiveToFailed);

                if (unitTestResult.DatarowIndex >= 0)
                {
                    testResult.DisplayName = string.Format(CultureInfo.CurrentCulture, Resource.DataDrivenResultDisplayName, test.DisplayName, unitTestResult.DatarowIndex);
                }

                testExecutionRecorder.RecordEnd(test, testResult.Outcome);

                if (testResult.Outcome == TestOutcome.Failed)
                {
                    PlatformServiceProvider.Instance.AdapterTraceLogger.LogInfo("MSTestExecutor:Test {0} failed. ErrorMessage:{1}, ErrorStackTrace:{2}.", testResult.TestCase.FullyQualifiedName, testResult.ErrorMessage, testResult.ErrorStackTrace);
                    this.HasAnyTestFailed = true;
                }

                try
                {
                    testExecutionRecorder.RecordResult(testResult);
                }
                catch (TestCanceledException)
                {
                    // Ignore this exception
                }
            }
        }

        private static bool MatchTestFilter(ITestCaseFilterExpression filterExpression, TestCase test, TestMethodFilter testMethodFilter)
        {
            if (filterExpression != null && filterExpression.MatchTestCase(test, p => testMethodFilter.PropertyValueProvider(test, p)) == false)
            {
                // Skip test if not fitting filter criteria.
                return false;
            }

            return true;
        }

        /// <summary>
        /// Execute the parameter tests present in parameter source
        /// </summary>
        /// <param name="tests">Tests to execute.</param>
        /// <param name="runContext">The run context.</param>
        /// <param name="frameworkHandle">Handle to record test start/end/results.</param>
        /// <param name="source">The test container for the tests.</param>
        /// <param name="isDeploymentDone">Indicates if deployment is done.</param>
        private void ExecuteTestsInSource(IEnumerable<TestCase> tests, IRunContext runContext, IFrameworkHandle frameworkHandle, string source, bool isDeploymentDone)
        {
            Debug.Assert(!string.IsNullOrEmpty(source), "Source cannot be empty");

            if (isDeploymentDone)
            {
                source = Path.Combine(PlatformServiceProvider.Instance.TestDeployment.GetDeploymentDirectory(), Path.GetFileName(source));
            }

            using (var isolationHost = PlatformServiceProvider.Instance.CreateTestSourceHost(source, runContext?.RunSettings, frameworkHandle))
            {
                var testRunner = isolationHost.CreateInstanceForType(
                    typeof(UnitTestRunner),
                    new object[] { MSTestSettings.CurrentSettings }) as UnitTestRunner;
                PlatformServiceProvider.Instance.AdapterTraceLogger.LogInfo("Created unit-test runner {0}", source);

                // Default test set is filtered based on user provided filter criteria
                IEnumerable<TestCase> testsToRun = Enumerable.Empty<TestCase>();
                var filterExpression = this.TestMethodFilter.GetFilterExpression(runContext, frameworkHandle, out var filterHasError);
                if (!filterHasError)
                {
                    testsToRun = tests.Where(t => MatchTestFilter(filterExpression, t, this.TestMethodFilter));
                }

                // Create test sets for execution, we can execute them in parallel based on parallel settings
                IEnumerable<IGrouping<bool, TestCase>> testsets = Enumerable.Empty<IGrouping<bool, TestCase>>();
                var parallelLevel = PlatformServiceProvider.Instance.TestSource.GetParallelizationLevel(source);
                if (parallelLevel <= 0)
                {
                    this.ExecuteTestsWithTestRunner(testsToRun, runContext, frameworkHandle, source, testRunner);
                }
                else
                {
                    // Parallelization is enabled. Let's do further classification for sets.
                    var parallelMode = PlatformServiceProvider.Instance.TestSource.GetParallelizationMode(source);
                    var logger = (IMessageLogger)frameworkHandle;
                    logger.SendMessage(TestMessageLevel.Informational, $"MSTestExecutor: Parallel Configuration. Level = {parallelLevel}, Mode = {parallelMode}");

                    // Parallel and not parallel sets.
                    testsets = testsToRun.GroupBy(t => t.GetPropertyValue<bool>(TestAdapter.Constants.DoNotParallelizeProperty, false));

                    // Chunk the sets into further groups based on parallel level
                    ConcurrentQueue<IEnumerable<TestCase>> queue = null;
                    var parallelizableTestSet = testsets.FirstOrDefault(g => g.Key == false);
                    var nonparallelizableTestSet = testsets.FirstOrDefault(g => g.Key == true);

                    if (parallelizableTestSet != null)
                    {
                        switch (parallelMode)
                        {
                            case TestParallelizationMode.MethodLevel:
                                queue = new ConcurrentQueue<IEnumerable<TestCase>>(parallelizableTestSet.Select(t => new[] { t }).AsEnumerable<IEnumerable<TestCase>>());
                                break;
                            case TestParallelizationMode.ClassLevel:
                                queue = new ConcurrentQueue<IEnumerable<TestCase>>(parallelizableTestSet.GroupBy(t => t.GetPropertyValue(TestAdapter.Constants.TestClassNameProperty) as string));
                                break;
                        }

                        var tasks = new List<Task>();
                        for (int i = 0; i < parallelLevel; i++)
                        {
                            tasks.Add(Task.Factory.StartNew(
                                () =>
                                    {
                                        while (!queue.IsEmpty)
                                        {
                                            if (queue.TryDequeue(out IEnumerable<TestCase> testSet))
                                            {
                                                this.ExecuteTestsWithTestRunner(testSet, runContext, frameworkHandle, source, testRunner);
                                            }
                                        }
                                    },
                            CancellationToken.None,
                            TaskCreationOptions.LongRunning,
                            TaskScheduler.Default));
                        }

                        Task.WaitAll(tasks.ToArray());
                    }

                    // Queue the non parallel set
                    if (nonparallelizableTestSet != null)
                    {
                        this.ExecuteTestsWithTestRunner(nonparallelizableTestSet, runContext, frameworkHandle, source, testRunner);
                    }
                }

                this.RunCleanup(frameworkHandle, testRunner);
                PlatformServiceProvider.Instance.AdapterTraceLogger.LogInfo(
                    "Executed tests belonging to source {0}",
                    source);
            }
        }

        private void ExecuteTestsWithTestRunner(
            IEnumerable<TestCase> tests,
            IRunContext runContext,
            ITestExecutionRecorder testExecutionRecorder,
            string source,
            UnitTestRunner testRunner)
        {
            foreach (var currentTest in tests)
            {
                if (this.cancellationToken != null && this.cancellationToken.Canceled)
                {
                    break;
                }

                var unitTestElement = currentTest.ToUnitTestElement(source);
                testExecutionRecorder.RecordStart(currentTest);

                var startTime = DateTimeOffset.Now;

                PlatformServiceProvider.Instance.AdapterTraceLogger.LogInfo(
                    "Executing test {0}",
                    unitTestElement.TestMethod.Name);

                // this is done so that appropriate values of testcontext properties are set at source level
                // and are merged with session level parameters
                var sourceLevelParameters = PlatformServiceProvider.Instance.SettingsProvider.GetProperties(source);

                if (this.sessionParameters != null && this.sessionParameters.Count > 0)
                {
                    sourceLevelParameters = sourceLevelParameters.Concat(this.sessionParameters).ToDictionary(x => x.Key, x => x.Value);
                }

                var unitTestResult = testRunner.RunSingleTest(unitTestElement.TestMethod, sourceLevelParameters);

                PlatformServiceProvider.Instance.AdapterTraceLogger.LogInfo(
                    "Executed test {0}",
                    unitTestElement.TestMethod.Name);

                var endTime = DateTimeOffset.Now;

                this.SendTestResults(currentTest, unitTestResult, startTime, endTime, testExecutionRecorder);
            }
        }

        private void RunCleanup(
            ITestExecutionRecorder testExecutionRecorder,
            UnitTestRunner testRunner)
        {
            // All cleanups (Class and Assembly) run at the end of test execution. Failures in these cleanup
            // methods will be reported as Warnings.
            PlatformServiceProvider.Instance.AdapterTraceLogger.LogInfo("Executing cleanup methods.");
            var cleanupResult = testRunner.RunCleanup();
            PlatformServiceProvider.Instance.AdapterTraceLogger.LogInfo("Executed cleanup methods.");
            if (cleanupResult != null)
            {
                IList<string> warnings = cleanupResult.Warnings;

                // Do not attach the standard output and error messages to any test result. It is not
                // guaranteed that a test method of same class would have run last. We will end up
                // adding stdout to test method of another class.
                this.LogWarnings(testExecutionRecorder, warnings);
            }
        }

        [SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes", Justification = "Requirement is to handle errors in user specified run parameters")]
        private void CacheSessionParameters(IRunContext runContext, ITestExecutionRecorder testExecutionRecorder)
        {
            if (!string.IsNullOrEmpty(runContext?.RunSettings?.SettingsXml))
            {
                try
                {
                    var testRunParameters = RunSettingsUtilities.GetTestRunParameters(runContext.RunSettings.SettingsXml);
                    if (testRunParameters != null)
                    {
                        foreach (var kvp in testRunParameters)
                        {
                            this.sessionParameters.Add(kvp);
                        }
                    }
                }
                catch (Exception ex)
                {
                    testExecutionRecorder.SendMessage(TestMessageLevel.Error, ex.Message);
                }
            }
        }

        /// <summary>
        /// Log the parameter warnings on the parameter logger
        /// </summary>
        /// <param name="testExecutionRecorder">Handle to record test start/end/results/messages.</param>
        /// <param name="warnings">Any warnings during run operation.</param>
        private void LogWarnings(ITestExecutionRecorder testExecutionRecorder, IEnumerable<string> warnings)
        {
            if (warnings == null)
            {
                return;
            }

            Debug.Assert(testExecutionRecorder != null, "Logger should not be null");

            // log the warnings
            foreach (string warning in warnings)
            {
                testExecutionRecorder.SendMessage(TestMessageLevel.Warning, warning);
            }
        }
    }
}
