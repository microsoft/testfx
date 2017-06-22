// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Helpers
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.CodeAnalysis;
    using System.Globalization;
    using System.IO;
    using System.Xml;
    using Microsoft.VisualStudio.TestPlatform.ObjectModel.Utilities;
    using TestAdapter;
    using TestPlatform.ObjectModel;

    internal class RunSettingsUtilities
    {
        /// <summary>
        /// Gets the settings to be used while creating XmlReader for runsettings.
        /// </summary>
        internal static XmlReaderSettings ReaderSettings
        {
            get
            {
                var settings = new XmlReaderSettings();
                settings.IgnoreComments = true;
                settings.IgnoreWhitespace = true;
                return settings;
            }
        }

        /// <summary>
        /// Gets the set of user defined test run parameters from settings xml as key value pairs.
        /// </summary>
        /// <param name="settingsXml">The runsettings xml.</param>
        /// <returns>The test run parameters.</returns>
        /// <remarks>If there is no test run parameters section defined in the settingsxml a blank dictionary is returned.</remarks>
        internal static Dictionary<string, object> GetTestRunParameters(string settingsXml)
        {
            var defaultTestRunParameters = new Dictionary<string, object>();
            return GetNodeValue(settingsXml, TestAdapter.Constants.TestRunParametersName, defaultTestRunParameters, TestRunParameters.FromXml);
        }

        internal static bool IsDesignMode(string settingsXml)
        {
            bool defaultDesignMode = true;
            return GetNodeValue(settingsXml, "DesignMode", defaultDesignMode);
        }

        /// <summary>
        /// Throws if the node has an attribute.
        /// </summary>
        /// <param name="reader"> The reader. </param>
        /// <exception cref="SettingsException"> Thrown if the node has an attribute. </exception>
        internal static void ThrowOnHasAttributes(XmlReader reader)
        {
            if (reader.HasAttributes)
            {
                reader.MoveToNextAttribute();
                throw new SettingsException(
                    string.Format(
                        CultureInfo.CurrentCulture,
                        Resource.InvalidSettingsXmlAttribute,
                        TestPlatform.ObjectModel.Constants.RunConfigurationSettingsName,
                        reader.Name));
            }
        }

        [SuppressMessage("Microsoft.Security.Xml", "CA3053:UseXmlSecureResolver", Justification = "XmlReaderSettings.XmlResolver is not available in portable code.")]
        private static T GetNodeValue<T>(string settingsXml, string nodeName, T defaultValue, Func<XmlReader, T> nodeParser = null)
        {
            T result = defaultValue;

            // use XmlReader to avoid loading of the plugins in client code (mainly from VS).
            if (!string.IsNullOrWhiteSpace(settingsXml))
            {
                using (StringReader stringReader = new StringReader(settingsXml))
                {
                    XmlReader reader = XmlReader.Create(stringReader, ReaderSettings);

                    // read to the fist child
                    XmlReaderUtilities.ReadToRootNode(reader);
                    reader.ReadToNextElement();

                    // Read to desired node
                    if (reader.ReadToFollowing(nodeName))
                    {
                        if (nodeParser != null)
                        {
                            // read nodeName element.
                            return nodeParser(reader);
                        }
                        else
                        {
                            if (typeof(T).Equals(typeof(bool)))
                            {
                                bool nodeValue;
                                if (bool.TryParse(reader.ReadInnerXml(), out nodeValue))
                                {
                                    result = (T)Convert.ChangeType(nodeValue, typeof(bool));
                                    return result;
                                }
                            }
                        }
                    }
                }
            }

            return result;
        }
    }
}
