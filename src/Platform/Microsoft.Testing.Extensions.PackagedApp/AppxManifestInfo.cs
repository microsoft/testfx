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

        // A package may declare several applications, each with its own id (and AUMID). Capture them
        // all so the launcher can resolve the one matching the executable it was asked to launch,
        // instead of guessing with the first entry.
        List<AppxApplicationInfo> applications = root?.Elements().FirstOrDefault(static e => e.Name.LocalName == "Applications")?
            .Elements().Where(static e => e.Name.LocalName == "Application")
            .Select(application =>
            {
                string? applicationId = application.Attribute("Id")?.Value;
                string? executable = application.Attribute("Executable")?.Value;
                return applicationId is null || applicationId.Length == 0
                    ? null
                    : new AppxApplicationInfo(applicationId, executable, $"{packageFamilyName}!{applicationId}");
            })
            .Where(static application => application is not null)
            .Select(static application => application!)
            .ToList()
            ?? [];

        return new AppxManifestInfo(name, publisher, applications);
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
            && string.Equals(application.Executable, executableFileName, StringComparison.OrdinalIgnoreCase))];

        return matches.Length == 1
            ? matches[0]
            : throw new InvalidOperationException(
                string.Format(
                    CultureInfo.CurrentCulture,
                    ExtensionResources.AmbiguousAppxManifestApplication,
                    executableFileName,
                    string.Join(", ", Applications.Select(static application => application.AppUserModelId))));
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
