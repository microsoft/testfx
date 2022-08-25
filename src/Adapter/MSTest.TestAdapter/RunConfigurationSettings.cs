﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter
{
    using System;
    using System.IO;
    using System.Xml;

    using Microsoft.VisualStudio.TestPlatform.ObjectModel;
    using Microsoft.VisualStudio.TestPlatform.ObjectModel.Adapter;
    using Microsoft.VisualStudio.TestPlatform.ObjectModel.Utilities;

    public class RunConfigurationSettings
    {
        /// <summary>
        /// The settings name.
        /// </summary>
        public const string SettingsName = "RunConfiguration";

        /// <summary>
        /// Initializes a new instance of the <see cref="RunConfigurationSettings"/> class.
        /// </summary>
        public RunConfigurationSettings()
        {
            this.CollectSourceInformation = true;
        }

        /// <summary>
        /// Gets a value indicating whether source information needs to be collected or not.
        /// </summary>
        public bool CollectSourceInformation { get; private set; }

        /// <summary>
        /// Populate adapter settings from the context
        /// </summary>
        /// <param name="context">
        /// The discovery context that contains the runsettings.
        /// </param>
        /// <returns>Populated RunConfigurationSettings from the discovery context.</returns>
        public static RunConfigurationSettings PopulateSettings(IDiscoveryContext context)
        {
            if (context == null || context.RunSettings == null || string.IsNullOrEmpty(context.RunSettings.SettingsXml))
            {
                // This will contain default configuration settings
                return new RunConfigurationSettings();
            }

            var settings = GetSettings(context.RunSettings.SettingsXml, SettingsName);

            if (settings != null)
            {
                return settings;
            }

            return new RunConfigurationSettings();
        }

        /// <summary>
        /// Gets the configuration settings from the xml.
        /// </summary>
        /// <param name="runsettingsXml"> The xml with the settings passed from the test platform. </param>
        /// <param name="settingName"> The name of the settings to fetch.</param>
        /// <returns> The settings if found. Null otherwise. </returns>
        internal static RunConfigurationSettings GetSettings(string runsettingsXml, string settingName)
        {
            using (var stringReader = new StringReader(runsettingsXml))
            {
                XmlReader reader = XmlReader.Create(stringReader, XmlRunSettingsUtilities.ReaderSettings);

                // read to the fist child
                XmlReaderUtilities.ReadToRootNode(reader);
                reader.ReadToNextElement();

                // Read till we reach nodeName element or reach EOF
                while (!string.Equals(reader.Name, settingName, StringComparison.OrdinalIgnoreCase)
                        &&
                        !reader.EOF)
                {
                    reader.SkipToNextElement();
                }

                if (!reader.EOF)
                {
                    // read nodeName element.
                    return ToSettings(reader.ReadSubtree());
                }
            }

            return null;
        }

        /// <summary>
        /// Convert the parameter xml to TestSettings
        /// </summary>
        /// <param name="reader">Reader to load the settings from.</param>
        /// <returns>An instance of the <see cref="MSTestSettings"/> class</returns>
        private static RunConfigurationSettings ToSettings(XmlReader reader)
        {
            ValidateArg.NotNull<XmlReader>(reader, "reader");

            // Expected format of the xml is: -
            //
            // <Runsettings>
            // <RunConfiguration>
            // <CollectSourceInformation>true</CollectSourceInformation>
            // </RunConfiguration>
            // </Runsettings>
            RunConfigurationSettings settings = new RunConfigurationSettings();

            // Read the first element in the section
            reader.ReadToNextElement();

            if (!reader.IsEmptyElement)
            {
                reader.Read();

                while (reader.NodeType == XmlNodeType.Element)
                {
                    string elementName = reader.Name.ToUpperInvariant();
                    switch (elementName)
                    {
                        case "COLLECTSOURCEINFORMATION":
                            {
                                if (bool.TryParse(reader.ReadInnerXml(), out var result))
                                {
                                    settings.CollectSourceInformation = result;
                                    PlatformServiceProvider.Instance.AdapterTraceLogger.LogInfo(
                                    "CollectSourceInformation value Found : {0} ",
                                    result);
                                }

                                break;
                            }

                        default:
                            {
                                reader.SkipToNextElement();
                                break;
                            }
                    }
                }
            }

            return settings;
        }
    }
}
