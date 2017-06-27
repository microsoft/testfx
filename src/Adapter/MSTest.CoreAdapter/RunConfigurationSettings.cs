// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter
{
    using System;
    using System.IO;
    using System.Xml;

    using Microsoft.VisualStudio.TestPlatform.ObjectModel;
    using Microsoft.VisualStudio.TestPlatform.ObjectModel.Adapter;
    using Microsoft.VisualStudio.TestPlatform.ObjectModel.Logging;
    using Microsoft.VisualStudio.TestPlatform.ObjectModel.Utilities;

    public class RunConfigurationSettings
    {
        /// <summary>
        /// The settings name.
        /// </summary>
        public const string SettingsName = "RunConfiguration";

        /// <summary>
        /// Member variable for RunConfiguration settings
        /// </summary>
        private static RunConfigurationSettings configurationSettings;

        /// <summary>
        /// Initializes a new instance of the <see cref="RunConfigurationSettings"/> class.
        /// </summary>
        public RunConfigurationSettings()
        {
            this.DesignMode = true;
            this.CollectSourceInformation = true;
        }

        /// <summary>
        /// Gets the current settings.
        /// </summary>
        public static RunConfigurationSettings ConfigurationSettings
        {
            get
            {
                if (configurationSettings == null)
                {
                    configurationSettings = new RunConfigurationSettings();
                }

                return configurationSettings;
            }

            private set
            {
                configurationSettings = value;
            }
        }

        /// <summary>
        /// Gets a value indicating whether designMode is on(IDE scenario) or off(CLI scenario).
        /// </summary>
        public bool DesignMode { get; private set; }

        /// <summary>
        /// Gets a value indicating whether adapter should collect source information for discovered tests (non-Roslyn supported test projects) or not (Roslyn supported test projects).
        /// </summary>
        public bool CollectSourceInformation { get; private set; }

        /// <summary>
        /// Populate adapter settings from the context
        /// </summary>
        /// <param name="context">
        /// The discovery context that contains the runsettings.
        /// </param>
        public static void PopulateSettings(IDiscoveryContext context)
        {
            if (context == null || context.RunSettings == null || string.IsNullOrEmpty(context.RunSettings.SettingsXml))
            {
                // This will contain default adapter settings
                ConfigurationSettings = new RunConfigurationSettings();
                return;
            }

            var settings = GetSettings(context.RunSettings.SettingsXml, SettingsName);

            if (settings != null)
            {
                ConfigurationSettings = settings;
                return;
            }

            ConfigurationSettings = new RunConfigurationSettings();
        }

        /// <summary>
        /// Gets the adapter specific settings from the xml.
        /// </summary>
        /// <param name="runsettingsXml"> The xml with the settings passed from the test platform. </param>
        /// <param name="settingName"> The name of the adapter settings to fetch - Its either MSTest or MSTestV2 </param>
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
        /// Resets any settings loaded.
        /// </summary>
        internal static void Reset()
        {
            RunConfigurationSettings.ConfigurationSettings = null;
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
            //   <RunConfiguration>
            //     <DesignMode>true</DesignMode>
            //     <CollectSourceInformation>true</CollectSourceInformation>
            //   </RunConfiguration>
            // </Runsettings>
            RunConfigurationSettings settings = new RunConfigurationSettings();

            reader.ReadToNextElement();

            if (!reader.IsEmptyElement)
            {
                reader.Read();

                while (reader.NodeType == XmlNodeType.Element)
                {
                    bool result;
                    string elementName = reader.Name.ToUpperInvariant();
                    switch (elementName)
                    {
                        case "DESIGNMODE":
                            {
                                if (bool.TryParse(reader.ReadInnerXml(), out result))
                                {
                                    settings.DesignMode = result;
                                    PlatformServiceProvider.Instance.AdapterTraceLogger.LogInfo(
                                    "DesignMode value Found : {0} ",
                                    result);
                                }

                                break;
                            }

                        case "COLLECTSOURCEINFORMATION":
                            {
                                if (bool.TryParse(reader.ReadInnerXml(), out result))
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
