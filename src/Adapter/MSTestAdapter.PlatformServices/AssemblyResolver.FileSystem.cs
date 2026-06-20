// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if NETFRAMEWORK || (NET && !WINDOWS_UWP)

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;

internal partial class AssemblyResolver
{
    /// <summary>
    /// Verifies if a directory exists.
    /// </summary>
    /// <param name="path">The path to the directory.</param>
    /// <returns>True if the directory exists.</returns>
    /// <remarks>Only present for unit testing scenarios.</remarks>
#if NETFRAMEWORK
    protected virtual
#else
    private static
#endif
    bool DoesDirectoryExist(string path) => Directory.Exists(path);

    /// <summary>
    /// Gets the directories from a path.
    /// </summary>
    /// <param name="path">The path to the directory.</param>
    /// <returns>A list of directories in path.</returns>
    /// <remarks>Only present for unit testing scenarios.</remarks>
#if NETFRAMEWORK
    protected virtual
#else
    private static
#endif
    string[] GetDirectories(string path) => Directory.GetDirectories(path);

    /// <summary>
    /// Verifies if a file exists.
    /// </summary>
    /// <param name="filePath">The file path.</param>
    /// <returns><c>true</c> if the file exists; <c>false</c> otherwise.</returns>
#if NETFRAMEWORK
    protected virtual
#else
    private static
#endif
    bool DoesFileExist(string filePath) => File.Exists(filePath);

    /// <summary>
    /// Loads an assembly from the given path.
    /// </summary>
    /// <param name="path">The path of the assembly.</param>
    /// <returns>The loaded <see cref="Assembly"/>.</returns>
    [UnconditionalSuppressMessage("ReflectionAnalysis", "IL2026:Members attributed with RequiresUnreferencedCode may break when trimming", Justification = "AssemblyResolver is part of the legacy reflection-mode loader and is not used in source-generator / Native AOT execution mode.")]
#if NETFRAMEWORK
    protected virtual
#else
    private static
#endif
    Assembly LoadAssemblyFrom(string path) => Assembly.LoadFrom(path);

#if NETFRAMEWORK
    /// <summary>
    /// Loads an assembly from the given path in a reflection-only context.
    /// </summary>
    /// <param name="path">The path of the assembly.</param>
    /// <returns>The loaded <see cref="Assembly"/>.</returns>
    protected virtual Assembly ReflectionOnlyLoadAssemblyFrom(string path) => Assembly.ReflectionOnlyLoadFrom(path);
#endif
}
#endif
