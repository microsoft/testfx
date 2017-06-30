// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Extensions
{
    using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;

    using UTF = Microsoft.VisualStudio.TestTools.UnitTesting;

    public static class TestResultExtensions
    {
        /// <summary>
        /// Converts the test framework's TestResult objects array to a serializable UnitTestResult objects array.
        /// </summary>
        /// <param name="testResults">The test framework's TestResult object array.</param>
        /// <returns>The serializable UnitTestResult object array.</returns>
        public static UnitTestResult[] ToUnitTestResults(this UTF.TestResult[] testResults)
        {
            UnitTestResult[] unitTestResults = new UnitTestResult[testResults.Length];

            for (int i = 0; i < testResults.Length; ++i)
            {
                UnitTestResult unitTestResult = null;
                UnitTestOutcome outcome = testResults[i].Outcome.ToUnitTestOutcome();

                if (testResults[i].TestFailureException != null)
                {
                    TestFailedException testException = testResults[i].TestFailureException as TestFailedException;

                    unitTestResult =
                        new UnitTestResult(
                            new TestFailedException(
                                outcome,
                                testResults[i].TestFailureException.TryGetMessage(),
                                testException != null ? testException.StackTraceInformation : testResults[i].TestFailureException.TryGetStackTraceInformation()));
                }
                else
                {
                    unitTestResult = new UnitTestResult { Outcome = outcome };
                }

                unitTestResult.StandardOut = testResults[i].LogOutput;
                unitTestResult.StandardError = testResults[i].LogError;
                unitTestResult.DebugTrace = testResults[i].DebugTrace;
                unitTestResult.TestContextMessages = testResults[i].TestContextMessages;
                unitTestResult.Duration = testResults[i].Duration;
                unitTestResult.DisplayName = testResults[i].DisplayName;
                unitTestResult.DatarowIndex = testResults[i].DatarowIndex;
                unitTestResult.ResultFiles = testResults[i].ResultFiles;
                unitTestResults[i] = unitTestResult;
            }

            return unitTestResults;
        }

        /// <summary>
        /// Converts the test framework's UnitTestOutcome object to adapter's UnitTestOutcome object.
        /// </summary>
        /// <param name="frameworkTestOutcome">The test framework's UnitTestOutcome object.</param>
        /// <returns>The adapter's UnitTestOutcome object.</returns>
        public static UnitTestOutcome ToUnitTestOutcome(this UTF.UnitTestOutcome frameworkTestOutcome)
        {
            UnitTestOutcome outcome = UnitTestOutcome.Passed;

            switch (frameworkTestOutcome)
            {
                case UTF.UnitTestOutcome.Failed:
                    outcome = UnitTestOutcome.Failed;
                    break;

                case UTF.UnitTestOutcome.Inconclusive:
                    outcome = UnitTestOutcome.Inconclusive;
                    break;

                case UTF.UnitTestOutcome.InProgress:
                    outcome = UnitTestOutcome.InProgress;
                    break;

                case UTF.UnitTestOutcome.Passed:
                    outcome = UnitTestOutcome.Passed;
                    break;

                case UTF.UnitTestOutcome.Timeout:
                    outcome = UnitTestOutcome.Timeout;
                    break;

                case UTF.UnitTestOutcome.Unknown:
                default:
                    outcome = UnitTestOutcome.Error;
                    break;
            }

            return outcome;
        }
    }
}
