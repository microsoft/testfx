// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Acceptance.IntegrationTests;
using Microsoft.Testing.Platform.Acceptance.IntegrationTests.Helpers;
using Microsoft.Testing.Platform.Helpers;

namespace MSTest.Acceptance.IntegrationTests;

/// <summary>
/// Acceptance tests for the flow of <see cref="TestContext.Properties"/> values across the
/// AssemblyInitialize / ClassInitialize / TestMethod / ClassCleanup / AssemblyCleanup lifecycle.
/// See https://github.com/microsoft/testfx/issues/5986.
/// </summary>
[TestClass]
public sealed class TestContextPropertyFlowTests : AcceptanceTestBase<TestContextPropertyFlowTests.TestAssetFixture>
{
    [TestMethod]
    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    public async Task PropertiesSetInAssemblyInitAndClassInitAreVisibleEverywhere(string tfm)
    {
        var testHost = TestHost.LocateFrom(AssetFixture.ProjectPath, TestAssetFixture.ProjectName, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync(cancellationToken: TestContext.CancellationToken);

        testHostResult.AssertExitCodeIs(0);
        // PropertyFlowTests: TestMethodOne + TestMethodTwo = 2
        // SecondClassTests: TestMethod = 1
        // DataRowFlowTests: two data rows = 2
        // Total = 5
        testHostResult.AssertOutputContainsSummary(failed: 0, passed: 5, skipped: 0);
    }

    public sealed class TestAssetFixture() : TestAssetFixtureBase()
    {
        public const string ProjectName = "TestContextPropertyFlow";

        public string ProjectPath => GetAssetPath(ProjectName);

        public override (string ID, string Name, string Code) GetAssetsToGenerate() => (ProjectName, ProjectName,
                SourceCode
                .PatchTargetFrameworks(TargetFrameworks.All)
                .PatchCodeWithReplace("$MSTestVersion$", MSTestVersion));

        private const string SourceCode = """
#file TestContextPropertyFlow.csproj
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <EnableMSTestRunner>true</EnableMSTestRunner>
    <TargetFrameworks>$TargetFrameworks$</TargetFrameworks>
    <LangVersion>preview</LangVersion>
    <EnableMicrosoftTestingPlatform>true</EnableMicrosoftTestingPlatform>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="MSTest" Version="$MSTestVersion$" />
  </ItemGroup>

</Project>

#file UnitTest1.cs
using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
public sealed class PropertyFlowTests
{
    public TestContext TestContext { get; set; } = null!;

    [AssemblyInitialize]
    public static void AssemblyInit(TestContext context)
    {
        context.Properties["AssemblyInitKey"] = "AssemblyInitValue";
        context.Properties["SharedKey"] = "FromAssemblyInit";
    }

    [ClassInitialize]
    public static void ClassInit(TestContext context)
    {
        // AssemblyInit-set properties must be visible inside ClassInitialize.
        Assert.AreEqual("AssemblyInitValue", context.Properties["AssemblyInitKey"]);
        Assert.AreEqual("FromAssemblyInit", context.Properties["SharedKey"]);

        context.Properties["ClassInitKey"] = "ClassInitValue";
        // ClassInit overrides a value previously set by AssemblyInit; the override should win
        // in subsequent contexts (tests, cleanups) of this class.
        context.Properties["SharedKey"] = "FromClassInit";
    }

    [TestMethod]
    public void TestMethodOne()
    {
        Assert.AreEqual("AssemblyInitValue", TestContext.Properties["AssemblyInitKey"]);
        Assert.AreEqual("ClassInitValue", TestContext.Properties["ClassInitKey"]);
        // ClassInit's override of SharedKey must win.
        Assert.AreEqual("FromClassInit", TestContext.Properties["SharedKey"]);

        // Properties added by a sibling test method must NOT leak across tests.
        Assert.IsFalse(
            TestContext.Properties.ContainsKey("FromTestTwo"),
            "Properties set by TestMethodTwo should not leak into TestMethodOne.");

        TestContext.Properties["FromTestOne"] = "ShouldNotLeak";
    }

    [TestMethod]
    public void TestMethodTwo()
    {
        Assert.AreEqual("AssemblyInitValue", TestContext.Properties["AssemblyInitKey"]);
        Assert.AreEqual("ClassInitValue", TestContext.Properties["ClassInitKey"]);
        Assert.AreEqual("FromClassInit", TestContext.Properties["SharedKey"]);

        // Properties added by a sibling test method must NOT leak across tests.
        Assert.IsFalse(
            TestContext.Properties.ContainsKey("FromTestOne"),
            "Properties set by TestMethodOne should not leak into TestMethodTwo.");

        TestContext.Properties["FromTestTwo"] = "ShouldNotLeak";
    }

    [ClassCleanup]
    public static void ClassCleanup(TestContext context)
    {
        Assert.AreEqual("AssemblyInitValue", context.Properties["AssemblyInitKey"]);
        Assert.AreEqual("ClassInitValue", context.Properties["ClassInitKey"]);
        Assert.AreEqual("FromClassInit", context.Properties["SharedKey"]);
    }

    [AssemblyCleanup]
    public static void AssemblyCleanup(TestContext context)
    {
        // AssemblyCleanup must see AssemblyInit-set properties, including any override the
        // AssemblyInit itself made. It must NOT see ClassInit-set properties (those are class-scoped).
        Assert.AreEqual("AssemblyInitValue", context.Properties["AssemblyInitKey"]);
        Assert.AreEqual("FromAssemblyInit", context.Properties["SharedKey"]);
        Assert.IsFalse(
            context.Properties.ContainsKey("ClassInitKey"),
            "Properties set by ClassInitialize should not flow to AssemblyCleanup.");
        // SecondClassTests.ClassInit must not leak into the AssemblyCleanup either.
        Assert.IsFalse(
            context.Properties.ContainsKey("SecondClassKey"),
            "Properties set by SecondClassTests.ClassInitialize should not flow to AssemblyCleanup.");
    }
}

// Second class to verify that the assembly-init snapshot flows here too, AND that the
// FIRST class's class-init snapshot does NOT leak into THIS class's contexts.
[TestClass]
public sealed class SecondClassTests
{
    public TestContext TestContext { get; set; } = null!;

    [ClassInitialize]
    public static void ClassInit(TestContext context)
    {
        // AssemblyInit-set properties must be visible here too.
        Assert.AreEqual("AssemblyInitValue", context.Properties["AssemblyInitKey"]);
        Assert.AreEqual("FromAssemblyInit", context.Properties["SharedKey"]);
        // PropertyFlowTests's class-init properties must NOT leak across classes.
        Assert.IsFalse(
            context.Properties.ContainsKey("ClassInitKey"),
            "Properties set by another class's ClassInitialize should not flow to this class's ClassInitialize.");

        context.Properties["SecondClassKey"] = "SecondClassValue";
    }

    [TestMethod]
    public void TestMethod()
    {
        Assert.AreEqual("AssemblyInitValue", TestContext.Properties["AssemblyInitKey"]);
        Assert.AreEqual("SecondClassValue", TestContext.Properties["SecondClassKey"]);
        // First class's class-init properties must not leak.
        Assert.IsFalse(
            TestContext.Properties.ContainsKey("ClassInitKey"),
            "Properties set by another class's ClassInitialize should not flow to this class's tests.");
    }
}

// Data-driven flow: a single TestContext is used for all data rows so the merged properties
// from AssemblyInit/ClassInit are visible to every row.
[TestClass]
public sealed class DataRowFlowTests
{
    public TestContext TestContext { get; set; } = null!;

    [ClassInitialize]
    public static void ClassInit(TestContext context)
        => context.Properties["DataRowClassInitKey"] = "DataRowClassInitValue";

    [TestMethod]
    [DataRow(1)]
    [DataRow(2)]
    public void DataRowSeesFlowedProperties(int rowNumber)
    {
        Assert.AreEqual("AssemblyInitValue", TestContext.Properties["AssemblyInitKey"]);
        Assert.AreEqual("DataRowClassInitValue", TestContext.Properties["DataRowClassInitKey"]);
        Assert.IsTrue(rowNumber > 0);
    }
}
""";
    }

    public TestContext TestContext { get; set; } = null!;
}

/// <summary>
/// Acceptance test for the <c>ClassCleanupManager.ForceCleanup</c> fallback path that runs
/// when execution stops early (e.g. via <c>--maximum-failed-tests</c>). It validates that
/// <c>ClassCleanup</c> and <c>AssemblyCleanup</c> invoked via that fallback still observe
/// the lifecycle property snapshots captured during <c>AssemblyInitialize</c> and
/// <c>ClassInitialize</c>.
/// </summary>
[TestClass]
public sealed class TestContextPropertyFlowForceCleanupTests : AcceptanceTestBase<TestContextPropertyFlowForceCleanupTests.TestAssetFixture>
{
    private const string ClassCleanupMarker = "FORCECLEANUP_CLASSCLEANUP_OK";
    private const string AssemblyCleanupMarker = "FORCECLEANUP_ASSEMBLYCLEANUP_OK";

