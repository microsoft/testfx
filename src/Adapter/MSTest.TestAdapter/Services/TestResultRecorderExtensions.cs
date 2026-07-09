// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Extensions;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Helpers;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Adapter;

using FrameworkTestResult = Microsoft.VisualStudio.TestTools.UnitTesting.TestResult;

namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter;

/// <summary>
/// Bridges a VSTest <see cref="ITestExecutionRecorder"/> to the platform-agnostic <see cref="ITestResultRecorder"/>.
/// </summary>
/// <remarks>
/// This is the single translation point between the neutral execution model (<see cref="UnitTestElement"/> and
/// the framework <see cref="FrameworkTestResult"/>) and the VSTest result object model (<c>TestResult</c>,
/// <c>TestOutcome</c>, attachments, ...). It lives in the adapter layer; the platform services engine reports
/// through the neutral <see cref="ITestResultRecorder"/> only. The element's host test case is resolved (and
/// materialized/cached on demand for internally discovered tests) via <c>UnitTestElementExtensions.GetOrCreateHostTestCase</c>
/// so recorded results preserve host-injected data (test-case-management / data-collector properties) with full fidelity.
/// </remarks>
internal static class TestResultRecorderExtensions
{
    /// <summary>
    /// Wraps a VSTest <see cref="ITestExecutionRecorder"/> as an <see cref="ITestResultRecorder"/>.
    /// </summary>
    /// <param name="testExecutionRecorder">The host recorder to wrap.</param>
    /// <param name="computerName">The computer name stamped on reported results.</param>
    /// <param name="settings">The current MSTest settings used to map framework outcomes to host outcomes.</param>
    /// <returns>A platform-agnostic recorder that forwards to <paramref name="testExecutionRecorder"/>.</returns>
    public static ITestResultRecorder ToTestResultRecorder(this ITestExecutionRecorder testExecutionRecorder, string computerName, MSTestSettings settings)
        => new HostTestResultRecorder(testExecutionRecorder, computerName, settings);

    private sealed class HostTestResultRecorder : ITestResultRecorder
    {
        private readonly ITestExecutionRecorder _testExecutionRecorder;
        private readonly string _computerName;
        private readonly MSTestSettings _settings;

        public HostTestResultRecorder(ITestExecutionRecorder testExecutionRecorder, string computerName, MSTestSettings settings)
        {
            _testExecutionRecorder = testExecutionRecorder;
            _computerName = computerName;
            _settings = settings;
        }

        public Task RecordStartAsync(UnitTestElement testElement)
        {
            _testExecutionRecorder.RecordStart(testElement.GetOrCreateHostTestCase());
            return Task.CompletedTask;
        }

        public Task RecordEmptyResultAsync(UnitTestElement testElement)
        {
            _testExecutionRecorder.RecordEnd(testElement.GetOrCreateHostTestCase(), TestOutcome.None);
            return Task.CompletedTask;
        }

        public Task<bool> RecordResultAsync(UnitTestElement testElement, FrameworkTestResult unitTestResult, DateTimeOffset startTime, DateTimeOffset endTime)
        {
            TestCase testCase = testElement.GetOrCreateHostTestCase();
            var testResult = unitTestResult.ToTestResult(testCase, startTime, endTime, _computerName, _settings);

            _testExecutionRecorder.RecordEnd(testCase, testResult.Outcome);

            bool isFailed = testResult.Outcome == TestOutcome.Failed;
            if (isFailed && PlatformServiceProvider.Instance.AdapterTraceLogger.IsInfoEnabled)
            {
                PlatformServiceProvider.Instance.AdapterTraceLogger.Info("MSTestExecutor:Test {0} failed. ErrorMessage:{1}, ErrorStackTrace:{2}.", testResult.TestCase.FullyQualifiedName, testResult.ErrorMessage, testResult.ErrorStackTrace);
            }

            try
            {
                if (testResult.Outcome != TestOutcome.NotFound
                    || !RuntimeContext.IsHotReloadEnabled)
                {
                    _testExecutionRecorder.RecordResult(testResult);
                }
            }
            catch (TestCanceledException)
            {
                // Ignore this exception
            }

            // A failure is reported only once RecordResult has completed (a swallowed TestCanceledException
            // still counts as completed). This mirrors the original inline flow where the failure was
            // observed as part of reporting the result.
            return Task.FromResult(isFailed);
        }
    }
}
