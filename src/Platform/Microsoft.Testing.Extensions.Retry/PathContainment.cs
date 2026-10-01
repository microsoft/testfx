// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under dual-license. See LICENSE.PLATFORMTOOLS.txt file in the project root for full license information.

namespace Microsoft.Testing.Extensions.Policy;

internal static class PathContainment
{
    private static readonly bool IsWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

    internal static bool IsUnderDirectory(string path, string directory)
        => IsUnderNormalizedDirectory(path, NormalizeDirectoryPrefix(directory));

    // Callers that check containment against the same directory across many paths (e.g. a per-record or
    // per-artifact loop) should normalize the prefix once via this method and reuse it with
    // IsUnderNormalizedDirectory, instead of paying the Path.GetFullPath + string allocation cost on every call.
    internal static string NormalizeDirectoryPrefix(string directory)
        => Path.GetFullPath(directory)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;

    internal static bool IsUnderNormalizedDirectory(string path, string normalizedDirectoryPrefix)
    {
        StringComparison comparison = IsWindows
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        return path.StartsWith(normalizedDirectoryPrefix, comparison);
    }
}
