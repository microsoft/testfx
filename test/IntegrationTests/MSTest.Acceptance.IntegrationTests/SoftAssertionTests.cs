// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Acceptance.IntegrationTests;
using Microsoft.Testing.Platform.Acceptance.IntegrationTests.Helpers;
using Microsoft.Testing.Platform.Helpers;

namespace MSTest.Acceptance.IntegrationTests;

[TestClass]
public sealed class SoftAssertionTests : AcceptanceTestBase<SoftAssertionTests.TestAssetFixture>
{
    [TestMethod]
    public async Task ScopeWithNoFailures_TestPasses()
    {
        var testHost = TestHost.LocateFrom(AssetFixture.ProjectPath, TestAssetFixture.ProjectName, TargetFrameworks.NetCurrent);
        TestHostResult testHostResult = await testHost.ExecuteAsync("--filter ScopeWithNoFailures", cancellationToken: TestContext.CancellationToken);

        testHostResult.AssertExitCodeIs(ExitCodes.Success);
        testHostResult.AssertOutputContainsSummary(failed: 0, passed: 1, skipped: 0);
    }

    [TestMethod]
    public async Task ScopeWithSingleFailure_TestFails()
    {
        var testHost = TestHost.LocateFrom(AssetFixture.ProjectPath, TestAssetFixture.ProjectName, TargetFrameworks.NetCurrent);
        TestHostResult testHostResult = await testHost.ExecuteAsync("--filter ScopeWithSingleFailure", cancellationToken: TestContext.CancellationToken);

        testHostResult.AssertExitCodeIs(ExitCodes.AtLeastOneTestFailed);
        testHostResult.AssertOutputMatchesRegex(
            """failed ScopeWithSingleFailure \(\d+ms\)[\s\S]+Assert\.AreEqual failed\. Expected:<1>\. Actual:<2>\.[\s\S]+at UnitTest1\.ScopeWithSingleFailure\(\)""");
    }

    [TestMethod]
    public async Task ScopeWithMultipleFailures_TestFailsWithAggregatedMessage()
    {
        var testHost = TestHost.LocateFrom(AssetFixture.ProjectPath, TestAssetFixture.ProjectName, TargetFrameworks.NetCurrent);
        TestHostResult testHostResult = await testHost.ExecuteAsync("--filter ScopeWithMultipleFailures", cancellationToken: TestContext.CancellationToken);

        testHostResult.AssertExitCodeIs(ExitCodes.AtLeastOneTestFailed);
        // Validate the output includes the aggregate message and that inner exception stack traces
        // point to the test method (assertion call site).
        testHostResult.AssertOutputMatchesRegex(
            """failed ScopeWithMultipleFailures \(\d+ms\)[\s\S]+2 assertion\(s\) failed within the assert scope\.[\s\S]+Assert\.AreEqual failed\. Expected:<1>\. Actual:<2>\.[\s\S]+at UnitTest1\.ScopeWithMultipleFailures\(\)[\s\S]+Assert\.IsTrue failed\.[\s\S]+at UnitTest1\.ScopeWithMultipleFailures\(\)""");
    }

    [TestMethod]
    public async Task AssertFailIsHardFailure_ThrowsImmediately()
    {
        var testHost = TestHost.LocateFrom(AssetFixture.ProjectPath, TestAssetFixture.ProjectName, TargetFrameworks.NetCurrent);
        TestHostResult testHostResult = await testHost.ExecuteAsync("--filter AssertFailIsHardFailure", cancellationToken: TestContext.CancellationToken);

        testHostResult.AssertExitCodeIs(ExitCodes.AtLeastOneTestFailed);
        // Assert.Fail is a hard assertion — it throws immediately, even within a scope.
        // The second Assert.Fail should not be reached.
        testHostResult.AssertOutputMatchesRegex(
            """failed AssertFailIsHardFailure \(\d+ms\)[\s\S]+Assert\.Fail failed\. hard failure""");
        testHostResult.AssertOutputDoesNotContain("second failure");
    }

    [TestMethod]
    public async Task ScopeWithSoftFailureFollowedByException_CollectsBoth()
    {
        var testHost = TestHost.LocateFrom(AssetFixture.ProjectPath, TestAssetFixture.ProjectName, TargetFrameworks.NetCurrent);
        TestHostResult testHostResult = await testHost.ExecuteAsync("--filter SoftFailureFollowedByException", cancellationToken: TestContext.CancellationToken);

        testHostResult.AssertExitCodeIs(ExitCodes.AtLeastOneTestFailed);
        testHostResult.AssertOutputMatchesRegex(
            """failed SoftFailureFollowedByException \(\d+ms\)[\s\S]+at UnitTest1\.SoftFailureFollowedByException\(\)""");
    }

