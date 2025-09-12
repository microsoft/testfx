// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Acceptance.IntegrationTests;
using Microsoft.Testing.Platform.Acceptance.IntegrationTests.Helpers;
using Microsoft.Testing.Platform.Helpers;

namespace MSTest.Acceptance.IntegrationTests;

[TestClass]
public sealed class WinUITests : AcceptanceTestBase<WinUITests.TestAssetFixture>
{
    private static readonly string WinUITargetFramework = $"{TargetFrameworks.NetCurrent}-windows10.0.19041.0";

    [TestMethod]
    public async Task SimpleWinUITestCase()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            // WinUI is Windows-only :)
            return;
        }

        var testHost = TestHost.LocateFrom(AssetFixture.ProjectPath, TestAssetFixture.ProjectName, WinUITargetFramework);
        TestHostResult testHostResult = await testHost.ExecuteAsync(cancellationToken: TestContext.CancellationToken);

        // Assert
        testHostResult.AssertExitCodeIs(ExitCodes.Success);
        testHostResult.AssertOutputContainsSummary(failed: 0, passed: 1, skipped: 0);
    }

    public sealed class TestAssetFixture() : TestAssetFixtureBase(AcceptanceFixture.NuGetGlobalPackagesFolder)
    {
        public const string ProjectName = "WinUITests";

        public string ProjectPath => GetAssetPath(ProjectName);

        public override IEnumerable<(string ID, string Name, string Code)> GetAssetsToGenerate()
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                // WinUI is Windows-only :)
                yield break;
            }

            yield return (ProjectName, ProjectName,
                SourceCode
                .PatchCodeWithReplace("$TargetFramework$", WinUITargetFramework)
                .PatchCodeWithReplace("$MSTestVersion$", MSTestVersion));
        }

        private const string SourceCode = """
#file WinUITests.csproj
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <EnableMSTestRunner>true</EnableMSTestRunner>
    <TargetFramework>$TargetFramework$</TargetFramework>
    <UseWinUI>true</UseWinUI>
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
