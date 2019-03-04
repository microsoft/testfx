// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Utilities
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Globalization;
    using System.IO;
    using System.Reflection;

    using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Deployment;
    using Microsoft.VisualStudio.TestPlatform.ObjectModel;

#pragma warning disable SA1649 // File name must match first type name

    /// <summary>
    /// Utility for assembly specific functionality.
    /// </summary>
    internal class AssemblyUtility
    {
        private readonly string[] assemblyExtensions = new string[] { ".dll", ".exe" };

        /// <summary>
        /// Whether file extension is an assembly file extension.
        /// Returns true for .exe and .dll, otherwise false.
        /// </summary>
        /// <param name="extensionWithLeadingDot"> Extension containing leading dot, e.g. ".exe". </param>
        /// <remarks> Path.GetExtension() returns extension with leading dot. </remarks>
        /// <returns> True if this is an assembly extension. </returns>
        internal bool IsAssemblyExtension(string extensionWithLeadingDot)
        {
            foreach (var realExtension in this.assemblyExtensions)
            {
                if (string.Equals(extensionWithLeadingDot, realExtension, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
