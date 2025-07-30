// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.MSTestV2.CLIAutomation;

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Extensions;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;

namespace MSTest.IntegrationTests;

[TestClass]
public class NoNamespaceTests : CLITestBase
{
    private const string TestAssetName = "HierarchyProject";

    [TestMethod]
    public void TestsAreDiscoveredWithExpectedHierarchy()
    {
        // Arrange & Act
        System.Collections.Immutable.ImmutableArray<TestCase> testCases = DiscoverTests(GetAssetFullPath(TestAssetName));

        // Assert
        Assert.HasCount(2, testCases, "Should discover exactly 2 tests with hierarchy information");

        VerifyHierarchy(testCases[0], null!, "ClassWithNoNamespace", "MyMethodUnderTest");
        VerifyHierarchy(testCases[1], "SomeNamespace.WithMultipleLevels", "ClassWithNamespace", "MyMethodUnderTest");
    }

    private static void VerifyHierarchy(TestCase testCase, string expectedNamespace, string expectedClassName, string expectedMethodName)
    {
        string[]? hierarchy = testCase.GetPropertyValue(TestCaseExtensions.HierarchyProperty) as string[];
        Assert.IsNotNull(hierarchy);
        Assert.HasCount(4, hierarchy);

        // This level is always null.
        Assert.IsNull(hierarchy[0]);
        Assert.AreEqual(expectedNamespace, hierarchy[1]);
        Assert.AreEqual(expectedClassName, hierarchy[2]);
        Assert.AreEqual(expectedMethodName, hierarchy[3]);
    }
}
