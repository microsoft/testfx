// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Runtime.InteropServices;

using Microsoft.Testing.Platform.Acceptance.IntegrationTests;
using Microsoft.Testing.Platform.Acceptance.IntegrationTests.Helpers;

namespace MSTest.Acceptance.IntegrationTests;

[TestGroup]
public sealed class STATestClassTests : AcceptanceTestBase
{
    private readonly TestAssetFixture _testAssetFixture;
    private const string AssetName = "STATestClass";
    private const string TimeoutAssetName = "TimeoutSTATestClass";
    private const string CooperativeTimeoutAssetName = "CooperativeTimeoutSTATestClass";

    // There's a bug in TAFX where we need to use it at least one time somewhere to use it inside the fixture self (AcceptanceFixture).
    public STATestClassTests(ITestExecutionContext testExecutionContext, TestAssetFixture testAssetFixture,
        AcceptanceFixture globalFixture)
        : base(testExecutionContext)
    {
        _testAssetFixture = testAssetFixture;
    }

    [ArgumentsProvider(nameof(TargetFrameworks.All), typeof(TargetFrameworks))]
    public async Task STATestClass_OnWindows_OnLifeCycleTestClass_FixturesAndMethodsAreOnExpectedApartmentState(string currentTfm)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return;
        }

        var testHost = TestHost.LocateFrom(_testAssetFixture.TargetAssetPath, AssetName, currentTfm);
        string runSettingsFilePath = Path.Combine(testHost.DirectoryName, "mta.runsettings");
        TestHostResult testHostResult = await testHost.ExecuteAsync($"--settings {runSettingsFilePath} --filter className=LifeCycleTestClass");

        testHostResult.AssertExitCodeIs(0);
        testHostResult.AssertOutputContains("Passed! - Failed: 0, Passed: 1, Skipped: 0, Total: 1");
        testHostResult.AssertOutputContains("LifeCycleTestClass.AssemblyInitialize");
        testHostResult.AssertOutputContains("LifeCycleTestClass.ClassInitialize");
        testHostResult.AssertOutputContains("LifeCycleTestClass.Constructor");
        testHostResult.AssertOutputContains("LifeCycleTestClass.TestInitialize");
        testHostResult.AssertOutputContains("LifeCycleTestClass.TestMethod1");
        testHostResult.AssertOutputContains("LifeCycleTestClass.TestCleanup");
        testHostResult.AssertOutputContains("LifeCycleTestClass.Dispose");
        testHostResult.AssertOutputContains("LifeCycleTestClass.ClassCleanup");
        testHostResult.AssertOutputContains("LifeCycleTestClass.AssemblyCleanup");
    }

    [ArgumentsProvider(nameof(TargetFrameworks.All), typeof(TargetFrameworks))]
    public async Task STATestClass_OnWindows_OnLifeCycleTestClassWithLastTestSkipped_FixturesAndMethodsAreOnExpectedApartmentState(string currentTfm)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return;
        }

        var testHost = TestHost.LocateFrom(_testAssetFixture.TargetAssetPath, AssetName, currentTfm);
        string runSettingsFilePath = Path.Combine(testHost.DirectoryName, "mta.runsettings");
        TestHostResult testHostResult = await testHost.ExecuteAsync($"--settings {runSettingsFilePath} --filter className=LifeCycleTestClassWithLastTestSkipped");

        testHostResult.AssertExitCodeIs(0);
        testHostResult.AssertOutputContains("Passed! - Failed: 0, Passed: 1, Skipped: 1, Total: 2");
        testHostResult.AssertOutputContains("LifeCycleTestClass.AssemblyInitialize");
        testHostResult.AssertOutputContains("LifeCycleTestClassWithLastTestSkipped.ClassInitialize");
        testHostResult.AssertOutputContains("LifeCycleTestClassWithLastTestSkipped.Constructor");
        testHostResult.AssertOutputContains("LifeCycleTestClassWithLastTestSkipped.TestInitialize");
        testHostResult.AssertOutputContains("LifeCycleTestClassWithLastTestSkipped.TestMethod1");
        testHostResult.AssertOutputContains("LifeCycleTestClassWithLastTestSkipped.TestCleanup");
        testHostResult.AssertOutputContains("LifeCycleTestClassWithLastTestSkipped.Dispose");
        testHostResult.AssertOutputContains("LifeCycleTestClassWithLastTestSkipped.ClassCleanup");
        testHostResult.AssertOutputContains("LifeCycleTestClass.AssemblyCleanup");
    }

    [ArgumentsProvider(nameof(TargetFrameworks.All), typeof(TargetFrameworks))]
    public async Task STATestClass_OnWindows_OnLifeCycleTestClass_WithTimeout_FixturesAndMethodsAreOnExpectedApartmentState(string currentTfm)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return;
        }

        var testHost = TestHost.LocateFrom(_testAssetFixture.TargetTimeoutAssetPath, TimeoutAssetName, currentTfm);
        string runSettingsFilePath = Path.Combine(testHost.DirectoryName, "mta.runsettings");
        TestHostResult testHostResult = await testHost.ExecuteAsync($"--settings {runSettingsFilePath} --filter className=LifeCycleTestClass");

        testHostResult.AssertExitCodeIs(0);
        testHostResult.AssertOutputContains("Passed! - Failed: 0, Passed: 1, Skipped: 0, Total: 1");
        testHostResult.AssertOutputContains("LifeCycleTestClass.AssemblyInitialize");
        testHostResult.AssertOutputContains("LifeCycleTestClass.ClassInitialize");
        testHostResult.AssertOutputContains("LifeCycleTestClass.Constructor");
        testHostResult.AssertOutputContains("LifeCycleTestClass.TestInitialize");
        testHostResult.AssertOutputContains("LifeCycleTestClass.TestMethod1");
        testHostResult.AssertOutputContains("LifeCycleTestClass.TestCleanup");
        testHostResult.AssertOutputContains("LifeCycleTestClass.Dispose");
        testHostResult.AssertOutputContains("LifeCycleTestClass.ClassCleanup");
        testHostResult.AssertOutputContains("LifeCycleTestClass.AssemblyCleanup");
    }

    [ArgumentsProvider(nameof(TargetFrameworks.All), typeof(TargetFrameworks))]
    public async Task STATestClass_OnWindows_OnLifeCycleTestClassWithLastTestSkipped_WithTimeout_FixturesAndMethodsAreOnExpectedApartmentState(string currentTfm)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return;
        }

        var testHost = TestHost.LocateFrom(_testAssetFixture.TargetTimeoutAssetPath, TimeoutAssetName, currentTfm);
        string runSettingsFilePath = Path.Combine(testHost.DirectoryName, "mta.runsettings");
        TestHostResult testHostResult = await testHost.ExecuteAsync($"--settings {runSettingsFilePath} --filter className=LifeCycleTestClassWithLastTestSkipped");

        testHostResult.AssertExitCodeIs(0);
        testHostResult.AssertOutputContains("Passed! - Failed: 0, Passed: 1, Skipped: 1, Total: 2");
        testHostResult.AssertOutputContains("LifeCycleTestClass.AssemblyInitialize");
        testHostResult.AssertOutputContains("LifeCycleTestClassWithLastTestSkipped.ClassInitialize");
        testHostResult.AssertOutputContains("LifeCycleTestClassWithLastTestSkipped.Constructor");
        testHostResult.AssertOutputContains("LifeCycleTestClassWithLastTestSkipped.TestInitialize");
        testHostResult.AssertOutputContains("LifeCycleTestClassWithLastTestSkipped.TestMethod1");
        testHostResult.AssertOutputContains("LifeCycleTestClassWithLastTestSkipped.TestCleanup");
        testHostResult.AssertOutputContains("LifeCycleTestClassWithLastTestSkipped.Dispose");
        testHostResult.AssertOutputContains("LifeCycleTestClassWithLastTestSkipped.ClassCleanup");
        testHostResult.AssertOutputContains("LifeCycleTestClass.AssemblyCleanup");
    }

    [ArgumentsProvider(nameof(TargetFrameworks.All), typeof(TargetFrameworks))]
    public async Task STATestClass_OnWindows_OnLifeCycleTestClass_WithCooperativeTimeout_FixturesAndMethodsAreOnExpectedApartmentState(string currentTfm)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return;
        }

        var testHost = TestHost.LocateFrom(_testAssetFixture.TargetCooperativeTimeoutAssetPath, CooperativeTimeoutAssetName, currentTfm);
        string runSettingsFilePath = Path.Combine(testHost.DirectoryName, "mta.runsettings");
        TestHostResult testHostResult = await testHost.ExecuteAsync($"--settings {runSettingsFilePath} --filter className=LifeCycleTestClass");

        testHostResult.AssertExitCodeIs(0);
        testHostResult.AssertOutputContains("Passed! - Failed: 0, Passed: 1, Skipped: 0, Total: 1");
        testHostResult.AssertOutputContains("LifeCycleTestClass.AssemblyInitialize");
        testHostResult.AssertOutputContains("LifeCycleTestClass.ClassInitialize");
        testHostResult.AssertOutputContains("LifeCycleTestClass.Constructor");
        testHostResult.AssertOutputContains("LifeCycleTestClass.TestInitialize");
        testHostResult.AssertOutputContains("LifeCycleTestClass.TestMethod1");
        testHostResult.AssertOutputContains("LifeCycleTestClass.TestCleanup");
        testHostResult.AssertOutputContains("LifeCycleTestClass.Dispose");
        testHostResult.AssertOutputContains("LifeCycleTestClass.ClassCleanup");
        testHostResult.AssertOutputContains("LifeCycleTestClass.AssemblyCleanup");
    }

    [ArgumentsProvider(nameof(TargetFrameworks.All), typeof(TargetFrameworks))]
    public async Task STATestClass_OnWindows_OnLifeCycleTestClassWithLastTestSkipped_WithCooperativeTimeout_FixturesAndMethodsAreOnExpectedApartmentState(string currentTfm)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return;
        }

        var testHost = TestHost.LocateFrom(_testAssetFixture.TargetCooperativeTimeoutAssetPath, CooperativeTimeoutAssetName, currentTfm);
        string runSettingsFilePath = Path.Combine(testHost.DirectoryName, "mta.runsettings");
        TestHostResult testHostResult = await testHost.ExecuteAsync($"--settings {runSettingsFilePath} --filter className=LifeCycleTestClassWithLastTestSkipped");

        testHostResult.AssertExitCodeIs(0);
        testHostResult.AssertOutputContains("Passed! - Failed: 0, Passed: 1, Skipped: 1, Total: 2");
        testHostResult.AssertOutputContains("LifeCycleTestClass.AssemblyInitialize");
        testHostResult.AssertOutputContains("LifeCycleTestClassWithLastTestSkipped.ClassInitialize");
        testHostResult.AssertOutputContains("LifeCycleTestClassWithLastTestSkipped.Constructor");
        testHostResult.AssertOutputContains("LifeCycleTestClassWithLastTestSkipped.TestInitialize");
        testHostResult.AssertOutputContains("LifeCycleTestClassWithLastTestSkipped.TestMethod1");
        testHostResult.AssertOutputContains("LifeCycleTestClassWithLastTestSkipped.TestCleanup");
        testHostResult.AssertOutputContains("LifeCycleTestClassWithLastTestSkipped.Dispose");
        testHostResult.AssertOutputContains("LifeCycleTestClassWithLastTestSkipped.ClassCleanup");
        testHostResult.AssertOutputContains("LifeCycleTestClass.AssemblyCleanup");
    }

    [ArgumentsProvider(nameof(TargetFrameworks.All), typeof(TargetFrameworks))]
    public async Task DerivedSTATestClass_OnWindows_OnTestClassWithClassCleanupEndOfAssembly_ClassCleanupIsMTA(string currentTfm)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return;
        }

        var testHost = TestHost.LocateFrom(_testAssetFixture.TargetAssetPath, AssetName, currentTfm);
        string runSettingsFilePath = Path.Combine(testHost.DirectoryName, "mta.runsettings");
        TestHostResult testHostResult = await testHost.ExecuteAsync($"--settings {runSettingsFilePath} --filter className=TestClassWithClassCleanupEndOfAssembly");

        testHostResult.AssertExitCodeIs(0);
        testHostResult.AssertOutputContains("Passed! - Failed: 0, Passed: 1, Skipped: 0, Total: 1");
        testHostResult.AssertOutputContains("LifeCycleTestClass.AssemblyInitialize");
        testHostResult.AssertOutputContains("TestClassWithClassCleanupEndOfAssembly.ClassInitialize");
        testHostResult.AssertOutputContains("TestClassWithClassCleanupEndOfAssembly.Constructor");
        testHostResult.AssertOutputContains("TestClassWithClassCleanupEndOfAssembly.TestInitialize");
        testHostResult.AssertOutputContains("TestClassWithClassCleanupEndOfAssembly.TestMethod1");
        testHostResult.AssertOutputContains("TestClassWithClassCleanupEndOfAssembly.TestCleanup");
        testHostResult.AssertOutputContains("TestClassWithClassCleanupEndOfAssembly.Dispose");
        testHostResult.AssertOutputContains("TestClassWithClassCleanupEndOfAssembly.ClassCleanup");
        testHostResult.AssertOutputContains("LifeCycleTestClass.AssemblyCleanup");
    }

    [TestFixture(TestFixtureSharingStrategy.PerTestGroup)]
    public sealed class TestAssetFixture(AcceptanceFixture acceptanceFixture) : TestAssetFixtureBase(acceptanceFixture.NuGetGlobalPackagesFolder)
    {
        public string TargetAssetPath => GetAssetPath(AssetName);

        public string TargetTimeoutAssetPath => GetAssetPath(TimeoutAssetName);

        public string TargetCooperativeTimeoutAssetPath => GetAssetPath(CooperativeTimeoutAssetName);

        public override IEnumerable<(string ID, string Name, string Code)> GetAssetsToGenerate()
        {
            yield return (AssetName, AssetName,
                SourceCode
                .PatchTargetFrameworks(TargetFrameworks.All)
                .PatchCodeWithReplace("$ProjectName$", AssetName)
                .PatchCodeWithReplace("$TimeoutAttribute$", string.Empty)
                .PatchCodeWithReplace("$MSTestVersion$", MSTestVersion));

            yield return (TimeoutAssetName, TimeoutAssetName,
                SourceCode
                .PatchTargetFrameworks(TargetFrameworks.All)
                .PatchCodeWithReplace("$ProjectName$", TimeoutAssetName)
                .PatchCodeWithReplace("$TimeoutAttribute$", ", Timeout(5000)")
                .PatchCodeWithReplace("$MSTestVersion$", MSTestVersion));

            yield return (CooperativeTimeoutAssetName, CooperativeTimeoutAssetName,
                SourceCode
                .PatchTargetFrameworks(TargetFrameworks.All)
                .PatchCodeWithReplace("$ProjectName$", CooperativeTimeoutAssetName)
                .PatchCodeWithReplace("$TimeoutAttribute$", ", Timeout(5000, CooperativeCancellation = true)")
                .PatchCodeWithReplace("$MSTestVersion$", MSTestVersion));
        }

        private const string SourceCode = """
#file mta.runsettings
<?xml version="1.0" encoding="utf-8" ?>
<RunSettings>
    <RunConfiguration>
        <ExecutionThreadApartmentState>MTA</ExecutionThreadApartmentState>
    </RunConfiguration>
    <MSTest>
        <CaptureTraceOutput>false</CaptureTraceOutput>
    </MSTest>
</RunSettings>

#file $ProjectName$.csproj
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
    <None Update="mta.runsettings">
      <CopyToOutputDirectory>PreserveNewest</CopyToOutputDirectory>
    </None>
  </ItemGroup>

</Project>

#file UnitTest1.cs

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[STATestClass]
public class LifeCycleTestClass : IDisposable
{
    [AssemblyInitialize$TimeoutAttribute$]
    public static void AssemblyInitialize(TestContext context)
    {
        Console.WriteLine("LifeCycleTestClass.AssemblyInitialize");
        ThreadAssert.AssertApartmentStateIsMTA();
    }

    [AssemblyCleanup$TimeoutAttribute$]
    public static void AssemblyCleanup()
    {
        Console.WriteLine("LifeCycleTestClass.AssemblyCleanup");
        ThreadAssert.AssertApartmentStateIsMTA();
    }

    public LifeCycleTestClass()
    {
        Console.WriteLine("LifeCycleTestClass.Constructor");
        ThreadAssert.AssertApartmentStateIsSTA();
    }

    [ClassInitialize$TimeoutAttribute$]
    public static void ClassInitialize(TestContext context)
    {
        Console.WriteLine("LifeCycleTestClass.ClassInitialize");
        ThreadAssert.AssertApartmentStateIsSTA();
    }

    [ClassCleanup(ClassCleanupBehavior.EndOfClass)$TimeoutAttribute$]
    public static void ClassCleanup()
    {
        Console.WriteLine("LifeCycleTestClass.ClassCleanup");
        ThreadAssert.AssertApartmentStateIsSTA();
    }

    [TestInitialize$TimeoutAttribute$]
    public void TestInitialize()
    {
        Console.WriteLine("LifeCycleTestClass.TestInitialize");
        ThreadAssert.AssertApartmentStateIsSTA();
    }

    [TestCleanup$TimeoutAttribute$]
    public void TestCleanup()
    {
        Console.WriteLine("LifeCycleTestClass.TestCleanup");
        ThreadAssert.AssertApartmentStateIsSTA();
    }

    [TestMethod$TimeoutAttribute$]
    public void TestMethod1()
    {
        Console.WriteLine("LifeCycleTestClass.TestMethod1");
        ThreadAssert.AssertApartmentStateIsSTA();
    }

    public void Dispose()
    {
        Console.WriteLine("LifeCycleTestClass.Dispose");
        ThreadAssert.AssertApartmentStateIsSTA();
    }
}

[STATestClass]
public class LifeCycleTestClassWithLastTestSkipped : IDisposable
{
    public LifeCycleTestClassWithLastTestSkipped()
    {
        Console.WriteLine("LifeCycleTestClassWithLastTestSkipped.Constructor");
        ThreadAssert.AssertApartmentStateIsSTA();
    }

    [ClassInitialize$TimeoutAttribute$]
    public static void ClassInitialize(TestContext context)
    {
        Console.WriteLine("LifeCycleTestClassWithLastTestSkipped.ClassInitialize");
        ThreadAssert.AssertApartmentStateIsSTA();
    }

    [ClassCleanup(ClassCleanupBehavior.EndOfClass)$TimeoutAttribute$]
    public static void ClassCleanup()
    {
        Console.WriteLine("LifeCycleTestClassWithLastTestSkipped.ClassCleanup");
        ThreadAssert.AssertApartmentStateIsSTA();
    }

    [TestInitialize$TimeoutAttribute$]
    public void TestInitialize()
    {
        Console.WriteLine("LifeCycleTestClassWithLastTestSkipped.TestInitialize");
        ThreadAssert.AssertApartmentStateIsSTA();
    }

    [TestCleanup$TimeoutAttribute$]
    public void TestCleanup()
    {
        Console.WriteLine("LifeCycleTestClassWithLastTestSkipped.TestCleanup");
        ThreadAssert.AssertApartmentStateIsSTA();
    }

    [TestMethod$TimeoutAttribute$]
    public void TestMethod1()
    {
        Console.WriteLine("LifeCycleTestClassWithLastTestSkipped.TestMethod1");
        ThreadAssert.AssertApartmentStateIsSTA();
    }

    [TestMethod]
    [Ignore]
    public void TestMethod2()
    {
        Assert.Fail("TestMethod2 should not be executed");
    }

    public void Dispose()
    {
        Console.WriteLine("LifeCycleTestClassWithLastTestSkipped.Dispose");
        ThreadAssert.AssertApartmentStateIsSTA();
    }
}

public class DerivedSTATestClass : STATestClassAttribute
{
}

[DerivedSTATestClass]
public class TestClassWithClassCleanupEndOfAssembly : IDisposable
{
    public TestClassWithClassCleanupEndOfAssembly()
    {
        Console.WriteLine("TestClassWithClassCleanupEndOfAssembly.Constructor");
        ThreadAssert.AssertApartmentStateIsSTA();
    }

    [ClassInitialize$TimeoutAttribute$]
    public static void ClassInitialize(TestContext context)
    {
        Console.WriteLine("TestClassWithClassCleanupEndOfAssembly.ClassInitialize");
        ThreadAssert.AssertApartmentStateIsSTA();
    }

    [ClassCleanup(ClassCleanupBehavior.EndOfAssembly)$TimeoutAttribute$]
    public static void ClassCleanup()
    {
        Console.WriteLine("TestClassWithClassCleanupEndOfAssembly.ClassCleanup");
        ThreadAssert.AssertApartmentStateIsMTA();
    }

    [TestInitialize$TimeoutAttribute$]
    public void TestInitialize()
    {
        Console.WriteLine("TestClassWithClassCleanupEndOfAssembly.TestInitialize");
        ThreadAssert.AssertApartmentStateIsSTA();
    }

    [TestCleanup$TimeoutAttribute$]
    public void TestCleanup()
    {
        Console.WriteLine("TestClassWithClassCleanupEndOfAssembly.TestCleanup");
        ThreadAssert.AssertApartmentStateIsSTA();
    }

    [TestMethod$TimeoutAttribute$]
    public void TestMethod1()
    {
        Console.WriteLine("TestClassWithClassCleanupEndOfAssembly.TestMethod1");
        ThreadAssert.AssertApartmentStateIsSTA();
    }

    public void Dispose()
    {
        Console.WriteLine("TestClassWithClassCleanupEndOfAssembly.Dispose");
        ThreadAssert.AssertApartmentStateIsSTA();
    }
}

public static class ThreadAssert
{
    public static void AssertApartmentStateIsMTA()
    {
        var apartmentState = Thread.CurrentThread.GetApartmentState();
        Assert.AreEqual(ApartmentState.MTA, apartmentState);
    }

    public static void AssertApartmentStateIsSTA()
    {
        var apartmentState = Thread.CurrentThread.GetApartmentState();
        Assert.AreEqual(ApartmentState.STA, apartmentState);
    }
}
""";
    }
}
