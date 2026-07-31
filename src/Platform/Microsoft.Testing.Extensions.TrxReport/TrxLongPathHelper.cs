// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Extensions.TrxReport.Abstractions;

/// <summary>
/// Makes Windows paths usable with the <c>System.IO</c> APIs even when they are longer than
/// <c>MAX_PATH</c>.
/// </summary>
/// <remarks>
/// The TRX attachment layout nests every per-test artifact under
/// <c>&lt;resultsDirectory&gt;/&lt;runDeploymentRoot&gt;/In/&lt;executionId&gt;/&lt;machineName&gt;/</c>, which adds
/// roughly 100 characters to a user-supplied results directory. On .NET that is transparent: the
/// runtime rewrites Windows paths beyond <c>MAX_PATH</c> into the <c>\\?\</c> extended-length form
/// before calling Win32. On .NET Framework it is not, and the copy fails with a
/// <see cref="DirectoryNotFoundException"/> that the engine converts into a skipped attachment — so
/// the same test run silently loses its <c>ResultFile</c> entries on .NET Framework only.
/// Applying the prefix ourselves closes that gap. The <c>\\?\</c> syntax is honored by Win32
/// regardless of the <c>LongPathsEnabled</c> machine policy or the process long-path manifest, so
/// this does not depend on any opt-in.
/// </remarks>
internal static class TrxLongPathHelper
{
#if !NETCOREAPP
    /// <summary>
    /// Win32 <c>MAX_PATH</c>. A path of this length or longer needs the extended-length prefix.
    /// </summary>
    /// <remarks>
    /// Deliberately 260 and not the legacy 248-character <em>directory</em> limit. That lower limit
    /// only applies when the consuming application opts into the pre-4.6.2 path quirks
    /// (<c>Switch.System.IO.UseLegacyPathHandling</c>), which is off by default on every framework
    /// version this package supports. Lowering the threshold would prefix every path in the 248-259
    /// band — including file paths, whose real limit is 260 — to cover a configuration almost nobody
    /// runs. In that configuration <see cref="Path.GetFullPath(string)"/> throws first anyway, and the
    /// caller then surfaces a visible warning instead of losing the attachment silently.
    /// </remarks>
    private const int MaxShortPathLength = 260;

    private const string ExtendedPathPrefix = @"\\?\";
    private const string DevicePathPrefix = @"\\.\";
    private const string UncPathPrefix = @"\\";
    private const string ExtendedUncPathPrefix = @"\\?\UNC\";
#endif

    /// <summary>
    /// Returns a form of <paramref name="path"/> that the <c>System.IO</c> APIs can use even when it
    /// exceeds <c>MAX_PATH</c>. The returned value is only meant to be handed to file-system APIs; it
    /// must not be persisted into the TRX because the extended-length form is not a display path.
    /// </summary>
    public static string GetPathForFileSystemAccess(string path)
    {
#if NETCOREAPP
        // .NET already switches to the extended-length form on Windows when the path exceeds MAX_PATH.
        return path;
#else
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            || path.StartsWith(ExtendedPathPrefix, StringComparison.Ordinal)
            || path.StartsWith(DevicePathPrefix, StringComparison.Ordinal))
        {
            return path;
        }

        // The extended-length syntax disables all path normalization, so the value has to be fully
        // qualified and canonical before the prefix is applied. Resolving also has to happen before
        // the length is measured, because a short relative path can still resolve past MAX_PATH.
        // GetFullPath can itself reject the path when the consuming application opted into the legacy
        // (pre-4.6.2) path quirks, in which case we hand back the original path and let the caller
        // surface the failure.
        string fullPath;
        try
        {
            fullPath = Path.GetFullPath(path);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return path;
        }

        // Below the limit the original path already works, and keeping it avoids gratuitously turning
        // caller-supplied relative paths into absolute ones.
        return fullPath.Length < MaxShortPathLength
            ? path
            : fullPath.StartsWith(UncPathPrefix, StringComparison.Ordinal)
                ? ExtendedUncPathPrefix + fullPath.Substring(UncPathPrefix.Length)
                : ExtendedPathPrefix + fullPath;
#endif
    }
}
