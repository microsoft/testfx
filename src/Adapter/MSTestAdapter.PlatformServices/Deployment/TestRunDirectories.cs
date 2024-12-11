// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if !WINDOWS_UWP

using System.Diagnostics.CodeAnalysis;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Deployment;

/// <summary>
/// The test run directories.
/// </summary>
#if NET6_0_OR_GREATER
[Obsolete(Constants.PublicTypeObsoleteMessage, DiagnosticId = "MSTESTOBS")]
#else
[Obsolete(Constants.PublicTypeObsoleteMessage)]
#endif
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

    [MemberNotNull(nameof(InDirectory), nameof(OutDirectory), nameof(InMachineNameDirectory))]
    private void OnRootDeploymentDirectoryUpdated()
    {
        InDirectory = Path.Combine(RootDeploymentDirectory, DeploymentInDirectorySuffix);
        OutDirectory = Path.Combine(RootDeploymentDirectory, DeploymentOutDirectorySuffix);
        InMachineNameDirectory = Path.Combine(InDirectory, Environment.MachineName);
    }

    /// <summary>
    /// Gets or sets the root deployment directory.
    /// </summary>
    public string RootDeploymentDirectory
    {
        get => field;
        // TODO: Remove the setter as a breaking change and simplify the code.
        [MemberNotNull(nameof(InDirectory), nameof(OutDirectory), nameof(InMachineNameDirectory))]
        set
        {
            field = value;
            OnRootDeploymentDirectoryUpdated();
        }
    }

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
