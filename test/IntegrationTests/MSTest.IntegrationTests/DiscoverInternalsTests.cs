// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.MSTestV2.CLIAutomation;

namespace MSTest.IntegrationTests;

[TestClass]
public class DiscoverInternalsTests : CLITestBase
{
    private const string TestAsset = "DiscoverInternalsProject";

    [TestMethod]
    public async Task InternalTestClassesAreDiscoveredWhenTheDiscoverInternalsAttributeIsPresent()
    {
        // Arrange
        string assemblyPath = Path.IsPathRooted(TestAsset) ? TestAsset : GetAssetFullPath(TestAsset);

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
        string assemblyPath = Path.IsPathRooted(TestAsset) ? TestAsset : GetAssetFullPath(TestAsset);

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
        string assemblyPath = Path.IsPathRooted(TestAsset) ? TestAsset : GetAssetFullPath(TestAsset);

        // Act
        System.Collections.Immutable.ImmutableArray<Microsoft.VisualStudio.TestPlatform.ObjectModel.TestCase> testCases = DiscoverTests(assemblyPath);

        IEnumerable<Microsoft.VisualStudio.TestPlatform.ObjectModel.TestCase> targetTestCases = testCases.Where(t => t.DisplayName == "DynamicDataTestMethod (DiscoverInternalsProject.SerializableInternalType)");

        System.Collections.Immutable.ImmutableArray<Microsoft.VisualStudio.TestPlatform.ObjectModel.TestResult> testResults = await RunTestsAsync(targetTestCases);

        // Assert
        VerifyE2E.TestsPassed(
            testResults,
            "DynamicDataTestMethod (DiscoverInternalsProject.SerializableInternalType)");
    }
}
