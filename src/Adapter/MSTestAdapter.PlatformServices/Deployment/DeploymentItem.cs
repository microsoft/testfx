// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if !WINDOWS_UWP
using System.Globalization;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Deployment;

/// <summary>
/// Specifies type of deployment item origin, where the item comes from.
/// </summary>
[Serializable]
internal enum DeploymentItemOriginType
{
    /// <summary>
    /// A per test deployment item.
    /// </summary>
    PerTestDeployment,

    /// <summary>
    /// A test storage.
    /// </summary>
    TestStorage,

    /// <summary>
    /// A dependency item.
    /// </summary>
    Dependency,

    /// <summary>
    /// A satellite assembly.
    /// </summary>
    Satellite,
}

/// <summary>
/// The deployment item for a test class or a test method.
/// </summary>
[Serializable]
internal sealed class DeploymentItem
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DeploymentItem"/> class.
    /// </summary>
    /// <param name="sourcePath">Absolute or relative to test assembly.</param>
    internal DeploymentItem(string sourcePath)
        : this(sourcePath, string.Empty)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="DeploymentItem"/> class.
    /// </summary>
    /// <param name="sourcePath">Absolute or relative to test assembly.</param>
    /// <param name="relativeOutputDirectory">Relative to the deployment directory. string.Empty means deploy to
    /// deployment directory itself.
    /// </param>
    internal DeploymentItem(string sourcePath, string relativeOutputDirectory)
         : this(sourcePath, relativeOutputDirectory, DeploymentItemOriginType.PerTestDeployment)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="DeploymentItem"/> class.
    /// </summary>
    /// <param name="sourcePath">Absolute or relative to test assembly.</param>
    /// <param name="relativeOutputDirectory">Relative to the deployment directory. string.Empty means deploy to deployment directory itself. </param>
    /// <param name="originType">Origin of this deployment directory.</param>
    internal DeploymentItem(string sourcePath, string relativeOutputDirectory, DeploymentItemOriginType originType)
    {
        DebugEx.Assert(!StringEx.IsNullOrEmpty(sourcePath), "sourcePath");
        DebugEx.Assert(relativeOutputDirectory != null, "relativeOutputDirectory");

        SourcePath = sourcePath.TrimEnd('/', '\\');
        RelativeOutputDirectory = relativeOutputDirectory;
        OriginType = originType;
    }

    /// <summary>
    /// Gets the full path to the 'source' deployment item.
    /// </summary>
    internal string SourcePath
    {
        get; private set;
    }

    /// <summary>
    /// Gets the directory in which item should be deployed. Relative to the deployment root.
    /// </summary>
    internal string RelativeOutputDirectory
    {
        get; private set;
    }

    /// <summary>
    /// Gets the origin of deployment item.
    /// </summary>
    internal DeploymentItemOriginType OriginType { get; private set; }

    #region Object - overrides

    /// <summary>
    /// Equals implementation.
    /// </summary>
    /// <param name="obj"> The object. </param>
    /// <returns> True if the two objects are equal. </returns>
    public override bool Equals(object? obj)
    {
        if (obj is not DeploymentItem otherItem)
        {
            return false;
        }

        DebugEx.Assert(SourcePath != null, "SourcePath");
        DebugEx.Assert(RelativeOutputDirectory != null, "RelativeOutputDirectory");
        return SourcePath.Equals(otherItem.SourcePath, StringComparison.OrdinalIgnoreCase) &&
               RelativeOutputDirectory.Equals(otherItem.RelativeOutputDirectory, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// GetHashCode implementation.
    /// </summary>
    /// <returns> The hash code value. </returns>
    public override int GetHashCode()
    {
        DebugEx.Assert(SourcePath != null, "SourcePath");
        DebugEx.Assert(RelativeOutputDirectory != null, "RelativeOutputDirectory");
        return SourcePath.GetHashCode() + RelativeOutputDirectory.GetHashCode();
    }

    /// <summary>
    /// Deployment item description.
    /// </summary>
    /// <returns> The <see cref="string"/> value. </returns>
    public override string ToString()
    {
        DebugEx.Assert(SourcePath != null, "SourcePath");
        DebugEx.Assert(RelativeOutputDirectory != null, "RelativeOutputDirectory");

        return
            StringEx.IsNullOrEmpty(RelativeOutputDirectory) ?
                string.Format(CultureInfo.CurrentCulture, Resource.DeploymentItem, SourcePath) :
                string.Format(CultureInfo.CurrentCulture, Resource.DeploymentItemWithOutputDirectory, SourcePath, RelativeOutputDirectory);
    }

    #endregion
}
#endif
