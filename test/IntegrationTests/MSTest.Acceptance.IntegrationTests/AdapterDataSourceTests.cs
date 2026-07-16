// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Acceptance.IntegrationTests;
using Microsoft.Testing.TestInfrastructure;

using static MSTest.Acceptance.IntegrationTests.AdapterTestHost;

namespace MSTest.Acceptance.IntegrationTests;

[TestClass]
[OSCondition(OperatingSystems.Windows)]
public sealed class AdapterDataSourceTests : AcceptanceTestBase<AdapterDataSourceTests.TestAssetFixture>
{
    private const string TestAssetName = "DataSourceTestProject";

    private static string GetAssetFullPath(string _) => AssetFixture.AssemblyPath;

#pragma warning disable IDE0051 // Remove unused private members
    [TestMethod]
    [Ignore("This test is ignored because it fails under CI. It will be fixed in a future PR.")]
    public async Task ExecuteCsvTestDataSourceTests()
#pragma warning restore IDE0051 // Remove unused private members
    {
        // Arrange
        string assemblyPath = GetAssetFullPath(TestAssetName);

        // Act
        System.Collections.Immutable.ImmutableArray<Microsoft.VisualStudio.TestPlatform.ObjectModel.TestCase> testCases = DiscoverTests(assemblyPath, "CsvTestMethod");
        System.Collections.Immutable.ImmutableArray<Microsoft.VisualStudio.TestPlatform.ObjectModel.TestResult> testResults = await RunTestsAsync(testCases);

        // Assert
        VerifyE2E.ContainsTestsPassed(
            testResults,
            "CsvTestMethod (Data Row 0)",
            "CsvTestMethod (Data Row 2)");
        VerifyE2E.ContainsTestsFailed(
            testResults,
            "CsvTestMethod (Data Row 1)",
            "CsvTestMethod (Data Row 3)");
    }

    public sealed class TestAssetFixture : GeneratedAssetFixture
    {
        protected override string ProjectName => TestAssetName;

        protected override string SourceFiles
            => GeneratedAssetSource.FromSharedDirectories(
                @"test\IntegrationTests\TestAssets\DataSourceTestProject");

        protected override string AdditionalProjectItems => """
            <ItemGroup>
              <None Update="a.csv">
                <CopyToOutputDirectory>Always</CopyToOutputDirectory>
              </None>
            </ItemGroup>
            """;
    }
}
