// Copyright (c) Microsoft Corporation. All rights reserved.

namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution
{
    using System;
    using System.Diagnostics;
    using System.Diagnostics.CodeAnalysis;
    using System.Globalization;

    using Extensions;
    using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter;
    using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;
    using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface;
    using Microsoft.VisualStudio.TestPlatform.ObjectModel;
    using MSTestAdapter.PlatformServices;

    using UTF = Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// This class is responsible to running tests and converting framework TestResults to adapter TestResults.
    /// </summary>
    public class TestMethodRunner
    {
        /// <summary>
        /// Test context which needs to be passed to the various methods of the test
        /// </summary>
        private readonly ITestContext testContext;

        /// <summary>
        /// TestMethod that needs to be executed. 
        /// </summary>
        private readonly TestMethod test;

        /// <summary>
        /// TestMethod referred by the above test element
        /// </summary>
        private readonly TestMethodInfo testMethodInfo;

        /// <summary>
        /// Specifies whether debug traces should be captured or not
        /// </summary>
        private readonly bool captureDebugTraces;

        /// <summary>
        /// Initializes a new instance of the <see cref="TestMethodRunner"/> class.
        /// </summary>
        /// <param name="testMethodInfo">
        /// The test method info.
        /// </param>
        /// <param name="testMethod">
        /// The test method.
        /// </param>
        /// <param name="testContext">
        /// The test context.
        /// </param>
        /// <param name="captureDebugTraces">
        /// The capture debug traces.
        /// </param>
        public TestMethodRunner(
            TestMethodInfo testMethodInfo,
            TestMethod testMethod,
            ITestContext testContext,
            bool captureDebugTraces)
        {
            Debug.Assert(testMethodInfo != null);
            Debug.Assert(testMethod != null);
            Debug.Assert(testContext != null);

            this.testMethodInfo = testMethodInfo;
            this.test = testMethod;
            this.testContext = testContext;
            this.captureDebugTraces = captureDebugTraces;
        }


        /// <summary>
        /// Executes a test
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes",
            Justification = "Catching all exceptions that will be thrown by user code.")]
        internal UnitTestResult[] Execute()
        {
            string initLogs = string.Empty;
            string initTrace = string.Empty;
            string errorLogs = string.Empty;

            UnitTestResult[] result = null;
            try
            {

                using (LogMessageListener logListener = new LogMessageListener(this.captureDebugTraces))
                {
                    try
                    {
                        // Run the assembly and class Initialize methods if required.
                        // Assembly or class initialize can throw exceptions in which case we need to ensure that we fail the test.
                        this.testMethodInfo.Parent.Parent.RunAssemblyInitialize(this.testContext.Context);
                        this.testMethodInfo.Parent.RunClassInitialize(this.testContext.Context);

                        result = this.RunTestMethod();
                    }
            finally
                    {
                        initLogs = logListener.StandardOutput;
                        initTrace = logListener.DebugTrace;
                        errorLogs = logListener.StandardError;
                    }
                }
            }
            catch (TestFailedException ex)
            {
                result = new[] { new UnitTestResult(ex) };
            }
            catch (Exception ex)
            {
                if (result == null || result.Length == 0)
                {
                    result = new[] { new UnitTestResult() };
                }

                var newResult =
                    new UnitTestResult(new TestFailedException(UnitTestOutcome.Error, ex.TryGetMessage(), ex.TryGetStackTraceInformation()));
                newResult.StandardOut = result[result.Length - 1].StandardOut;
                newResult.StandardError = result[result.Length - 1].StandardError;
                newResult.DebugTrace = result[result.Length - 1].DebugTrace;
                newResult.Duration = result[result.Length - 1].Duration;
                result[result.Length - 1] = newResult;
            }
            finally
            {
                var firstResult = result[0];
                firstResult.StandardOut = initLogs + firstResult.StandardOut;
                firstResult.StandardError = errorLogs + firstResult.StandardError;
                firstResult.DebugTrace = initTrace + firstResult.DebugTrace;
            }
            return result;
        }

        /// <summary>
        /// Runs the test method
        /// </summary>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Usage", "CA2201:DoNotRaiseReservedExceptionTypes")]
        [SuppressMessage("Microsoft.Design", "CA1031")]
        internal UnitTestResult[] RunTestMethod()
        {
            Debug.Assert(this.test != null);
            Debug.Assert(this.testMethodInfo.TestMethod != null);
            
            UTF.TestResult[] results = null;

            if (this.testMethodInfo.Executor != null)
            {
                try
                {
                    bool isDataDriven = PlatformServiceProvider.Instance.TestDataSource.HasDataDrivenTests(this.testMethodInfo);
                    if (isDataDriven)
                    {
                        results = PlatformServiceProvider.Instance.TestDataSource.RunDataDrivenTest(this.testContext.Context, this.testMethodInfo, this.test, this.testMethodInfo.Executor);
                    }
                    else
                    {
                        results = this.testMethodInfo.Executor.Execute(this.testMethodInfo);
                    }
                }
                catch (Exception ex)
                {
                    results = new[] { new UTF.TestResult() { TestFailureException = new Exception(string.Format( CultureInfo.CurrentCulture, Resource.UTA_ExecuteThrewException, ex.Message),ex)} };
                }
            }
            else
            {
                PlatformServiceProvider.Instance.AdapterTraceLogger.LogError(
                    "Not able to get executor for method {0}.{1}",
                    this.testMethodInfo.TestClassName,
                    this.testMethodInfo.TestMethodName);
            }

            if (results != null && results.Length > 0)
            {
                // aggregate for data driven tests
                UTF.UnitTestOutcome aggregateOutcome = UTF.UnitTestOutcome.Passed;

                foreach (var result in results)
                {
                    if (result.Outcome != UTF.UnitTestOutcome.Passed)
                    {
                        if (aggregateOutcome != UTF.UnitTestOutcome.Failed)
                        {
                            if (result.Outcome == UTF.UnitTestOutcome.Failed
                                || aggregateOutcome != UTF.UnitTestOutcome.Timeout)
                            {
                                aggregateOutcome = result.Outcome;
                            }
                        }
                    }
                }

                this.testContext.SetOutcome(aggregateOutcome);
            }
            else
            {
                this.testContext.SetOutcome(UTF.UnitTestOutcome.Unknown);
                results = new[] { new UTF.TestResult() { Outcome = UTF.UnitTestOutcome.Unknown, TestFailureException = new TestFailedException( UnitTestOutcome.Error, Resource.UTA_NoTestResult) } };
            }

            return this.ConvertTestResultToUnitTestResult(results);
        }

        /// <summary>
        /// The convert test result to unit test result.
        /// </summary>
        /// <param name="results">
        /// The results.
        /// </param>
        /// <returns>
        /// converted UnitTestResult array
        /// </returns>
        internal UnitTestResult[] ConvertTestResultToUnitTestResult(UTF.TestResult[] results)
        {
            UnitTestResult[] unitTestResults = new UnitTestResult[results.Length];

            for (int i = 0; i < results.Length; ++i)
            {
                UnitTestResult unitTestResult = null;
                UnitTestOutcome outcome = UnitTestOutcome.Passed;

                switch (results[i].Outcome)
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

                if (results[i].TestFailureException != null)
                {
                    TestFailedException testException = results[i].TestFailureException as TestFailedException;

                    unitTestResult =
                        new UnitTestResult(
                            new TestFailedException(
                                outcome,
                                results[i].TestFailureException.TryGetMessage(),
                                testException != null
                                    ? testException.StackTraceInformation
                                    : results[i].TestFailureException.TryGetStackTraceInformation()));
                }
                else
                {
                    unitTestResult = new UnitTestResult { Outcome = outcome };
                }

                unitTestResult.StandardOut = results[i].LogOutput;
                unitTestResult.StandardError = results[i].LogError;
                unitTestResult.DebugTrace = results[i].DebugTrace;
                unitTestResult.Duration = results[i].Duration;
                unitTestResult.DisplayName = results[i].DisplayName;
                unitTestResult.DatarowIndex = results[i].DatarowIndex;
                unitTestResult.ResultFiles = testContext.GetResultFiles();
                unitTestResults[i] = unitTestResult;
            }

            return unitTestResults;
        }
    }
}