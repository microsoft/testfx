// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;

using UTF = Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Extensions;

#if NET6_0_OR_GREATER
[Obsolete(Constants.PublicTypeObsoleteMessage, DiagnosticId = "MSTESTOBS")]
#else
[Obsolete(Constants.PublicTypeObsoleteMessage)]
#endif
public static class TestResultExtensions
{
    /// <summary>
    /// Converts the test framework's TestResult objects array to a serializable UnitTestResult objects array.
    /// </summary>
    /// <param name="testResults">The test framework's TestResult object array.</param>
    /// <returns>The serializable UnitTestResult object array.</returns>
    public static UnitTestResult[] ToUnitTestResults(this UTF.TestResult[] testResults)
        => ToUnitTestResults((IReadOnlyCollection<UTF.TestResult>)testResults);

    internal static UnitTestResult[] ToUnitTestResults(this IReadOnlyCollection<UTF.TestResult> testResults)
    {
        var unitTestResults = new UnitTestResult[testResults.Count];

        int i = 0;
        foreach (UTF.TestResult testResult in testResults)
        {
            var outcome = testResult.Outcome.ToUnitTestOutcome();

            UnitTestResult unitTestResult = testResult.TestFailureException is { } testFailureException
                ? new UnitTestResult(
                    new TestFailedException(
                        outcome,
                        testFailureException.TryGetMessage(),
                        testFailureException is TestFailedException testException ? testException.StackTraceInformation : testFailureException.TryGetStackTraceInformation()))
                : new UnitTestResult { Outcome = outcome };
            unitTestResult.StandardOut = testResult.LogOutput;
            unitTestResult.StandardError = testResult.LogError;
            unitTestResult.DebugTrace = testResult.DebugTrace;
            unitTestResult.TestContextMessages = testResult.TestContextMessages;
            unitTestResult.Duration = testResult.Duration;
            unitTestResult.DisplayName = testResult.DisplayName;
            unitTestResult.DatarowIndex = testResult.DatarowIndex;
            unitTestResult.ResultFiles = testResult.ResultFiles;
            unitTestResult.ExecutionId = testResult.ExecutionId;
            unitTestResult.ParentExecId = testResult.ParentExecId;
            unitTestResult.InnerResultsCount = testResult.InnerResultsCount;
            unitTestResults[i] = unitTestResult;
            i++;
        }

        return unitTestResults;
    }
}
