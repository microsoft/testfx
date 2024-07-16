// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Acceptance.IntegrationTests;
using Microsoft.Testing.Platform.Acceptance.IntegrationTests.Helpers;

namespace MSTest.Acceptance.IntegrationTests;

[TestGroup]
public sealed class TestContextTests : AcceptanceTestBase
{
    private readonly TestAssetFixture _testAssetFixture;

    public TestContextTests(ITestExecutionContext testExecutionContext, TestAssetFixture testAssetFixture)
        : base(testExecutionContext)
    {
        _testAssetFixture = testAssetFixture;
    }

    public async Task TestContextsAreCorrectlySet()
    {
        var testHost = TestHost.LocateFrom(_testAssetFixture.ProjectPath, TestAssetFixture.ProjectName, TargetFrameworks.NetCurrent.Arguments);
        TestHostResult testHostResult = await testHost.ExecuteAsync();

        // Assert
        testHostResult.AssertExitCodeIs(0);
        testHostResult.AssertOutputContains("Passed! - Failed: 0, Passed: 4, Skipped: 0, Total: 4");
    }

    [TestFixture(TestFixtureSharingStrategy.PerTestGroup)]
    public sealed class TestAssetFixture(AcceptanceFixture acceptanceFixture) : TestAssetFixtureBase(acceptanceFixture.NuGetGlobalPackagesFolder)
    {
        public const string ProjectName = "TestTestContext";

        public string ProjectPath => GetAssetPath(ProjectName);

        public override IEnumerable<(string ID, string Name, string Code)> GetAssetsToGenerate()
        {
            yield return (ProjectName, ProjectName,
                SourceCode
                .PatchTargetFrameworks(TargetFrameworks.NetCurrent)
                .PatchCodeWithReplace("$MSTestVersion$", MSTestVersion));
        }

        private const string SourceCode = """
#file TestTestContext.csproj
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <EnableMSTestRunner>true</EnableMSTestRunner>
    <TargetFrameworks>$TargetFrameworks$</TargetFrameworks>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="MSTest" Version="$MSTestVersion$" />
  </ItemGroup>

</Project>

#file UnitTest1.cs
using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
public class TestContextCtorWithReadOnlyProperty
{
    public TestContextCtorWithReadOnlyProperty(TestContext testContext)
    {
        TestContext = testContext;
    }

    public TestContext TestContext { get; }

    [TestMethod]
    public void TestMethod()
    {
        TestContext.WriteLine("Method TestContextCtorWithReadOnlyProperty.TestMethod() was called");
    }
}

[TestClass]
public class TestContextCtor
{
    protected TestContext _testContext;

    public TestContextCtor(TestContext testContext)
    {
        _testContext = testContext;
    }

    [TestMethod]
    public void TestMethod()
    {
        _testContext.WriteLine("Method TestContextCtor.TestMethod() was called");
    }
}

[TestClass]
public class TestContextCtorAndProperty
{
    private TestContext _testContext;

    public TestContextCtorAndProperty(TestContext testContext)
    {
        _testContext = testContext;
    }

    public TestContext TestContext { get; set; }

    [TestMethod]
    public void TestMethod()
    {
        _testContext.WriteLine("Method TestContextCtorAndProperty.TestMethod() was called");
        TestContext.WriteLine("Method TestContextCtorAndProperty.TestMethod() was called");
    }
}

[TestClass]
public class TestContextCtorDerived : TestContextCtor
{
    public TestContextCtorDerived(TestContext testContext)
        : base(testContext)
    {
    }

    [TestMethod]
    public void DerivedTestMethod()
    {
        _testContext.WriteLine("Method TestContextCtorDerived.DerivedTestMethod() was called");
    }
}
""";
    }
}
