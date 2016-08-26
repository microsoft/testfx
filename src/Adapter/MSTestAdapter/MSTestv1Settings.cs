// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter
{
    using Microsoft.VisualStudio.TestPlatform.ObjectModel.Adapter;
    using Microsoft.VisualStudio.TestPlatform.ObjectModel;
    using Microsoft.VisualStudio.TestPlatform.ObjectModel.Utilities;

    using System;
    using System.IO;
    using System.Xml;
    using System.Diagnostics;
    using TestPlatform.ObjectModel.Logging;
    /// <summary>
    /// Adapter Settings for the run
    /// </summary>
    public class MSTestv1Settings
    {
        /// <summary>
        /// Specifies whether user wants the adapter to run in legacy mode or not. 
        /// Default is False.
        /// </summary>
        public bool ForcedLegacyMode { get; private set; }

        /// <summary>
        /// Specifies the path to settings file. 
        /// </summary>
        public string SettingsFile { get; private set; }

        public MSTestv1Settings()
        {
            ForcedLegacyMode = false;
        }

        /// <summary>
        /// Get the mstestv1 adapter settings from the context
        /// </summary>
        public static bool isTestSettingsGiven(IDiscoveryContext context, IMessageLogger logger)
        {
            if (context == null || context.RunSettings == null)
            {
                return false;
            }

            IRunSettings runSettings = context.RunSettings;
            StringReader stringReader = new StringReader(runSettings.SettingsXml);
            XmlReader reader = XmlReader.Create(stringReader, XmlRunSettingsUtilities.ReaderSettings);

            MSTestv1Settings v1adapterSettings =  ToSettings(reader);
            Debug.Assert(v1adapterSettings != null, "MSTestv1 Adapter settings should not be null.");

            if (v1adapterSettings.ForcedLegacyMode || !String.IsNullOrEmpty(v1adapterSettings.SettingsFile))
            {
                logger.SendMessage(TestMessageLevel.Warning, "Warning : .testsettings file is not supported in MSTestAdapter.");
                return true;
            }
            return false;
        }

        /// <summary>
        /// Convert the parameter xml to TestSettings
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Maintainability", "CA1502:AvoidExcessiveComplexity")
        ]
        public static MSTestv1Settings ToSettings(XmlReader reader)
        {

            //// Expected format of the xml is: - 
            ////
            //// <MSTest>
            ////     <ForcedLegacyMode>true</ForcedLegacyMode>
            ////     <SettingsFile>..\..\Local.testsettings</SettingsFile>
            //// </MSTest>

            MSTestv1Settings settings = new MSTestv1Settings();

            bool MSTestSettingsPresent = reader.ReadToFollowing("MSTest");
            reader.ReadToNextElement();
            if (MSTestSettingsPresent)
            {
                while (reader.NodeType == XmlNodeType.Element)
                {
                    bool result;
                    string elementName = reader.Name.ToUpperInvariant();
                    switch (elementName)
                    {
                        case "FORCEDLEGACYMODE":

                            if (bool.TryParse(reader.ReadInnerXml(), out result))
                            {
                                settings.ForcedLegacyMode = result;
                            }
                            break;
                        case "SETTINGSFILE":
                            string fileName = reader.ReadInnerXml();

                            if (!string.IsNullOrEmpty(fileName))
                            { 
                                settings.SettingsFile = fileName;
                            }
                            break;
                        default:
                            reader.Skip();
                            break;
                    }
                }
            }
            return settings;
        }
    }
}
