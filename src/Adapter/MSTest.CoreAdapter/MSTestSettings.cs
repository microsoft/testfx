// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter
{
    using System;
    using System.Globalization;
    using System.IO;
    using System.Xml;
    using System.Xml.Linq;

    using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;
    using Microsoft.VisualStudio.TestPlatform.ObjectModel;
    using Microsoft.VisualStudio.TestPlatform.ObjectModel.Adapter;
    using Microsoft.VisualStudio.TestPlatform.ObjectModel.Logging;
    using Microsoft.VisualStudio.TestPlatform.ObjectModel.Utilities;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// Adapter Settings for the run
    /// </summary>
    [Serializable]
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

        private const string ParallelizeSettingsName = "Parallelize";

        /// <summary>
        /// Member variable for Adapter settings
        /// </summary>
        private static MSTestSettings currentSettings;

        /// <summary>
        /// Member variable for RunConfiguration settings
        /// </summary>
        private static RunConfigurationSettings runConfigurationSettings;

        /// <summary>
        /// Initializes a new instance of the <see cref="MSTestSettings"/> class.
        /// </summary>
        public MSTestSettings()
        {
            this.CaptureDebugTraces = true;
            this.MapInconclusiveToFailed = false;
            this.EnableBaseClassTestMethodsFromOtherAssemblies = true;
            this.ForcedLegacyMode = false;
            this.TestSettingsFile = null;
            this.DisableParallelization = false;
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
        /// Gets the current configuration settings.
        /// </summary>
        public static RunConfigurationSettings RunConfigurationSettings
        {
            get
            {
                if (runConfigurationSettings == null)
                {
                    runConfigurationSettings = new RunConfigurationSettings();
                }

                return runConfigurationSettings;
            }

            private set
            {
                runConfigurationSettings = value;
            }
        }

        /// <summary>
        /// Gets a value indicating whether capture debug traces.
        /// </summary>
        public bool CaptureDebugTraces { get; private set; }

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
        /// Gets a value indicating whether to enable discovery of test methods from base classes in a different assembly from the inheriting test class.
        /// </summary>
        public bool EnableBaseClassTestMethodsFromOtherAssemblies { get; private set; }

        /// <summary>
        /// Gets the number of threads/workers to be used for parallelization.
        /// </summary>
        public int? ParallelizationWorkers { get; private set; }

        /// <summary>
        /// Gets the scope of parallelization.
        /// </summary>
        public ExecutionScope? ParallelizationScope { get; private set; }

        /// <summary>
        /// Gets a value indicating whether the assembly can be parallelized.
        /// </summary>
        /// <remarks>
        /// This is also re-used to disable parallelization on format errors
        /// </remarks>
        public bool DisableParallelization { get; private set; }

        /// <summary>
        /// Populate settings based on existing settings object.
        /// </summary>
        /// <param name="settings">The existing settings object.</param>
        public static void PopulateSettings(MSTestSettings settings)
        {
            CurrentSettings.CaptureDebugTraces = settings.CaptureDebugTraces;
            CurrentSettings.ForcedLegacyMode = settings.ForcedLegacyMode;
            CurrentSettings.TestSettingsFile = settings.TestSettingsFile;
            CurrentSettings.MapInconclusiveToFailed = settings.MapInconclusiveToFailed;
            CurrentSettings.EnableBaseClassTestMethodsFromOtherAssemblies = settings.EnableBaseClassTestMethodsFromOtherAssemblies;
            CurrentSettings.ParallelizationWorkers = settings.ParallelizationWorkers;
            CurrentSettings.ParallelizationScope = settings.ParallelizationScope;
            CurrentSettings.DisableParallelization = settings.DisableParallelization;
        }

        /// <summary>
        /// Populate adapter settings from the context
        /// </summary>
        /// <param name="context">
        /// The discovery context that contains the runsettings.
        /// </param>
        public static void PopulateSettings(IDiscoveryContext context)
        {
            RunConfigurationSettings = RunConfigurationSettings.PopulateSettings(context);

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
            }
            else
            {
                var settings = GetSettings(context.RunSettings.SettingsXml, SettingsName);

                if (settings != null)
                {
                    CurrentSettings = settings;
                }
                else
                {
                    CurrentSettings = new MSTestSettings();
                }
            }

            SetGlobalSettings(context.RunSettings.SettingsXml, CurrentSettings);
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
            MSTestSettings.RunConfigurationSettings = null;
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
            //     <EnableBaseClassTestMethodsFromOtherAssemblies>false</EnableBaseClassTestMethodsFromOtherAssemblies>
            //     <Parallelize>
            //        <Workers>4</Workers>
            //        <Scope>TestClass</Scope>
            //     </Parallelize>
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
                        case "CAPTURETRACEOUTPUT":
                            {
                                if (bool.TryParse(reader.ReadInnerXml(), out result))
                                {
                                    settings.CaptureDebugTraces = result;
                                }

                                break;
                            }

                        case "ENABLEBASECLASSTESTMETHODSFROMOTHERASSEMBLIES":
                            {
                                if (bool.TryParse(reader.ReadInnerXml(), out result))
                                {
                                    settings.EnableBaseClassTestMethodsFromOtherAssemblies = result;
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

                        case "MAPINCONCLUSIVETOFAILED":
                            {
                                if (bool.TryParse(reader.ReadInnerXml(), out result))
                                {
                                    settings.MapInconclusiveToFailed = result;
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

                        case "PARALLELIZE":
                            {
                                SetParallelSettings(reader.ReadSubtree(), settings);
                                reader.SkipToNextElement();

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

        private static void SetParallelSettings(XmlReader reader, MSTestSettings settings)
        {
            reader.Read();
            if (!reader.IsEmptyElement)
            {
                // Read the first child.
                reader.Read();

                while (reader.NodeType == XmlNodeType.Element)
                {
                    string elementName = reader.Name.ToUpperInvariant();
                    switch (elementName)
                    {
                        case "WORKERS":
                            {
                                var value = reader.ReadInnerXml();
                                if (int.TryParse(value, out int parallelWorkers))
                                {
                                    if (parallelWorkers == 0)
                                    {
                                        settings.ParallelizationWorkers = Environment.ProcessorCount;
                                    }
                                    else if (parallelWorkers > 0)
                                    {
                                        settings.ParallelizationWorkers = parallelWorkers;
                                    }
                                    else
                                    {
                                        throw new AdapterSettingsException(
                                        string.Format(
                                            CultureInfo.CurrentCulture,
                                            Resource.InvalidParallelWorkersValue,
                                            value));
                                    }
                                }
                                else
                                {
                                    throw new AdapterSettingsException(
                                        string.Format(
                                            CultureInfo.CurrentCulture,
                                            Resource.InvalidParallelWorkersValue,
                                            value));
                                }

                                break;
                            }

                        case "SCOPE":
                            {
                                var value = reader.ReadInnerXml();
                                if (Enum.TryParse(value, out ExecutionScope scope))
                                {
                                    settings.ParallelizationScope = scope;
                                }
                                else
                                {
                                    throw new AdapterSettingsException(
                                        string.Format(
                                            CultureInfo.CurrentCulture,
                                            Resource.InvalidParallelScopeValue,
                                            value,
                                            string.Join(", ", Enum.GetNames(typeof(ExecutionScope)))));
                                }

                                break;
                            }

                        default:
                            {
                                throw new AdapterSettingsException(
                                    string.Format(
                                        CultureInfo.CurrentCulture,
                                        Resource.InvalidSettingsXmlElement,
                                        ParallelizeSettingsName,
                                        reader.Name));
                            }
                    }
                }
            }

            // If any of these properties are not set, resort to the defaults.
            if (!settings.ParallelizationWorkers.HasValue)
            {
                settings.ParallelizationWorkers = Environment.ProcessorCount;
            }

            if (!settings.ParallelizationScope.HasValue)
            {
                settings.ParallelizationScope = ExecutionScope.ClassLevel;
            }
        }

        private static void SetGlobalSettings(string runsettingsXml, MSTestSettings settings)
        {
            var runconfigElement = XDocument.Parse(runsettingsXml)?.Element("RunSettings")?.Element("RunConfiguration");

            if (runconfigElement == null)
            {
                return;
            }

            var disableParallelizationString = runconfigElement.Element("DisableParallelization")?.Value;
            if (bool.TryParse(disableParallelizationString, out bool disableParallelization))
            {
                settings.DisableParallelization = disableParallelization;
            }
        }
    }
}
