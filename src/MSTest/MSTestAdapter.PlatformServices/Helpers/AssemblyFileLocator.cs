// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Helpers;

/// <summary>
/// Centralizes access to <see cref="Assembly.Location"/> so that single-file / Native AOT
/// scenarios (where <c>Assembly.Location</c> returns an empty string) are handled once instead
/// of being scattered across many call sites with individual <c>IL3000</c> suppressions.
/// </summary>
/// <remarks>
/// All members tolerate <see cref="Assembly.Location"/> returning an empty string — that is the
/// documented behavior for assemblies embedded in single-file or Native AOT executables.
/// Callers receive <see langword="null"/> (for try-style getters) or fall back to
/// <see cref="AppContext.BaseDirectory"/> / the assembly simple name when appropriate.
/// </remarks>
internal static class AssemblyFileLocator
{
    /// <summary>
    /// Returns the file path of the given assembly, or <see langword="null"/> when the
    /// assembly is embedded in a single-file or Native AOT executable (and therefore has no
    /// on-disk location).
    /// </summary>
    [UnconditionalSuppressMessage("SingleFile", "IL3000:Avoid accessing Assembly file path when publishing as a single file", Justification = "Empty return is the documented contract; callers are expected to handle null.")]
    public static string? TryGetLocation(Assembly assembly)
    {
        string location = assembly.Location;
        return string.IsNullOrEmpty(location) ? null : location;
    }

    /// <summary>
    /// Returns the directory containing the given assembly, falling back to
    /// <see cref="AppContext.BaseDirectory"/> when <see cref="Assembly.Location"/> is empty
    /// (single-file / Native AOT).
    /// </summary>
    [UnconditionalSuppressMessage("SingleFile", "IL3000:Avoid accessing Assembly file path when publishing as a single file", Justification = "Falls back to AppContext.BaseDirectory when Assembly.Location is empty (single-file/Native AOT case).")]
    public static string GetDirectoryOrAppContextBase(Assembly assembly)
    {
        string location = assembly.Location;
        if (!string.IsNullOrEmpty(location))
        {
            string? directory = Path.GetDirectoryName(location);
            if (!string.IsNullOrEmpty(directory))
            {
                return directory;
            }
        }

        return AppContext.BaseDirectory;
    }

    /// <summary>
    /// Returns the file name (with extension) of the given assembly, falling back to the
    /// assembly simple name with a <c>.dll</c> suffix when <see cref="Assembly.Location"/>
    /// is empty (single-file / Native AOT).
    /// </summary>
    [UnconditionalSuppressMessage("SingleFile", "IL3000:Avoid accessing Assembly file path when publishing as a single file", Justification = "Falls back to assembly simple name when Assembly.Location is empty (single-file/Native AOT case).")]
    public static string GetFileNameOrSimpleName(Assembly assembly)
    {
        string location = assembly.Location;
        if (!string.IsNullOrEmpty(location))
        {
            return Path.GetFileName(location);
        }

        string? simpleName = assembly.GetName().Name;
        return string.IsNullOrEmpty(simpleName)
            ? string.Empty
            : simpleName + ".dll";
    }
}
