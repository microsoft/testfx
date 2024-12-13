// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if !WINDOWS_UWP
using System.Diagnostics.CodeAnalysis;
#endif
using System.Xml;

using Microsoft.Testing.Platform.Configurations;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface;

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;

/// <summary>
/// Class to read settings from the runsettings xml for the desktop.
/// </summary>
#if NET6_0_OR_GREATER
[Obsolete(Constants.PublicTypeObsoleteMessage, DiagnosticId = "MSTESTOBS")]
#else
[Obsolete(Constants.PublicTypeObsoleteMessage)]
#endif
public class MSTestSettingsProvider : ISettingsProvider
{
#if !WINDOWS_UWP
    /// <summary>
    /// Gets settings provided to the adapter.
    /// </summary>
    [field: AllowNull]
    [field: MaybeNull]
    [AllowNull]
    public static MSTestAdapterSettings Settings
    {
        get => field ??= new MSTestAdapterSettings();
        private set;
    }

    /// <summary>
    /// Reset the settings to its default.
    /// </summary>
    public static void Reset() => Settings = null;
#endif

    internal static void Load(IConfiguration configuration)
    {
#if !WINDOWS_UWP
#pragma warning disable IDE0022 // Use expression body for method
        var settings = MSTestAdapterSettings.ToSettings(configuration);
#pragma warning restore IDE0022 // Use expression body for method
#endif
    }

    /// <summary>
    /// Load the settings from the reader.
    /// </summary>
    /// <param name="reader">Reader to load the settings from.</param>
    public void Load(XmlReader reader)
    {
#if !WINDOWS_UWP
        Guard.NotNull(reader);
        Settings = MSTestAdapterSettings.ToSettings(reader);
#endif
    }

    public IDictionary<string, object> GetProperties(string source)
#if !WINDOWS_UWP
        => TestDeployment.GetDeploymentInformation(source);
#else
        => new Dictionary<string, object>();
#endif
}