    [TestMethod]
    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    public async Task ForceCleanupSeesAssemblyAndClassInitProperties(string tfm)
    {
        var testHost = TestHost.LocateFrom(AssetFixture.ProjectPath, TestAssetFixture.ProjectName, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync("--maximum-failed-tests 1", cancellationToken: TestContext.CancellationToken);

        testHostResult.AssertExitCodeIs(ExitCode.TestExecutionStoppedForMaxFailedTests);
        // ClassCleanup and AssemblyCleanup invoked via ForceCleanup must see both snapshots.
        // The cleanup methods emit a sentinel marker only when their assertions pass; the
        // markers' presence proves the snapshots flowed correctly into the fallback contexts.
        Assert.Contains(ClassCleanupMarker, testHostResult.StandardOutput);
        Assert.Contains(AssemblyCleanupMarker, testHostResult.StandardOutput);
    }

    public sealed class TestAssetFixture() : TestAssetFixtureBase()
    {
        public const string ProjectName = "TestContextPropertyFlowForceCleanup";

        public string ProjectPath => GetAssetPath(ProjectName);

        public override (string ID, string Name, string Code) GetAssetsToGenerate() => (ProjectName, ProjectName,
                SourceCode
                .PatchTargetFrameworks(TargetFrameworks.All)
                .PatchCodeWithReplace("$MSTestVersion$", MSTestVersion));

        private const string SourceCode = """
#file TestContextPropertyFlowForceCleanup.csproj
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <EnableMSTestRunner>true</EnableMSTestRunner>
    <TargetFrameworks>$TargetFrameworks$</TargetFrameworks>
    <LangVersion>preview</LangVersion>
    <EnableMicrosoftTestingPlatform>true</EnableMicrosoftTestingPlatform>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="MSTest" Version="$MSTestVersion$" />
  </ItemGroup>

</Project>

#file UnitTest1.cs
using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
public sealed class ForceCleanupFlowTests
{
    public TestContext TestContext { get; set; } = null!;

    [AssemblyInitialize]
    public static void AssemblyInit(TestContext context)
        => context.Properties["AssemblyInitKey"] = "AssemblyInitValue";

    [ClassInitialize]
    public static void ClassInit(TestContext context)
        => context.Properties["ClassInitKey"] = "ClassInitValue";

    // Failing test triggers --maximum-failed-tests=1 graceful stop, leaving the
    // remaining tests un-run; the normal end-of-class / end-of-assembly cleanup is
    // skipped, so cleanup must come through the ForceCleanup fallback path.
    [TestMethod]
    public void TestA_Fails() => Assert.Fail("intentional fail to trigger graceful stop");

    [TestMethod]
    public void TestB_Passes() { }

    [TestMethod]
    public void TestC_Passes() { }

    [ClassCleanup]
    public static void ClassCleanup(TestContext context)
    {
        Assert.AreEqual("AssemblyInitValue", context.Properties["AssemblyInitKey"]);
        Assert.AreEqual("ClassInitValue", context.Properties["ClassInitKey"]);
        Console.WriteLine("FORCECLEANUP_CLASSCLEANUP_OK");
    }

    [AssemblyCleanup]
    public static void AssemblyCleanup(TestContext context)
    {
        // AssemblyCleanup must see AssemblyInit-set values. ClassInit-set values are
        // class-scoped and must NOT flow to AssemblyCleanup even via the fallback path.
        Assert.AreEqual("AssemblyInitValue", context.Properties["AssemblyInitKey"]);
        Assert.IsFalse(
            context.Properties.ContainsKey("ClassInitKey"),
            "Properties set by ClassInitialize must not flow to AssemblyCleanup, even via ForceCleanup.");
        Console.WriteLine("FORCECLEANUP_ASSEMBLYCLEANUP_OK");
    }
}
""";
    }

    public TestContext TestContext { get; set; } = null!;
}
