// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Acceptance.IntegrationTests;
using Microsoft.Testing.Platform.Acceptance.IntegrationTests.Helpers;
using Microsoft.Testing.Platform.Helpers;

namespace MSTest.Acceptance.IntegrationTests;

[TestClass]
public sealed class IgnoreTests : AcceptanceTestBase<IgnoreTests.TestAssetFixture>
{
    [TestMethod]
    public async Task ClassCleanup_Inheritance_WhenClassIsSkipped()
    {
        var testHost = TestHost.LocateFrom(AssetFixture.ProjectPath, TestAssetFixture.ProjectName, TargetFrameworks.NetCurrent);
        TestHostResult testHostResult = await testHost.ExecuteAsync("--settings my.runsettings --filter ClassName!~TestClassWithAssemblyInitialize");

        // Assert
        testHostResult.AssertExitCodeIs(ExitCodes.Success);
        testHostResult.AssertOutputContainsSummary(failed: 0, passed: 11, skipped: 8);

        testHostResult.AssertOutputContains("SubClass.Method");
        testHostResult.AssertOutputContains("SubClass.ClassCleanup");
        testHostResult.AssertOutputDoesNotContain("SubClass.IgnoredMethod");
    }

    [TestMethod]
    public async Task WhenAllTestsAreIgnored_AssemblyInitializeAndCleanupAreSkipped()
    {
        var testHost = TestHost.LocateFrom(AssetFixture.ProjectPath, TestAssetFixture.ProjectName, TargetFrameworks.NetCurrent);
        TestHostResult testHostResult = await testHost.ExecuteAsync("--settings my.runsettings --filter TestClassWithAssemblyInitialize");

        // Assert
        testHostResult.AssertExitCodeIs(ExitCodes.ZeroTests);
        testHostResult.AssertOutputContainsSummary(failed: 0, passed: 0, skipped: 1);
        testHostResult.AssertOutputDoesNotContain("AssemblyInitialize");
        testHostResult.AssertOutputDoesNotContain("AssemblyCleanup");
    }

    [TestMethod]
    public async Task WhenSpecificDataSourceIsIgnoredViaIgnoreMessageProperty()
    {
        var testHost = TestHost.LocateFrom(AssetFixture.ProjectPath, TestAssetFixture.ProjectName, TargetFrameworks.NetCurrent);
        TestHostResult testHostResult = await testHost.ExecuteAsync("--settings my.runsettings --filter TestClassWithDataSourcesUsingIgnoreMessage");

        // Assert
        testHostResult.AssertExitCodeIs(ExitCodes.Success);
        testHostResult.AssertOutputContains("TestInitialize: TestMethod1 (0)");
        testHostResult.AssertOutputContains("TestCleanup: TestMethod1 (0)");

        testHostResult.AssertOutputContains("TestInitialize: TestMethod1 (2)");
        testHostResult.AssertOutputContains("TestCleanup: TestMethod1 (2)");

        testHostResult.AssertOutputContains("TestInitialize: TestMethod2 (0)");
        testHostResult.AssertOutputContains("TestCleanup: TestMethod2 (0)");

        testHostResult.AssertOutputContains("TestInitialize: TestMethod2 (1)");
        testHostResult.AssertOutputContains("TestCleanup: TestMethod2 (1)");

        testHostResult.AssertOutputContains("TestInitialize: TestMethod2 (2)");
        testHostResult.AssertOutputContains("TestCleanup: TestMethod2 (2)");

        testHostResult.AssertOutputContains("TestInitialize: TestMethod3 (0)");
        testHostResult.AssertOutputContains("TestCleanup: TestMethod3 (0)");

        testHostResult.AssertOutputContains("TestInitialize: TestMethod3 (2)");
        testHostResult.AssertOutputContains("TestCleanup: TestMethod3 (2)");

        testHostResult.AssertOutputContains("TestInitialize: TestMethod4 (0)");
        testHostResult.AssertOutputContains("TestCleanup: TestMethod4 (0)");

        testHostResult.AssertOutputContains("TestInitialize: TestMethod4 (1)");
        testHostResult.AssertOutputContains("TestCleanup: TestMethod4 (1)");

        testHostResult.AssertOutputContains("TestInitialize: TestMethod4 (2)");
        testHostResult.AssertOutputContains("TestCleanup: TestMethod4 (2)");

        testHostResult.AssertOutputContains("skipped TestMethod1");
        testHostResult.AssertOutputContains("skipped TestMethod2");
        testHostResult.AssertOutputContains("skipped TestMethod3 (1)");
        testHostResult.AssertOutputContains("skipped TestMethod4 (3)");
        testHostResult.AssertOutputContains("skipped TestMethod4 (4)");
        testHostResult.AssertOutputContains("skipped TestMethod4 (5)");

        testHostResult.AssertOutputDoesNotContain("TestInitialize: TestMethod1 (1)");
        testHostResult.AssertOutputDoesNotContain("TestCleanup: TestMethod1 (1)");

        testHostResult.AssertOutputDoesNotContain("TestInitialize: TestMethod2 (3)");
        testHostResult.AssertOutputDoesNotContain("TestCleanup: TestMethod2 (3)");
        testHostResult.AssertOutputDoesNotContain("TestInitialize: TestMethod2 (4)");
        testHostResult.AssertOutputDoesNotContain("TestCleanup: TestMethod2 (4)");
        testHostResult.AssertOutputDoesNotContain("TestInitialize: TestMethod2 (5)");
        testHostResult.AssertOutputDoesNotContain("TestCleanup: TestMethod2 (5)");

        testHostResult.AssertOutputDoesNotContain("TestInitialize: TestMethod3 (1)");
        testHostResult.AssertOutputDoesNotContain("TestCleanup: TestMethod3 (1)");

        testHostResult.AssertOutputDoesNotContain("TestInitialize: TestMethod4 (3)");
        testHostResult.AssertOutputDoesNotContain("TestCleanup: TestMethod4 (3)");
        testHostResult.AssertOutputDoesNotContain("TestInitialize: TestMethod4 (4)");
        testHostResult.AssertOutputDoesNotContain("TestCleanup: TestMethod4 (4)");
        testHostResult.AssertOutputDoesNotContain("TestInitialize: TestMethod4 (5)");
        testHostResult.AssertOutputDoesNotContain("TestCleanup: TestMethod4 (5)");

        testHostResult.AssertOutputContainsSummary(failed: 0, passed: 10, skipped: 6);
    }

