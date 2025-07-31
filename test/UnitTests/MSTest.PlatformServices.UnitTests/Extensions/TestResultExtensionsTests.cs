// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using TestFramework.ForTestingMSTest;

using UTF = Microsoft.VisualStudio.TestTools.UnitTesting;
using VSTestTestOutcome = Microsoft.VisualStudio.TestPlatform.ObjectModel.TestOutcome;
using VSTestTestResultMessage = Microsoft.VisualStudio.TestPlatform.ObjectModel.TestResultMessage;

namespace MSTest.PlatformServices.Extensions.UnitTests;

public class TestResultExtensionsTests : TestContainer
{
    public void ToUnitTestResultsForTestResultWithExceptionConvertsToUnitTestResultsWithFailureOutcome()
    {
        var result = new TestResult { TestFailureException = new Exception() };
        var convertedResult = result.ToTestResult(new(), default, default, string.Empty, new());

        Verify(convertedResult.Outcome == VSTestTestOutcome.Failed);
    }

    public void ToUnitTestResultsForTestResultWithExceptionConvertsToUnitTestResultsWithInconclusiveOutcome()
    {
        var result = new TestResult { TestFailureException = new Exception(), Outcome = UnitTestOutcome.Inconclusive };
        var convertedResult = result.ToTestResult(new(), default, default, string.Empty, new());

        Verify(convertedResult.Outcome == VSTestTestOutcome.Skipped);
    }

    public void ToUnitTestResultsForTestResultShouldSetLoggingDataForConvertedUnitTestResults()
    {
        var timespan = default(TimeSpan);
        var result = new TestResult
        {
            DebugTrace = "debugTrace",
            DisplayName = "displayName",
            Duration = timespan,
            LogOutput = "logOutput",
            LogError = "logError",
            DatarowIndex = 1,
        };

        var convertedResult = result.ToTestResult(new() { DisplayName = result.DisplayName }, default, default, string.Empty, new());
        VSTestTestResultMessage[] stdOutMessages = [.. convertedResult.Messages.Where(m => m.Category == VSTestTestResultMessage.StandardOutCategory)];
        Verify(stdOutMessages[0].Text == "logOutput");
        Verify(convertedResult.Messages.Single(m => m.Category == VSTestTestResultMessage.StandardErrorCategory).Text == "logError");
        Verify(convertedResult.DisplayName == "displayName (Data Row 1)");
        Verify(stdOutMessages[1].Text == """


            Debug Trace:
            debugTrace
            """);
        Verify(timespan == convertedResult.Duration);
    }

    public void ToUnitTestResultsForTestResultShouldSetStandardOut()
    {
        var result = new TestResult
        {
            LogOutput = "logOutput",
        };

        var convertedResult = result.ToTestResult(new(), default, default, string.Empty, new());

        Verify(convertedResult.Messages.Single(m => m.Category == VSTestTestResultMessage.StandardOutCategory).Text == "logOutput");
    }

    public void ToUnitTestResultsForTestResultShouldSetStandardError()
    {
        var result = new TestResult
        {
            LogError = "logError",
        };

        var convertedResult = result.ToTestResult(new(), default, default, string.Empty, new());

        Verify(convertedResult.Messages.Single(m => m.Category == VSTestTestResultMessage.StandardErrorCategory).Text == "logError");
    }

    public void ToUnitTestResultsForTestResultShouldSetDebugTrace()
    {
        var result = new TestResult
        {
            DebugTrace = "debugTrace",
        };

        var convertedResult = result.ToTestResult(new(), default, default, string.Empty, new());

        Verify(convertedResult.Messages.Single(m => m.Category == VSTestTestResultMessage.StandardOutCategory).Text == """


            Debug Trace:
            debugTrace
            """);
    }

    public void ToUnitTestResultsForTestResultShouldSetTestContextMessages()
    {
        var result = new TestResult
        {
            TestContextMessages = "Context",
        };

        var convertedResult = result.ToTestResult(new(), default, default, string.Empty, new());

        Verify(convertedResult.Messages.Single(m => m.Category == VSTestTestResultMessage.StandardOutCategory).Text == """


            TestContext Messages:
            Context
            """);
    }

    public void ToUnitTestResultsForTestResultShouldSetDuration()
    {
        var timespan = default(TimeSpan);
        var result = new TestResult
        {
            Duration = timespan,
        };

        var convertedResult = result.ToTestResult(new(), default, default, string.Empty, new());

        Verify(timespan == convertedResult.Duration);
    }

    public void ToUnitTestResultsForTestResultShouldSetDisplayName()
    {
        var result = new TestResult
        {
            DisplayName = "displayName",
        };

        var convertedResult = result.ToTestResult(new(), default, default, string.Empty, new());

        Verify(convertedResult.DisplayName == "displayName");
    }

    public void ToUnitTestResultsForTestResultShouldSetDataRowIndex()
    {
        var result = new TestResult
        {
            DatarowIndex = 1,
        };

        var convertedResult = result.ToTestResult(new(), default, default, string.Empty, new());

        Verify(convertedResult.DisplayName == " (Data Row 1)");
    }

    public void ToUnitTestResultsForTestResultShouldSetParentInfo()
    {
        var executionId = Guid.NewGuid();
        var parentExecId = Guid.NewGuid();
        int innerResultsCount = 5;

        var result = new TestResult
        {
            ExecutionId = executionId,
            ParentExecId = parentExecId,
            InnerResultsCount = innerResultsCount,
        };

        var convertedResult = result.ToTestResult(new(), default, default, string.Empty, new());

        Verify(executionId == (Guid)convertedResult.GetPropertyValue(EngineConstants.ExecutionIdProperty)!);
        Verify(parentExecId == (Guid)convertedResult.GetPropertyValue(EngineConstants.ParentExecIdProperty)!);
        Verify(innerResultsCount == (int)convertedResult.GetPropertyValue(EngineConstants.InnerResultsCountProperty)!);
    }

    public void ToUnitTestResultsShouldHaveResultsFileProvidedToTestResult()
    {
        // NOTE: TextContextImplementation.AddResultFile calls Path.GetFullPath.
        // Otherwise, ToTestResult will crash because it calls new Uri on the result file.
        string resultFile = Path.GetFullPath("DummyFile.txt");

        var result = new TestResult { ResultFiles = [resultFile] };
        var convertedResult = result.ToTestResult(new(), default, default, string.Empty, new());
        Verify(convertedResult.Attachments[0].Attachments[0].Description == resultFile);
    }
}
