// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Linq;

namespace Microsoft.MSTestV2.Smoke.DiscoveryAndExecutionTests;

using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Text.RegularExpressions;

using Microsoft.MSTestV2.CLIAutomation;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;

using OM = Microsoft.VisualStudio.TestPlatform.ObjectModel;

public class OutputTests : CLITestBase
{
    private const string TestAssembly = "OutputTestProject.dll";

    public void OutputIsNotMixedWhenTestsRunInParallel()
    {
        ValidateOutputForClass(TestAssembly, "UnitTest1");
    }

    public void OutputIsNotMixedWhenAsyncTestsRunInParallel()
    {
        ValidateOutputForClass(TestAssembly, "UnitTest2");
    }

    private void ValidateOutputForClass(string testAssembly, string className)
    {
        // LogMessageListener uses an implementation of a string writer that captures output per async context.
        // This allows us to capture output from tasks even when they are running in parallel.

        // Arrange
        var assemblyPath = Path.IsPathRooted(testAssembly) ? testAssembly : GetAssetFullPath(testAssembly);

        // Act
        var testCases = DiscoverTests(assemblyPath).Where(tc => tc.FullyQualifiedName.Contains(className)).ToList();
        var testResults = RunTests(assemblyPath, testCases);

        // Assert
        Verify(3 == testResults.Count);

        // TODO: Re-enable this once we figure out how to make that pass in our CI pipeline.
        //// Ensure that some tests are running in parallel, because otherwise the output just works correctly.
        //var firstEnd = testResults.Min(t => t.EndTime);
        //var someStartedBeforeFirstEnded = testResults.Where(t => t.EndTime != firstEnd).Any(t => firstEnd > t.StartTime);
        //Assert.IsTrue(someStartedBeforeFirstEnded, "Tests must run in parallel, but there were no other tests that started, before the first one ended.");

        ValidateOutputsAreNotMixed(testResults, "TestMethod1", new[] { "TestMethod2", "TestMethod3" });
        ValidateOutputsAreNotMixed(testResults, "TestMethod2", new[] { "TestMethod1", "TestMethod3" });
        ValidateOutputsAreNotMixed(testResults, "TestMethod3", new[] { "TestMethod1", "TestMethod2" });

        ValidateInitializationsAndCleanups(testResults);
    }

    private static readonly Func<TestResultMessage, bool> IsDebugMessage = m => m.Category == "StdOutMsgs" && m.Text.StartsWith("\n\nDebug Trace:\n");
    private static readonly Func<TestResultMessage, bool> IsStandardOutputMessage = m => m.Category == "StdOutMsgs" && !m.Text.StartsWith("\n\nDebug Trace:\n");
    private static readonly Func<TestResultMessage, bool> IsStandardErrorMessage = m => m.Category == "StdErrMsgs";

    private static void ValidateOutputsAreNotMixed(ReadOnlyCollection<OM.TestResult> testResults, string methodName, string[] shouldNotContain)
    {
        ValidateOutputIsNotMixed(testResults, methodName, shouldNotContain, IsStandardOutputMessage);
        ValidateOutputIsNotMixed(testResults, methodName, shouldNotContain, IsStandardErrorMessage);
        ValidateOutputIsNotMixed(testResults, methodName, shouldNotContain, IsDebugMessage);
    }

    private static void ValidateInitializationsAndCleanups(ReadOnlyCollection<OM.TestResult> testResults)
    {
        ValidateInitializeAndCleanup(testResults, IsStandardOutputMessage);
        ValidateInitializeAndCleanup(testResults, IsStandardErrorMessage);
        ValidateInitializeAndCleanup(testResults, IsDebugMessage);
    }

    private static void ValidateOutputIsNotMixed(ReadOnlyCollection<OM.TestResult> testResults, string methodName, string[] shouldNotContain, Func<TestResultMessage, bool> messageFilter)
    {
        // Make sure that the output between methods is not mixed. And that every method has test initialize and cleanup.
        var testMethod = testResults.Single(t => t.DisplayName == methodName);
        // Test method {methodName} was not found.
        Verify(testMethod is not null);
        var message = testMethod.Messages.SingleOrDefault(messageFilter);
        // Message for {testMethod.DisplayName} was not found. All messages: { string.Join(Environment.NewLine, testMethod.Messages.Select(m => $"{m.Category} - {m.Text}")) }
        Verify(message is not null);
        Verify(new Regex(methodName).IsMatch(message.Text));
        Verify(new Regex(methodName).IsMatch(message.Text));
        Verify(new Regex("TestInitialize").IsMatch(message.Text));
        Verify(new Regex("TestCleanup").IsMatch(message.Text));
        Verify(!new Regex(string.Join("|", shouldNotContain)).IsMatch(message.Text));
    }

    private static void ValidateInitializeAndCleanup(ReadOnlyCollection<OM.TestResult> testResults, Func<TestResultMessage, bool> messageFilter)
    {
        // It is not deterministic where the class initialize and class cleanup will run, so we look at all tests, to make sure it is includes somewhere.
        var output = string.Join(Environment.NewLine, testResults.SelectMany(r => r.Messages).Where(messageFilter).Select(m => m.Text));
        Verify(output is not null);
        Verify(new Regex("ClassInitialize").IsMatch(output));
        Verify(new Regex("ClassCleanup").IsMatch(output));
    }
}
