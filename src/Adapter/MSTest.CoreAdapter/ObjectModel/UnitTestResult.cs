// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Globalization;
    
    using Microsoft.VisualStudio.TestPlatform.ObjectModel;

    using Constants = Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Constants;

    [Serializable]
    public class UnitTestResult
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="UnitTestResult"/> class.
        /// </summary>
        internal UnitTestResult()
        {
            this.DatarowIndex = -1;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="UnitTestResult"/> class.
        /// </summary>
        /// <param name="testFailedException"> The test failed exception. </param>
        internal UnitTestResult(TestFailedException testFailedException) 
            : this()
        {
            this.Outcome = testFailedException.Outcome;
            this.ErrorMessage = testFailedException.Message;

            if (testFailedException.StackTraceInformation != null)
            {
                this.ErrorStackTrace = testFailedException.StackTraceInformation.ErrorStackTrace;
                this.ErrorLineNumber = testFailedException.StackTraceInformation.ErrorLineNumber;
                this.ErrorFilePath = testFailedException.StackTraceInformation.ErrorFilePath;
                this.ErrorColumnNumber = testFailedException.StackTraceInformation.ErrorColumnNumber; 
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="UnitTestResult"/> class.
        /// </summary>
        /// <param name="outcome"> The outcome. </param>
        /// <param name="errorMessage"> The error message. </param>
        internal UnitTestResult(UnitTestOutcome outcome, string errorMessage) 
            : this()
        {
            this.Outcome = outcome;
            this.ErrorMessage = errorMessage;
        }

        /// <summary>
        /// Gets the display name for the result
        /// </summary>
        public string DisplayName { get; internal set; }

        /// <summary>
        /// Gets the outcome of the result
        /// </summary>
        public UnitTestOutcome Outcome { get; internal set; }

        /// <summary>
        /// Gets the errorMessage of the result
        /// </summary>
        public string ErrorMessage { get; internal set; }

        /// <summary>
        /// Gets the stackTrace of the result
        /// </summary>
        public string ErrorStackTrace { get; internal set; }

        /// <summary>
        /// Gets the duration of the result
        /// </summary>
        public TimeSpan Duration { get; internal set; }

        /// <summary>
        /// Gets the standard output of the result
        /// </summary>
        public string StandardOut { get; internal set; }

        /// <summary>
        /// Gets the Standard Error of the result
        /// </summary>
        public string StandardError { get; internal set; }

        /// <summary>
        /// Gets the debug trace of the result 
        /// </summary>
        public string DebugTrace { get; internal set; }

        /// <summary>
        /// Gets the source code FilePath where the error was thrown.
        /// </summary>
        public string ErrorFilePath { get; internal set; }

        /// <summary>
        /// Gets the line number in the source code file where the error was thrown.
        /// </summary>
        public int ErrorLineNumber { get; private set; }

        /// <summary>
        /// Gets the column number in the source code file where the error was thrown.
        /// </summary>
        public int ErrorColumnNumber { get; private set; }

        /// <summary>
        /// Data row index in data source. Set only for results of individual 
        /// run of data row of a data driven test.
        /// </summary>
        public int DatarowIndex { get; internal set; }

        /// <summary>
        /// Gets the result files attached by the test. 
        /// </summary>
        public IList<string> ResultFiles { get; internal set; }

        /// <summary>
        /// Convert parameter unitTestResult to testResult
        /// </summary>
        /// <param name="testCase"> The test Case.  </param>
        /// <param name="startTime"> The start Time.  </param>
        /// <param name="endTime"> The end Time.  </param>
        /// <returns> The <see cref="TestResult"/>. </returns>
        internal TestResult ToTestResult(TestCase testCase, DateTimeOffset startTime, DateTimeOffset endTime, bool mapInconclusiveToFailed)
        {
            Debug.Assert(testCase != null, "testCase");

            var testResult = new TestResult(testCase)
                                        {
                                            DisplayName = this.DisplayName,
                                            Duration = this.Duration,
                                            ErrorMessage = this.ErrorMessage,
                                            ErrorStackTrace = this.ErrorStackTrace,
                                            Outcome = UnitTestOutcomeHelper.ToTestOutcome(this.Outcome, mapInconclusiveToFailed),
                                            StartTime = startTime,
                                            EndTime = endTime                                   
            };

            if (!string.IsNullOrEmpty(this.StandardOut))
            {
                TestResultMessage message = new TestResultMessage(TestResultMessage.StandardOutCategory, this.StandardOut);
                testResult.Messages.Add(message);
            }
            if (!string.IsNullOrEmpty(this.StandardError))
            {
                TestResultMessage message = new TestResultMessage(TestResultMessage.StandardErrorCategory, this.StandardError);
                testResult.Messages.Add(message);
            }

            if (!string.IsNullOrEmpty(this.DebugTrace))
            {
                string debugTraceMessagesinStdOut = String.Format(CultureInfo.InvariantCulture, "\n\n{0}\n{1}", Resource.DebugTraceBanner, this.DebugTrace);
                TestResultMessage debugTraceMessage = new TestResultMessage(TestResultMessage.StandardOutCategory, debugTraceMessagesinStdOut);
                testResult.Messages.Add(debugTraceMessage);
            }

            if (this.ResultFiles != null && this.ResultFiles.Count > 0)
            {
                AttachmentSet attachmentSet = new AttachmentSet(Constants.ExecutorUri, Resource.AttachmentSetDisplayName);
                foreach (var resultFile in this.ResultFiles)
                {
                    string pathToResultFile = PlatformServiceProvider.Instance.FileOperations.GetFullFilePath(resultFile);
                    UriDataAttachment attachment = new UriDataAttachment(new Uri(pathToResultFile), resultFile);
                    attachmentSet.Attachments.Add(attachment);
                }

                testResult.Attachments.Add(attachmentSet);
            }

            return testResult;
        }
    }
    
    /// <summary>
    /// Outcome of a test 
    /// </summary>
    public enum UnitTestOutcome : int
    {
        /// <summary>
        /// There was a system error while we were trying to execute a test.
        /// </summary>
        Error,

        /// <summary>
        /// Test was executed, but there were issues.
        /// Issues may involve exceptions or failed assertions.
        /// </summary>
        Failed,

        /// <summary>
        /// The test timed out
        /// </summary>
        Timeout,

        /// <summary>
        /// Test has completed, but we can't say if it passed or failed.
        /// (Used in Assert.InConclusive scenario)
        /// </summary>
        Inconclusive,

        /// <summary>
        /// Test had it chance for been executed but was not, as Ignore == true.
        /// </summary>
        Ignored,

        /// <summary>
        /// Test cannot be executed. 
        /// </summary>
        NotRunnable,

        /// <summary>
        /// Test was executed w/o any issues.
        /// </summary>
        Passed,

        /// <summary>
        /// The specific test cannot be found.
        /// </summary>
        NotFound,

        /// <summary>
        /// When test is handed over to runner for execution, it goes into progress state. 
        /// It is added so that the right status can be set in TestContext.
        /// </summary>
        InProgress,
    }

    internal static class UnitTestOutcomeHelper
    {
        /// <summary>
        /// Converts the parameter unitTestOutcome to testOutcome
        /// </summary>
        /// <param name="unitTestOutcome"> The unit Test Outcome. </param>
        internal static TestOutcome ToTestOutcome(UnitTestOutcome unitTestOutcome, bool mapInconclusiveToFailed)
        {
            switch (unitTestOutcome)
            {
                case UnitTestOutcome.Passed:
                    return TestOutcome.Passed;

                case UnitTestOutcome.Failed:
                case UnitTestOutcome.Error:
                case UnitTestOutcome.NotRunnable:
                case UnitTestOutcome.Timeout:
                    return TestOutcome.Failed;

                case UnitTestOutcome.Ignored:
                    return TestOutcome.Skipped;
                case UnitTestOutcome.Inconclusive:
                    {
                        if (mapInconclusiveToFailed)
                            return TestOutcome.Failed;
                        return TestOutcome.Skipped;
                    }

                case UnitTestOutcome.NotFound:
                    return TestOutcome.NotFound;

                default:
                    return TestOutcome.None;
            }
        }
    }
}

