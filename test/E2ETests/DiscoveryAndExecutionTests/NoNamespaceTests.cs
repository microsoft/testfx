// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.MSTestV2.CLIAutomation;

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Extensions;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;

namespace Microsoft.MSTestV2.Smoke.DiscoveryAndExecutionTests;
public class NoNamespaceTests : CLITestBase
{
    private const string TestAssembly = "HierarchyProject.dll";

    public void TestsAreDiscoveredWithExpectedHierarchy()
    {
        // Arrange & Act
        var testCases = DiscoverTests(GetAssetFullPath(TestAssembly));

        // Assert
        Verify(testCases.Count == 2);

        VerifyHierarchy(testCases[0], null, "ClassWithNoNamespace", "MyMethodUnderTest");
        VerifyHierarchy(testCases[1], "SomeNamespace.WithMultipleLevels", "ClassWithNamespace", "MyMethodUnderTest");
    }

    private static void VerifyHierarchy(TestCase testCase, string expectedNamespace, string expectedClassName, string expectedMethodName)
    {
        var hierarchy = testCase.GetPropertyValue(TestCaseExtensions.HierarchyProperty) as string[];
        Verify(hierarchy?.Length == 4);

        // This level is always null.
        Verify(hierarchy[0] == null);
        Verify(hierarchy[1] == expectedNamespace);
        Verify(hierarchy[2] == expectedClassName);
        Verify(hierarchy[3] == expectedMethodName);
    }
}
