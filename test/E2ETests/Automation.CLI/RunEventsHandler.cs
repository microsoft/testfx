// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.MSTestV2.CLIAutomation
{
    using System.Collections.Generic;
    using Microsoft.VisualStudio.TestPlatform.ObjectModel;
    using Microsoft.VisualStudio.TestPlatform.ObjectModel.Client;
    using Microsoft.VisualStudio.TestPlatform.ObjectModel.Logging;

    public class RunEventsHandler : ITestRunEventsHandler
    {
        /// <summary>
        /// Gets a list of Tests which passed.
        /// </summary>
        public IList<TestResult> PassedTests { get; private set; }

        /// <summary>
        /// Gets a list of Tests which failed.
        /// </summary>
        public IList<TestResult> FailedTests { get; private set; }

        /// <summary>
        /// Gets a list of Tests which skipped.
        /// </summary>
        public IList<TestResult> SkippedTests { get; private set; }

        public double ElapsedTimeInRunningTests { get; private set; }

        public RunEventsHandler()
        {
            this.PassedTests = new List<TestResult>();
            this.FailedTests = new List<TestResult>();
            this.SkippedTests = new List<TestResult>();
        }

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
                            this.PassedTests.Add(testResult);
                            break;
                        case TestOutcome.Failed:
                            this.FailedTests.Add(testResult);
                            break;
                        case TestOutcome.Skipped:
                            this.SkippedTests.Add(testResult);
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
                            this.PassedTests.Add(testResult);
                            break;
                        case TestOutcome.Failed:
                            this.FailedTests.Add(testResult);
                            break;
                        case TestOutcome.Skipped:
                            this.SkippedTests.Add(testResult);
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
