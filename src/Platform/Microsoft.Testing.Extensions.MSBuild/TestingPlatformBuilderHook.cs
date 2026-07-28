// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Builder;

#if !NETCOREAPP
using Polyfills;
#endif

namespace Microsoft.Testing.Platform.MSBuild;

/// <summary>
/// This class is used by Microsoft.Testing.Platform.MSBuild to hook into the Testing Platform Builder to add MSBuild support.
/// </summary>
public static class TestingPlatformBuilderHook
{
    /// <summary>
    /// Adds MSBuild support to the Testing Platform Builder.
    /// </summary>
    /// <param name="testApplicationBuilder">The test application builder.</param>
    /// <param name="_">The command line arguments.</param>
    public static void AddExtensions(ITestApplicationBuilder testApplicationBuilder, string[] _)
    {
        // The MSBuild extension provides the `dotnet test` / MSBuild-node integration, which relies on
        // named-pipe IPC that is unavailable on browser-wasm. This hook is auto-registered for every
        // MSTest runner app (via the generated SelfRegisteredExtensions, because MSTest.TestAdapter
        // references Microsoft.Testing.Platform.MSBuild), so skip it on browser instead of throwing —
        // otherwise every browser-wasm test app crashes at startup. See
        // https://github.com/microsoft/testfx/issues/2196.
        if (OperatingSystem.IsBrowser())
        {
            return;
        }

        testApplicationBuilder.AddMSBuild();
    }
}
