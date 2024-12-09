// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Reflection;

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface;

/// <summary>
/// This service is responsible for any file based operations.
/// </summary>
#if NET6_0_OR_GREATER
[Obsolete(Constants.PublicTypeObsoleteMessage, DiagnosticId = "MSTESTOBS")]
#else
[Obsolete(Constants.PublicTypeObsoleteMessage)]
#endif
public interface IFileOperations
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
    /// Gets the path to the .DLL of the assembly.
    /// </summary>
    /// <param name="assembly">The assembly.</param>
    /// <returns>Path to the .DLL of the assembly.</returns>
    string? GetAssemblyPath(Assembly assembly);

    /// <summary>
    /// Verify if a file exists in the current context.
    /// </summary>
    /// <param name="assemblyFileName"> The assembly file name. </param>
    /// <returns> true if the file exists. </returns>
    bool DoesFileExist(string assemblyFileName);

    /// <summary>
    /// Creates a Navigation session for the source file.
    /// This is used to get file path and line number information for its components.
    /// </summary>
    /// <param name="source"> The source file. </param>
    /// <returns> A Navigation session instance for the current platform. </returns>
    /// <remarks>
    /// Unfortunately we cannot use INavigationSession introduced in Object Model in Dev14 update-2 because
    /// the adapter needs to work with older VS versions as well where this new type would not be defined resulting in a type not found exception.
    /// </remarks>
    object? CreateNavigationSession(string source);

    /// <summary>
    /// Gets the navigation data for a navigation session.
    /// </summary>
    /// <param name="navigationSession"> The navigation session. </param>
    /// <param name="className"> The class name. </param>
    /// <param name="methodName"> The method name. </param>
    /// <param name="minLineNumber"> The min line number. </param>
    /// <param name="fileName"> The file name. </param>
    /// <remarks>
    /// Unfortunately we cannot use INavigationSession introduced in Object Model in Dev14 update-2 because
    /// the adapter needs to work with older VS versions as well where this new type would not be defined resulting in a type not found exception.
    /// </remarks>
    void GetNavigationData(object navigationSession, string className, string methodName, out int minLineNumber, out string? fileName);

    /// <summary>
    /// Disposes the navigation session instance.
    /// </summary>
    /// <param name="navigationSession"> The navigation session. </param>
    /// <remarks>
    /// Unfortunately we cannot use INavigationSession introduced in Object Model in Dev14 update-2 because
    /// the adapter needs to work with older VS versions as well where this new type would not be defined resulting in a type not found exception.
    /// </remarks>
    void DisposeNavigationSession(object? navigationSession);

    /// <summary>
    /// Gets the full file path of an assembly file.
    /// </summary>
    /// <param name="assemblyFileName"> The file name. </param>
    /// <returns> The full file path. </returns>
    string GetFullFilePath(string assemblyFileName);
}
