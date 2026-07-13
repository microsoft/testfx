// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Acceptance.IntegrationTests;
using Microsoft.Testing.Platform.Acceptance.IntegrationTests.Helpers;
using Microsoft.Testing.Platform.Helpers;

namespace MSTest.Acceptance.IntegrationTests;

/// <summary>
/// Validates the end-to-end scenario the WinUI MTP runner sample demonstrates: a real WinUI test host
/// that references <c>Microsoft.Testing.Extensions.PackagedApp</c> is deployed into an isolated directory
/// and launched from there (through the platform's <c>ITestHostLauncher</c> extension point) rather than
/// started in place. It complements <see cref="WinUITests"/> (WinUI without deployment) and the platform's
/// <c>PackagedAppDeploymentTests</c> (deployment with a dummy framework instead of a real WinUI host).
/// </summary>
[TestClass]
public sealed class WinUIPackagedAppDeploymentTests : AcceptanceTestBase<WinUIPackagedAppDeploymentTests.TestAssetFixture>
{
    private static readonly string WinUITargetFramework = $"{TargetFrameworks.NetCurrent}-windows10.0.19041.0";

    [TestMethod]
    [OSCondition(OperatingSystems.Windows, IgnoreMessage = "Packaged WinUI apps are a Windows-only scenario.")]
    public async Task WinUITestHost_IsDeployedAndLaunched_FromDeploymentDirectory()
    {
        var testHost = TestHost.LocateFrom(AssetFixture.ProjectPath, TestAssetFixture.ProjectName, WinUITargetFramework);

        // The deployed WinUI test host reports the directory it actually ran from into this file (it learns
        // the path from the PACKAGEDAPP_BASEDIR_MARKER env var, which the platform forwards to the launched
        // host). Keeping the proof of deployment in the test asset keeps the shipping extension unaware of it.
        string markerPath = Path.Combine(testHost.DirectoryName, "deployment-basedir.txt");

        TestHostResult testHostResult = await testHost.ExecuteAsync(
            environmentVariables: new Dictionary<string, string?> { ["PACKAGEDAPP_BASEDIR_MARKER"] = markerPath },
            cancellationToken: TestContext.CancellationToken);

        // The run must still complete successfully even though the host is deployed elsewhere and launched
        // through a handle that exposes no local PID.
        testHostResult.AssertExitCodeIs(ExitCode.Success);
        testHostResult.AssertOutputContainsSummary(failed: 0, passed: 1, skipped: 0);

        Assert.IsTrue(File.Exists(markerPath), $"Expected the deployed WinUI host to write its base directory to '{markerPath}'.");
        string runtimeBaseDirectory = File.ReadAllText(markerPath).Trim().TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        string originalDirectory = testHost.DirectoryName.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        Assert.AreNotEqual(originalDirectory, runtimeBaseDirectory, "The WinUI test host must have been deployed to and launched from a different directory.");
    }

    public sealed class TestAssetFixture() : TestAssetFixtureBase()
    {
        public const string ProjectName = "WinUIPackagedAppTests";

        public string ProjectPath => GetAssetPath(ProjectName);

        public override (string ID, string Name, string Code) GetAssetsToGenerate() => (ProjectName, ProjectName,
                SourceCode
                .PatchCodeWithReplace("$TargetFramework$", WinUITargetFramework)
                .PatchCodeWithReplace("$MSTestVersion$", MSTestVersion)
                .PatchCodeWithReplace("$MicrosoftTestingExtensionsPackagedAppVersion$", MicrosoftTestingExtensionsPackagedAppVersion));

        private const string SourceCode = """
#file WinUIPackagedAppTests.csproj
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <EnableMSTestRunner>true</EnableMSTestRunner>
    <TargetFramework>$TargetFramework$</TargetFramework>
    <UseWinUI>true</UseWinUI>
    <Nullable>enable</Nullable>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="MSTest.TestAdapter" Version="$MSTestVersion$" />
    <PackageReference Include="MSTest.TestFramework" Version="$MSTestVersion$" />
    <!-- Referencing the PackagedApp package is enough: its MSBuild props auto-register the launcher
         through the generated SelfRegisteredExtensions (no explicit AddPackagedAppDeployment() call). -->
    <PackageReference Include="Microsoft.Testing.Extensions.PackagedApp" Version="$MicrosoftTestingExtensionsPackagedAppVersion$" />
  </ItemGroup>

</Project>

#file UnitTest1.cs
using System;
using System.IO;

using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
public class TestClass1
{
    [TestMethod]
    public void DeployedHostRunsFromDeploymentDirectory()
    {
        // Runs inside the deployed test host, so AppContext.BaseDirectory points at the deployment
        // directory the PackagedApp launcher created. Record it so the acceptance test can prove the
        // host was launched from a different directory than the one it was built into.
        string? markerPath = Environment.GetEnvironmentVariable("PACKAGEDAPP_BASEDIR_MARKER");
        if (!string.IsNullOrEmpty(markerPath))
        {
            File.WriteAllText(markerPath, AppContext.BaseDirectory);
        }
    }
}
""";
    }

    public TestContext TestContext { get; set; }
}
