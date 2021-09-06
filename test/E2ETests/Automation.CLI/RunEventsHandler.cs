// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.MSTestV2.CLIAutomation
{
    using System.Collections.Generic;
    using System.Collections.ObjectModel;

    using Microsoft.VisualStudio.TestPlatform.ObjectModel;
    using Microsoft.VisualStudio.TestPlatform.ObjectModel.Client;
    using Microsoft.VisualStudio.TestPlatform.ObjectModel.Logging;

    public class RunEventsHandler : ITestRunEventsHandler
    {
        private readonly List<TestResult> passedTests = new List<TestResult>();
        private readonly List<TestResult> failedTests = new List<TestResult>();
        private readonly List<TestResult> skippedTests = new List<TestResult>();
        private readonly List<string> errors = new List<string>();

        /// <summary>
        /// Gets a list of Tests which passed.
        /// </summary>
        public ReadOnlyCollection<TestResult> PassedTests => this.passedTests.AsReadOnly();

        /// <summary>
        /// Gets a list of Tests which failed.
        /// </summary>
        public ReadOnlyCollection<TestResult> FailedTests => this.failedTests.AsReadOnly();

        /// <summary>
        /// Gets a list of Tests which skipped.
        /// </summary>
        public ReadOnlyCollection<TestResult> SkippedTests => this.skippedTests.AsReadOnly();

        public ReadOnlyCollection<string> Errors => this.errors.AsReadOnly();

        public double ElapsedTimeInRunningTests { get; private set; }

        public void HandleLogMessage(TestMessageLevel level, string message)
        {
            switch ((TestMessageLevel)level)
            {
                case TestMessageLevel.Informational:
                    EqtTrace.Info(message);
                    break;
                case TestMessageLevel.Warning:
                    EqtTrace.Warning(message);
                    break;
                case TestMessageLevel.Error:
                    this.errors.Add(message);
                    EqtTrace.Error(message);
                    break;
                default:
                    EqtTrace.Info(message);
                    break;
            }
        }

        public void HandleRawMessage(string rawMessage)
        {
        }

        public void HandleTestRunComplete(TestRunCompleteEventArgs testRunCompleteArgs, TestRunChangedEventArgs lastChunkArgs, ICollection<AttachmentSet> runContextAttachments, ICollection<string> executorUris)
        {
            if (lastChunkArgs != null)
            {
                foreach (TestResult testResult in lastChunkArgs.NewTestResults)
                {
                    switch (testResult.Outcome)
                    {
                        case TestOutcome.Passed:
                            this.passedTests.Add(testResult);
                            break;
                        case TestOutcome.Failed:
                            this.failedTests.Add(testResult);
                            break;
                        case TestOutcome.Skipped:
                            this.skippedTests.Add(testResult);
                            break;
                    }
                }
            }

            this.ElapsedTimeInRunningTests = testRunCompleteArgs.ElapsedTimeInRunningTests.TotalMilliseconds;
        }

        public void HandleTestRunStatsChange(TestRunChangedEventArgs testRunChangedArgs)
        {
            if (testRunChangedArgs != null)
            {
                foreach (TestResult testResult in testRunChangedArgs.NewTestResults)
                {
                    switch (testResult.Outcome)
                    {
                        case TestOutcome.Passed:
                            this.passedTests.Add(testResult);
                            break;
                        case TestOutcome.Failed:
                            this.failedTests.Add(testResult);
                            break;
                        case TestOutcome.Skipped:
                            this.skippedTests.Add(testResult);
                            break;
                        default:
                            break;
                    }
                }
            }
        }

        public int LaunchProcessWithDebuggerAttached(TestProcessStartInfo testProcessStartInfo)
        {
            return 0;
        }
    }
}
