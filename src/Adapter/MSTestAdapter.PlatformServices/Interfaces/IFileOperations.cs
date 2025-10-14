// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface;

/// <summary>
/// This service is responsible for any file based operations.
/// </summary>
internal interface IFileOperations
{
    /// <summary>
    /// Loads an assembly into the current context.
    /// </summary>
    /// <param name="assemblyName">
    /// The name of the assembly.
    /// </param>
    /// <param name="isReflectionOnly">
    /// Indicates whether this should be a reflection only load.
    /// </param>
    /// <returns>
    /// A handle to the loaded assembly.
    /// </returns>
    /// <remarks>
    /// A platform can choose how it wants the assembly loaded. (ReflectionOnlyLoad/Load etc).
    /// </remarks>
    Assembly LoadAssembly(string assemblyName, bool isReflectionOnly);

    /// <summary>
    /// Verify if a file exists in the current context.
    /// </summary>
    /// <param name="assemblyFileName"> The assembly file name. </param>
    /// <returns> true if the file exists. </returns>
    bool DoesFileExist(string assemblyFileName);

    /// <summary>
    /// Gets the full file path of an assembly file.
    /// </summary>
    /// <param name="assemblyFileName"> The file name. </param>
    /// <returns> The full file path. </returns>
    string GetFullFilePath(string assemblyFileName);
}
