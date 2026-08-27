// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Security.Cryptography;

using Microsoft.Testing.Extensions.PackagedApp.Resources;

namespace Microsoft.Testing.Extensions.PackagedApp;

/// <summary>
/// The identity of a packaged Windows (UWP/WinUI) app, read from its <c>AppxManifest.xml</c>.
/// </summary>
/// <remarks>
/// <para>
/// A packaged app is launched by <em>Application User Model ID</em> (AUMID), never by
/// <c>Process.Start</c>. The AUMID is <c>{PackageFamilyName}!{ApplicationId}</c>, and the package
/// family name is <c>{PackageName}_{publisherId}</c> where <c>publisherId</c> is a 13-character hash
/// of the manifest's <c>Publisher</c>. VSTest reads these values from VS-internal deployment
/// components; this type computes them from the manifest using only public, cross-platform managed
/// APIs so the extension does not depend on the Visual Studio install.
/// </para>
/// </remarks>
internal sealed class AppxManifestInfo
{
    /// <summary>
    /// The canonical file name of a packaged app's manifest inside its (loose or extracted) layout.
    /// </summary>
    internal const string AppxManifestFileName = "AppxManifest.xml";

    // The alphabet Windows uses to encode the publisher hash (Douglas Crockford's base32: the digits
    // and lowercase letters with i, l, o and u removed). Must not be reordered.
    private const string PublisherHashAlphabet = "0123456789abcdefghjkmnpqrstvwxyz";

    private AppxManifestInfo(string packageName, string publisher, IReadOnlyList<AppxApplicationInfo> applications)
    {
        PackageName = packageName;
        Publisher = publisher;
        PackageFamilyName = $"{packageName}_{ComputePublisherId(publisher)}";
        Applications = applications;
    }

    /// <summary>Gets the package name (the manifest's <c>Identity/@Name</c>).</summary>
    public string PackageName { get; }

    /// <summary>Gets the publisher (the manifest's <c>Identity/@Publisher</c>).</summary>
    public string Publisher { get; }

    /// <summary>Gets the package family name (<c>{PackageName}_{publisherId}</c>).</summary>
    public string PackageFamilyName { get; }

