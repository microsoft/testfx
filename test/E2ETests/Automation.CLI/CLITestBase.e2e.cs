﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;

using FluentAssertions;

using Microsoft.TestPlatform.VsTestConsole.TranslationLayer;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Client;

using TestFramework.ForTestingMSTest;

namespace Microsoft.MSTestV2.CLIAutomation;
public partial class CLITestBase : TestContainer
{
    private static VsTestConsoleWrapper s_vsTestConsoleWrapper;
    private DiscoveryEventsHandler _discoveryEventsHandler;

    protected RunEventsHandler RunEventsHandler { get; private set; }

    public CLITestBase()
    {
        s_vsTestConsoleWrapper = new VsTestConsoleWrapper(GetConsoleRunnerPath());
        s_vsTestConsoleWrapper.StartSession();
    }

    /// <summary>
    /// Invokes <c>vstest.console</c> to discover tests in the provided sources.
    /// </summary>
    /// <param name="sources">Collection of test containers.</param>
    /// <param name="runSettings">Run settings for execution.</param>
    public void InvokeVsTestForDiscovery(string[] sources, string runSettings = "")
    {
        ExpandTestSourcePaths(sources);

        _discoveryEventsHandler = new DiscoveryEventsHandler();
        string runSettingXml = GetRunSettingXml(runSettings, GetTestAdapterPath());

        s_vsTestConsoleWrapper.DiscoverTests(sources, runSettingXml, _discoveryEventsHandler);
    }

    /// <summary>
    /// Invokes <c>vstest.console</c> to execute tests in provided sources.
    /// </summary>
    /// <param name="sources">List of test assemblies.</param>
    /// <param name="runSettings">Run settings for execution.</param>
    /// <param name="testCaseFilter">Test Case filter for execution.</param>
    public void InvokeVsTestForExecution(string[] sources, string runSettings = "", string testCaseFilter = null, string targetFramework = "")
    {
        ExpandTestSourcePaths(sources);

        RunEventsHandler = new RunEventsHandler();
        string runSettingXml = GetRunSettingXml(runSettings, GetTestAdapterPath(), targetFramework);

        s_vsTestConsoleWrapper.RunTests(sources, runSettingXml, new TestPlatformOptions { TestCaseFilter = testCaseFilter }, RunEventsHandler);
        if (RunEventsHandler.Errors.Any())
        {
            throw new Exception($"Run failed with {RunEventsHandler.Errors.Count} errors:{Environment.NewLine}{string.Join(Environment.NewLine, RunEventsHandler.Errors)}");
        }
    }

    /// <summary>
    /// Gets the path to <c>vstest.console.exe</c>.
    /// </summary>
    /// <returns>Full path to <c>vstest.console.exe</c>.</returns>
    public string GetConsoleRunnerPath()
    {
        var vstestConsolePath = Path.Combine(Environment.CurrentDirectory, PackagesFolder, TestPlatformCLIPackageName, GetTestPlatformVersion(), VstestConsoleRelativePath);

        File.Exists(vstestConsolePath).Should().BeTrue("GetConsoleRunnerPath: Path not found: {0}", vstestConsolePath);

        return vstestConsolePath;
    }

    /// <summary>
    /// Validate if the discovered tests list contains provided tests.
    /// </summary>
    /// <param name="discoveredTestsList">List of tests expected to be discovered.</param>
    public void ValidateDiscoveredTests(params string[] discoveredTestsList)
    {
        foreach (var test in discoveredTestsList)
        {
            var flag = _discoveryEventsHandler.Tests.Contains(test)
                       || _discoveryEventsHandler.Tests.Contains(GetTestMethodName(test));
            flag.Should().BeTrue("Test '{0}' does not appear in discovered tests list.", test);
        }

        // Make sure only expected number of tests are discovered and not more.
        discoveredTestsList.Should().HaveSameCount(_discoveryEventsHandler.Tests);
    }

    /// <summary>
    /// Validates if the test results have the specified set of passed tests.
    /// </summary>
    /// <param name="passedTests">Set of passed tests.</param>
    /// <remarks>Provide the full test name similar to this format SampleTest.TestCode.TestMethodPass.</remarks>
    public void ValidatePassedTests(params string[] passedTests)
    {
        ValidatePassedTestsCount(passedTests.Length);
        ValidatePassedTestsContain(passedTests);
    }

    public void ValidatePassedTestsCount(int expectedPassedTestsCount)
    {
        // Make sure only expected number of tests passed and not more.
        RunEventsHandler.PassedTests.Should().HaveCount(expectedPassedTestsCount);
    }

    /// <summary>
    /// Validates if the test results have the specified set of failed tests.
    /// </summary>
    /// <param name="source">The test container.</param>
    /// <param name="failedTests">Set of failed tests.</param>
    /// <remarks>
    /// Provide the full test name similar to this format SampleTest.TestCode.TestMethodFailed.
    /// Also validates whether these tests have stack trace info.
    /// </remarks>
    public void ValidateFailedTests(string source, params string[] failedTests)
    {
        ValidateFailedTestsCount(failedTests.Length);
        ValidateFailedTestsContain(source, true, failedTests);
    }

    /// <summary>
    /// Validates the count of failed tests.
    /// </summary>
    /// <param name="expectedFailedTestsCount">Expected failed tests count.</param>
    public void ValidateFailedTestsCount(int expectedFailedTestsCount)
    {
        // Make sure only expected number of tests failed and not more.
        RunEventsHandler.FailedTests.Should().HaveCount(expectedFailedTestsCount);
    }

