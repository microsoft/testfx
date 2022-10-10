// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;

#if !WINDOWS_UWP
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

#if NETFRAMEWORK
    /// <summary>
    /// The deployment out directory suffix.
    /// </summary>
    internal const string DeploymentOutDirectorySuffix = "Out";
#endif

    public TestRunDirectories(string rootDirectory)
    {
        Debug.Assert(!string.IsNullOrEmpty(rootDirectory), "rootDirectory");

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
    {
        get
        {
            return Path.Combine(RootDeploymentDirectory, DeploymentInDirectorySuffix);
        }
    }

    /// <summary>
    /// Gets the Out directory.
    /// </summary>
    [SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "Part of the public API")]
    public string OutDirectory
    {
        get
        {
#if NETFRAMEWORK
            return Path.Combine(RootDeploymentDirectory, DeploymentOutDirectorySuffix);
#else
            return Directory.GetCurrentDirectory();
#endif
        }
    }

    /// <summary>
    /// Gets In\MachineName directory.
    /// </summary>
    public string InMachineNameDirectory
    {
        get
        {
            return Path.Combine(Path.Combine(RootDeploymentDirectory, DeploymentInDirectorySuffix), Environment.MachineName);
        }
    }
}

#endif
