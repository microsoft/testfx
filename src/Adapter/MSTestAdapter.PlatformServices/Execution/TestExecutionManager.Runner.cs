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
            // A superseded in-process retry attempt is not the test's outcome, so it must not turn the run red:
            // the recorder still reports it (so tooling can show the retry), but the "any test failed" verdict
            // only follows the final attempt.
            if (await testResultRecorder.RecordResultAsync(test, unitTestResult, startTime, endTime).ConfigureAwait(false)
                && !unitTestResult.IsSupersededRetryAttempt)
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
        bool usesAppDomains,
        TestDependencyCoordinator? dependencyCoordinator = null)
    {
        // Ordering keys mirror the historical VSTest ManagedType/ManagedMethod test-case properties, which are
        // only populated when the test method carries managed method metadata (see UnitTestElement.ToTestCase).
        // When a dependency graph is in effect the order is already fixed by the graph's topological sort -
        // which took name order as its tie-breaker, so the setting is honored there rather than bypassed -
        // and re-sorting here would undo it.
        IEnumerable<UnitTestElement> orderedTests = dependencyCoordinator is null
            && MSTestSettings.CurrentSettings.OrderTestsByNameInClass && !MSTestSettings.CurrentSettings.RandomizeTestOrder
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

            // A test whose prerequisite did not pass is reported as skipped instead of being run. This is
            // decided here, right before the test starts, so that the outcome of a prerequisite that finished
            // moments ago on another worker is always taken into account.
            if (dependencyCoordinator is not null && dependencyCoordinator.ShouldSkip(currentTest, out string? skipReason))
            {
                await ReportSkippedDependentAsync(
                    currentTest,
                    unitTestElement,
                    skipReason,
                    dependencyCoordinator,
                    sourceLevelParameters,
                    lifecycleContextProperties,
                    testRunner,
                    usesAppDomains,
                    remotingMessageLogger).ConfigureAwait(false);
                continue;
            }

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

            dependencyCoordinator?.RecordOutcome(currentTest, AllPassed(unitTestResult));

            await SendTestResultsAsync(currentTest, unitTestResult, startTime, endTime, _testResultRecorder).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Determines whether a test counts as having passed for the purpose of gating its dependents. A test
    /// that produced no result at all (which the recorder reports as an error) does not qualify, and a
    /// data-driven test qualifies only when every one of its rows passed. Superseded in-process retry attempts
    /// are ignored: only the final attempt decides the test's outcome.
    /// </summary>
    private static bool AllPassed(TestTools.UnitTesting.TestResult[] results)
    {
        if (results.Length == 0)
        {
            return false;
        }

        foreach (TestTools.UnitTesting.TestResult result in results)
        {
            if (result.IsSupersededRetryAttempt)
            {
                continue;
            }

            if (result.Outcome != TestTools.UnitTesting.UnitTestOutcome.Passed)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Reports a test that was not run because one of its prerequisites did not pass. It is recorded as
    /// skipped - not failed - so that a single root cause stays visible as one failure surrounded by clearly
    /// labelled skips, and it is recorded as "did not pass" so the skip propagates to its own dependents.
    /// </summary>
    private async Task ReportSkippedDependentAsync(
        UnitTestElement test,
        UnitTestElement unitTestElement,
        string reason,
        TestDependencyCoordinator dependencyCoordinator,
        IDictionary<string, object> sourceLevelParameters,
        Dictionary<string, object?> lifecycleContextProperties,
        UnitTestRunner testRunner,
        bool usesAppDomains,
        IAdapterMessageLogger remotingMessageLogger)
    {
        dependencyCoordinator.RecordNotRun(test);

        DateTimeOffset now = DateTimeOffset.Now;
        await _testResultRecorder.RecordStartAsync(test).ConfigureAwait(false);

        // The test was selected, so it is counted in the class-cleanup countdown even though it is not going
        // to run. Tell the runner, or the class never completes - which loses its [ClassCleanup] and, because
        // end-of-assembly cleanup waits on every class, the assembly's [AssemblyCleanup] too.
        Dictionary<string, object?> testContextProperties = GetTestContextProperties(test.ExecutionContextProperties, sourceLevelParameters, unitTestElement);
        TestTools.UnitTesting.TestResult[] cleanupResults = usesAppDomains || Thread.CurrentThread.GetApartmentState() == ApartmentState.STA
#pragma warning disable VSTHRD103 // Call async methods when in an async method - mirrors RunSingleTest: Task cannot cross app domains, and an await would leave the STA thread.
            ? testRunner.NotifyTestNotRun(unitTestElement, testContextProperties, lifecycleContextProperties, remotingMessageLogger)
#pragma warning restore VSTHRD103
            : await testRunner.NotifyTestNotRunAsync(unitTestElement, testContextProperties, lifecycleContextProperties, remotingMessageLogger).ConfigureAwait(false);

        await SendTestResultsAsync(
            test,
            [TestTools.UnitTesting.TestResult.CreateIgnoredResult(reason), .. cleanupResults],
            now,
            now,
            _testResultRecorder).ConfigureAwait(false);
    }
}
