// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Execution;

using System;
using System.Linq;
using System.Reflection;

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;

using TestFramework.ForTestingMSTest;

using UnitTestOutcome = Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel.UnitTestOutcome;

public class UnitTestResultTest : TestContainer
{
    public void UnitTestResultConstrutorWithOutcomeAndErrorMessageShouldSetRequiredFields()
    {
        UnitTestResult result = new(UnitTestOutcome.Error, "DummyMessage");

        Verify(UnitTestOutcome.Error == result.Outcome);
        Verify("DummyMessage" == result.ErrorMessage);
    }

    public void UnitTestResultConstrutorWithTestFailedExceptionShouldSetRequiredFields()
    {
        var stackTrace = new StackTraceInformation("trace", "filePath", 2, 3);
        TestFailedException ex = new(UnitTestOutcome.Error, "DummyMessage", stackTrace);

        UnitTestResult result = new(ex);

        Verify(UnitTestOutcome.Error == result.Outcome);
        Verify("DummyMessage" == result.ErrorMessage);
        Verify("trace" == result.ErrorStackTrace);
        Verify("filePath" == result.ErrorFilePath);
        Verify(2 == result.ErrorLineNumber);
        Verify(3 == result.ErrorColumnNumber);
    }

    public void ToTestResultShouldReturnConvertedTestResultWithFieldsSet()
    {
        var stackTrace = new StackTraceInformation("DummyStackTrace", "filePath", 2, 3);
        TestFailedException ex = new(UnitTestOutcome.Error, "DummyMessage", stackTrace);
        var dummyTimeSpan = new TimeSpan(20);
        UnitTestResult result = new(ex)
        {
            DisplayName = "DummyDisplayName",
            Duration = dummyTimeSpan
        };

        TestCase testCase = new("Foo", new Uri("Uri", UriKind.Relative), Assembly.GetExecutingAssembly().FullName);
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
        Verify(testCase == testResult.TestCase);
        Verify("DummyDisplayName" == testResult.DisplayName);
        Verify(dummyTimeSpan == testResult.Duration);
        Verify(TestOutcome.Failed == testResult.Outcome);
        Verify("DummyMessage" == testResult.ErrorMessage);
        Verify("DummyStackTrace" == testResult.ErrorStackTrace);
        Verify(startTime == testResult.StartTime);
        Verify(endTime == testResult.EndTime);
        Verify(0 == testResult.Messages.Count);
    }

    public void ToTestResultForUniTestResultWithStandardOutShouldReturnTestResultWithStdOutMessage()
    {
        UnitTestResult result = new()
        {
            StandardOut = "DummyOutput"
        };
        TestCase testCase = new("Foo", new Uri("Uri", UriKind.Relative), Assembly.GetExecutingAssembly().FullName);
        string runSettingxml =
           @"<RunSettings>
                    <MSTestV2>
                    </MSTestV2>
                  </RunSettings>";

        MSTestSettings adapterSettings = MSTestSettings.GetSettings(runSettingxml, MSTestSettings.SettingsNameAlias);

        var testresult = result.ToTestResult(testCase, DateTimeOffset.Now, DateTimeOffset.Now, adapterSettings);
        Verify(testresult.Messages.All(m => m.Text.Contains("DummyOutput") && m.Category.Equals("StdOutMsgs")));
    }

    public void ToTestResultForUniTestResultWithDebugTraceShouldReturnTestResultWithDebugTraceStdOutMessage()
    {
        UnitTestResult result = new()
        {
            DebugTrace = "DummyDebugTrace"
        };
        TestCase testCase = new("Foo", new Uri("Uri", UriKind.Relative), Assembly.GetExecutingAssembly().FullName);
        string runSettingxml =
           @"<RunSettings>
                    <MSTestV2>
                    </MSTestV2>
                  </RunSettings>";

        MSTestSettings adapterSettings = MSTestSettings.GetSettings(runSettingxml, MSTestSettings.SettingsNameAlias);
        var testresult = result.ToTestResult(testCase, DateTimeOffset.Now, DateTimeOffset.Now, adapterSettings);
        Verify(testresult.Messages.All(m => m.Text.Contains("\n\nDebug Trace:\nDummyDebugTrace") && m.Category.Equals("StdOutMsgs")));
    }
}
