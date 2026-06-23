// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Builder;

namespace Microsoft.Testing.Extensions.PackagedApp;

/// <summary>
/// This class is used by Microsoft.Testing.Platform.MSBuild to hook into the Testing Platform Builder to add packaged Windows (UWP/WinUI) test host deployment support.
/// </summary>
public static class TestingPlatformBuilderHook
{
    /// <summary>
    /// Adds packaged Windows (UWP/WinUI) test host deployment support to the Testing Platform Builder.
    /// </summary>
    /// <param name="testApplicationBuilder">The test application builder.</param>
    /// <param name="_">The command line arguments.</param>
    public static void AddExtensions(ITestApplicationBuilder testApplicationBuilder, string[] _)
        => testApplicationBuilder.AddPackagedAppDeployment();
}
