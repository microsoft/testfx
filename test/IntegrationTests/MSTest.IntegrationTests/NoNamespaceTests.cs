// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using FluentAssertions;

using Microsoft.MSTestV2.CLIAutomation;

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Extensions;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;

namespace MSTest.IntegrationTests;

public class NoNamespaceTests : CLITestBase
{
    private const string TestAssetName = "HierarchyProject";

    public void TestsAreDiscoveredWithExpectedHierarchy()
    {
        // Arrange & Act
        System.Collections.Immutable.ImmutableArray<TestCase> testCases = DiscoverTests(GetAssetFullPath(TestAssetName));

        // Assert
        testCases.Should().HaveCount(2);

        VerifyHierarchy(testCases[0], null, "ClassWithNoNamespace", "MyMethodUnderTest");
        VerifyHierarchy(testCases[1], "SomeNamespace.WithMultipleLevels", "ClassWithNamespace", "MyMethodUnderTest");
    }

    private static void VerifyHierarchy(TestCase testCase, string expectedNamespace, string expectedClassName, string expectedMethodName)
    {
        string[] hierarchy = testCase.GetPropertyValue(TestCaseExtensions.HierarchyProperty) as string[];
        hierarchy.Should().HaveCount(4);

        // This level is always null.
        hierarchy[0].Should().BeNull();
        hierarchy[1].Should().Be(expectedNamespace);
        hierarchy[2].Should().Be(expectedClassName);
        hierarchy[3].Should().Be(expectedMethodName);
    }
}
