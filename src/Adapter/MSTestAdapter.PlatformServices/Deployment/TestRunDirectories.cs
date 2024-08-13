// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if !WINDOWS_UWP

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Deployment;

/// <summary>
/// The test run directories.
/// </summary>
[Serializable]
public class TestRunDirectories
{
    /// <summary>
    /// The default deployment root directory. We do not want to localize it.
    /// </summary>
    internal const string DefaultDeploymentRootDirectory = "TestResults";

    /// <summary>
    /// The deployment in directory suffix.
    /// </summary>
    internal const string DeploymentInDirectorySuffix = "In";

    /// <summary>
    /// The deployment out directory suffix.
    /// </summary>
    internal const string DeploymentOutDirectorySuffix = "Out";

    public TestRunDirectories(string rootDirectory)
    {
        DebugEx.Assert(!StringEx.IsNullOrEmpty(rootDirectory), "rootDirectory");

        RootDeploymentDirectory = rootDirectory;
    }

    /// <summary>
    /// Gets or sets the root deployment directory.
    /// </summary>
    public string RootDeploymentDirectory { get; set; }

    /// <summary>
    /// Gets the In directory.
    /// </summary>
    public string InDirectory
        => Path.Combine(RootDeploymentDirectory, DeploymentInDirectorySuffix);

    /// <summary>
    /// Gets the Out directory.
    /// </summary>
    public string OutDirectory
        => Path.Combine(RootDeploymentDirectory, DeploymentOutDirectorySuffix);

    /// <summary>
    /// Gets In\MachineName directory.
    /// </summary>
    public string InMachineNameDirectory
        => Path.Combine(Path.Combine(RootDeploymentDirectory, DeploymentInDirectorySuffix), Environment.MachineName);
}

#endif
