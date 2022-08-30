// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.MSTestV2.CLIAutomation;

using System.Xml;

/// <summary>
/// Stores information about a run setting.
/// </summary>
public class RunConfiguration
{
    /// <summary>
    /// Gets or sets name of RunConfiguration settings node in RunSettings.
    /// </summary>
    public string SettingsName { get; set; }

    /// <summary>
    /// Gets the paths at which engine should look for test adapters
    /// </summary>
    public string[] TestAdaptersPaths { get; }

    public RunConfiguration(params string[] testAdapterPaths)
    {
        this.SettingsName = Constants.RunConfigurationSettingsName;
        this.TestAdaptersPaths = testAdapterPaths;
    }

    /// <summary>
    /// Converts the setting to be an XmlElement.
    /// </summary>
    /// <returns>The XmlElement instance.</returns>
    public XmlElement ToXml()
    {
        XmlDocument doc = new();
        XmlElement root = doc.CreateElement(this.SettingsName);

        foreach (var p in this.TestAdaptersPaths)
        {
            var testAdaptersPaths = doc.CreateElement("TestAdaptersPaths");
            testAdaptersPaths.InnerText = p;

            root.AppendChild(testAdaptersPaths);
        }

        return root;
    }
}
