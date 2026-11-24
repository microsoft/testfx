// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using AwesomeAssertions;

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Resources;

using Moq;

using TestFramework.ForTestingMSTest;

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests;

public sealed class RunConfigurationSettingsMutationTests : TestContainer
{
    private const string StaEntry = "<ExecutionThreadApartmentState>STA</ExecutionThreadApartmentState>";
    private const string MtaEntry = "<ExecutionThreadApartmentState>MTA</ExecutionThreadApartmentState>";

    public void GetSettingsIgnoresCommentsAroundTheApartmentStateEntries()
    {
        (string Case, string Xml, ApartmentState? Expected)[] cases =
        [
            ("a comment is the first child of RunConfiguration", $"<RunSettings><RunConfiguration><!-- c -->{StaEntry}</RunConfiguration></RunSettings>", ApartmentState.STA),
            ("a comment sits between two apartment state entries", $"<RunSettings><RunConfiguration>{StaEntry}<!-- c -->{MtaEntry}</RunConfiguration></RunSettings>", ApartmentState.MTA),
            ("a comment sits between an unknown entry and the apartment state entry", $"<RunSettings><RunConfiguration><Unknown/><!-- c -->{StaEntry}</RunConfiguration></RunSettings>", ApartmentState.STA),
        ];

        foreach ((string description, string xml, ApartmentState? expected) in cases)
        {
            RunConfigurationSettings.GetSettings(xml).ExecutionApartmentState.Should().Be(expected, because: description);
        }
    }

    public void GetSettingsFindsTheApartmentStateAfterNonElementNodesFollowingAnUnknownEntry()
    {
        (string Case, string Xml, ApartmentState? Expected)[] cases =
        [
            ("text follows an unknown entry", $"<RunSettings><RunConfiguration><Unknown>1</Unknown>stray{StaEntry}</RunConfiguration></RunSettings>", ApartmentState.STA),
            ("CDATA follows an unknown entry", $"<RunSettings><RunConfiguration><Unknown/><![CDATA[stray]]>{MtaEntry}</RunConfiguration></RunSettings>", ApartmentState.MTA),
            ("a processing instruction follows an unknown entry", $"<RunSettings><RunConfiguration><Unknown/><?stray x?>{StaEntry}</RunConfiguration></RunSettings>", ApartmentState.STA),
            ("text follows a nested unknown entry", $"<RunSettings><RunConfiguration><A><B/></A><C/>tail{MtaEntry}</RunConfiguration></RunSettings>", ApartmentState.MTA),
        ];

        foreach ((string description, string xml, ApartmentState? expected) in cases)
        {
            RunConfigurationSettings.GetSettings(xml).ExecutionApartmentState.Should().Be(expected, because: description);
        }
    }

    public void GetSettingsFindsRunConfigurationAfterANodeNamedLikeItThatIsNotAnElement()
    {
        string xml = $"<RunSettings><Other/><?RunConfiguration x?><RunConfiguration>{StaEntry}</RunConfiguration></RunSettings>";

        RunConfigurationSettings.GetSettings(xml).ExecutionApartmentState.Should().Be(ApartmentState.STA);
    }

    public void GetSettingsRejectsAnUndefinedApartmentStateNumberAndReportsItsValue()
    {
        Action action = () => RunConfigurationSettings.GetSettings("<RunSettings><RunConfiguration><ExecutionThreadApartmentState>99</ExecutionThreadApartmentState></RunConfiguration></RunSettings>");

        action.Should().Throw<NotSupportedException>().Which.Message.Should().Be("Platform apartment state '99' is not supported.");
    }

    public void SetRunConfigurationSettingsFromConfigRejectsAnUndefinedApartmentStateNumberAndNamesTheSetting()
    {
        var configuration = new Mock<IConfiguration>();
        configuration.Setup(c => c[It.IsAny<string>()]).Returns("99");

        Action action = () => RunConfigurationSettings.SetRunConfigurationSettingsFromConfig(configuration.Object, new RunConfigurationSettings());

        action.Should().Throw<NotSupportedException>().Which.Message.Should().Be(
            string.Format(CultureInfo.CurrentCulture, Resource.InvalidValue, "99", "execution:executionApartmentState"));
    }
}
