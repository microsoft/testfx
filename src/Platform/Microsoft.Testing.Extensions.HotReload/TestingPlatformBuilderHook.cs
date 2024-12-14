// Copyright (c) Microsoft Corporation. All rights reserved.

using Microsoft.Testing.Platform.Builder;

namespace Microsoft.Testing.Extensions.HotReload;

public static class TestingPlatformBuilderHook
{
    public static void AddExtensions(ITestApplicationBuilder testApplicationBuilder, string[] _)
        => testApplicationBuilder.AddHotReloadProvider();
}
