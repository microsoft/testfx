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

    private AppxManifestInfo(string packageName, string publisher, string? applicationId)
    {
        PackageName = packageName;
        Publisher = publisher;
        ApplicationId = applicationId;
        PackageFamilyName = $"{packageName}_{ComputePublisherId(publisher)}";
        AppUserModelId = applicationId is null ? null : $"{PackageFamilyName}!{applicationId}";
    }

    /// <summary>Gets the package name (the manifest's <c>Identity/@Name</c>).</summary>
    public string PackageName { get; }

    /// <summary>Gets the publisher (the manifest's <c>Identity/@Publisher</c>).</summary>
    public string Publisher { get; }

    /// <summary>
    /// Gets the id of the first application declared in the manifest (<c>Applications/Application/@Id</c>),
    /// or <see langword="null"/> when the manifest declares no application.
    /// </summary>
    public string? ApplicationId { get; }

    /// <summary>Gets the package family name (<c>{PackageName}_{publisherId}</c>).</summary>
    public string PackageFamilyName { get; }

    /// <summary>
    /// Gets the Application User Model ID (<c>{PackageFamilyName}!{ApplicationId}</c>) used to activate
    /// the app, or <see langword="null"/> when the manifest declares no application.
    /// </summary>
    public string? AppUserModelId { get; }

    /// <summary>
    /// Reads the manifest at the root of <paramref name="layoutDirectory"/> if the directory is a
    /// packaged-app layout.
    /// </summary>
    /// <param name="layoutDirectory">The directory to probe for an <c>AppxManifest.xml</c>.</param>
    /// <param name="info">The parsed manifest info when the directory is a packaged-app layout.</param>
    /// <returns>
    /// <see langword="true"/> when an <c>AppxManifest.xml</c> was found and parsed; otherwise
    /// <see langword="false"/>.
    /// </returns>
    public static bool TryReadFromLayout(string layoutDirectory, [NotNullWhen(true)] out AppxManifestInfo? info)
    {
        string manifestPath = Path.Combine(layoutDirectory, AppxManifestFileName);
        if (!File.Exists(manifestPath))
        {
            info = null;
            return false;
        }

        info = ReadFromManifest(manifestPath);
        return true;
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

        string? applicationId = root?.Elements().FirstOrDefault(static e => e.Name.LocalName == "Applications")?
            .Elements().FirstOrDefault(static e => e.Name.LocalName == "Application")?
            .Attribute("Id")?.Value;

        return new AppxManifestInfo(name, publisher, applicationId);
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
