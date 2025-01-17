// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Helpers;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Logging;

using Moq;

using TestFramework.ForTestingMSTest;

using UnitTestOutcome = Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel.UnitTestOutcome;

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.ObjectModel;

public class UnitTestResultTests : TestContainer
{
    private readonly Mock<IMessageLogger> _mockMessageLogger = new();

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

    public void UniTestHelperToTestOutcomeForUnitTestOutcomePassedShouldReturnTestOutcomePassed()
    {
        string runSettingsXml =
            """
            <RunSettings>
              <MSTestV2>
              </MSTestV2>
            </RunSettings>
            """;

        var adapterSettings = MSTestSettings.GetSettings(runSettingsXml, MSTestSettings.SettingsNameAlias, _mockMessageLogger.Object);

        var resultOutcome = UnitTestOutcomeHelper.ToTestOutcome(UnitTestOutcome.Passed, adapterSettings);
        Verify(resultOutcome == TestOutcome.Passed);
    }

    public void UniTestHelperToTestOutcomeForUnitTestOutcomeFailedShouldReturnTestOutcomeFailed()
    {
        string runSettingsXml =
            """
            <RunSettings>
              <MSTestV2>
              </MSTestV2>
            </RunSettings>
            """;

        var adapterSettings = MSTestSettings.GetSettings(runSettingsXml, MSTestSettings.SettingsNameAlias, _mockMessageLogger.Object);

        var resultOutcome = UnitTestOutcomeHelper.ToTestOutcome(UnitTestOutcome.Failed, adapterSettings);
        Verify(resultOutcome == TestOutcome.Failed);
    }

    public void UniTestHelperToTestOutcomeForUnitTestOutcomeErrorShouldReturnTestOutcomeFailed()
    {
        string runSettingsXml =
            """
            <RunSettings>
              <MSTestV2>
              </MSTestV2>
            </RunSettings>
            """;

        var adapterSettings = MSTestSettings.GetSettings(runSettingsXml, MSTestSettings.SettingsNameAlias, _mockMessageLogger.Object);

        var resultOutcome = UnitTestOutcomeHelper.ToTestOutcome(UnitTestOutcome.Error, adapterSettings);
        Verify(resultOutcome == TestOutcome.Failed);
    }

    public void UnitTestHelperToTestOutcomeForUnitTestOutcomeNotRunnableShouldReturnTestOutComeNoneWhenSpecifiedInAdapterSettings()
    {
        string runSettingsXml =
            """
            <RunSettings>
              <MSTestV2>
                <MapNotRunnableToFailed>false</MapNotRunnableToFailed>
              </MSTestV2>
            </RunSettings>
            """;

        var adapterSettings = MSTestSettings.GetSettings(runSettingsXml, MSTestSettings.SettingsNameAlias, _mockMessageLogger.Object);

        var resultOutcome = UnitTestOutcomeHelper.ToTestOutcome(UnitTestOutcome.NotRunnable, adapterSettings);
        Verify(resultOutcome == TestOutcome.None);
    }

    public void UnitTestHelperToTestOutcomeForUnitTestOutcomeNotRunnableShouldReturnTestOutcomeFailedByDefault()
    {
        string runSettingsXml =
            """
            <RunSettings>
              <MSTestV2>
              </MSTestV2>
            </RunSettings>
            """;

        var adapterSettings = MSTestSettings.GetSettings(runSettingsXml, MSTestSettings.SettingsNameAlias, _mockMessageLogger.Object);

        var resultOutcome = UnitTestOutcomeHelper.ToTestOutcome(UnitTestOutcome.NotRunnable, adapterSettings);
        Verify(resultOutcome == TestOutcome.Failed);
    }

    public void UniTestHelperToTestOutcomeForUnitTestOutcomeTimeoutShouldReturnTestOutcomeFailed()
    {
        string runSettingsXml =
            """
            <RunSettings>
              <MSTestV2>
              </MSTestV2>
            </RunSettings>
            """;

        var adapterSettings = MSTestSettings.GetSettings(runSettingsXml, MSTestSettings.SettingsNameAlias, _mockMessageLogger.Object);

        var resultOutcome = UnitTestOutcomeHelper.ToTestOutcome(UnitTestOutcome.Timeout, adapterSettings);
        Verify(resultOutcome == TestOutcome.Failed);
    }

