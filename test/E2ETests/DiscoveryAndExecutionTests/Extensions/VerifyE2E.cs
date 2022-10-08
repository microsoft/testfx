// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;
using System.Linq;

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;

using TestContainer = TestFramework.ForTestingMSTest.TestContainer;

namespace Microsoft.MSTestV2.Smoke.DiscoveryAndExecutionTests;
public static class VerifyE2E
{
    public static void ContainsTestsDiscovered(IEnumerable<TestCase> actualTests, IEnumerable<string> expectedTests)
        => ContainsTestsDiscovered(actualTests, expectedTests);

    public static void PassedTestCount(IEnumerable<TestResult> actual, int expectedCount)
        => AssertOutcomeCount(actual, TestOutcome.Passed, expectedCount);

    public static void FailedTestCount(IEnumerable<TestResult> actual, int expectedCount)
        => AssertOutcomeCount(actual, TestOutcome.Failed, expectedCount);

    public static void TestsDiscovered(IEnumerable<TestCase> actualTests, IEnumerable<string> expectedTests)
        => ContainsTestsDiscovered(actualTests, expectedTests, true);

    public static void TestsDiscovered(IEnumerable<TestCase> actualTests, params string[] expectedTests)
        => ContainsTestsDiscovered(actualTests, expectedTests, true);

    public static void AtLeastTestsDiscovered(IEnumerable<TestCase> actualTests, params string[] expectedTests)
        => ContainsTestsDiscovered(actualTests, expectedTests, false);

    public static void TestsPassed(IEnumerable<TestResult> actual, IEnumerable<TestCase> testCases, IEnumerable<string> expectedTests, MSTestSettings settings = null)
        => ContainsExpectedTestsWithOutcome(actual, testCases, TestOutcome.Passed, expectedTests, true, settings);

    public static void TestsPassed(IEnumerable<TestResult> actual, params string[] expectedTests)
        => ContainsExpectedTestsWithOutcome(actual, TestOutcome.Passed, expectedTests, true);

    public static void TestsFailed(IEnumerable<TestResult> actual, IEnumerable<TestCase> testCases, IEnumerable<string> expectedTests, MSTestSettings settings = null)
      => ContainsExpectedTestsWithOutcome(actual, testCases, TestOutcome.Failed, expectedTests, true, settings);

    public static void TestsFailed(IEnumerable<TestResult> actual, params string[] expectedTests)
        => ContainsExpectedTestsWithOutcome(actual, TestOutcome.Failed, expectedTests, true);

    public static void ContainsTestsPassed(IEnumerable<TestResult> actual, IEnumerable<TestCase> testCases, IEnumerable<string> expectedTests, MSTestSettings settings = null)
        => ContainsExpectedTestsWithOutcome(actual, testCases, TestOutcome.Passed, expectedTests, false, settings);

    public static void ContainsTestsPassed(IEnumerable<TestResult> actual, params string[] expectedTests)
        => ContainsExpectedTestsWithOutcome(actual, TestOutcome.Passed, expectedTests);

    public static void ContainsTestsFailed(IEnumerable<TestResult> actual, IEnumerable<TestCase> testCases, IEnumerable<string> expectedTests, MSTestSettings settings = null)
        => ContainsExpectedTestsWithOutcome(actual, testCases, TestOutcome.Failed, expectedTests, false, settings);

    public static void ContainsTestsFailed(IEnumerable<TestResult> actual, params string[] expectedTests)
        => ContainsExpectedTestsWithOutcome(actual, TestOutcome.Failed, expectedTests);

    private static void ContainsTestsDiscovered(IEnumerable<TestCase> discoveredTests, IEnumerable<string> expectedTests, bool matchCount = false)
    {
        if (matchCount)
        {
            var expectedCount = discoveredTests.Count();
            var outcomedCount = expectedTests.Count();

            AssertDiscoveryCount(outcomedCount, expectedCount);
        }

        foreach (var test in expectedTests)
        {
            var testFound = discoveredTests.Any(
                p => test.Equals(p.FullyQualifiedName)
                     || test.Equals(p.DisplayName)
                     || test.Equals(p.DisplayName));

            // Test Discovery run was expecting to discover \"{test}\", but it has not discovered.
            TestContainer.Verify(testFound);
        }
    }

    private static void ContainsExpectedTestsWithOutcome(IEnumerable<TestResult> outcomedTests, IEnumerable<TestCase> testCases, TestOutcome expectedOutcome, IEnumerable<string> expectedTests, bool matchCount = false, MSTestSettings settings = null)
    {
        if (matchCount)
        {
            var expectedCount = expectedTests.Count();
            AssertOutcomeCount(outcomedTests, expectedOutcome, expectedCount);
        }

        foreach (var test in expectedTests)
        {
            var testFound = outcomedTests.Any(
                p => test.Equals(p.TestCase?.FullyQualifiedName)
                     || test.Equals(p.DisplayName)
                     || test.Equals(p.TestCase.DisplayName));

            TestContainer.Verify(testFound);
        }
    }

    private static void ContainsExpectedTestsWithOutcome(IEnumerable<TestResult> outcomedTests, TestOutcome expectedOutcome, string[] expectedTests, bool matchCount = false)
    {
        if (matchCount)
        {
            var expectedCount = expectedTests.Length;
            AssertOutcomeCount(outcomedTests, expectedOutcome, expectedCount);
        }

        foreach (var test in expectedTests)
        {
            var testFound = outcomedTests.Any(p => p.DisplayName == test);

            TestContainer.Verify(testFound);
        }
    }

    private static string GetOutcomeAssertString(string testName, TestOutcome outcome)
    {
        return outcome switch
        {
            TestOutcome.None => $"\"{testName}\" does not have TestOutcome.None outcome.",
            TestOutcome.Passed => $"\"{testName}\" does not appear in passed tests list.",
            TestOutcome.Failed => $"\"{testName}\" does not appear in failed tests list.",
            TestOutcome.Skipped => $"\"{testName}\" does not appear in skipped tests list.",
            TestOutcome.NotFound => $"\"{testName}\" does not appear in not found tests list.",
            _ => string.Empty,
        };
    }

    private static void AssertOutcomeCount(IEnumerable<TestResult> actual, TestOutcome expectedOutcome, int expectedCount)
    {
        var outcomedTests = actual.Where(i => i.Outcome == expectedOutcome);
        var actualCount = outcomedTests.Count();

        AssertOutcomeCount(actualCount, expectedCount);
    }

    private static void AssertOutcomeCount(int actualCount, int expectedCount)
    {
        // Test run expected to contain {expectedCount} tests, but ran {actualCount}.
        TestContainer.Verify(expectedCount == actualCount);
    }

    private static void AssertDiscoveryCount(int actualCount, int expectedCount)
    {
        // Test discovery expected to contain {expectedCount} tests, but ran {actualCount}.
        TestContainer.Verify(expectedCount == actualCount);
    }
}
