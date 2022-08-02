// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.MSTestV2.CLIAutomation
{
    using System;
    using System.IO;
    using System.Linq;
    using System.Xml;

    using Microsoft.TestPlatform.VsTestConsole.TranslationLayer;
    using Microsoft.VisualStudio.TestPlatform.ObjectModel.Client;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    public partial class CLITestBase
    {
        private static VsTestConsoleWrapper vsTestConsoleWrapper;
        private DiscoveryEventsHandler discoveryEventsHandler;
        private RunEventsHandler runEventsHandler;

        public CLITestBase()
        {
            vsTestConsoleWrapper = new VsTestConsoleWrapper(this.GetConsoleRunnerPath());
            vsTestConsoleWrapper.StartSession();
        }

        /// <summary>
        /// Invokes <c>vstest.console</c> to discover tests in the provided sources.
        /// </summary>
        /// <param name="sources">Collection of test containers.</param>
        /// <param name="runSettings">Run settings for execution.</param>
        public void InvokeVsTestForDiscovery(string[] sources, string runSettings = "")
        {
            this.ExpandTestSourcePaths(sources);

            this.discoveryEventsHandler = new DiscoveryEventsHandler();
            string runSettingXml = this.GetRunSettingXml(runSettings, this.GetTestAdapterPath());

            // this step of Initializing extensions should not be required after this issue: https://github.com/Microsoft/vstest/issues/236 is fixed
            vsTestConsoleWrapper.InitializeExtensions(Directory.GetFiles(this.GetTestAdapterPath(), "*TestAdapter.dll"));
            vsTestConsoleWrapper.DiscoverTests(sources, runSettingXml, this.discoveryEventsHandler);
        }

        /// <summary>
        /// Invokes <c>vstest.console</c> to execute tests in provided sources.
        /// </summary>
        /// <param name="sources">List of test assemblies.</param>
        /// <param name="runSettings">Run settings for execution.</param>
        /// <param name="testCaseFilter">Test Case filter for execution.</param>
        public void InvokeVsTestForExecution(string[] sources, string runSettings = "", string testCaseFilter = null)
        {
            this.ExpandTestSourcePaths(sources);

            this.runEventsHandler = new RunEventsHandler();
            string runSettingXml = this.GetRunSettingXml(runSettings, this.GetTestAdapterPath());

            // this step of Initializing extensions should not be required after this issue: https://github.com/Microsoft/vstest/issues/236 is fixed
            vsTestConsoleWrapper.InitializeExtensions(Directory.GetFiles(this.GetTestAdapterPath(), "*TestAdapter.dll"));
            vsTestConsoleWrapper.RunTests(sources, runSettingXml, new TestPlatformOptions { TestCaseFilter = testCaseFilter }, this.runEventsHandler);
            if (this.runEventsHandler.Errors.Any())
            {
                throw new Exception($"Run failed with {this.runEventsHandler.Errors.Count} errors:{Environment.NewLine}{string.Join(Environment.NewLine, this.runEventsHandler.Errors)}");
            }
        }

        /// <summary>
        /// Gets the path to <c>vstest.console.exe</c>.
        /// </summary>
        /// <returns>Full path to <c>vstest.console.exe</c></returns>
        public string GetConsoleRunnerPath()
        {
            var packagesFolder = Path.Combine(Environment.CurrentDirectory, this.GetRelativeRepositoryRootPath(), PackagesFolder);
            var vstestConsolePath = Path.Combine(packagesFolder, TestPlatformCLIPackage, VstestConsoleRelativePath);

            Assert.IsTrue(File.Exists(vstestConsolePath), "GetConsoleRunnerPath: Path not found: {0}", vstestConsolePath);

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
                var flag = this.discoveryEventsHandler.Tests.Contains(test)
                           || this.discoveryEventsHandler.Tests.Contains(GetTestMethodName(test));
                Assert.IsTrue(flag, "Test '{0}' does not appear in discovered tests list.", test);
            }

            // Make sure only expected number of tests are discovered and not more.
            Assert.AreEqual(discoveredTestsList.Length, this.discoveryEventsHandler.Tests.Count);
        }

        /// <summary>
        /// Validates if the test results have the specified set of passed tests.
        /// </summary>
        /// <param name="passedTests">Set of passed tests.</param>
        /// <remarks>Provide the full test name similar to this format SampleTest.TestCode.TestMethodPass.</remarks>
        public void ValidatePassedTests(params string[] passedTests)
        {
            this.ValidatePassedTestsCount(passedTests.Length);
            this.ValidatePassedTestsContain(passedTests);
        }

        public void ValidatePassedTestsCount(int expectedPassedTestsCount)
        {
            // Make sure only expected number of tests passed and not more.
            Assert.AreEqual(expectedPassedTestsCount, this.runEventsHandler.PassedTests.Count);
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
            this.ValidateFailedTestsCount(failedTests.Length);
            this.ValidateFailedTestsContain(source, true, failedTests);
        }

        /// <summary>
        /// Validates the count of failed tests.
        /// </summary>
        /// <param name="expectedFailedTestsCount">Expected failed tests count.</param>
        public void ValidateFailedTestsCount(int expectedFailedTestsCount)
        {
            // Make sure only expected number of tests failed and not more.
            Assert.AreEqual(expectedFailedTestsCount, this.runEventsHandler.FailedTests.Count);
        }

        /// <summary>
        /// Validates if the test results have the specified set of skipped tests.
        /// </summary>
        /// <param name="skippedTests">The set of skipped tests.</param>
        /// <remarks>Provide the full test name similar to this format SampleTest.TestCode.TestMethodSkipped.</remarks>
        public void ValidateSkippedTests(params string[] skippedTests)
        {
            // Make sure only expected number of tests skipped and not more.
            Assert.AreEqual(skippedTests.Length, this.runEventsHandler.SkippedTests.Count);

            this.ValidateSkippedTestsContain(skippedTests);
        }

        /// <summary>
        /// Validates if the test results contains the specified set of passed tests.
        /// </summary>
        /// <param name="passedTests">Set of passed tests.</param>
        /// <remarks>Provide the full test name similar to this format SampleTest.TestCode.TestMethodPass.</remarks>
        public void ValidatePassedTestsContain(params string[] passedTests)
        {
            var passedTestResults = this.runEventsHandler.PassedTests;
            var failedTestResults = this.runEventsHandler.FailedTests;
            var skippedTestsResults = this.runEventsHandler.SkippedTests;

            foreach (var test in passedTests)
            {
                var testFound = passedTestResults.Any(
                    p => test.Equals(p.TestCase?.FullyQualifiedName)
                         || test.Equals(p.DisplayName)
                         || test.Equals(p.TestCase.DisplayName));

                var isFailed = failedTestResults.Any(
                    p => test.Equals(p.TestCase?.FullyQualifiedName)
                         || test.Equals(p.DisplayName)
                         || test.Equals(p.TestCase.DisplayName));

                var isSkipped = skippedTestsResults.Any(
                    p => test.Equals(p.TestCase?.FullyQualifiedName)
                         || test.Equals(p.DisplayName)
                         || test.Equals(p.TestCase.DisplayName));

                var failedOrSkippedMessage = isFailed ? " (Test failed)" : isSkipped ? " (Test skipped)" : string.Empty;

                Assert.IsTrue(testFound, "Test '{0}' does not appear in passed tests list." + failedOrSkippedMessage, test);
            }
        }

        /// <summary>
        /// Validates if the test results contains the specified set of failed tests.
        /// </summary>
        /// <param name="source">The test container.</param>
        /// <param name="validateStackTraceInfo">Validates the existence of stack trace when set to true</param>
        /// <param name="failedTests">Set of failed tests.</param>
        /// <remarks>
        /// Provide the full test name similar to this format SampleTest.TestCode.TestMethodFailed.
        /// Also validates whether these tests have stack trace info.
        /// </remarks>
        public void ValidateFailedTestsContain(string source, bool validateStackTraceInfo, params string[] failedTests)
        {
            foreach (var test in failedTests)
            {
                var testFound = this.runEventsHandler.FailedTests.FirstOrDefault(f => test.Equals(f.TestCase?.FullyQualifiedName) ||
                           test.Equals(f.DisplayName));
                Assert.IsNotNull(testFound, "Test '{0}' does not appear in failed tests list.", test);

                // Skipping this check for x64 as of now. https://github.com/Microsoft/testfx/issues/60 should fix this.
                if (source.IndexOf("x64") == -1 && validateStackTraceInfo)
                {
                    if (string.IsNullOrWhiteSpace(testFound.ErrorStackTrace))
                    {
                        Assert.Fail($"The test failure {testFound.DisplayName ?? testFound.TestCase.FullyQualifiedName} with message {testFound.ErrorMessage} lacks stacktrace.");
                    }

                    // Verify stack information as well.
                    Assert.IsTrue(testFound.ErrorStackTrace.Contains(GetTestMethodName(test)), "No stack trace for failed test: {0}", test);
                }
            }
        }

        /// <summary>
        /// Validates if the test results contains the specified set of skipped tests.
        /// </summary>
        /// <param name="skippedTests">The set of skipped tests.</param>
        /// <remarks>Provide the full test name similar to this format SampleTest.TestCode.TestMethodSkipped.</remarks>
        public void ValidateSkippedTestsContain(params string[] skippedTests)
        {
            foreach (var test in skippedTests)
            {
                var testFound = this.runEventsHandler.SkippedTests.Any(s => test.Equals(s.TestCase.FullyQualifiedName) ||
                           test.Equals(s.DisplayName));
                Assert.IsTrue(testFound, "Test '{0}' does not appear in skipped tests list.", test);
            }
        }

        public void ValidateTestRunTime(int thresholdTime)
        {
            Assert.IsTrue(
                this.runEventsHandler.ElapsedTimeInRunningTests >= 0 && this.runEventsHandler.ElapsedTimeInRunningTests < thresholdTime,
                $"Test Run was expected to not exceed {thresholdTime} but it took {this.runEventsHandler.ElapsedTimeInRunningTests}");
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
                    paths[i] = this.GetAssetFullPath(path);
                }
                else
                {
                    paths[i] = Path.GetFullPath(path);
                }
            }
        }
    }
}
