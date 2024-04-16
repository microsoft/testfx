// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Xml;

using FluentAssertions;

using TestFramework.ForTestingMSTest;

namespace Microsoft.MSTestV2.CLIAutomation;

public partial class CLITestBase : TestContainer
{
    private const string EngineeringFolder = "eng";

    private const string Configuration =
#if DEBUG
        "Debug";
#else
        "Release";
#endif

#pragma warning disable IDE0051 // Remove unused private members
    private const string TestPlatformCLIPackageName = "Microsoft.TestPlatform";
#pragma warning restore IDE0051 // Remove unused private members
    private const string DefaultTargetFramework = "net462";

    protected static XmlDocument ReadVersionProps()
    {
        var versionPropsFilePath = Path.Combine(GetArtifactsBinFolderPath(), "..", "..", EngineeringFolder, "Versions.props");
        using var fileStream = File.OpenRead(versionPropsFilePath);
#pragma warning disable CA3075 // Insecure DTD processing in XML
        using var xmlTextReader = new XmlTextReader(fileStream) { Namespaces = false };
#pragma warning restore CA3075 // Insecure DTD processing in XML
        var versionPropsXml = new XmlDocument();
        versionPropsXml.Load(xmlTextReader);

        return versionPropsXml;
    }

    protected static string GetTestPlatformVersion()
    {
        var versionPropsXml = ReadVersionProps();
        var testSdkVersion = versionPropsXml.DocumentElement.SelectSingleNode($"PropertyGroup/MicrosoftNETTestSdkVersion");

        return testSdkVersion.InnerText;
    }

    protected static string GetArtifactsBinFolderPath()
    {
        var assemblyLocation = System.Reflection.Assembly.GetExecutingAssembly().Location;

        var artifactsBinFolder = Path.GetFullPath(Path.Combine(assemblyLocation, @"..\..\..\.."));
        Directory.Exists(artifactsBinFolder).Should().BeTrue();

        return artifactsBinFolder;
    }

    protected static string GetArtifactsTestResultsFolderPath()
    {
        var assemblyLocation = System.Reflection.Assembly.GetExecutingAssembly().Location;

        var artifactsFolder = Path.GetFullPath(Path.Combine(assemblyLocation, @"..\..\..\..\.."));
        Directory.Exists(artifactsFolder).Should().BeTrue();

        var testResultsFolder = Path.Combine(artifactsFolder, "TestResults", Configuration);
        Directory.CreateDirectory(testResultsFolder);

        return testResultsFolder;
    }

    protected static string GetAssetFullPath(string assetName, string configuration = null, string targetFramework = null)
    {
        configuration ??= Configuration;
        targetFramework ??= DefaultTargetFramework;
        var assetPath = Path.GetFullPath(Path.Combine(GetArtifactsBinFolderPath(), assetName, configuration, targetFramework, assetName + ".dll"));
        File.Exists(assetPath).Should().BeTrue($"asset '{assetPath}' should exist");

        return assetPath;
    }

    /// <summary>
    /// Gets the RunSettingXml having testadapterpath filled in specified by argument.
    /// Inserts testAdapterPath in existing runSetting if not present already,
    /// or generates new runSettings with testAdapterPath if runSettings is Empty.
    /// </summary>
    /// <param name="settingsXml">RunSettings provided for discovery/execution.</param>
    /// <returns>RunSettingXml as string.</returns>
    protected static string GetRunSettingXml(string settingsXml)
    {
        if (string.IsNullOrEmpty(settingsXml))
        {
            settingsXml = XmlRunSettingsUtilities.CreateDefaultRunSettings();
        }

        XmlDocument doc = new();
        using (var xmlReader = XmlReader.Create(new StringReader(settingsXml), new XmlReaderSettings() { XmlResolver = null, CloseInput = true }))
        {
            doc.Load(xmlReader);
        }

        XmlElement root = doc.DocumentElement;
        RunConfiguration runConfiguration = new(string.Empty) { TestResultsDirectory = GetArtifactsTestResultsFolderPath() };
        XmlElement runConfigurationElement = runConfiguration.ToXml();
        if (root[runConfiguration.SettingsName] == null)
        {
            XmlNode newNode = doc.ImportNode(runConfigurationElement, true);
            root.AppendChild(newNode);
        }
        else
        {
            XmlNode newNode = doc.ImportNode(runConfigurationElement.FirstChild, true);
            root[runConfiguration.SettingsName].AppendChild(newNode);
        }

        return doc.OuterXml;
    }
}
