﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if !WINDOWS_UWP

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Deployment;

/// <summary>
/// The test run directories.
/// </summary>
[Serializable]
internal sealed class TestRunDirectories
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

    /// <summary>
    /// Initializes a new instance of the <see cref="TestRunDirectories"/> class.
    /// </summary>
    /// <param name="rootDirectory">The root directory path.</param>
    /// <param name="firstTestSource">
    /// The path to the test assembly of the first test case. In most cases, all
    /// test cases belong to the same assembly, but not guaranteed. We are using the path from
    /// the first test case as a "best effort" implementation. DeploymentItem isn't correctly designed and should be deprecated in future.
    /// </param>
    /// <param name="isAppDomainCreationDisabled">Whether or not app domain is disabled.</param>
    public TestRunDirectories(string rootDirectory, string? firstTestSource, bool isAppDomainCreationDisabled)
    {
        DebugEx.Assert(!StringEx.IsNullOrEmpty(rootDirectory), "rootDirectory");

        RootDeploymentDirectory = rootDirectory;
        InDirectory = Path.Combine(RootDeploymentDirectory, DeploymentInDirectorySuffix);

        OutDirectory = isAppDomainCreationDisabled && firstTestSource is not null
            ? Path.GetDirectoryName(firstTestSource)!
            : Path.Combine(RootDeploymentDirectory, DeploymentOutDirectorySuffix);

        InMachineNameDirectory = Path.Combine(InDirectory, Environment.MachineName);
    }

    /// <summary>
    /// Gets the root deployment directory.
    /// </summary>
    public string RootDeploymentDirectory { get; }

    /// <summary>
    /// Gets the In directory.
    /// </summary>
    public string InDirectory { get; private set; }

    /// <summary>
    /// Gets the Out directory.
    /// </summary>
    public string OutDirectory { get; private set; }

    /// <summary>
    /// Gets In\MachineName directory.
    /// </summary>
    public string InMachineNameDirectory { get; private set; }
}

#endif
