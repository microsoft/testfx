// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using AwesomeAssertions;

using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Configurations;
using Microsoft.Testing.Platform.Extensions.CommandLine;
using Microsoft.Testing.Platform.Extensions.TestHostControllers;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Resources;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.TestingPlatformAdapter;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using Moq;

using TestFramework.ForTestingMSTest;

using EnvironmentVariable = Microsoft.Testing.Platform.Extensions.TestHostControllers.EnvironmentVariable;
using IEnvironment = Microsoft.Testing.Platform.Helpers.IEnvironment;

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests;

public sealed class MSTestRunSettingsProviderTests : TestContainer
{
    public async Task ConfigurationBuildLoadsResultsDirectoryAndRejectsOtherKeys()
    {
        var fileSystem = new Mock<IFileSystem>(MockBehavior.Strict);
        fileSystem.Setup(value => value.ExistFile("chosen.runsettings")).Returns(true);
        fileSystem.Setup(value => value.ReadAllText("chosen.runsettings"))
            .Returns("<RunSettings><RunConfiguration><ResultsDirectory>chosen-results</ResultsDirectory></RunConfiguration></RunSettings>");
        var extension = new MSTestExtension();
        var source = new MSTestRunSettingsConfigurationProvider(extension, fileSystem.Object);

        IConfigurationProvider provider = await source.BuildAsync(new CommandLineParseResult(null, [new("settings", ["chosen.runsettings"])], []));
        await provider.LoadAsync();

        provider.TryGet(PlatformConfigurationConstants.PlatformResultDirectory, out string? result).Should().BeTrue();
        result.Should().Be("chosen-results");
        provider.TryGet("unrelated", out string? unknown).Should().BeFalse();
        unknown.Should().BeNull();
        source.GetChildKeys(null).Should().Equal("platformOptions");
        source.GetChildKeys("platformOptions").Should().Equal("resultDirectory");
        source.GetChildKeys("other").Should().BeEmpty();
        source.Uid.Should().Be("MSTestRunSettingsConfigurationProvider");
        source.Version.Should().Be(extension.Version);
        source.DisplayName.Should().Be("MSTest: runsettings configuration");
        fileSystem.Verify(value => value.ReadAllText("chosen.runsettings"), Times.Once);
    }

    public async Task EnvironmentProviderAppliesLockedSecretVariables()
    {
        var environment = new Mock<IEnvironment>();
        environment.Setup(value => value.GetEnvironmentVariable("TESTINGPLATFORM_EXPERIMENTAL_VSTEST_RUNSETTINGS"))
            .Returns("<RunSettings><RunConfiguration><EnvironmentVariables><FIRST>one=two</FIRST><EMPTY /></EnvironmentVariables></RunConfiguration></RunSettings>");
        var provider = new MSTestRunSettingsEnvironmentVariableProvider(
            new MSTestExtension(), Mock.Of<ICommandLineOptions>(), Mock.Of<IFileSystem>(), environment.Object);
        List<(string Name, string? Value, bool Secret, bool Locked)> updates = [];
        var variables = new Mock<IEnvironmentVariables>(MockBehavior.Strict);
        variables.Setup(value => value.SetVariable(It.IsAny<EnvironmentVariable>()))
            .Callback<EnvironmentVariable>(value => updates.Add((value.Variable, value.Value, value.IsSecret, value.IsLocked)));

        (await provider.IsEnabledAsync()).Should().BeTrue();
        await provider.UpdateAsync(variables.Object);
        (await provider.ValidateTestHostEnvironmentVariablesAsync(variables.Object)).IsValid.Should().BeTrue();

        updates.Should().Equal(("FIRST", "one=two", true, true), ("EMPTY", string.Empty, true, true));
        environment.Verify(value => value.SetEnvironmentVariable(It.IsAny<string>(), It.IsAny<string?>()), Times.Never);
    }

    public async Task EnvironmentProviderWithoutSectionIsDisabled()
    {
        var environment = new Mock<IEnvironment>();
        environment.Setup(value => value.GetEnvironmentVariable("TESTINGPLATFORM_EXPERIMENTAL_VSTEST_RUNSETTINGS"))
            .Returns("<RunSettings><RunConfiguration /></RunSettings>");
        var provider = new MSTestRunSettingsEnvironmentVariableProvider(
            new MSTestExtension(), Mock.Of<ICommandLineOptions>(), Mock.Of<IFileSystem>(), environment.Object);

        (await provider.IsEnabledAsync()).Should().BeFalse();
        environment.Verify(value => value.SetEnvironmentVariable(It.IsAny<string>(), It.IsAny<string?>()), Times.Never);
    }

    public async Task SettingsOptionRejectsMissingAndUnreadableFiles()
    {
        var fileSystem = new Mock<IFileSystem>();
        var provider = new MSTestRunSettingsCommandLineOptionsProvider(new MSTestExtension(), fileSystem.Object);
        CommandLineOption option = provider.GetCommandLineOptions().Single();
        option.Name.Should().Be("settings");

        ValidationResult missing = await provider.ValidateOptionArgumentsAsync(option, ["missing.runsettings"]);
        missing.IsValid.Should().BeFalse();
        missing.ErrorMessage.Should().Be(string.Format(CultureInfo.InvariantCulture, PlatformAdapterResources.RunsettingsFileDoesNotExist, "missing.runsettings"));

        fileSystem.Setup(value => value.ExistFile("unreadable.runsettings")).Returns(true);
        fileSystem.Setup(value => value.NewFileStream("unreadable.runsettings", FileMode.Open, FileAccess.Read)).Throws(new UnauthorizedAccessException());
        ValidationResult unreadable = await provider.ValidateOptionArgumentsAsync(option, ["unreadable.runsettings"]);
        unreadable.IsValid.Should().BeFalse();
        unreadable.ErrorMessage.Should().Be(string.Format(CultureInfo.InvariantCulture, PlatformAdapterResources.RunsettingsFileCannotBeRead, "unreadable.runsettings"));
    }
}
