﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Helpers
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Xml;

    using Microsoft.VisualStudio.TestPlatform.ObjectModel;

    internal static class TestRunParameters
    {
        internal static Dictionary<string, object> FromXml(XmlReader reader)
        {
            var testParameters = new Dictionary<string, object>();

            if (!reader.IsEmptyElement)
            {
                RunSettingsUtilities.ThrowOnHasAttributes(reader);
                reader.Read();

                while (reader.NodeType == XmlNodeType.Element)
                {
                    var elementName = reader.Name;
                    switch (elementName)
                    {
                        case "Parameter":
                            string paramName = null;
                            string paramValue = null;
                            for (var attIndex = 0; attIndex < reader.AttributeCount; attIndex++)
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
                            throw new SettingsException(
                                string.Format(
                                    CultureInfo.CurrentCulture,
                                    Resource.InvalidSettingsXmlElement,
                                    TestAdapter.Constants.TestRunParametersName,
                                    reader.Name));
                    }

                    reader.Read();
                }
            }

            return testParameters;
        }
    }
}
