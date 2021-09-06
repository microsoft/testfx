// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.MSTestV2.Smoke.DiscoveryAndExecutionTests
{
    using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter;
    using Microsoft.VisualStudio.TestPlatform.ObjectModel;

    using System.Collections.Generic;
    using System.Linq;

    using Assert = Microsoft.VisualStudio.TestTools.UnitTesting.Assert;

    public static class AssertionExtensions
    {
        public static void ContainsTestsDiscovered(this Assert _, IEnumerable<TestCase> actualTests, IEnumerable<string> expectedTests)
            => ContainsTestsDiscovered(actualTests, expectedTests);

        public static void PassedTestCount(this Assert _, IEnumerable<TestResult> actual, int expectedCount)
            => AssertOutcomeCount(actual, TestOutcome.Passed, expectedCount);

        public static void FailedTestCount(this Assert _, IEnumerable<TestResult> actual, int expectedCount)
            => AssertOutcomeCount(actual, TestOutcome.Failed, expectedCount);

        public static void TestsDiscovered(this Assert _, IEnumerable<TestCase> actualTests, IEnumerable<string> expectedTests)
            => ContainsTestsDiscovered(actualTests, expectedTests, true);

        public static void TestsDiscovered(this Assert _, IEnumerable<TestCase> actualTests, params string[] expectedTests)
            => ContainsTestsDiscovered(actualTests, expectedTests, true);

        public static void AtLeastTestsDiscovered(this Assert _, IEnumerable<TestCase> actualTests, params string[] expectedTests)
            => ContainsTestsDiscovered(actualTests, expectedTests, false);

        public static void TestsPassed(this Assert _, IEnumerable<TestResult> actual, IEnumerable<TestCase> testCases, IEnumerable<string> expectedTests, MSTestSettings settings = null)
            => ContainsExpectedTestsWithOutcome(actual, testCases, TestOutcome.Passed, expectedTests, true, settings);

        public static void TestsPassed(this Assert _, IEnumerable<TestResult> actual, params string[] expectedTests)
            => ContainsExpectedTestsWithOutcome(actual, TestOutcome.Passed, expectedTests, true);

        public static void TestsFailed(this Assert _, IEnumerable<TestResult> actual, IEnumerable<TestCase> testCases, IEnumerable<string> expectedTests, MSTestSettings settings = null)
          => ContainsExpectedTestsWithOutcome(actual, testCases, TestOutcome.Failed, expectedTests, true, settings);

        public static void TestsFailed(this Assert _, IEnumerable<TestResult> actual, params string[] expectedTests)
            => ContainsExpectedTestsWithOutcome(actual, TestOutcome.Failed, expectedTests, true);

        public static void ContainsTestsPassed(this Assert _, IEnumerable<TestResult> actual, IEnumerable<TestCase> testCases, IEnumerable<string> expectedTests, MSTestSettings settings = null)
            => ContainsExpectedTestsWithOutcome(actual, testCases, TestOutcome.Passed, expectedTests, false, settings);

        public static void ContainsTestsPassed(this Assert _, IEnumerable<TestResult> actual, params string[] expectedTests)
            => ContainsExpectedTestsWithOutcome(actual, TestOutcome.Passed, expectedTests);

        public static void ContainsTestsFailed(this Assert _, IEnumerable<TestResult> actual, IEnumerable<TestCase> testCases, IEnumerable<string> expectedTests, MSTestSettings settings = null)
            => ContainsExpectedTestsWithOutcome(actual, testCases, TestOutcome.Failed, expectedTests, false, settings);

        public static void ContainsTestsFailed(this Assert _, IEnumerable<TestResult> actual, params string[] expectedTests)
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

                Assert.IsTrue(testFound,
                    $"Test Discovery run was expecting to discover \"{test}\", but it has not discovered.");
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

                Assert.IsTrue(testFound, GetOutcomeAssertString(test, expectedOutcome));
            }
        }

        private static void ContainsExpectedTestsWithOutcome(IEnumerable<TestResult> outcomedTests, TestOutcome expectedOutcome, string[] expectedTests, bool matchCount = false)
        {
            if (matchCount)
            {
                var expectedCount = expectedTests.Count();
                AssertOutcomeCount(outcomedTests, expectedOutcome, expectedCount);
            }

            foreach (var test in expectedTests)
            {
                var testFound = outcomedTests.Any(p => p.DisplayName == test);

                Assert.IsTrue(testFound, GetOutcomeAssertString(test, expectedOutcome));
            }
        }

        private static string GetOutcomeAssertString(string testName, TestOutcome outcome)
        {
            switch (outcome)
            {
                case TestOutcome.None:
                    return $"\"{testName}\" does not have TestOutcome.None outcome.";

                case TestOutcome.Passed:
                    return $"\"{testName}\" does not appear in passed tests list.";

                case TestOutcome.Failed:
                    return $"\"{testName}\" does not appear in failed tests list.";

                case TestOutcome.Skipped:
                    return $"\"{testName}\" does not appear in skipped tests list.";

                case TestOutcome.NotFound:
                    return $"\"{testName}\" does not appear in not found tests list.";
            }

            return string.Empty;
        }

        private static void AssertOutcomeCount(IEnumerable<TestResult> actual, TestOutcome expectedOutcome, int expectedCount)
        {
            var outcomedTests = actual.Where(i => i.Outcome == expectedOutcome);
            var actualCount = outcomedTests.Count();

            AssertOutcomeCount(actualCount, expectedCount);
        }
        private static void AssertOutcomeCount(int actualCount, int expectedCount)
        {
            Assert.AreEqual(expectedCount, actualCount, $"Test run expected to contain {expectedCount} tests, but ran {actualCount}.");
        }
        private static void AssertDiscoveryCount(int actualCount, int expectedCount)
        {
            Assert.AreEqual(expectedCount, actualCount, $"Test discovery expected to contain {expectedCount} tests, but ran {actualCount}.");
        }
    }
}
