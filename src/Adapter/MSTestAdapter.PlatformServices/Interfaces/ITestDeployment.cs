// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Reflection;

using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Adapter;

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface;

/// <summary>
/// The TestDeployment interface.
/// </summary>
#if NET6_0_OR_GREATER
[Obsolete(Constants.PublicTypeObsoleteMessage, DiagnosticId = "MSTESTOBS")]
#else
[Obsolete(Constants.PublicTypeObsoleteMessage)]
#endif
public interface ITestDeployment
{
    /// <summary>
    /// Deploy deployment items for the specified test cases.
    /// </summary>
    /// <param name="testCases"> The test cases. </param>
    /// <param name="runContext"> The run context. </param>
    /// <param name="frameworkHandle"> The framework handle. </param>
    /// <returns> True if deployment is done. </returns>
    bool Deploy(IEnumerable<TestCase> testCases, IRunContext? runContext, IFrameworkHandle frameworkHandle);

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
