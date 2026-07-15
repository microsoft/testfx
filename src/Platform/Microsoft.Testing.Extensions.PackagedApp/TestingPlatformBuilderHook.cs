// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Builder;

namespace Microsoft.Testing.Extensions.PackagedApp;

/// <summary>
/// This class is used by Microsoft.Testing.Platform.MSBuild to hook into the Testing Platform Builder to add non-packaged (loose-layout) Windows test host deployment support (for example, unpackaged WinUI).
/// </summary>
public static class TestingPlatformBuilderHook
{
    /// <summary>
    /// Adds non-packaged (loose-layout) Windows test host deployment support (for example, unpackaged WinUI) to the Testing Platform Builder.
    /// Genuinely packaged (MSIX) layouts — UWP or packaged WinUI — are not launched yet and are rejected with an actionable error (see https://github.com/microsoft/testfx/issues/9933).
    /// </summary>
    /// <param name="testApplicationBuilder">The test application builder.</param>
    /// <param name="_">The command line arguments.</param>
    public static void AddExtensions(ITestApplicationBuilder testApplicationBuilder, string[] _)
        => testApplicationBuilder.AddPackagedAppDeployment();
}
