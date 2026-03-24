// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Acceptance.IntegrationTests;

[TestClass]
public static class AcceptanceFixture
{
    private static string? s_nugetCache;

    [AssemblyInitialize]
    public static void AssemblyInitialize(TestContext context)
    {
        Environment.SetEnvironmentVariable("MSBUILDDISABLENODEREUSE", "1");

        // Ensure all integration tests restore packages in a centralized place other than the NuGet cache.
        // The centralized place also changes between runs (RandomId.Next()) so that re-packaging locally works as expected.
        // So, when running Build.cmd -pack, running test, running Build.cmd -pack again, and running test again, the latest
        // packages should be picked.
        // If we restore to the same place (whether or not it is the machine-wide cache), NuGet will consider restore up-to-date and
        // will use stale packages.
        s_nugetCache = Path.Combine(TempDirectory.TestSuiteDirectory, RandomId.Next(), ".packages");
        Directory.CreateDirectory(s_nugetCache);
        Environment.SetEnvironmentVariable("NUGET_PACKAGES", s_nugetCache);
    }

    [AssemblyCleanup]
    public static void AssemblyCleanup(TestContext context)
    {
        if (s_nugetCache is not null)
        {
            try
            {
                Directory.Delete(s_nugetCache, recursive: true);
            }
            catch (IOException)
            {
            }
        }
    }
}
