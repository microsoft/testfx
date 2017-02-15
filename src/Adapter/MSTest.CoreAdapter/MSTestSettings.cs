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

    /// <summary>
    /// Adapter Settings for the run
    /// </summary>
    public class MSTestSettings
    {
        /// <summary>
        /// The settings name.
        /// </summary>
        public const string SettingsName = "MSTest";

        /// <summary>
        /// The alias to the default settings name.
        /// </summary>
        public const string SettingsNameAlias = "MSTestV2";

        /// <summary>
        /// Member variable for Adapter settings
        /// </summary>
        private static MSTestSettings currentSettings;

        /// <summary>
        /// Initializes a new instance of the <see cref="MSTestSettings"/> class.
        /// </summary>
        public MSTestSettings()
        {
            this.CaptureDebugTraces = true;
            this.MapInconclusiveToFailed = false;
            this.ForcedLegacyMode = false;
            this.TestSettingsFile = null;
        }

        /// <summary>
        /// Gets the current settings.
        /// </summary>
        public static MSTestSettings CurrentSettings
        {
            get
            {
                if (currentSettings == null)
                {
                    currentSettings = new MSTestSettings();
                }

                return currentSettings;
            }

            private set
            {
                currentSettings = value;
            }
        }

        /// <summary>
        /// Gets or sets a value indicating whether capture debug traces.
        /// </summary>
        public bool CaptureDebugTraces { get; set; }

        /// <summary>
        /// Gets a value indicating whether user wants the adapter to run in legacy mode or not.
        /// Default is False.
        /// </summary>
        public bool ForcedLegacyMode { get; private set; }

        /// <summary>
        /// Gets the path to settings file.
        /// </summary>
        public string TestSettingsFile { get; private set; }

        /// <summary>
        /// Gets a value indicating whether an inconclusive result be mapped to failed test.
        /// </summary>
        public bool MapInconclusiveToFailed { get; private set; }

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
                CurrentSettings = new MSTestSettings();
                return;
            }

            var aliasSettings = GetSettings(context.RunSettings.SettingsXml, SettingsNameAlias);

            // If a user specifies MSTestV2 in the runsettings, then prefer that over the v1 settings.
            if (aliasSettings != null)
            {
                CurrentSettings = aliasSettings;
                return;
            }
            else
            {
                var settings = GetSettings(context.RunSettings.SettingsXml, SettingsName);

                if (settings != null)
                {
                    CurrentSettings = settings;
                    return;
                }

                CurrentSettings = new MSTestSettings();
            }
        }

        /// <summary>
        /// Get the MSTestV1 adapter settings from the context
        /// </summary>
        /// <param name="logger"> The logger for messages. </param>
        /// <returns> Returns true if test settings is provided.. </returns>
        public static bool IsLegacyScenario(IMessageLogger logger)
        {
            if (CurrentSettings.ForcedLegacyMode || !string.IsNullOrEmpty(CurrentSettings.TestSettingsFile))
            {
                logger.SendMessage(TestMessageLevel.Warning, Resource.LegacyScenariosNotSupportedWarning);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Gets the adapter specific settings from the xml.
        /// </summary>
        /// <param name="runsettingsXml"> The xml with the settings passed from the test platform. </param>
        /// <param name="settingName"> The name of the adapter settings to fetch - Its either MSTest or MSTestV2 </param>
        /// <returns> The settings if found. Null otherwise. </returns>
        internal static MSTestSettings GetSettings(string runsettingsXml, string settingName)
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
            MSTestSettings.CurrentSettings = null;
        }

        /// <summary>
        /// Convert the parameter xml to TestSettings
        /// </summary>
        /// <param name="reader">Reader to load the settings from.</param>
        /// <returns>An instance of the <see cref="MSTestSettings"/> class</returns>
        private static MSTestSettings ToSettings(XmlReader reader)
        {
            ValidateArg.NotNull<XmlReader>(reader, "reader");

            // Expected format of the xml is: -
            //
            // <MSTestV2>
            //     <CaptureTraceOutput>true</CaptureTraceOutput>
            //     <MapInconclusiveToFailed>false</MapInconclusiveToFailed>
            // </MSTestV2>
            //
            // (or)
            //
            // <MSTest>
            //     <ForcedLegacyMode>true</ForcedLegacyMode>
            //     <SettingsFile>..\..\Local.testsettings</SettingsFile>
            //     <CaptureTraceOutput>true</CaptureTraceOutput>
            // </MSTest>
            MSTestSettings settings = new MSTestSettings();

            // Read the first element in the section which is either "MSTest"/"MSTestV2"
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
                        case "MAPINCONCLUSIVETOFAILED":
                            {
                                if (bool.TryParse(reader.ReadInnerXml(), out result))
                                {
                                    settings.MapInconclusiveToFailed = result;
                                }

                                break;
                            }

                        case "FORCEDLEGACYMODE":
                            {
                                if (bool.TryParse(reader.ReadInnerXml(), out result))
                                {
                                    settings.ForcedLegacyMode = result;
                                }

                                break;
                            }

                        case "SETTINGSFILE":
                            {
                                string fileName = reader.ReadInnerXml();

                                if (!string.IsNullOrEmpty(fileName))
                                {
                                    settings.TestSettingsFile = fileName;
                                }

                                break;
                            }

                        default:
                            {
                                PlatformServiceProvider.Instance.SettingsProvider.Load(reader.ReadSubtree());
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
