// Copyright (c) Microsoft Corporation. All rights reserved.

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.ObjectModel
{
    using System;
    using System.Linq;
    using System.Reflection;
    using System.Collections.Generic;

    using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;
    using Microsoft.VisualStudio.TestPlatform.ObjectModel;
    using Microsoft.VisualStudio.TestPlatform.UnitTestFramework;
    using UnitTestOutcome = Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel.UnitTestOutcome;
    

    [TestClass]
    public class UnitTestResultTests
    {
        [TestMethod]
        public void UnitTestResultConstrutorWithOutcomeAndErrorMessageShouldSetRequiredFields()
        {
            UnitTestResult result = new UnitTestResult(UnitTestOutcome.Error, "DummyMessage");

            Assert.AreEqual(UnitTestOutcome.Error, result.Outcome);
            Assert.AreEqual("DummyMessage", result.ErrorMessage);
        }

        [TestMethod]
        public void UnitTestResultConstrutorWithTestFailedExceptionShouldSetRequiredFields()
        {
            var stackTrace = new StackTraceInformation("trace", "filePath", 2, 3);
            TestFailedException ex = new TestFailedException(UnitTestOutcome.Error, "DummyMessage", stackTrace);

            UnitTestResult result = new UnitTestResult(ex);

            Assert.AreEqual(UnitTestOutcome.Error, result.Outcome);
            Assert.AreEqual("DummyMessage", result.ErrorMessage);
            Assert.AreEqual("trace", result.ErrorStackTrace);
            Assert.AreEqual("filePath", result.ErrorFilePath);
            Assert.AreEqual(2, result.ErrorLineNumber);
            Assert.AreEqual(3, result.ErrorColumnNumber);
        }

        [TestMethod]
        public void ToTestResultShouldReturnConvertedTestResultWithFieldsSet()
        {
            var stackTrace = new StackTraceInformation("DummyStackTrace", "filePath", 2, 3);
            TestFailedException ex = new TestFailedException(UnitTestOutcome.Error, "DummyMessage", stackTrace);
            var dummyTimeSpan = new TimeSpan(20);
            UnitTestResult result = new UnitTestResult(ex)
                                        {
                                            DisplayName = "DummyDisplayName",
                                            Duration = dummyTimeSpan
                                        };
           
            TestCase testCase = new TestCase("Foo", new Uri("Uri", UriKind.Relative), Assembly.GetExecutingAssembly().FullName);
            var startTime = DateTimeOffset.Now;
            var endTime = DateTimeOffset.Now; 
            // Act
            var testResult = result.ToTestResult(testCase, startTime, endTime, false);

            // Validate
            Assert.AreEqual(testCase, testResult.TestCase);
            Assert.AreEqual("DummyDisplayName", testResult.DisplayName);
            Assert.AreEqual(dummyTimeSpan, testResult.Duration);
            Assert.AreEqual(TestOutcome.Failed, testResult.Outcome);
            Assert.AreEqual("DummyMessage", testResult.ErrorMessage);
            Assert.AreEqual("DummyStackTrace", testResult.ErrorStackTrace);
            Assert.AreEqual(startTime, testResult.StartTime);
            Assert.AreEqual(endTime, testResult.EndTime);
            Assert.AreEqual(0, testResult.Messages.Count);
        }

        [TestMethod]
        public void ToTestResultForUniTestResultWithStandardOutShouldReturnTestResultWithStdOutMessage()
        {
            UnitTestResult result = new UnitTestResult()
            {
                StandardOut = "DummyOutput"
            };
            TestCase testCase = new TestCase("Foo", new Uri("Uri", UriKind.Relative), Assembly.GetExecutingAssembly().FullName);
            var testresult = result.ToTestResult(testCase, DateTimeOffset.Now, DateTimeOffset.Now, false);
            Assert.IsTrue(testresult.Messages.All(m => m.Text.Contains("DummyOutput") && m.Category.Equals("StdOutMsgs")));
        }
        
        [TestMethod]
        public void ToTestResultForUniTestResultWithDebugTraceShouldReturnTestResultWithDebugTraceStdOutMessage()
        {
            UnitTestResult result = new UnitTestResult()
            {
                DebugTrace = "DummyDebugTrace"
            };
            TestCase testCase = new TestCase("Foo", new Uri("Uri", UriKind.Relative), Assembly.GetExecutingAssembly().FullName);
            var testresult = result.ToTestResult(testCase, DateTimeOffset.Now, DateTimeOffset.Now, false);
            Assert.IsTrue(testresult.Messages.All(m => m.Text.Contains("\n\nDebug Trace:\nDummyDebugTrace") && m.Category.Equals("StdOutMsgs")));
        }

        [TestMethod]
        public void ToTestResultForUniTestResultWithResultFilesShouldReturnTestResultWithResultFilesAttachment()
        {
            UnitTestResult result = new UnitTestResult()
            {
                ResultFiles = new List<string>() { "dummy://DummyFile.txt" }
            };
            TestCase testCase = new TestCase("Foo", new Uri("Uri", UriKind.Relative), Assembly.GetExecutingAssembly().FullName);
            var testresult = result.ToTestResult(testCase, DateTimeOffset.Now, DateTimeOffset.Now, false);
           
            Assert.AreEqual(testresult.Attachments.Count, 1);
            Assert.AreEqual(testresult.Attachments[0].Attachments[0].Description, "dummy://DummyFile.txt");          
        }

        [TestMethod]
        public void ToTestResultForUniTestResultWithNoResultFilesShouldReturnTestResultWithNoResultFilesAttachment()
        {
            UnitTestResult result = new UnitTestResult()
            {
                ResultFiles = null
            };
            TestCase testCase = new TestCase("Foo", new Uri("Uri", UriKind.Relative), Assembly.GetExecutingAssembly().FullName);
            var testresult = result.ToTestResult(testCase, DateTimeOffset.Now, DateTimeOffset.Now, false);

            Assert.AreEqual(testresult.Attachments.Count, 0);
        }


        [TestMethod]
        public void UniTestHelperToTestOutcomeForUnitTestOutcomePassedShouldReturnTestOutcomePassed()
        {
            var resultOutcome = UnitTestOutcomeHelper.ToTestOutcome(UnitTestOutcome.Passed,false);
            Assert.AreEqual(TestOutcome.Passed, resultOutcome);
        }

        [TestMethod]
        public void UniTestHelperToTestOutcomeForUnitTestOutcomeFailedShouldReturnTestOutcomeFailed()
        {
            var resultOutcome = UnitTestOutcomeHelper.ToTestOutcome(UnitTestOutcome.Failed, false);
            Assert.AreEqual(TestOutcome.Failed, resultOutcome);
        }

        [TestMethod]
        public void UniTestHelperToTestOutcomeForUnitTestOutcomeErrorShouldReturnTestOutcomeFailed()
        {
            var resultOutcome = UnitTestOutcomeHelper.ToTestOutcome(UnitTestOutcome.Error, false);
            Assert.AreEqual(TestOutcome.Failed, resultOutcome);
        }

        [TestMethod]
        public void UniTestHelperToTestOutcomeForUnitTestOutcomeNotRunnableShouldReturnTestOutcomeFailed()
        {
            var resultOutcome = UnitTestOutcomeHelper.ToTestOutcome(UnitTestOutcome.NotRunnable,false);
            Assert.AreEqual(TestOutcome.Failed, resultOutcome);
        }

        [TestMethod]
        public void UniTestHelperToTestOutcomeForUnitTestOutcomeTimeoutShouldReturnTestOutcomeFailed()
        {
            var resultOutcome = UnitTestOutcomeHelper.ToTestOutcome(UnitTestOutcome.Timeout, false);
            Assert.AreEqual(TestOutcome.Failed, resultOutcome);
        }

        [TestMethod]
        public void UniTestHelperToTestOutcomeForUnitTestOutcomeIgnoredShouldReturnTestOutcomeSkipped()
        { 
            var resultOutcome = UnitTestOutcomeHelper.ToTestOutcome(UnitTestOutcome.Ignored, false);
            Assert.AreEqual(TestOutcome.Skipped, resultOutcome);
        }

        [TestMethod]
        public void UniTestHelperToTestOutcomeForUnitTestOutcomeInconclusiveShouldReturnTestOutcomeSkipped()
        {
            var resultOutcome = UnitTestOutcomeHelper.ToTestOutcome(UnitTestOutcome.Inconclusive, false);
            Assert.AreEqual(TestOutcome.Skipped, resultOutcome);
        }

        [TestMethod]
        public void UniTestHelperToTestOutcomeForUnitTestOutcomeInconclusiveShouldReturnTestOutcomeFailedWhenSpecifiedSo()
        {
            var resultOutcome = UnitTestOutcomeHelper.ToTestOutcome(UnitTestOutcome.Inconclusive, true);
            Assert.AreEqual(TestOutcome.Failed, resultOutcome);
        }

        [TestMethod]
        public void UniTestHelperToTestOutcomeForUnitTestOutcomeNotFoundShouldReturnTestOutcomeNotFound()
        {
            var resultOutcome = UnitTestOutcomeHelper.ToTestOutcome(UnitTestOutcome.NotFound, false);
            Assert.AreEqual(TestOutcome.NotFound, resultOutcome);
        }

        [TestMethod]
        public void UniTestHelperToTestOutcomeForUnitTestOutcomeInProgressShouldReturnTestOutcomeNone()
        {
            var resultOutcome = UnitTestOutcomeHelper.ToTestOutcome(UnitTestOutcome.InProgress, false);
            Assert.AreEqual(TestOutcome.None, resultOutcome);
        }


    }
}
