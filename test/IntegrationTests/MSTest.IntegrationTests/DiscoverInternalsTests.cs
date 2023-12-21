// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.MSTestV2.CLIAutomation;

namespace MSTest.IntegrationTests;

public class DiscoverInternalsTests : CLITestBase
{
    private const string TestAsset = "DiscoverInternalsProject";

    public void InternalTestClassesAreDiscoveredWhenTheDiscoverInternalsAttributeIsPresent()
    {
        // Arrange
        var assemblyPath = Path.IsPathRooted(TestAsset) ? TestAsset : GetAssetFullPath(TestAsset);

        // Act
        var testCases = DiscoverTests(assemblyPath);
        _ = RunTests(testCases);

        // Assert
        VerifyE2E.AtLeastTestsDiscovered(
            testCases,
            "TopLevelInternalClass_TestMethod1",
            "NestedInternalClass_TestMethod1");
    }

    public void AnInternalTestClassDerivedFromAPublicAbstractGenericBaseClassForAnInternalTypeIsDiscovered()
    {
        // Arrange
        var assemblyPath = Path.IsPathRooted(TestAsset) ? TestAsset : GetAssetFullPath(TestAsset);

        // Act
        var testCases = DiscoverTests(assemblyPath);

        // Assert
        VerifyE2E.AtLeastTestsDiscovered(
            testCases,
            "EqualityIsCaseInsensitive");
    }

    public void AnInternalTypeCanBeUsedInADynamicDataTestMethod()
    {
        var assemblyPath = Path.IsPathRooted(TestAsset) ? TestAsset : GetAssetFullPath(TestAsset);

        // Act
        var testCases = DiscoverTests(assemblyPath);

        var targetTestCases = testCases.Where(t => t.DisplayName == "DynamicDataTestMethod (DiscoverInternalsProject.SerializableInternalType)");

        var testResults = RunTests(targetTestCases);

        // Assert
        VerifyE2E.TestsPassed(
            testResults,
            "DynamicDataTestMethod (DiscoverInternalsProject.SerializableInternalType)");
    }
}
