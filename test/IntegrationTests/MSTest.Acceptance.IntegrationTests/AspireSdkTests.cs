// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.MSTestV2.CLIAutomation;
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
    [OSCondition(OperatingSystems.Windows)]
    public async Task EnableAspireProperty_WhenUsingVSTest_AllowsToRunAspireTests()
    {
        var testHost = TestHost.LocateFrom(AssetFixture.AspireProjectPath, TestAssetFixture.AspireProjectName, TargetFrameworks.NetCurrent);
        string testApplicationSource = GetTestApplicationSourcePath(testHost);

        using var commandLine = new CommandLine();
        await commandLine.RunAsync(
            $"\"{VSTestConsoleLocator.GetConsoleRunnerPath()}\" \"{testApplicationSource}\"",
            cancellationToken: TestContext.CancellationToken);

        Assert.Contains("VSTest version", commandLine.StandardOutput);
        Assert.Contains("Test Run Successful.", commandLine.StandardOutput);
        Assert.Contains("Total tests: 1", commandLine.StandardOutput);
        Assert.Contains("Passed: 1", commandLine.StandardOutput);
    }

    public sealed class TestAssetFixture() : TestAssetFixtureBase()
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
    <PackageDownload Include="Microsoft.TestPlatform" Version="[$(MicrosoftNETTestSdkVersion)]" />
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

        public override (string ID, string Name, string Code) GetAssetsToGenerate() => (AspireProjectName, AspireProjectName,
                AspireSourceCode
                .PatchTargetFrameworks(TargetFrameworks.NetCurrent)
                .PatchCodeWithReplace("$MSTestVersion$", MSTestVersion));
    }
}
