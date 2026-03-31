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
    }

    [AssemblyCleanup]
    public static void AssemblyCleanup()
        => NuGetGlobalPackagesFolder.Dispose();
}
