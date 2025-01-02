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
        testHostResult.AssertExitCodeIs(0);
        testHostResult.AssertOutputContainsSummary(failed: 0, passed: 1, skipped: 1);

        testHostResult.AssertOutputContains("SubClass.Method");
    }

    [TestMethod]
    public async Task WhenAllTestsAreIgnored_AssemblyInitializeAndCleanupAreSkipped()
    {
        var testHost = TestHost.LocateFrom(AssetFixture.ProjectPath, TestAssetFixture.ProjectName, TargetFrameworks.NetCurrent);
        TestHostResult testHostResult = await testHost.ExecuteAsync("--settings my.runsettings --filter TestClassWithAssemblyInitialize");

        // Assert
        testHostResult.AssertExitCodeIs(8);
        testHostResult.AssertOutputContainsSummary(failed: 0, passed: 0, skipped: 1);
        testHostResult.AssertOutputDoesNotContain("AssemblyInitialize");
        testHostResult.AssertOutputDoesNotContain("AssemblyCleanup");
    }

    [TestMethod]
    public async Task WhenTestClassIsIgnoredViaIgnoreMessageProperty()
    {
        var testHost = TestHost.LocateFrom(AssetFixture.ProjectPath, TestAssetFixture.ProjectName, TargetFrameworks.NetCurrent);
        TestHostResult testHostResult = await testHost.ExecuteAsync("--settings my.runsettings --filter TestClassWithIgnoreMessage");

        // Assert
        testHostResult.AssertExitCodeIs(ExitCodes.ZeroTests);
        testHostResult.AssertOutputContainsSummary(failed: 0, passed: 1, skipped: 1);
    }

    [TestMethod]
    public async Task WhenTestMethodIsIgnoredViaIgnoreMessageProperty()
    {
        var testHost = TestHost.LocateFrom(AssetFixture.ProjectPath, TestAssetFixture.ProjectName, TargetFrameworks.NetCurrent);
        TestHostResult testHostResult = await testHost.ExecuteAsync("--settings my.runsettings --filter TestClassWithMethodUsingIgnoreMessage");

        // Assert
        testHostResult.AssertExitCodeIs(ExitCodes.Success);
        testHostResult.AssertOutputContainsSummary(failed: 0, passed: 0, skipped: 1);
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

[TestClass(IgnoreMessage = "This test class is ignored")]
public class TestClassWithIgnoreMessage
{
    [TestMethod]
    public void TestMethod1()
    {
    }
}

[TestClass]
public class TestClassWithMethodUsingIgnoreMessage
{
    [TestMethod(IgnoreMessage = "This test method is ignored")]
    public void TestMethod1()
    {
    }

    [TestMethod]
    public void TestMethod2()
    {
    }
}
""";
    }
}
