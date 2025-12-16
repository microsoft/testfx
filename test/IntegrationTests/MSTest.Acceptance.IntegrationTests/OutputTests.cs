// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Acceptance.IntegrationTests;
using Microsoft.Testing.Platform.Acceptance.IntegrationTests.Helpers;

namespace MSTest.Acceptance.IntegrationTests;

[TestClass]
public sealed class OutputTests : AcceptanceTestBase<OutputTests.TestAssetFixture>
{
    [TestMethod]
    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    public async Task DetailedOutputIsAsExpected(string tfm)
    {
        var testHost = TestHost.LocateFrom(AssetFixture.ProjectPath, TestAssetFixture.ProjectName, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync("--output detailed", cancellationToken: TestContext.CancellationToken);

        // Assert
        testHostResult.AssertOutputContains("Assert.AreEqual failed. Expected:<1>. Actual:<2>.");
        testHostResult.AssertOutputContains("""
              Standard output
                Console message
                TestContext Messages:
                TestContext message
              Error output
            """);
    }

    [TestMethod]
    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    public async Task DetailedOutput_WithTestFormat_FormatsTestNames(string tfm)
    {
        var testHost = TestHost.LocateFrom(AssetFixture.ProjectPath, TestAssetFixture.ProjectName, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync("--output detailed --test-format \"<fqn>\"", cancellationToken: TestContext.CancellationToken);

        // Assert - verify FQN is shown instead of display name
        testHostResult.AssertOutputContains("UnitTest1.TestMethod");
    }

    public sealed class TestAssetFixture() : TestAssetFixtureBase(AcceptanceFixture.NuGetGlobalPackagesFolder)
    {
        public const string ProjectName = "TestOutput";

        public string ProjectPath => GetAssetPath(ProjectName);

        public override IEnumerable<(string ID, string Name, string Code)> GetAssetsToGenerate()
        {
            yield return (ProjectName, ProjectName,
                SourceCode
                .PatchTargetFrameworks(TargetFrameworks.All)
                .PatchCodeWithReplace("$MSTestVersion$", MSTestVersion));
        }

        private const string SourceCode = """
#file TestOutput.csproj
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <EnableMSTestRunner>true</EnableMSTestRunner>
    <TargetFrameworks>$TargetFrameworks$</TargetFrameworks>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="MSTest.TestAdapter" Version="$MSTestVersion$" />
    <PackageReference Include="MSTest.TestFramework" Version="$MSTestVersion$" />
  </ItemGroup>

</Project>

#file UnitTest1.cs
using System;
using System.Diagnostics;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
public class UnitTest1
{
    public TestContext TestContext { get; set; }

    [TestMethod]
    public void TestMethod()
    {
        Debug.WriteLine("Debug message");
        Console.WriteLine("Console message");
        TestContext.WriteLine("TestContext message");

        Assert.AreEqual(1, 2);
    }
}
""";
    }

    public TestContext TestContext { get; set; }
}
