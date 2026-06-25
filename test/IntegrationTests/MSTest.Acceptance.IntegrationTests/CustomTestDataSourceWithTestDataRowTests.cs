// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Acceptance.IntegrationTests;
using Microsoft.Testing.Platform.Acceptance.IntegrationTests.Helpers;
using Microsoft.Testing.Platform.Helpers;

namespace MSTest.Acceptance.IntegrationTests;

[TestClass]
public sealed class CustomTestDataSourceWithTestDataRowTests : AcceptanceTestBase<CustomTestDataSourceWithTestDataRowTests.TestAssetFixture>
{
    private const string AssetName = "CustomTestDataSourceWithTestDataRow";

    [TestMethod]
    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    public async Task CustomDataSourceUsingTestDataRow_AppliesPerRowMetadata(string currentTfm)
    {
        var testHost = TestHost.LocateFrom(AssetFixture.GetAssetPath(AssetName), AssetName, currentTfm);

        TestHostResult testHostResult = await testHost.ExecuteAsync("--output Detailed", cancellationToken: TestContext.CancellationToken);

        // One row passes with a custom display name, one row is ignored (skipped), and one row passes with a custom category.
        testHostResult.AssertExitCodeIs(ExitCode.Success);
        testHostResult.AssertOutputContainsSummary(failed: 0, passed: 2, skipped: 1);

        // The DisplayName from TestDataRow<T> overrides the default display name.
        testHostResult.AssertOutputContains("passed CustomDisplayNameRow");
        testHostResult.AssertOutputContains("passed CustomCategoryRow");

        // The IgnoreMessage from TestDataRow<T> causes that single row to be skipped, and it must NOT leak to other rows.
        testHostResult.AssertOutputContains("skipped IgnoredRow");
    }

    [TestMethod]
    [DynamicData(nameof(TargetFrameworks.AllForDynamicData), typeof(TargetFrameworks))]
    public async Task CustomDataSourceUsingTestDataRow_TestCategoryIsVisibleToFilter(string currentTfm)
    {
        var testHost = TestHost.LocateFrom(AssetFixture.GetAssetPath(AssetName), AssetName, currentTfm);

        TestHostResult testHostResult = await testHost.ExecuteAsync("--output Detailed --filter TestCategory=custom-category", cancellationToken: TestContext.CancellationToken);

        // Only the row carrying the custom category from TestDataRow<T> should run.
        testHostResult.AssertExitCodeIs(ExitCode.Success);
        testHostResult.AssertOutputContainsSummary(failed: 0, passed: 1, skipped: 0);
        testHostResult.AssertOutputContains("passed CustomCategoryRow");
        testHostResult.AssertOutputDoesNotContain("CustomDisplayNameRow");
        testHostResult.AssertOutputDoesNotContain("IgnoredRow");
    }

    public sealed class TestAssetFixture() : TestAssetFixtureBase()
    {
        public override (string ID, string Name, string Code) GetAssetsToGenerate() => (AssetName, AssetName,
                SourceCode
                .PatchTargetFrameworks(TargetFrameworks.All)
                .PatchCodeWithReplace("$MSTestVersion$", MSTestVersion));

        private const string SourceCode = """
#file CustomTestDataSourceWithTestDataRow.csproj
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

using System;
using System.Collections.Generic;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
public class TestClass
{
    [TestMethod]
    [CustomTestDataSource]
    public void Test(int a, int b, int c)
    {
    }
}

[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
public class CustomTestDataSourceAttribute : Attribute, ITestDataSource
{
    public IEnumerable<object[]> GetData(MethodInfo methodInfo) =>
    [
        [new TestDataRow<(int, int, int)>((1, 2, 3)) { DisplayName = "CustomDisplayNameRow" }],
        [new TestDataRow<(int, int, int)>((4, 5, 6)) { DisplayName = "IgnoredRow", IgnoreMessage = "Not ready yet" }],
        [new TestDataRow<(int, int, int)>((7, 8, 9)) { DisplayName = "CustomCategoryRow", TestCategories = ["custom-category"] }],
    ];

    public string GetDisplayName(MethodInfo methodInfo, object[] data) => null;
}
""";
    }

    public TestContext TestContext { get; set; }
}
