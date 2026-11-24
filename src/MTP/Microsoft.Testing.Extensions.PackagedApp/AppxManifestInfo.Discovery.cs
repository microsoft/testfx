// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Extensions.PackagedApp;

internal sealed partial class AppxManifestInfo
{
    /// <summary>
    /// Returns the path to the <c>AppxManifest.xml</c> at the root of <paramref name="layoutDirectory"/>
    /// when the directory is a packaged-app layout. This is a cheap, non-throwing probe: it only tests
    /// for the file's existence and never parses it. Callers that need the parsed identity pass the
    /// returned path to <see cref="ReadFromManifest(string)"/>.
    /// </summary>
    /// <param name="layoutDirectory">The directory to probe for an <c>AppxManifest.xml</c>.</param>
    /// <returns>
    /// The full path to the manifest when the directory is a packaged-app layout; otherwise
    /// <see langword="null"/>.
    /// </returns>
    public static string? GetManifestPath(string layoutDirectory)
    {
        string manifestPath = Path.Combine(layoutDirectory, Path.GetFileName(AppxManifestFileName));
        return File.Exists(manifestPath) ? manifestPath : null;
    }

    /// <summary>
    /// Searches <paramref name="startDirectory"/> and its ancestors for an <c>AppxManifest.xml</c>
    /// that plausibly describes the app under <paramref name="startDirectory"/>. A packaged app's
    /// manifest lives at the package layout root, but an <c>Application/@Executable</c> can point into
    /// an arbitrarily deep subdirectory (for example <c>bin\host.exe</c>), so the executable's own
    /// directory is not necessarily the root. The manifest in <paramref name="startDirectory"/> is
    /// accepted as the app's own layout without parsing; ancestor manifests are parsed and accepted only
    /// when an application executable resolves back to <paramref name="startDirectory"/>. Malformed or
    /// unreadable ancestor manifests are ignored.
    /// </summary>
    /// <param name="startDirectory">The directory to start searching from (typically the executable's directory).</param>
    /// <returns>
    /// The full path to the nearest <c>AppxManifest.xml</c> at or above <paramref name="startDirectory"/>
    /// that matches the app directory; otherwise <see langword="null"/>.
    /// </returns>
    public static string? FindManifestPath(string startDirectory)
        => FindManifestPathCore(startDirectory, executablePath: null);

    /// <summary>
    /// Searches <paramref name="startDirectory"/> and its ancestors for an <c>AppxManifest.xml</c>
    /// that plausibly describes <paramref name="executablePath"/>. The manifest in
    /// <paramref name="startDirectory"/> is accepted as the app's own layout without parsing; an
    /// ancestor manifest is parsed and accepted only when one of its applications declares exactly
    /// <paramref name="executablePath"/>. Matching the whole path (rather than just the directory, as
    /// the enablement overload must) keeps a package that declares a different application alongside it
    /// from being activated under the wrong Application User Model ID. Malformed or unreadable ancestor
    /// manifests are ignored.
    /// </summary>
    /// <param name="startDirectory">The directory to start searching from (typically the executable's directory).</param>
    /// <param name="executablePath">The executable that the manifest must describe when found in an ancestor directory.</param>
    /// <returns>
    /// The full path to the nearest matching <c>AppxManifest.xml</c> at or above
    /// <paramref name="startDirectory"/>; otherwise <see langword="null"/>.
    /// </returns>
    public static string? FindManifestPath(string startDirectory, string executablePath)
        => FindManifestPathCore(startDirectory, executablePath);

    private static string? FindManifestPathCore(string startDirectory, string? executablePath)
    {
        // The trailing separator matters: the launcher passes AppContext.BaseDirectory, which ends with
        // one, while Path.GetDirectoryName never returns one. Comparing the two unnormalized would never
        // match, so a valid ancestor manifest would never be attributed during enablement.
        string fullStartDirectory = NormalizeDirectory(startDirectory);
        string? fullExecutablePath = executablePath is null ? null : Path.GetFullPath(executablePath);
        DirectoryInfo? directory = new(startDirectory);
        bool isStartDirectory = true;
        while (directory is not null)
        {
            string? manifestPath = GetManifestPath(directory.FullName);
            if (manifestPath is not null
                && (isStartDirectory || IsAncestorManifestForApp(manifestPath, directory.FullName, fullStartDirectory, fullExecutablePath)))
            {
                return manifestPath;
            }

            directory = directory.Parent;
            isStartDirectory = false;
        }

        return null;
    }

    private static bool IsAncestorManifestForApp(string manifestPath, string manifestDirectory, string appDirectory, string? executablePath)
    {
        AppxManifestInfo manifestInfo;
        try
        {
            manifestInfo = ReadFromManifest(manifestPath);
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
        catch (System.Xml.XmlException)
        {
            return false;
        }

        foreach (AppxApplicationInfo application in manifestInfo.Applications)
        {
            if (application.Executable is null)
            {
                continue;
            }

            string? applicationExecutablePath = ResolvePackageRelativePath(manifestDirectory, application.Executable);
            if (applicationExecutablePath is null)
            {
                continue;
            }

            if (executablePath is string expectedExecutablePath)
            {
                // The launch path knows exactly which executable it was asked to start, so it must match
                // that entry. Accepting any executable that merely sits in the same directory would let a
                // package that declares a different application there be activated under the wrong
                // Application User Model ID.
                if (string.Equals(applicationExecutablePath, expectedExecutablePath, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                continue;
            }

            // Enablement only knows the app directory (there is no launch context yet), so attribute the
            // manifest when it places an application there.
            string? applicationDirectory = Path.GetDirectoryName(applicationExecutablePath);
            if (applicationDirectory is not null
                && string.Equals(NormalizeDirectory(applicationDirectory), appDirectory, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Returns <paramref name="directory"/> as a full path without a trailing directory separator, so
    /// that directories obtained from different APIs compare equal. <c>AppContext.BaseDirectory</c> ends
    /// with a separator while <see cref="Path.GetDirectoryName(string)"/> never does.
    /// </summary>
    private static string NormalizeDirectory(string directory)
        => Path.TrimEndingDirectorySeparator(Path.GetFullPath(directory));
}
