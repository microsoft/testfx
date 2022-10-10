// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Helpers;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;

using TestFramework.ForTestingMSTest;

using UnitTestOutcome = Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel.UnitTestOutcome;

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.ObjectModel;
public class UnitTestResultTests : TestContainer
{
    public void UnitTestResultConstructorWithOutcomeAndErrorMessageShouldSetRequiredFields()
    {
        UnitTestResult result = new(UnitTestOutcome.Error, "DummyMessage");

        Verify(result.Outcome == UnitTestOutcome.Error);
        Verify(result.ErrorMessage == "DummyMessage");
    }

    public void UnitTestResultConstructorWithTestFailedExceptionShouldSetRequiredFields()
    {
        var stackTrace = new StackTraceInformation("trace", "filePath", 2, 3);
        TestFailedException ex = new(UnitTestOutcome.Error, "DummyMessage", stackTrace);

        UnitTestResult result = new(ex);

        Verify(result.Outcome == UnitTestOutcome.Error);
        Verify(result.ErrorMessage == "DummyMessage");
        Verify(result.ErrorStackTrace == "trace");
        Verify(result.ErrorFilePath == "filePath");
        Verify(result.ErrorLineNumber == 2);
        Verify(result.ErrorColumnNumber == 3);
    }

    public void ToTestResultShouldReturnConvertedTestResultWithFieldsSet()
    {
        var stackTrace = new StackTraceInformation("DummyStackTrace", "filePath", 2, 3);
        TestFailedException ex = new(UnitTestOutcome.Error, "DummyMessage", stackTrace);
        var dummyTimeSpan = new TimeSpan(20);
        UnitTestResult result = new(ex)
        {
            DisplayName = "DummyDisplayName",
            Duration = dummyTimeSpan,
        };

        TestCase testCase = new("Foo", new Uri("Uri", UriKind.Relative), Assembly.GetExecutingAssembly().FullName);
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
        Verify(testCase == testResult.TestCase);
        Verify(testResult.DisplayName == "DummyDisplayName");
        Verify(dummyTimeSpan == testResult.Duration);
        Verify(testResult.Outcome == TestOutcome.Failed);
        Verify(testResult.ErrorMessage == "DummyMessage");
        Verify(testResult.ErrorStackTrace == "DummyStackTrace");
        Verify(startTime == testResult.StartTime);
        Verify(endTime == testResult.EndTime);
        Verify(testResult.Messages.Count == 0);
    }

    public void ToTestResultForUniTestResultWithStandardOutShouldReturnTestResultWithStdOutMessage()
    {
        UnitTestResult result = new()
        {
            StandardOut = "DummyOutput",
        };
        TestCase testCase = new("Foo", new Uri("Uri", UriKind.Relative), Assembly.GetExecutingAssembly().FullName);

        string runSettingxml =
        @"<RunSettings>
                    <MSTestV2>
                    </MSTestV2>
                  </RunSettings>";

        var adapterSettings = MSTestSettings.GetSettings(runSettingxml, MSTestSettings.SettingsNameAlias);

        var testresult = result.ToTestResult(testCase, DateTimeOffset.Now, DateTimeOffset.Now, adapterSettings);
        Verify(testresult.Messages.All(m => m.Text.Contains("DummyOutput") && m.Category.Equals("StdOutMsgs")));
    }

    public void ToTestResultForUniTestResultWithStandardErrorShouldReturnTestResultWithStdErrorMessage()
    {
        UnitTestResult result = new()
        {
            StandardError = "DummyError",
        };
        TestCase testCase = new("Foo", new Uri("Uri", UriKind.Relative), Assembly.GetExecutingAssembly().FullName);

        string runSettingxml =
        @"<RunSettings>
                    <MSTestV2>
                    </MSTestV2>
                  </RunSettings>";

        var adapterSettings = MSTestSettings.GetSettings(runSettingxml, MSTestSettings.SettingsNameAlias);

        var testresult = result.ToTestResult(testCase, DateTimeOffset.Now, DateTimeOffset.Now, adapterSettings);
        Verify(testresult.Messages.All(m => m.Text.Contains("DummyError") && m.Category.Equals("StdErrMsgs")));
    }

    public void ToTestResultForUniTestResultWithDebugTraceShouldReturnTestResultWithDebugTraceStdOutMessage()
    {
        UnitTestResult result = new()
        {
            DebugTrace = "DummyDebugTrace",
        };
        TestCase testCase = new("Foo", new Uri("Uri", UriKind.Relative), Assembly.GetExecutingAssembly().FullName);

        string runSettingxml =
        @"<RunSettings>
                    <MSTestV2>
                    </MSTestV2>
                  </RunSettings>";

        var adapterSettings = MSTestSettings.GetSettings(runSettingxml, MSTestSettings.SettingsNameAlias);

        var testresult = result.ToTestResult(testCase, DateTimeOffset.Now, DateTimeOffset.Now, adapterSettings);
        Verify(testresult.Messages.All(m => m.Text.Contains("\n\nDebug Trace:\nDummyDebugTrace") && m.Category.Equals("StdOutMsgs")));
    }