    public void UniTestHelperToTestOutcomeForUnitTestOutcomeIgnoredShouldReturnTestOutcomeSkipped()
    {
        string runSettingsXml =
            """
            <RunSettings>
              <MSTestV2>
              </MSTestV2>
            </RunSettings>
            """;

        var adapterSettings = MSTestSettings.GetSettings(runSettingsXml, MSTestSettings.SettingsNameAlias, _mockMessageLogger.Object);

        var resultOutcome = UnitTestOutcomeHelper.ToTestOutcome(UnitTestOutcome.Ignored, adapterSettings);
        Verify(resultOutcome == TestOutcome.Skipped);
    }

    public void UniTestHelperToTestOutcomeForUnitTestOutcomeInconclusiveShouldReturnTestOutcomeSkipped()
    {
        string runSettingsXml =
            """
            <RunSettings>
              <MSTestV2>
              </MSTestV2>
            </RunSettings>
            """;

        var adapterSettings = MSTestSettings.GetSettings(runSettingsXml, MSTestSettings.SettingsNameAlias, _mockMessageLogger.Object);

        var resultOutcome = UnitTestOutcomeHelper.ToTestOutcome(UnitTestOutcome.Inconclusive, adapterSettings);
        Verify(resultOutcome == TestOutcome.Skipped);
    }

    public void UniTestHelperToTestOutcomeForUnitTestOutcomeInconclusiveShouldReturnTestOutcomeFailedWhenSpecifiedSo()
    {
        string runSettingsXml =
            """
            <RunSettings>
              <MSTestV2>
                <MapInconclusiveToFailed>true</MapInconclusiveToFailed>
              </MSTestV2>
            </RunSettings>
            """;

        var adapterSettings = MSTestSettings.GetSettings(runSettingsXml, MSTestSettings.SettingsNameAlias, _mockMessageLogger.Object);

        var resultOutcome = UnitTestOutcomeHelper.ToTestOutcome(UnitTestOutcome.Inconclusive, adapterSettings);
        Verify(resultOutcome == TestOutcome.Failed);
    }

    public void UniTestHelperToTestOutcomeForUnitTestOutcomeNotFoundShouldReturnTestOutcomeNotFound()
    {
        string runSettingsXml =
            """
            <RunSettings>
              <MSTestV2>
              </MSTestV2>
            </RunSettings>
            """;

        var adapterSettings = MSTestSettings.GetSettings(runSettingsXml, MSTestSettings.SettingsNameAlias, _mockMessageLogger.Object);

        var resultOutcome = UnitTestOutcomeHelper.ToTestOutcome(UnitTestOutcome.NotFound, adapterSettings);
        Verify(resultOutcome == TestOutcome.NotFound);
    }

    public void UniTestHelperToTestOutcomeForUnitTestOutcomeInProgressShouldReturnTestOutcomeNone()
    {
        string runSettingsXml =
            """
            <RunSettings>
              <MSTestV2>
              </MSTestV2>
            </RunSettings>
            """;

        var adapterSettings = MSTestSettings.GetSettings(runSettingsXml, MSTestSettings.SettingsNameAlias, _mockMessageLogger.Object);

        var resultOutcome = UnitTestOutcomeHelper.ToTestOutcome(UnitTestOutcome.InProgress, adapterSettings);
        Verify(resultOutcome == TestOutcome.None);
    }

    public void UniTestHelperToTestOutcomeForUnitTestOutcomeNotRunnableShouldReturnTestOutcomeFailedWhenSpecifiedSo()
    {
        string runSettingsXml =
            """
            <RunSettings>
              <MSTestV2>
                <MapNotRunnableToFailed>true</MapNotRunnableToFailed>
              </MSTestV2>
            </RunSettings>
            """;

        var adapterSettings = MSTestSettings.GetSettings(runSettingsXml, MSTestSettings.SettingsNameAlias, _mockMessageLogger.Object);
        var resultOutcome = UnitTestOutcomeHelper.ToTestOutcome(UnitTestOutcome.NotRunnable, adapterSettings);
        Verify(resultOutcome == TestOutcome.Failed);
    }
}
