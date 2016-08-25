// ---------------------------------------------------------------------------
// <copyright file="XmlRunSettingsUtilities.cs" company="Microsoft"> 
//     Copyright (c) Microsoft Corporation. All rights reserved. 
// </copyright> 
// <summary>
//      Utility methods for manipulating RunSettings in Xml format. It does not
//      work with RunSettings object.
// </summary>
// <owner>dhruvk</owner> 
// ---------------------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.XPath;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;

namespace Microsoft.VisualStudio.TestPlatform.ObjectModel.Utilities
{
    public static partial class XmlRunSettingsUtilities
    {
        /// <summary>
        /// Create a default run settings
        /// </summary>
        public static IXPathNavigable CreateDefaultRunSettings()
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
            XmlNode xmlDeclaration = doc.CreateNode(XmlNodeType.XmlDeclaration, "", "");
            doc.AppendChild(xmlDeclaration);
            XmlElement runSettingsNode = doc.CreateElement(Constants.RunSettingsName);
            doc.AppendChild(runSettingsNode);

            XmlElement dataCollectionRunSettingsNode = doc.CreateElement(Constants.DataCollectionRunSettingsName);
            runSettingsNode.AppendChild(dataCollectionRunSettingsNode);

            XmlElement dataCollectorsNode = doc.CreateElement(Constants.DataCollectorsSettingName);
            dataCollectionRunSettingsNode.AppendChild(dataCollectorsNode);

            return doc;
        }

        /// <summary>
        /// Replaces (or adds) the given node in run settings Xml.
        /// </summary>
        /// <param name="settingsXml"></param>
        /// <param name="settingsNode"></param>
        /// <returns></returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2202")]
        public static string ReplaceSettingsNode(string settingsXml, TestRunSettings settingsNode)
        {
            ValidateArg.NotNull(settingsNode, "settingsNode");
            ValidateArg.NotNull(settingsXml, "settingsXml");

            XmlElement newElement = settingsNode.ToXml();

            XmlDocument doc = new XmlDocument();

            using (StringReader stringReader = new StringReader(settingsXml))
            using (XmlTextReader xmlReader = new XmlTextReader(stringReader))
            {
                xmlReader.ProhibitDtd = true;
                xmlReader.XmlResolver = null;
                doc.Load(xmlReader);
            }

            XmlElement root = doc.DocumentElement;


            if (null == root[settingsNode.Name])
            {
                XmlNode newNode = doc.ImportNode(newElement, true);
                root.AppendChild(newNode);
            }
            else
            {
                root[settingsNode.Name].InnerXml = newElement.InnerXml;
            }
            return doc.OuterXml;

        }

        /// <summary>
        /// Inserts a data collector settings in the file
        /// </summary>
        /// <param name="runSettingDocument"></param>
        /// <param name="settings"></param>
        /// <returns></returns>
        public static void InsertDataCollectorsNode(
            IXPathNavigable runSettingDocument,
            DataCollectorSettings settings)
        {
            if (runSettingDocument == null)
                throw new ArgumentNullException("runSettingDocument");
            if (settings == null)
                throw new ArgumentNullException("settings");

            var navigator = runSettingDocument.CreateNavigator();
            MoveToDataCollectorsNode(navigator);

            var settingsXml = settings.ToXml();
            var dataCollectorNode = settingsXml.CreateNavigator();
            dataCollectorNode.MoveToRoot();

            navigator.AppendChild(dataCollectorNode);
        }       

        /// <summary>
        /// Removes the given settings node from run settings Xml
        /// </summary>
        /// <param name="settingsXml"></param>
        /// <param name="settingsName"></param>
        /// <returns></returns>
        public static string RemoveSettingsNode(string settingsXml, string settingsName)
        {
            ValidateArg.NotNullOrEmpty(settingsXml, "settingsXml");
            ValidateArg.NotNullOrEmpty(settingsName, "settingsName");

            XmlDocument doc = new XmlDocument();
            doc.LoadXml(settingsXml);
            XmlElement root = doc.DocumentElement;

            if (null != root[settingsName])
            {
                root.RemoveChild(root[settingsName]);
            }
            return doc.OuterXml;
        }

        /// <summary>
        /// Returns whether data collection is enabled in the parameter settings xml or not
        /// </summary>
        public static bool IsDataCollectionEnabled(string runSettingsXml)
        {
            DataCollectionRunSettings dataCollectionRunSettings = GetDataCollectionRunSettings(runSettingsXml);
            if (dataCollectionRunSettings == null || !dataCollectionRunSettings.IsCollectionEnabled)
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Get DataCollection Run settings
        /// </summary>
        public static DataCollectionRunSettings GetDataCollectionRunSettings(string runSettingsXml)
        {
            // use XmlReader to avoid loading of the plugins in client code (mainly from VS).
            if (!StringUtilities.IsNullOrWhiteSpace(runSettingsXml))
            {
                using (StringReader stringReader1 = new StringReader(runSettingsXml))
                {
                    XmlReader reader = XmlReader.Create(stringReader1, ReaderSettings);

                    // read to the fist child
                    XmlReaderUtilities.ReadToRootNode(reader);
                    reader.ReadToNextElement();

                    // Read till we reach DC element or reach EOF
                    while (!string.Equals(reader.Name, Constants.DataCollectionRunSettingsName)
                            &&
                            !reader.EOF)
                    {
                        reader.SkipToNextElement();
                    }

                    // If reached EOF => DC element not there
                    if (reader.EOF)
                    {
                        return null;
                    }

                    // Reached here => DC element present. 
                    //
                    return DataCollectionRunSettings.FromXml(reader);
                }

            }
            return null;
        }

        /// <summary>
        /// Adds the Fakes data collector settings in the run settings document.
        /// </summary>
        /// <param name="runSettings">A run settings document with a DataCollectors element available</param>
        /// <param name="fakesSettings"></param>
        public static DataCollectorSettings CreateFakesDataCollectorSettings()
        {
            // embed the fakes run settings
            var settings = new DataCollectorSettings
            {
                AssemblyQualifiedName = FakesMetadata.DataCollectorAssemblyQualifiedName,
                FriendlyName = FakesMetadata.FriendlyName,
                IsEnabled = true,
                Uri = new Uri(FakesMetadata.DataCollectorUri)
            };
            return settings;
        }
    }

}
