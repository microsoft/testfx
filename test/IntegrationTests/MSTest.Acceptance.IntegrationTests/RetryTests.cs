// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Acceptance.IntegrationTests;
using Microsoft.Testing.Platform.Acceptance.IntegrationTests.Helpers;
using Microsoft.Testing.Platform.Helpers;

namespace MSTest.Acceptance.IntegrationTests;

[TestClass]
public sealed class RetryTests : AcceptanceTestBase<RetryTests.TestAssetFixture>
{
    [TestMethod]
    public async Task BasicRetryScenarioTest()
    {
        var testHost = TestHost.LocateFrom(AssetFixture.ProjectPath, TestAssetFixture.ProjectName, TargetFrameworks.NetCurrent);
        TestHostResult testHostResult = await testHost.ExecuteAsync("--settings my.runsettings", cancellationToken: TestContext.CancellationToken);

        testHostResult.AssertExitCodeIs(ExitCode.AtLeastOneTestFailed);

        // The "executed N times." lines are written directly to stdout by ClassCleanup (CaptureTraceOutput
        // is false in my.runsettings), so the live terminal reporter can interleave its own "failed ..."
        // lines between them. Assert each line individually instead of as a single contiguous block to avoid
        // depending on the ordering of two concurrent stdout writers.
        testHostResult.AssertOutputContains("TestMethod1 executed 1 time.");
        testHostResult.AssertOutputContains("TestMethod2 executed 2 times.");
        testHostResult.AssertOutputContains("TestMethod3 executed 3 times.");
        testHostResult.AssertOutputContains("TestMethod4 executed 4 times.");
        testHostResult.AssertOutputContains("TestMethod5 executed 4 times.");

        testHostResult.AssertOutputContains("failed TestMethod5");
        testHostResult.AssertOutputMatchesRegex(
            """Assertion failed\.[\r\n]+\s+Failing TestMethod4\. Attempts: 4 \(from TestContext: 4\)""");
        testHostResult.AssertOutputContainsSummary(failed: 1, passed: 4, skipped: 0);
    }

    public sealed class TestAssetFixture() : TestAssetFixtureBase()
    {
        public const string ProjectName = "RetryTests";

        public string ProjectPath => GetAssetPath(ProjectName);

        public override (string ID, string Name, string Code) GetAssetsToGenerate() => (ProjectName, ProjectName,
                SourceCode
                .PatchTargetFrameworks(TargetFrameworks.NetCurrent)
                .PatchCodeWithReplace("$MSTestVersion$", MSTestVersion));

        private const string SourceCode = """
#file RetryTests.csproj
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

#file UnitTest1.cs
using System;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
public class UnitTest1
{
    private static int _count1;
    private static int _count2;
    private static int _count3;
    private static int _count4;
    private static int _count5;

    public TestContext TestContext { get; set; }

    [TestMethod]
    [Retry(3)]
    public void TestMethod1()
    {
        _count1++;
    }

    [TestMethod]
    [Retry(3)]
    public void TestMethod2()
    {
        _count2++;
        // This will fail TestMethod2 once. Then the first retry attempt will work.
        if (_count2 <= 1) Assert.Fail("Failing TestMethod2");
    }

    [TestMethod]
    [Retry(3)]
    public void TestMethod3()
    {
        _count3++;
        // This will fail TestMethod3 twice. Then the second retry attempt will work.
        if (_count3 <= 2) Assert.Fail("Failing TestMethod3");
    }

    [TestMethod]
    [Retry(3)]
    public void TestMethod4()
    {
        _count4++;
        // This will fail TestMethod4 three times. Then the third retry attempt will work.
        // In total, this test will run four times, and the last run will pass.
        if (_count4 <= 3) Assert.Fail("Failing TestMethod4");
    }

    [TestMethod]
    [Retry(3)]
    public void TestMethod5()
    {
        _count5++;
        // This will fail TestMethod5 four times. The end result is failure of this test.
        Assert.Fail($"Failing TestMethod4. Attempts: {_count5} (from TestContext: {TestContext.TestRunCount})");
    }

    [ClassCleanup]
    public static void ClassCleanup()
    {
        Console.WriteLine($"TestMethod1 executed {_count1} time.");
        Console.WriteLine($"TestMethod2 executed {_count2} times.");
        Console.WriteLine($"TestMethod3 executed {_count3} times.");
        Console.WriteLine($"TestMethod4 executed {_count4} times.");
        Console.WriteLine($"TestMethod5 executed {_count5} times.");
    }
}

#file my.runsettings
<RunSettings>
  <MSTest>
    <CaptureTraceOutput>false</CaptureTraceOutput>
  </MSTest>
</RunSettings>
""";
    }

    public TestContext TestContext { get; set; }
}

