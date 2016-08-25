// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface
{
    using System.Collections.Generic;
    using System.Xml;

    /// <summary>
    /// To read settings from the runsettings xml for the corresponding platform service.
    /// </summary>
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
        /// <returns></returns>
        IDictionary<string, object> GetProperties();
    }
}
