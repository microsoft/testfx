// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.MSBuild;
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
    private const string TestHostControllerPidOption = "--internal-testhostcontroller-pid";
    private const string RetryPipeNameOption = "--internal-retry-pipename";

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

        // The full-trust controller owns the MSBuild-node connection and relays the child host's
        // messages through the controller protocol. A sandboxed test host cannot and must not open
        // the outer build pipe directly.
        if (_.Contains(TestHostControllerPidOption, StringComparer.Ordinal)
            || _.Contains(RetryPipeNameOption, StringComparer.Ordinal))
        {
            testApplicationBuilder.CommandLine.AddProvider(() => new MSBuildCommandLineProvider());
            return;
        }

        testApplicationBuilder.AddMSBuild();
    }
}
