// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Acceptance.IntegrationTests;
using Microsoft.Testing.TestInfrastructure;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;

using static MSTest.Acceptance.IntegrationTests.AdapterTestHost;

namespace MSTest.Acceptance.IntegrationTests;

[TestClass]
[OSCondition(OperatingSystems.Windows)]
public sealed class NoNamespaceTests : AcceptanceTestBase<NoNamespaceTests.TestAssetFixture>
{
    private const string TestAssetName = "HierarchyProject";

    [TestMethod]
    public void TestsAreDiscoveredWithExpectedHierarchy()
    {
        // Arrange & Act
        System.Collections.Immutable.ImmutableArray<TestCase> testCases = DiscoverTests(AssetFixture.AssemblyPath);

        // Assert
        Assert.HasCount(2, testCases, "Should discover exactly 2 tests with hierarchy information");

        VerifyHierarchy(testCases[0], null!, "ClassWithNoNamespace", "MyMethodUnderTest");
        VerifyHierarchy(testCases[1], "SomeNamespace.WithMultipleLevels", "ClassWithNamespace", "MyMethodUnderTest");
    }

    private static void VerifyHierarchy(TestCase testCase, string expectedNamespace, string expectedClassName, string expectedMethodName)
    {
        string[]? hierarchy = testCase.GetProperties()
            .Single(property => property.Key.Id == "TestCase.Hierarchy")
            .Value as string[];
        Assert.IsNotNull(hierarchy);
        Assert.HasCount(4, hierarchy);

        // This level is always null.
        Assert.IsNull(hierarchy[0]);
        Assert.AreEqual(expectedNamespace, hierarchy[1]);
        Assert.AreEqual(expectedClassName, hierarchy[2]);
        Assert.AreEqual(expectedMethodName, hierarchy[3]);
    }

    public sealed class TestAssetFixture : GeneratedAssetFixture
    {
        protected override string ProjectName => TestAssetName;

        protected override string SourceFiles => GeneratedAssetSource.Hierarchy;
    }
}
