// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices
{
    using System;
    using System.Diagnostics.CodeAnalysis;
    using System.Security;

    /// <summary>
    /// MSTest settings in runsettings look like this
    /// 
    /// <code>
    /// &lt;MSTestV2&gt;
    ///   &lt;AssemblyResolution&gt;
    ///     &lt;Directory path= &quot;% HOMEDRIVE %\direvtory &quot; includeSubDirectories = &quot;true&quot; /&gt;
    ///     &lt;Directory path= &quot;C:\windows&quot; includeSubDirectories = &quot;false&quot; /&gt;
    ///     &lt;Directory path= &quot;.\DirectoryName&quot; /&gt;  ...// by default includeSubDirectories is false
    ///   &lt;/AssemblyResolution&gt;
    /// &lt;/MSTestV2&gt;
    /// </code>
    /// 
    /// For each directory <c>path</c> and <c>includeSubDirectories</c> must be specified.
    /// </summary>
    [Serializable]
    public class RecursiveDirectoryPath : MarshalByRefObject
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="RecursiveDirectoryPath"/> class.
        /// </summary>
        /// <param name="dirPath">The directory path.</param>
        /// <param name="includeSubDirectories">
        /// True if to include subdirectory else false
        /// </param>
        public RecursiveDirectoryPath(string dirPath, bool includeSubDirectories)
        {
            this.DirectoryPath = dirPath;
            this.IncludeSubDirectories = includeSubDirectories;
        }

        /// <summary>
        /// Gets the directory path.
        /// </summary>
        public string DirectoryPath { get; private set; }

        /// <summary>
        /// Gets a value indicating whether to include sub directories.
        /// </summary>
        public bool IncludeSubDirectories { get; private set; }

        /// <summary>
        /// Returns object to be used for controlling lifetime, null means infinite lifetime.
        /// </summary>
        /// <returns>
        /// The <see cref="object"/>.
        /// </returns>
        [SecurityCritical]
#if NET5_0_OR_GREATER
        [Obsolete]
#endif
        public override object InitializeLifetimeService()
        {
            return null;
        }
    }
}
