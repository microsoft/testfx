// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if !WINDOWS_UWP && !WIN_UI

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Deployment;

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface;

/// <summary>
/// The TestDeployment interface.
/// </summary>
internal interface ITestDeployment
{
    /// <summary>
    /// Deploy deployment items for the specified tests.
    /// </summary>
    /// <param name="testElements"> The tests. </param>
    /// <param name="deploymentContext"> The host deployment inputs (test-run directory, run settings XML). </param>
    /// <param name="messageLogger"> The logger used to surface deployment warnings. </param>
    /// <returns> True if deployment is done. </returns>
    bool Deploy(IEnumerable<UnitTestElement> testElements, DeploymentContext deploymentContext, IAdapterMessageLogger messageLogger);

    /// <summary>
    /// Gets the set of deployment items on a method and its corresponding class.
    /// </summary>
    /// <param name="method"> The method. </param>
    /// <param name="type"> The type. </param>
    /// <param name="warnings"> The warnings. </param>
    /// <returns> A KeyValuePair of deployment items. </returns>
    KeyValuePair<string, string>[]? GetDeploymentItems(MethodInfo method, Type type, ICollection<string> warnings);

    /// <summary>
    /// Cleanup deployment item directories.
    /// </summary>
    void Cleanup();

    /// <summary>
    /// Gets the deployment output directory where the source file along with all its dependencies is dropped.
    /// </summary>
    /// <returns> The deployment output directory. </returns>
    string? GetDeploymentDirectory();
}
#endif