    public sealed class TestAssetFixture() : TestAssetFixtureBase(AcceptanceFixture.NuGetGlobalPackagesFolder)
    {
        public const string ProjectName = "TestIgnore";

        public string ProjectPath => GetAssetPath(ProjectName);

        public override IEnumerable<(string ID, string Name, string Code)> GetAssetsToGenerate()
        {
            yield return (ProjectName, ProjectName,
                SourceCode
                .PatchTargetFrameworks(TargetFrameworks.NetCurrent)
                .PatchCodeWithReplace("$MSTestVersion$", MSTestVersion));
        }

        private const string SourceCode = """
#file TestIgnore.csproj
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

[Ignore]
[TestClass]
public class UnitTest1
{
    [ClassCleanup]
    public static void ClassCleanup()
        => throw new InvalidOperationException("ClassCleanup should not be called");

    [TestMethod]
    public void Method()
        => throw new InvalidOperationException("Test method should not be called");
}

[TestClass]
public class BaseClass
{
    [ClassCleanup]
    public static void BaseClassCleanup()
        => Console.WriteLine("BaseClass.ClassCleanup");
}

[Ignore]
[TestClass]
public class IntermediateClass : BaseClass
{
    [ClassCleanup]
    public static void IntermediateClassCleanup()
        => throw new InvalidOperationException("IntermediateClass.ClassCleanup should not be called");
}

[TestClass]
public class SubClass : IntermediateClass
{
    [ClassCleanup]
    public static void SubClassCleanup()
        => Console.WriteLine("SubClass.ClassCleanup");

    // Ignore the first test on purpose, see https://github.com/microsoft/testfx/issues/5062
    [TestMethod]
    [Ignore]
    public void IgnoredMethod()
        => Console.WriteLine("SubClass.IgnoredMethod");

    [TestMethod]
    public void Method()
        => Console.WriteLine("SubClass.Method");
}

[TestClass]
public class TestClassWithAssemblyInitialize
{
    [AssemblyInitialize]
    public static void AssemblyInitialize(TestContext context)
    {
        Console.WriteLine("AssemblyInitialize");
    }

    [ClassCleanup]
    public static void AssemblyCleanup()
    {
        Console.WriteLine("AssemblyCleanup");
    }

    [TestMethod, Ignore]
    public void TestMethod1()
    {
    }
}

[TestClass]
public class TestClassWithDataSourcesUsingIgnoreMessage
{
    private readonly TestContext _testContext;

    public TestClassWithDataSourcesUsingIgnoreMessage(TestContext testContext)
        => _testContext = testContext;

    [TestMethod(UnfoldingStrategy = TestDataSourceUnfoldingStrategy.Fold)] // 1 skipped, 2 pass
    [DataRow(0)]
    [DataRow(1, IgnoreMessage = "This data row is ignored")]
    [DataRow(2)]
    public void TestMethod1(int i)
    {
    }

    [TestMethod(UnfoldingStrategy = TestDataSourceUnfoldingStrategy.Fold)] // 1 skipped (folded), 3 pass
    [DynamicData("Data1")]
    [DynamicData("Data2", IgnoreMessage = "This source is ignored")]
    public void TestMethod2(int i)
    {
    }

    [TestMethod(UnfoldingStrategy = TestDataSourceUnfoldingStrategy.Ufold)] // 1 skipped, 2 pass
    [DataRow(0)]
    [DataRow(1, IgnoreMessage = "This data row is ignored")]
    [DataRow(2)]
    public void TestMethod3(int i)
    {
    }

    [TestMethod(UnfoldingStrategy = TestDataSourceUnfoldingStrategy.Unfold)] // 3 skipped (unfolded), 3 pass
    [DynamicData("Data1")]
    [DynamicData("Data2", IgnoreMessage = "This source is ignored")]
    public void TestMethod4(int i)
    {
    }

    [TestInitialize]
    public void TestInit()
    {
        Console.WriteLine($"TestInitialize: {_testContext.TestDisplayName}");
    }

    [TestCleanup]
    public void TestClean()
    {
        Console.WriteLine($"TestCleanup: {_testContext.TestDisplayName}");
    }

    public static IEnumerable<object[]> Data1
    {
        get
        {
            yield return new object[] { 0 };
            yield return new object[] { 1 };
            yield return new object[] { 2 };
        }
    }

    public static IEnumerable<object[]> Data2
    {
        get
        {
            yield return new object[] { 3 };
            yield return new object[] { 4 };
            yield return new object[] { 5 };
        }
    }
}
""";
    }
}
