// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;

using System.Collections.Generic;
using System.Xml;

using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface;

#pragma warning disable SA1649 // SA1649FileNameMustMatchTypeName

/// <summary>
/// A class to read settings from the runsettings xml for the corresponding platform service.
/// </summary>
public class MSTestSettingsProvider : ISettingsProvider
{
    /// <summary>
    /// Load settings from the runsettings xml for the corresponding platform service.
    /// </summary>
    /// <param name="reader">Reader to load the settings from.</param>
    public void Load(XmlReader reader)
    {
        // if we have to read any thing from runsettings special for this platform service then we have to implement it.
    }

    public IDictionary<string, object> GetProperties(string source) => new Dictionary<string, object>();
}

#pragma warning restore SA1649 // SA1649FileNameMustMatchTypeName
