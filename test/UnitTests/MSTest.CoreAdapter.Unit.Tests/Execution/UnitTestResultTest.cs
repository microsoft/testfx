// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Execution
{
    extern alias FrameworkV1;
    extern alias FrameworkV2;
    extern alias FrameworkV2CoreExtension;

    using System;
    using System.Linq;
    using System.Reflection;
    using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter;
    using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;
    using Microsoft.VisualStudio.TestPlatform.ObjectModel;
    using Assert = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.Assert;
    using TestClass = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.TestClassAttribute;
    using TestInitialize = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.TestInitializeAttribute;
    using TestMethod = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.TestMethodAttribute;
    using UnitTestOutcome = Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel.UnitTestOutcome;
    using UTF = FrameworkV2::Microsoft.VisualStudio.TestTools.UnitTesting;

    [TestClass]
    public class UnitTestResultTest
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

            MSTestSettings adapterSettings = MSTestSettings.GetSettings(runSettingxml, MSTestSettings.SettingsNameAlias);

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

            MSTestSettings adapterSettings = MSTestSettings.GetSettings(runSettingxml, MSTestSettings.SettingsNameAlias);

            var testresult = result.ToTestResult(testCase, DateTimeOffset.Now, DateTimeOffset.Now, adapterSettings);
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
            string runSettingxml =
               @"<RunSettings>
                    <MSTestV2>
                    </MSTestV2>
                  </RunSettings>";

            MSTestSettings adapterSettings = MSTestSettings.GetSettings(runSettingxml, MSTestSettings.SettingsNameAlias);
            var testresult = result.ToTestResult(testCase, DateTimeOffset.Now, DateTimeOffset.Now, adapterSettings);
            Assert.IsTrue(testresult.Messages.All(m => m.Text.Contains("\n\nDebug Trace:\nDummyDebugTrace") && m.Category.Equals("StdOutMsgs")));
        }
    }
}
