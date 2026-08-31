// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Acceptance.IntegrationTests;

[TestClass]
public static class AcceptanceFixture
{
    private static string? s_directoryToCleanup;

    [AssemblyInitialize]
    public static void AssemblyInitialize(TestContext context)
    {
        Environment.SetEnvironmentVariable("MSBUILDDISABLENODEREUSE", "1");

        s_directoryToCleanup = Path.Combine(TempDirectory.TestSuiteDirectory, RandomId.Next());

        // Ensure all integration tests restore packages in a centralized place other than the NuGet cache.
        // Local runs keep changing this location so rebuilding same-version development packages cannot
        // reuse stale contents. Cached CI fixture builds need one stable package root for portable cache
        // fingerprints, and nested acceptance commands must use that same populated root.
        string? acceptanceCacheRoot = Environment.GetEnvironmentVariable("TESTFX_ACCEPTANCE_MSBUILD_CACHE_ROOT");
        string? acceptanceCacheMode = Environment.GetEnvironmentVariable("TESTFX_ACCEPTANCE_MSBUILD_CACHE_MODE");
        string? stableNuGetCache = acceptanceCacheMode is "read" or "write"
            && !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("SYSTEM_ACCESSTOKEN"))
            && acceptanceCacheRoot is { Length: > 0 }
                ? Path.Combine(acceptanceCacheRoot, "NuGetPackages")
                : null;
        string nugetCache = stableNuGetCache ?? Path.Combine(s_directoryToCleanup, ".packages");
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
