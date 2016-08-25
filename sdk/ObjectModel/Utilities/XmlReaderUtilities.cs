// ---------------------------------------------------------------------------
// <copyright file="XmlReaderUtilities.cs" company="Microsoft"> 
//     Copyright (c) Microsoft Corporation. All rights reserved. 
// </copyright> 
// <summary>
//      Utility methods for working with an XmlReader.
// </summary>
// <owner>apatters</owner> 
// ---------------------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;

namespace Microsoft.VisualStudio.TestPlatform.ObjectModel.Utilities
{
    /// <summary>
    /// Utility methods for working with an XmlReader.
    /// </summary>
    public static class XmlReaderUtilities
    {
        #region Constants

        private const string c_runSettingsRootNodeName = "RunSettings";

        #endregion

        #region Utility Methods

        /// <summary>
        /// Reads up to the next Element in the document.
        /// </summary>
        /// <param name="reader">Reader to move to the next element.</param>
        public static void ReadToNextElement(this XmlReader reader)
        {
            ValidateArg.NotNull<XmlReader>(reader, "reader");
            while (!reader.EOF && reader.Read() && reader.NodeType != XmlNodeType.Element)
            {
            }
        }

        /// <summary>
        /// Skips the current element and moves to the next Element in the document.
        /// </summary>
        /// <param name="reader">Reader to move to the next element.</param>
        public static void SkipToNextElement(this XmlReader reader)
        {
            ValidateArg.NotNull<XmlReader>(reader, "reader");
            reader.Skip();

            if (reader.NodeType != XmlNodeType.Element)
            {
                reader.ReadToNextElement();
            }
        }

        /// <summary>
        /// Reads to the root node of the run settings and verifies that it is a "RunSettings" node.
        /// </summary>
        /// <param name="path">Path to the file.</param>
        /// <param name="reader">XmlReader for the file.</param>
        public static void ReadToRootNode(XmlReader reader)
        {
            ValidateArg.NotNull<XmlReader>(reader, "reader");
            // Read to the root node.
            reader.ReadToNextElement();

            // Verify that it is a "RunSettings" node.
            if (reader.Name != c_runSettingsRootNodeName)
            {
                throw new SettingsException(
                    String.Format(
                        CultureInfo.CurrentCulture,
                        Resources.InvalidRunSettingsRootNode));
            }
        }

        #endregion
    }
}
