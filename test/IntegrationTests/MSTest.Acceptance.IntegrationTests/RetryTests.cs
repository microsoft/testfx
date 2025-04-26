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
        TestHostResult testHostResult = await testHost.ExecuteAsync("--settings my.runsettings");

        testHostResult.AssertExitCodeIs(ExitCodes.AtLeastOneTestFailed);
        testHostResult.AssertOutputContains("""
            TestMethod1 executed 1 time.
            TestMethod2 executed 2 times.
            TestMethod3 executed 3 times.
            TestMethod4 executed 4 times.
            TestMethod5 executed 4 times.
            """);

        testHostResult.AssertOutputContains("failed TestMethod5");
        testHostResult.AssertOutputContains("Assert.Fail failed. Failing TestMethod4. Attempts: 4 (from TestContext: 4)");
        testHostResult.AssertOutputContainsSummary(failed: 1, passed: 4, skipped: 0);
    }

    public sealed class TestAssetFixture() : TestAssetFixtureBase(AcceptanceFixture.NuGetGlobalPackagesFolder)
    {
        public const string ProjectName = "RetryTests";

        public string ProjectPath => GetAssetPath(ProjectName);

        public override IEnumerable<(string ID, string Name, string Code)> GetAssetsToGenerate()
        {
            yield return (ProjectName, ProjectName,
                SourceCode
                .PatchTargetFrameworks(TargetFrameworks.NetCurrent)
                .PatchCodeWithReplace("$MSTestVersion$", MSTestVersion));
        }

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
}
