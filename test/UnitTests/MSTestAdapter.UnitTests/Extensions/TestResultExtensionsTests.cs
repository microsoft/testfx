// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Extensions;

using TestFramework.ForTestingMSTest;

using AdapterTestOutcome = Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel.UnitTestOutcome;
using UTF = Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Extensions;

public class TestResultExtensionsTests : TestContainer
{
    public void ToUnitTestResultsForTestResultWithExceptionConvertsToUnitTestResultsWithFailureOutcome()
    {
        UTF.TestResult[] results = [new UTF.TestResult() { TestFailureException = new Exception() }];
        MSTest.TestAdapter.ObjectModel.UnitTestResult[] convertedResults = results.ToUnitTestResults();

        Verify(convertedResults[0].Outcome == AdapterTestOutcome.Failed);
    }

    public void ToUnitTestResultsForTestResultWithExceptionConvertsToUnitTestResultsWithInconclusiveOutcome()
    {
        UTF.TestResult[] results = [new UTF.TestResult() { TestFailureException = new Exception(), Outcome = UTF.UnitTestOutcome.Inconclusive }];
        MSTest.TestAdapter.ObjectModel.UnitTestResult[] convertedResults = results.ToUnitTestResults();

        Verify(convertedResults[0].Outcome == AdapterTestOutcome.Inconclusive);
    }

    public void ToUnitTestResultsForTestResultShouldSetLoggingDataForConvertedUnitTestResults()
    {
        var timespan = default(TimeSpan);
        UTF.TestResult[] results =
        [
            new UTF.TestResult()
            {
                DebugTrace = "debugTrace",
                DisplayName = "displayName",
                Duration = timespan,
                LogOutput = "logOutput",
                LogError = "logError",
                DatarowIndex = 1,
            }
        ];
        MSTest.TestAdapter.ObjectModel.UnitTestResult[] convertedResults = results.ToUnitTestResults();

        Verify(convertedResults[0].StandardOut == "logOutput");
        Verify(convertedResults[0].StandardError == "logError");
        Verify(convertedResults[0].DisplayName == "displayName");
        Verify(convertedResults[0].DebugTrace == "debugTrace");
        Verify(timespan == convertedResults[0].Duration);
        Verify(convertedResults[0].DatarowIndex == 1);
    }

    public void ToUnitTestResultsForTestResultShouldSetStandardOut()
    {
        UTF.TestResult[] results =
        [
            new UTF.TestResult()
            {
                LogOutput = "logOutput",
            }
        ];
        MSTest.TestAdapter.ObjectModel.UnitTestResult[] convertedResults = results.ToUnitTestResults();

        Verify(convertedResults[0].StandardOut == "logOutput");
    }

    public void ToUnitTestResultsForTestResultShouldSetStandardError()
    {
        UTF.TestResult[] results =
        [
            new UTF.TestResult()
            {
                LogError = "logError",
            }
        ];

        MSTest.TestAdapter.ObjectModel.UnitTestResult[] convertedResults = results.ToUnitTestResults();

        Verify(convertedResults[0].StandardError == "logError");
    }

    public void ToUnitTestResultsForTestResultShouldSetDebugTrace()
    {
        UTF.TestResult[] results =
        [
            new UTF.TestResult()
            {
                DebugTrace = "debugTrace",
            }
        ];

        MSTest.TestAdapter.ObjectModel.UnitTestResult[] convertedResults = results.ToUnitTestResults();

        Verify(convertedResults[0].DebugTrace == "debugTrace");
    }

    public void ToUnitTestResultsForTestResultShouldSetTestContextMessages()
    {
        UTF.TestResult[] results =
        [
            new UTF.TestResult()
            {
                TestContextMessages = "Context",
            }
        ];

        MSTest.TestAdapter.ObjectModel.UnitTestResult[] convertedResults = results.ToUnitTestResults();

        Verify(convertedResults[0].TestContextMessages == "Context");
    }

    public void ToUnitTestResultsForTestResultShouldSetDuration()
    {
        var timespan = default(TimeSpan);
        UTF.TestResult[] results =
        [
            new UTF.TestResult()
            {
                Duration = timespan,
            }
        ];

        MSTest.TestAdapter.ObjectModel.UnitTestResult[] convertedResults = results.ToUnitTestResults();

        Verify(timespan == convertedResults[0].Duration);
    }

    public void ToUnitTestResultsForTestResultShouldSetDisplayName()
    {
        UTF.TestResult[] results =
        [
            new UTF.TestResult()
            {
                DisplayName = "displayName",
            }
        ];

        MSTest.TestAdapter.ObjectModel.UnitTestResult[] convertedResults = results.ToUnitTestResults();

        Verify(convertedResults[0].DisplayName == "displayName");
    }

    public void ToUnitTestResultsForTestResultShouldSetDataRowIndex()
    {
        UTF.TestResult[] results =
        [
            new UTF.TestResult()
            {
                DatarowIndex = 1,
            }
        ];

        MSTest.TestAdapter.ObjectModel.UnitTestResult[] convertedResults = results.ToUnitTestResults();

        Verify(convertedResults[0].DatarowIndex == 1);
    }

    public void ToUnitTestResultsForTestResultShouldSetParentInfo()
    {
        var executionId = Guid.NewGuid();
        var parentExecId = Guid.NewGuid();
        int innerResultsCount = 5;

        UTF.TestResult[] results =
        [
            new UTF.TestResult()
            {
                ExecutionId = executionId,
                ParentExecId = parentExecId,
                InnerResultsCount = innerResultsCount,
            }
        ];

        MSTest.TestAdapter.ObjectModel.UnitTestResult[] convertedResults = results.ToUnitTestResults();

        Verify(executionId == convertedResults[0].ExecutionId);
        Verify(parentExecId == convertedResults[0].ParentExecId);
        Verify(innerResultsCount == convertedResults[0].InnerResultsCount);
    }

    public void ToUnitTestResultsShouldHaveResultsFileProvidedToTestResult()
    {
        UTF.TestResult[] results = [new UTF.TestResult() { ResultFiles = new List<string>() { "DummyFile.txt" } }];
        MSTest.TestAdapter.ObjectModel.UnitTestResult[] convertedResults = results.ToUnitTestResults();
        Verify(convertedResults[0].ResultFiles[0] == "DummyFile.txt");
    }
}
