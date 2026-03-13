// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Acceptance.IntegrationTests;
using Microsoft.Testing.Platform.Acceptance.IntegrationTests.Helpers;

namespace MSTest.Acceptance.IntegrationTests;

[TestClass]
public sealed class AspireSdkTests : AcceptanceTestBase<AspireSdkTests.TestAssetFixture>
{
    public TestContext TestContext { get; set; }

    [TestMethod]
    public async Task EnableAspireProperty_WhenUsingRunner_AllowsToRunAspireTests()
    {
        var testHost = TestHost.LocateFrom(AssetFixture.AspireProjectPath, TestAssetFixture.AspireProjectName, TargetFrameworks.NetCurrent);
        TestHostResult testHostResult = await testHost.ExecuteAsync(cancellationToken: TestContext.CancellationToken);
        testHostResult.AssertOutputContainsSummary(0, 1, 0);
    }

    [TestMethod]
    public async Task EnableAspireProperty_WhenUsingVSTest_AllowsToRunAspireTests()
    {
        var testHost = TestHost.LocateFrom(AssetFixture.AspireProjectPath, TestAssetFixture.AspireProjectName, TargetFrameworks.NetCurrent);
        string exeOrDllName = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? testHost.FullName
            : testHost.FullName + ".dll";
        DotnetMuxerResult dotnetTestResult = await DotnetCli.RunAsync(
            $"test {exeOrDllName}",
            AcceptanceFixture.NuGetGlobalPackagesFolder.Path,
            workingDirectory: AssetFixture.AspireProjectPath,
            warnAsError: false,
            suppressPreviewDotNetMessage: false,
            cancellationToken: TestContext.CancellationToken);
        dotnetTestResult.AssertExitCodeIs(0);
        // Ensure output contains the right platform banner
        dotnetTestResult.AssertOutputContains("VSTest version");
        dotnetTestResult.AssertOutputContains("Passed!  - Failed:     0, Passed:     1, Skipped:     0, Total:     1");
    }

    public sealed class TestAssetFixture() : TestAssetFixtureBase(AcceptanceFixture.NuGetGlobalPackagesFolder)
    {
        public const string AspireProjectName = "AspireProject";

        private const string AspireSourceCode = """
#file AspireProject.csproj
<Project Sdk="MSTest.Sdk/$MSTestVersion$">
  <PropertyGroup>
    <TargetFrameworks>$TargetFrameworks$</TargetFrameworks>
    <LangVersion>latest</LangVersion>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <!-- Disable all extensions by default -->
    <TestingExtensionsProfile>None</TestingExtensionsProfile>
    <EnableAspireTesting>true</EnableAspireTesting>
  </PropertyGroup>

  <ItemGroup>
    <Using Include="System.Threading.Tasks" />
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="$(MicrosoftNETTestSdkVersion)" />
  </ItemGroup>
</Project>

#file global.json
{
  "test": {
    "runner": "VSTest"
  }
}

#file UnitTest1.cs
namespace AspireProject;

[TestClass]
public class IntegrationTest1
{
    [TestMethod]
    public void GetWebResourceRootReturnsOkStatusCode()
    {
        // TODO: Test could be improved to run a real Aspire app, their starter is a big multi-projects app
    }
}
""";

        public string AspireProjectPath => GetAssetPath(AspireProjectName);

        public override IEnumerable<(string ID, string Name, string Code)> GetAssetsToGenerate()
        {
            yield return (AspireProjectName, AspireProjectName,
                AspireSourceCode
                .PatchTargetFrameworks(TargetFrameworks.NetCurrent)
                .PatchCodeWithReplace("$MSTestVersion$", MSTestVersion));
        }
    }
}
