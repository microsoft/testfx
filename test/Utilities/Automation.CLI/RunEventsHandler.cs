// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.ObjectModel;

using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Client;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Logging;

namespace Microsoft.MSTestV2.CLIAutomation;

public class RunEventsHandler : ITestRunEventsHandler
{
    private readonly List<TestResult> _passedTests = [];
    private readonly List<TestResult> _failedTests = [];
    private readonly List<TestResult> _skippedTests = [];
    private readonly List<string> _errors = [];

    /// <summary>
    /// Gets a list of Tests which passed.
    /// </summary>
    public ReadOnlyCollection<TestResult> PassedTests => _passedTests.AsReadOnly();

    /// <summary>
    /// Gets a list of Tests which failed.
    /// </summary>
    public ReadOnlyCollection<TestResult> FailedTests => _failedTests.AsReadOnly();

    /// <summary>
    /// Gets a list of Tests which skipped.
    /// </summary>
    public ReadOnlyCollection<TestResult> SkippedTests => _skippedTests.AsReadOnly();

    public ReadOnlyCollection<string> Errors => _errors.AsReadOnly();

    public double ElapsedTimeInRunningTests { get; private set; }

    public void HandleLogMessage(TestMessageLevel level, string message)
    {
        switch (level)
        {
            case TestMessageLevel.Informational:
                EqtTrace.Info(message);
                break;
            case TestMessageLevel.Warning:
                EqtTrace.Warning(message);
                break;
            case TestMessageLevel.Error:
                _errors.Add(message);
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
                        _passedTests.Add(testResult);
                        break;
                    case TestOutcome.Failed:
                        _failedTests.Add(testResult);
                        break;
                    case TestOutcome.Skipped:
                        _skippedTests.Add(testResult);
                        break;
                }
            }
        }

        ElapsedTimeInRunningTests = testRunCompleteArgs.ElapsedTimeInRunningTests.TotalMilliseconds;
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
                        _passedTests.Add(testResult);
                        break;
                    case TestOutcome.Failed:
                        _failedTests.Add(testResult);
                        break;
                    case TestOutcome.Skipped:
                        _skippedTests.Add(testResult);
                        break;
                    default:
                        break;
                }
            }
        }
    }

    public int LaunchProcessWithDebuggerAttached(TestProcessStartInfo testProcessStartInfo) => 0;
}