[TestClass]
public sealed class ClassLevelRetryTests : AcceptanceTestBase<ClassLevelRetryTests.TestAssetFixture>
{
    [TestMethod]
    public async Task ClassLevelRetry_AppliesToAllTestMethods_AndMethodLevelOverrides()
    {
        var testHost = TestHost.LocateFrom(AssetFixture.ProjectPath, TestAssetFixture.ProjectName, TargetFrameworks.NetCurrent);

        // The retry counts are recorded by ClassCleanup. They must NOT be asserted against the test host's
        // stdout: ClassCleanup writes them concurrently with the live terminal reporter (which emits its own
        // "failed ..." lines), so the two writers can interleave - even within a single line - making any
        // stdout-based assertion flaky. Instead, ClassCleanup writes the counts to a marker file in a
        // directory we pass via an environment variable, and we read that file back here.
        string markerDirectory = Path.Combine(Path.GetTempPath(), "mstest-class-level-retry-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(markerDirectory);
        try
        {
            TestHostResult testHostResult = await testHost.ExecuteAsync(
                environmentVariables: new() { [TestAssetFixture.MarkerDirectoryEnvVar] = markerDirectory },
                cancellationToken: TestContext.CancellationToken);

            testHostResult.AssertExitCodeIs(ExitCode.AtLeastOneTestFailed);
            testHostResult.AssertOutputContainsSummary(failed: 2, passed: 1, skipped: 0);

            string markerFile = Path.Combine(markerDirectory, TestAssetFixture.MarkerFileName);
            Assert.IsTrue(
                File.Exists(markerFile),
                $"Retry-count marker file not found. StandardOutput:\n{testHostResult.StandardOutput}");

            string[] counts = File.ReadAllLines(markerFile);

            // ClassLevelOnly is decorated only by class-level [Retry(3)] => 4 total runs.
            // MethodLevelOverride overrides the class-level [Retry(3)] with method-level [Retry(1)] => 2 total runs.
            // PassingMethod also has class-level retry but passes on first attempt => 1 total run.
            Assert.Contains("ClassLevelOnly=4", counts);
            Assert.Contains("MethodLevelOverride=2", counts);
            Assert.Contains("PassingMethod=1", counts);
        }
        finally
        {
            try
            {
                Directory.Delete(markerDirectory, recursive: true);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Best-effort cleanup of the temporary marker directory.
            }
        }
    }

    public sealed class TestAssetFixture() : TestAssetFixtureBase()
    {
        public const string ProjectName = "ClassLevelRetryTests";

        public const string MarkerDirectoryEnvVar = "CLASSLEVELRETRY_MARKER_DIR";

        public const string MarkerFileName = "retry-counts.marker";

        public string ProjectPath => GetAssetPath(ProjectName);

        public override (string ID, string Name, string Code) GetAssetsToGenerate() => (ProjectName, ProjectName,
                SourceCode
                .PatchTargetFrameworks(TargetFrameworks.NetCurrent)
                .PatchCodeWithReplace("$MSTestVersion$", MSTestVersion)
                .PatchCodeWithReplace("$MarkerDirectoryEnvVar$", MarkerDirectoryEnvVar)
                .PatchCodeWithReplace("$MarkerFileName$", MarkerFileName));

        private const string SourceCode = """
#file ClassLevelRetryTests.csproj
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
</Project>

#file UnitTest1.cs
using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
[Retry(3)]
public class UnitTest1
{
    private static int _classLevelOnly;
    private static int _methodOverride;
    private static int _passing;

    [TestMethod]
    public void ClassLevelOnly()
    {
        _classLevelOnly++;
        Assert.Fail("Always failing ClassLevelOnly");
    }

    [TestMethod]
    [Retry(1)]
    public void MethodLevelOverride()
    {
        _methodOverride++;
        Assert.Fail("Always failing MethodLevelOverride");
    }

    [TestMethod]
    public void PassingMethod()
    {
        _passing++;
    }

    [ClassCleanup]
    public static void ClassCleanup()
    {
        // Write the retry counts to a marker file rather than stdout. Asserting against stdout is flaky
        // because the live terminal reporter writes concurrently with these lines and can interleave with
        // (or split) them. A file write is observed atomically by the acceptance test.
        string markerDirectory = Environment.GetEnvironmentVariable("$MarkerDirectoryEnvVar$");
        if (string.IsNullOrEmpty(markerDirectory))
        {
            return;
        }

        File.WriteAllLines(
            Path.Combine(markerDirectory, "$MarkerFileName$"),
            new[]
            {
                $"ClassLevelOnly={_classLevelOnly}",
                $"MethodLevelOverride={_methodOverride}",
                $"PassingMethod={_passing}",
            });
    }
}
""";
    }

    public TestContext TestContext { get; set; }
}
