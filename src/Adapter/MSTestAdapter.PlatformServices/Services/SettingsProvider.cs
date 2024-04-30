// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Xml;

using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface;
#if !WINDOWS_UWP
using Microsoft.VisualStudio.TestPlatform.ObjectModel;
#endif

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;

/// <summary>
/// Class to read settings from the runsettings xml for the desktop.
/// </summary>
public class MSTestSettingsProvider : ISettingsProvider
{
#if !WINDOWS_UWP
    /// <summary>
    /// Member variable for Adapter settings.
    /// </summary>
    private static MSTestAdapterSettings? s_settings;

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
    public static void Reset() => s_settings = null;
#endif

    /// <summary>
    /// Load the settings from the reader.
    /// </summary>
    /// <param name="reader">Reader to load the settings from.</param>
    public void Load(XmlReader reader)
    {
#if !WINDOWS_UWP
        ValidateArg.NotNull(reader, "reader");
        s_settings = MSTestAdapterSettings.ToSettings(reader);
#endif
    }

    public IDictionary<string, object> GetProperties(string source)
#if !WINDOWS_UWP
        => TestDeployment.GetDeploymentInformation(source);
#else
        => new Dictionary<string, object>();
#endif
}
