// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices
{
    using System;
    using System.Diagnostics.CodeAnalysis;
    using System.Security;

    /// <summary>
    /// Mstest settings in runsettings look like this
    ///  <MSTestV2>
    ///     <AssemblyResolution>
    ///         <Directory path= "% HOMEDRIVE %\direvtory " includeSubDirectories = "true" />
    ///         <Directory path= "C:\windows" includeSubDirectories = "false" />
    ///         <Directory path= ".\DirectoryName" />  ...// by default includeSubDirectories is false
    ///     </AssemblyResolution>
    /// </MSTestV2>
    ///
    /// For each directory we need to have two info 1) path 2) includeSubDirectories
    /// </summary>
    [Serializable]
    [SuppressMessage("StyleCop.CSharp.DocumentationRules", "SA1603:DocumentationMustContainValidXml", Justification = "Reviewed. Suppression is ok here.")]
#pragma warning disable SA1649 // File name must match first type name
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
#if NET5_0
        [Obsolete]
#endif
        public override object InitializeLifetimeService()
        {
            return null;
        }
    }
}
