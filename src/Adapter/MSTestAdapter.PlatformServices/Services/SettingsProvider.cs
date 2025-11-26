// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface;

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;

/// <summary>
/// Class to read settings from the runsettings xml for the desktop.
/// </summary>
internal sealed class MSTestSettingsProvider : ISettingsProvider
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
        var settings = MSTestAdapterSettings.ToSettings(configuration);
        if (!ReferenceEquals(settings, Settings))
        {
            // NOTE: ToSettings mutates the Settings property and just returns it.
            // This invariant is important to preserve, because we load from from runsettings through the XmlReader overload below.
            // Then we read via IConfiguration.
            // So this path should be unreachable.
            // In v4 when we will make this class internal, we can start changing the API to clean this up.
            throw ApplicationStateGuard.Unreachable();
        }
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
        var settings = MSTestAdapterSettings.ToSettings(reader);
        if (!ReferenceEquals(settings, Settings))
        {
            // NOTE: ToSettings mutates the Settings property and just returns it.
            // This invariant is important to preserve, because we load from from runsettings through the XmlReader overload below.
            // Then we read via IConfiguration.
            // So this path should be unreachable.
            // In v4 when we will make this class internal, we can start changing the API to clean this up.
            throw ApplicationStateGuard.Unreachable();
        }
#endif
    }

    /// <summary>
    /// Gets the properties specific to the source.
    /// </summary>
    /// <param name="source">The source.</param>
    /// <returns>A collection of properties.</returns>
    public IDictionary<string, object> GetProperties(string? source)
#if !WINDOWS_UWP && !WIN_UI
        => TestDeployment.GetDeploymentInformation(source);
#else
        => new Dictionary<string, object>();
#endif
}
