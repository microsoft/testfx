// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;

using Microsoft.MSTestV2.CLIAutomation;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;

using TestResult = Microsoft.VisualStudio.TestPlatform.ObjectModel.TestResult;

namespace MSTest.IntegrationTests;

/// <summary>
/// Regression guard for the platform-agnostic filtering refactor (neutral <c>ITestElementFilter</c>).
/// Filtering is now expressed over the neutral <c>UnitTestElement</c>: discovery matches
/// <c>element.ToTestCase()</c>, and execution matches
/// <c>incomingTestCase.ToUnitTestElementWithUpdatedSource(source).ToTestCase()</c>. These tests lock the
/// end-to-end discover-then-run behavior for <c>FullyQualifiedName</c> and <c>Id</c> filters — the two
/// properties most sensitive to the <c>TestCase</c> ↔ <c>UnitTestElement</c> round-trip that the refactor
/// re-routes — so a future change cannot silently break <c>--filter</c>.
/// </summary>
[TestClass]
public class TestCaseFilteringTests : CLITestBase
{
    private const string TestAsset = "DiscoverInternalsProject";

    // A non-parameterized test with a stable, filter-safe fully qualified name (no filter operator chars).
    private const string TargetDisplayName = "TopLevelInternalClass_TestMethod1";

    [TestMethod]
    public void FilterByFullyQualifiedNameSelectsExactlyTheMatchingTestDuringDiscovery()
    {
        string assemblyPath = GetAssetFullPath(TestAsset);
        TestCase target = GetTargetTestCase(assemblyPath);

        ImmutableArray<TestCase> filtered = DiscoverTests(assemblyPath, $"FullyQualifiedName={target.FullyQualifiedName}");

        Assert.HasCount(1, filtered);
        Assert.AreEqual(target.FullyQualifiedName, filtered[0].FullyQualifiedName);
        Assert.AreEqual(target.Id, filtered[0].Id);
    }

    [TestMethod]
    public void FilterByIdSelectsExactlyTheMatchingTestDuringDiscovery()
    {
        string assemblyPath = GetAssetFullPath(TestAsset);
        TestCase target = GetTargetTestCase(assemblyPath);

        ImmutableArray<TestCase> filtered = DiscoverTests(assemblyPath, $"Id={target.Id}");

        Assert.HasCount(1, filtered);
        Assert.AreEqual(target.FullyQualifiedName, filtered[0].FullyQualifiedName);
        Assert.AreEqual(target.Id, filtered[0].Id);
    }

    [TestMethod]
    public async Task FilterByFullyQualifiedNameSelectsExactlyTheMatchingTestDuringExecution()
    {
        string assemblyPath = GetAssetFullPath(TestAsset);
        ImmutableArray<TestCase> allTests = DiscoverTests(assemblyPath);
        TestCase target = allTests.First(t => t.DisplayName == TargetDisplayName);

        SimulateCrossProcessTestCases(allTests);

        ImmutableArray<TestResult> results = await RunTestsAsync(allTests, $"FullyQualifiedName={target.FullyQualifiedName}");

        // HasCount(1) is the load-bearing assertion: it fails if the filter is ignored (all tests run) or if
        // the FQN no longer matches after reconstruction (no test runs). The outcome check proves the target
        // was actually executed via the reconstructed element, not merely selected.
        Assert.HasCount(1, results);
        Assert.AreEqual(target.FullyQualifiedName, results[0].TestCase.FullyQualifiedName);
        Assert.AreEqual(TestOutcome.Passed, results[0].Outcome);
    }

    [TestMethod]
    public async Task FilterByIdSelectsExactlyTheMatchingTestDuringExecution()
    {
        string assemblyPath = GetAssetFullPath(TestAsset);
        ImmutableArray<TestCase> allTests = DiscoverTests(assemblyPath);
        Guid targetId = allTests.First(t => t.DisplayName == TargetDisplayName).Id;

        SimulateCrossProcessTestCases(allTests);

        ImmutableArray<TestResult> results = await RunTestsAsync(allTests, $"Id={targetId}");

        // HasCount(1) is the load-bearing assertion: filtering by Id after the TestCase -> UnitTestElement ->
        // TestCase reconstruction must still recompute the same Id and select exactly the target. The outcome
        // check proves the target was actually executed via the reconstructed element.
        Assert.HasCount(1, results);
        Assert.AreEqual(targetId, results[0].TestCase.Id);
        Assert.AreEqual(TestOutcome.Passed, results[0].Outcome);
    }

    private static TestCase GetTargetTestCase(string assemblyPath)
        => DiscoverTests(assemblyPath).First(t => t.DisplayName == TargetDisplayName);

    // Clears LocalExtensionData so the execution pipeline rebuilds the UnitTestElement (and its regenerated
    // TestCase/Id) from the test-case properties, mirroring the out-of-proc VSTest path where deserialized
    // test cases carry no LocalExtensionData. This is the round-trip the filtering refactor must preserve.
    private static void SimulateCrossProcessTestCases(ImmutableArray<TestCase> testCases)
    {
        foreach (TestCase testCase in testCases)
        {
            testCase.LocalExtensionData = null;
        }
    }
}