    /// <summary>
    /// Validates if the test results have the specified set of skipped tests.
    /// </summary>
    /// <param name="skippedTests">The set of skipped tests.</param>
    /// <remarks>Provide the full test name similar to this format SampleTest.TestCode.TestMethodSkipped.</remarks>
    public void ValidateSkippedTests(params string[] skippedTests)
    {
        // Make sure only expected number of tests skipped and not more.
        RunEventsHandler.SkippedTests.Should().HaveSameCount(skippedTests);

        ValidateSkippedTestsContain(skippedTests);
    }

    /// <summary>
    /// Validates if the test results contains the specified set of passed tests.
    /// </summary>
    /// <param name="passedTests">Set of passed tests.</param>
    /// <remarks>Provide the full test name similar to this format SampleTest.TestCode.TestMethodPass.</remarks>
    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
    public void ValidatePassedTestsContain(params string[] passedTests)
    {
        var passedTestResults = RunEventsHandler.PassedTests;
        var failedTestResults = RunEventsHandler.FailedTests;
        var skippedTestsResults = RunEventsHandler.SkippedTests;

        foreach (var test in passedTests)
        {
            var isFailed = failedTestResults.Any(
                p => test.Equals(p.TestCase?.FullyQualifiedName)
                     || test.Equals(p.DisplayName)
                     || test.Equals(p.TestCase.DisplayName));

            var isSkipped = skippedTestsResults.Any(
                p => test.Equals(p.TestCase?.FullyQualifiedName)
                     || test.Equals(p.DisplayName)
                     || test.Equals(p.TestCase.DisplayName));

            var failedOrSkippedMessage = isFailed ? " (Test failed)" : isSkipped ? " (Test skipped)" : string.Empty;

            passedTestResults.Should().Contain(
                p => test.Equals(p.TestCase.FullyQualifiedName)
                     || test.Equals(p.DisplayName)
                     || test.Equals(p.TestCase.DisplayName),
                $"Test '{test}' does not appear in passed tests list." + failedOrSkippedMessage);
        }
    }

    /// <summary>
    /// Validates if the test results contains the specified set of failed tests.
    /// </summary>
    /// <param name="source">The test container.</param>
    /// <param name="validateStackTraceInfo">Validates the existence of stack trace when set to true.</param>
    /// <param name="failedTests">Set of failed tests.</param>
    /// <remarks>
    /// Provide the full test name similar to this format SampleTest.TestCode.TestMethodFailed.
    /// Also validates whether these tests have stack trace info.
    /// </remarks>
    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
    public void ValidateFailedTestsContain(string source, bool validateStackTraceInfo, params string[] failedTests)
    {
        foreach (var test in failedTests)
        {
            var testFound = RunEventsHandler.FailedTests.FirstOrDefault(f => test.Equals(f.TestCase?.FullyQualifiedName) ||
                       test.Equals(f.DisplayName));
            testFound.Should().NotBeNull("Test '{0}' does not appear in failed tests list.", test);

            if (!validateStackTraceInfo)
            {
                continue;
            }

            testFound.ErrorStackTrace.Should().NotBeNullOrWhiteSpace($"The test failure {testFound.DisplayName ?? testFound.TestCase.FullyQualifiedName} with message {testFound.ErrorMessage} lacks stacktrace.");

            // Verify stack information as well.
            testFound.ErrorStackTrace.Should().Contain(GetTestMethodName(test), "No stack trace for failed test: {0}", test);
        }
    }

    /// <summary>
    /// Validates if the test results contains the specified set of skipped tests.
    /// </summary>
    /// <param name="skippedTests">The set of skipped tests.</param>
    /// <remarks>Provide the full test name similar to this format SampleTest.TestCode.TestMethodSkipped.</remarks>
    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
    public void ValidateSkippedTestsContain(params string[] skippedTests)
    {
        foreach (var test in skippedTests)
        {
            RunEventsHandler.SkippedTests.Should().Contain(
                s => test.Equals(s.TestCase.FullyQualifiedName) || test.Equals(s.DisplayName),
                "Test '{0}' does not appear in skipped tests list.", test);
        }
    }

    public void ValidateTestRunTime(int thresholdTime)
    {
        var time = RunEventsHandler.ElapsedTimeInRunningTests >= 0 && RunEventsHandler.ElapsedTimeInRunningTests < thresholdTime;
        time.Should().BeTrue($"Test Run was expected to not exceed {thresholdTime} but it took {RunEventsHandler.ElapsedTimeInRunningTests}");
    }

    /// <summary>
    /// Gets the test method name from full name.
    /// </summary>
    /// <param name="testFullName">Fully qualified name of the test.</param>
    /// <returns>Simple name of the test.</returns>
    private static string GetTestMethodName(string testFullName)
    {
        string testMethodName = string.Empty;

        var splits = testFullName.Split('.');
        if (splits.Length >= 3)
        {
            testMethodName = splits[2];
        }

        return testMethodName;
    }

    /// <summary>
    /// Converts relative paths to absolute.
    /// </summary>
    /// <param name="paths">An array of file paths, elements may be modified to absolute paths.</param>
    private void ExpandTestSourcePaths(string[] paths)
    {
        for (var i = 0; i < paths.Length; i++)
        {
            var path = paths[i];

            if (!Path.IsPathRooted(path))
            {
                paths[i] = GetAssetFullPath(path);
            }
            else
            {
                paths[i] = Path.GetFullPath(path);
            }
        }
    }
}
