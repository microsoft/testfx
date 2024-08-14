// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Acceptance.IntegrationTests;
using Microsoft.Testing.Platform.Acceptance.IntegrationTests.Helpers;

namespace MSTest.Acceptance.IntegrationTests;

[TestGroup]
public sealed class CancellationTests : AcceptanceTestBase
{
    private readonly TestAssetFixture _testAssetFixture;

    public CancellationTests(ITestExecutionContext testExecutionContext, TestAssetFixture testAssetFixture)
        : base(testExecutionContext) => _testAssetFixture = testAssetFixture;

    public async Task WhenCancelingTestContextTokenInAssemblyInit_MessageIsAsExpected()
    {
        var testHost = TestHost.LocateFrom(_testAssetFixture.ProjectPath, TestAssetFixture.ProjectName, TargetFrameworks.NetCurrent.Arguments);
        TestHostResult testHostResult = await testHost.ExecuteAsync(environmentVariables: new()
        {
            ["ASSEMBLYINIT_CANCEL"] = "1",
        });

        // Assert
        testHostResult.AssertExitCodeIs(2);
        testHostResult.AssertOutputContains("Assembly initialize method 'UnitTest1.AssemblyInitialize' was canceled");
        testHostResult.AssertOutputContains("Failed!");
    }

    public async Task WhenCancelingTestContextTokenInClassInit_MessageIsAsExpected()
    {
        var testHost = TestHost.LocateFrom(_testAssetFixture.ProjectPath, TestAssetFixture.ProjectName, TargetFrameworks.NetCurrent.Arguments);
        TestHostResult testHostResult = await testHost.ExecuteAsync(environmentVariables: new()
        {
            ["CLASSINIT_CANCEL"] = "1",
        });

        // Assert
        testHostResult.AssertExitCodeIs(2);
        testHostResult.AssertOutputContains("Class initialize method 'UnitTest1.ClassInitialize' was canceled");
        testHostResult.AssertOutputContains("Failed!");
    }

    public async Task WhenCancelingTestContextTokenInTestInit_MessageIsAsExpected()
    {
        var testHost = TestHost.LocateFrom(_testAssetFixture.ProjectPath, TestAssetFixture.ProjectName, TargetFrameworks.NetCurrent.Arguments);
        TestHostResult testHostResult = await testHost.ExecuteAsync(environmentVariables: new()
        {
            ["TESTINIT_CANCEL"] = "1",
        });

        // Assert
        testHostResult.AssertExitCodeIs(2);
        testHostResult.AssertOutputContains("Test initialize method 'UnitTest1.TestInit' was canceled");
        testHostResult.AssertOutputContains("Failed!");
    }

    public async Task WhenCancelingTestContextTokenInTestCleanup_MessageIsAsExpected()
    {
        var testHost = TestHost.LocateFrom(_testAssetFixture.ProjectPath, TestAssetFixture.ProjectName, TargetFrameworks.NetCurrent.Arguments);
        TestHostResult testHostResult = await testHost.ExecuteAsync(environmentVariables: new()
        {
            ["TESTCLEANUP_CANCEL"] = "1",
        });

        // Assert
        testHostResult.AssertExitCodeIs(2);
        testHostResult.AssertOutputContains("Test cleanup method 'UnitTest1.TestCleanup' was canceled");
        testHostResult.AssertOutputContains("Failed!");
    }

    public async Task WhenCancelingTestContextTokenInTestMethod_MessageIsAsExpected()
    {
        var testHost = TestHost.LocateFrom(_testAssetFixture.ProjectPath, TestAssetFixture.ProjectName, TargetFrameworks.NetCurrent.Arguments);
        TestHostResult testHostResult = await testHost.ExecuteAsync(environmentVariables: new()
        {
            ["TESTMETHOD_CANCEL"] = "1",
        });

        // Assert
        testHostResult.AssertExitCodeIs(2);
        testHostResult.AssertOutputContains("Test 'TestMethod' execution has been aborted.");
        testHostResult.AssertOutputContains("Failed!");
    }

    [TestFixture(TestFixtureSharingStrategy.PerTestGroup)]
    public sealed class TestAssetFixture(AcceptanceFixture acceptanceFixture) : TestAssetFixtureBase(acceptanceFixture.NuGetGlobalPackagesFolder)
    {
        public const string ProjectName = "TestCancellation";

        public string ProjectPath => GetAssetPath(ProjectName);

        public override IEnumerable<(string ID, string Name, string Code)> GetAssetsToGenerate()
        {
            yield return (ProjectName, ProjectName,
                SourceCode
                .PatchTargetFrameworks(TargetFrameworks.NetCurrent)
                .PatchCodeWithReplace("$MSTestVersion$", MSTestVersion));
        }

        private const string SourceCode = """
#file TestCancellation.csproj
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
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
public class UnitTest1
{
    [AssemblyInitialize]
    public static void AssemblyInitialize(TestContext testContext)
    {
        if (Environment.GetEnvironmentVariable("ASSEMBLYINIT_CANCEL") == "1")
        {
            testContext.CancellationTokenSource.Cancel();
            testContext.CancellationTokenSource.Token.ThrowIfCancellationRequested();
        }
    }

    [ClassInitialize]
    public static void ClassInitialize(TestContext testContext)
    {
        if (Environment.GetEnvironmentVariable("CLASSINIT_CANCEL") == "1")
        {
            testContext.CancellationTokenSource.Cancel();
            testContext.CancellationTokenSource.Token.ThrowIfCancellationRequested();
        }
    }

    public TestContext TestContext { get; set; }

    [TestInitialize]
    public void TestInit()
    {
        if (Environment.GetEnvironmentVariable("TESTINIT_CANCEL") == "1")
        {
            TestContext.CancellationTokenSource.Cancel();
            TestContext.CancellationTokenSource.Token.ThrowIfCancellationRequested();
        }
    }

    [TestCleanup]
    public void TestCleanup()
    {
        if (Environment.GetEnvironmentVariable("TESTCLEANUP_CANCEL") == "1")
        {
            TestContext.CancellationTokenSource.Cancel();
            TestContext.CancellationTokenSource.Token.ThrowIfCancellationRequested();
        }
    }

    [TestMethod]
    public void TestMethod()
    {
        if (Environment.GetEnvironmentVariable("TESTMETHOD_CANCEL") == "1")
        {
            TestContext.CancellationTokenSource.Cancel();
            TestContext.CancellationTokenSource.Token.ThrowIfCancellationRequested();
        }
    }
}
""";
    }
}
