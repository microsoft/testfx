// ---------------------------------------------------------------------------
// <copyright file="ISettingsProvider.cs" company="Microsoft"> 
//     Copyright (c) Microsoft Corporation. All rights reserved. 
// </copyright> 
// <summary>
//     Interface implemented to provide a section in the run settings.  A class that
//     implements this interface will be available for use if it exports its type via
//     MEF, and if its containing assembly is placed in the Extensions folder.
// </summary>
// <owner>apatters</owner> 
// ---------------------------------------------------------------------------
using System;
using System.Xml;

namespace Microsoft.VisualStudio.TestPlatform.ObjectModel.Adapter
{
    /// <summary>
    /// Interface implemented to provide a section in the run settings.  A class that
    /// implements this interface will be available for use if it exports its type via
    /// MEF, and if its containing assembly is placed in the Extensions folder.
    /// </summary>
    public interface ISettingsProvider
    {
        /// <summary>
        /// Load the settings from the reader.
        /// </summary>
        /// <param name="reader">Reader to load the settings from.</param>
        void Load(XmlReader reader);
    }
}
