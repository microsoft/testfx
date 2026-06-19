// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Extensions;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Helpers;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Adapter;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Logging;

namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution;

internal partial class TestExecutionManager
{
    internal void SendTestResults(
        TestCase test,
        TestTools.UnitTesting.TestResult[] unitTestResults,
        DateTimeOffset startTime,
        DateTimeOffset endTime,
        ITestExecutionRecorder testExecutionRecorder)
    {
        foreach (TestTools.UnitTesting.TestResult unitTestResult in unitTestResults)
        {
            _testRunCancellationToken?.ThrowIfCancellationRequested();

            var testResult = unitTestResult.ToTestResult(
                test,
                startTime,
                endTime,
                _environment.MachineName,
                MSTestSettings.CurrentSettings);

            testExecutionRecorder.RecordEnd(test, testResult.Outcome);

            if (testResult.Outcome == TestOutcome.Failed)
            {
                if (PlatformServiceProvider.Instance.AdapterTraceLogger.IsInfoEnabled)
                {
                    PlatformServiceProvider.Instance.AdapterTraceLogger.Info("MSTestExecutor:Test {0} failed. ErrorMessage:{1}, ErrorStackTrace:{2}.", testResult.TestCase.FullyQualifiedName, testResult.ErrorMessage, testResult.ErrorStackTrace);
                }

#if !WINDOWS_UWP && !WIN_UI
                _hasAnyTestFailed = true;
#endif
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
            // Skip test if not fitting filter criteria.
            return false;
        }

        return true;
    }

    private async Task ExecuteTestsWithTestRunnerAsync(
        IEnumerable<TestCase> tests,
        ITestExecutionRecorder testExecutionRecorder,
        string source,
        IDictionary<string, object> sourceLevelParameters,
        UnitTestRunner testRunner,
        bool usesAppDomains)
    {
        IEnumerable<TestCase> orderedTests = MSTestSettings.CurrentSettings.OrderTestsByNameInClass && !MSTestSettings.CurrentSettings.RandomizeTestOrder
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

            UnitTestElement unitTestElement = currentTest.ToUnitTestElementWithUpdatedSource(source);

            testExecutionRecorder.RecordStart(currentTest);

            DateTimeOffset startTime = DateTimeOffset.Now;

            if (PlatformServiceProvider.Instance.AdapterTraceLogger.IsInfoEnabled)
            {
                PlatformServiceProvider.Instance.AdapterTraceLogger.Info("Executing test {0}", unitTestElement.TestMethod.Name);
            }

            // Run single test passing test context properties to it.
            IDictionary<TestProperty, object?>? tcmProperties = TcmTestPropertiesProvider.GetTcmProperties(currentTest);
            Dictionary<string, object?> testContextProperties = GetTestContextProperties(tcmProperties, sourceLevelParameters, unitTestElement);

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
                unitTestResult = testRunner.RunSingleTest(unitTestElement, testContextProperties, remotingMessageLogger);
#pragma warning restore VSTHRD103 // Call async methods when in an async method
            }
            else
            {
                unitTestResult = await testRunner.RunSingleTestAsync(unitTestElement, testContextProperties, remotingMessageLogger).ConfigureAwait(false);
            }

            if (PlatformServiceProvider.Instance.AdapterTraceLogger.IsInfoEnabled)
            {
                PlatformServiceProvider.Instance.AdapterTraceLogger.Info("Executed test {0}", unitTestElement.TestMethod.Name);
            }

            DateTimeOffset endTime = DateTimeOffset.Now;

            SendTestResults(currentTest, unitTestResult, startTime, endTime, testExecutionRecorder);
        }
    }
}
