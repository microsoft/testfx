// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Acceptance.IntegrationTests;
using Microsoft.Testing.Platform.Acceptance.IntegrationTests.Helpers;
using Microsoft.Testing.Platform.Helpers;

namespace MSTest.Acceptance.IntegrationTests;

[TestClass]
public sealed class TimeoutTestMethodTests : AcceptanceTestBase<TimeoutTestMethodTests.TestAssetFixture>
{
    [TestMethod]
    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    public async Task Timeout_WhenMethodTimeoutAndWaitInCtor_TestGetsCanceled(string tfm)
    {
        var testHost = TestHost.LocateFrom(AssetFixture.TargetAssetPath, TestAssetFixture.ProjectName, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync(
            environmentVariables: new()
            {
                ["LONG_WAIT_CTOR"] = "1",
            },
            cancellationToken: TestContext.CancellationToken);

        testHostResult.AssertExitCodeIs(ExitCode.AtLeastOneTestFailed);
        testHostResult.AssertOutputContains("Test 'TestMethod' timed out after 1000ms");
    }

    [TestMethod]
    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    public async Task Timeout_WhenMethodTimeoutAndWaitInTestInit_TestGetsCanceled(string tfm)
    {
        var testHost = TestHost.LocateFrom(AssetFixture.TargetAssetPath, TestAssetFixture.ProjectName, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync(
            environmentVariables: new()
            {
                ["LONG_WAIT_TESTINIT"] = "1",
            },
            cancellationToken: TestContext.CancellationToken);

        testHostResult.AssertExitCodeIs(ExitCode.AtLeastOneTestFailed);
        testHostResult.AssertOutputContains("Test 'TestMethod' timed out after 1000ms");
    }

    [TestMethod]
    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    public async Task Timeout_WhenMethodTimeoutAndWaitInTestCleanup_TestGetsCanceled(string tfm)
    {
        var testHost = TestHost.LocateFrom(AssetFixture.TargetAssetPath, TestAssetFixture.ProjectName, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync(
            environmentVariables: new()
            {
                ["LONG_WAIT_TESTCLEANUP"] = "1",
            },
            cancellationToken: TestContext.CancellationToken);

        testHostResult.AssertExitCodeIs(ExitCode.AtLeastOneTestFailed);
        testHostResult.AssertOutputContains("Test 'TestMethod' timed out after 1000ms");
    }

    [TestMethod]
    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    public async Task Timeout_WhenMethodTimeoutAndWaitInTestMethod_TestGetsCanceled(string tfm)
    {
        var testHost = TestHost.LocateFrom(AssetFixture.TargetAssetPath, TestAssetFixture.ProjectName, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync(
            environmentVariables: new()
            {
                ["LONG_WAIT_TEST"] = "1",
            },
            cancellationToken: TestContext.CancellationToken);

        testHostResult.AssertExitCodeIs(ExitCode.AtLeastOneTestFailed);
        testHostResult.AssertOutputContains("Test 'TestMethod' timed out after 1000ms");
    }

    public sealed class TestAssetFixture : TestAssetFixtureBase
    {
        public const string ProjectName = "TimeoutTestMethodTimeout";

        public string TargetAssetPath => GetAssetPath(ProjectName);

        public override (string ID, string Name, string Code) GetAssetsToGenerate() => (ProjectName, ProjectName,
                SourceCode
                .PatchTargetFrameworks(TargetFrameworks.All)
                .PatchCodeWithReplace("$ProjectName$", ProjectName)
                .PatchCodeWithReplace("$TimeoutExtraArgs$", string.Empty)
                .PatchCodeWithReplace("$MSTestVersion$", MSTestVersion));

        private const string SourceCode = """
#file $ProjectName$.csproj
<Project Sdk="Microsoft.NET.Sdk">
   <PropertyGroup>
    <TargetFrameworks>$TargetFrameworks$</TargetFrameworks>
    <EnableMSTestRunner>true</EnableMSTestRunner>
    <OutputType>Exe</OutputType>
    <Nullable>enable</Nullable>
    <LangVersion>preview</LangVersion>

    <!--
        This property is not required by users and is only set to simplify our testing infrastructure. When testing out in local or ci,
        we end up with a -dev or -ci version which will lose resolution over -preview dependency of code coverage. Because we want to
        ensure we are testing with locally built version, we force adding the platform dependency.
    -->
    <EnableMicrosoftTestingPlatform>true</EnableMicrosoftTestingPlatform>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="MSTest" Version="$MSTestVersion$" />
  </ItemGroup>
</Project>

#file UnitTest1.cs
using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
namespace TimeoutTest;
[TestClass]
public class UnitTest1
{
    private readonly TestContext _testContext;

    public UnitTest1(TestContext testContext)
    {
        _testContext = testContext;
        if (Environment.GetEnvironmentVariable("LONG_WAIT_CTOR") == "1")
        {
            Task.Delay(10_000, _testContext.CancellationTokenSource.Token).Wait();
        }
    }

    [TestInitialize]
    public async Task TestInit()
    {
        if (Environment.GetEnvironmentVariable("LONG_WAIT_TESTINIT") == "1")
        {
            await Task.Delay(10_000, _testContext.CancellationTokenSource.Token);
        }
    }

    [TestCleanup]
    public async Task TestCleanup()
    {
        if (Environment.GetEnvironmentVariable("LONG_WAIT_TESTCLEANUP") == "1")
        {
            await Task.Delay(10_000, _testContext.CancellationTokenSource.Token);
        }
    }

    [TestMethod]
    [Timeout(1000$TimeoutExtraArgs$)]
    public async Task TestMethod()
    {
        if (Environment.GetEnvironmentVariable("LONG_WAIT_TEST") == "1")
        {
            await Task.Delay(10_000, _testContext.CancellationTokenSource.Token);
        }
    }
}
""";
    }

    public TestContext TestContext { get; set; } = default!;
}
