// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Linq;

namespace Microsoft.MSTestV2.Smoke.DiscoveryAndExecutionTests;

using System.IO;
using Microsoft.MSTestV2.CLIAutomation;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
public class DiscoverInternalsTests : CLITestBase
{
    private const string TestAssembly = "DiscoverInternalsProject.dll";

    [TestMethod]
    public void InternalTestClassesAreDiscoveredWhenTheDiscoverInternalsAttributeIsPresent()
    {
        // Arrange
        var assemblyPath = Path.IsPathRooted(TestAssembly) ? TestAssembly : GetAssetFullPath(TestAssembly);

        // Act
        var testCases = DiscoverTests(assemblyPath);
        var testResults = RunTests(assemblyPath, testCases);

        // Assert
        Assert.That.AtLeastTestsDiscovered(
            testCases,
            "TopLevelInternalClass_TestMethod1",
            "NestedInternalClass_TestMethod1"
        );
    }

    [TestMethod]
    public void AnInternalTestClassDerivedFromAPublicAbstractGenericBaseClassForAnInternalTypeIsDiscovered()
    {
        // Arrange
        var assemblyPath = Path.IsPathRooted(TestAssembly) ? TestAssembly : GetAssetFullPath(TestAssembly);

        // Act
        var testCases = DiscoverTests(assemblyPath);

        // Assert
        Assert.That.AtLeastTestsDiscovered(
            testCases,
            "EqualityIsCaseInsensitive"
        );
    }

    [TestMethod]
    public void AnInternalTypeCanBeUsedInADynamicDataTestMethod()
    {
        var assemblyPath = Path.IsPathRooted(TestAssembly) ? TestAssembly : GetAssetFullPath(TestAssembly);

        // Act
        var testCases = DiscoverTests(assemblyPath);

        var targetTestCases = testCases.Where(t => t.DisplayName == "DynamicDataTestMethod (DiscoverInternalsProject.SerializableInternalType)");

        var testResults = RunTests(assemblyPath, targetTestCases);

        // Assert
        Assert.That.TestsPassed(
            testResults,
            "DynamicDataTestMethod (DiscoverInternalsProject.SerializableInternalType)"
        );
    }
}
