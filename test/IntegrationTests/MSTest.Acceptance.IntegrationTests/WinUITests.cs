// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Acceptance.IntegrationTests;
using Microsoft.Testing.Platform.Acceptance.IntegrationTests.Helpers;
using Microsoft.Testing.Platform.Helpers;

namespace MSTest.Acceptance.IntegrationTests;

/// <summary>
/// Smoke coverage that a <c>UseWinUI</c> project resolves and loads the WinUI-flavored MSTest
/// assemblies and runs to completion. The asset deliberately does not reference
/// <c>Microsoft.WindowsAppSDK</c>, which keeps it buildable by the dotnet muxer on every leg; the real
/// WinUI end-to-end coverage lives in <see cref="UnpackagedWinUITests"/>.
/// </summary>
[TestClass]
public sealed class WinUITests : AcceptanceTestBase<WinUITests.TestAssetFixture>
{
    private static readonly string WinUITargetFramework = $"{TargetFrameworks.NetCurrent}-windows10.0.19041.0";

    [TestMethod]
    [OSCondition(OperatingSystems.Windows, IgnoreMessage = "WinUI is Windows-only")]
    public async Task SimpleWinUITestCase()
    {
        var testHost = TestHost.LocateFrom(AssetFixture.ProjectPath, TestAssetFixture.ProjectName, WinUITargetFramework);
        TestHostResult testHostResult = await testHost.ExecuteAsync(cancellationToken: TestContext.CancellationToken);

        // Assert
        testHostResult.AssertExitCodeIs(ExitCode.Success);
        testHostResult.AssertOutputContainsSummary(failed: 0, passed: 1, skipped: 0);
    }

    public sealed class TestAssetFixture() : TestAssetFixtureBase()
    {
        public const string ProjectName = "WinUITests";

        public string ProjectPath => GetAssetPath(ProjectName);

        public override (string ID, string Name, string Code) GetAssetsToGenerate() => (ProjectName, ProjectName,
                SourceCode
                .PatchCodeWithReplace("$TargetFramework$", WinUITargetFramework)
                .PatchCodeWithReplace("$MSTestVersion$", MSTestVersion));

        private const string SourceCode = """
#file WinUITests.csproj
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <EnableMSTestRunner>true</EnableMSTestRunner>
    <TargetFramework>$TargetFramework$</TargetFramework>
    <UseWinUI>true</UseWinUI>
    <!--
      No package identity. Without a Microsoft.WindowsAppSDK reference this property does not change the
      build; it records the intent, and the process genuinely runs without identity, which is what makes
      this asset cover the unpackaged WIN_UI adapter paths (AppModel.IsPackagedProcess() returning false
      and GetCurrentPackagePath() returning null).
    -->
    <WindowsPackageType>None</WindowsPackageType>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="MSTest.TestAdapter" Version="$MSTestVersion$" />
    <PackageReference Include="MSTest.TestFramework" Version="$MSTestVersion$" />
  </ItemGroup>

</Project>

#file UnitTest1.cs
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
public class TestClass1
{
    [TestMethod]
    public void TestMethod1()
    {
    }
}
""";
    }

    public TestContext TestContext { get; set; }
}
