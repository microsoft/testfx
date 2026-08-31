// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Builder;

namespace Microsoft.Testing.Extensions.PackagedApp;

/// <summary>
/// This class is used by Microsoft.Testing.Platform.MSBuild to hook into the Testing Platform Builder to add Windows test host launch support (packaged, full-trust MSIX desktop apps activated by AUMID, and — when opted in — non-packaged loose layouts deployed to an isolated directory).
/// </summary>
public static class TestingPlatformBuilderHook
{
    /// <summary>
    /// Adds Windows test host launch support to the Testing Platform Builder: it registers and activates
    /// packaged, full-trust MSIX desktop hosts by Application User Model ID in the Windows build (see
    /// https://github.com/microsoft/testfx/issues/9933), and deploys and launches an opted-in
    /// non-packaged (loose-layout) host. Because this hook runs for anyone who merely references the
    /// package, the launcher stays disabled unless the test application is a packaged layout.
    /// </summary>
    /// <param name="testApplicationBuilder">The test application builder.</param>
    /// <param name="_">The command line arguments.</param>
    public static void AddExtensions(ITestApplicationBuilder testApplicationBuilder, string[] _)
        => testApplicationBuilder.AddPackagedAppDeployment();
}
