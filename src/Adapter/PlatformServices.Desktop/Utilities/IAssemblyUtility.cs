// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Utilities
{
    using System.Reflection;

    internal interface IAssemblyUtility
    {
        /// <summary>
        /// Loads an assembly into the reflection-only context, given its path.
        /// </summary>
        /// <param name="assemblyPath">The path of the file that contains the manifest of the assembly.</param>
        /// <returns>The loaded assembly.</returns>
        Assembly ReflectionOnlyLoadFrom(string assemblyPath);

        /// <summary>
        /// Loads an assembly into the reflection-only context, given its display name.
        /// </summary>
        /// <param name="assemblyString">The display name of the assembly, as returned by the System.Reflection.AssemblyName.FullName property.</param>
        /// <returns>The loaded assembly.</returns>
        Assembly ReflectionOnlyLoad(string assemblyString);
    }
}
