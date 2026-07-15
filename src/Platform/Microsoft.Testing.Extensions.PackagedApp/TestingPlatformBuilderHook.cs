// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Builder;

namespace Microsoft.Testing.Extensions.PackagedApp;

/// <summary>
/// This class is used by Microsoft.Testing.Platform.MSBuild to hook into the Testing Platform Builder to add Windows test host deployment support (non-packaged loose layouts such as unpackaged WinUI, and packaged MSIX apps launched by AUMID activation).
/// </summary>
public static class TestingPlatformBuilderHook
{
    /// <summary>
    /// Adds Windows test host deployment support to the Testing Platform Builder: it deploys and
    /// launches non-packaged (loose-layout) hosts, and — in the Windows build — registers and activates
    /// packaged (MSIX) hosts by Application User Model ID (see https://github.com/microsoft/testfx/issues/9933).
    /// </summary>
    /// <param name="testApplicationBuilder">The test application builder.</param>
    /// <param name="_">The command line arguments.</param>
    public static void AddExtensions(ITestApplicationBuilder testApplicationBuilder, string[] _)
        => testApplicationBuilder.AddPackagedAppDeployment();
}
