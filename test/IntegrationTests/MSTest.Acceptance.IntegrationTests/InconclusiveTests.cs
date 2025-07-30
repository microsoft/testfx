// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Acceptance.IntegrationTests;
using Microsoft.Testing.Platform.Acceptance.IntegrationTests.Helpers;
using Microsoft.Testing.Platform.Helpers;

namespace MSTest.Acceptance.IntegrationTests;

[TestClass]
public sealed class InconclusiveTests : AcceptanceTestBase<InconclusiveTests.TestAssetFixture>
{
    public enum Lifecycle
    {
        AssemblyInitialize,
        ClassInitialize,
        TestInitialize,
        TestMethod,
        TestCleanup,
        ClassCleanup,
        AssemblyCleanup,
    }

    [TestMethod]
    [DataRow(Lifecycle.AssemblyInitialize)]
    [DataRow(Lifecycle.ClassInitialize)]
    [DataRow(Lifecycle.TestInitialize)]
    [DataRow(Lifecycle.TestMethod)]
    [DataRow(Lifecycle.TestCleanup)]
    [DataRow(Lifecycle.ClassCleanup)]
    [DataRow(Lifecycle.AssemblyCleanup)]
    public async Task TestOutcomeShouldBeRespectedCorrectly(Lifecycle inconclusiveStep)
    {
        var testHost = TestHost.LocateFrom(AssetFixture.ProjectPath, TestAssetFixture.ProjectName, TargetFrameworks.NetCurrent);
        TestHostResult testHostResult = await testHost.ExecuteAsync(
            "--settings my.runsettings",
            environmentVariables: new Dictionary<string, string?>
            {
                [$"{inconclusiveStep}Inconclusive"] = "1",
            });

        if (inconclusiveStep >= Lifecycle.ClassCleanup)
        {
            testHostResult.AssertExitCodeIs(ExitCodes.AtLeastOneTestFailed);
            testHostResult.AssertOutputContainsSummary(failed: 1, passed: 0, skipped: 0);
        }
        else
        {
            testHostResult.AssertExitCodeIs(ExitCodes.ZeroTests);
            testHostResult.AssertOutputContainsSummary(failed: 0, passed: 0, skipped: 1);
        }

        testHostResult.AssertOutputContains("AssemblyInitialize called");

        if (inconclusiveStep >= Lifecycle.ClassInitialize)
        {
            testHostResult.AssertOutputContains("ClassInitialize called");
        }
        else
        {
            testHostResult.AssertOutputDoesNotContain("ClassInitialize called");
        }

        if (inconclusiveStep >= Lifecycle.TestInitialize)
        {
            testHostResult.AssertOutputContains("TestInitialize called");
        }
        else
        {
            testHostResult.AssertOutputDoesNotContain("TestInitialize called");
        }

        if (inconclusiveStep >= Lifecycle.TestMethod)
        {
            testHostResult.AssertOutputContains("TestMethod called");
        }
        else
        {
            testHostResult.AssertOutputDoesNotContain("TestMethod called");
        }

        if (inconclusiveStep >= Lifecycle.TestInitialize)
        {
            testHostResult.AssertOutputContains("TestCleanup called");
        }
        else
        {
            testHostResult.AssertOutputDoesNotContain("TestCleanup called");
        }

        if (inconclusiveStep >= Lifecycle.ClassInitialize)
        {
            testHostResult.AssertOutputContains("ClassCleanup called");
        }
        else
        {
            testHostResult.AssertOutputDoesNotContain("ClassCleanup called");
        }

        testHostResult.AssertOutputContains("AssemblyCleanup called");
    }

    public sealed class TestAssetFixture() : TestAssetFixtureBase(AcceptanceFixture.NuGetGlobalPackagesFolder)
    {
        public const string ProjectName = "TestInconclusive";

        public string ProjectPath => GetAssetPath(ProjectName);

        public override IEnumerable<(string ID, string Name, string Code)> GetAssetsToGenerate()
        {
            yield return (ProjectName, ProjectName,
                SourceCode
                .PatchTargetFrameworks(TargetFrameworks.NetCurrent)
                .PatchCodeWithReplace("$MSTestVersion$", MSTestVersion));
        }

        private const string SourceCode = """
#file TestInconclusive.csproj
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

  <ItemGroup>
    <None Update="*.runsettings">
      <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
    </None>
  </ItemGroup>

</Project>

#file my.runsettings
<RunSettings>
  <MSTest>
    <CaptureTraceOutput>false</CaptureTraceOutput>
  </MSTest>
</RunSettings>

#file UnitTest1.cs
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
public class UnitTest1
{
    [AssemblyInitialize]
    public static void AsmInitialize(TestContext _)
    {
        Console.WriteLine("AssemblyInitialize called");
        if (Environment.GetEnvironmentVariable("AssemblyInitializeInconclusive") == "1")
        {
            Assert.Inconclusive();
        }
    }

    [ClassInitialize]
    public static void ClassInit(TestContext _)
    {
        Console.WriteLine("ClassInitialize called");
        if (Environment.GetEnvironmentVariable("ClassInitializeInconclusive") == "1")
        {
            Assert.Inconclusive();
        }
    }

    [TestInitialize]
    public void TestInit()
    {
        Console.WriteLine("TestInitialize called");
        if (Environment.GetEnvironmentVariable("TestInitializeInconclusive") == "1")
        {
            Assert.Inconclusive();
        }
    }

    [TestMethod]
    public void TestMethod()
    {
        Console.WriteLine("TestMethod called");
        if (Environment.GetEnvironmentVariable("TestMethodInconclusive") == "1")
        {
            Assert.Inconclusive();
        }
    }

    [TestCleanup]
    public void TestCleanup()
    {
        Console.WriteLine("TestCleanup called");
        if (Environment.GetEnvironmentVariable("TestCleanupInconclusive") == "1")
        {
            Assert.Inconclusive();
        }
    }

    [ClassCleanup]
    public static void ClassCleanup(TestContext _)
    {
        Console.WriteLine("ClassCleanup called");
        if (Environment.GetEnvironmentVariable("ClassCleanupInconclusive") == "1")
        {
            Assert.Inconclusive();
        }
    }

    [AssemblyCleanup]
    public static void AsmCleanup(TestContext _)
    {
        Console.WriteLine("AssemblyCleanup called");
        if (Environment.GetEnvironmentVariable("AssemblyCleanupInconclusive") == "1")
        {
            Assert.Inconclusive();
        }
    }
}
""";
    }
}
