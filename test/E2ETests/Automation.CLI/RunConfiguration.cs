// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.MSTestV2.CLIAutomation
{
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
        /// Gets or sets paths at which engine should look for test adapters
        /// </summary>
        public string TestAdaptersPaths { get; set; }

        public RunConfiguration(string testAdapterPaths)
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
            XmlDocument doc = new XmlDocument();
            XmlElement root = doc.CreateElement(this.SettingsName);

            var testAdaptersPaths = doc.CreateElement("TestAdaptersPaths");
            testAdaptersPaths.InnerXml = this.TestAdaptersPaths;
            root.AppendChild(testAdaptersPaths);

            return root;
        }
    }
}
