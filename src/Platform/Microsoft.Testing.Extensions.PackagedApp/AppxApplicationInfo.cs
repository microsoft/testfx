// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Extensions.PackagedApp;

/// <summary>
/// A single application declared by a packaged Windows app's <c>AppxManifest.xml</c>
/// (<c>Applications/Application</c>). A package can declare more than one, each with its own
/// Application User Model ID, so the launcher resolves the one matching the requested executable
/// rather than assuming a single application.
/// </summary>
internal sealed class AppxApplicationInfo
{
    internal AppxApplicationInfo(string id, string? executable, string appUserModelId)
    {
        Id = id;
        Executable = executable;
        AppUserModelId = appUserModelId;
    }

    /// <summary>Gets the application id (the manifest's <c>Application/@Id</c>).</summary>
    public string Id { get; }

    /// <summary>
    /// Gets the application's executable file name (the manifest's <c>Application/@Executable</c>),
    /// or <see langword="null"/> when the manifest declares none. Used to disambiguate a package that
    /// declares multiple applications by matching the executable the platform asked to launch.
    /// </summary>
    public string? Executable { get; }

    /// <summary>
    /// Gets the Application User Model ID (<c>{PackageFamilyName}!{Id}</c>) used to activate this
    /// application.
    /// </summary>
    public string AppUserModelId { get; }
}
