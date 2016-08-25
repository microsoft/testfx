// ---------------------------------------------------------------------------
// <copyright file="TestRunParameters.cs" company="Microsoft">
//     Copyright (c) Microsoft Corporation. All rights reserved.
// </copyright>
// <summary>
//     This class deals with the Runsettings, Test Run Parameters node and the extracting of Key Value Pair's from the parameters listed.
// </summary>
// ---------------------------------------------------------------------------

namespace Microsoft.VisualStudio.TestPlatform.ObjectModel
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Xml;
    using Microsoft.VisualStudio.TestPlatform.ObjectModel.Utilities;

    internal static class TestRunParameters
    {
        internal static Dictionary<string, object> FromXml(XmlReader reader)
        {
            Dictionary<string, object> testParameters = new Dictionary<string, object>();

            if (!reader.IsEmptyElement)
            {
                XmlRunSettingsUtilities.ThrowOnHasAttributes(reader);
                reader.Read();

                while (reader.NodeType == XmlNodeType.Element)
                {
                    string elementName = reader.Name;
                    switch (elementName)
                    {
                        case "Parameter":
                            string paramName = null;
                            string paramValue = null;
                            for (int attIndex = 0; attIndex < reader.AttributeCount; attIndex++)
                            {
                                reader.MoveToAttribute(attIndex);
                                if (string.Equals(reader.Name, "Name", StringComparison.OrdinalIgnoreCase))
                                {
                                    paramName = reader.Value;
                                }
                                else if (string.Equals(reader.Name, "Value", StringComparison.OrdinalIgnoreCase))
                                {
                                    paramValue = reader.Value;
                                }
                            }
                            if (paramName != null && paramValue != null)
                            {
                                testParameters[paramName] = paramValue;
                            }
                            break;
                        default:
                            throw new SettingsException(String.Format(CultureInfo.CurrentCulture,
                                    Resources.InvalidSettingsXmlElement, Constants.TestRunParametersName, reader.Name));
                    }
                    reader.Read();
                }
            }

            return testParameters;
        }
    }
}
