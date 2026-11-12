// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

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
internal sealed partial class AppxManifestInfo
{
    /// <summary>
    /// The canonical file name of a packaged app's manifest inside its (loose or extracted) layout.
    /// </summary>
    internal const string AppxManifestFileName = "AppxManifest.xml";

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

}
