// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Acceptance.IntegrationTests;

[TestClass]
public static class AcceptanceFixture
{
    public static TempDirectory NuGetGlobalPackagesFolder { get; private set; } = null!;

    [AssemblyInitialize]
    public static void AssemblyInitialize(TestContext context)
    {
        NuGetGlobalPackagesFolder = new(".packages");
        Environment.SetEnvironmentVariable("MSBUILDDISABLENODEREUSE", "1");

        // Our MSTest/MTP integration tests run lots of builds via 'dotnet build' in child processes.
        // Profiling that, we noticed lots of time is spent jitting.
        // In one of the investigated occurrences, we saw this data:
        // Process CPU time 21.11 seconds.
        // 58.2% of that is jitting, which is roughly 12.28 seconds.
        // From the trace:
        // - Jitting trigger: Foreground -> 2.65 seconds.
        // - Jitting trigger: Tiered Compilation Background -> 9.63 seconds
        //
        // Above was running:
        // <RepoRoot>/.dotnet/dotnet.exe" build <RepoRoot>\artifacts\tmp\Debug\testsuite\Umx6q\DataRowTests -c Release -p:MSBuildTreatWarningsAsErrors=true -p:TreatWarningsAsErrors=true -p:SuppressNETCoreSdkPreviewMessage=true -bl:"<RepoRoot>\artifacts\tmp\Debug\testsuite\DataRowTests-87.binlog
        //
        // The whole MSTest.Acceptance.IntegrationTests run when running with tiered compilation enabled locally was roughly 30 minutes.
        // After disabling tiered compilation, it went down to 20 minutes locally.
        Environment.SetEnvironmentVariable("DOTNET_TieredCompilation", "0");
    }

    [AssemblyCleanup]
    public static void AssemblyCleanup()
        => NuGetGlobalPackagesFolder.Dispose();
}
