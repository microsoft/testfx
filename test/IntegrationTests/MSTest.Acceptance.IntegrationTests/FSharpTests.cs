// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Acceptance.IntegrationTests;
using Microsoft.Testing.TestInfrastructure;

namespace MSTest.Acceptance.IntegrationTests;

/// <summary>
/// Acceptance-style rewrite of the former CLITestBase-based F# test. The F# test asset is generated
/// inline and built, then executed out-of-process through the MSTest runner
/// (Microsoft.Testing.Platform) host.
/// </summary>
[TestClass]
public sealed class FSharpTests : AcceptanceTestBase<FSharpTests.TestAssetFixture>
{
    private const string ProjectName = "FSharpTestProject";

    public TestContext TestContext { get; set; } = default!;

    [TestMethod]
    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    public async Task DiscoversFSharpTestWithSpaceAndDotInName(string tfm)
    {
        TestHost testHost = AssetFixture.GetTestHost(tfm);

        TestHostResult listResult = await testHost.ExecuteAsync("--list-tests", cancellationToken: TestContext.CancellationToken);
        Assert.AreEqual(0, listResult.ExitCode, listResult.StandardOutput);
        Assert.Contains("Test method passing with a . in it", listResult.StandardOutput);
    }

    [TestMethod]
    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    public async Task RunsFSharpTestWithSpaceAndDotInName(string tfm)
    {
        TestHost testHost = AssetFixture.GetTestHost(tfm);
        TestHostResult result = await testHost.ExecuteAsync(cancellationToken: TestContext.CancellationToken);

        Assert.AreEqual(0, result.ExitCode, result.StandardOutput);
        Assert.Contains("Test run summary: Passed!", result.StandardOutput);
        Assert.Contains("failed: 0", result.StandardOutput);
        Assert.Contains("succeeded: 1", result.StandardOutput);
    }

    public sealed class TestAssetFixture : ITestAssetFixture
    {
        private readonly TempDirectory _tempDirectory = new();
        private TestAsset? _asset;

        public TestHost GetTestHost(string tfm)
            => TestHost.LocateFrom(_asset!.TargetAssetPath, ProjectName, tfm);

        public async Task InitializeAsync(CancellationToken cancellationToken)
        {
            string code = SourceCode
                .PatchTargetFrameworks(TargetFrameworks.All)
                .PatchCodeWithReplace("$MSTestVersion$", MSTestVersion);
            _asset = await TestAsset.GenerateAssetAsync(ProjectName, code, _tempDirectory);
            await DotnetCli.RunAsync($"build \"{_asset.TargetAssetPath}\" -c Release", callerMemberName: ProjectName, cancellationToken: cancellationToken);
        }

        public void Dispose()
        {
            _asset?.Dispose();
            _tempDirectory.Dispose();
        }

        private const string SourceCode = """
#file FSharpTestProject.fsproj
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFrameworks>$TargetFrameworks$</TargetFrameworks>
    <OutputType>Exe</OutputType>
    <EnableMSTestRunner>true</EnableMSTestRunner>
    <GenerateProgramFile>false</GenerateProgramFile>
    <EnableDefaultCompileItems>false</EnableDefaultCompileItems>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="MSTest.TestAdapter" Version="$MSTestVersion$" />
    <PackageReference Include="MSTest.TestFramework" Version="$MSTestVersion$" />
  </ItemGroup>

  <ItemGroup>
    <Compile Include="Tests.fs" />
  </ItemGroup>

</Project>

#file Tests.fs
namespace FSharpTestProject

open System
open Microsoft.VisualStudio.TestTools.UnitTesting

[<TestClass>]
type ``This is a test type`` () =

    [<TestMethod>]
    member this.``Test method passing with a . in it`` () =
        Assert.IsTrue(true);
""";
    }
}
