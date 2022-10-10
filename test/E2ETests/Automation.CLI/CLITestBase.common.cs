// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Xml;

using TestFramework.ForTestingMSTest;

namespace Microsoft.MSTestV2.CLIAutomation;
public partial class CLITestBase : TestContainer
{
    private const string TestAssetsFolder = "TestAssets";
    private const string ArtifactsFolder = "artifacts";
    private const string PackagesFolder = "packages";
    private const string EngineeringFolder = "eng";

    // This value is automatically updated by "build.ps1" script.
    private const string TestPlatformCLIPackageName = "Microsoft.TestPlatform";
    private const string VstestConsoleRelativePath = @"tools\net462\Common7\IDE\Extensions\TestPlatform\vstest.console.exe";

    protected XmlDocument ReadVersionProps()
    {
        var versionPropsFilePath = Path.Combine(Environment.CurrentDirectory, EngineeringFolder, "Versions.props");
        using var fileStream = File.OpenRead(versionPropsFilePath);
        using var xmlTextReader = new XmlTextReader(fileStream) { Namespaces = false };
        var versionPropsXml = new XmlDocument();
        versionPropsXml.Load(xmlTextReader);

        return versionPropsXml;
    }

    protected string GetTestPlatformVersion()
    {
        var versionPropsXml = ReadVersionProps();
        var testSdkVersion = versionPropsXml.DocumentElement.SelectSingleNode($"PropertyGroup/MicrosoftNETTestSdkVersion");

        return testSdkVersion.InnerText;
    }

    /// <summary>
    /// Gets the path of test assets folder.
    /// </summary>
    /// <returns>Path to testassets folder.</returns>
    protected string GetAssetFolderPath() => Path.Combine(Environment.CurrentDirectory, ArtifactsFolder, TestAssetsFolder);

    /// <summary>
    /// Gets the full path to a test asset.
    /// </summary>
    /// <param name="assetName">Name of the asset with extension. E.g. <c>SimpleUnitTest.dll</c>.</param>
    /// <returns>Full path to the test asset.</returns>
    /// <remarks>
    /// Test assets follow several conventions:
    /// (a) They are built for provided build configuration.
    /// (b) Name of the test asset matches the parent directory name. E.g. <c>TestAssets\SimpleUnitTest\SimpleUnitTest.xproj</c> must
    /// produce <c>TestAssets\SimpleUnitTest\bin\Debug\SimpleUnitTest.dll</c>
    /// (c) TestAssets are copied over to a central location i.e. "TestAssets\artifacts\*.*".
    /// </remarks>
    protected string GetAssetFullPath(string assetName)
    {
        var assetPath = Path.GetFullPath(Path.Combine(GetAssetFolderPath(), assetName));

        // GetTestAsset: Path not found.
        Verify(File.Exists(assetPath));
        return assetPath;
    }

    protected string GetTestAdapterPath()
    {
        var testAdapterPath = Path.Combine(Environment.CurrentDirectory, ArtifactsFolder, TestAssetsFolder);
        return testAdapterPath;
    }

    /// <summary>
    /// Gets the RunSettingXml having testadapterpath filled in specified by argument.
    /// Inserts testAdapterPath in existing runSetting if not present already,
    /// or generates new runSettings with testAdapterPath if runSettings is Empty.
    /// </summary>
    /// <param name="settingsXml">RunSettings provided for discovery/execution.</param>
    /// <param name="testAdapterPath">Full path to TestAdapter.</param>
    /// <returns>RunSettingXml as string.</returns>
    protected string GetRunSettingXml(string settingsXml, string testAdapterPath)
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
        RunConfiguration runConfiguration = new(testAdapterPath);
        XmlElement runConfigElement = runConfiguration.ToXml();
        if (root[runConfiguration.SettingsName] == null)
        {
            XmlNode newNode = doc.ImportNode(runConfigElement, true);
            root.AppendChild(newNode);
        }
        else
        {
            XmlNode newNode = doc.ImportNode(runConfigElement.FirstChild, true);
            root[runConfiguration.SettingsName].AppendChild(newNode);
        }

        return doc.OuterXml;
    }
}
