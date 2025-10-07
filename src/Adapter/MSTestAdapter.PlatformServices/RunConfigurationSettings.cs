// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if !WINDOWS_UWP
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface;
#endif

using Microsoft.VisualStudio.TestPlatform.PlatformAbstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter;

/// <summary>
/// The run configuration settings.
/// </summary>
internal sealed class RunConfigurationSettings
{
    /// <summary>
    /// The settings name.
    /// </summary>
    public const string SettingsName = "RunConfiguration";

    /// <summary>
    /// Gets a value indicating the requested platform apartment state.
    /// </summary>
    internal ApartmentState? ExecutionApartmentState { get; private set; }

    public static RunConfigurationSettings PopulateSettings([StringSyntax(StringSyntaxAttribute.Xml, nameof(runSettingsXml))] string? runSettingsXml)
    {
        if (StringEx.IsNullOrEmpty(runSettingsXml))
        {
            // This will contain default configuration settings
            return new RunConfigurationSettings();
        }

        RunConfigurationSettings? settings = GetSettings(runSettingsXml, SettingsName);

        return settings ?? new RunConfigurationSettings();
    }

    /// <summary>
    /// Gets the configuration settings from the xml.
    /// </summary>
    /// <param name="runSettingsXml"> The xml with the settings passed from the test platform. </param>
    /// <param name="settingName"> The name of the settings to fetch.</param>
    /// <returns> The settings if found. Null otherwise. </returns>
    internal static RunConfigurationSettings? GetSettings(
        [StringSyntax(StringSyntaxAttribute.Xml, nameof(runSettingsXml))] string runSettingsXml,
        string settingName)
    {
        using var stringReader = new StringReader(runSettingsXml);
        var reader = XmlReader.Create(stringReader, new() { IgnoreComments = true, IgnoreWhitespace = true, DtdProcessing = DtdProcessing.Prohibit });

        // read to the fist child
        ReadToRootNode(reader);
        ReadToNextElement(reader);

        // Read till we reach nodeName element or reach EOF
        while (!string.Equals(reader.Name, settingName, StringComparison.OrdinalIgnoreCase)
                && !reader.EOF)
        {
            SkipToNextElement(reader);
        }

        if (!reader.EOF)
        {
            // read nodeName element.
            return ToSettings(reader.ReadSubtree());
        }

        return null;
    }

    /// <summary>
    /// Convert the parameter xml to TestSettings.
    /// </summary>
    /// <param name="reader">Reader to load the settings from.</param>
    /// <returns>An instance of the <see cref="MSTestSettings"/> class.</returns>
    private static RunConfigurationSettings ToSettings(XmlReader reader)
    {
        Guard.NotNull(reader);

        // Expected format of the xml is: -
        //
        // <Runsettings>
        // <RunConfiguration>
        // <CollectSourceInformation>true</CollectSourceInformation>
        // <ExecutionApartmentState>STA/MTA</ExecutionApartmentState>
        // </RunConfiguration>
        // </Runsettings>
        RunConfigurationSettings settings = new();

        // Read the first element in the section
        ReadToNextElement(reader);

        if (!reader.IsEmptyElement)
        {
            reader.Read();

            while (reader.NodeType == XmlNodeType.Element)
            {
                string elementName = reader.Name.ToUpperInvariant();
                switch (elementName)
                {
                    case "EXECUTIONTHREADAPARTMENTSTATE":
                        {
                            if (Enum.TryParse(reader.ReadInnerXml(), out PlatformApartmentState platformApartmentState))
                            {
                                settings.ExecutionApartmentState = platformApartmentState switch
                                {
                                    PlatformApartmentState.STA => ApartmentState.STA,
                                    PlatformApartmentState.MTA => ApartmentState.MTA,
                                    _ => throw new NotSupportedException($"Platform apartment state '{platformApartmentState}' is not supported."),
                                };
                            }

                            break;
                        }

                    default:
                        {
                            SkipToNextElement(reader);
                            break;
                        }
                }
            }
        }

        return settings;
    }

#if !WINDOWS_UWP
    internal static RunConfigurationSettings SetRunConfigurationSettingsFromConfig(IConfiguration configuration, RunConfigurationSettings settings)
    {
        // Expected format of the json is: -
        // "mstest" : {
        //  "execution": {
        //    "executionApartmentState": "STA"
        //  }
        // }
        string? apartmentStateValue = configuration["mstest:execution:executionApartmentState"];
        if (Enum.TryParse(apartmentStateValue, out PlatformApartmentState platformApartmentState))
        {
            settings.ExecutionApartmentState = platformApartmentState switch
            {
                PlatformApartmentState.STA => ApartmentState.STA,
                PlatformApartmentState.MTA => ApartmentState.MTA,
                _ => throw new NotSupportedException(string.Format(CultureInfo.CurrentCulture, Resource.InvalidValue, platformApartmentState, "execution:executionApartmentState")),
            };
        }

        return settings;
    }
#endif

    private static void ReadToRootNode(XmlReader reader)
    {
        ReadToNextElement(reader);

        // Verify that it is a "RunSettings" node.
        if (reader.Name != "RunSettings")
        {
            throw new FormatException("Invalid runsettings");
        }
    }

    private static void ReadToNextElement(XmlReader reader)
    {
        while (!reader.EOF && reader.Read() && reader.NodeType != XmlNodeType.Element)
        {
        }
    }

    private static void SkipToNextElement(XmlReader reader)
    {
        reader.Skip();

        if (reader.NodeType != XmlNodeType.Element)
        {
            ReadToNextElement(reader);
        }
    }
}
