// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.TestPlatform.VsTestConsole.TranslationLayer;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Client;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.MSTestV2.CLIAutomation;

public abstract partial class CLITestBase
{
    private static VsTestConsoleWrapper? s_vsTestConsoleWrapper;
    private DiscoveryEventsHandler? _discoveryEventsHandler;

    protected CLITestBase()
    {
        s_vsTestConsoleWrapper = new(
            VSTestConsoleLocator.GetConsoleRunnerPath(),
            new()
            {
                EnvironmentVariables = new()
                {
                    ["DOTNET_ROOT"] = FindDotNetRoot(),
                },
            });
        s_vsTestConsoleWrapper.StartSession();
    }

    protected RunEventsHandler RunEventsHandler { get; private set; } = null!;

    /// <summary>
    /// Invokes <c>vstest.console</c> to discover tests in the provided sources.
    /// </summary>
    /// <param name="sources">Collection of test containers.</param>
    /// <param name="runSettings">Run settings for execution.</param>
    /// <param name="targetFramework">Target framework for the test run.</param>
    public void InvokeVsTestForDiscovery(string[] sources, string runSettings = "", string? targetFramework = null)
    {
        ExpandTestSourcePaths(sources, targetFramework);

        _discoveryEventsHandler = new DiscoveryEventsHandler();
        string runSettingsXml = GetRunSettingsXml(runSettings);

        s_vsTestConsoleWrapper!.DiscoverTests(sources, runSettingsXml, _discoveryEventsHandler);
    }

    /// <summary>
    /// Invokes <c>vstest.console</c> to execute tests in provided sources.
    /// </summary>
    /// <param name="sources">List of test assemblies.</param>
    /// <param name="runSettings">Run settings for execution.</param>
    /// <param name="testCaseFilter">Test Case filter for execution.</param>
    /// <param name="targetFramework">Target framework for the test run.</param>
    public void InvokeVsTestForExecution(string[] sources, string runSettings = "", string? testCaseFilter = null, string? targetFramework = null)
    {
        ExpandTestSourcePaths(sources, targetFramework);

        RunEventsHandler = new RunEventsHandler();
        string runSettingsXml = GetRunSettingsXml(runSettings);

        s_vsTestConsoleWrapper!.RunTests(sources, runSettingsXml, new TestPlatformOptions { TestCaseFilter = testCaseFilter }, RunEventsHandler);
        if (RunEventsHandler.Errors.Count != 0)
        {
            throw new Exception($"Run failed with {RunEventsHandler.Errors.Count} errors:{Environment.NewLine}{string.Join(Environment.NewLine, RunEventsHandler.Errors)}");
        }
    }



