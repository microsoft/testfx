// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using AwesomeAssertions;

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface;

using Moq;

using TestFramework.ForTestingMSTest;

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests;

public class MSTestDiscovererHelpersTests : TestContainer
{
    private readonly Mock<ITestSourceHandler> _mockTestSourceHandler = new();

    public void AreValidSourcesShouldThrowIfPlatformsValidSourceExtensionsIsNull()
    {
        _mockTestSourceHandler.SetupGet(ts => ts.ValidSourceExtensions).Returns((List<string>)null!);
        var sources = new List<string> { "dummy" };
        Action action = () => MSTestDiscovererHelpers.AreValidSources(sources, _mockTestSourceHandler.Object);
        action.Should().Throw<ArgumentNullException>();
    }

    public void AreValidSourcesShouldReturnFalseIfValidSourceExtensionsIsEmpty()
    {
        _mockTestSourceHandler.SetupGet(ts => ts.ValidSourceExtensions).Returns(new List<string> { });
        MSTestDiscovererHelpers.AreValidSources(new List<string> { "dummy.te" }, _mockTestSourceHandler.Object).Should().BeFalse();
    }

    public void AreValidSourcesShouldReturnTrueForValidSourceExtensions()
    {
        _mockTestSourceHandler.SetupGet(ts => ts.ValidSourceExtensions).Returns(new List<string> { ".te" });
        MSTestDiscovererHelpers.AreValidSources(new List<string> { "dummy.te" }, _mockTestSourceHandler.Object).Should().BeTrue();
    }

    public void AreValidSourcesShouldReturnFalseForInvalidSourceExtensions()
    {
        _mockTestSourceHandler.SetupGet(ts => ts.ValidSourceExtensions).Returns(new List<string> { ".nte", ".tep" });
        MSTestDiscovererHelpers.AreValidSources(new List<string> { "dummy.te" }, _mockTestSourceHandler.Object).Should().BeFalse();
    }

    public void GetSettingsExceptionMessageShouldReturnPlainMessageWhenThereIsNoInnerException()
    {
        var ex = new AdapterSettingsException("Invalid value 'Pond' specified for 'Scope'.");

        MSTestDiscovererHelpers.GetSettingsExceptionMessage(ex).Should().Be("Invalid value 'Pond' specified for 'Scope'.");
    }

    public void GetSettingsExceptionMessageShouldIncludeMalformedXmlDetailsFromPopulateSettings()
    {
        Action act = () => MSTestSettings.PopulateSettings("<RunSettings><MSTest>", null, null);
        AdapterSettingsException ex = act.Should().Throw<AdapterSettingsException>().Which;

        string message = MSTestDiscovererHelpers.GetSettingsExceptionMessage(ex);

        ex.InnerException.Should().BeOfType<XmlException>();
        message.Should().Contain(ex.Message);
        message.Should().Contain(nameof(XmlException));
        message.Should().Contain(ex.InnerException.Message);
    }

    public void AdapterSettingsExceptionShouldSetInnerExceptionWhenProvided()
    {
        var innerException = new FormatException("bad format");
        var ex = new AdapterSettingsException("outer message", innerException);

        ex.Message.Should().Be("outer message");
        ex.InnerException.Should().BeSameAs(innerException);
    }
}
