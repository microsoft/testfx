// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using AwesomeAssertions;

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Helpers;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Logging;

using Moq;

using TestFramework.ForTestingMSTest;

using UnitTestOutcome = Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel.UnitTestOutcome;
using UTF = Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.ObjectModel;

public class UnitTestResultTests : TestContainer
{
    private readonly Mock<IMessageLogger> _mockMessageLogger = new();

    public void UnitTestResultConstructorWithOutcomeAndErrorMessageShouldSetRequiredFields()
    {
        UnitTestResult result = new(UnitTestOutcome.Error, "DummyMessage");

        result.Outcome.Should().Be(UnitTestOutcome.Error);
        result.ErrorMessage.Should().Be("DummyMessage");
    }

    public void UnitTestResultConstructorWithTestFailedExceptionShouldSetRequiredFields()
    {
        var stackTrace = new StackTraceInformation("trace", "filePath", 2, 3);
        TestFailedException ex = new(UTF.UnitTestOutcome.Error, "DummyMessage", stackTrace);

        UnitTestResult result = new(ex);

        result.Outcome.Should().Be(UnitTestOutcome.Error);
        result.ErrorMessage.Should().Be("DummyMessage");
        result.ErrorStackTrace.Should().Be("trace");
        result.ErrorFilePath.Should().Be("filePath");
        result.ErrorLineNumber.Should().Be(2);
        result.ErrorColumnNumber.Should().Be(3);
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

        MSTestSettings adapterSettings = MSTestSettings.GetSettings(runSettingsXml, MSTestSettings.SettingsNameAlias, _mockMessageLogger.Object)!;

        var resultOutcome = UnitTestOutcomeHelper.ToTestOutcome(UTF.UnitTestOutcome.Passed, adapterSettings);
        resultOutcome.Should().Be(TestOutcome.Passed);
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

        MSTestSettings adapterSettings = MSTestSettings.GetSettings(runSettingsXml, MSTestSettings.SettingsNameAlias, _mockMessageLogger.Object)!;

        var resultOutcome = UnitTestOutcomeHelper.ToTestOutcome(UTF.UnitTestOutcome.Failed, adapterSettings);
        resultOutcome.Should().Be(TestOutcome.Failed);
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

        MSTestSettings adapterSettings = MSTestSettings.GetSettings(runSettingsXml, MSTestSettings.SettingsNameAlias, _mockMessageLogger.Object)!;

        var resultOutcome = UnitTestOutcomeHelper.ToTestOutcome(UTF.UnitTestOutcome.Error, adapterSettings);
        resultOutcome.Should().Be(TestOutcome.Failed);
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

        MSTestSettings adapterSettings = MSTestSettings.GetSettings(runSettingsXml, MSTestSettings.SettingsNameAlias, _mockMessageLogger.Object)!;

        var resultOutcome = UnitTestOutcomeHelper.ToTestOutcome(UTF.UnitTestOutcome.NotRunnable, adapterSettings);
        resultOutcome.Should().Be(TestOutcome.None);
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

        MSTestSettings adapterSettings = MSTestSettings.GetSettings(runSettingsXml, MSTestSettings.SettingsNameAlias, _mockMessageLogger.Object)!;

        var resultOutcome = UnitTestOutcomeHelper.ToTestOutcome(UTF.UnitTestOutcome.NotRunnable, adapterSettings);
        resultOutcome.Should().Be(TestOutcome.Failed);
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

        MSTestSettings adapterSettings = MSTestSettings.GetSettings(runSettingsXml, MSTestSettings.SettingsNameAlias, _mockMessageLogger.Object)!;

        var resultOutcome = UnitTestOutcomeHelper.ToTestOutcome(UTF.UnitTestOutcome.Timeout, adapterSettings);
        resultOutcome.Should().Be(TestOutcome.Failed);
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

        MSTestSettings adapterSettings = MSTestSettings.GetSettings(runSettingsXml, MSTestSettings.SettingsNameAlias, _mockMessageLogger.Object)!;

        var resultOutcome = UnitTestOutcomeHelper.ToTestOutcome(UTF.UnitTestOutcome.Ignored, adapterSettings);
        resultOutcome.Should().Be(TestOutcome.Skipped);
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

        MSTestSettings adapterSettings = MSTestSettings.GetSettings(runSettingsXml, MSTestSettings.SettingsNameAlias, _mockMessageLogger.Object)!;

        var resultOutcome = UnitTestOutcomeHelper.ToTestOutcome(UTF.UnitTestOutcome.Inconclusive, adapterSettings);
        resultOutcome.Should().Be(TestOutcome.Skipped);
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

        MSTestSettings adapterSettings = MSTestSettings.GetSettings(runSettingsXml, MSTestSettings.SettingsNameAlias, _mockMessageLogger.Object)!;

        var resultOutcome = UnitTestOutcomeHelper.ToTestOutcome(UTF.UnitTestOutcome.Inconclusive, adapterSettings);
        resultOutcome.Should().Be(TestOutcome.Failed);
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

        MSTestSettings adapterSettings = MSTestSettings.GetSettings(runSettingsXml, MSTestSettings.SettingsNameAlias, _mockMessageLogger.Object)!;

        var resultOutcome = UnitTestOutcomeHelper.ToTestOutcome(UTF.UnitTestOutcome.NotFound, adapterSettings);
        resultOutcome.Should().Be(TestOutcome.NotFound);
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

        MSTestSettings adapterSettings = MSTestSettings.GetSettings(runSettingsXml, MSTestSettings.SettingsNameAlias, _mockMessageLogger.Object)!;

        var resultOutcome = UnitTestOutcomeHelper.ToTestOutcome(UTF.UnitTestOutcome.InProgress, adapterSettings);
        resultOutcome.Should().Be(TestOutcome.None);
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

        MSTestSettings adapterSettings = MSTestSettings.GetSettings(runSettingsXml, MSTestSettings.SettingsNameAlias, _mockMessageLogger.Object)!;
        var resultOutcome = UnitTestOutcomeHelper.ToTestOutcome(UTF.UnitTestOutcome.NotRunnable, adapterSettings);
        resultOutcome.Should().Be(TestOutcome.Failed);
    }
}
