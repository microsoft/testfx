// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

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
            Debug.Assert(testMethodInfo != null, "testMethodInfo should not be null");
            Debug.Assert(testMethod != null, "testMethod should not be null");
            Debug.Assert(testContext != null, "testContext should not be null");

            this.testMethodInfo = testMethodInfo;
            this.test = testMethod;
            this.testContext = testContext;
            this.captureDebugTraces = captureDebugTraces;
        }

        /// <summary>
        /// Executes a test
        /// </summary>
        /// <returns>The test results.</returns>
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes", Justification = "Catching all exceptions that will be thrown by user code.")]
        internal UnitTestResult[] Execute()
        {
            string initLogs = string.Empty;
            string initTrace = string.Empty;
            string initErrorLogs = string.Empty;
            string inittestContextMessages = string.Empty;

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
                    }
                    finally
                    {
                        initLogs = logListener.StandardOutput;
                        initTrace = logListener.DebugTrace;
                        initErrorLogs = logListener.StandardError;
                        inittestContextMessages = this.testContext.GetAndClearDiagnosticMessages();
                    }
                }

                // Listening to log messages when running the test method with its Test Initialize and cleanup later on in the stack.
                // This allows us to differentiate logging when data driven methods are used.
                result = this.RunTestMethod();
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
                newResult.TestContextMessages = result[result.Length - 1].TestContextMessages;
                newResult.Duration = result[result.Length - 1].Duration;
                result[result.Length - 1] = newResult;
            }
            finally
            {
                var firstResult = result[0];
                firstResult.StandardOut = initLogs + firstResult.StandardOut;
                firstResult.StandardError = initErrorLogs + firstResult.StandardError;
                firstResult.DebugTrace = initTrace + firstResult.DebugTrace;
                firstResult.TestContextMessages = inittestContextMessages + firstResult.TestContextMessages;
            }

            return result;
        }

        /// <summary>
        /// Runs the test method
        /// </summary>
        /// <returns>The test results.</returns>
        [SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes", Justification = "Requirement is to handle all kinds of user exceptions and message appropriately.")]
        internal UnitTestResult[] RunTestMethod()
        {
            Debug.Assert(this.test != null, "Test should not be null.");
            Debug.Assert(this.testMethodInfo.TestMethod != null, "Test method should not be null.");

            UTF.TestResult[] results = null;

            if (this.testMethodInfo.TestMethodOptions.Executor != null)
            {
                try
                {
                    bool isDataDriven = PlatformServiceProvider.Instance.TestDataSource.HasDataDrivenTests(this.testMethodInfo);
                    if (isDataDriven)
                    {
                        results = PlatformServiceProvider.Instance.TestDataSource.RunDataDrivenTest(this.testContext.Context, this.testMethodInfo, this.test, this.testMethodInfo.TestMethodOptions.Executor);
                    }
                    else
                    {
                        results = this.testMethodInfo.TestMethodOptions.Executor.Execute(this.testMethodInfo);
                    }
                }
                catch (Exception ex)
                {
                    results = new[] { new UTF.TestResult() { TestFailureException = new Exception(string.Format(CultureInfo.CurrentCulture, Resource.UTA_ExecuteThrewException, ex.Message), ex) } };
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
                results = new[] { new UTF.TestResult() { Outcome = UTF.UnitTestOutcome.Unknown, TestFailureException = new TestFailedException(UnitTestOutcome.Error, Resource.UTA_NoTestResult) } };
            }

            return results.ToUnitTestResults();
        }
    }
}