    /// <summary>
    /// Validate if the discovered tests list contains provided tests.
    /// </summary>
    /// <param name="discoveredTestsList">List of tests expected to be discovered.</param>
    public void ValidateDiscoveredTests(params string[] discoveredTestsList)
    {
        foreach (string test in discoveredTestsList)
        {
            Assert.IsTrue(_discoveryEventsHandler!.Tests.Contains(test) || _discoveryEventsHandler.Tests.Contains(GetTestMethodName(test)), $"Test '{test}' does not appear in discovered tests list.");
        }

        // Make sure only expected number of tests are discovered and not more.
        Assert.AreEqual(_discoveryEventsHandler!.Tests.Length, discoveredTestsList.Length);
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

    public void ValidatePassedTestsCount(int expectedPassedTestsCount) =>
        // Make sure only expected number of tests passed and not more.
        Assert.HasCount(expectedPassedTestsCount, RunEventsHandler.PassedTests);

    /// <summary>
    /// Validates if the test results have the specified set of failed tests.
    /// </summary>
    /// <param name="failedTests">Set of failed tests.</param>
    /// <remarks>
    /// Provide the full test name similar to this format SampleTest.TestCode.TestMethodFailed.
    /// Also validates whether these tests have stack trace info.
    /// </remarks>
    public void ValidateFailedTests(params string[] failedTests)
    {
        ValidateFailedTestsCount(failedTests.Length);
        ValidateFailedTestsContain(true, failedTests);
    }

    /// <summary>
    /// Validates if the test results have the specified set of failed tests.
    /// </summary>
    /// <param name="validateStackTraceInfo">Whether or not to validate the error stack trace.</param>
    /// <param name="failedTests">Set of failed tests.</param>
    /// <remarks>
    /// Provide the full test name similar to this format SampleTest.TestCode.TestMethodFailed.
    /// Also validates whether these tests have stack trace info.
    /// </remarks>
    public void ValidateFailedTests(bool validateStackTraceInfo, params string[] failedTests)
    {
        ValidateFailedTestsCount(failedTests.Length);
        ValidateFailedTestsContain(validateStackTraceInfo, failedTests);
    }

    /// <summary>
    /// Validates the count of failed tests.
    /// </summary>
    /// <param name="expectedFailedTestsCount">Expected failed tests count.</param>
    public void ValidateFailedTestsCount(int expectedFailedTestsCount) =>
        // Make sure only expected number of tests failed and not more.
        Assert.HasCount(expectedFailedTestsCount, RunEventsHandler.FailedTests);

    /// <summary>
    /// Validates if the test results have the specified set of skipped tests.
    /// </summary>
    /// <param name="skippedTests">The set of skipped tests.</param>
    /// <remarks>Provide the full test name similar to this format SampleTest.TestCode.TestMethodSkipped.</remarks>
    public void ValidateSkippedTests(params string[] skippedTests)
    {
        // Make sure only expected number of tests skipped and not more.
        Assert.AreEqual(skippedTests.Length, RunEventsHandler.SkippedTests.Count);
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
        System.Collections.ObjectModel.ReadOnlyCollection<VisualStudio.TestPlatform.ObjectModel.TestResult> passedTestResults = RunEventsHandler.PassedTests;
        System.Collections.ObjectModel.ReadOnlyCollection<VisualStudio.TestPlatform.ObjectModel.TestResult> failedTestResults = RunEventsHandler.FailedTests;
        System.Collections.ObjectModel.ReadOnlyCollection<VisualStudio.TestPlatform.ObjectModel.TestResult> skippedTestsResults = RunEventsHandler.SkippedTests;

        foreach (string test in passedTests)
        {
            bool isFailed = failedTestResults.Any(
                p => test.Equals(p.TestCase?.FullyQualifiedName, StringComparison.Ordinal)
                     || test.Equals(p.DisplayName, StringComparison.Ordinal)
                     || test.Equals(p.TestCase!.DisplayName, StringComparison.Ordinal));

            bool isSkipped = skippedTestsResults.Any(
                p => test.Equals(p.TestCase?.FullyQualifiedName, StringComparison.Ordinal)
                     || test.Equals(p.DisplayName, StringComparison.Ordinal)
                     || test.Equals(p.TestCase!.DisplayName, StringComparison.Ordinal));

            string failedOrSkippedMessage = isFailed ? " (Test failed)" : isSkipped ? " (Test skipped)" : string.Empty;

            Assert.Contains(
                p => test.Equals(p.TestCase.FullyQualifiedName, StringComparison.Ordinal)
                     || test.Equals(p.DisplayName, StringComparison.Ordinal)
                     || test.Equals(p.TestCase.DisplayName, StringComparison.Ordinal),
                passedTestResults,
                $"Test '{test}' does not appear in passed tests list." + failedOrSkippedMessage);
        }
    }

    /// <summary>
    /// Validates if the test results contains the specified set of failed tests.
    /// </summary>
    /// <param name="validateStackTraceInfo">Validates the existence of stack trace when set to true.</param>
    /// <param name="failedTests">Set of failed tests.</param>
    /// <remarks>
    /// Provide the full test name similar to this format SampleTest.TestCode.TestMethodFailed.
    /// Also validates whether these tests have stack trace info.
    /// </remarks>
    [MethodImpl(MethodImplOptions.NoInlining | MethodImplOptions.NoOptimization)]
    public void ValidateFailedTestsContain(bool validateStackTraceInfo, params string[] failedTests)
    {
        foreach (string test in failedTests)
        {
            VisualStudio.TestPlatform.ObjectModel.TestResult testFound = RunEventsHandler.FailedTests.FirstOrDefault(f => test.Equals(f.TestCase?.FullyQualifiedName, StringComparison.Ordinal) ||
                       test.Equals(f.DisplayName, StringComparison.Ordinal));
            Assert.IsNotNull(testFound, "Test '{0}' does not appear in failed tests list.", test);

#if DEBUG
            if (!validateStackTraceInfo)
            {
                continue;
            }

            Assert.IsFalse(
                string.IsNullOrWhiteSpace(testFound.ErrorStackTrace),
                $"The test failure {testFound.DisplayName ?? testFound.TestCase.FullyQualifiedName} with message {testFound.ErrorMessage} lacks stack trace.");

            // If test name is not empty, verify stack information as well.
            if (GetTestMethodName(test) is { Length: > 0 } testMethodName)
            {
                Assert.IsNotNull(testFound.ErrorStackTrace);
                Assert.Contains(testMethodName, testFound.ErrorStackTrace, $"No stack trace for failed test: {test}");
            }
#endif
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
        foreach (string test in skippedTests)
        {
            Assert.Contains(
                s => test.Equals(s.TestCase.FullyQualifiedName, StringComparison.Ordinal) || test.Equals(s.DisplayName, StringComparison.Ordinal),
                RunEventsHandler.SkippedTests,
                $"Test '{test}' does not appear in skipped tests list.");
        }
    }

    public void ValidateTestRunTime(int thresholdTime)
        => Assert.IsTrue(
            RunEventsHandler.ElapsedTimeInRunningTests >= 0 && RunEventsHandler.ElapsedTimeInRunningTests < thresholdTime,
            $"Test Run was expected to not exceed {thresholdTime} but it took {RunEventsHandler.ElapsedTimeInRunningTests}");

    /// <summary>
    /// Gets the test method name from full name.
    /// </summary>
    /// <param name="testFullName">Fully qualified name of the test.</param>
    /// <returns>Simple name of the test.</returns>
    private static string GetTestMethodName(string testFullName)
    {
        string testMethodName = string.Empty;

        string[] splits = testFullName.Split('.');
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
    /// <param name="targetFramework">Target framework for the test run.</param>
    private void ExpandTestSourcePaths(string[] paths, string? targetFramework = null)
    {
        for (int i = 0; i < paths.Length; i++)
        {
            string path = paths[i];

            paths[i] = !Path.IsPathRooted(path) ? GetAssetFullPath(path, targetFramework: targetFramework) : Path.GetFullPath(path);
        }
    }

    private static string FindDotNetRoot()
    {
        string dotNetRoot = Environment.GetEnvironmentVariable("DOTNET_ROOT");
        if (!string.IsNullOrEmpty(dotNetRoot))
        {
            return dotNetRoot;
        }

        var currentDirectory = new DirectoryInfo(Directory.GetCurrentDirectory());
        do
        {
            string folderName = ".dotnet";
            if (currentDirectory.EnumerateDirectories(folderName).Any())
            {
                return Path.Combine(currentDirectory.FullName, folderName);
            }
        }
        while ((currentDirectory = currentDirectory.Parent) != null);

        throw new InvalidOperationException("Could not find .dotnet folder in the current directory or any parent directories.");
    }
}