    /// <summary>
    /// Gets the applications declared by the manifest (<c>Applications/Application</c>), in manifest
    /// order. A package can declare several applications, so callers must select the one they want
    /// (see <see cref="ResolveApplication(string?)"/>) rather than assuming a single entry. The list
    /// is empty when the manifest declares no application.
    /// </summary>
    public IReadOnlyList<AppxApplicationInfo> Applications { get; }

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
        string manifestPath = Path.Combine(layoutDirectory, AppxManifestFileName);
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
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidOperationException or System.Xml.XmlException)
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

            if (executablePath is not null)
            {
                // The launch path knows exactly which executable it was asked to start, so it must match
                // that entry. Accepting any executable that merely sits in the same directory would let a
                // package that declares a different application there be activated under the wrong
                // Application User Model ID.
                if (string.Equals(applicationExecutablePath, executablePath, StringComparison.OrdinalIgnoreCase))
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

    /// <summary>Reads and parses the manifest at <paramref name="manifestPath"/>.</summary>
    /// <param name="manifestPath">The path to an <c>AppxManifest.xml</c>.</param>
    /// <returns>The parsed manifest info.</returns>
    public static AppxManifestInfo ReadFromManifest(string manifestPath)
    {
        using FileStream stream = File.OpenRead(manifestPath);
        return ReadFromManifest(stream);
    }

    /// <summary>Reads and parses a manifest from <paramref name="manifestStream"/>.</summary>
    /// <param name="manifestStream">A stream over an <c>AppxManifest.xml</c>.</param>
    /// <returns>The parsed manifest info.</returns>
    public static AppxManifestInfo ReadFromManifest(Stream manifestStream)
    {
        var document = XDocument.Load(manifestStream);
        XElement? root = document.Root;

        // Match by local name so we are resilient to the manifest schema version (the foundation
        // namespace URI changes across Windows 10 SDK revisions).
        XElement? identity = root?.Elements().FirstOrDefault(static e => e.Name.LocalName == "Identity");
        string? name = identity?.Attribute("Name")?.Value;
        string? publisher = identity?.Attribute("Publisher")?.Value;

        if (name is null || name.Length == 0 || publisher is null || publisher.Length == 0)
        {
            throw new InvalidOperationException(ExtensionResources.InvalidAppxManifestMissingIdentity);
        }

        string packageFamilyName = $"{name}_{ComputePublisherId(publisher)}";

        bool canRunFullTrust = root is not null
            && root.Descendants()
            .Any(static element =>
                element.Name.LocalName == "Capability"
                && string.Equals(element.Attribute("Name")?.Value, "runFullTrust", StringComparison.OrdinalIgnoreCase));

        List<XElement> applicationElements = root?.Elements().FirstOrDefault(static e => e.Name.LocalName == "Applications")?
            .Elements().Where(static e => e.Name.LocalName == "Application")
            .ToList()
            ?? [];

        // A package may declare several applications, each with its own id (and AUMID). Capture them
        // all so the launcher can resolve the one matching the executable it was asked to launch,
        // instead of guessing with the first entry.
        var applications = applicationElements
            .Select(application =>
            {
                string? applicationId = application.Attribute("Id")?.Value;
                string? executable = application.Attribute("Executable")?.Value;
                string? entryPoint = application.Attribute("EntryPoint")?.Value;
                string? trustLevel = application.Attributes().FirstOrDefault(static attribute => attribute.Name.LocalName == "TrustLevel")?.Value;
                string? runtimeBehavior = application.Attributes().FirstOrDefault(static attribute => attribute.Name.LocalName == "RuntimeBehavior")?.Value;
                bool hasFullTrustCompanion = application.Descendants()
                    .Any(static element =>
                        element.Name.LocalName == "Extension"
                        && string.Equals(element.Attribute("Category")?.Value, "windows.fullTrustProcess", StringComparison.OrdinalIgnoreCase));
                return applicationId is null || applicationId.Length == 0
                    ? null
                    : new AppxApplicationInfo(
                        applicationId,
                        executable,
                        $"{packageFamilyName}!{applicationId}",
                        UsesLaunchActivationArguments(
                            entryPoint,
                            trustLevel,
                            runtimeBehavior,
                            canUsePackageFullTrustFallback:
                                canRunFullTrust
                                && applicationElements.Count == 1
                                && !hasFullTrustCompanion),
                        RunsInAppContainer(
                            entryPoint,
                            trustLevel,
                            runtimeBehavior,
                            canUsePackageFullTrustFallback:
                                canRunFullTrust
                                && applicationElements.Count == 1
                                && !hasFullTrustCompanion));
            })
            .OfType<AppxApplicationInfo>()
            .ToList();

        return new AppxManifestInfo(name, publisher, applications);
    }

    /// <summary>
    /// Classifies whether an application runs inside an AppContainer, i.e. with a restricted token. This is
    /// the question the controller-connection authorization asks, and it differs from
    /// <see cref="UsesLaunchActivationArguments"/> in one shape: a <c>packagedClassicApp</c> whose
    /// <c>TrustLevel</c> is <c>appContainer</c> is sandboxed but still receives process <c>argv</c>.
    /// </summary>
    private static bool RunsInAppContainer(
        string? entryPoint,
        string? trustLevel,
        string? runtimeBehavior,
        bool canUsePackageFullTrustFallback)
        // An explicit trust level wins over the activation model: it is a direct statement about the token
        // the host will run with. In particular, a windowsApp can use launch activation while explicitly
        // running at medium integrity, so UsesLaunchActivationArguments cannot answer this question alone.
        => trustLevel switch
        {
            string value when string.Equals(value, "appContainer", StringComparison.OrdinalIgnoreCase) => true,
            string value when string.Equals(value, "mediumIL", StringComparison.OrdinalIgnoreCase) => false,
            _ => UsesLaunchActivationArguments(entryPoint, trustLevel, runtimeBehavior, canUsePackageFullTrustFallback),
        };

    private static bool UsesLaunchActivationArguments(
        string? entryPoint,
        string? trustLevel,
        string? runtimeBehavior,
        bool canUsePackageFullTrustFallback)
    {
        // RuntimeBehavior determines the activation model. A packaged classic or Win32 application
        // receives process argv even when its sandbox TrustLevel is appContainer.
        if (string.Equals(runtimeBehavior, "packagedClassicApp", StringComparison.OrdinalIgnoreCase)
            || string.Equals(runtimeBehavior, "win32App", StringComparison.OrdinalIgnoreCase)
            || string.Equals(entryPoint, "Windows.FullTrustApplication", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (string.Equals(runtimeBehavior, "windowsApp", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (string.Equals(trustLevel, "appContainer", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (string.Equals(trustLevel, "mediumIL", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        // Legacy UWP manifests express AppContainer by omission. A package-wide runFullTrust capability
        // is a safe fallback only for one application with no full-trust companion extension (the shape
        // produced by packaged WinUI). Mixed packages and UWP apps with a windows.fullTrustProcess helper
        // need application-specific desktop evidence.
        return !canUsePackageFullTrustFallback;
    }

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
        AppxApplicationInfo[] exactMatches = [.. Applications.Where(application =>
            application.Executable is not null
            && string.Equals(ResolvePackageRelativePath(manifestDirectory, application.Executable), fullExecutablePath, StringComparison.OrdinalIgnoreCase))];

        if (exactMatches.Length == 1)
        {
            return exactMatches[0];
        }

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
                    string.Join(", ", exactMatches.Select(static application => application.AppUserModelId))));
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

        AppxApplicationInfo[] matches = [.. Applications.Where(application =>
            application.Executable is not null
            && string.Equals(GetExecutableFileName(application.Executable), executableFileName, StringComparison.OrdinalIgnoreCase))];

        return matches.Length == 1
            ? matches[0]
            : throw new InvalidOperationException(
                string.Format(
                    CultureInfo.CurrentCulture,
                    ExtensionResources.AmbiguousAppxManifestApplication,
                    executableFileName,
                    string.Join(", ", Applications.Select(static application => application.AppUserModelId))));
    }

    // The manifest's Application/@Executable is package-root-relative and may include a subdirectory
    // (for example "bin\host.exe"), always with Windows separators. Reduce it to the bare file name so
    // it can be matched against the executable file name the platform asked to launch, independently of
    // the OS running the parser.
    private static string GetExecutableFileName(string executable)
    {
        int lastSeparator = executable.LastIndexOfAny(['\\', '/']);
        return lastSeparator < 0 ? executable : executable[(lastSeparator + 1)..];
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

    /// <summary>
    /// Computes the 13-character publisher id (the suffix of the package family name) for
    /// <paramref name="publisher"/>, using the public Windows algorithm: base32-encode the first 8
    /// bytes of the SHA-256 hash of the UTF-16LE publisher string.
    /// </summary>
    /// <param name="publisher">The manifest's <c>Identity/@Publisher</c> value.</param>
    /// <returns>The 13-character publisher id.</returns>
    internal static string ComputePublisherId(string publisher)
    {
        byte[] hash = SHA256.HashData(Encoding.Unicode.GetBytes(publisher));

        // Take the first 8 bytes (64 bits) and encode them big-endian as 13 base32 characters. 64 bits
        // is not a multiple of 5, so the final character carries the 4 leftover bits padded with a 0.
        var builder = new StringBuilder(13);
        int buffer = 0;
        int bitsInBuffer = 0;
        for (int i = 0; i < 8; i++)
        {
            buffer = (buffer << 8) | hash[i];
            bitsInBuffer += 8;
            while (bitsInBuffer >= 5)
            {
                bitsInBuffer -= 5;
                builder.Append(PublisherHashAlphabet[(buffer >> bitsInBuffer) & 0x1F]);
                buffer &= (1 << bitsInBuffer) - 1;
            }
        }

        if (bitsInBuffer > 0)
        {
            builder.Append(PublisherHashAlphabet[(buffer << (5 - bitsInBuffer)) & 0x1F]);
        }

        return builder.ToString();
    }
}
