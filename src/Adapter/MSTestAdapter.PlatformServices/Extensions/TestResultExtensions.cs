// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Helpers;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Extensions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using UTF = Microsoft.VisualStudio.TestTools.UnitTesting;

using VSTestAttachmentSet = Microsoft.VisualStudio.TestPlatform.ObjectModel.AttachmentSet;
using VSTestTestCase = Microsoft.VisualStudio.TestPlatform.ObjectModel.TestCase;
using VSTestTestResult = Microsoft.VisualStudio.TestPlatform.ObjectModel.TestResult;
using VSTestTestResultMessage = Microsoft.VisualStudio.TestPlatform.ObjectModel.TestResultMessage;
using VSTestUriDataAttachment = Microsoft.VisualStudio.TestPlatform.ObjectModel.UriDataAttachment;

namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Extensions;

/// <summary>
/// Extension methods for TestResult.
/// </summary>
#if NET6_0_OR_GREATER
[Obsolete(FrameworkConstants.PublicTypeObsoleteMessage, DiagnosticId = "MSTESTOBS")]
#else
[Obsolete(FrameworkConstants.PublicTypeObsoleteMessage)]
#endif
public static class TestResultExtensions
{
    /// <summary>
    /// Convert parameter unitTestResult to testResult.
    /// </summary>
    /// <param name="frameworkTestResult"> The framework result to be converted to VSTest test result.</param>
    /// <param name="testCase"> The test Case. </param>
    /// <param name="startTime"> The start Time. </param>
    /// <param name="endTime"> The end Time. </param>
    /// <param name="computerName">The computer name.</param>
    /// <param name="currentSettings">Current MSTest settings.</param>
    internal static VSTestTestResult ToTestResult(this TestResult frameworkTestResult, VSTestTestCase testCase, DateTimeOffset startTime, DateTimeOffset endTime, string computerName, MSTestSettings currentSettings)
    {
        DebugEx.Assert(testCase != null, "testCase");

        var testResult = new VSTestTestResult(testCase)
        {
            DisplayName = frameworkTestResult.DisplayName,
            Duration = frameworkTestResult.Duration,
            ErrorMessage = frameworkTestResult.ExceptionMessage ?? frameworkTestResult.IgnoreReason,
            ErrorStackTrace = frameworkTestResult.ExceptionStackTrace,
            Outcome = UnitTestOutcomeHelper.ToTestOutcome(frameworkTestResult.Outcome, currentSettings),
            StartTime = startTime,
            EndTime = endTime,
            ComputerName = computerName,
        };

        testResult.SetPropertyValue(EngineConstants.ExecutionIdProperty, frameworkTestResult.ExecutionId);
        testResult.SetPropertyValue(EngineConstants.ParentExecIdProperty, frameworkTestResult.ParentExecId);
        testResult.SetPropertyValue(EngineConstants.InnerResultsCountProperty, frameworkTestResult.InnerResultsCount);

        if (!StringEx.IsNullOrEmpty(frameworkTestResult.LogOutput))
        {
            VSTestTestResultMessage message = new(VSTestTestResultMessage.StandardOutCategory, frameworkTestResult.LogOutput);
            testResult.Messages.Add(message);
        }

        if (!StringEx.IsNullOrEmpty(frameworkTestResult.LogError))
        {
            VSTestTestResultMessage message = new(VSTestTestResultMessage.StandardErrorCategory, frameworkTestResult.LogError);
            testResult.Messages.Add(message);
        }

        if (!StringEx.IsNullOrEmpty(frameworkTestResult.DebugTrace))
        {
            string debugTraceMessagesInStdOut =
                $"""


                 {Resource.DebugTraceBanner}
                 {frameworkTestResult.DebugTrace}
                 """;
            VSTestTestResultMessage debugTraceMessage = new(VSTestTestResultMessage.StandardOutCategory, debugTraceMessagesInStdOut);
            testResult.Messages.Add(debugTraceMessage);
        }

        if (!StringEx.IsNullOrEmpty(frameworkTestResult.TestContextMessages))
        {
            string testContextMessagesInStdOut =
                $"""


                 {Resource.TestContextMessageBanner}
                 {frameworkTestResult.TestContextMessages}
                 """;
            VSTestTestResultMessage testContextMessage = new(VSTestTestResultMessage.StandardOutCategory, testContextMessagesInStdOut);
            testResult.Messages.Add(testContextMessage);
        }

        if (frameworkTestResult.ResultFiles is { Count: > 0 })
        {
            VSTestAttachmentSet attachmentSet = new(EngineConstants.ExecutorUri, Resource.AttachmentSetDisplayName);
            foreach (string resultFile in frameworkTestResult.ResultFiles)
            {
                string pathToResultFile = PlatformServiceProvider.Instance.FileOperations.GetFullFilePath(resultFile);
                VSTestUriDataAttachment attachment = new(new Uri(pathToResultFile), resultFile);
                attachmentSet.Attachments.Add(attachment);
            }

            testResult.Attachments.Add(attachmentSet);
        }

        if (frameworkTestResult.DatarowIndex >= 0)
        {
            testResult.DisplayName = string.Format(CultureInfo.CurrentCulture, Resource.DataDrivenResultDisplayName, testCase.DisplayName, frameworkTestResult.DatarowIndex);
        }

        return testResult;
    }

    /// <summary>
    /// Converts the test framework's TestResult objects array to a serializable UnitTestResult objects array.
    /// </summary>
    /// <param name="testResults">The test framework's TestResult object array.</param>
    /// <returns>The serializable UnitTestResult object array.</returns>
    public static UnitTestResult[] ToUnitTestResults(this TestResult[] testResults)
    {
        var unitTestResults = new UnitTestResult[testResults.Length];

        int i = 0;
        foreach (TestResult testResult in testResults)
        {
            UTF.UnitTestOutcome outcome = testResult.Outcome;

            UnitTestResult unitTestResult = testResult.TestFailureException is { } testFailureException
                ? new UnitTestResult(
                    new TestFailedException(
                        outcome,
                        testFailureException.TryGetMessage(),
                        testFailureException is TestFailedException testException
                            ? testException.StackTraceInformation
                            : testFailureException.TryGetStackTraceInformation()))
                : new UnitTestResult { Outcome = outcome.ToUnitTestOutcome() };

            if (testResult.IgnoreReason is not null)
            {
                unitTestResult.ErrorMessage = testResult.IgnoreReason;
            }

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
