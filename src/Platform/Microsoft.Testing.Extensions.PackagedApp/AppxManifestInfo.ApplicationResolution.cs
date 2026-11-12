// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.PackagedApp.Resources;

namespace Microsoft.Testing.Extensions.PackagedApp;

internal sealed partial class AppxManifestInfo
{
    /// <summary>
    /// Resolves the application whose test host the platform asked to launch, preferring the one whose
    /// declared executable resolves exactly to <paramref name="executablePath"/>. A package may declare
    /// several applications whose executables share a file name in different subdirectories, and the
    /// file name alone cannot tell those apart; the full path can. Falls back to file-name matching
    /// (see <see cref="ResolveApplication(string?)"/>) when no application declares this exact path,
    /// which is the usual case for a manifest that omits <c>Application/@Executable</c>.
    /// </summary>
    /// <param name="manifestDirectory">The directory holding the manifest, used to resolve package-relative executables.</param>
    /// <param name="executablePath">The full path of the executable the platform asked to launch.</param>
    /// <returns>
    /// The matching application, or <see langword="null"/> when the manifest declares no application.
    /// </returns>
    /// <exception cref="InvalidOperationException">
    /// The manifest declares application executables but none of them is
    /// <paramref name="executablePath"/> (so it describes a different application), or several
    /// applications declare that same executable (so it cannot identify one of them).
    /// </exception>
    public AppxApplicationInfo? ResolveApplication(string manifestDirectory, string executablePath)
    {
        string fullExecutablePath = Path.GetFullPath(executablePath);
        AppxApplicationInfo[] exactMatches = [
            .. Applications.Where(application =>
            application.Executable is not null
            && string.Equals(ResolvePackageRelativePath(manifestDirectory, application.Executable), fullExecutablePath, StringComparison.OrdinalIgnoreCase)),
        ];

        if (exactMatches.Length == 1)
        {
            return exactMatches[0];
        }

        // Stryker disable once Equality: the preceding single-match return makes Length == 1 unreachable here.
        if (exactMatches.Length > 1)
        {
            // Several applications declare this very executable, so it cannot identify one of them. That
            // is an ambiguity, not a mismatch: name the candidate Application User Model IDs rather than
            // claiming nothing matched.
            throw new InvalidOperationException(
                string.Format(
                    CultureInfo.CurrentCulture,
                    ExtensionResources.AmbiguousAppxManifestApplication,
                    executablePath,
                    string.Join(", ", exactMatches.Select(static application => application.AppUserModelId)));
        }

        // The manifest names executables, but not the one we were asked to launch: it describes a
        // different application, and activating that would use the wrong Application User Model ID. Only
        // fall back to file-name resolution when nothing declares an executable, which is the case that
        // cannot be validated at all (a minimal manifest that omits Application/@Executable).
        AppxApplicationInfo[] declaringApplications = [.. Applications.Where(static application => application.Executable is not null)];

        return declaringApplications.Length == 0
            ? ResolveApplication(Path.GetFileName(executablePath))
            : throw new InvalidOperationException(
                string.Format(
                    CultureInfo.CurrentCulture,
                    ExtensionResources.AppxManifestApplicationExecutableMismatch,
                    executablePath,
                    string.Join(", ", declaringApplications.Select(static application => application.Executable))));
    }

    /// <summary>
    /// Resolves the application whose test host the platform asked to launch. Most packages declare a
    /// single application, which is returned directly. When a package declares several, the entry is
    /// disambiguated by matching <paramref name="executableFileName"/> against each
    /// <c>Application/@Executable</c>; an ambiguous request (no match, or several matches) is rejected
    /// rather than silently defaulting to the first application, which would identify the wrong app.
    /// </summary>
    /// <param name="executableFileName">
    /// The file name (not full path) of the executable the platform asked to launch, used to pick the
    /// matching application when the manifest declares more than one.
    /// </param>
    /// <returns>
    /// The matching application, or <see langword="null"/> when the manifest declares no application.
    /// </returns>
    /// <exception cref="InvalidOperationException">
    /// The manifest declares multiple applications and <paramref name="executableFileName"/> does not
    /// match exactly one of them.
    /// </exception>
    public AppxApplicationInfo? ResolveApplication(string? executableFileName)
    {
        switch (Applications.Count)
        {
            case 0:
                return null;
            case 1:
                return Applications[0];
        }

        AppxApplicationInfo[] matches = [
            .. Applications.Where(application =>
            application.Executable is not null
            && string.Equals(GetExecutableFileName(application.Executable), executableFileName, StringComparison.OrdinalIgnoreCase)),
        ];

        return matches.Length == 1
            ? matches[0]
            : throw new InvalidOperationException(
                string.Format(
                    CultureInfo.CurrentCulture,
                    ExtensionResources.AmbiguousAppxManifestApplication,
                    executableFileName,
                    string.Join(", ", Applications.Select(static application => application.AppUserModelId)));
    }

    // The manifest's Application/@Executable is package-root-relative and may include a subdirectory
    // (for example "bin\host.exe"), always with Windows separators. Reduce it to the bare file name so
    // it can be matched against the executable file name the platform asked to launch, independently of
    // the OS running the parser.
    private static string GetExecutableFileName(string executable)
    {
        int lastSeparator = executable.LastIndexOfAny(['\\', '/']);
        return executable[(lastSeparator + 1)..];
    }

    private static string? ResolvePackageRelativePath(string manifestDirectory, string packageRelativePath)
    {
        if (packageRelativePath.Length == 0
            || packageRelativePath[0] is '\\' or '/'
            || packageRelativePath.Contains(':', StringComparison.Ordinal))
        {
            return null;
        }

        string fullManifestDirectory = Path.GetFullPath(manifestDirectory);
        string[] pathSegments = packageRelativePath.Split(['\\', '/'], StringSplitOptions.RemoveEmptyEntries);
        string resolvedPath = Path.GetFullPath(Path.Combine([fullManifestDirectory, .. pathSegments]));
        string relativePath = Path.GetRelativePath(fullManifestDirectory, resolvedPath);

        return relativePath == ".."
            || relativePath.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
            || relativePath.StartsWith($"..{Path.AltDirectorySeparatorChar}", StringComparison.Ordinal)
            || Path.IsPathRooted(relativePath)
            ? null
            : resolvedPath;
    }
}
