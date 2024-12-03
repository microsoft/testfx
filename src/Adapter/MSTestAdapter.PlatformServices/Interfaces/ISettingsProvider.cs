// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Xml;

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface;

/// <summary>
/// To read settings from the runsettings xml for the corresponding platform service.
/// </summary>
#if NET6_0_OR_GREATER
[Obsolete(Constants.PublicTypeObsoleteMessage, DiagnosticId = "MSTESTOBS")]
#else
[Obsolete(Constants.PublicTypeObsoleteMessage)]
#endif
public interface ISettingsProvider
{
    /// <summary>
    /// Load settings from the xml reader instance which are specific
    /// for the corresponding platform service.
    /// </summary>
    /// <param name="reader">
    /// Reader that can be used to read current node and all its descendants,
    /// to load the settings from.</param>
    void Load(XmlReader reader);

    /// <summary>
    /// The set of properties/settings specific to the platform, that will be surfaced to the user through the test context.
    /// </summary>
    /// <param name="source">
    /// source is used to find application base directory used for setting test context properties.
    /// </param>
    /// <returns>Properties specific to the platform.</returns>
    IDictionary<string, object> GetProperties(string source);
}
