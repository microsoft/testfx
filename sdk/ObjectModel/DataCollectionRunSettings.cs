// ---------------------------------------------------------------------------
// <copyright file="DataCollectorSettings.cs" company="Microsoft">
//     Copyright (c) Microsoft Corporation. All rights reserved.
// </copyright>
// <summary>
//     Stores information about a data collector settings.
// </summary>
// ---------------------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Runtime.Serialization;
using System.Xml;



namespace Microsoft.VisualStudio.TestPlatform.ObjectModel
{
    /// <summary>
    /// Run specific data collection settings.
    /// </summary>
    public class DataCollectionRunSettings : TestRunSettings
    {
        public DataCollectionRunSettings()
            : base(Constants.DataCollectionRunSettingsName)
        {
            DataCollectorSettingsList = new Collection<DataCollectorSettings>();
        }

        /// <summary>
        /// Settings for all data collectors configured for the run.
        /// </summary>
        public Collection<DataCollectorSettings> DataCollectorSettingsList
        {
            get;
            private set;
        }


        public bool IsCollectionEnabled
        {
            get
            {
                return DataCollectorSettingsList.Any<DataCollectorSettings>(setting => setting.IsEnabled);
            }
        }

        public override XmlElement ToXml()
        {
            XmlDocument doc = new XmlDocument();
            XmlElement root = doc.CreateElement(Constants.DataCollectionRunSettingsName);
            XmlElement subRoot = doc.CreateElement(Constants.DataCollectorsSettingName);
            root.AppendChild(subRoot);

            foreach (var collectorSettings in DataCollectorSettingsList)
            {
                XmlNode child = doc.ImportNode(collectorSettings.ToXml(), true);
                subRoot.AppendChild(child);
            }

            return root;
        }

        /// <summary>
        /// Loads data collection settings from Xml reader. Used by settings provider to load collection settings.
        /// It reads Dev10 equivalent data collection configuration node from test run configuration.
        /// </summary>
        /// <param name="reader"></param>
        /// <returns></returns>
        public static DataCollectionRunSettings FromXml(XmlReader reader)
        {
            ValidateArg.NotNull<XmlReader>(reader, "reader");
            DataCollectionRunSettings settings = new DataCollectionRunSettings();
            bool empty = reader.IsEmptyElement;
            if (reader.HasAttributes)
            {
                reader.MoveToNextAttribute();
                throw new SettingsException(String.Format(CultureInfo.CurrentCulture,
                                    Resources.InvalidSettingsXmlAttribute, Constants.DataCollectionRunSettingsName, reader.Name));
            }

            // Process the fields in Xml elements
            reader.Read();
            if (!empty)
            {
                while (reader.NodeType == XmlNodeType.Element)
                {
                    switch (reader.Name)
                    {
                        case "DataCollectors":
                            List<DataCollectorSettings> items = ReadListElementFromXml(reader);
                            items.ForEach(item => settings.DataCollectorSettingsList.Add(item));
                           break;
                        default:
                            throw new SettingsException(String.Format(CultureInfo.CurrentCulture,
                                    Resources.InvalidSettingsXmlElement, Constants.DataCollectionRunSettingsName, reader.Name));
                    }
                }
                reader.ReadEndElement();
            }
            return settings;
        }


        /// <summary>
        /// Reads the list of data collector settings.
        /// </summary>
        /// <param name="reader"></param>
        /// <returns></returns>
        internal static List<DataCollectorSettings> ReadListElementFromXml(XmlReader reader)
        {
            List<DataCollectorSettings> settings = new List<DataCollectorSettings>();
            bool empty = reader.IsEmptyElement;
            if (reader.HasAttributes)
            {
                reader.MoveToNextAttribute();
                throw new SettingsException(String.Format(CultureInfo.CurrentCulture,
                                    Resources.InvalidSettingsXmlAttribute, Constants.DataCollectionRunSettingsName, reader.Name));
            }

            reader.Read();
            if (!empty)
            {
                while (reader.NodeType == XmlNodeType.Element)
                {
                    switch (reader.Name)
                    {
                        case "DataCollector":
                            settings.Add(DataCollectorSettings.FromXml(reader));
                            break;

                        default:
                            throw new SettingsException(String.Format(CultureInfo.CurrentCulture,
                                    Resources.InvalidSettingsXmlElement, Constants.DataCollectionRunSettingsName, reader.Name));
                    }
                }
                reader.ReadEndElement();
            }
            return settings;
        }
    }
}
