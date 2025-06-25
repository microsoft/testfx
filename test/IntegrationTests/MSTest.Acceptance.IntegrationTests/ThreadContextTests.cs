// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Acceptance.IntegrationTests;
using Microsoft.Testing.Platform.Acceptance.IntegrationTests.Helpers;

namespace MSTest.Acceptance.IntegrationTests;

[TestClass]
public sealed class ThreadContextTests : AcceptanceTestBase<ThreadContextTests.TestAssetFixture>
{
    [TestMethod]
    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    public async Task ThreadingContext_WhenCultureIsNotSet_TestMethodFails(string tfm)
    {
        var testHost = TestHost.LocateFrom(AssetFixture.InitToTestProjectPath, TestAssetFixture.InitToTestProjectName, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync();
        testHostResult.AssertOutputContainsSummary(failed: 2, passed: 0, skipped: 0);
    }

    [TestMethod]
    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    public async Task ThreadingContext_WhenChangedInAssemblyInitialize_IsPassedToTestMethod(string tfm)
        => await SetCultureInFixtureMethodAndRunTests(tfm, "MSTEST_TEST_SET_CULTURE_ASSEMBLY_INIT");

    [TestMethod]
    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    public async Task ThreadingContext_WhenChangedInClassInitialize_IsPassedToTestMethod(string tfm)
        => await SetCultureInFixtureMethodAndRunTests(tfm, "MSTEST_TEST_SET_CULTURE_CLASS_INIT");

    [TestMethod]
    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    public async Task ThreadingContext_WhenChangedInTestInitialize_IsPassedToTestMethod(string tfm)
        => await SetCultureInFixtureMethodAndRunTests(tfm, "MSTEST_TEST_SET_CULTURE_TEST_INIT");

    private static async Task SetCultureInFixtureMethodAndRunTests(string tfm, string envVarKey)
    {
        var testHost = TestHost.LocateFrom(AssetFixture.InitToTestProjectPath, TestAssetFixture.InitToTestProjectName, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync(environmentVariables: new() { [envVarKey] = "true" });
        testHostResult.AssertExitCodeIs(0);
        testHostResult.AssertOutputContainsSummary(failed: 0, passed: 2, skipped: 0);
    }

    [TestMethod]
    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    public async Task ThreadingContext_CurrentCultureFlowsBetweenMethods(string tfm)
    {
        var testHost = TestHost.LocateFrom(AssetFixture.CultureFlowsProjectPath, TestAssetFixture.CultureFlowsProjectName, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync();
        testHostResult.AssertExitCodeIs(0);
        testHostResult.AssertOutputContainsSummary(failed: 0, passed: 1, skipped: 0);
    }

    [TestMethod]
    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    public async Task ThreadingContext_WhenUsingSTAThread_CurrentCultureFlowsBetweenMethods(string tfm)
    {
        var testHost = TestHost.LocateFrom(AssetFixture.CultureFlowsProjectPath, TestAssetFixture.CultureFlowsProjectName, tfm);
        string runSettingsFilePath = Path.Combine(testHost.DirectoryName, "sta.runsettings");
        TestHostResult testHostResult = await testHost.ExecuteAsync($"--settings {runSettingsFilePath}");
        testHostResult.AssertExitCodeIs(0);
        testHostResult.AssertOutputContainsSummary(failed: 0, passed: 1, skipped: 0);
    }

    [TestMethod]
    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    public async Task ThreadingContext_WhenUsingSTAThreadAndTimeout_CurrentCultureFlowsBetweenMethods(string tfm)
    {
        var testHost = TestHost.LocateFrom(AssetFixture.CultureFlowsProjectPath, TestAssetFixture.CultureFlowsProjectName, tfm);
        string runSettingsFilePath = Path.Combine(testHost.DirectoryName, "sta-timeout.runsettings");
        TestHostResult testHostResult = await testHost.ExecuteAsync($"--settings {runSettingsFilePath}");
        testHostResult.AssertExitCodeIs(0);
        testHostResult.AssertOutputContainsSummary(failed: 0, passed: 1, skipped: 0);
    }

    [TestMethod]
    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    public async Task ThreadingContext_WhenUsingTimeout_CurrentCultureFlowsBetweenMethods(string tfm)
    {
        var testHost = TestHost.LocateFrom(AssetFixture.CultureFlowsProjectPath, TestAssetFixture.CultureFlowsProjectName, tfm);
        string runSettingsFilePath = Path.Combine(testHost.DirectoryName, "timeout.runsettings");
        TestHostResult testHostResult = await testHost.ExecuteAsync($"--settings {runSettingsFilePath}");
        testHostResult.AssertExitCodeIs(0);
        testHostResult.AssertOutputContainsSummary(failed: 0, passed: 1, skipped: 0);
    }

    [TestMethod]
    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    public async Task ThreadingContext_Inheritance_CurrentCultureFlowsBetweenMethods(string tfm)
    {
        var testHost = TestHost.LocateFrom(AssetFixture.CultureFlowsInheritanceProjectPath, TestAssetFixture.CultureFlowsInheritanceProjectName, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync();
        testHostResult.AssertExitCodeIs(0);
        testHostResult.AssertOutputContainsSummary(failed: 0, passed: 16, skipped: 0);
    }

    [TestMethod]
    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    public async Task ThreadingContext_Inheritance_WhenUsingSTAThread_CurrentCultureFlowsBetweenMethods(string tfm)
    {
        var testHost = TestHost.LocateFrom(AssetFixture.CultureFlowsInheritanceProjectPath, TestAssetFixture.CultureFlowsInheritanceProjectName, tfm);
        string runSettingsFilePath = Path.Combine(testHost.DirectoryName, "sta.runsettings");
        TestHostResult testHostResult = await testHost.ExecuteAsync($"--settings {runSettingsFilePath}");
        testHostResult.AssertExitCodeIs(0);
        testHostResult.AssertOutputContainsSummary(failed: 0, passed: 16, skipped: 0);
    }

    [TestMethod]
    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    public async Task ThreadingContext_Inheritance_WhenUsingSTAThreadAndTimeout_CurrentCultureFlowsBetweenMethods(string tfm)
    {
        var testHost = TestHost.LocateFrom(AssetFixture.CultureFlowsInheritanceProjectPath, TestAssetFixture.CultureFlowsInheritanceProjectName, tfm);
        string runSettingsFilePath = Path.Combine(testHost.DirectoryName, "sta-timeout.runsettings");
        TestHostResult testHostResult = await testHost.ExecuteAsync($"--settings {runSettingsFilePath}");
        testHostResult.AssertExitCodeIs(0);
        testHostResult.AssertOutputContainsSummary(failed: 0, passed: 16, skipped: 0);
    }

    [TestMethod]
    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    public async Task ThreadingContext_Inheritance_WhenUsingTimeout_CurrentCultureFlowsBetweenMethods(string tfm)
    {
        var testHost = TestHost.LocateFrom(AssetFixture.CultureFlowsInheritanceProjectPath, TestAssetFixture.CultureFlowsInheritanceProjectName, tfm);
        string runSettingsFilePath = Path.Combine(testHost.DirectoryName, "timeout.runsettings");
        TestHostResult testHostResult = await testHost.ExecuteAsync($"--settings {runSettingsFilePath}");
        testHostResult.AssertExitCodeIs(0);
        testHostResult.AssertOutputContainsSummary(failed: 0, passed: 16, skipped: 0);
    }

    public sealed class TestAssetFixture() : TestAssetFixtureBase(AcceptanceFixture.NuGetGlobalPackagesFolder)
    {
        public const string InitToTestProjectName = "InitToTestThreadContextProject";
        public const string CultureFlowsProjectName = "CultureFlowsThreadContextProject";
        public const string CultureFlowsInheritanceProjectName = "CultureFlowsInheritanceThreadContextProject";
        private const string InitToTestSourceCode = """
#file InitToTestThreadContextProject.csproj
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <EnableMSTestRunner>true</EnableMSTestRunner>
    <TargetFrameworks>$TargetFrameworks$</TargetFrameworks>
    <LangVersion>latest</LangVersion>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="MSTest.TestAdapter" Version="$MSTestVersion$" />
    <PackageReference Include="MSTest.TestFramework" Version="$MSTestVersion$" />
  </ItemGroup>

</Project>

#file UnitTest1.cs
namespace InitToTestThreadContextProject;

using System;
using System.Globalization;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
public class UnitTest1
{
    private const string CultureCodeName = "th-TH";

    // Test methods should execute on the class context, and should be isolated.
    // Changes in one shouldn't affect the other.
    // This also makes the behavior of parallelizing and non-parallelizing tests consistent.
    private const string CultureToBeSetInTestMethodAndNotObservedInAnother = "fr-FR";

    [AssemblyInitialize]
    public static void AssemblyInitialize(TestContext context)
    {
        if (Environment.GetEnvironmentVariable("MSTEST_TEST_SET_CULTURE_ASSEMBLY_INIT") == "true")
        {
            CultureInfo.CurrentCulture = new CultureInfo(CultureCodeName);
        }
    }

    [ClassInitialize]
    public static void ClassInitialize(TestContext context)
    {
        if (Environment.GetEnvironmentVariable("MSTEST_TEST_SET_CULTURE_CLASS_INIT") == "true")
        {
            CultureInfo.CurrentCulture = new CultureInfo(CultureCodeName);
        }
    }

    [TestInitialize]
    public void TestInitialize()
    {
        if (Environment.GetEnvironmentVariable("MSTEST_TEST_SET_CULTURE_TEST_INIT") == "true")
        {
            CultureInfo.CurrentCulture = new CultureInfo(CultureCodeName);
        }
    }

    [TestMethod]
    public void TestMethod1()
    {
        Assert.AreEqual(CultureCodeName, CultureInfo.CurrentCulture.Name);
        CultureInfo.CurrentCulture = new CultureInfo(CultureToBeSetInTestMethodAndNotObservedInAnother);
    }

    [TestMethod]
    public void TestMethod2()
    {
        Assert.AreEqual(CultureCodeName, CultureInfo.CurrentCulture.Name);
        CultureInfo.CurrentCulture = new CultureInfo(CultureToBeSetInTestMethodAndNotObservedInAnother);
    }
}
""";

        private const string CultureFlowsSourceCode = """
#file sta.runsettings
<?xml version="1.0" encoding="utf-8" ?>
<RunSettings>
    <RunConfiguration>
        <ExecutionThreadApartmentState>STA</ExecutionThreadApartmentState>
    </RunConfiguration>
</RunSettings>

#file sta-timeout.runsettings
<?xml version="1.0" encoding="utf-8" ?>
<RunSettings>
    <RunConfiguration>
        <ExecutionThreadApartmentState>STA</ExecutionThreadApartmentState>
    </RunConfiguration>
    <MSTest>
        <AssemblyInitializeTimeout>10001</AssemblyInitializeTimeout>
        <ClassInitializeTimeout>10002</ClassInitializeTimeout>
        <TestInitializeTimeout>10003</TestInitializeTimeout>
        <TestTimeout>30004</TestTimeout>
        <TestCleanupTimeout>10005</TestCleanupTimeout>
        <ClassCleanupTimeout>10006</ClassCleanupTimeout>
        <AssemblyCleanupTimeout>10007</AssemblyCleanupTimeout>
    </MSTest>
</RunSettings>

#file timeout.runsettings
<?xml version="1.0" encoding="utf-8" ?>
<RunSettings>
    <MSTest>
        <AssemblyInitializeTimeout>10001</AssemblyInitializeTimeout>
        <ClassInitializeTimeout>10002</ClassInitializeTimeout>
        <TestInitializeTimeout>10003</TestInitializeTimeout>
        <TestTimeout>30004</TestTimeout>
        <TestCleanupTimeout>10005</TestCleanupTimeout>
        <ClassCleanupTimeout>10006</ClassCleanupTimeout>
        <AssemblyCleanupTimeout>10007</AssemblyCleanupTimeout>
    </MSTest>
</RunSettings>

#file CultureFlowsThreadContextProject.csproj
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <EnableMSTestRunner>true</EnableMSTestRunner>
    <TargetFrameworks>$TargetFrameworks$</TargetFrameworks>
    <LangVersion>latest</LangVersion>
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

#file UnitTest1.cs
namespace CultureFlowsThreadContextProject;

using System;
using System.Globalization;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
public class UnitTest1
{
    private const string AssemblyInitCultureCodeName = "th-TH";
    private const string ClassInitCultureCodeName = "tr-TR";
    private const string TestInitCultureCodeName = "sv-SE";
    private const string TestMethodCultureCodeName = "ak-GH";
    private const string TestCleanupCultureCodeName = "pt-BR";
    private const string ClassCleanupCultureCodeName = "hu-HU";

    [AssemblyInitialize]
    public static void AssemblyInitialize(TestContext context)
    {
        CultureInfo.CurrentCulture = new CultureInfo(AssemblyInitCultureCodeName);
    }

    [ClassInitialize]
    public static void ClassInitialize(TestContext context)
    {
        Assert.AreEqual(AssemblyInitCultureCodeName, CultureInfo.CurrentCulture.Name,
            "ClassInitialize culture should have been the one set by AssemblyInitialize");
        CultureInfo.CurrentCulture = new CultureInfo(ClassInitCultureCodeName);
    }

    [TestInitialize]
    public void TestInitialize()
    {
        Assert.AreEqual(ClassInitCultureCodeName, CultureInfo.CurrentCulture.Name,
            "TestInitialize culture should have been the one set by ClassInitialize");
        CultureInfo.CurrentCulture = new CultureInfo(TestInitCultureCodeName);
    }

    [TestMethod]
    public void TestMethod1()
    {
        Assert.AreEqual(TestInitCultureCodeName, CultureInfo.CurrentCulture.Name,
            "TestMethod culture should have been the one set by TestInitialize");
        CultureInfo.CurrentCulture = new CultureInfo(TestMethodCultureCodeName);
    }

    [TestCleanup]
    public void TestCleanup()
    {
        Assert.AreEqual(TestMethodCultureCodeName, CultureInfo.CurrentCulture.Name,
            "TestCleanup culture should have been the one set by TestMethod");
        CultureInfo.CurrentCulture = new CultureInfo(TestCleanupCultureCodeName);
    }

    [ClassCleanup]
    public static void ClassCleanup()
    {
        Assert.AreEqual(ClassInitCultureCodeName, CultureInfo.CurrentCulture.Name,
            "ClassCleanup culture should have been the one set by ClassInitialize");
    }

    [AssemblyCleanup]
    public static void AssemblyCleanup()
    {
        Assert.AreEqual(AssemblyInitCultureCodeName, CultureInfo.CurrentCulture.Name,
            "AssemblyCleanup culture should have been the one set by AssemblyInitialize");
    }
}
""";

        private const string CultureFlowsInheritanceSourceCode = """
#file sta.runsettings
<?xml version="1.0" encoding="utf-8" ?>
<RunSettings>
    <RunConfiguration>
        <ExecutionThreadApartmentState>STA</ExecutionThreadApartmentState>
    </RunConfiguration>
</RunSettings>

#file sta-timeout.runsettings
<?xml version="1.0" encoding="utf-8" ?>
<RunSettings>
    <RunConfiguration>
        <ExecutionThreadApartmentState>STA</ExecutionThreadApartmentState>
    </RunConfiguration>
    <MSTest>
        <AssemblyInitializeTimeout>10001</AssemblyInitializeTimeout>
        <ClassInitializeTimeout>10002</ClassInitializeTimeout>
        <TestInitializeTimeout>10003</TestInitializeTimeout>
        <TestTimeout>30004</TestTimeout>
        <TestCleanupTimeout>10005</TestCleanupTimeout>
        <ClassCleanupTimeout>10006</ClassCleanupTimeout>
        <AssemblyCleanupTimeout>10007</AssemblyCleanupTimeout>
    </MSTest>
</RunSettings>

#file timeout.runsettings
<?xml version="1.0" encoding="utf-8" ?>
<RunSettings>
    <MSTest>
        <AssemblyInitializeTimeout>10001</AssemblyInitializeTimeout>
        <ClassInitializeTimeout>10002</ClassInitializeTimeout>
        <TestInitializeTimeout>10003</TestInitializeTimeout>
        <TestTimeout>30004</TestTimeout>
        <TestCleanupTimeout>10005</TestCleanupTimeout>
        <ClassCleanupTimeout>10006</ClassCleanupTimeout>
        <AssemblyCleanupTimeout>10007</AssemblyCleanupTimeout>
    </MSTest>
</RunSettings>

#file CultureFlowsInheritanceThreadContextProject.csproj
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

#file UnitTest1.cs
namespace CultureFlowsExtraCasesThreadContextProject;

using System;
using System.Globalization;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

public class ExpectedCultures
{
    public const string BaseClassInitCulture = "fr-FR";
    public const string BaseTestInitCulture = "hr-BA";
    public const string IntermediateClassInitCulture = "es-ES";
    public const string IntermediateTestInitCulture = "et-EE";
    public const string IntermediateTestCleanupCulture = "hu-HU";
    public const string IntermediateClassCleanupCulture = "vi-VN";
    public const string TestMethodCulture = "it-IT";
}

public class BaseClassWithInheritance
{
    private protected static string _managedMethod;

    public TestContext TestContext
    {
        get => field;
        set
        {
            field = value;
            _managedMethod ??= value.ManagedMethod;
        }
    }

    [ClassInitialize(InheritanceBehavior.BeforeEachDerivedClass)]
    public static void BaseClassInitialize(TestContext testContext)
    {
        if (_managedMethod is not null)
        {
            throw new InvalidOperationException($"Was expected to be running tests sequentially but '{_managedMethod}' is still running.");
        }

        CultureInfo.CurrentCulture = new CultureInfo(ExpectedCultures.BaseClassInitCulture);
    }

    [ClassCleanup(InheritanceBehavior.BeforeEachDerivedClass)]
    public static void BaseClassCleanup()
    {
        switch (_managedMethod)
        {
            case "DerivedClassIntermediateClassWithoutInheritanceBaseClassWithInheritanceTestMethod":
            case "DerivedClassIntermediateClassWithoutInheritanceBaseClassWithInheritanceTestMethod2":
                Assert.AreEqual(ExpectedCultures.BaseClassInitCulture, CultureInfo.CurrentCulture.Name);
                break;

            case "DerivedClassIntermediateClassWithInheritanceBaseClassWithInheritanceTestMethod":
            case "DerivedClassIntermediateClassWithInheritanceBaseClassWithInheritanceTestMethod2":
                Assert.AreEqual(ExpectedCultures.IntermediateClassCleanupCulture, CultureInfo.CurrentCulture.Name);
                break;

            default:
                throw new NotSupportedException($"Unsupported method name '{_managedMethod}'");
        }

        _managedMethod = null;
    }
}

public class BaseClassWithoutInheritance
{
    [ClassInitialize(InheritanceBehavior.None)]
    public static void BaseClassInitialize(TestContext testContext)
    {
        Assert.Fail("BaseClassWithoutInheritance.BaseClassInitialize should not have been called");
    }

    [ClassCleanup(InheritanceBehavior.None)]
    public static void BaseClassCleanup()
    {
        Assert.Fail("BaseClassWithoutInheritance.BaseClassCleanup should not have been called");
    }
}

public class IntermediateClassWithInheritanceBaseClassWithInheritance : BaseClassWithInheritance
{
    [ClassInitialize(InheritanceBehavior.BeforeEachDerivedClass)]
    public static void IntermediateClassInitialize(TestContext testContext)
    {
        Assert.AreEqual(ExpectedCultures.BaseClassInitCulture, CultureInfo.CurrentCulture.Name);
        CultureInfo.CurrentCulture = new CultureInfo(ExpectedCultures.IntermediateClassInitCulture);
    }

    [ClassCleanup(InheritanceBehavior.BeforeEachDerivedClass)]
    public static void IntermediateClassCleanup()
    {
        Assert.AreEqual(ExpectedCultures.IntermediateClassInitCulture, CultureInfo.CurrentCulture.Name);
        CultureInfo.CurrentCulture = new CultureInfo(ExpectedCultures.IntermediateClassCleanupCulture);
    }
}

public class IntermediateClassWithInheritanceBaseClassWithoutInheritance : BaseClassWithoutInheritance
{
    [ClassInitialize(InheritanceBehavior.BeforeEachDerivedClass)]
    public static void IntermediateClassInitialize(TestContext testContext)
    {
        Assert.AreNotEqual(ExpectedCultures.BaseClassInitCulture, CultureInfo.CurrentCulture.Name);
        CultureInfo.CurrentCulture = new CultureInfo(ExpectedCultures.IntermediateClassInitCulture);
    }

    [ClassCleanup(InheritanceBehavior.BeforeEachDerivedClass)]
    public static void IntermediateClassCleanup()
    {
        Assert.AreEqual(ExpectedCultures.IntermediateClassInitCulture, CultureInfo.CurrentCulture.Name);
        CultureInfo.CurrentCulture = new CultureInfo(ExpectedCultures.IntermediateClassCleanupCulture);
    }
}

public class IntermediateClassWithoutInheritanceBaseClassWithInheritance : BaseClassWithInheritance
{
    [ClassInitialize(InheritanceBehavior.None)]
    public static void IntermediateClassInitialize(TestContext testContext)
    {
        Assert.Fail("IntermediateClassWithoutInheritanceBaseClassWithInheritance.IntermediateClassInitialize should not have been called");
    }

    [ClassCleanup(InheritanceBehavior.None)]
    public static void IntermediateClassCleanup()
    {
        Assert.Fail("IntermediateClassWithoutInheritanceBaseClassWithInheritance.IntermediateClassCleanup should not have been called");
    }
}

public class IntermediateClassWithoutInheritanceBaseClassWithoutInheritance : BaseClassWithoutInheritance
{
    [ClassInitialize(InheritanceBehavior.None)]
    public static void IntermediateClassInitialize(TestContext testContext)
    {
        Assert.Fail("IntermediateClassWithoutInheritanceBaseClassWithoutInheritance.IntermediateClassInitialize should not have been called");
    }

    [ClassCleanup(InheritanceBehavior.None)]
    public static void IntermediateClassCleanup()
    {
        Assert.Fail("IntermediateClassWithoutInheritanceBaseClassWithoutInheritance.IntermediateClassCleanup should not have been called");
    }
}

[TestClass]
public class DerivedClassIntermediateClassWithInheritanceBaseClassWithInheritance : IntermediateClassWithInheritanceBaseClassWithInheritance
{
    [TestMethod]
    public void DerivedClassIntermediateClassWithInheritanceBaseClassWithInheritanceTestMethod()
    {
        Assert.AreEqual(ExpectedCultures.IntermediateClassInitCulture, CultureInfo.CurrentCulture.Name);
        CultureInfo.CurrentCulture = new CultureInfo(ExpectedCultures.TestMethodCulture);
    }

    [TestMethod]
    public void DerivedClassIntermediateClassWithInheritanceBaseClassWithInheritanceTestMethod2()
    {
        Assert.AreEqual(ExpectedCultures.IntermediateClassInitCulture, CultureInfo.CurrentCulture.Name);
        CultureInfo.CurrentCulture = new CultureInfo(ExpectedCultures.TestMethodCulture);
    }
}

[TestClass]
public class DerivedClassIntermediateClassWithInheritanceBaseClassWithoutInheritance : IntermediateClassWithInheritanceBaseClassWithoutInheritance
{
    [TestMethod]
    public void DerivedClassIntermediateClassWithInheritanceBaseClassWithoutInheritanceTestMethod()
    {
        Assert.AreEqual(ExpectedCultures.IntermediateClassInitCulture, CultureInfo.CurrentCulture.Name);
        CultureInfo.CurrentCulture = new CultureInfo(ExpectedCultures.TestMethodCulture);
    }

    [TestMethod]
    public void DerivedClassIntermediateClassWithInheritanceBaseClassWithoutInheritanceTestMethod2()
    {
        Assert.AreEqual(ExpectedCultures.IntermediateClassInitCulture, CultureInfo.CurrentCulture.Name);
        CultureInfo.CurrentCulture = new CultureInfo(ExpectedCultures.TestMethodCulture);
    }
}

[TestClass]
public class DerivedClassIntermediateClassWithoutInheritanceBaseClassWithInheritance : IntermediateClassWithoutInheritanceBaseClassWithInheritance
{
    [TestMethod]
    public void DerivedClassIntermediateClassWithoutInheritanceBaseClassWithInheritanceTestMethod()
    {
        Assert.AreEqual(ExpectedCultures.BaseClassInitCulture, CultureInfo.CurrentCulture.Name);
        CultureInfo.CurrentCulture = new CultureInfo(ExpectedCultures.TestMethodCulture);
    }

    [TestMethod]
    public void DerivedClassIntermediateClassWithoutInheritanceBaseClassWithInheritanceTestMethod2()
    {
        Assert.AreEqual(ExpectedCultures.BaseClassInitCulture, CultureInfo.CurrentCulture.Name);
        CultureInfo.CurrentCulture = new CultureInfo(ExpectedCultures.TestMethodCulture);
    }
}

[TestClass]
public class DerivedClassIntermediateClassWithoutInheritanceBaseClassWithoutInheritance : IntermediateClassWithoutInheritanceBaseClassWithoutInheritance
{
    [TestMethod]
    public void DerivedClassIntermediateClassWithoutInheritanceBaseClassWithoutInheritanceTestMethod()
    {
        Assert.AreNotEqual(ExpectedCultures.IntermediateClassInitCulture, CultureInfo.CurrentCulture.Name);
        Assert.AreNotEqual(ExpectedCultures.BaseClassInitCulture, CultureInfo.CurrentCulture.Name);
        CultureInfo.CurrentCulture = new CultureInfo(ExpectedCultures.TestMethodCulture);
    }

    [TestMethod]
    public void DerivedClassIntermediateClassWithoutInheritanceBaseClassWithoutInheritanceTestMethod2()
    {
        Assert.AreNotEqual(ExpectedCultures.IntermediateClassInitCulture, CultureInfo.CurrentCulture.Name);
        Assert.AreNotEqual(ExpectedCultures.BaseClassInitCulture, CultureInfo.CurrentCulture.Name);
        CultureInfo.CurrentCulture = new CultureInfo(ExpectedCultures.TestMethodCulture);
    }
}

public class BaseClassWithTestInitCleanup
{
    public TestContext TestContext { get; set; }

    [TestInitialize]
    public void BaseTestInitialize()
    {
        CultureInfo.CurrentCulture = new CultureInfo(ExpectedCultures.BaseTestInitCulture);
    }

    [TestCleanup]
    public void BaseTestCleanup()
    {
        switch (TestContext.TestName)
        {
            case "DerivedClassIntermediateClassWithTestInitCleanupBaseClassWithTestInitCleanupTestMethod":
            case "DerivedClassIntermediateClassWithTestInitCleanupBaseClassWithTestInitCleanupTestMethod2":
                Assert.AreEqual(ExpectedCultures.IntermediateTestCleanupCulture, CultureInfo.CurrentCulture.Name);
                break;

            case "DerivedClassIntermediateClassWithoutTestInitCleanupBaseClassWithTestInitCleanupTestMethod":
            case "DerivedClassIntermediateClassWithoutTestInitCleanupBaseClassWithTestInitCleanupTestMethod2":
                Assert.AreEqual(ExpectedCultures.TestMethodCulture, CultureInfo.CurrentCulture.Name);
                break;

            default:
                throw new NotSupportedException($"Unsupported method name '{TestContext.TestName}'");
        }
    }
}

public class BaseClassWithoutTestInitCleanup
{
}

public class IntermediateClassWithTestInitCleanupBaseClassWithTestInitCleanup : BaseClassWithTestInitCleanup
{
    [TestInitialize]
    public void IntermediateTestInitialize()
    {
        Assert.AreEqual(ExpectedCultures.BaseTestInitCulture, CultureInfo.CurrentCulture.Name);
        CultureInfo.CurrentCulture = new CultureInfo(ExpectedCultures.IntermediateTestInitCulture);
    }

    [TestCleanup]
    public void IntermediateTestCleanup()
    {
        Assert.AreEqual(ExpectedCultures.TestMethodCulture, CultureInfo.CurrentCulture.Name);
        CultureInfo.CurrentCulture = new CultureInfo(ExpectedCultures.IntermediateTestCleanupCulture);
    }
}

public class IntermediateClassWithoutTestInitCleanupBaseClassWithTestInitCleanup : BaseClassWithTestInitCleanup
{
}

public class IntermediateClassWithTestInitCleanupBaseClassWithoutTestInitCleanup : BaseClassWithoutTestInitCleanup
{
    [TestInitialize]
    public void IntermediateTestInitialize()
    {
        Assert.AreNotEqual(ExpectedCultures.BaseTestInitCulture, CultureInfo.CurrentCulture.Name);
        CultureInfo.CurrentCulture = new CultureInfo(ExpectedCultures.IntermediateTestInitCulture);
    }

    [TestCleanup]
    public void IntermediateTestCleanup()
    {
        Assert.AreEqual(ExpectedCultures.TestMethodCulture, CultureInfo.CurrentCulture.Name);
        CultureInfo.CurrentCulture = new CultureInfo(ExpectedCultures.IntermediateTestCleanupCulture);
    }
}

public class IntermediateClassWithoutTestInitCleanupBaseClassWithoutTestInitCleanup : BaseClassWithoutTestInitCleanup
{
}

[TestClass]
public class DerivedClassIntermediateClassWithTestInitCleanupBaseClassWithTestInitCleanup : IntermediateClassWithTestInitCleanupBaseClassWithTestInitCleanup
{
    [TestMethod]
    public void DerivedClassIntermediateClassWithTestInitCleanupBaseClassWithTestInitCleanupTestMethod()
    {
        Assert.AreEqual(ExpectedCultures.IntermediateTestInitCulture, CultureInfo.CurrentCulture.Name);
        CultureInfo.CurrentCulture = new CultureInfo(ExpectedCultures.TestMethodCulture);
    }

    [TestMethod]
    public void DerivedClassIntermediateClassWithTestInitCleanupBaseClassWithTestInitCleanupTestMethod2()
    {
        Assert.AreEqual(ExpectedCultures.IntermediateTestInitCulture, CultureInfo.CurrentCulture.Name);
        CultureInfo.CurrentCulture = new CultureInfo(ExpectedCultures.TestMethodCulture);
    }
}


[TestClass]
public class DerivedClassIntermediateClassWithTestInitCleanupBaseClassWithoutTestInitCleanup : IntermediateClassWithTestInitCleanupBaseClassWithoutTestInitCleanup
{
    [TestMethod]
    public void DerivedClassIntermediateClassWithTestInitCleanupBaseClassWithoutTestInitCleanupTestMethod()
    {
        Assert.AreEqual(ExpectedCultures.IntermediateTestInitCulture, CultureInfo.CurrentCulture.Name);
        CultureInfo.CurrentCulture = new CultureInfo(ExpectedCultures.TestMethodCulture);
    }

    [TestMethod]
    public void DerivedClassIntermediateClassWithTestInitCleanupBaseClassWithoutTestInitCleanupTestMethod2()
    {
        Assert.AreEqual(ExpectedCultures.IntermediateTestInitCulture, CultureInfo.CurrentCulture.Name);
        CultureInfo.CurrentCulture = new CultureInfo(ExpectedCultures.TestMethodCulture);
    }
}

[TestClass]
public class DerivedClassIntermediateClassWithoutTestInitCleanupBaseClassWithTestInitCleanup : IntermediateClassWithoutTestInitCleanupBaseClassWithTestInitCleanup
{
    [TestMethod]
    public void DerivedClassIntermediateClassWithoutTestInitCleanupBaseClassWithTestInitCleanupTestMethod()
    {
        Assert.AreEqual(ExpectedCultures.BaseTestInitCulture, CultureInfo.CurrentCulture.Name);
        CultureInfo.CurrentCulture = new CultureInfo(ExpectedCultures.TestMethodCulture);
    }

    [TestMethod]
    public void DerivedClassIntermediateClassWithoutTestInitCleanupBaseClassWithTestInitCleanupTestMethod2()
    {
        Assert.AreEqual(ExpectedCultures.BaseTestInitCulture, CultureInfo.CurrentCulture.Name);
        CultureInfo.CurrentCulture = new CultureInfo(ExpectedCultures.TestMethodCulture);
    }
}

[TestClass]
public class DerivedClassIntermediateClassWithoutTestInitCleanupBaseClassWithoutTestInitCleanup : IntermediateClassWithoutTestInitCleanupBaseClassWithoutTestInitCleanup
{
    [TestMethod]
    public void DerivedClassIntermediateClassWithoutTestInitCleanupBaseClassWithoutTestInitCleanupTestMethod()
    {
        Assert.AreNotEqual(ExpectedCultures.IntermediateTestInitCulture, CultureInfo.CurrentCulture.Name);
        Assert.AreNotEqual(ExpectedCultures.BaseTestInitCulture, CultureInfo.CurrentCulture.Name);
        CultureInfo.CurrentCulture = new CultureInfo(ExpectedCultures.TestMethodCulture);
    }

    [TestMethod]
    public void DerivedClassIntermediateClassWithoutTestInitCleanupBaseClassWithoutTestInitCleanupTestMethod2()
    {
        Assert.AreNotEqual(ExpectedCultures.IntermediateTestInitCulture, CultureInfo.CurrentCulture.Name);
        Assert.AreNotEqual(ExpectedCultures.BaseTestInitCulture, CultureInfo.CurrentCulture.Name);
        CultureInfo.CurrentCulture = new CultureInfo(ExpectedCultures.TestMethodCulture);
    }
}
""";

        public string InitToTestProjectPath => GetAssetPath(InitToTestProjectName);

        public string CultureFlowsProjectPath => GetAssetPath(CultureFlowsProjectName);

        public string CultureFlowsInheritanceProjectPath => GetAssetPath(CultureFlowsInheritanceProjectName);

        public override IEnumerable<(string ID, string Name, string Code)> GetAssetsToGenerate()
        {
            yield return (InitToTestProjectName, InitToTestProjectName,
                InitToTestSourceCode
                .PatchTargetFrameworks(TargetFrameworks.All)
                .PatchCodeWithReplace("$MSTestVersion$", MSTestVersion));

            yield return (CultureFlowsProjectName, CultureFlowsProjectName,
                CultureFlowsSourceCode
                .PatchTargetFrameworks(TargetFrameworks.All)
                .PatchCodeWithReplace("$MSTestVersion$", MSTestVersion));

            yield return (CultureFlowsInheritanceProjectName, CultureFlowsInheritanceProjectName,
                CultureFlowsInheritanceSourceCode
                .PatchTargetFrameworks(TargetFrameworks.All)
                .PatchCodeWithReplace("$MSTestVersion$", MSTestVersion));
        }
    }
}
