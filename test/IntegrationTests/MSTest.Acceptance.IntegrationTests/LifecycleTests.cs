// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Acceptance.IntegrationTests;
using Microsoft.Testing.Platform.Acceptance.IntegrationTests.Helpers;
using Microsoft.Testing.Platform.Helpers;

namespace MSTest.Acceptance.IntegrationTests;

[TestClass]
public sealed class LifecycleTests : AcceptanceTestBase<LifecycleTests.TestAssetFixture>
{
    [TestMethod]
    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    public async Task LifecycleTest(string tfm)
    {
        var testHost = TestHost.LocateFrom(AssetFixture.ProjectPath, TestAssetFixture.ProjectName, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync("--settings my.runsettings", cancellationToken: TestContext.CancellationToken);

        testHostResult.AssertExitCodeIs(ExitCodes.Success);
        testHostResult.AssertOutputContainsSummary(failed: 0, passed: 4, skipped: 0);
        // Order is:
        // - Assembly initialize
        // - foreach test class
        //     - ClassInitialize
        //     - foreach test:
        //         - GlobalTestInitialize
        //         - ctor
        //         - TestContext property setter
        //         - TestInitialize
        //         - TestMethod
        //         - TestCleanup
        //         - Dispose
        //         - GlobalTestCleanup
        //     - ClassCleanup
        // - AssemblyCleanup
        testHostResult.AssertOutputContains("""
            AssemblyInitialize called.
            TestClass1.ClassInitialize called.
            GlobalTestInitialize called for 'TestMethodParameterized (0)'.
            TestClass1 constructor called.
            TestContext property set for TestClass1.
            TestClass1.TestInitialize for 'TestMethodParameterized (0)' is called.
            TestMethodParameterized called with: 0
            TestClass1.TestCleanup for 'TestMethodParameterized (0)' is called.
            TestClass1 disposed.
            GlobalTestCleanup called for 'TestMethodParameterized (0)'.
            GlobalTestInitialize called for 'TestMethodParameterized (1)'.
            TestClass1 constructor called.
            TestContext property set for TestClass1.
            TestClass1.TestInitialize for 'TestMethodParameterized (1)' is called.
            TestMethodParameterized called with: 1
            TestClass1.TestCleanup for 'TestMethodParameterized (1)' is called.
            TestClass1 disposed.
            GlobalTestCleanup called for 'TestMethodParameterized (1)'.
            GlobalTestInitialize called for 'TestMethodNonParameterized'.
            TestClass1 constructor called.
            TestContext property set for TestClass1.
            TestClass1.TestInitialize for 'TestMethodNonParameterized' is called.
            TestMethodNonParameterized called
            TestClass1.TestCleanup for 'TestMethodNonParameterized' is called.
            TestClass1 disposed.
            GlobalTestCleanup called for 'TestMethodNonParameterized'.
            TestClass1.ClassCleanup called.
            TestClass2.ClassInitialize called.
            GlobalTestInitialize called for 'TestMethodFromTestClass2'.
            TestClass2 constructor called.
            TestContext property set for TestClass2.
            TestClass2.TestInitialize for 'TestMethodFromTestClass2' is called.
            TestMethodFromTestClass2 called
            TestClass2.TestCleanup for 'TestMethodFromTestClass2' is called.
            TestClass2 disposed.
            GlobalTestCleanup called for 'TestMethodFromTestClass2'.
            TestClass2.ClassCleanup called.
            AssemblyCleanup called.
            """);
    }

    public sealed class TestAssetFixture() : TestAssetFixtureBase(AcceptanceFixture.NuGetGlobalPackagesFolder)
    {
        public const string ProjectName = "LifecycleTests";

        public string ProjectPath => GetAssetPath(ProjectName);

        public override IEnumerable<(string ID, string Name, string Code)> GetAssetsToGenerate()
        {
            yield return (ProjectName, ProjectName,
                SourceCode
                .PatchTargetFrameworks(TargetFrameworks.All)
                .PatchCodeWithReplace("$MSTestVersion$", MSTestVersion));
        }

        private const string SourceCode = """
#file LifecycleTests.csproj
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <EnableMSTestRunner>true</EnableMSTestRunner>
    <TargetFrameworks>$TargetFrameworks$</TargetFrameworks>
    <LangVersion>preview</LangVersion>
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

#file TestClass1.cs
using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
public static class Fixtures
{
    [AssemblyInitialize]
    public static void AssemblyInitialize(TestContext context)
        => Console.WriteLine("AssemblyInitialize called.");

    [AssemblyCleanup]
    public static void AssemblyCleanup(TestContext context)
        => Console.WriteLine("AssemblyCleanup called.");

    [GlobalTestInitialize]
    public static void GlobalTestInitialize(TestContext context)
        => Console.WriteLine($"GlobalTestInitialize called for '{context.TestDisplayName}'.");

    [GlobalTestCleanup]
    public static void GlobalTestCleanup(TestContext context)
        => Console.WriteLine($"GlobalTestCleanup called for '{context.TestDisplayName}'.");
}

[TestClass]
public class TestClass1 : IDisposable
{
    public TestClass1()
    {
        Console.WriteLine("TestClass1 constructor called.");
    }

    public TestContext TestContext
    {
        get => field;
        set
        {
            field = value;
            Console.WriteLine("TestContext property set for TestClass1.");
        }
    }

    [TestInitialize]
    public void TestInitialize()
    {
        Console.WriteLine($"TestClass1.TestInitialize for '{TestContext.TestDisplayName}' is called.");
    }

    [TestCleanup]
    public void TestCleanup()
    {
        Console.WriteLine($"TestClass1.TestCleanup for '{TestContext.TestDisplayName}' is called.");
    }

    [ClassInitialize]
    public static void ClassInitialize(TestContext context)
        => Console.WriteLine("TestClass1.ClassInitialize called.");

    [ClassCleanup]
    public static void ClassCleanup(TestContext context)
        => Console.WriteLine("TestClass1.ClassCleanup called.");

    [TestMethod]
    [DataRow(0)]
    [DataRow(1)]
    public void TestMethodParameterized(int a)
        => Console.WriteLine($"TestMethodParameterized called with: {a}");

    [TestMethod]
    public void TestMethodNonParameterized()
        => Console.WriteLine("TestMethodNonParameterized called");

    public void Dispose()
        => Console.WriteLine("TestClass1 disposed.");
}

[TestClass]
public class TestClass2 : IDisposable
{
    public TestClass2()
    {
        Console.WriteLine("TestClass2 constructor called.");
    }

    public TestContext TestContext
    {
        get => field;
        set
        {
            field = value;
            Console.WriteLine("TestContext property set for TestClass2.");
        }
    }

    [TestInitialize]
    public void TestInitialize()
    {
        Console.WriteLine($"TestClass2.TestInitialize for '{TestContext.TestDisplayName}' is called.");
    }

    [TestCleanup]
    public void TestCleanup()
    {
        Console.WriteLine($"TestClass2.TestCleanup for '{TestContext.TestDisplayName}' is called.");
    }

    [ClassInitialize]
    public static void ClassInitialize(TestContext context)
        => Console.WriteLine("TestClass2.ClassInitialize called.");

    [ClassCleanup]
    public static void ClassCleanup(TestContext context)
        => Console.WriteLine("TestClass2.ClassCleanup called.");

    [TestMethod]
    public void TestMethodFromTestClass2()
        => Console.WriteLine("TestMethodFromTestClass2 called");

    public void Dispose()
        => Console.WriteLine("TestClass2 disposed.");
}

#file my.runsettings
<RunSettings>
  <MSTest>
    <CaptureTraceOutput>false</CaptureTraceOutput>
    <ClassCleanupLifecycle>EndOfClass</ClassCleanupLifecycle>
  </MSTest>
</RunSettings>
""";
    }

    public TestContext TestContext { get; set; }
}
