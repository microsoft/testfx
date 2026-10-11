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

        public void PrepareResults(UnitTestElement testElement, FrameworkTestResult[] results)
        {
            StringComparer pathComparer = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                ? StringComparer.OrdinalIgnoreCase
                : StringComparer.Ordinal;
            var supersededFiles = new Dictionary<(string DisplayName, int Ordinal), HashSet<string>>();
            var finalResults = new List<((string DisplayName, int Ordinal) Key, FrameworkTestResult Result)>();
            var ordinals = new Dictionary<string, int>(StringComparer.Ordinal);
            int currentAttempt = -1;

            foreach (FrameworkTestResult result in results)
            {
                if (currentAttempt != result.RetryAttemptNumber)
                {
                    currentAttempt = result.RetryAttemptNumber;
                    ordinals.Clear();
                }

                string displayName = GetResultDisplayName(testElement, result);
                ordinals.TryGetValue(displayName, out int ordinal);
                ordinals[displayName] = ordinal + 1;
                (string DisplayName, int Ordinal) key = (displayName, ordinal);

                if (result.IsSupersededRetryAttempt)
                {
                    if (result.ResultFiles is { Count: > 0 })
                    {
                        if (!supersededFiles.TryGetValue(key, out HashSet<string>? files))
                        {
#pragma warning disable IDE0028 // Collection initialization cannot preserve the platform-specific comparer.
                            files = new HashSet<string>(pathComparer);
#pragma warning restore IDE0028
                            supersededFiles.Add(key, files);
                        }

                        files.UnionWith(result.ResultFiles);
                    }
                }
                else
                {
                    finalResults.Add((key, result));
                }
            }

            if (supersededFiles.Count == 0 || finalResults.Count == 0)
            {
                return;
            }

            foreach (((string DisplayName, int Ordinal) key, FrameworkTestResult result) in finalResults)
            {
                HashSet<string>? files = supersededFiles.TryGetValue(key, out HashSet<string>? matchedFiles)
                    && supersededFiles.Remove(key)
                        ? matchedFiles
                        : null;
                if (files is not null)
                {
                    MergeResultFiles(result, files, pathComparer);
                }
            }

            if (supersededFiles.Count > 0)
            {
                MergeResultFiles(
                    finalResults[finalResults.Count - 1].Result,
                    supersededFiles.Values.SelectMany(static files => files),
                    pathComparer);
            }
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
            // VSTest has no notion of in-process retry attempts: it keys results by test case and would show a
            // [Retry]-decorated test's earlier attempts as duplicate results in Test Explorer and in the TRX it
            // produces. Only the final attempt - the test's actual outcome - is recorded here, which is exactly
            // what MSTest reported before the attempts were surfaced to Microsoft.Testing.Platform.
            if (unitTestResult.IsSupersededRetryAttempt)
            {
                return Task.FromResult(false);
            }

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

        private static string GetResultDisplayName(UnitTestElement testElement, FrameworkTestResult result)
            => result.DisplayName
                ?? testElement.TestMethod.DisplayName
                ?? testElement.TestMethod.Name;

        private static void MergeResultFiles(
            FrameworkTestResult result,
            IEnumerable<string> additionalFiles,
            StringComparer pathComparer)
        {
            var mergedFiles = new HashSet<string>(result.ResultFiles ?? [], pathComparer);
            mergedFiles.UnionWith(additionalFiles);
            result.ResultFiles = [.. mergedFiles];
        }
    }
}
