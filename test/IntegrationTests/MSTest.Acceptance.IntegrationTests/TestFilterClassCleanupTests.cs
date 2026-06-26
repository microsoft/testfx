// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Acceptance.IntegrationTests;
using Microsoft.Testing.Platform.Acceptance.IntegrationTests.Helpers;
using Microsoft.Testing.Platform.Helpers;

namespace MSTest.Acceptance.IntegrationTests;

[TestClass]
public sealed class TestFilterClassCleanupTests : AcceptanceTestBase<TestFilterClassCleanupTests.TestAssetFixture>
{
    public TestContext TestContext { get; set; } = null!;

    // Regression test for the [ClassCleanup] leak when an ITestFilter drops the *last-in-order* test of
    // a class that was already initialized by an earlier (non-dropped) test in the same worker. This is
    // the exact scenario reported for the per-worker sharding pattern (each worker enumerates the full
    // set per class, runs its subset, and Drops the rest). Before the fix, ClassCleanupManager counted
    // the dropped test as the last test of the class, so the drop path skipped class cleanup even though
    // the type had been loaded and [ClassInitialize] had run.
    [TestMethod]
    public async Task ClassCleanup_RunsWhenLastTestOfInitializedClassIsDropped()
    {
        var testHost = TestHost.LocateFrom(AssetFixture.ProjectPath, TestAssetFixture.ProjectName, TargetFrameworks.NetCurrent);
        TestHostResult testHostResult = await testHost.ExecuteAsync(
            "--settings my.runsettings",
            environmentVariables: new Dictionary<string, string?>
            {
                // Drop the last-in-order test of the partially-run class and the only test of the
                // fully-dropped class.
                ["DROP_METHODS"] = "Test_Z_Dropped,OnlyTest",
            },
            cancellationToken: TestContext.CancellationToken);

        // Only the single non-dropped test of PartiallyDroppedClass runs.
        testHostResult.AssertExitCodeIs(ExitCode.Success);
        testHostResult.AssertOutputContainsSummary(failed: 0, passed: 1, skipped: 0);

        // The partially-dropped class was initialized (a real test ran) so its class cleanup MUST run,
        // even though its last-in-order test was filtered out.
        testHostResult.AssertOutputContains("PartiallyDroppedClass.ClassInitialize");
        testHostResult.AssertOutputContains("PartiallyDroppedClass.Test_A_Run");
        testHostResult.AssertOutputContains("PartiallyDroppedClass.ClassCleanup");
        testHostResult.AssertOutputDoesNotContain("PartiallyDroppedClass.Test_Z_Dropped");

        // The fully-dropped class never loaded its type, so neither [ClassInitialize] nor [ClassCleanup]
        // should run. This guards the fix from over-correcting (running cleanup for a class that was
        // never initialized).
        testHostResult.AssertOutputDoesNotContain("FullyDroppedClass.ClassInitialize");
        testHostResult.AssertOutputDoesNotContain("FullyDroppedClass.ClassCleanup");
        testHostResult.AssertOutputDoesNotContain("FullyDroppedClass.OnlyTest");
    }

    // Covers the failure branch of the drop path: when the dropped test is the last in its class and
    // the class's [ClassCleanup] throws, the failure must surface and be attributed to the last real
    // test that ran in the class (Test_A_Run), not to the dropped test (which produced no result of
    // its own). This exercises the `cleanupResult is not null` branch and the AssociatedUnitTestElement
    // attribution in FinishFilteredOutTestAsync.
    [TestMethod]
    public async Task ClassCleanup_FailureIsAttributedToLastRealTest_WhenLastTestIsDropped()
    {
        var testHost = TestHost.LocateFrom(AssetFixture.ProjectPath, TestAssetFixture.ProjectName, TargetFrameworks.NetCurrent);
        TestHostResult testHostResult = await testHost.ExecuteAsync(
            "--settings my.runsettings",
            environmentVariables: new Dictionary<string, string?>
            {
                ["DROP_METHODS"] = "Test_Z_Dropped,OnlyTest",
                // Make PartiallyDroppedClass.ClassCleanup throw so we exercise the failure branch.
                ["THROW_CLEANUP"] = "1",
            },
            cancellationToken: TestContext.CancellationToken);

        // The class was initialized and its cleanup ran (in the drop path) and threw, so the run fails.
        testHostResult.AssertExitCodeIs(ExitCode.AtLeastOneTestFailed);

        // The cleanup actually ran in the drop path and its failure surfaced (proves the
        // `cleanupResult is not null` branch and the filterResult spread are exercised).
        testHostResult.AssertOutputContains("PartiallyDroppedClass.ClassInitialize");
        testHostResult.AssertOutputContains("PartiallyDroppedClass.Test_A_Run");
        testHostResult.AssertOutputContains("ClassCleanup failed on purpose");

        // The failure is attributed to the last real test that ran (Test_A_Run), not the dropped test.
        testHostResult.AssertOutputContains("PartiallyDroppedClass.Test_A_Run");
        testHostResult.AssertOutputDoesNotContain("PartiallyDroppedClass.Test_Z_Dropped");
    }

