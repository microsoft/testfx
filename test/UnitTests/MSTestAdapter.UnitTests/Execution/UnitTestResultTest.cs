// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Reflection;

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;

using TestFramework.ForTestingMSTest;

using UnitTestOutcome = Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel.UnitTestOutcome;

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Execution;

public class UnitTestResultTest : TestContainer
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

        string runSettingsXml =
            @"<RunSettings>
                    <MSTestV2>
                    </MSTestV2>
                  </RunSettings>";

        MSTestSettings adapterSettings = MSTestSettings.GetSettings(runSettingsXml, MSTestSettings.SettingsNameAlias);

        // Act
        var testResult = result.ToTestResult(testCase, startTime, endTime, "MachineName", adapterSettings);

        // Validate
        Verify(testCase == testResult.TestCase);
        Verify(testResult.DisplayName == "DummyDisplayName");
        Verify(dummyTimeSpan == testResult.Duration);
        Verify(testResult.Outcome == TestOutcome.Failed);
        Verify(testResult.ErrorMessage == "DummyMessage");
        Verify(testResult.ErrorStackTrace == "DummyStackTrace");
        Verify(startTime == testResult.StartTime);
        Verify(endTime == testResult.EndTime);
        Verify(testResult.ComputerName == "MachineName");
        Verify(testResult.Messages.Count == 0);
    }

    public void ToTestResultForUniTestResultWithStandardOutShouldReturnTestResultWithStdOutMessage()
    {
        UnitTestResult result = new()
        {
            StandardOut = "DummyOutput",
        };
        TestCase testCase = new("Foo", new Uri("Uri", UriKind.Relative), Assembly.GetExecutingAssembly().FullName);
        string runSettingsXml =
           @"<RunSettings>
                    <MSTestV2>
                    </MSTestV2>
                  </RunSettings>";

        MSTestSettings adapterSettings = MSTestSettings.GetSettings(runSettingsXml, MSTestSettings.SettingsNameAlias);

        var testResult = result.ToTestResult(testCase, DateTimeOffset.Now, DateTimeOffset.Now, "MachineName", adapterSettings);
        Verify(testResult.Messages.All(m => m.Text.Contains("DummyOutput") && m.Category.Equals("StdOutMsgs", StringComparison.Ordinal)));
    }

    public void ToTestResultForUniTestResultWithDebugTraceShouldReturnTestResultWithDebugTraceStdOutMessage()
    {
        UnitTestResult result = new()
        {
            DebugTrace = "DummyDebugTrace",
        };
        TestCase testCase = new("Foo", new Uri("Uri", UriKind.Relative), Assembly.GetExecutingAssembly().FullName);
        string runSettingsXml =
           @"<RunSettings>
                    <MSTestV2>
                    </MSTestV2>
                  </RunSettings>";

        MSTestSettings adapterSettings = MSTestSettings.GetSettings(runSettingsXml, MSTestSettings.SettingsNameAlias);
        var testResult = result.ToTestResult(testCase, DateTimeOffset.Now, DateTimeOffset.Now, "MachineName", adapterSettings);
        Verify(testResult.Messages.All(m => m.Text.Contains("\r\n\r\nDebug Trace:\r\nDummyDebugTrace") && m.Category.Equals("StdOutMsgs", StringComparison.Ordinal)));
    }
}
