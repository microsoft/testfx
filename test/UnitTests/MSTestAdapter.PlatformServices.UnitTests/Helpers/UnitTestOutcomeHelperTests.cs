// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using AwesomeAssertions;

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Helpers;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Logging;

using Moq;

using TestFramework.ForTestingMSTest;

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Helpers;

public class UnitTestOutcomeHelperTests : TestContainer
{
    private readonly MSTestSettings _adapterSettings;

    public UnitTestOutcomeHelperTests()
    {
        string runSettingsXml =
            """
            <RunSettings>
              <MSTestV2>
              </MSTestV2>
            </RunSettings>
            """;
        var mockMessageLogger = new Mock<IMessageLogger>();
        _adapterSettings = MSTestSettings.GetSettings(runSettingsXml, MSTestSettings.SettingsNameAlias, mockMessageLogger.Object)!;
    }

    public void UniTestHelperToTestOutcomeForUnitTestOutcomePassedShouldReturnTestOutcomePassed()
    {
        var resultOutcome = UnitTestOutcomeHelper.ToTestOutcome(UnitTestOutcome.Passed, _adapterSettings);
        resultOutcome.Should().Be(TestOutcome.Passed);
    }

    public void UniTestHelperToTestOutcomeForUnitTestOutcomeFailedShouldReturnTestOutcomeFailed()
    {
        var resultOutcome = UnitTestOutcomeHelper.ToTestOutcome(UnitTestOutcome.Failed, _adapterSettings);
        resultOutcome.Should().Be(TestOutcome.Failed);
    }

    public void UniTestHelperToTestOutcomeForUnitTestOutcomeErrorShouldReturnTestOutcomeFailed()
    {
        var resultOutcome = UnitTestOutcomeHelper.ToTestOutcome(UnitTestOutcome.Error, _adapterSettings);
        resultOutcome.Should().Be(TestOutcome.Failed);
    }

    public void UniTestHelperToTestOutcomeForUnitTestOutcomeNotRunnableShouldReturnTestOutcomeFailed()
    {
        var resultOutcome = UnitTestOutcomeHelper.ToTestOutcome(UnitTestOutcome.NotRunnable, _adapterSettings);
        resultOutcome.Should().Be(TestOutcome.Failed);
    }

    public void UniTestHelperToTestOutcomeForUnitTestOutcomeTimeoutShouldReturnTestOutcomeFailed()
    {
        var resultOutcome = UnitTestOutcomeHelper.ToTestOutcome(UnitTestOutcome.Timeout, _adapterSettings);
        resultOutcome.Should().Be(TestOutcome.Failed);
    }

    public void UniTestHelperToTestOutcomeForUnitTestOutcomeIgnoredShouldReturnTestOutcomeSkipped()
    {
        var resultOutcome = UnitTestOutcomeHelper.ToTestOutcome(UnitTestOutcome.Ignored, _adapterSettings);
        resultOutcome.Should().Be(TestOutcome.Skipped);
    }

    public void UniTestHelperToTestOutcomeForUnitTestOutcomeInconclusiveShouldReturnTestOutcomeSkipped()
    {
        var resultOutcome = UnitTestOutcomeHelper.ToTestOutcome(UnitTestOutcome.Inconclusive, _adapterSettings);
        resultOutcome.Should().Be(TestOutcome.Skipped);
    }

    public void UniTestHelperToTestOutcomeForUnitTestOutcomeNotFoundShouldReturnTestOutcomeNotFound()
    {
        var resultOutcome = UnitTestOutcomeHelper.ToTestOutcome(UnitTestOutcome.NotFound, _adapterSettings);
        resultOutcome.Should().Be(TestOutcome.NotFound);
    }

    public void UniTestHelperToTestOutcomeForUnitTestOutcomeInProgressShouldReturnTestOutcomeNone()
    {
        var resultOutcome = UnitTestOutcomeHelper.ToTestOutcome(UnitTestOutcome.InProgress, _adapterSettings);
        resultOutcome.Should().Be(TestOutcome.None);
    }
}
