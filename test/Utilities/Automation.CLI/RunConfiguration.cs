// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Xml;

namespace Microsoft.MSTestV2.CLIAutomation;

/// <summary>
/// Stores information about a run setting.
/// </summary>
public class RunConfiguration
{
    public RunConfiguration(params string[] testAdapterPaths)
    {
        SettingsName = Constants.RunConfigurationSettingsName;
        TestAdaptersPaths = testAdapterPaths;
    }

    /// <summary>
    /// Gets or sets name of RunConfiguration settings node in RunSettings.
    /// </summary>
    public string SettingsName { get; set; }

    /// <summary>
    /// Gets the paths at which engine should look for test adapters.
    /// </summary>
    public string[] TestAdaptersPaths { get; }

    public string TestResultsDirectory { get; set; }

    /// <summary>
    /// Converts the setting to be an XmlElement.
    /// </summary>
    /// <returns>The XmlElement instance.</returns>
    public XmlElement ToXml()
    {
        XmlDocument doc = new();
        XmlElement root = doc.CreateElement(SettingsName);

        if (TestResultsDirectory is not null)
        {
            var resultsDirectory = doc.CreateElement("ResultsDirectory");
            resultsDirectory.InnerText = TestResultsDirectory;
            root.AppendChild(resultsDirectory);
        }

        foreach (var p in TestAdaptersPaths)
        {
            var testAdaptersPaths = doc.CreateElement("TestAdaptersPaths");
            testAdaptersPaths.InnerText = p;

            root.AppendChild(testAdaptersPaths);
        }

        return root;
    }
}
