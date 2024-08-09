// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Runtime.InteropServices;

using Microsoft.Testing.Platform.Acceptance.IntegrationTests;
using Microsoft.Testing.Platform.Acceptance.IntegrationTests.Helpers;

namespace MSTest.Acceptance.IntegrationTests;

[TestGroup]
public sealed class STATestMethodTests : AcceptanceTestBase
{
    private readonly TestAssetFixture _testAssetFixture;
    private const string AssetName = "STATestMethodProject";
    private const string TimeoutAssetName = "TimeoutSTATestMethodProject";
    private const string CooperativeTimeoutAssetName = "CooperativeTimeoutSTATestMethodProject";

    // There's a bug in TAFX where we need to use it at least one time somewhere to use it inside the fixture self (AcceptanceFixture).
    public STATestMethodTests(ITestExecutionContext testExecutionContext, TestAssetFixture testAssetFixture,
        AcceptanceFixture globalFixture)
        : base(testExecutionContext)
    {
        _testAssetFixture = testAssetFixture;
    }

    [ArgumentsProvider(nameof(TargetFrameworks.All), typeof(TargetFrameworks))]
    public async Task STATestMethod_OnWindows_OnLifeCycleTestClass_FixturesAndMethodsAreOnExpectedApartmentState(string currentTfm)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return;
        }

        var testHost = TestHost.LocateFrom(_testAssetFixture.TargetAssetPath, AssetName, currentTfm);
        string runSettingsFilePath = Path.Combine(testHost.DirectoryName, "mta.runsettings");
        TestHostResult testHostResult = await testHost.ExecuteAsync($"--settings {runSettingsFilePath} --filter className=LifeCycleTestClass");

        testHostResult.AssertExitCodeIs(0);
        testHostResult.AssertOutputContainsSummary(failed: 0, passed: 1, skipped: 0);
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
    public async Task DerivedSTATestMethod_OnWindows_OnTestClassWithClassCleanupEndOfAssembly(string currentTfm)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return;
        }

        var testHost = TestHost.LocateFrom(_testAssetFixture.TargetAssetPath, AssetName, currentTfm);
        string runSettingsFilePath = Path.Combine(testHost.DirectoryName, "mta.runsettings");
        TestHostResult testHostResult = await testHost.ExecuteAsync($"--settings {runSettingsFilePath} --filter className=TestClassWithClassCleanupEndOfAssembly");

        testHostResult.AssertExitCodeIs(0);
        testHostResult.AssertOutputContainsSummary(failed: 0, passed: 1, skipped: 0);
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

    [ArgumentsProvider(nameof(TargetFrameworks.All), typeof(TargetFrameworks))]
    public async Task STATestMethod_OnWindows_OnTestClassWithMultipleTests_MethodsAreOnExpectedApartmentState(string currentTfm)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return;
        }

        var testHost = TestHost.LocateFrom(_testAssetFixture.TargetAssetPath, AssetName, currentTfm);
        string runSettingsFilePath = Path.Combine(testHost.DirectoryName, "mta.runsettings");
        TestHostResult testHostResult = await testHost.ExecuteAsync($"--settings {runSettingsFilePath} --filter className=TestClassWithMultipleTests");

        testHostResult.AssertExitCodeIs(0);
        testHostResult.AssertOutputContainsSummary(failed: 0, passed: 2, skipped: 0);
        testHostResult.AssertOutputContains("LifeCycleTestClass.AssemblyInitialize");
        testHostResult.AssertOutputContains("TestClassWithMultipleTests.Constructor");
        testHostResult.AssertOutputContains("TestClassWithMultipleTests.TestInitialize");
        testHostResult.AssertOutputContains("TestClassWithMultipleTests.TestMethod1");
        testHostResult.AssertOutputContains("TestClassWithMultipleTests.TestCleanup");
        testHostResult.AssertOutputContains("TestClassWithMultipleTests.Dispose");
        testHostResult.AssertOutputContains("TestClassWithMultipleTests.Constructor");
        testHostResult.AssertOutputContains("TestClassWithMultipleTests.TestInitialize");
        testHostResult.AssertOutputContains("TestClassWithMultipleTests.TestMethod2");
        testHostResult.AssertOutputContains("TestClassWithMultipleTests.TestCleanup");
        testHostResult.AssertOutputContains("TestClassWithMultipleTests.Dispose");
        testHostResult.AssertOutputContains("LifeCycleTestClass.AssemblyCleanup");
    }

    [ArgumentsProvider(nameof(TargetFrameworks.All), typeof(TargetFrameworks))]
    public async Task STATestMethod_OnWindows_OnLifeCycleTestClass_WithTimeout_FixturesAndMethodsAreOnExpectedApartmentState(string currentTfm)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return;
        }

        var testHost = TestHost.LocateFrom(_testAssetFixture.TimeoutTargetAssetPath, TimeoutAssetName, currentTfm);
        string runSettingsFilePath = Path.Combine(testHost.DirectoryName, "mta.runsettings");
        TestHostResult testHostResult = await testHost.ExecuteAsync($"--settings {runSettingsFilePath} --filter className=LifeCycleTestClass");

        testHostResult.AssertExitCodeIs(0);
        testHostResult.AssertOutputContainsSummary(failed: 0, passed: 1, skipped: 0);
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
    public async Task DerivedSTATestMethod_OnWindows_WithTimeout_OnTestClassWithClassCleanupEndOfAssembly(string currentTfm)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return;
        }

        var testHost = TestHost.LocateFrom(_testAssetFixture.TimeoutTargetAssetPath, TimeoutAssetName, currentTfm);
        string runSettingsFilePath = Path.Combine(testHost.DirectoryName, "mta.runsettings");
        TestHostResult testHostResult = await testHost.ExecuteAsync($"--settings {runSettingsFilePath} --filter className=TestClassWithClassCleanupEndOfAssembly");

        testHostResult.AssertExitCodeIs(0);
        testHostResult.AssertOutputContainsSummary(failed: 0, passed: 1, skipped: 0);
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

    [ArgumentsProvider(nameof(TargetFrameworks.All), typeof(TargetFrameworks))]
    public async Task STATestMethod_OnWindows_OnTestClassWithMultipleTests_WithTimeout_MethodsAreOnExpectedApartmentState(string currentTfm)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return;
        }

        var testHost = TestHost.LocateFrom(_testAssetFixture.TimeoutTargetAssetPath, TimeoutAssetName, currentTfm);
        string runSettingsFilePath = Path.Combine(testHost.DirectoryName, "mta.runsettings");
        TestHostResult testHostResult = await testHost.ExecuteAsync($"--settings {runSettingsFilePath} --filter className=TestClassWithMultipleTests");

        testHostResult.AssertExitCodeIs(0);
        testHostResult.AssertOutputContainsSummary(failed: 0, passed: 2, skipped: 0);
        testHostResult.AssertOutputContains("LifeCycleTestClass.AssemblyInitialize");
        testHostResult.AssertOutputContains("TestClassWithMultipleTests.Constructor");
        testHostResult.AssertOutputContains("TestClassWithMultipleTests.TestInitialize");
        testHostResult.AssertOutputContains("TestClassWithMultipleTests.TestMethod1");
        testHostResult.AssertOutputContains("TestClassWithMultipleTests.TestCleanup");
        testHostResult.AssertOutputContains("TestClassWithMultipleTests.Dispose");
        testHostResult.AssertOutputContains("TestClassWithMultipleTests.Constructor");
        testHostResult.AssertOutputContains("TestClassWithMultipleTests.TestInitialize");
        testHostResult.AssertOutputContains("TestClassWithMultipleTests.TestMethod2");
        testHostResult.AssertOutputContains("TestClassWithMultipleTests.TestCleanup");
        testHostResult.AssertOutputContains("TestClassWithMultipleTests.Dispose");
        testHostResult.AssertOutputContains("LifeCycleTestClass.AssemblyCleanup");
    }

    [ArgumentsProvider(nameof(TargetFrameworks.All), typeof(TargetFrameworks))]
    public async Task STATestMethod_OnWindows_OnLifeCycleTestClass_WithCooperativeTimeout_FixturesAndMethodsAreOnExpectedApartmentState(string currentTfm)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return;
        }

        var testHost = TestHost.LocateFrom(_testAssetFixture.CooperativeTimeoutTargetAssetPath, CooperativeTimeoutAssetName, currentTfm);
        string runSettingsFilePath = Path.Combine(testHost.DirectoryName, "mta.runsettings");
        TestHostResult testHostResult = await testHost.ExecuteAsync($"--settings {runSettingsFilePath} --filter className=LifeCycleTestClass");

        testHostResult.AssertExitCodeIs(0);
        testHostResult.AssertOutputContainsSummary(failed: 0, passed: 1, skipped: 0);
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
    public async Task DerivedSTATestMethod_OnWindows_WithCooperativeTimeout_OnTestClassWithClassCleanupEndOfAssembly(string currentTfm)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return;
        }

        var testHost = TestHost.LocateFrom(_testAssetFixture.CooperativeTimeoutTargetAssetPath, CooperativeTimeoutAssetName, currentTfm);
        string runSettingsFilePath = Path.Combine(testHost.DirectoryName, "mta.runsettings");
        TestHostResult testHostResult = await testHost.ExecuteAsync($"--settings {runSettingsFilePath} --filter className=TestClassWithClassCleanupEndOfAssembly");

        testHostResult.AssertExitCodeIs(0);
        testHostResult.AssertOutputContainsSummary(failed: 0, passed: 1, skipped: 0);
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

    [ArgumentsProvider(nameof(TargetFrameworks.All), typeof(TargetFrameworks))]
    public async Task STATestMethod_OnWindows_OnTestClassWithMultipleTests_WithCooperativeTimeout_MethodsAreOnExpectedApartmentState(string currentTfm)
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return;
        }

        var testHost = TestHost.LocateFrom(_testAssetFixture.CooperativeTimeoutTargetAssetPath, CooperativeTimeoutAssetName, currentTfm);
        string runSettingsFilePath = Path.Combine(testHost.DirectoryName, "mta.runsettings");
        TestHostResult testHostResult = await testHost.ExecuteAsync($"--settings {runSettingsFilePath} --filter className=TestClassWithMultipleTests");

        testHostResult.AssertExitCodeIs(0);
        testHostResult.AssertOutputContainsSummary(failed: 0, passed: 2, skipped: 0);
        testHostResult.AssertOutputContains("LifeCycleTestClass.AssemblyInitialize");
        testHostResult.AssertOutputContains("TestClassWithMultipleTests.Constructor");
        testHostResult.AssertOutputContains("TestClassWithMultipleTests.TestInitialize");
        testHostResult.AssertOutputContains("TestClassWithMultipleTests.TestMethod1");
        testHostResult.AssertOutputContains("TestClassWithMultipleTests.TestCleanup");
        testHostResult.AssertOutputContains("TestClassWithMultipleTests.Dispose");
        testHostResult.AssertOutputContains("TestClassWithMultipleTests.Constructor");
        testHostResult.AssertOutputContains("TestClassWithMultipleTests.TestInitialize");
        testHostResult.AssertOutputContains("TestClassWithMultipleTests.TestMethod2");
        testHostResult.AssertOutputContains("TestClassWithMultipleTests.TestCleanup");
        testHostResult.AssertOutputContains("TestClassWithMultipleTests.Dispose");
        testHostResult.AssertOutputContains("LifeCycleTestClass.AssemblyCleanup");
    }

    [TestFixture(TestFixtureSharingStrategy.PerTestGroup)]
    public sealed class TestAssetFixture(AcceptanceFixture acceptanceFixture) : TestAssetFixtureBase(acceptanceFixture.NuGetGlobalPackagesFolder)
    {
        public string TargetAssetPath => GetAssetPath(AssetName);

        public string TimeoutTargetAssetPath => GetAssetPath(TimeoutAssetName);

        public string CooperativeTimeoutTargetAssetPath => GetAssetPath(CooperativeTimeoutAssetName);

        public override IEnumerable<(string ID, string Name, string Code)> GetAssetsToGenerate()
        {
            yield return (AssetName, AssetName,
                SourceCode
                .PatchTargetFrameworks(TargetFrameworks.All)
                .PatchCodeWithReplace("$ProjectName$", AssetName)
                .PatchCodeWithReplace("$TimeoutAttribute$", string.Empty)
                .PatchCodeWithReplace("$MSTestVersion$", MSTestVersion));

            yield return (CooperativeTimeoutAssetName, CooperativeTimeoutAssetName,
                SourceCode
                .PatchTargetFrameworks(TargetFrameworks.All)
                .PatchCodeWithReplace("$ProjectName$", CooperativeTimeoutAssetName)
                .PatchCodeWithReplace("$TimeoutAttribute$", ", Timeout(5000, CooperativeCancellation = true)")
                .PatchCodeWithReplace("$MSTestVersion$", MSTestVersion));

            yield return (TimeoutAssetName, TimeoutAssetName,
                SourceCode
                .PatchTargetFrameworks(TargetFrameworks.All)
                .PatchCodeWithReplace("$ProjectName$", TimeoutAssetName)
                .PatchCodeWithReplace("$TimeoutAttribute$", ", Timeout(5000)")
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

[TestClass]
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
        ThreadAssert.AssertApartmentStateIsMTA();
    }

    [ClassCleanup(ClassCleanupBehavior.EndOfClass)$TimeoutAttribute$]
    public static void ClassCleanup()
    {
        Console.WriteLine("LifeCycleTestClass.ClassCleanup");
        ThreadAssert.AssertApartmentStateIsMTA();
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

    [STATestMethod$TimeoutAttribute$]
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

public class DerivedSTATestMethodAttribute : STATestMethodAttribute
{
}

[TestClass]
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
        ThreadAssert.AssertApartmentStateIsMTA();
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

    [DerivedSTATestMethod$TimeoutAttribute$]
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

[TestClass]
public class TestClassWithMultipleTests : IDisposable
{
    private ApartmentState _ctorApartmentState;

    public TestClassWithMultipleTests()
    {
        _ctorApartmentState = Thread.CurrentThread.GetApartmentState();
        Console.WriteLine("TestClassWithMultipleTests.Constructor");
        ThreadAssert.AssertApartmentStateIs(_ctorApartmentState);
    }

    [TestInitialize$TimeoutAttribute$]
    public void TestInitialize()
    {
        Console.WriteLine("TestClassWithMultipleTests.TestInitialize");
        ThreadAssert.AssertApartmentStateIs(_ctorApartmentState);
    }

    [STATestMethod$TimeoutAttribute$]
    public void TestMethod1()
    {
        Console.WriteLine("TestClassWithMultipleTests.TestMethod1");
        ThreadAssert.AssertApartmentStateIs(_ctorApartmentState);
        Assert.AreEqual(ApartmentState.STA, _ctorApartmentState);
    }

    [TestMethod$TimeoutAttribute$]
    public void TestMethod2()
    {
        Console.WriteLine("TestClassWithMultipleTests.TestMethod2");
        ThreadAssert.AssertApartmentStateIs(_ctorApartmentState);
        Assert.AreNotEqual(ApartmentState.STA, _ctorApartmentState);
    }

    [TestCleanup$TimeoutAttribute$]
    public void TestCleanup()
    {
        Console.WriteLine("TestClassWithMultipleTests.TestCleanup");
        ThreadAssert.AssertApartmentStateIs(_ctorApartmentState);
    }

    public void Dispose()
    {
        Console.WriteLine("TestClassWithMultipleTests.Dispose");
        ThreadAssert.AssertApartmentStateIs(_ctorApartmentState);
    }
}

public static class ThreadAssert
{
    public static void AssertApartmentStateIsMTA() => AssertApartmentStateIs(ApartmentState.MTA);

    public static void AssertApartmentStateIsSTA() => AssertApartmentStateIs(ApartmentState.STA);

    public static void AssertApartmentStateIs(ApartmentState apartmentState)
    {
        var currentApartmentState = Thread.CurrentThread.GetApartmentState();
        Assert.AreEqual(apartmentState, currentApartmentState);
    }
}
""";
    }
}
