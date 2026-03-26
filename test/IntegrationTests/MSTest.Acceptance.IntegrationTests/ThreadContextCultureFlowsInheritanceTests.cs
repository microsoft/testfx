// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Acceptance.IntegrationTests;
using Microsoft.Testing.Platform.Acceptance.IntegrationTests.Helpers;

namespace MSTest.Acceptance.IntegrationTests;

[TestClass]
public sealed class ThreadContextCultureFlowsInheritanceTests : AcceptanceTestBase<ThreadContextCultureFlowsInheritanceTests.TestAssetFixture>
{
    [TestMethod]
    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    public async Task ThreadingContext_Inheritance_CurrentCultureFlowsBetweenMethods(string tfm)
    {
        var testHost = TestHost.LocateFrom(AssetFixture.TargetAssetPath, TestAssetFixture.ProjectName, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync(cancellationToken: TestContext.CancellationToken);
        testHostResult.AssertExitCodeIs(0);
        testHostResult.AssertOutputContainsSummary(failed: 0, passed: 16, skipped: 0);
    }

    [TestMethod]
    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    public async Task ThreadingContext_Inheritance_WhenUsingSTAThread_CurrentCultureFlowsBetweenMethods(string tfm)
    {
        var testHost = TestHost.LocateFrom(AssetFixture.TargetAssetPath, TestAssetFixture.ProjectName, tfm);
        string runSettingsFilePath = Path.Combine(testHost.DirectoryName, "sta.runsettings");
        TestHostResult testHostResult = await testHost.ExecuteAsync($"--settings {runSettingsFilePath}", cancellationToken: TestContext.CancellationToken);
        testHostResult.AssertExitCodeIs(0);
        testHostResult.AssertOutputContainsSummary(failed: 0, passed: 16, skipped: 0);
    }

    [TestMethod]
    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    public async Task ThreadingContext_Inheritance_WhenUsingSTAThreadAndTimeout_CurrentCultureFlowsBetweenMethods(string tfm)
    {
        var testHost = TestHost.LocateFrom(AssetFixture.TargetAssetPath, TestAssetFixture.ProjectName, tfm);
        string runSettingsFilePath = Path.Combine(testHost.DirectoryName, "sta-timeout.runsettings");
        TestHostResult testHostResult = await testHost.ExecuteAsync($"--settings {runSettingsFilePath}", cancellationToken: TestContext.CancellationToken);
        testHostResult.AssertExitCodeIs(0);
        testHostResult.AssertOutputContainsSummary(failed: 0, passed: 16, skipped: 0);
    }

    [TestMethod]
    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    public async Task ThreadingContext_Inheritance_WhenUsingTimeout_CurrentCultureFlowsBetweenMethods(string tfm)
    {
        var testHost = TestHost.LocateFrom(AssetFixture.TargetAssetPath, TestAssetFixture.ProjectName, tfm);
        string runSettingsFilePath = Path.Combine(testHost.DirectoryName, "timeout.runsettings");
        TestHostResult testHostResult = await testHost.ExecuteAsync($"--settings {runSettingsFilePath}", cancellationToken: TestContext.CancellationToken);
        testHostResult.AssertExitCodeIs(0);
        testHostResult.AssertOutputContainsSummary(failed: 0, passed: 16, skipped: 0);
    }

    public sealed class TestAssetFixture() : TestAssetFixtureBase(AcceptanceFixture.NuGetGlobalPackagesFolder)
    {
        public const string ProjectName = "CultureFlowsInheritanceThreadContextProject";

        public string TargetAssetPath => GetAssetPath(ProjectName);

        public override (string ID, string Name, string Code) GetAssetsToGenerate() => (ProjectName, ProjectName,
                SourceCode
                .PatchTargetFrameworks(TargetFrameworks.All)
                .PatchCodeWithReplace("$MSTestVersion$", MSTestVersion));

        private const string SourceCode = """
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
    private protected static string _testName;

    public TestContext TestContext
    {
        get => field;
        set
        {
            field = value;
            _testName ??= value.TestName;
        }
    }

    [ClassInitialize(InheritanceBehavior.BeforeEachDerivedClass)]
    public static void BaseClassInitialize(TestContext testContext)
    {
        if (_testName is not null)
        {
            throw new InvalidOperationException($"Was expected to be running tests sequentially but '{_testName}' is still running.");
        }

        CultureInfo.CurrentCulture = new CultureInfo(ExpectedCultures.BaseClassInitCulture);
    }

    [ClassCleanup(InheritanceBehavior.BeforeEachDerivedClass)]
    public static void BaseClassCleanup()
    {
        switch (_testName)
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
                throw new NotSupportedException($"Unsupported method name '{_testName}'");
        }

        _testName = null;
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
    }

    public TestContext TestContext { get; set; }
}
