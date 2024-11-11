// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Builder;

namespace Microsoft.Testing.Platform.MSBuild;

public static class TestingPlatformBuilderHook
{
    public static void AddExtensions(ITestApplicationBuilder testApplicationBuilder, string[] _)
        => MSBuildExtensions.AddMSBuild(testApplicationBuilder);
}
