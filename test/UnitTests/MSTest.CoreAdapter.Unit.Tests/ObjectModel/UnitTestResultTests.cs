// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.ObjectModel
{
    extern alias FrameworkV1;

    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter;
    using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Helpers;
    using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;
    using Microsoft.VisualStudio.TestPlatform.ObjectModel;

    using Assert = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.Assert;
    using TestClass = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.TestClassAttribute;
    using TestInitialize = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.TestInitializeAttribute;
    using TestMethod = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.TestMethodAttribute;
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

            string runSettingxml =
            @"<RunSettings>
                    <MSTestV2>
                    </MSTestV2>
                  </RunSettings>";

            var adapterSettings = MSTestSettings.GetSettings(runSettingxml, MSTestSettings.SettingsNameAlias);

            // Act
            var testResult = result.ToTestResult(testCase, startTime, endTime, adapterSettings);

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

            string runSettingxml =
            @"<RunSettings>
                    <MSTestV2>
                    </MSTestV2>
                  </RunSettings>";

            var adapterSettings = MSTestSettings.GetSettings(runSettingxml, MSTestSettings.SettingsNameAlias);

            var testresult = result.ToTestResult(testCase, DateTimeOffset.Now, DateTimeOffset.Now, adapterSettings);
            Assert.IsTrue(testresult.Messages.All(m => m.Text.Contains("DummyOutput") && m.Category.Equals("StdOutMsgs")));
        }

        [TestMethod]
        public void ToTestResultForUniTestResultWithStandardErrorShouldReturnTestResultWithStdErrorMessage()
        {
            UnitTestResult result = new UnitTestResult()
            {
                StandardError = "DummyError"
            };
            TestCase testCase = new TestCase("Foo", new Uri("Uri", UriKind.Relative), Assembly.GetExecutingAssembly().FullName);

            string runSettingxml =
            @"<RunSettings>
                    <MSTestV2>
                    </MSTestV2>
                  </RunSettings>";

            var adapterSettings = MSTestSettings.GetSettings(runSettingxml, MSTestSettings.SettingsNameAlias);

            var testresult = result.ToTestResult(testCase, DateTimeOffset.Now, DateTimeOffset.Now, adapterSettings);
            Assert.IsTrue(testresult.Messages.All(m => m.Text.Contains("DummyError") && m.Category.Equals("StdErrMsgs")));
        }

        [TestMethod]
        public void ToTestResultForUniTestResultWithDebugTraceShouldReturnTestResultWithDebugTraceStdOutMessage()
        {
            UnitTestResult result = new UnitTestResult()
            {
                DebugTrace = "DummyDebugTrace"
            };
            TestCase testCase = new TestCase("Foo", new Uri("Uri", UriKind.Relative), Assembly.GetExecutingAssembly().FullName);

            string runSettingxml =
            @"<RunSettings>
                    <MSTestV2>
                    </MSTestV2>
                  </RunSettings>";

            var adapterSettings = MSTestSettings.GetSettings(runSettingxml, MSTestSettings.SettingsNameAlias);

            var testresult = result.ToTestResult(testCase, DateTimeOffset.Now, DateTimeOffset.Now, adapterSettings);
            Assert.IsTrue(testresult.Messages.All(m => m.Text.Contains("\n\nDebug Trace:\nDummyDebugTrace") && m.Category.Equals("StdOutMsgs")));
        }

        [TestMethod]
        public void ToTestResultForUniTestResultWithTestContextMessagesShouldReturnTestResultWithTestContextStdOutMessage()
        {
            UnitTestResult result = new UnitTestResult()
            {
                TestContextMessages = "KeepMovingForward"
            };
            TestCase testCase = new TestCase("Foo", new Uri("Uri", UriKind.Relative), Assembly.GetExecutingAssembly().FullName);

            string runSettingxml =
            @"<RunSettings>
                    <MSTestV2>
                    </MSTestV2>
                  </RunSettings>";

            var adapterSettings = MSTestSettings.GetSettings(runSettingxml, MSTestSettings.SettingsNameAlias);

            var testresult = result.ToTestResult(testCase, DateTimeOffset.Now, DateTimeOffset.Now, adapterSettings);
            Assert.IsTrue(testresult.Messages.All(m => m.Text.Contains("\n\nTestContext Messages:\nKeepMovingForward") && m.Category.Equals("StdOutMsgs")));
        }

        [TestMethod]
        public void ToTestResultForUniTestResultWithResultFilesShouldReturnTestResultWithResultFilesAttachment()
        {
            UnitTestResult result = new UnitTestResult()
            {
                ResultFiles = new List<string>() { "dummy://DummyFile.txt" }
            };
            TestCase testCase = new TestCase("Foo", new Uri("Uri", UriKind.Relative), Assembly.GetExecutingAssembly().FullName);

            string runSettingxml =
            @"<RunSettings>
                    <MSTestV2>
                    </MSTestV2>
                  </RunSettings>";

            var adapterSettings = MSTestSettings.GetSettings(runSettingxml, MSTestSettings.SettingsNameAlias);

            var testresult = result.ToTestResult(testCase, DateTimeOffset.Now, DateTimeOffset.Now, adapterSettings);

            Assert.AreEqual(1, testresult.Attachments.Count);
            Assert.AreEqual("dummy://DummyFile.txt", testresult.Attachments[0].Attachments[0].Description);
        }

        [TestMethod]
        public void ToTestResultForUniTestResultWithNoResultFilesShouldReturnTestResultWithNoResultFilesAttachment()
        {
            UnitTestResult result = new UnitTestResult()
            {
                ResultFiles = null
            };
            TestCase testCase = new TestCase("Foo", new Uri("Uri", UriKind.Relative), Assembly.GetExecutingAssembly().FullName);

            string runSettingxml =
            @"<RunSettings>
                    <MSTestV2>
                    </MSTestV2>
                  </RunSettings>";

            var adapterSettings = MSTestSettings.GetSettings(runSettingxml, MSTestSettings.SettingsNameAlias);

            var testresult = result.ToTestResult(testCase, DateTimeOffset.Now, DateTimeOffset.Now, adapterSettings);

            Assert.AreEqual(0, testresult.Attachments.Count);
        }

        [TestMethod]
        public void ToTestResultForUniTestResultWithParentInfoShouldReturnTestResultWithParentInfo()
        {
            var executionId = Guid.NewGuid();
            var parentExecId = Guid.NewGuid();
            var innerResultsCount = 5;

            UnitTestResult result = new UnitTestResult()
            {
                ExecutionId = executionId,
                ParentExecId = parentExecId,
                InnerResultsCount = innerResultsCount
            };
            TestCase testCase = new TestCase("Foo", new Uri("Uri", UriKind.Relative), Assembly.GetExecutingAssembly().FullName);

            string runSettingxml =
            @"<RunSettings>
                    <MSTestV2>
                    </MSTestV2>
                  </RunSettings>";

            var adapterSettings = MSTestSettings.GetSettings(runSettingxml, MSTestSettings.SettingsNameAlias);

            var testresult = result.ToTestResult(testCase, DateTimeOffset.Now, DateTimeOffset.Now, adapterSettings);

            Assert.AreEqual(executionId, testresult.GetPropertyValue(MSTest.TestAdapter.Constants.ExecutionIdProperty));
            Assert.AreEqual(parentExecId, testresult.GetPropertyValue(MSTest.TestAdapter.Constants.ParentExecIdProperty));
            Assert.AreEqual(innerResultsCount, testresult.GetPropertyValue(MSTest.TestAdapter.Constants.InnerResultsCountProperty));
        }

        [TestMethod]
        public void UniTestHelperToTestOutcomeForUnitTestOutcomePassedShouldReturnTestOutcomePassed()
        {
            string runSettingxml =
            @"<RunSettings>
                    <MSTestV2>
                    </MSTestV2>
                  </RunSettings>";

            var adapterSettings = MSTestSettings.GetSettings(runSettingxml, MSTestSettings.SettingsNameAlias);

            var resultOutcome = UnitTestOutcomeHelper.ToTestOutcome(UnitTestOutcome.Passed, adapterSettings);
            Assert.AreEqual(TestOutcome.Passed, resultOutcome);
        }

        [TestMethod]
        public void UniTestHelperToTestOutcomeForUnitTestOutcomeFailedShouldReturnTestOutcomeFailed()
        {
            string runSettingxml =
            @"<RunSettings>
                    <MSTestV2>
                    </MSTestV2>
                  </RunSettings>";

            var adapterSettings = MSTestSettings.GetSettings(runSettingxml, MSTestSettings.SettingsNameAlias);

            var resultOutcome = UnitTestOutcomeHelper.ToTestOutcome(UnitTestOutcome.Failed, adapterSettings);
            Assert.AreEqual(TestOutcome.Failed, resultOutcome);
        }

        [TestMethod]
        public void UniTestHelperToTestOutcomeForUnitTestOutcomeErrorShouldReturnTestOutcomeFailed()
        {
            string runSettingxml =
            @"<RunSettings>
                    <MSTestV2>
                    </MSTestV2>
                  </RunSettings>";

            var adapterSettings = MSTestSettings.GetSettings(runSettingxml, MSTestSettings.SettingsNameAlias);

            var resultOutcome = UnitTestOutcomeHelper.ToTestOutcome(UnitTestOutcome.Error, adapterSettings);
            Assert.AreEqual(TestOutcome.Failed, resultOutcome);
        }

        [TestMethod]
        public void UnitTestHelperToTestOutcomeForUnitTestOutcomeNotRunnableShouldReturnTestOutComeNoneWhenSpecifiedInAdapterSettings()
        {
            string runSettingxml =
            @"<RunSettings>
                    <MSTestV2>
                        <MapNotRunnableToFailed>false</MapNotRunnableToFailed>
                    </MSTestV2>
                  </RunSettings>";

            var adapterSettings = MSTestSettings.GetSettings(runSettingxml, MSTestSettings.SettingsNameAlias);

            var resultOutcome = UnitTestOutcomeHelper.ToTestOutcome(UnitTestOutcome.NotRunnable, adapterSettings);
            Assert.AreEqual(TestOutcome.None, resultOutcome);
        }

        [TestMethod]
        public void UnitTestHelperToTestOutcomeForUnitTestOutcomeNotRunnableShouldReturnTestOutcomeFailedByDefault()
        {
            string runSettingxml =
            @"<RunSettings>
                    <MSTestV2>
                    </MSTestV2>
                  </RunSettings>";

            var adapterSettings = MSTestSettings.GetSettings(runSettingxml, MSTestSettings.SettingsNameAlias);

            var resultOutcome = UnitTestOutcomeHelper.ToTestOutcome(UnitTestOutcome.NotRunnable, adapterSettings);
            Assert.AreEqual(TestOutcome.Failed, resultOutcome);
        }

        [TestMethod]
        public void UniTestHelperToTestOutcomeForUnitTestOutcomeTimeoutShouldReturnTestOutcomeFailed()
        {
            string runSettingxml =
            @"<RunSettings>
                    <MSTestV2>
                    </MSTestV2>
                  </RunSettings>";

            var adapterSettings = MSTestSettings.GetSettings(runSettingxml, MSTestSettings.SettingsNameAlias);

            var resultOutcome = UnitTestOutcomeHelper.ToTestOutcome(UnitTestOutcome.Timeout, adapterSettings);
            Assert.AreEqual(TestOutcome.Failed, resultOutcome);
        }

        [TestMethod]
        public void UniTestHelperToTestOutcomeForUnitTestOutcomeIgnoredShouldReturnTestOutcomeSkipped()
        {
            string runSettingxml =
            @"<RunSettings>
                    <MSTestV2>
                    </MSTestV2>
                  </RunSettings>";

            var adapterSettings = MSTestSettings.GetSettings(runSettingxml, MSTestSettings.SettingsNameAlias);

            var resultOutcome = UnitTestOutcomeHelper.ToTestOutcome(UnitTestOutcome.Ignored, adapterSettings);
            Assert.AreEqual(TestOutcome.Skipped, resultOutcome);
        }

        [TestMethod]
        public void UniTestHelperToTestOutcomeForUnitTestOutcomeInconclusiveShouldReturnTestOutcomeSkipped()
        {
            string runSettingxml =
            @"<RunSettings>
                    <MSTestV2>
                    </MSTestV2>
                  </RunSettings>";

            var adapterSettings = MSTestSettings.GetSettings(runSettingxml, MSTestSettings.SettingsNameAlias);

            var resultOutcome = UnitTestOutcomeHelper.ToTestOutcome(UnitTestOutcome.Inconclusive, adapterSettings);
            Assert.AreEqual(TestOutcome.Skipped, resultOutcome);
        }

        [TestMethod]
        public void UniTestHelperToTestOutcomeForUnitTestOutcomeInconclusiveShouldReturnTestOutcomeFailedWhenSpecifiedSo()
        {
            string runSettingxml =
            @"<RunSettings>
                    <MSTestV2>
                            <MapInconclusiveToFailed>true</MapInconclusiveToFailed>
                    </MSTestV2>
                  </RunSettings>";

            var adapterSettings = MSTestSettings.GetSettings(runSettingxml, MSTestSettings.SettingsNameAlias);

            var resultOutcome = UnitTestOutcomeHelper.ToTestOutcome(UnitTestOutcome.Inconclusive, adapterSettings);
            Assert.AreEqual(TestOutcome.Failed, resultOutcome);
        }

        [TestMethod]
        public void UniTestHelperToTestOutcomeForUnitTestOutcomeNotFoundShouldReturnTestOutcomeNotFound()
        {
            string runSettingxml =
            @"<RunSettings>
                    <MSTestV2>
                    </MSTestV2>
                  </RunSettings>";

            var adapterSettings = MSTestSettings.GetSettings(runSettingxml, MSTestSettings.SettingsNameAlias);

            var resultOutcome = UnitTestOutcomeHelper.ToTestOutcome(UnitTestOutcome.NotFound, adapterSettings);
            Assert.AreEqual(TestOutcome.NotFound, resultOutcome);
        }

        [TestMethod]
        public void UniTestHelperToTestOutcomeForUnitTestOutcomeInProgressShouldReturnTestOutcomeNone()
        {
            string runSettingxml =
            @"<RunSettings>
                    <MSTestV2>
                    </MSTestV2>
                  </RunSettings>";

            var adapterSettings = MSTestSettings.GetSettings(runSettingxml, MSTestSettings.SettingsNameAlias);

            var resultOutcome = UnitTestOutcomeHelper.ToTestOutcome(UnitTestOutcome.InProgress, adapterSettings);
            Assert.AreEqual(TestOutcome.None, resultOutcome);
        }

        [TestMethod]
        public void UniTestHelperToTestOutcomeForUnitTestOutcomeNotRunnableShouldReturnTestOutcomeFailedWhenSpecifiedSo()
        {
            string runSettingxml =
            @"<RunSettings>
                    <MSTestV2>
                            <MapNotRunnableToFailed>true</MapNotRunnableToFailed>
                    </MSTestV2>
                  </RunSettings>";

            var adapterSettings = MSTestSettings.GetSettings(runSettingxml, MSTestSettings.SettingsNameAlias);
            var resultOutcome = UnitTestOutcomeHelper.ToTestOutcome(UnitTestOutcome.NotRunnable, adapterSettings);
            Assert.AreEqual(TestOutcome.Failed, resultOutcome);
        }
    }
}
