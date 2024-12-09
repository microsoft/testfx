// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Xml;

using Microsoft.Testing.Platform.Configurations;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Adapter;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Utilities;
using Microsoft.VisualStudio.TestPlatform.PlatformAbstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter;

#if NET6_0_OR_GREATER
[Obsolete(Constants.PublicTypeObsoleteMessage, DiagnosticId = "MSTESTOBS")]
#else
[Obsolete(Constants.PublicTypeObsoleteMessage)]
#endif
public class RunConfigurationSettings
{
    /// <summary>
    /// The settings name.
    /// </summary>
    public const string SettingsName = "RunConfiguration";

    /// <summary>
    /// Initializes a new instance of the <see cref="RunConfigurationSettings"/> class.
    /// </summary>
    public RunConfigurationSettings() => CollectSourceInformation = true;

    /// <summary>
    /// Gets a value indicating whether source information needs to be collected or not.
    /// </summary>
    public bool CollectSourceInformation { get; private set; }

    /// <summary>
    /// Gets a value indicating the requested platform apartment state.
    /// </summary>
    internal ApartmentState? ExecutionApartmentState { get; private set; }

    /// <summary>
    /// Populate adapter settings from the context.
    /// </summary>
    /// <param name="context">
    /// The discovery context that contains the runsettings.
    /// </param>
    /// <returns>Populated RunConfigurationSettings from the discovery context.</returns>
    public static RunConfigurationSettings PopulateSettings(IDiscoveryContext? context)
    {
        if (context?.RunSettings == null || StringEx.IsNullOrEmpty(context.RunSettings.SettingsXml))
        {
            // This will contain default configuration settings
            return new RunConfigurationSettings();
        }

        RunConfigurationSettings? settings = GetSettings(context.RunSettings.SettingsXml, SettingsName);

        return settings ?? new RunConfigurationSettings();
    }

    /// <summary>
    /// Gets the configuration settings from the xml.
    /// </summary>
    /// <param name="runsettingsXml"> The xml with the settings passed from the test platform. </param>
    /// <param name="settingName"> The name of the settings to fetch.</param>
    /// <returns> The settings if found. Null otherwise. </returns>
    internal static RunConfigurationSettings? GetSettings(
        [StringSyntax(StringSyntaxAttribute.Xml, nameof(runsettingsXml))] string runsettingsXml,
        string settingName)
    {
        using var stringReader = new StringReader(runsettingsXml);
        var reader = XmlReader.Create(stringReader, XmlRunSettingsUtilities.ReaderSettings);

        // read to the fist child
        XmlReaderUtilities.ReadToRootNode(reader);
        reader.ReadToNextElement();

        // Read till we reach nodeName element or reach EOF
        while (!string.Equals(reader.Name, settingName, StringComparison.OrdinalIgnoreCase)
                && !reader.EOF)
        {
            reader.SkipToNextElement();
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
        reader.ReadToNextElement();

        if (!reader.IsEmptyElement)
        {
            reader.Read();

            while (reader.NodeType == XmlNodeType.Element)
            {
                string elementName = reader.Name.ToUpperInvariant();
                switch (elementName)
                {
                    case "COLLECTSOURCEINFORMATION":
                        {
                            if (bool.TryParse(reader.ReadInnerXml(), out bool result))
                            {
                                settings.CollectSourceInformation = result;
                                PlatformServiceProvider.Instance.AdapterTraceLogger.LogInfo(
                                "CollectSourceInformation value found : {0} ",
                                result);
                            }

                            break;
                        }

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
                            reader.SkipToNextElement();
                            break;
                        }
                }
            }
        }

        return settings;
    }

    internal static RunConfigurationSettings SetRunConfigurationSettingsFromConfig(IConfiguration configuration, RunConfigurationSettings settings)
    {
        // Expected format of the json is: -
        // "mstest" : {
        //  "execution": {
        //    "collectSourceInformation": true,
        //    "executionApartmentState": "STA"
        //  }
        // }
        if (bool.TryParse(configuration["mstest:execution:collectSourceInformation"], out bool collectSourceInformation))
        {
            settings.CollectSourceInformation = collectSourceInformation;
            PlatformServiceProvider.Instance.AdapterTraceLogger.LogInfo(
                "CollectSourceInformation value found : {0}", collectSourceInformation);
        }

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
}
