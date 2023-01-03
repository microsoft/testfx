// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.IO;
using System.Linq;
using System.Threading.Tasks;

using Microsoft.MSTestV2.CLIAutomation;

namespace Microsoft.MSTestV2.Smoke.DiscoveryAndExecutionTests;
public class DiscoverInternalsTests : CLITestBase
{
    private const string TestAssembly = "DiscoverInternalsProject.dll";

    public async Task InternalTestClassesAreDiscoveredWhenTheDiscoverInternalsAttributeIsPresent()
    {
        // Arrange
        var assemblyPath = Path.IsPathRooted(TestAssembly) ? TestAssembly : GetAssetFullPath(TestAssembly);

        // Act
        var testCases = DiscoverTests(assemblyPath);
        var testResults = await RunTests(testCases);

        // Assert
        VerifyE2E.AtLeastTestsDiscovered(
            testCases,
            "TopLevelInternalClass_TestMethod1",
            "NestedInternalClass_TestMethod1");
    }

    public void AnInternalTestClassDerivedFromAPublicAbstractGenericBaseClassForAnInternalTypeIsDiscovered()
    {
        // Arrange
        var assemblyPath = Path.IsPathRooted(TestAssembly) ? TestAssembly : GetAssetFullPath(TestAssembly);

        // Act
        var testCases = DiscoverTests(assemblyPath);

        // Assert
        VerifyE2E.AtLeastTestsDiscovered(
            testCases,
            "EqualityIsCaseInsensitive");
    }

    public async Task AnInternalTypeCanBeUsedInADynamicDataTestMethod()
    {
        var assemblyPath = Path.IsPathRooted(TestAssembly) ? TestAssembly : GetAssetFullPath(TestAssembly);

        // Act
        var testCases = DiscoverTests(assemblyPath);

        var targetTestCases = testCases.Where(t => t.DisplayName == "DynamicDataTestMethod (DiscoverInternalsProject.SerializableInternalType)");

        var testResults = await RunTests(targetTestCases);

        // Assert
        VerifyE2E.TestsPassed(
            testResults,
            "DynamicDataTestMethod (DiscoverInternalsProject.SerializableInternalType)");
    }
}
