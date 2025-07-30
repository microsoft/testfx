// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.VisualStudio.TestPlatform.ObjectModel;

using TestResult = Microsoft.VisualStudio.TestPlatform.ObjectModel.TestResult;

namespace MSTest.IntegrationTests;

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

    public static void TestsPassed(IEnumerable<TestResult> actual, params string[] expectedTests)
        => ContainsExpectedTestsWithOutcome(actual, TestOutcome.Passed, expectedTests, true);

    public static void TestsFailed(IEnumerable<TestResult> actual, params string[] expectedTests)
        => ContainsExpectedTestsWithOutcome(actual, TestOutcome.Failed, expectedTests, true);

    public static void ContainsTestsPassed(IEnumerable<TestResult> actual, params string[] expectedTests)
        => ContainsExpectedTestsWithOutcome(actual, TestOutcome.Passed, expectedTests);

    public static void ContainsTestsFailed(IEnumerable<TestResult> actual, params string[] expectedTests)
        => ContainsExpectedTestsWithOutcome(actual, TestOutcome.Failed, expectedTests);

    private static void ContainsTestsDiscovered(IEnumerable<TestCase> discoveredTests, IEnumerable<string> expectedTests, bool matchCount = false)
    {
        if (matchCount)
        {
            Assert.AreEqual(expectedTests.Count(), discoveredTests.Count());
        }

        foreach (string test in expectedTests)
        {
            // Test Discovery run was expecting to discover \"{test}\", but it has not discovered.
            Assert.Contains(
                p => test.Equals(p.FullyQualifiedName, StringComparison.Ordinal)
                     || test.Equals(p.DisplayName, StringComparison.Ordinal)
                     || test.Equals(p.DisplayName, StringComparison.Ordinal),
                discoveredTests);
        }
    }

    private static void ContainsExpectedTestsWithOutcome(IEnumerable<TestResult> tests, TestOutcome expectedOutcome,
        string[] expectedTests, bool matchCount = false)
    {
        if (matchCount)
        {
            int expectedCount = expectedTests.Length;
            AssertOutcomeCount(tests, expectedOutcome, expectedCount);
        }

        foreach (string test in expectedTests)
        {
            Assert.Contains(p => p.DisplayName == test, tests);
        }
    }

    private static void AssertOutcomeCount(IEnumerable<TestResult> actual, TestOutcome expectedOutcome, int expectedCount)
        => Assert.HasCount(expectedCount, actual.Where(i => i.Outcome == expectedOutcome));
}
