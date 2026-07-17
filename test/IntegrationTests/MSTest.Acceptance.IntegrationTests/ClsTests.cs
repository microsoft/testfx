// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Acceptance.IntegrationTests;
using Microsoft.Testing.TestInfrastructure;

using static MSTest.Acceptance.IntegrationTests.AdapterTestHost;

namespace MSTest.Acceptance.IntegrationTests;

[TestClass]
[OSCondition(OperatingSystems.Windows)]
public sealed class ClsTests : AcceptanceTestBase<ClsTests.TestAssetFixture>
{
    private const string TestAssetName = "ClsTestProject";

    // This test in itself is not so important. What matters is that the asset gets build. If we regress and start having
    // the [DataRow] attribute no longer CLS compliant, the build will raise a warning in VS (and the build will fail in CI).
    [TestMethod]
    public async Task TestsAreRun()
    {
        // Arrange
        string assemblyPath = AssetFixture.AssemblyPath;

        // Act
        System.Collections.Immutable.ImmutableArray<Microsoft.VisualStudio.TestPlatform.ObjectModel.TestCase> testCases = DiscoverTests(assemblyPath);
        System.Collections.Immutable.ImmutableArray<Microsoft.VisualStudio.TestPlatform.ObjectModel.TestResult> testResults = await RunTestsAsync(testCases);

        // Assert
        VerifyE2E.TestsPassed(
            testResults,
            "TestMethod",
            "IntDataRow (10)",
            "StringDataRow (\"some string\")",
            "StringDataRow2 (\"some string\")",
            "StringDataRow2 (\"some other string\")");
    }

    public sealed class TestAssetFixture : GeneratedAssetFixture
    {
        protected override string ProjectName => TestAssetName;

        protected override string SourceFiles => GeneratedAssetSource.Cls;
    }
}
