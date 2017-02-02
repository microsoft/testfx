// Copyright (c) Microsoft Corporation. All rights reserved.

namespace Microsoft.MSTestV2.CLIAutomation
{
    using System.Xml;

    /// <summary>
    /// Stores information about a run setting.
    /// </summary>
    public class RunConfiguration
    {
        /// <summary>
        /// Name of RunConfiguration settings node in RunSettings.
        /// </summary>
        public string SettingsName { get; set; }

        /// <summary>
        /// Paths at which enigine should look for test adapters
        /// </summary>
        public string TestAdaptersPaths { get; set; }

        public RunConfiguration(string testAdapterPaths)
        {
            SettingsName = Constants.RunConfigurationSettingsName;
            TestAdaptersPaths = testAdapterPaths;
        }

        /// <summary>
        /// Converts the setting to be an XmlElement.
        /// </summary>
        /// <returns></returns>
        public XmlElement ToXml()
        {
            XmlDocument doc = new XmlDocument();
            XmlElement root = doc.CreateElement(SettingsName);

            var testAdaptersPaths = doc.CreateElement("TestAdaptersPaths");
            testAdaptersPaths.InnerXml = this.TestAdaptersPaths;
            root.AppendChild(testAdaptersPaths);

            return root;
        }
    }


}
