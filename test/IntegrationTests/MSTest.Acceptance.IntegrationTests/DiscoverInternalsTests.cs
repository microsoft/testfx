// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Acceptance.IntegrationTests;
using Microsoft.Testing.TestInfrastructure;

using static MSTest.Acceptance.IntegrationTests.AdapterTestHost;

namespace MSTest.Acceptance.IntegrationTests;

[TestClass]
[OSCondition(OperatingSystems.Windows)]
public sealed class DiscoverInternalsTests : AcceptanceTestBase<DiscoverInternalsTests.TestAssetFixture>
{
    private const string TestAsset = "DiscoverInternalsProject";

    [TestMethod]
    public async Task InternalTestClassesAreDiscoveredWhenTheDiscoverInternalsAttributeIsPresent()
    {
        // Arrange
        string assemblyPath = AssetFixture.AssemblyPath;

        // Act
        System.Collections.Immutable.ImmutableArray<Microsoft.VisualStudio.TestPlatform.ObjectModel.TestCase> testCases = DiscoverTests(assemblyPath);
        _ = await RunTestsAsync(testCases);

        // Assert
        VerifyE2E.AtLeastTestsDiscovered(
            testCases,
            "TopLevelInternalClass_TestMethod1",
            "NestedInternalClass_TestMethod1");
    }

    [TestMethod]
    public void AnInternalTestClassDerivedFromAPublicAbstractGenericBaseClassForAnInternalTypeIsDiscovered()
    {
        // Arrange
        string assemblyPath = AssetFixture.AssemblyPath;

        // Act
        System.Collections.Immutable.ImmutableArray<Microsoft.VisualStudio.TestPlatform.ObjectModel.TestCase> testCases = DiscoverTests(assemblyPath);

        // Assert
        VerifyE2E.AtLeastTestsDiscovered(
            testCases,
            "EqualityIsCaseInsensitive");
    }

    [TestMethod]
    public async Task AnInternalTypeCanBeUsedInADynamicDataTestMethod()
    {
        string assemblyPath = AssetFixture.AssemblyPath;

        // Act
        System.Collections.Immutable.ImmutableArray<Microsoft.VisualStudio.TestPlatform.ObjectModel.TestCase> testCases = DiscoverTests(assemblyPath);

        IEnumerable<Microsoft.VisualStudio.TestPlatform.ObjectModel.TestCase> targetTestCases = testCases.Where(t => t.DisplayName == "DynamicDataTestMethod (DiscoverInternalsProject.SerializableInternalType)");

        System.Collections.Immutable.ImmutableArray<Microsoft.VisualStudio.TestPlatform.ObjectModel.TestResult> testResults = await RunTestsAsync(targetTestCases);

        // Assert
        VerifyE2E.TestsPassed(
            testResults,
            "DynamicDataTestMethod (DiscoverInternalsProject.SerializableInternalType)");
    }

    public sealed class TestAssetFixture : GeneratedAssetFixture
    {
        protected override string ProjectName => TestAsset;

        protected override string SourceFiles => GeneratedAssetSource.DiscoverInternals;
    }
}
