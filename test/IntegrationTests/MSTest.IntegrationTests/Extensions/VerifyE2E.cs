// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using FluentAssertions;

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;

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

    public static void TestsPassed(IEnumerable<TestResult> actual, IEnumerable<TestCase> testCases, IEnumerable<string> expectedTests, MSTestSettings settings = null)
        => ContainsExpectedTestsWithOutcome(actual, TestOutcome.Passed, expectedTests, true);

    public static void TestsPassed(IEnumerable<TestResult> actual, params string[] expectedTests)
        => ContainsExpectedTestsWithOutcome(actual, TestOutcome.Passed, expectedTests, true);

    public static void TestsFailed(IEnumerable<TestResult> actual, IEnumerable<TestCase> testCases, IEnumerable<string> expectedTests, MSTestSettings settings = null)
      => ContainsExpectedTestsWithOutcome(actual, TestOutcome.Failed, expectedTests, true);

    public static void TestsFailed(IEnumerable<TestResult> actual, params string[] expectedTests)
        => ContainsExpectedTestsWithOutcome(actual, TestOutcome.Failed, expectedTests, true);

    public static void ContainsTestsPassed(IEnumerable<TestResult> actual, IEnumerable<TestCase> testCases, IEnumerable<string> expectedTests, MSTestSettings settings = null)
        => ContainsExpectedTestsWithOutcome(actual, TestOutcome.Passed, expectedTests);

    public static void ContainsTestsPassed(IEnumerable<TestResult> actual, params string[] expectedTests)
        => ContainsExpectedTestsWithOutcome(actual, TestOutcome.Passed, expectedTests);

    public static void ContainsTestsFailed(IEnumerable<TestResult> actual, IEnumerable<TestCase> testCases, IEnumerable<string> expectedTests, MSTestSettings settings = null)
        => ContainsExpectedTestsWithOutcome(actual, TestOutcome.Failed, expectedTests);

    public static void ContainsTestsFailed(IEnumerable<TestResult> actual, params string[] expectedTests)
        => ContainsExpectedTestsWithOutcome(actual, TestOutcome.Failed, expectedTests);

    private static void ContainsTestsDiscovered(IEnumerable<TestCase> discoveredTests, IEnumerable<string> expectedTests, bool matchCount = false)
    {
        if (matchCount)
        {
            discoveredTests.Should().HaveSameCount(expectedTests);
        }

        foreach (string test in expectedTests)
        {
            // Test Discovery run was expecting to discover \"{test}\", but it has not discovered.
            discoveredTests.Should().Contain(
                p => test.Equals(p.FullyQualifiedName, StringComparison.Ordinal)
                     || test.Equals(p.DisplayName, StringComparison.Ordinal)
                     || test.Equals(p.DisplayName, StringComparison.Ordinal));
        }
    }

    private static void ContainsExpectedTestsWithOutcome(IEnumerable<TestResult> tests, TestOutcome expectedOutcome,
        IEnumerable<string> expectedTests, bool matchCount = false)
    {
        if (matchCount)
        {
            int expectedCount = expectedTests.Count();
            AssertOutcomeCount(tests, expectedOutcome, expectedCount);
        }

        foreach (string test in expectedTests)
        {
            tests.Should().Contain(
                p => test.Equals(p.TestCase.FullyQualifiedName, StringComparison.Ordinal)
                     || test.Equals(p.DisplayName, StringComparison.Ordinal)
                     || test.Equals(p.TestCase.DisplayName, StringComparison.Ordinal));
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
            tests.Should().Contain(p => p.DisplayName == test);
        }
    }

    private static void AssertOutcomeCount(IEnumerable<TestResult> actual, TestOutcome expectedOutcome, int expectedCount) => actual.Where(i => i.Outcome == expectedOutcome).Should().HaveCount(expectedCount);
}
