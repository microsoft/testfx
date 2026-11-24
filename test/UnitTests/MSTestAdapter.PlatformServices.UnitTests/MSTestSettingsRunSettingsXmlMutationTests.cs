// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using AwesomeAssertions;

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Resources;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using Moq;

using TestFramework.ForTestingMSTest;

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests;

public sealed class MSTestSettingsRunSettingsXmlMutationTests : TestContainer
{
    public void InvalidGlobalTestTimeoutsWarnWithTheirSettingName()
    {
        foreach (string settingName in new[] { "GlobalTestInitializeTimeout", "GlobalTestCleanupTimeout" })
        {
            string warning = string.Format(CultureInfo.CurrentCulture, Resource.InvalidTimeoutValue, "abc", settingName);
            var logger = new Mock<IAdapterMessageLogger>(MockBehavior.Strict);
            logger.Setup(value => value.SendMessage(MessageLevel.Warning, warning));

            MSTestSettings settings = Parse($"<{settingName}>abc</{settingName}>", logger.Object);

            logger.Verify(value => value.SendMessage(MessageLevel.Warning, warning), Times.Once, settingName);
            logger.VerifyNoOtherCalls();
            settings.GlobalTestInitializeTimeout.Should().Be(0, because: settingName);
            settings.GlobalTestCleanupTimeout.Should().Be(0, because: settingName);
        }
    }

    public void InvalidOrderTestsByNameInClassWarnsAndKeepsTheDefault()
    {
        string warning = string.Format(CultureInfo.CurrentCulture, Resource.InvalidValue, "abc", "OrderTestsByNameInClass");
        var logger = new Mock<IAdapterMessageLogger>(MockBehavior.Strict);
        logger.Setup(value => value.SendMessage(MessageLevel.Warning, warning));

        MSTestSettings settings = Parse("<OrderTestsByNameInClass>abc</OrderTestsByNameInClass>", logger.Object);

        settings.OrderTestsByNameInClass.Should().BeFalse();
        logger.Verify(value => value.SendMessage(MessageLevel.Warning, warning), Times.Once);
        logger.VerifyNoOtherCalls();
    }

    private static MSTestSettings Parse(string content, IAdapterMessageLogger? logger)
        => MSTestSettings.GetSettings($"<RunSettings><MSTest>{content}</MSTest></RunSettings>", MSTestSettings.SettingsName, logger)!;
}
