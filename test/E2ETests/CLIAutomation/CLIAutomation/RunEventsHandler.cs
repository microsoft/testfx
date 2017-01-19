// Copyright (c) Microsoft Corporation. All rights reserved.

namespace Microsoft.MSTestV2.CLIAutomation
{
    using Microsoft.VisualStudio.TestPlatform.ObjectModel;
    using Microsoft.VisualStudio.TestPlatform.ObjectModel.Client;
    using Microsoft.VisualStudio.TestPlatform.ObjectModel.Logging;
    using System.Collections.Generic;

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
   
        public RunEventsHandler()
        {
            PassedTests = new List<TestResult>();
            FailedTests = new List<TestResult>();
            SkippedTests = new List<TestResult>();
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
                            PassedTests.Add(testResult);
                            break;
                        case TestOutcome.Failed:
                            FailedTests.Add(testResult);
                            break;
                        case TestOutcome.Skipped:
                            SkippedTests.Add(testResult);
                            break;
                            
                    }
                }
            }
        }

        public void HandleTestRunStatsChange(TestRunChangedEventArgs testRunChangedArgs)
        {
            if (testRunChangedArgs != null)
            {
                foreach (TestResult testResult in testRunChangedArgs.NewTestResults)
                {
                    switch(testResult.Outcome)
                    {
                        case TestOutcome.Passed:
                            PassedTests.Add(testResult);
                            break;
                        case TestOutcome.Failed:
                            FailedTests.Add(testResult);
                            break;
                        case TestOutcome.Skipped:
                            SkippedTests.Add(testResult);
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