    public void ToTestResultForUniTestResultWithTestContextMessagesShouldReturnTestResultWithTestContextStdOutMessage()
    {
        UnitTestResult result = new()
        {
            TestContextMessages = "KeepMovingForward",
        };
        TestCase testCase = new("Foo", new Uri("Uri", UriKind.Relative), Assembly.GetExecutingAssembly().FullName);

        string runSettingxml =
        @"<RunSettings>
                    <MSTestV2>
                    </MSTestV2>
                  </RunSettings>";

        var adapterSettings = MSTestSettings.GetSettings(runSettingxml, MSTestSettings.SettingsNameAlias);

        var testresult = result.ToTestResult(testCase, DateTimeOffset.Now, DateTimeOffset.Now, adapterSettings);
        Verify(testresult.Messages.All(m => m.Text.Contains("\n\nTestContext Messages:\nKeepMovingForward") && m.Category.Equals("StdOutMsgs")));
    }

    public void ToTestResultForUniTestResultWithResultFilesShouldReturnTestResultWithResultFilesAttachment()
    {
        UnitTestResult result = new()
        {
            ResultFiles = new List<string>() { "dummy://DummyFile.txt" },
        };
        TestCase testCase = new("Foo", new Uri("Uri", UriKind.Relative), Assembly.GetExecutingAssembly().FullName);

        string runSettingxml =
        @"<RunSettings>
                    <MSTestV2>
                    </MSTestV2>
                  </RunSettings>";

        var adapterSettings = MSTestSettings.GetSettings(runSettingxml, MSTestSettings.SettingsNameAlias);

        var testresult = result.ToTestResult(testCase, DateTimeOffset.Now, DateTimeOffset.Now, adapterSettings);

        Verify(testresult.Attachments.Count == 1);
        Verify(testresult.Attachments[0].Attachments[0].Description == "dummy://DummyFile.txt");
    }

    public void ToTestResultForUniTestResultWithNoResultFilesShouldReturnTestResultWithNoResultFilesAttachment()
    {
        UnitTestResult result = new()
        {
            ResultFiles = null,
        };
        TestCase testCase = new("Foo", new Uri("Uri", UriKind.Relative), Assembly.GetExecutingAssembly().FullName);

        string runSettingxml =
        @"<RunSettings>
                    <MSTestV2>
                    </MSTestV2>
                  </RunSettings>";

        var adapterSettings = MSTestSettings.GetSettings(runSettingxml, MSTestSettings.SettingsNameAlias);

        var testresult = result.ToTestResult(testCase, DateTimeOffset.Now, DateTimeOffset.Now, adapterSettings);

        Verify(testresult.Attachments.Count == 0);
    }

    public void ToTestResultForUniTestResultWithParentInfoShouldReturnTestResultWithParentInfo()
    {
        var executionId = Guid.NewGuid();
        var parentExecId = Guid.NewGuid();
        var innerResultsCount = 5;

        UnitTestResult result = new()
        {
            ExecutionId = executionId,
            ParentExecId = parentExecId,
            InnerResultsCount = innerResultsCount,
        };
        TestCase testCase = new("Foo", new Uri("Uri", UriKind.Relative), Assembly.GetExecutingAssembly().FullName);

        string runSettingxml =
        @"<RunSettings>
                    <MSTestV2>
                    </MSTestV2>
                  </RunSettings>";

        var adapterSettings = MSTestSettings.GetSettings(runSettingxml, MSTestSettings.SettingsNameAlias);

        var testresult = result.ToTestResult(testCase, DateTimeOffset.Now, DateTimeOffset.Now, adapterSettings);

        Verify(executionId.Equals(testresult.GetPropertyValue(MSTest.TestAdapter.Constants.ExecutionIdProperty)));
        Verify(parentExecId.Equals(testresult.GetPropertyValue(MSTest.TestAdapter.Constants.ParentExecIdProperty)));
        Verify(innerResultsCount.Equals(testresult.GetPropertyValue(MSTest.TestAdapter.Constants.InnerResultsCountProperty)));
    }

    public void UniTestHelperToTestOutcomeForUnitTestOutcomePassedShouldReturnTestOutcomePassed()
    {
        string runSettingxml =
        @"<RunSettings>
                    <MSTestV2>
                    </MSTestV2>
                  </RunSettings>";

        var adapterSettings = MSTestSettings.GetSettings(runSettingxml, MSTestSettings.SettingsNameAlias);

        var resultOutcome = UnitTestOutcomeHelper.ToTestOutcome(UnitTestOutcome.Passed, adapterSettings);
        Verify(resultOutcome == TestOutcome.Passed);
    }

