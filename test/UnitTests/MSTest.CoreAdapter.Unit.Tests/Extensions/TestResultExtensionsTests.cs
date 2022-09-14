// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Extensions;

using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Extensions;

using TestFramework.ForTestingMSTest;

using AdapterTestOutcome = Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel.UnitTestOutcome;
using UTF = Microsoft.VisualStudio.TestTools.UnitTesting;

public class TestResultExtensionsTests : TestContainer
{
    public void ToUnitTestResultsForTestResultWithExceptionConvertsToUnitTestResultsWithFailureOutcome()
    {
        var results = new[] { new UTF.TestResult() { TestFailureException = new Exception() } };
        var convertedResults = results.ToUnitTestResults();

        Verify(AdapterTestOutcome.Failed == convertedResults[0].Outcome);
    }

    public void ToUnitTestResultsForTestResultWithExceptionConvertsToUnitTestResultsWithInconclusiveOutcome()
    {
        var results = new[] { new UTF.TestResult() { TestFailureException = new Exception(), Outcome = UTF.UnitTestOutcome.Inconclusive } };
        var convertedResults = results.ToUnitTestResults();

        Verify(AdapterTestOutcome.Inconclusive == convertedResults[0].Outcome);
    }

    public void ToUnitTestResultsForTestResultShouldSetLoggingDataForConvertedUnitTestResults()
    {
        var timespan = default(TimeSpan);
        var results = new[]
        {
            new UTF.TestResult()
            {
                DebugTrace = "debugTrace", DisplayName = "displayName", Duration = timespan, LogOutput = "logOutput",
                                                     LogError = "logError", DatarowIndex = 1
            }
        };
        var convertedResults = results.ToUnitTestResults();

        Verify("logOutput" == convertedResults[0].StandardOut);
        Verify("logError" == convertedResults[0].StandardError);
        Verify("displayName" == convertedResults[0].DisplayName);
        Verify("debugTrace" == convertedResults[0].DebugTrace);
        Verify(timespan == convertedResults[0].Duration);
        Verify(1 == convertedResults[0].DatarowIndex);
    }

    public void ToUnitTestResultsForTestResultShouldSetStandardOut()
    {
        var results = new[]
        {
            new UTF.TestResult()
            {
                LogOutput = "logOutput"
            }
        };
        var convertedResults = results.ToUnitTestResults();

        Verify("logOutput" == convertedResults[0].StandardOut);
    }

    public void ToUnitTestResultsForTestResultShouldSetStandardError()
    {
        var results = new[]
        {
            new UTF.TestResult()
            {
                LogError = "logError"
            }
        };

        var convertedResults = results.ToUnitTestResults();

        Verify("logError" == convertedResults[0].StandardError);
    }

    public void ToUnitTestResultsForTestResultShouldSetDebugTrace()
    {
        var results = new[]
        {
            new UTF.TestResult()
            {
                DebugTrace = "debugTrace"
            }
        };

        var convertedResults = results.ToUnitTestResults();

        Verify("debugTrace" == convertedResults[0].DebugTrace);
    }

    public void ToUnitTestResultsForTestResultShouldSetTestContextMessages()
    {
        var results = new[]
        {
            new UTF.TestResult()
            {
                TestContextMessages = "Context"
            }
        };

        var convertedResults = results.ToUnitTestResults();

        Verify("Context" == convertedResults[0].TestContextMessages);
    }

    public void ToUnitTestResultsForTestResultShouldSetDuration()
    {
        var timespan = default(TimeSpan);
        var results = new[]
        {
            new UTF.TestResult()
            {
                Duration = timespan
            }
        };

        var convertedResults = results.ToUnitTestResults();

        Verify(timespan == convertedResults[0].Duration);
    }

    public void ToUnitTestResultsForTestResultShouldSetDisplayName()
    {
        var results = new[]
        {
            new UTF.TestResult()
            {
                DisplayName = "displayName"
            }
        };

        var convertedResults = results.ToUnitTestResults();

        Verify("displayName" == convertedResults[0].DisplayName);
    }

    public void ToUnitTestResultsForTestResultShouldSetDataRowIndex()
    {
        var results = new[]
        {
            new UTF.TestResult()
            {
                DatarowIndex = 1
            }
        };

        var convertedResults = results.ToUnitTestResults();

        Verify(1 == convertedResults[0].DatarowIndex);
    }

    public void ToUnitTestResultsForTestResultShouldSetParentInfo()
    {
        var executionId = Guid.NewGuid();
        var parentExecId = Guid.NewGuid();
        var innerResultsCount = 5;

        var results = new[]
        {
            new UTF.TestResult()
            {
                ExecutionId = executionId,
                ParentExecId = parentExecId,
                InnerResultsCount = innerResultsCount
            }
        };

        var convertedResults = results.ToUnitTestResults();

        Verify(executionId == convertedResults[0].ExecutionId);
        Verify(parentExecId == convertedResults[0].ParentExecId);
        Verify(innerResultsCount == convertedResults[0].InnerResultsCount);
    }

    public void ToUnitTestResultsShouldHaveResultsFileProvidedToTestResult()
    {
        var results = new[] { new UTF.TestResult() { ResultFiles = new List<string>() { "DummyFile.txt" } } };
        var convertedResults = results.ToUnitTestResults();
        Verify("DummyFile.txt" == convertedResults[0].ResultFiles[0]);
    }
}