    [TestMethod]
    public async Task ScopeWithIsNotNullSoftFailure_CollectsFailure()
    {
        var testHost = TestHost.LocateFrom(AssetFixture.ProjectPath, TestAssetFixture.ProjectName, TargetFrameworks.NetCurrent);
        TestHostResult testHostResult = await testHost.ExecuteAsync("--filter ScopeWithIsNotNullSoftFailure", cancellationToken: TestContext.CancellationToken);

        testHostResult.AssertExitCodeIs(ExitCodes.AtLeastOneTestFailed);
        testHostResult.AssertOutputMatchesRegex(
            """failed ScopeWithIsNotNullSoftFailure \(\d+ms\)[\s\S]+Assert\.IsNotNull failed\.[\s\S]+at UnitTest1\.ScopeWithIsNotNullSoftFailure\(\)""");
    }

    [TestMethod]
    public async Task ScopeAssertionsAreIndependentBetweenTests_SecondTestPasses()
    {
        var testHost = TestHost.LocateFrom(AssetFixture.ProjectPath, TestAssetFixture.ProjectName, TargetFrameworks.NetCurrent);
        TestHostResult testHostResult = await testHost.ExecuteAsync("--filter IndependentTest", cancellationToken: TestContext.CancellationToken);

        testHostResult.AssertExitCodeIs(ExitCodes.Success);
        testHostResult.AssertOutputContainsSummary(failed: 0, passed: 1, skipped: 0);
    }

    public sealed class TestAssetFixture() : TestAssetFixtureBase(AcceptanceFixture.NuGetGlobalPackagesFolder)
    {
        public const string ProjectName = "SoftAssertionTests";

        public string ProjectPath => GetAssetPath(ProjectName);

        public override IEnumerable<(string ID, string Name, string Code)> GetAssetsToGenerate()
        {
            yield return (ProjectName, ProjectName,
                SourceCode
                .PatchTargetFrameworks(TargetFrameworks.NetCurrent)
                .PatchCodeWithReplace("$MSTestVersion$", MSTestVersion));
        }

        private const string SourceCode = """
#file SoftAssertionTests.csproj
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <EnableMSTestRunner>true</EnableMSTestRunner>
    <TargetFrameworks>$TargetFrameworks$</TargetFrameworks>
    <NoWarn>$(NoWarn);MSTESTEXP</NoWarn>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="MSTest.TestAdapter" Version="$MSTestVersion$" />
    <PackageReference Include="MSTest.TestFramework" Version="$MSTestVersion$" />
  </ItemGroup>

</Project>

#file UnitTest1.cs
using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
public class UnitTest1
{
    [TestMethod]
    public void ScopeWithNoFailures()
    {
        using (Assert.Scope())
        {
            Assert.IsTrue(true);
            Assert.AreEqual(1, 1);
        }
    }

    [TestMethod]
    public void ScopeWithSingleFailure()
    {
        using (Assert.Scope())
        {
            Assert.AreEqual(1, 2);
        }
    }

    [TestMethod]
    public void ScopeWithMultipleFailures()
    {
        using (Assert.Scope())
        {
            Assert.AreEqual(1, 2);
            Assert.IsTrue(false);
        }
    }

    [TestMethod]
    public void AssertFailIsHardFailure()
    {
        using (Assert.Scope())
        {
            Assert.Fail("hard failure");
            Assert.Fail("second failure");
        }
    }

    [TestMethod]
    public void SoftFailureFollowedByException()
    {
        string x = null;
        using (Assert.Scope())
        {
            Assert.IsNotNull(x);
            Assert.AreEqual(1, x.Length); // throws NullReferenceException
        }
    }

    [TestMethod]
    public void ScopeWithIsNotNullSoftFailure()
    {
        object value = null;
        using (Assert.Scope())
        {
            Assert.IsNotNull(value);
            Assert.AreEqual(1, 1);
        }
    }

    [TestMethod]
    public void IndependentTest()
    {
        // Verify that a scope from a previous test does not leak into this test.
        Assert.IsTrue(true);
    }
}
""";
    }

    public TestContext TestContext { get; set; }
}
