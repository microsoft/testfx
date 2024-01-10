// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Globalization;

using FluentAssertions;

using Microsoft.MSTestV2.CLIAutomation;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;

namespace MSTest.IntegrationTests;

public class OutputTests : CLITestBase
{
    private const string TestAssetName = "OutputTestProject";

    public void OutputIsNotMixedWhenTestsRunInParallel()
    {
        ValidateOutputForClass("UnitTest1");
    }

    public void OutputIsNotMixedWhenAsyncTestsRunInParallel()
    {
        ValidateOutputForClass("UnitTest2");
    }

    private void ValidateOutputForClass(string className)
    {
        // LogMessageListener uses an implementation of a string writer that captures output per async context.
        // This allows us to capture output from tasks even when they are running in parallel.

        // Arrange
        var assemblyPath = GetAssetFullPath(TestAssetName);

        // Act
        var testCases = DiscoverTests(assemblyPath).Where(tc => tc.FullyQualifiedName.Contains(className)).ToList();
        testCases.Should().HaveCount(3);
        testCases.Should().NotContainNulls();

        var testResults = RunTests(testCases);
        testResults.Should().HaveCount(3);
        testResults.Should().NotContainNulls();

        // Assert
        // Ensure that some tests are running in parallel, because otherwise the output just works correctly.
        var firstEnd = testResults.Min(t => t.EndTime);
        var someStartedBeforeFirstEnded = testResults.Where(t => t.EndTime != firstEnd).Any(t => firstEnd > t.StartTime);
        someStartedBeforeFirstEnded.Should().BeTrue("Tests must run in parallel, but there were no other tests that started, before the first one ended.");

        ValidateOutputsAreNotMixed(testResults, "TestMethod1", ["TestMethod2", "TestMethod3"]);
        ValidateOutputsAreNotMixed(testResults, "TestMethod2", ["TestMethod1", "TestMethod3"]);
        ValidateOutputsAreNotMixed(testResults, "TestMethod3", ["TestMethod1", "TestMethod2"]);

        ValidateInitializationsAndCleanups(testResults);
    }

    private static readonly string DebugTraceString = string.Format(CultureInfo.InvariantCulture, "{0}{0}Debug Trace:{0}", Environment.NewLine);
    private static readonly Func<TestResultMessage, bool> IsDebugMessage = m => m.Category == "StdOutMsgs" && m.Text.StartsWith(DebugTraceString, StringComparison.Ordinal);
    private static readonly Func<TestResultMessage, bool> IsStandardOutputMessage = m => m.Category == "StdOutMsgs" && !m.Text.StartsWith(DebugTraceString, StringComparison.Ordinal);
    private static readonly Func<TestResultMessage, bool> IsStandardErrorMessage = m => m.Category == "StdErrMsgs";

    private static void ValidateOutputsAreNotMixed(IEnumerable<TestResult> testResults, string methodName, string[] shouldNotContain)
    {
        ValidateOutputIsNotMixed(testResults, methodName, shouldNotContain, IsStandardOutputMessage);
        ValidateOutputIsNotMixed(testResults, methodName, shouldNotContain, IsStandardErrorMessage);
        ValidateOutputIsNotMixed(testResults, methodName, shouldNotContain, IsDebugMessage);
    }

    private static void ValidateInitializationsAndCleanups(IEnumerable<TestResult> testResults)
    {
        ValidateInitializeAndCleanup(testResults, IsStandardOutputMessage);
        ValidateInitializeAndCleanup(testResults, IsStandardErrorMessage);
        ValidateInitializeAndCleanup(testResults, IsDebugMessage);
    }

    private static void ValidateOutputIsNotMixed(IEnumerable<TestResult> testResults, string methodName, string[] shouldNotContain, Func<TestResultMessage, bool> messageFilter)
    {
        // Make sure that the output between methods is not mixed. And that every method has test initialize and cleanup.
        var testMethod = testResults.Single(t => t.DisplayName == methodName);

        // Test method {methodName} was not found.
        testMethod.Should().NotBeNull();
        var message = testMethod.Messages.SingleOrDefault(messageFilter);

        // Message for {testMethod.DisplayName} was not found. All messages: { string.Join(Environment.NewLine, testMethod.Messages.Select(m => $"{m.Category} - {m.Text}")) }
        message.Should().NotBeNull();
        message.Text.Should().Contain(methodName);
        message.Text.Should().Contain("TestInitialize");
        message.Text.Should().Contain("TestCleanup");
        message.Text.Should().NotContainAny(shouldNotContain);
    }

    private static void ValidateInitializeAndCleanup(IEnumerable<TestResult> testResults, Func<TestResultMessage, bool> messageFilter)
    {
        // It is not deterministic where the class initialize and class cleanup will run, so we look at all tests, to make sure it is includes somewhere.
        var output = string.Join(Environment.NewLine, testResults.SelectMany(r => r.Messages).Where(messageFilter).Select(m => m.Text));
        output.Should().NotBeNull();
        output.Should().Contain("ClassInitialize");
        output.Should().Contain("ClassCleanup");
    }
}
