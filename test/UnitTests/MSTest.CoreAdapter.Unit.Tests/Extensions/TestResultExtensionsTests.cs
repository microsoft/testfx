// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Extensions
{
    extern alias FrameworkV1;
    extern alias FrameworkV2;
    extern alias FrameworkV2CoreExtension;

    using System;
    using System.Collections.Generic;
    using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Extensions;
    using AdapterTestOutcome = Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel.UnitTestOutcome;
    using Assert = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.Assert;
    using TestClass = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.TestClassAttribute;
    using TestMethod = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.TestMethodAttribute;
    using UTF = FrameworkV2::Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class TestResultExtensionsTests
    {
        [TestMethod]
        public void ToUnitTestResultsForPassedTestResultsConvertsToPassedUnitTestResults()
        {
            var results = new[] { new UTF.TestResult() { Outcome = UTF.UnitTestOutcome.Passed } };
            var convertedResults = results.ToUnitTestResults();

            Assert.AreEqual(AdapterTestOutcome.Passed, convertedResults[0].Outcome);
        }

        [TestMethod]
        public void ToUnitTestResultsForFailedTestResultsConvertsToFailedUnitTestResults()
        {
            var results = new[] { new UTF.TestResult() { Outcome = UTF.UnitTestOutcome.Failed } };
            var convertedResults = results.ToUnitTestResults();

            Assert.AreEqual(AdapterTestOutcome.Failed, convertedResults[0].Outcome);
        }

        [TestMethod]
        public void ToUnitTestResultsForInProgressTestResultsConvertsToInProgressUnitTestResults()
        {
            var results = new[] { new UTF.TestResult() { Outcome = UTF.UnitTestOutcome.InProgress } };
            var convertedResults = results.ToUnitTestResults();

            Assert.AreEqual(AdapterTestOutcome.InProgress, convertedResults[0].Outcome);
        }

        [TestMethod]
        public void ToUnitTestResultsForInconclusiveTestResultsConvertsToInconclusiveUnitTestResults()
        {
            var results = new[] { new UTF.TestResult() { Outcome = UTF.UnitTestOutcome.Inconclusive } };
            var convertedResults = results.ToUnitTestResults();

            Assert.AreEqual(AdapterTestOutcome.Inconclusive, convertedResults[0].Outcome);
        }

        [TestMethod]
        public void ToUnitTestResultsForTimeoutTestResultsConvertsToTimeoutUnitTestResults()
        {
            var results = new[] { new UTF.TestResult() { Outcome = UTF.UnitTestOutcome.Timeout } };
            var convertedResults = results.ToUnitTestResults();

            Assert.AreEqual(AdapterTestOutcome.Timeout, convertedResults[0].Outcome);
        }

        [TestMethod]
        public void ToUnitTestResultsForUnknownTestResultsConvertsToErrorUnitTestResults()
        {
            var results = new[] { new UTF.TestResult() { Outcome = UTF.UnitTestOutcome.Unknown } };
            var convertedResults = results.ToUnitTestResults();

            Assert.AreEqual(AdapterTestOutcome.Error, convertedResults[0].Outcome);
        }

        [TestMethod]
        public void ToUnitTestResultsForTestResultWithExceptionConvertsToUnitTestResultsWithFailureOutcome()
        {
            var results = new[] { new UTF.TestResult() { TestFailureException = new Exception() } };
            var convertedResults = results.ToUnitTestResults();

            Assert.AreEqual(AdapterTestOutcome.Failed, convertedResults[0].Outcome);
        }

        [TestMethod]
        public void ToUnitTestResultsForTestResultWithExceptionConvertsToUnitTestResultsWithInconclusiveOutcome()
        {
            var results = new[] { new UTF.TestResult() { TestFailureException = new Exception(), Outcome = UTF.UnitTestOutcome.Inconclusive } };
            var convertedResults = results.ToUnitTestResults();

            Assert.AreEqual(AdapterTestOutcome.Inconclusive, convertedResults[0].Outcome);
        }

        [TestMethod]
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

            Assert.AreEqual("logOutput", convertedResults[0].StandardOut);
            Assert.AreEqual("logError", convertedResults[0].StandardError);
            Assert.AreEqual("displayName", convertedResults[0].DisplayName);
            Assert.AreEqual("debugTrace", convertedResults[0].DebugTrace);
            Assert.AreEqual(timespan, convertedResults[0].Duration);
            Assert.AreEqual(1, convertedResults[0].DatarowIndex);
        }

        [TestMethod]
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

            Assert.AreEqual("logOutput", convertedResults[0].StandardOut);
        }

        [TestMethod]
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

            Assert.AreEqual("logError", convertedResults[0].StandardError);
        }

        [TestMethod]
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

            Assert.AreEqual("debugTrace", convertedResults[0].DebugTrace);
        }

        [TestMethod]
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

            Assert.AreEqual("Context", convertedResults[0].TestContextMessages);
        }

        [TestMethod]
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

            Assert.AreEqual(timespan, convertedResults[0].Duration);
        }

        [TestMethod]
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

            Assert.AreEqual("displayName", convertedResults[0].DisplayName);
        }

        [TestMethod]
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

            Assert.AreEqual(1, convertedResults[0].DatarowIndex);
        }

        [TestMethod]
        public void ToUnitTestResultsShouldHaveResultsFileProvidedToTestResult()
        {
            var results = new[] { new UTF.TestResult() { ResultFiles = new List<string>() { "DummyFile.txt" } } };
            var convertedResults = results.ToUnitTestResults();
            Assert.AreEqual("DummyFile.txt", convertedResults[0].ResultFiles[0]);
        }
    }
}
