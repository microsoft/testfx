// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using AwesomeAssertions;

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Resources;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using Moq;

using TestFramework.ForTestingMSTest;

namespace MSTestAdapter.PlatformServices.UnitTests.Helpers;

public sealed class MSTestDiscovererHelpersMutationTests : TestContainer
{
    private const string TimeoutSettings = "<RunSettings><MSTest><TestTimeout>73</TestTimeout></MSTest></RunSettings>";

    public MSTestDiscovererHelpersMutationTests()
    {
        MSTestSettings.Reset();
        MSTestSettingsProvider.Reset();
    }

    public void AreValidSources_RequiresOnlyOneSourceToHaveOneOfTheValidExtensions()
    {
        Mock<ITestSourceHandler> handler = HandlerWithExtensions(".dll", ".exe");

        MSTestDiscovererHelpers.AreValidSources(["readme.txt", "tests.DLL"], handler.Object)
            .Should().BeTrue(because: "one source with a valid extension is enough, whatever the casing, and the other sources may be invalid");
        MSTestDiscovererHelpers.AreValidSources(["tests.exe"], handler.Object)
            .Should().BeTrue(because: "a source has to match any of the valid extensions, not all of them");
        MSTestDiscovererHelpers.AreValidSources(["readme.txt", "notes.md"], handler.Object)
            .Should().BeFalse(because: "no source has a valid extension");
        MSTestDiscovererHelpers.AreValidSources([], handler.Object)
            .Should().BeFalse(because: "there is no source that could be valid");
    }

    public void InitializeDiscovery_AppliesTheRunSettingsAndSucceedsForSupportedSources()
    {
        Mock<ITestSourceHandler> handler = HandlerWithExtensions(".dll");
        var logger = new Mock<IAdapterMessageLogger>(MockBehavior.Strict);

        bool initialized = MSTestDiscovererHelpers.InitializeDiscovery(["tests.dll"], TimeoutSettings, logger.Object, configuration: null, handler.Object);

        initialized.Should().BeTrue(because: "supported sources with valid settings must initialize the discovery");
        MSTestSettings.CurrentSettings.TestTimeout.Should().Be(73, because: "the run settings must be applied to the current MSTest settings");
        logger.VerifyNoOtherCalls();
    }

    public void InitializeDiscovery_RejectsSourcesWithoutAValidExtensionBeforeReadingTheSettings()
    {
        Mock<ITestSourceHandler> handler = HandlerWithExtensions(".dll");
        var logger = new Mock<IAdapterMessageLogger>(MockBehavior.Strict);

        Action initialize = () => MSTestDiscovererHelpers.InitializeDiscovery(["readme.txt"], TimeoutSettings, logger.Object, configuration: null, handler.Object);

        initialize.Should().ThrowExactly<NotSupportedException>().WithMessage(Resource.SourcesNotSupported);
        MSTestSettings.CurrentSettings.TestTimeout.Should().Be(0, because: "the settings of unsupported sources must not be applied");
        logger.VerifyNoOtherCalls();
    }

    public void InitializeDiscovery_ReportsASettingsFailureWithItsInnerExceptionAsOneErrorAndReturnsFalse()
    {
        Mock<ITestSourceHandler> handler = HandlerWithExtensions(".dll");
        var messages = new List<(MessageLevel Level, string Message)>();
        var logger = new Mock<IAdapterMessageLogger>(MockBehavior.Strict);
        logger.Setup(value => value.SendMessage(It.IsAny<MessageLevel>(), It.IsAny<string>()))
            .Callback<MessageLevel, string>((level, message) => messages.Add((level, message)));

        bool initialized = MSTestDiscovererHelpers.InitializeDiscovery(["tests.dll"], "<RunSettings><MSTest>", logger.Object, configuration: null, handler.Object);

        initialized.Should().BeFalse(because: "malformed settings must make the discovery bail out");
        messages.Should().HaveCount(1, because: "the failure must be reported exactly once");
        messages[0].Level.Should().Be(MessageLevel.Error, because: "a settings failure is an error");
        messages[0].Message.Should().StartWith(
            $"{typeof(AdapterSettingsException)}: {Resource.InvalidSettingsXml} ---> {typeof(XmlException)}: ",
            because: "the message must carry the whole exception chain when the settings failure wraps another exception");
    }

    public void InitializeDiscovery_ReportsASettingsFailureWithoutInnerExceptionAsItsPlainMessage()
    {
        Mock<ITestSourceHandler> handler = HandlerWithExtensions(".dll");
        var logger = new Mock<IAdapterMessageLogger>(MockBehavior.Strict);
        string expected = string.Format(CultureInfo.CurrentCulture, Resource.InvalidParallelScopeValue, "Pond", "ClassLevel, MethodLevel");
        logger.Setup(value => value.SendMessage(MessageLevel.Error, expected));

        bool initialized = MSTestDiscovererHelpers.InitializeDiscovery(
            ["tests.dll"],
            "<RunSettings><MSTest><Parallelize><Scope>Pond</Scope></Parallelize></MSTest></RunSettings>",
            logger.Object,
            configuration: null,
            handler.Object);

        initialized.Should().BeFalse(because: "invalid settings must make the discovery bail out");
        logger.Verify(value => value.SendMessage(MessageLevel.Error, expected), Times.Once);
        logger.VerifyNoOtherCalls();
    }

    public void GetSettingsExceptionMessage_FormatsTheWholeExceptionChainOnlyWhenThereIsAnInnerException()
    {
        var withoutInner = new AdapterSettingsException("outer failure");
        var withInner = new AdapterSettingsException("outer failure", new InvalidOperationException("inner cause"));

        MSTestDiscovererHelpers.GetSettingsExceptionMessage(withoutInner)
            .Should().Be("outer failure", because: "without an inner exception the plain message is reported unchanged");
        MSTestDiscovererHelpers.GetSettingsExceptionMessage(withInner)
            .Should().Be(
                $"{typeof(AdapterSettingsException)}: outer failure ---> {typeof(InvalidOperationException)}: inner cause",
                because: "with an inner exception every level of the chain is reported with its type and message");
    }

    protected override void Dispose(bool disposing)
    {
        try
        {
            MSTestSettings.Reset();
            MSTestSettingsProvider.Reset();
        }
        finally
        {
            base.Dispose(disposing);
        }
    }

    private static Mock<ITestSourceHandler> HandlerWithExtensions(params string[] extensions)
    {
        var handler = new Mock<ITestSourceHandler>(MockBehavior.Strict);
        handler.SetupGet(value => value.ValidSourceExtensions).Returns(extensions);
        return handler;
    }
}
