// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Reflection;

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface;

/// <summary>
/// This platform service is responsible for any data or operations to validate
/// the test sources provided to the adapter.
/// </summary>
#if NET6_0_OR_GREATER
[Obsolete(Constants.PublicTypeObsoleteMessage, DiagnosticId = "MSTESTOBS")]
#else
[Obsolete(Constants.PublicTypeObsoleteMessage)]
#endif
public interface ITestSource
{
    /// <summary>
    /// Gets the set of valid extensions for sources targeting this platform.
    /// </summary>
    IEnumerable<string> ValidSourceExtensions { get; }

    /// <summary>
    /// Verifies if the assembly provided is referenced by the source.
    /// </summary>
    /// <param name="assemblyName"> The assembly name. </param>
    /// <param name="source"> The source. </param>
    /// <returns> True if the assembly is referenced. </returns>
    bool IsAssemblyReferenced(AssemblyName assemblyName, string source);

    /// <summary>
    /// Gets the set of sources (dll's/exe's) that contain tests. If a source is a package(appx), return the file(dll/exe) that contains tests from it.
    /// </summary>
    /// <param name="sources"> Sources given to the adapter.  </param>
    /// <returns> Sources that contains tests. </returns>
    IEnumerable<string> GetTestSources(IEnumerable<string> sources);
}
