// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.TestInfrastructure;

namespace MSTest.VstestConsoleWrapper.IntegrationTests;

[TestClass]
public static class TestSetup
{
    private static string? s_directoryToCleanup;

    [AssemblyInitialize]
    public static void AssemblyInitialize(TestContext context)
    {
        Environment.SetEnvironmentVariable("MSBUILDDISABLENODEREUSE", "1");

        s_directoryToCleanup = Path.Combine(TempDirectory.TestSuiteDirectory, RandomId.Next());

        // Restore packages into a per-run centralized place (not the machine-wide cache) so that
        // re-packaging locally (build.cmd -pack) between test runs picks the freshly built packages
        // rather than a stale cached copy of the floating -dev version.
        string nugetCache = Path.Combine(s_directoryToCleanup, ".packages");
        Directory.CreateDirectory(nugetCache);
        Environment.SetEnvironmentVariable("NUGET_PACKAGES", nugetCache);
    }

    [AssemblyCleanup]
    public static void AssemblyCleanup(TestContext context)
    {
        if (s_directoryToCleanup is not null)
        {
            try
            {
                Directory.Delete(s_directoryToCleanup, recursive: true);
            }
            catch (IOException)
            {
            }
        }
    }
}
