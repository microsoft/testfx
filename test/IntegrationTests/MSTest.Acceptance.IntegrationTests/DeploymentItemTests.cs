// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Acceptance.IntegrationTests;
using Microsoft.Testing.Platform.Acceptance.IntegrationTests.Helpers;
using Microsoft.Testing.Platform.Helpers;

namespace MSTest.Acceptance.IntegrationTests;

[TestClass]
public class DeploymentItemTests : AcceptanceTestBase<DeploymentItemTests.TestAssetFixture>
{
    private const string AssetName = nameof(DeploymentItemTests);

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public async Task AssemblyIsLoadedOnceFromDeploymentDirectory()
    {
        var testHost = TestHost.LocateFrom(AssetFixture.TargetAssetPath, AssetName, TargetFrameworks.NetFramework[0]);
        TestHostResult testHostResult = await testHost.ExecuteAsync(cancellationToken: TestContext.CancellationToken);
        testHostResult.AssertOutputContainsSummary(failed: 0, passed: 1, skipped: 0);
        testHostResult.AssertExitCodeIs(ExitCodes.Success);
    }

    public sealed class TestAssetFixture() : TestAssetFixtureBase(AcceptanceFixture.NuGetGlobalPackagesFolder)
    {
        private const string Sources = """
#file DeploymentItemTests.csproj
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFrameworks>$TargetFrameworks$</TargetFrameworks>
    <EnableMSTestRunner>true</EnableMSTestRunner>
    <OutputType>Exe</OutputType>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="MSTest.TestFramework" Version="$MSTestVersion$" />
    <PackageReference Include="MSTest.TestAdapter" Version="$MSTestVersion$" />
  </ItemGroup>

  <ItemGroup>
    <None Update="TestDeploymentItem.xml">
      <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
    </None>
  </ItemGroup>

</Project>

#file TestDeploymentItem.xml
<?xml version="1.0" encoding="utf-8" ?>
<Root />

#file TestClass1.cs
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
public sealed class Test1
{
    public TestContext TestContext { get; set; }

    [TestMethod]
    [DeploymentItem("TestDeploymentItem.xml")]
    public void TestMethod1()
    {
        var asm = Assert.ContainsSingle(AppDomain.CurrentDomain.GetAssemblies().Where(a => a.GetName().Name == "DeploymentItemTests"));
        var path = asm.Location;
        Assert.AreEqual(TestContext.DeploymentDirectory, Path.GetDirectoryName(path));
    }
}
""";

        public string TargetAssetPath => GetAssetPath(AssetName);

        public override IEnumerable<(string ID, string Name, string Code)> GetAssetsToGenerate()
        {
            yield return (AssetName, AssetName,
                Sources
                .PatchTargetFrameworks(TargetFrameworks.NetFramework[0])
                .PatchCodeWithReplace("$MSTestVersion$", MSTestVersion));
        }
    }

    public TestContext TestContext { get; set; }
}
