// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under dual-license. See LICENSE.PLATFORMTOOLS.txt file in the project root for full license information.

using Microsoft.Testing.Platform.Builder;

namespace Microsoft.Testing.Extensions.HotReload;

public static class TestingPlatformBuilderHook
{
    public static void AddExtensions(ITestApplicationBuilder testApplicationBuilder, string[] _)
        => testApplicationBuilder.AddHotReloadProvider();
}
