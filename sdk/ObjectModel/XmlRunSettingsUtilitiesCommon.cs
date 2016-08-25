using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security;
using System.Xml;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using System.Runtime.InteropServices;
#if !WINDOWS_UAP
#if SILVERLIGHT
using vstest_executionengine_platformbridge;
#else
using System.Xml.XPath;
#endif
#endif

namespace Microsoft.VisualStudio.TestPlatform.ObjectModel.Utilities
{

    public static partial class XmlRunSettingsUtilities
    {
#if !SILVERLIGHT
        /// <summary>
        /// Examines the given XPathNavigable representation of a runsettings file and determines if it has a configuration node
        /// for the data collector (used for Fakes and CodeCoverage)
        /// </summary>
        /// <param name="runSettingDocument">XPathNavigable representation of a runsettings file</param>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1054:UriParametersShouldNotBeStrings", MessageId = "1#")]
        public static bool ContainsDataCollector(IXPathNavigable runSettingDocument, string dataCollectorUri)
        {
            if (runSettingDocument == null)
                throw new ArgumentNullException("runSettingDocument");
            if (dataCollectorUri == null)
                throw new ArgumentNullException("dataCollectorUri");

            var navigator = runSettingDocument.CreateNavigator();
            var nodes = navigator.Select("/RunSettings/DataCollectionRunSettings/DataCollectors/DataCollector");
            foreach (XPathNavigator dataCollectorNavigator in nodes)
            {
                string uri = dataCollectorNavigator.GetAttribute("uri", string.Empty);
                if (string.Equals(dataCollectorUri, uri, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }


        /// <summary>
        /// Moves the given runsettings file navigator to the DataCollectors node in the runsettings xml.
        /// Throws XmlException if it was unable to find the DataCollectors node.
        /// </summary>
        /// <param name="runSettingsNavigator">XPathNavigator for a runsettings xml document.</param>
        /// <returns>Navigator with its current node set to the DataCollectors node.</returns>
        static XPathNavigator MoveToDataCollectorsNode(XPathNavigator runSettingsNavigator)
        {
            Debug.Assert(runSettingsNavigator != null);

            runSettingsNavigator.MoveToRoot();
            if (!runSettingsNavigator.MoveToChild("RunSettings", string.Empty))
            {
                throw new XmlException(string.Format(CultureInfo.CurrentCulture, Resources.CouldNotFindXmlNode, "RunSettings"));
            }

            if (!runSettingsNavigator.MoveToChild("DataCollectionRunSettings", string.Empty))
            {
                runSettingsNavigator.AppendChildElement(string.Empty, "DataCollectionRunSettings", string.Empty, string.Empty);
                runSettingsNavigator.MoveToChild("DataCollectionRunSettings", string.Empty);
            }
            if (!runSettingsNavigator.MoveToChild("DataCollectors", string.Empty))
            {
                runSettingsNavigator.AppendChildElement(string.Empty, "DataCollectors", string.Empty, string.Empty);
                runSettingsNavigator.MoveToChild("DataCollectors", string.Empty);
            }
            return runSettingsNavigator;
        }
#endif

#if SILVERLIGHT && !WINDOWS_UAP
        [DllImport("vstest_executionengine_platformbridge.dll")]
        internal static extern string GetProcessorArchitecture();
#endif

		/// <summary>
		/// Returns the OS Architecture type.
		/// </summary>
		/// <returns></returns>

		[System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Security", "CA2136:TransparencyAnnotationsShouldNotConflictFxCopRule")]
		public static Architecture OSArchitecture
        {
            [SecuritySafeCritical]
            get
            {
#if dotnet
                return Architecture.AnyCPU;
#elif WINDOWS_UAP
                var architecture = Windows.ApplicationModel.Package.Current.Id.Architecture;
                switch(architecture)
                {
                    case Windows.System.ProcessorArchitecture.X86:  return Architecture.X86;
                    case Windows.System.ProcessorArchitecture.X64: return Architecture.X64;
                    case Windows.System.ProcessorArchitecture.Arm: return Architecture.ARM;
                    case Windows.System.ProcessorArchitecture.Neutral: return Architecture.AnyCPU;
                    default:
                    case Windows.System.ProcessorArchitecture.Unknown: return Architecture.Default;
                }
#else

#if SILVERLIGHT
                string proc_arch = GetProcessorArchitecture();
                string proc_arch6432 = String.Empty;
#else
                string proc_arch = Environment.GetEnvironmentVariable("PROCESSOR_ARCHITECTURE");
                string proc_arch6432 = Environment.GetEnvironmentVariable("PROCESSOR_ARCHITEW6432");
#endif
                if (string.Equals(proc_arch, "amd64", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(proc_arch, "ia64", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(proc_arch6432, "amd64", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(proc_arch6432, "ia64", StringComparison.OrdinalIgnoreCase))
                {
                    return Architecture.X64;
                }
                if (string.Equals(proc_arch, "x86", StringComparison.OrdinalIgnoreCase))
                {
                    return Architecture.X86;
                }
                return Architecture.ARM;
#endif
            }
        }


        /// <summary>
        /// Settings to be used while creating XmlReader for runsettings.
        /// </summary>
        public static XmlReaderSettings ReaderSettings
        {
            get
            {
                var settings = new XmlReaderSettings();
                settings.IgnoreComments = true;
                settings.IgnoreWhitespace = true;
#if !SILVERLIGHT
                settings.XmlResolver = null;
#endif
                //settings.ProhibitDtd = true;
                return settings;
            }
        }

        /// <summary>
        /// Returns RunConfiguration from settingsXml. 
        /// </summary>
        /// <param name="settingsXml"></param>
        /// <returns></returns>
        /// 
        public static RunConfiguration GetRunConfigurationNode(string settingsXml)
        {
            var nodeValue = GetNodeValue<RunConfiguration>(settingsXml, Constants.RunConfigurationSettingsName, RunConfiguration.FromXml);
            if (nodeValue == default(RunConfiguration))
            {
                //Return default one.
                nodeValue = new RunConfiguration();
            }
            return nodeValue;
        }

        /// <summary>
        /// Gets the set of user defined test run parameters from settings xml as key value pairs.
        /// </summary>
        /// <param name="settingsXml"></param>
        /// <returns></returns>
        /// <remarks>If there is no test run parameters section defined in the settingsxml a blank dictionary is returned.</remarks>
        public static Dictionary<string, object> GetTestRunParameters(string settingsXml)
        {
            var nodeValue = GetNodeValue<Dictionary<string, object>>(settingsXml, Constants.TestRunParametersName, TestRunParameters.FromXml);
            if (nodeValue == default(Dictionary<string, object>))
            {
                //Return default.
                nodeValue = new Dictionary<string, object>();
            }
            return nodeValue;
        }

#if !SILVERLIGHT
        /// <summary>
        /// Returns a value that indicates if the Fakes data collector is already configured  in the settings.
        /// </summary>
        /// <param name="runSettings"></param>
        /// <returns></returns>

        public static bool ContainsFakesDataCollector(IXPathNavigable runSettings)
        {
            if (runSettings == null)
                throw new ArgumentNullException("runSettings");

            return XmlRunSettingsUtilities.ContainsDataCollector(runSettings, FakesMetadata.DataCollectorUri);
        }
#endif

        internal static void ThrowOnHasAttributes(XmlReader reader)
        {
            if (reader.HasAttributes)
            {
                reader.MoveToNextAttribute();
                throw new SettingsException(String.Format(CultureInfo.CurrentCulture,
                                    Resources.InvalidSettingsXmlAttribute, Constants.RunConfigurationSettingsName, reader.Name));
            }
        }

        private static T GetNodeValue<T>(string settingsXml, string nodeName, Func<XmlReader, T> nodeParser)
        {
            // use XmlReader to avoid loading of the plugins in client code (mainly from VS).
            if (!StringUtilities.IsNullOrWhiteSpace(settingsXml))
            {
                using (StringReader stringReader = new StringReader(settingsXml))
                {
                    XmlReader reader = XmlReader.Create(stringReader, ReaderSettings);

                    // read to the fist child
                    XmlReaderUtilities.ReadToRootNode(reader);
                    reader.ReadToNextElement();

                    // Read till we reach nodeName element or reach EOF
                    while (!string.Equals(reader.Name, nodeName, StringComparison.OrdinalIgnoreCase)
                            &&
                            !reader.EOF)
                    {
                        reader.SkipToNextElement();
                    }

                    if (!reader.EOF)
                    {
                        // read nodeName element.
                        return nodeParser(reader);
                    }
                }
            }
            return default(T);
        }

        static class FakesMetadata
        {
            /// <summary>
            /// Friendly name of the data collector
            /// </summary>
            public const string FriendlyName = "UnitTestIsolation";
            /// <summary>
            /// Gets the URI of the data collector
            /// </summary>
            public const string DataCollectorUri = "datacollector://microsoft/unittestisolation/1.0";
            /// <summary>
            /// Gets the assembly qualified name of the data collector type
            /// </summary>
            public const string DataCollectorAssemblyQualifiedName = "Microsoft.VisualStudio.TraceCollector.UnitTestIsolationDataCollector, Microsoft.VisualStudio.TraceCollector, Version=11.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a";
        }
    }

}
