// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;

using System.Collections.Generic;
using System.Xml;

using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface;

#if NETFRAMEWORK || WIN_UI || (NETSTANDARD && !NETSTANDARD_PORTABLE)
using ISettingsProvider = Interface.ISettingsProvider;
#endif

#pragma warning disable SA1649 // SA1649FileNameMustMatchTypeName

/// <summary>
/// Class to read settings from the runsettings xml for the desktop.
/// </summary>
public class MSTestSettingsProvider : ISettingsProvider
{
#if NETFRAMEWORK || WIN_UI || (NETSTANDARD && !NETSTANDARD_PORTABLE)
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
#endif

    /// <summary>
    /// Load the settings from the reader.
    /// </summary>
    /// <param name="reader">Reader to load the settings from.</param>
    public void Load(XmlReader reader)
    {
#if NETFRAMEWORK || WIN_UI || (NETSTANDARD && !NETSTANDARD_PORTABLE)
        ValidateArg.NotNull(reader, "reader");
        s_settings = MSTestAdapterSettings.ToSettings(reader);
#else
        // if we have to read any thing from runsettings special for this platform service then we have to implement it.
#endif
    }

    public IDictionary<string, object> GetProperties(string source)
    {
#if NETFRAMEWORK || WIN_UI || (NETSTANDARD && !NETSTANDARD_PORTABLE)
        return TestDeployment.GetDeploymentInformation(source);
#else
        return new Dictionary<string, object>();
#endif
    }
}

#pragma warning restore SA1649 // SA1649FileNameMustMatchTypeName
