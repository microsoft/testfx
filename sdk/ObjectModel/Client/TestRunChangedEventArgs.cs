// ---------------------------------------------------------------------------
// <copyright file="TestResultsEventArgs.cs.cs" company="Microsoft">
//     Copyright (c) Microsoft Corporation. All rights reserved.
// </copyright>
// <summary>
//    Event args TestResultsEventArgs indicates TestResults available to be fetched
// </summary>
// ---------------------------------------------------------------------------
using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;

namespace Microsoft.VisualStudio.TestPlatform.ObjectModel.Client
{
    public class TestRunChangedEventArgs : EventArgs
    {
        public TestRunChangedEventArgs(ITestRunStatistics stats, IEnumerable<TestResult> newTestResults, IEnumerable<TestCase> activeTests)
        {
            ValidateArg.NotNull(stats, "stats");

            TestRunStatistics = stats;
            NewTestResults = newTestResults;
            ActiveTests = activeTests;
        }

        public IEnumerable<TestResult> NewTestResults { get; private set; }

        public ITestRunStatistics TestRunStatistics { get; private set; }

        public IEnumerable<TestCase> ActiveTests { get; private set; }
    }
}
