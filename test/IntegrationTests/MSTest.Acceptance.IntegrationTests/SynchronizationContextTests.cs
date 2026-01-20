// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Acceptance.IntegrationTests;
using Microsoft.Testing.Platform.Acceptance.IntegrationTests.Helpers;

namespace MSTest.Acceptance.IntegrationTests;

[TestClass]
public sealed class SynchronizationContextTests : AcceptanceTestBase<SynchronizationContextTests.TestAssetFixture>
{
    [TestMethod]
    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    public async Task SynchronizationContext_WhenSetInTestInitialize_IsPreservedInTestMethod(string tfm)
    {
        var testHost = TestHost.LocateFrom(AssetFixture.ProjectPath, TestAssetFixture.ProjectName, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync(
            environmentVariables: new Dictionary<string, string?>()
            {
                ["SET_SYNC_CONTEXT_TEST_INIT"] = "1",
            }, cancellationToken: TestContext.CancellationToken);

        testHostResult.AssertExitCodeIs(0);
        testHostResult.AssertOutputContainsSummary(failed: 0, passed: 1, skipped: 0);
    }

    [TestMethod]
    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    public async Task SynchronizationContext_WhenSetInGlobalTestInitialize_IsPreservedInBothTestInitAndTestMethod(string tfm)
    {
        var testHost = TestHost.LocateFrom(AssetFixture.ProjectPath, TestAssetFixture.ProjectName, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync(
            environmentVariables: new Dictionary<string, string?>()
            {
                ["SET_SYNC_CONTEXT_GLOBAL_TEST_INIT"] = "1",
            }, cancellationToken: TestContext.CancellationToken);

        testHostResult.AssertExitCodeIs(0);
        testHostResult.AssertOutputContainsSummary(failed: 0, passed: 1, skipped: 0);
    }

    public sealed class TestAssetFixture() : TestAssetFixtureBase(AcceptanceFixture.NuGetGlobalPackagesFolder)
    {
        public const string ProjectName = "SynchronizationContextProject";

        public string ProjectPath => GetAssetPath(ProjectName);

        public override IEnumerable<(string ID, string Name, string Code)> GetAssetsToGenerate()
        {
            yield return (ProjectName, ProjectName,
                SourceCode
                .PatchTargetFrameworks(TargetFrameworks.All)
                .PatchCodeWithReplace("$MSTestVersion$", MSTestVersion));
        }

        private const string SourceCode = """
#file SynchronizationContextProject.csproj
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
namespace SynchronizationContextProject;

using System;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
public class UnitTest1
{
    private static UnitTestSynchronizationContext? _syncContext;

    [GlobalTestInitialize]
    public static void GlobalTestInit(TestContext _)
        => SetSyncContextIfRequested("SET_SYNC_CONTEXT_GLOBAL_TEST_INIT");

    [TestInitialize]
    public void TestInitialize()
    {
        SetSyncContextIfRequested("SET_SYNC_CONTEXT_TEST_INIT");
        AssertSyncContext();
    }

    [TestMethod]
    public void TestMethod_SynchronizationContextShouldBePreserved()
        => AssertSyncContext();

    [TestCleanup]
    public void TestCleanup()
    {
        Assert.IsNotNull(SynchronizationContext.Current);
        Assert.AreSame(_syncContext, SynchronizationContext.Current);
    }

    private static void SetSyncContextIfRequested(string envVariable)
    {
        if (Environment.GetEnvironmentVariable(envVariable) == "1")
        {
            _syncContext = new UnitTestSynchronizationContext();
            SynchronizationContext.SetSynchronizationContext(_syncContext);
        }
    }

    private static void AssertSyncContext()
    {
        Assert.IsNotNull(SynchronizationContext.Current);
        Assert.AreSame(_syncContext, SynchronizationContext.Current);
    }
}

public class UnitTestSynchronizationContext : SynchronizationContext
{
}
""";
    }

    public TestContext TestContext { get; set; }
}
