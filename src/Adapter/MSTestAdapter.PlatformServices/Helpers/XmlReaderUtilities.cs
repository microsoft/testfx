// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;

namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Helpers;

/// <summary>
/// Minimal, platform-agnostic <see cref="XmlReader"/> navigation helpers for reading runsettings XML.
/// These replace the equivalent helpers from the VSTest object model so the platform services layer does not
/// depend on it; the navigation semantics are unchanged.
/// </summary>
internal static class XmlReaderUtilities
{
    private const string RunSettingsRootNodeName = "RunSettings";

    /// <summary>
    /// Advances the reader to the root element and verifies it is the <c>&lt;RunSettings&gt;</c> node.
    /// </summary>
    /// <param name="reader">The reader positioned before the root element.</param>
    /// <exception cref="AdapterSettingsException">Thrown when the root element is not <c>&lt;RunSettings&gt;</c>.</exception>
    internal static void ReadToRootNode(XmlReader reader)
    {
        reader.ReadToNextElement();

        // Verify that it is a "RunSettings" node.
        if (reader.Name != RunSettingsRootNodeName)
        {
            throw new AdapterSettingsException($"Could not find '{RunSettingsRootNodeName}' node in the runsettings XML. Found '<{reader.Name}>' instead.");
        }
    }

    /// <summary>
    /// Reads until the next element node (or end of document).
    /// </summary>
    /// <param name="reader">The reader.</param>
    internal static void ReadToNextElement(this XmlReader reader)
    {
        while (!reader.EOF && reader.Read() && reader.NodeType != XmlNodeType.Element)
        {
        }
    }

    /// <summary>
    /// Skips the current subtree and positions the reader on the next element node.
    /// </summary>
    /// <param name="reader">The reader.</param>
    internal static void SkipToNextElement(this XmlReader reader)
    {
        reader.Skip();

        if (reader.NodeType != XmlNodeType.Element)
        {
            reader.ReadToNextElement();
        }
    }
}