    public void UniTestHelperToTestOutcomeForUnitTestOutcomeFailedShouldReturnTestOutcomeFailed()
    {
        string runSettingxml =
        @"<RunSettings>
                    <MSTestV2>
                    </MSTestV2>
                  </RunSettings>";

        var adapterSettings = MSTestSettings.GetSettings(runSettingxml, MSTestSettings.SettingsNameAlias);

        var resultOutcome = UnitTestOutcomeHelper.ToTestOutcome(UnitTestOutcome.Failed, adapterSettings);
        Verify(resultOutcome == TestOutcome.Failed);
    }

    public void UniTestHelperToTestOutcomeForUnitTestOutcomeErrorShouldReturnTestOutcomeFailed()
    {
        string runSettingxml =
        @"<RunSettings>
                    <MSTestV2>
                    </MSTestV2>
                  </RunSettings>";

        var adapterSettings = MSTestSettings.GetSettings(runSettingxml, MSTestSettings.SettingsNameAlias);

        var resultOutcome = UnitTestOutcomeHelper.ToTestOutcome(UnitTestOutcome.Error, adapterSettings);
        Verify(resultOutcome == TestOutcome.Failed);
    }

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
        Verify(resultOutcome == TestOutcome.None);
    }

    public void UnitTestHelperToTestOutcomeForUnitTestOutcomeNotRunnableShouldReturnTestOutcomeFailedByDefault()
    {
        string runSettingxml =
        @"<RunSettings>
                    <MSTestV2>
                    </MSTestV2>
                  </RunSettings>";

        var adapterSettings = MSTestSettings.GetSettings(runSettingxml, MSTestSettings.SettingsNameAlias);

        var resultOutcome = UnitTestOutcomeHelper.ToTestOutcome(UnitTestOutcome.NotRunnable, adapterSettings);
        Verify(resultOutcome == TestOutcome.Failed);
    }

    public void UniTestHelperToTestOutcomeForUnitTestOutcomeTimeoutShouldReturnTestOutcomeFailed()
    {
        string runSettingxml =
        @"<RunSettings>
                    <MSTestV2>
                    </MSTestV2>
                  </RunSettings>";

        var adapterSettings = MSTestSettings.GetSettings(runSettingxml, MSTestSettings.SettingsNameAlias);

        var resultOutcome = UnitTestOutcomeHelper.ToTestOutcome(UnitTestOutcome.Timeout, adapterSettings);
        Verify(resultOutcome == TestOutcome.Failed);
    }

    public void UniTestHelperToTestOutcomeForUnitTestOutcomeIgnoredShouldReturnTestOutcomeSkipped()
    {
        string runSettingxml =
        @"<RunSettings>
                    <MSTestV2>
                    </MSTestV2>
                  </RunSettings>";

        var adapterSettings = MSTestSettings.GetSettings(runSettingxml, MSTestSettings.SettingsNameAlias);

        var resultOutcome = UnitTestOutcomeHelper.ToTestOutcome(UnitTestOutcome.Ignored, adapterSettings);
        Verify(resultOutcome == TestOutcome.Skipped);
    }

    public void UniTestHelperToTestOutcomeForUnitTestOutcomeInconclusiveShouldReturnTestOutcomeSkipped()
    {
        string runSettingxml =
        @"<RunSettings>
                    <MSTestV2>
                    </MSTestV2>
                  </RunSettings>";

        var adapterSettings = MSTestSettings.GetSettings(runSettingxml, MSTestSettings.SettingsNameAlias);

        var resultOutcome = UnitTestOutcomeHelper.ToTestOutcome(UnitTestOutcome.Inconclusive, adapterSettings);
        Verify(resultOutcome == TestOutcome.Skipped);
    }

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
        Verify(resultOutcome == TestOutcome.Failed);
    }

    public void UniTestHelperToTestOutcomeForUnitTestOutcomeNotFoundShouldReturnTestOutcomeNotFound()
    {
        string runSettingxml =
        @"<RunSettings>
                    <MSTestV2>
                    </MSTestV2>
                  </RunSettings>";

        var adapterSettings = MSTestSettings.GetSettings(runSettingxml, MSTestSettings.SettingsNameAlias);

        var resultOutcome = UnitTestOutcomeHelper.ToTestOutcome(UnitTestOutcome.NotFound, adapterSettings);
        Verify(resultOutcome == TestOutcome.NotFound);
    }

    public void UniTestHelperToTestOutcomeForUnitTestOutcomeInProgressShouldReturnTestOutcomeNone()
    {
        string runSettingxml =
        @"<RunSettings>
                    <MSTestV2>
                    </MSTestV2>
                  </RunSettings>";

        var adapterSettings = MSTestSettings.GetSettings(runSettingxml, MSTestSettings.SettingsNameAlias);

        var resultOutcome = UnitTestOutcomeHelper.ToTestOutcome(UnitTestOutcome.InProgress, adapterSettings);
        Verify(resultOutcome == TestOutcome.None);
    }

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
        Verify(resultOutcome == TestOutcome.Failed);
    }
}
