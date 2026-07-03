// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Extensions;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Helpers;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Adapter;

using FrameworkTestResult = Microsoft.VisualStudio.TestTools.UnitTesting.TestResult;

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;

/// <summary>
/// Bridges a VSTest <see cref="ITestExecutionRecorder"/> to the platform-agnostic <see cref="ITestResultRecorder"/>.
/// </summary>
/// <remarks>
/// This is the single translation point between the VSTest result object model (<c>TestResult</c>,
/// <c>TestOutcome</c>, attachments, ...) and the platform services result-reporting abstraction. It is
/// expected to move entirely into the adapter layer once the execution pipeline no longer flows VSTest
/// recorders through the platform services.
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

        public void RecordStart(UnitTestElement testElement)
            => _testExecutionRecorder.RecordStart(ResolveTestCase(testElement));

        public void RecordEmptyResult(UnitTestElement testElement)
            => _testExecutionRecorder.RecordEnd(ResolveTestCase(testElement), TestOutcome.None);

        public bool RecordResult(UnitTestElement testElement, FrameworkTestResult unitTestResult, DateTimeOffset startTime, DateTimeOffset endTime)
        {
            TestCase testCase = ResolveTestCase(testElement);
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
            return isFailed;
        }

        /// <summary>
        /// Resolves the VSTest <see cref="TestCase"/> to record against. When the element carries the host's
        /// original representation (tests handed to the adapter to run), that exact test case is reused so all
        /// host-injected data (including test-case-management properties) is preserved with full fidelity;
        /// otherwise a single test case is materialized from the neutral element and reused (tests discovered
        /// internally).
        /// </summary>
        private static TestCase ResolveTestCase(UnitTestElement testElement)
            => testElement.GetOrCreateHostTestCase();
    }
}
