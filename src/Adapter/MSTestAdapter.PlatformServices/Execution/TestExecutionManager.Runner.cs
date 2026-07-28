// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface;

namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution;

internal partial class TestExecutionManager
{
    internal async Task SendTestResultsAsync(
        UnitTestElement test,
        TestTools.UnitTesting.TestResult[] unitTestResults,
        DateTimeOffset startTime,
        DateTimeOffset endTime,
        ITestResultRecorder testResultRecorder)
    {
        if (unitTestResults.Length == 0)
        {
            await testResultRecorder.RecordEmptyResultAsync(test).ConfigureAwait(false);
            return;
        }

        foreach (TestTools.UnitTesting.TestResult unitTestResult in unitTestResults)
        {
            _testRunCancellationToken?.ThrowIfCancellationRequested();

#if !WINDOWS_UWP && !WIN_UI
            if (await testResultRecorder.RecordResultAsync(test, unitTestResult, startTime, endTime).ConfigureAwait(false))
            {
                _hasAnyTestFailed = true;
            }
#else
            await testResultRecorder.RecordResultAsync(test, unitTestResult, startTime, endTime).ConfigureAwait(false);
#endif
        }
    }

    private static bool MatchTestFilter(ITestElementFilter? filter, UnitTestElement test, string source)
    {
        if (filter is not null
            && !filter.Matches(test.WithUpdatedSource(source)))
        {
            // Skip test if not fitting filter criteria.
            return false;
        }

        return true;
    }

    private async Task ExecuteTestsWithTestRunnerAsync(
        IEnumerable<UnitTestElement> tests,
        IAdapterMessageLogger adapterMessageLogger,
        string source,
        IDictionary<string, object> sourceLevelParameters,
        UnitTestRunner testRunner,
        bool usesAppDomains)
    {
        // Ordering keys mirror the historical VSTest ManagedType/ManagedMethod test-case properties, which are
        // only populated when the test method carries managed method metadata (see UnitTestElement.ToTestCase).
        IEnumerable<UnitTestElement> orderedTests = MSTestSettings.CurrentSettings.OrderTestsByNameInClass && !MSTestSettings.CurrentSettings.RandomizeTestOrder
            ? tests.OrderBy(t => t.TestMethod.HasManagedMethodAndTypeProperties ? t.TestMethod.ManagedTypeName : null)
                .ThenBy(t => t.TestMethod.HasManagedMethodAndTypeProperties ? t.TestMethod.ManagedMethodName : null)
            : tests;

        // If testRunner is in a different AppDomain, we cannot pass the message logger directly.
        // Instead, we pass a proxy (remoting object) that is marshallable by ref.
        IAdapterMessageLogger remotingMessageLogger = usesAppDomains
            ? new RemotingMessageLogger(adapterMessageLogger)
            : adapterMessageLogger;

        Dictionary<string, object?> lifecycleContextProperties = [with(sourceLevelParameters!)];

        foreach (UnitTestElement currentTest in orderedTests)
        {
            _testRunCancellationToken?.ThrowIfCancellationRequested();
            if (PlatformServiceProvider.Instance.IsGracefulStopRequested)
            {
                break;
            }

            UnitTestElement unitTestElement = currentTest.WithUpdatedSource(source);

            // Report through the neutral recorder using the element itself; the adapter-side recorder resolves
            // the host test case (preserving host-injected TCM / data-collector properties) with full fidelity.
            await _testResultRecorder.RecordStartAsync(currentTest).ConfigureAwait(false);

            DateTimeOffset startTime = DateTimeOffset.Now;

            if (PlatformServiceProvider.Instance.AdapterTraceLogger.IsInfoEnabled)
            {
                PlatformServiceProvider.Instance.AdapterTraceLogger.Info("Executing test {0}", unitTestElement.TestMethod.Name);
            }

            // Run single test passing test context properties to it.
            IReadOnlyDictionary<string, object?>? tcmProperties = currentTest.ExecutionContextProperties;
            Dictionary<string, object?> testContextProperties = GetTestContextProperties(tcmProperties, sourceLevelParameters, unitTestElement);

            TestTools.UnitTesting.TestResult[] unitTestResult;
            if (usesAppDomains || Thread.CurrentThread.GetApartmentState() == ApartmentState.STA)
            {
#pragma warning disable VSTHRD103 // Call async methods when in an async method - We cannot do right now because we are crossing app domains.
                // When app domains support is dropped, we can finally always be calling the async version.
                // In addition to app domains, if we are STA thread (e.g, because runsettings setting ExecutionApartmentState to STA), we want to preserve that.
                // If we await, we could end up in a thread pool thread, which is not what we want.
                // Alternatively, if we want to use RunSingleTestAsync for the case of STA, we should have:
                // 1. A custom single threaded synchronization context that keeps us in STA.
                // 2. Use ConfigureAwait(true).
                unitTestResult = testRunner.RunSingleTest(unitTestElement, testContextProperties, lifecycleContextProperties, remotingMessageLogger);
#pragma warning restore VSTHRD103 // Call async methods when in an async method
            }
            else
            {
                unitTestResult = await testRunner.RunSingleTestAsync(unitTestElement, testContextProperties, lifecycleContextProperties, remotingMessageLogger).ConfigureAwait(false);
            }

            if (PlatformServiceProvider.Instance.AdapterTraceLogger.IsInfoEnabled)
            {
                PlatformServiceProvider.Instance.AdapterTraceLogger.Info("Executed test {0}", unitTestElement.TestMethod.Name);
            }

            DateTimeOffset endTime = DateTimeOffset.Now;

            await SendTestResultsAsync(currentTest, unitTestResult, startTime, endTime, _testResultRecorder).ConfigureAwait(false);
        }
    }
}
