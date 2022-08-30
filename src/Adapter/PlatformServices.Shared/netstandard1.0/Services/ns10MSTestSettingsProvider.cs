// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;

using System.Collections.Generic;
using System.Xml;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using ISettingsProvider = Interface.ISettingsProvider;

/// <summary>
/// Class to read settings from the runsettings xml for the desktop.
/// </summary>
public class MSTestSettingsProvider : ISettingsProvider
{
    /// <summary>
    /// Member variable for Adapter settings
    /// </summary>
    private static MSTestAdapterSettings s_settings;

    /// <summary>
    /// Gets settings provided to the adapter.
    /// </summary>
    public static MSTestAdapterSettings Settings
    {
        get
        {
            s_settings ??= new MSTestAdapterSettings();

            return s_settings;
        }
    }

    /// <summary>
    /// Reset the settings to its default.
    /// </summary>
    public static void Reset()
    {
        s_settings = null;
    }

    /// <summary>
    /// Load the settings from the reader.
    /// </summary>
    /// <param name="reader">Reader to load the settings from.</param>
    public void Load(XmlReader reader)
    {
        ValidateArg.NotNull(reader, "reader");
        s_settings = MSTestAdapterSettings.ToSettings(reader);
    }

    public IDictionary<string, object> GetProperties(string source)
    {
        return TestDeployment.GetDeploymentInformation(source);
    }
}
