// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.PackagedApp;
using Microsoft.Testing.Platform.Builder;

namespace Microsoft.Testing.Extensions;

/// <summary>
/// Provides extension methods for adding Windows test host deployment support (non-packaged
/// loose layouts such as unpackaged WinUI, and packaged MSIX apps) to the test application builder.
/// </summary>
[Experimental("TPEXP", UrlFormat = "https://aka.ms/testingplatform/diagnostics#{0}")]
public static class PackagedAppExtensions
{
    /// <summary>
    /// Registers a test host launcher for Windows test applications. Non-packaged (loose-layout) hosts
    /// (for example, unpackaged WinUI) are deployed into an isolated directory and launched from there.
    /// Packaged (MSIX) hosts — UWP or packaged WinUI — are registered with the OS and activated by
    /// Application User Model ID in the Windows build of this extension (see
    /// https://github.com/microsoft/testfx/issues/9933); the plain build rejects a packaged layout with
    /// an actionable error.
    /// </summary>
    /// <param name="builder">The test application builder.</param>
    public static void AddPackagedAppDeployment(this ITestApplicationBuilder builder)
    {
        _ = builder ?? throw new System.ArgumentNullException(nameof(builder));

        // When this process is a packaged (MSIX) test host activated by AUMID it did not inherit the
        // controller-to-host connect-back environment variables; restore them from the launcher's
        // hand-off before the platform builds the test application and reads that environment. This runs
        // for both the MSBuild-registered hook and direct callers of this method. It is a no-op for the
        // controller, for non-packaged layouts, and when there is no handshake to consume.
        PackagedAppConnectBackReader.TryApplyConnectBackEnvironment(Environment.GetCommandLineArgs());

        builder.TestHostControllers.AddTestHostLauncher(_ => new PackagedAppTestHostLauncher());
    }
}
