// Copyright (c) Microsoft Corporation. All rights reserved.

namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter
{
    using System.Xml;

    using Microsoft.VisualStudio.TestPlatform.ObjectModel;
    using Microsoft.VisualStudio.TestPlatform.ObjectModel.Adapter;
    using System.IO;
    /// <summary>
    /// Settings Provider.
    /// </summary>
    [SettingsName(SettingsName)]
    public class MSTestAdapterSettingsProvider : ISettingsProvider
    {
        /// <summary>
        /// The settings name.
        /// </summary>
        public const string SettingsName = "MSTestV2";

        /// <summary>
        /// Member variable for Adapter settings
        /// </summary>
        public MSTestSettings Settings { get; private set; }

        /// <summary>
        /// Load the settings from the reader.
        /// </summary>
        /// <param name="reader">Reader to load the settings from.</param>
        public void Load(XmlReader reader)
        {
            ValidateArg.NotNull<XmlReader>(reader, "reader");

            Settings = MSTestSettings.ToSettings(reader);
        }
    }

    /// <summary>
    /// Adapter Settings for the run
    /// </summary>
    public class MSTestSettings
    {
        /// <summary>
        /// Gets or sets a value indicating whether capture debug traces.
        /// </summary>
        public bool CaptureDebugTraces { get; set; }

        /// <summary>
        /// Specifies whether an inconclusive result be mapped to failed test.
        /// </summary>
        public bool MapInconclusiveToFailed { get; private set; }

        public MSTestSettings()
        {
            CaptureDebugTraces = true;
            MapInconclusiveToFailed = false;
        }

        /// <summary>
        /// Get the mstest adapter settings from the context
        /// </summary>
        public static MSTestSettings GetSettings(IDiscoveryContext context)
        {        
            if (context == null || context.RunSettings == null)
            {
                // This will contain default adapter settings
                return new MSTestSettings();
            }

            IRunSettings runSettings = context.RunSettings;
            MSTestAdapterSettingsProvider settingsProvider = runSettings.GetSettings(MSTestAdapterSettingsProvider.SettingsName) as MSTestAdapterSettingsProvider;
            if (settingsProvider == null)
            {
                return new MSTestSettings();
            }

            return settingsProvider.Settings;
        }


        /// <summary>
        /// Convert the parameter xml to TestSettings
        /// </summary>
        /// <param name="reader">Reader to load the settings from.</param>
        /// <returns>An instance of the <see cref="MSTestSettings"/> class</returns>
        public static MSTestSettings ToSettings(XmlReader reader)
        {
            // Go to the first element.
            reader.Read();

            ValidateArg.NotNull<XmlReader>(reader, "reader");

            // Expected format of the xml is: - 
            //
            // <MSTestV2>
            //     <CaptureTraceOutput>true</CaptureTraceOutput>
            //     <MapInconclusiveToFailed>false</MapInconclusiveToFailed>
            // </MSTestV2>

            MSTestSettings settings = new MSTestSettings();
            bool empty = reader.IsEmptyElement;
            reader.Read();

            if (!empty)
            {
                while (reader.NodeType == XmlNodeType.Element)
                {
                    bool result;
                    string elementName = reader.Name.ToUpperInvariant();
                    switch (elementName)
                    {
                        case "MAPINCONCLUSIVETOFAILED":
                            if (bool.TryParse(reader.ReadInnerXml(), out result))
                            {
                                settings.MapInconclusiveToFailed = result;
                            }
                            break;
                        default:
                            PlatformServiceProvider.Instance.SettingsProvider.Load(reader.ReadSubtree());
                            reader.ReadEndElement();
                            break;
                    }
                }
            }

            return settings;
        }
    }
}
