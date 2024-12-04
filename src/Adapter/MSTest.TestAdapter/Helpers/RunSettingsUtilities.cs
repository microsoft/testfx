// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Xml;

using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Utilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Helpers;

internal static class RunSettingsUtilities
{
    /// <summary>
    /// Gets the settings to be used while creating XmlReader for runsettings.
    /// </summary>
    internal static XmlReaderSettings ReaderSettings
    {
        get
        {
            var settings = new XmlReaderSettings
            {
                IgnoreComments = true,
                IgnoreWhitespace = true,
            };
            return settings;
        }
    }

    /// <summary>
    /// Gets the set of user defined test run parameters from settings xml as key value pairs.
    /// </summary>
    /// <param name="settingsXml">The runsettings xml.</param>
    /// <returns>The test run parameters.</returns>
    /// <remarks>If there is no test run parameters section defined in the settingsxml a blank dictionary is returned.</remarks>
    internal static Dictionary<string, object>? GetTestRunParameters([StringSyntax(StringSyntaxAttribute.Xml, nameof(settingsXml))] string? settingsXml)
        => GetNodeValue(settingsXml, Constants.TestRunParametersName, TestRunParameters.FromXml);

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

    private static T? GetNodeValue<T>(
        [StringSyntax(StringSyntaxAttribute.Xml, nameof(settingsXml))] string? settingsXml,
        string nodeName,
        Func<XmlReader, T> nodeParser)
    {
        if (StringEx.IsNullOrWhiteSpace(settingsXml))
        {
            return default;
        }

        // use XmlReader to avoid loading of the plugins in client code (mainly from VS).
        using StringReader stringReader = new(settingsXml);
        var reader = XmlReader.Create(stringReader, ReaderSettings);

        // read to the fist child
        XmlReaderUtilities.ReadToRootNode(reader);
        reader.ReadToNextElement();

        // Read till we reach nodeName element or reach EOF
        while (!string.Equals(reader.Name, nodeName, StringComparison.OrdinalIgnoreCase)
               && !reader.EOF)
        {
            reader.SkipToNextElement();
        }

        if (!reader.EOF)
        {
            // read nodeName element.
            return nodeParser(reader);
        }

        return default;
    }
}
