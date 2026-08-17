// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.DynamicExtensions;

/// <summary>
/// Loads the assembly of a dynamically declared extension.
/// </summary>
/// <remarks>
/// Abstracted so unit tests can exercise discovery, validation, de-duplication and hook invocation without
/// touching the real runtime loader.
/// </remarks>
internal interface IDynamicExtensionAssemblyLoader
{
    /// <summary>
    /// Gets a value indicating whether loaded assemblies are isolated from the host's dependency graph.
    /// </summary>
    bool IsIsolated { get; }

    /// <summary>
    /// Loads (or returns the already loaded) assembly located at <paramref name="assemblyPath"/>.
    /// </summary>
    /// <param name="assemblyPath">Absolute path of the extension assembly.</param>
    /// <returns>The loaded assembly.</returns>
    Assembly LoadAssembly(string assemblyPath);
}
