// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices
{
    using System.Collections.Generic;
    using System.Xml;

    using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface;

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

        public IDictionary<string, object> GetProperties(string source)
        {
            return new Dictionary<string, object>();
        }
    }
}
