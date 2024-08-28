// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Xml.Linq;
using System.Xml.XPath;

using Microsoft.Testing.Extensions.VSTestBridge.CommandLine;
using Microsoft.Testing.Extensions.VSTestBridge.ObjectModel;
using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Configurations;
using Microsoft.Testing.Platform.Services;

using Moq;

namespace Microsoft.Testing.Extensions.VSTestBridge.UnitTests.ObjectModel;

[TestGroup]
public class RunSettingsPatcherTests(ITestExecutionContext testExecutionContext) : TestBase(testExecutionContext)
{
    private readonly Mock<IConfiguration> _configuration = new();
    private readonly Mock<ICommandLineOptions> _commandLineOptions = new();

    public void Patch_WhenNoRunSettingsProvided_CreateRunSettingsWithResultsDirectoryElement()
    {
        _configuration.Setup(x => x[PlatformConfigurationConstants.PlatformResultDirectory]).Returns("/PlatformResultDirectory");
        XDocument runSettingsDocument = RunSettingsPatcher.Patch(null, _configuration.Object,
            new ClientInfoService(string.Empty, string.Empty), _commandLineOptions.Object);
        Assert.AreEqual(
            "/PlatformResultDirectory",
            runSettingsDocument.XPathSelectElement("RunSettings/RunConfiguration/ResultsDirectory")!.Value);
    }

    public void Patch_WithRunSettingsProvidedButMissingResultsDirectory_AddsElement()
    {
        string runSettings = """
            <RunSettings>
                <RunConfiguration>
                    <Canary>true</Canary>
                </RunConfiguration>
            </RunSettings>
            """;

        _configuration.Setup(x => x[PlatformConfigurationConstants.PlatformResultDirectory]).Returns("/PlatformResultDirectory");

        XDocument runSettingsDocument = RunSettingsPatcher.Patch(runSettings, _configuration.Object, new ClientInfoService(string.Empty, string.Empty), _commandLineOptions.Object);
        Assert.AreEqual(
            "/PlatformResultDirectory",
            runSettingsDocument.XPathSelectElement("RunSettings/RunConfiguration/ResultsDirectory")!.Value);
        Assert.IsTrue(bool.Parse(runSettingsDocument.XPathSelectElement("RunSettings/RunConfiguration/Canary")!.Value));
    }

    public void Patch_WithRunSettingsContainingResultsDirectory_EntryIsNotOverridden()
    {
        string runSettings =
$"""
    <RunSettings>
        <RunConfiguration>
            <Canary>true</Canary>
            <ResultsDirectory>/PlatformResultDirectoryFromFile</ResultsDirectory>
        </RunConfiguration>
    </RunSettings>
""";

        _configuration.Setup(x => x[PlatformConfigurationConstants.PlatformResultDirectory]).Returns("/PlatformResultDirectory");
        XDocument runSettingsDocument = RunSettingsPatcher.Patch(runSettings, _configuration.Object, new ClientInfoService(string.Empty, string.Empty), _commandLineOptions.Object);
        Assert.AreEqual(
            "/PlatformResultDirectoryFromFile",
            runSettingsDocument.XPathSelectElement("RunSettings/RunConfiguration/ResultsDirectory")!.Value);
        Assert.IsTrue(bool.Parse(runSettingsDocument.XPathSelectElement("RunSettings/RunConfiguration/Canary")!.Value));
    }

    public void Patch_WhenRunSettingsExists_MergesParameters()
    {
        string runSettings = """
            <RunSettings>
                <TestRunParameters>
                    <Parameter name="key1" value="value1" />
                    <Parameter name="key2" value="value2" />
                </TestRunParameters>
            </RunSettings>
            """
        ;

        string[]? arguments;
        _commandLineOptions.Setup(x => x.TryGetOptionArgumentList(TestRunParametersCommandLineOptionsProvider.TestRunParameterOptionName, out arguments))
            .Returns((string optionName, out string[]? arguments) =>
            {
                arguments = ["key2=updated-value", "key3=value3"];
                return true;
            });

        _configuration.Setup(x => x[PlatformConfigurationConstants.PlatformResultDirectory]).Returns("/PlatformResultDirectory");
        XDocument runSettingsDocument = RunSettingsPatcher.Patch(runSettings, _configuration.Object, new ClientInfoService(string.Empty, string.Empty),
            _commandLineOptions.Object);

        XElement[] testRunParameters = runSettingsDocument.XPathSelectElements("RunSettings/TestRunParameters/Parameter").ToArray();
        Assert.AreEqual(testRunParameters[0].Attribute("name")!.Value, "key1");
        Assert.AreEqual(testRunParameters[0].Attribute("value")!.Value, "value1");
        Assert.AreEqual(testRunParameters[1].Attribute("name")!.Value, "key2");
        Assert.AreEqual(testRunParameters[1].Attribute("value")!.Value, "updated-value");
        Assert.AreEqual(testRunParameters[2].Attribute("name")!.Value, "key3");
        Assert.AreEqual(testRunParameters[2].Attribute("value")!.Value, "value3");
    }

    public void Patch_WhenRunSettingsDoesNotExist_AddParameters()
    {
        string[]? arguments;
        _commandLineOptions.Setup(x => x.TryGetOptionArgumentList(TestRunParametersCommandLineOptionsProvider.TestRunParameterOptionName, out arguments))
            .Returns((string optionName, out string[]? arguments) =>
            {
                arguments = ["key1=value1", "key2=value2"];
                return true;
            });

        _configuration.Setup(x => x[PlatformConfigurationConstants.PlatformResultDirectory]).Returns("/PlatformResultDirectory");
        XDocument runSettingsDocument = RunSettingsPatcher.Patch(null, _configuration.Object, new ClientInfoService(string.Empty, string.Empty),
            _commandLineOptions.Object);

        XElement[] testRunParameters = runSettingsDocument.XPathSelectElements("RunSettings/TestRunParameters/Parameter").ToArray();
        Assert.AreEqual(testRunParameters[0].Attribute("name")!.Value, "key1");
        Assert.AreEqual(testRunParameters[0].Attribute("value")!.Value, "value1");
        Assert.AreEqual(testRunParameters[1].Attribute("name")!.Value, "key2");
        Assert.AreEqual(testRunParameters[1].Attribute("value")!.Value, "value2");
    }
}
