// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if !WINDOWS_UWP
using System.Diagnostics.CodeAnalysis;
using System.Security;

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;

/// <summary>
/// MSTest settings in runsettings look like this
///  <MSTestV2>
///     <AssemblyResolution>
///         <Directory path= "% HOMEDRIVE %\directory " includeSubDirectories = "true" />
///         <Directory path= "C:\windows" includeSubDirectories = "false" />
///         <Directory path= ".\DirectoryName" />  ...// by default includeSubDirectories is false
///     </AssemblyResolution>
/// </MSTestV2>
///
/// For each directory we need to have two info 1) path 2) includeSubDirectories.
/// </summary>
[Serializable]
#if NET6_0_OR_GREATER
[Obsolete(Constants.PublicTypeObsoleteMessage, DiagnosticId = "MSTESTOBS")]
#else
[Obsolete(Constants.PublicTypeObsoleteMessage)]
#endif
[SuppressMessage("StyleCop.CSharp.DocumentationRules", "SA1603:DocumentationMustContainValidXml", Justification = "Reviewed. Suppression is ok here.")]
public class RecursiveDirectoryPath : MarshalByRefObject
{
    /// <summary>
    /// Initializes a new instance of the <see cref="RecursiveDirectoryPath"/> class.
    /// </summary>
    /// <param name="dirPath">The directory path.</param>
    /// <param name="includeSubDirectories">
    /// True if to include subdirectory else false.
    /// </param>
    public RecursiveDirectoryPath(string dirPath, bool includeSubDirectories)
    {
        DirectoryPath = dirPath;
        IncludeSubDirectories = includeSubDirectories;
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
#pragma warning disable CA1041 // Provide ObsoleteAttribute message
    [Obsolete]
#pragma warning restore CA1041 // Provide ObsoleteAttribute message
#endif
    public override object InitializeLifetimeService() => null!;
}
#endif
