// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Acceptance.IntegrationTests;
using Microsoft.Testing.Platform.Acceptance.IntegrationTests.Helpers;

namespace MSTest.Acceptance.IntegrationTests;

[TestClass]
public sealed class TestContextTests : AcceptanceTestBase<TestContextTests.TestAssetFixture>
{
    [TestMethod]
    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    public async Task TestContextsAreCorrectlySet(string tfm)
    {
        var testHost = TestHost.LocateFrom(AssetFixture.ProjectPath, TestAssetFixture.ProjectName, tfm);
        TestHostResult testHostResult = await testHost.ExecuteAsync("--filter ClassName~TestContextCtor");

        // Assert
        testHostResult.AssertExitCodeIs(0);
        testHostResult.AssertOutputContainsSummary(failed: 0, passed: 5, skipped: 0);
    }

    [TestMethod]
    public async Task TestContext_TestData_PropertyContainsExpectedValue()
    {
        var testHost = TestHost.LocateFrom(AssetFixture.ProjectPath, TestAssetFixture.ProjectName, TargetFrameworks.NetCurrent);
        TestHostResult testHostResult = await testHost.ExecuteAsync("--filter ClassName~TestContextData");

        // Assert
        testHostResult.AssertExitCodeIs(0);
        testHostResult.AssertOutputContainsSummary(failed: 0, passed: 3, skipped: 0);
    }

    [TestMethod]
    public async Task TestContext_TestException_PropertyContainsExpectedValue()
    {
        var testHost = TestHost.LocateFrom(AssetFixture.ProjectPath, TestAssetFixture.ProjectName, TargetFrameworks.NetCurrent);
        TestHostResult testHostResult = await testHost.ExecuteAsync("--filter ClassName~TestContextException");

        // Assert
        testHostResult.AssertExitCodeIs(2);
        testHostResult.AssertOutputContainsSummary(failed: 2, passed: 1, skipped: 0);
        testHostResult.AssertOutputContains("Initialization method TestContextExceptionFailingInTestInit.TInit threw exception. System.InvalidOperationException");
        testHostResult.AssertOutputContains("Test method TestContextExceptionFailingInTestMethod.TestFailingInTestMethod threw exception:");
    }

    [TestMethod]
    public async Task TestContext_TestDisplayName_PropertyContainsExpectedValue()
    {
        var testHost = TestHost.LocateFrom(AssetFixture.ProjectPath, TestAssetFixture.ProjectName, TargetFrameworks.NetCurrent);
        TestHostResult testHostResult = await testHost.ExecuteAsync("--filter ClassName~TestContextDisplayName");

        // Assert
        testHostResult.AssertExitCodeIs(0);
        testHostResult.AssertOutputContainsSummary(failed: 0, passed: 4, skipped: 0);
    }

    [TestMethod]
    public async Task TestContext_Properties_ConsidersClassTypeCorrectly()
    {
        var testHost = TestHost.LocateFrom(AssetFixture.ProjectPath, TestAssetFixture.ProjectName, TargetFrameworks.NetCurrent);
        TestHostResult testHostResult = await testHost.ExecuteAsync("--filter ClassName~TestContextTestPropertyImpl");

        // Assert
        testHostResult.AssertExitCodeIs(0);
        testHostResult.AssertOutputContainsSummary(failed: 0, passed: 1, skipped: 0);
    }

    public sealed class TestAssetFixture() : TestAssetFixtureBase(AcceptanceFixture.NuGetGlobalPackagesFolder)
    {
        public const string ProjectName = "TestTestContext";

        public string ProjectPath => GetAssetPath(ProjectName);

        public override IEnumerable<(string ID, string Name, string Code)> GetAssetsToGenerate()
        {
            yield return (ProjectName, ProjectName,
                SourceCode
                .PatchTargetFrameworks(TargetFrameworks.All)
                .PatchCodeWithReplace("$MSTestVersion$", MSTestVersion));
        }

