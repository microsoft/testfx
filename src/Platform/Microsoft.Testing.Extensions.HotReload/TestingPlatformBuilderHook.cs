// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under dual-license. See LICENSE.PLATFORMTOOLS.txt file in the project root for full license information.

using Microsoft.Testing.Platform.Builder;

namespace Microsoft.Testing.Extensions.HotReload;

/// <summary>
/// This class is used by Microsoft.Testing.Platform.MSBuild to hook into the Testing Platform Builder to add Hot Reload support.
/// </summary>
public static class TestingPlatformBuilderHook
{
    /// <summary>
    /// Adds Hot Reload support to the Testing Platform Builder.
    /// </summary>
    /// <param name="testApplicationBuilder">The test application builder.</param>
    public static void AddExtensions(ITestApplicationBuilder testApplicationBuilder)
        => testApplicationBuilder.AddHotReloadProvider();
}
