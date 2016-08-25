// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    
    using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Extensions;
    using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;
    using Microsoft.VisualStudio.TestPlatform.ObjectModel;
    using Microsoft.VisualStudio.TestPlatform.ObjectModel.Adapter;
    using Microsoft.VisualStudio.TestPlatform.ObjectModel.Logging;
    using Microsoft.VisualStudio.TestPlatform.ObjectModel.Utilities;
    
    /// <summary>
    /// Class responsible for execution of tests at assembly level and sending tests via framework handle
    /// </summary>
    public class TestExecutionManager
    {
        /// <summary>
        /// MSTestAdapter Settings
        /// </summary>
        private MSTestSettings adapterSettings;

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
        }

        /// <summary>
        /// Used for filtering tests
        /// </summary>
        private TestMethodFilter TestMethodFilter { get; set; }

        /// <summary>
        /// Returns true if any test executed by this has failed.
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

            //deploy everything first.
            foreach (var source in sources)
            {
                if (this.cancellationToken.Canceled)
                {
                    break;
                }
                
                var logger = (IMessageLogger)frameworkHandle;
                
                // discover the tests
                (new UnitTestDiscoverer()).DiscoverTestsInSource(source, logger, discoverySink, runContext?.RunSettings);
                tests.AddRange(discoverySink.Tests);
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
        private void ExecuteTests(IEnumerable<TestCase> tests, IRunContext runContext, IFrameworkHandle frameworkHandle, bool isDeploymentDone)
        {
            var testsBySource = (from test in tests
                                 group test by test.Source into testGroup
                                 select new { Source = testGroup.Key, Tests = testGroup });


            foreach (var group in testsBySource)
            {
                this.ExecuteTestsInSource(group.Tests, runContext, frameworkHandle, group.Source, isDeploymentDone);
            }
        }

        /// <summary>
        /// Execute the parameter tests present in parameter source
        /// </summary>
        private void ExecuteTestsInSource(IEnumerable<TestCase> tests,
                                         IRunContext runContext,
                                         ITestExecutionRecorder testExecutionRecorder,
                                         string source,
                                         bool isDeploymentDone)
        {
            Debug.Assert(!string.IsNullOrEmpty(source), "Source cannot be empty");

            adapterSettings = MSTestSettings.GetSettings(runContext);

            source = isDeploymentDone
                         ? Path.Combine(
                             PlatformServiceProvider.Instance.TestDeployment.GetDeploymentDirectory(),
                             Path.GetFileName(source)) : source;

            PlatformServiceProvider.Instance.TestDataSource.SetContext(source);

            using (var isolationHost = PlatformServiceProvider.Instance.TestSourceHost)
            {
                var testRunner = isolationHost.CreateInstanceForType(
                    typeof(UnitTestRunner),
                    new object[] { adapterSettings.CaptureDebugTraces },
                    source,
                    runContext?.RunSettings) as UnitTestRunner;
                PlatformServiceProvider.Instance.AdapterTraceLogger.LogInfo("Created unit-test runner {0}", source);

                this.ExecuteTestsWithTestRunner(tests, runContext, testExecutionRecorder, source, testRunner);

                PlatformServiceProvider.Instance.AdapterTraceLogger.LogInfo("Executed tests belonging to source {0}",
                    source);
            }
          
            PlatformServiceProvider.Instance.TestDataSource.ResetContext();
        }

        private void ExecuteTestsWithTestRunner(
            IEnumerable<TestCase> tests,
            IRunContext runContext,
            ITestExecutionRecorder testExecutionRecorder,
            string source,
            UnitTestRunner testRunner)
        {
            TestCase test = null;
            UnitTestResult[] unitTestResult = null;

            var startTime = DateTimeOffset.MinValue;
            var endTime = DateTimeOffset.MinValue;

            bool filterHasError;
            var filterExpression = this.TestMethodFilter.GetFilterExpression(
                runContext,
                testExecutionRecorder,
                out filterHasError);

            if (!filterHasError)
            {
                foreach (var currentTest in tests)
                {
                    // Skip test if not fitting filter criteria.
                    if (filterExpression != null && filterExpression.MatchTestCase(
                            currentTest,
                            (p) => this.TestMethodFilter.PropertyValueProvider(currentTest, p)) == false)
                    {
                        continue;
                    }

                    // Send previous test result.
                    this.SendTestResults(test, unitTestResult, startTime, endTime, testExecutionRecorder);

                    if (this.cancellationToken != null && this.cancellationToken.Canceled)
                    {
                        break;
                    }

                    var unitTestElement = currentTest.ToUnitTestElement(source);
                    testExecutionRecorder.RecordStart(currentTest);

                    startTime = DateTimeOffset.Now;

                    PlatformServiceProvider.Instance.AdapterTraceLogger.LogInfo(
                        "Executing test {0}",
                        unitTestElement.TestMethod.Name);

                    if (unitTestElement.Ignored)
                    {
                        unitTestResult = new[] { new UnitTestResult(UnitTestOutcome.Ignored, null) };
                    }
                    else
                    {
                        unitTestResult = testRunner.RunSingleTest(unitTestElement.TestMethod, this.sessionParameters);
                    }

                    PlatformServiceProvider.Instance.AdapterTraceLogger.LogInfo(
                        "Executed test {0}",
                        unitTestElement.TestMethod.Name);

                    endTime = DateTimeOffset.Now;
                    test = currentTest;
                }
            }

            IList<string> warnings = null;
            try
            {
                PlatformServiceProvider.Instance.AdapterTraceLogger.LogInfo("Executing cleanup methods.");
                var cleanupResult = testRunner.RunCleanup();
                PlatformServiceProvider.Instance.AdapterTraceLogger.LogInfo("Executed cleanup methods.");

                if (cleanupResult != null)
                {
                    warnings = cleanupResult.Warnings;

                    if (unitTestResult?.Length > 0)
                    {
                        var lastResult = unitTestResult[unitTestResult.Length - 1];
                        lastResult.StandardOut += cleanupResult.StandardOut;
                        lastResult.DebugTrace += cleanupResult.DebugTrace;
                    }
                }
            }
            finally
            {
                // Send last test result
                this.SendTestResults(test, unitTestResult, startTime, endTime, testExecutionRecorder);
            }
            this.LogWarnings(testExecutionRecorder, warnings);
        }

        private void CacheSessionParameters(IRunContext runContext, ITestExecutionRecorder testExecutionRecorder)
        {
            this.sessionParameters = PlatformServiceProvider.Instance.SettingsProvider.GetProperties();

            if (!string.IsNullOrEmpty(runContext?.RunSettings?.SettingsXml))
            {
                try
                {
                    var testRunParameters = XmlRunSettingsUtilities.GetTestRunParameters(runContext.RunSettings.SettingsXml);
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

        private void SendTestResults(TestCase test, UnitTestResult[] unitTestResults, DateTimeOffset startTime, DateTimeOffset endTime, ITestExecutionRecorder testExecutionRecorder)
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

                var testResult = unitTestResult.ToTestResult(test, startTime, endTime, adapterSettings.MapInconclusiveToFailed);
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
    }
}