        private const string SourceCode = """
#file TestTestContext.csproj
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <EnableMSTestRunner>true</EnableMSTestRunner>
    <TargetFrameworks>$TargetFrameworks$</TargetFrameworks>
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
using System.Collections.Generic;
#if NETFRAMEWORK
using System.Runtime.Remoting.Messaging;
#endif
using System.Threading;
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
        Assert.IsNotNull(TestContext);
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
        Assert.IsNotNull(_testContext);
    }
}

[TestClass]
public class TestContextCtorAndProperty
{
    private TestContext _testContext;
    private static AsyncLocal<string> s_asyncLocal = new();

    public TestContextCtorAndProperty(TestContext testContext)
    {
        _testContext = testContext;
    }

    public TestContext TestContext
    {
        get => field;
        set
        {
            field = value;
            s_asyncLocal.Value = "TestContext is set";
        }
    }

    [TestMethod]
    public void TestMethod()
    {
        _testContext.WriteLine("Method TestContextCtorAndProperty.TestMethod() was called");
        TestContext.WriteLine("Method TestContextCtorAndProperty.TestMethod() was called");
        Assert.IsNotNull(_testContext);
        Assert.IsNotNull(TestContext);
        Assert.AreEqual("TestContext is set", s_asyncLocal.Value);
#if NETFRAMEWORK
        Assert.AreEqual("Value from TestInitialize", CallContext.HostContext);
#endif
    }

#if NETFRAMEWORK
    [TestInitialize]
    public void TestInitialize()
    {
        CallContext.HostContext = "Value from TestInitialize";
    }
#endif
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
        Assert.IsNotNull(_testContext);
    }
}

[TestClass]
public class TestContextDataFromNonParameterizedMethod
{
    public TestContext TestContext { get; set; }

    [TestInitialize]
    public void TInit()
    {
        AssertTestContextData();
    }

    [TestMethod]
    public void Test()
    {
        AssertTestContextData();
    }

    [TestCleanup]
    public void TCleanup()
    {
        AssertTestContextData();
    }

    private void AssertTestContextData()
    {
        Assert.IsNull(TestContext.TestData);
    }
}

[TestClass]
public class TestContextDataFromDataRow
{
    public TestContext TestContext { get; set; }

    [TestInitialize]
    public void TInit()
    {
        AssertTestContextData();
    }

    [TestMethod]
    [DataRow(1, "ok")]
    public void Test(int i, string s)
    {
        AssertTestContextData();
    }

    [TestCleanup]
    public void TCleanup()
    {
        AssertTestContextData();
    }

    private void AssertTestContextData()
    {
        Assert.IsNotNull(TestContext.TestData);
        Assert.AreEqual(2, TestContext.TestData.Length);
        Assert.AreEqual(1, TestContext.TestData[0]);
        Assert.AreEqual("ok", TestContext.TestData[1]);
    }
}

[TestClass]
public class TestContextDataFromDynamicData
{
    public TestContext TestContext { get; set; }

    [TestInitialize]
    public void TInit()
    {
        AssertTestContextData();
    }

    [TestMethod]
    [DynamicData(nameof(GetData), DynamicDataSourceType.Method)]
    public void Test(int i, string s)
    {
        AssertTestContextData();
    }

    [TestCleanup]
    public void TCleanup()
    {
        AssertTestContextData();
    }

    private void AssertTestContextData()
    {
        Assert.IsNotNull(TestContext.TestData);
        Assert.AreEqual(2, TestContext.TestData.Length);
        Assert.AreEqual(1, TestContext.TestData[0]);
        Assert.AreEqual("ok", TestContext.TestData[1]);
    }

    private static IEnumerable<object[]> GetData()
    {
        yield return new object[] { 1, "ok" };
    }
}

[TestClass]
public class TestContextExceptionFailingInTestInit
{
    public TestContext TestContext { get; set; }

    [TestInitialize]
    public void TInit()
    {
        Assert.IsNull(TestContext.TestException);
        throw new InvalidOperationException();
    }

    [TestMethod]
    public void TestFailingInTestInit()
    {
    }

    [TestCleanup]
    public void TCleanup()
    {
        Assert.IsNotNull(TestContext.TestException);
        Assert.IsInstanceOfType<InvalidOperationException>(TestContext.TestException);
    }
}

[TestClass]
public class TestContextExceptionFailingInTestMethod
{
    public TestContext TestContext { get; set; }

    [TestInitialize]
    public void TInit()
    {
        Assert.IsNull(TestContext.TestException);
    }

    [TestMethod]
    public void TestFailingInTestMethod()
    {
        Assert.IsNull(TestContext.TestException);
        throw new NotSupportedException();
    }

    [TestCleanup]
    public void TCleanup()
    {
        Assert.IsNotNull(TestContext.TestException);
        Assert.IsInstanceOfType<NotSupportedException>(TestContext.TestException);
    }
}

[TestClass]
public class TestContextExceptionNotFailing
{
    public TestContext TestContext { get; set; }

    [TestInitialize]
    public void TInit()
    {
        Assert.IsNull(TestContext.TestException);
    }

    [TestMethod]
    public void TestNotFailing()
    {
        Assert.IsNull(TestContext.TestException);
    }

    [TestCleanup]
    public void TCleanup()
    {
        Assert.IsNull(TestContext.TestException);
    }
}

// NOTE: We are not testing all possible combinations of display name as it's covered by some other tests.
// We just want to ensure that the main paths of getting the computed display name are covered.
[TestClass]
public class TestContextDisplayName
{
    public TestContext TestContext { get; set; }

    [TestMethod("Custom name")]
    public void TestCustomName()
    {
        Assert.AreEqual("Custom name", TestContext.TestDisplayName);
    }

    [TestMethod]
    public void TestMethod()
    {
        Assert.AreEqual("TestMethod", TestContext.TestDisplayName);
    }

    [TestMethod("Custom name")]
    [DataRow(42)]
    public void TestCustomNameDataRow(int i)
    {
        Assert.AreEqual("Custom name (42)", TestContext.TestDisplayName);
    }

    [TestMethod("Custom name")]
    [DynamicData(nameof(Data))]
    public void TestCustomNameDynamicData(bool b)
    {
        Assert.AreEqual("Custom name (True)", TestContext.TestDisplayName);
    }

    public static IEnumerable<object[]> Data
    {
        get
        {
            yield return new object[] { true };
        }
    }
}

[TestClass]
public abstract class TestContextTestPropertyBase
{
    private readonly TestContext _testContext;

    protected TestContextTestPropertyBase(TestContext testContext)
        => _testContext = testContext;

    [TestMethod]
    public void TestMethod()
    {
        var value = (string)_testContext.Properties["MyProp"];
        Assert.AreEqual("MyValue", value);
    }
}

[TestClass]
[TestProperty("MyProp", "MyValue")]
public class TestContextTestPropertyImpl : TestContextTestPropertyBase
{
    public TestContextTestPropertyImpl(TestContext testContext) : base(testContext)
    {
    }
}
""";
    }
}
