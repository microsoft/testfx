// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Acceptance.IntegrationTests;

[TestClass]
public static class AcceptanceFixture
{
    private static string? s_directoryToCleanup;
    private static bool s_isAffectedTestCollection;
    private static string? s_previousCoreClrEnableProfiling;
    private static string? s_previousCorEnableProfiling;

    [AssemblyInitialize]
    public static void AssemblyInitialize(TestContext context)
    {
        DotnetCli.UseMultithreadedMSBuild = true;
        Environment.SetEnvironmentVariable("MSBUILDDISABLENODEREUSE", "1");

        s_directoryToCleanup = Path.Combine(TempDirectory.TestSuiteDirectory, RandomId.Next());

        // Ensure all integration tests restore packages in a centralized place other than the NuGet cache.
        // The centralized place also changes between runs (RandomId.Next()) so that re-packaging locally works as expected.
        // So, when running Build.cmd -pack, running test, running Build.cmd -pack again, and running test again, the latest
        // packages should be picked.
        // If we restore to the same place (whether or not it is the machine-wide cache), NuGet will consider restore up-to-date and
        // will use stale packages.
        string nugetCache = Path.Combine(s_directoryToCleanup, ".packages");
        Directory.CreateDirectory(nugetCache);
        Environment.SetEnvironmentVariable("NUGET_PACKAGES", nugetCache);

        s_isAffectedTestCollection = !string.IsNullOrEmpty(
            Environment.GetEnvironmentVariable("MicrosoftInstrumentationEngine_ConfigPath64_CtsInstrumentationProfiler"));
        if (s_isAffectedTestCollection)
        {
            // The profiler is already attached here. Generated consumers have independent CLR/dependency sets
            // and test identities, so they must not inherit an enabled CTS profiler from this process.
            s_previousCoreClrEnableProfiling = Environment.GetEnvironmentVariable("CORECLR_ENABLE_PROFILING");
            s_previousCorEnableProfiling = Environment.GetEnvironmentVariable("COR_ENABLE_PROFILING");
            Environment.SetEnvironmentVariable("CORECLR_ENABLE_PROFILING", "0");
            Environment.SetEnvironmentVariable("COR_ENABLE_PROFILING", "0");
        }
    }

    [AssemblyCleanup]
    public static void AssemblyCleanup(TestContext context)
    {
        try
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
        finally
        {
            if (s_isAffectedTestCollection)
            {
                Environment.SetEnvironmentVariable("CORECLR_ENABLE_PROFILING", s_previousCoreClrEnableProfiling);
                Environment.SetEnvironmentVariable("COR_ENABLE_PROFILING", s_previousCorEnableProfiling);
            }
        }
    }
}
