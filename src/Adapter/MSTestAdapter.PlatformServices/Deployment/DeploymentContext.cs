// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Deployment;

/// <summary>
/// Platform-agnostic inputs the execution pipeline needs from the running test host: the test-run/results
/// directory and the run settings XML. This lets the platform services execution and deployment layers run
/// without taking a dependency on a specific test platform's run-context object model (for example the VSTest
/// <c>IRunContext</c> / <c>IRunSettings</c> types). It is populated at the adapter boundary.
/// </summary>
internal sealed class DeploymentContext
{
    public DeploymentContext(string? testRunDirectory, string? runSettingsXml)
    {
        TestRunDirectory = testRunDirectory;
        RunSettingsXml = runSettingsXml;
    }

    /// <summary>
    /// Gets the host-provided test-run directory (the root under which results/deployment directories are
    /// created), or <see langword="null"/> to fall back to a temp directory.
    /// </summary>
    public string? TestRunDirectory { get; }

    /// <summary>
    /// Gets the run settings XML supplied by the host, or <see langword="null"/> when none was provided.
    /// </summary>
    public string? RunSettingsXml { get; }
}
