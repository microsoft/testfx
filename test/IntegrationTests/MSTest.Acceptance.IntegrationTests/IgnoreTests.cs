// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Acceptance.IntegrationTests;
using Microsoft.Testing.Platform.Acceptance.IntegrationTests.Helpers;

namespace MSTest.Acceptance.IntegrationTests;

[TestGroup]
public sealed class IgnoreTests : AcceptanceTestBase
{
    private readonly TestAssetFixture _testAssetFixture;

    public IgnoreTests(ITestExecutionContext testExecutionContext, TestAssetFixture testAssetFixture)
        : base(testExecutionContext)
    {
        _testAssetFixture = testAssetFixture;
    }

    public async Task ClassCleanup_Inheritance_WhenClassIsSkipped()
    {
        var testHost = TestHost.LocateFrom(_testAssetFixture.ProjectPath, TestAssetFixture.ProjectName, TargetFrameworks.NetCurrent.Arguments);
        TestHostResult testHostResult = await testHost.ExecuteAsync("--settings my.runsettings --filter ClassName!~TestClassWithAssemblyInitialize");

        // Assert
        testHostResult.AssertExitCodeIs(0);
        testHostResult.AssertOutputContainsSummary(failed: 0, passed: 1, skipped: 1);

        testHostResult.AssertOutputContains("SubClass.Method");
    }

    public async Task WhenAllTestsAreIgnored_AssemblyInitializeAndCleanupAreSkipped()
    {
        var testHost = TestHost.LocateFrom(_testAssetFixture.ProjectPath, TestAssetFixture.ProjectName, TargetFrameworks.NetCurrent.Arguments);
        TestHostResult testHostResult = await testHost.ExecuteAsync("--settings my.runsettings --filter TestClassWithAssemblyInitialize");

        // Assert
        testHostResult.AssertExitCodeIs(8);
        testHostResult.AssertOutputContains("Zero tests ran - Failed: 0, Passed: 0, Skipped: 1, Total: 1");
        testHostResult.AssertOutputDoesNotContain("AssemblyInitialize");
        testHostResult.AssertOutputDoesNotContain("AssemblyCleanup");
    }

    [TestFixture(TestFixtureSharingStrategy.PerTestGroup)]
    public sealed class TestAssetFixture(AcceptanceFixture acceptanceFixture) : TestAssetFixtureBase(acceptanceFixture.NuGetGlobalPackagesFolder)
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
""";
    }
}