    public sealed class TestAssetFixture() : TestAssetFixtureBase()
    {
        public const string ProjectName = "TestFilterClassCleanup";

        public string ProjectPath => GetAssetPath(ProjectName);

        public override (string ID, string Name, string Code) GetAssetsToGenerate() => (ProjectName, ProjectName,
                SourceCode
                .PatchTargetFrameworks(TargetFrameworks.NetCurrent)
                .PatchCodeWithReplace("$MSTestVersion$", MSTestVersion));

        private const string SourceCode = """
#file TestFilterClassCleanup.csproj
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
    <!-- Force alphabetical ordering so Test_A_Run always executes before Test_Z_Dropped, making the
         "last-in-order test is dropped" scenario deterministic instead of relying on discovery order. -->
    <OrderTestsByNameInClass>true</OrderTestsByNameInClass>
  </MSTest>
</RunSettings>

#file ShardFilter.cs
using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[assembly: TestFilterProvider(typeof(ShardFilter))]

// Drops every test method whose name is listed in the DROP_METHODS environment variable. This mimics a
// worker that claims a subset of tests and drops the rest before the test type is loaded.
public sealed class ShardFilter : ITestFilter
{
    private static readonly HashSet<string> DropMethods = new(
        (Environment.GetEnvironmentVariable("DROP_METHODS") ?? string.Empty)
            .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries),
        StringComparer.Ordinal);

    public TestFilterResult Filter(TestFilterContext context)
        => DropMethods.Contains(context.MethodName) ? TestFilterResult.Drop : TestFilterResult.Run;
}

#file PartiallyDroppedClass.cs
using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;

// This class runs at least one real test (Test_A_Run) so [ClassInitialize] executes, but its
// last-in-order test (Test_Z_Dropped) is filtered out. Its [ClassCleanup] must still run.
[TestClass]
public class PartiallyDroppedClass
{
    [ClassInitialize]
    public static void ClassInitialize(TestContext context)
        => Console.WriteLine("PartiallyDroppedClass.ClassInitialize");

    [ClassCleanup]
    public static void ClassCleanup()
    {
        if (Environment.GetEnvironmentVariable("THROW_CLEANUP") == "1")
        {
            throw new InvalidOperationException("PartiallyDroppedClass.ClassCleanup failed on purpose");
        }

        Console.WriteLine("PartiallyDroppedClass.ClassCleanup");
    }

    // Method names are intentional: with <OrderTestsByNameInClass> enabled, alphabetical ordering
    // guarantees Test_A_Run executes first (triggering [ClassInitialize]) and Test_Z_Dropped is the
    // last-in-order test that the filter drops — exactly the scenario the fix addresses. Renaming or
    // reordering these would silently stop exercising the bug.
    [TestMethod]
    public void Test_A_Run()
        => Console.WriteLine("PartiallyDroppedClass.Test_A_Run");

    [TestMethod]
    public void Test_Z_Dropped()
        => Console.WriteLine("PartiallyDroppedClass.Test_Z_Dropped");
}

#file FullyDroppedClass.cs
using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;

// Every test of this class is dropped, so the type is never loaded: neither [ClassInitialize] nor
// [ClassCleanup] should run.
[TestClass]
public class FullyDroppedClass
{
    [ClassInitialize]
    public static void ClassInitialize(TestContext context)
        => Console.WriteLine("FullyDroppedClass.ClassInitialize");

    [ClassCleanup]
    public static void ClassCleanup()
        => Console.WriteLine("FullyDroppedClass.ClassCleanup");

    [TestMethod]
    public void OnlyTest()
        => Console.WriteLine("FullyDroppedClass.OnlyTest");
}
""";
    }
}
