// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.MSTestV2.CLIAutomation
{
    using System.Xml;

    public static class XmlRunSettingsUtilities
    {
        /// <summary>
        /// Create a default run settings
        /// </summary>
        /// <returns>The runsettings xml string.</returns>
        public static string CreateDefaultRunSettings()
        {
            // Create a new default xml doc that looks like this:
            // <?xml version="1.0" encoding="utf-8"?>
            // <RunSettings>
            //   <DataCollectionRunSettings>
            //     <DataCollectors>
            //     </DataCollectors>
            //   </DataCollectionRunSettings>
            // </RunSettings>
            var doc = new XmlDocument();
            XmlNode xmlDeclaration = doc.CreateNode(XmlNodeType.XmlDeclaration, string.Empty, string.Empty);
            doc.AppendChild(xmlDeclaration);
            XmlElement runSettingsNode = doc.CreateElement(Constants.RunSettingsName);
            doc.AppendChild(runSettingsNode);

            XmlElement dataCollectionRunSettingsNode = doc.CreateElement(Constants.DataCollectionRunSettingsName);
            runSettingsNode.AppendChild(dataCollectionRunSettingsNode);

            XmlElement dataCollectorsNode = doc.CreateElement(Constants.DataCollectorsSettingName);
            dataCollectionRunSettingsNode.AppendChild(dataCollectorsNode);

            return doc.OuterXml;
        }
    }

    /// <summary>
    /// Defines the defaults/constants used across different components.
    /// </summary>
    public static class Constants
    {
        /// <summary>
        /// Name of data collection settings node in RunSettings.
        /// </summary>
        public const string DataCollectionRunSettingsName = "DataCollectionRunSettings";

        /// <summary>
        /// Name of RunConfiguration settings node in RunSettings.
        /// </summary>
        public const string RunConfigurationSettingsName = "RunConfiguration";

        public const string DataCollectorsSettingName = "DataCollectors";

        public const string RunSettingsName = "RunSettings";
    }
}
