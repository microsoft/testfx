// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Diagnostics.CodeAnalysis;
    using System.Globalization;
    using System.Linq;
    using System.Reflection;

    using Extensions;

    using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Helpers;
    using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;
    using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface;

    using UTF = Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// This class is responsible to running tests and converting framework TestResults to adapter TestResults.
    /// </summary>
    internal class TestMethodRunner
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
        /// Helper for reflection API's.
        /// </summary>
        private ReflectHelper reflectHelper;

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
        public TestMethodRunner(TestMethodInfo testMethodInfo, TestMethod testMethod, ITestContext testContext, bool captureDebugTraces)
            : this(testMethodInfo, testMethod, testContext, captureDebugTraces, ReflectHelper.Instance)
        {
        }

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
        /// <param name="reflectHelper">
        /// The reflect Helper object.
        /// </param>
        public TestMethodRunner(TestMethodInfo testMethodInfo, TestMethod testMethod, ITestContext testContext, bool captureDebugTraces, ReflectHelper reflectHelper)
        {
            Debug.Assert(testMethodInfo != null, "testMethodInfo should not be null");
            Debug.Assert(testMethod != null, "testMethod should not be null");
            Debug.Assert(testContext != null, "testContext should not be null");

            this.testMethodInfo = testMethodInfo;
            this.test = testMethod;
            this.testContext = testContext;
            this.captureDebugTraces = captureDebugTraces;
            this.reflectHelper = reflectHelper;
        }

        /// <summary>
        /// Executes a test
        /// </summary>
        /// <returns>The test results.</returns>
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
                        initLogs = logListener.GetAndClearStandardOutput();
                        initTrace = logListener.GetAndClearDebugTrace();
                        initErrorLogs = logListener.GetAndClearStandardError();
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

                var newResult = new UnitTestResult(new TestFailedException(UnitTestOutcome.Error, ex.TryGetMessage(), ex.TryGetStackTraceInformation()))
                {
                    StandardOut = result[result.Length - 1].StandardOut,
                    StandardError = result[result.Length - 1].StandardError,
                    DebugTrace = result[result.Length - 1].DebugTrace,
                    TestContextMessages = result[result.Length - 1].TestContextMessages,
                    Duration = result[result.Length - 1].Duration
                };
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

            List<UTF.TestResult> results = new List<UTF.TestResult>();
            var isDataDriven = false;

            if (this.testMethodInfo.TestMethodOptions.Executor != null)
            {
                if (this.test.DataType == DynamicDataType.ITestDataSource)
                {
                    var data = DataSerializationHelper.Deserialize(this.test.SerializedData);
                    var testResults = this.ExecuteTestWithDataSource(null, data);
                    results.AddRange(testResults);
                }
                else if (this.ExecuteDataSourceBasedTests(results))
                {
                    isDataDriven = true;
                }
                else
                {
                    var testResults = this.ExecuteTest(this.testMethodInfo);

                    foreach (var testResult in testResults)
                    {
                        if (string.IsNullOrWhiteSpace(testResult.DisplayName))
                        {
                            testResult.DisplayName = this.test.DisplayName;
                        }
                    }

                    results.AddRange(testResults);
                }
            }
            else
            {
                PlatformServiceProvider.Instance.AdapterTraceLogger.LogError(
                "Not able to get executor for method {0}.{1}",
                this.testMethodInfo.TestClassName,
                this.testMethodInfo.TestMethodName);
            }

            // Get aggregate outcome.
            var aggregateOutcome = this.GetAggregateOutcome(results);
            this.testContext.SetOutcome(aggregateOutcome);

            // Set a result in case no result is present.
            if (!results.Any())
            {
                results.Add(new UTF.TestResult() { Outcome = aggregateOutcome, TestFailureException = new TestFailedException(UnitTestOutcome.Error, Resource.UTA_NoTestResult) });
            }

            // In case of data driven, set parent info in results.
            if (isDataDriven)
            {
                results = this.UpdateResultsWithParentInfo(results, Guid.NewGuid());
            }

            return results.ToArray().ToUnitTestResults();
        }

        private bool ExecuteDataSourceBasedTests(List<UTF.TestResult> results)
        {
            var isDataDriven = false;

            UTF.DataSourceAttribute[] dataSourceAttribute = this.testMethodInfo.GetAttributes<UTF.DataSourceAttribute>(false);
            if (dataSourceAttribute != null && dataSourceAttribute.Length == 1)
            {
                isDataDriven = true;
                Stopwatch watch = new Stopwatch();
                watch.Start();

                try
                {
                    IEnumerable<object> dataRows = PlatformServiceProvider.Instance.TestDataSource.GetData(this.testMethodInfo, this.testContext);

                    if (dataRows == null)
                    {
                        watch.Stop();
                        var inconclusiveResult = new UTF.TestResult();
                        inconclusiveResult.Outcome = UTF.UnitTestOutcome.Inconclusive;
                        inconclusiveResult.Duration = watch.Elapsed;
                        results.Add(inconclusiveResult);
                    }
                    else
                    {
                        try
                        {
                            int rowIndex = 0;

                            foreach (object dataRow in dataRows)
                            {
                                UTF.TestResult[] testResults = this.ExecuteTestWithDataRow(dataRow, rowIndex++);
                                results.AddRange(testResults);
                            }
                        }
                        finally
                        {
                            this.testContext.SetDataConnection(null);
                            this.testContext.SetDataRow(null);
                        }
                    }
                }
                catch (Exception ex)
                {
                    watch.Stop();
                    var failedResult = new UTF.TestResult();
                    failedResult.Outcome = UTF.UnitTestOutcome.Error;
                    failedResult.TestFailureException = ex;
                    failedResult.Duration = watch.Elapsed;
                    results.Add(failedResult);
                }
            }
            else
            {
                UTF.ITestDataSource[] testDataSources = this.testMethodInfo.GetAttributes<Attribute>(false)?.Where(a => a is UTF.ITestDataSource).OfType<UTF.ITestDataSource>().ToArray();

                if (testDataSources != null && testDataSources.Length > 0)
                {
                    isDataDriven = true;
                    foreach (var testDataSource in testDataSources)
                    {
                        foreach (var data in testDataSource.GetData(this.testMethodInfo.MethodInfo))
                        {
                            try
                            {
                                var testResults = this.ExecuteTestWithDataSource(testDataSource, data);

                                results.AddRange(testResults);
                            }
                            finally
                            {
                                this.testMethodInfo.SetArguments(null);
                            }
                        }
                    }
                }
            }

            return isDataDriven;
        }

        private UTF.TestResult[] ExecuteTestWithDataSource(UTF.ITestDataSource testDataSource, object[] data)
        {
            var stopwatch = Stopwatch.StartNew();

            this.testMethodInfo.SetArguments(data);
            var testResults = this.ExecuteTest(this.testMethodInfo);
            stopwatch.Stop();

            var hasDisplayName = !string.IsNullOrWhiteSpace(this.test.DisplayName);
            foreach (var testResult in testResults)
            {
                if (testResult.Duration == TimeSpan.Zero)
                {
                    testResult.Duration = stopwatch.Elapsed;
                }

                var displayName = this.test.Name;
                if (testDataSource != null)
                {
                    displayName = testDataSource.GetDisplayName(this.testMethodInfo.MethodInfo, data);
                }
                else if (hasDisplayName)
                {
                    displayName = this.test.DisplayName;
                }

                testResult.DisplayName = displayName;
            }

            return testResults;
        }

        private UTF.TestResult[] ExecuteTestWithDataRow(object dataRow, int rowIndex)
        {
            var displayName = string.Format(CultureInfo.CurrentCulture, Resource.DataDrivenResultDisplayName, this.test.DisplayName, rowIndex);
            Stopwatch stopwatch = null;

            UTF.TestResult[] testResults = null;
            try
            {
                stopwatch = Stopwatch.StartNew();
                this.testContext.SetDataRow(dataRow);
                testResults = this.ExecuteTest(this.testMethodInfo);
            }
            finally
            {
                stopwatch?.Stop();
                this.testContext.SetDataRow(null);
            }

            foreach (var testResult in testResults)
            {
                testResult.DisplayName = displayName;
                testResult.DatarowIndex = rowIndex;
                testResult.Duration = stopwatch.Elapsed;
            }

            return testResults;
        }

        private UTF.TestResult[] ExecuteTest(TestMethodInfo testMethodInfo)
        {
            try
            {
                return this.testMethodInfo.TestMethodOptions.Executor.Execute(testMethodInfo);
            }
            catch (Exception ex)
            {
                return new[]
                {
                    new UTF.TestResult()
                    {
                        TestFailureException = new Exception(string.Format(CultureInfo.CurrentCulture, Resource.UTA_ExecuteThrewException, ex?.Message, ex?.StackTrace), ex)
                    }
                };
            }
        }

        /// <summary>
        /// Gets aggregate outcome.
        /// </summary>
        /// <param name="results">Results.</param>
        /// <returns>Aggregate outcome.</returns>
        private UTF.UnitTestOutcome GetAggregateOutcome(List<UTF.TestResult> results)
        {
            // In case results are not present, set outcome as unknown.
            if (!results.Any())
            {
                return UTF.UnitTestOutcome.Unknown;
            }

            // Get aggregate outcome.
            var aggregateOutcome = results[0].Outcome;
            foreach (var result in results)
            {
                aggregateOutcome = UnitTestOutcomeExtensions.GetMoreImportantOutcome(aggregateOutcome, result.Outcome);
            }

            return aggregateOutcome;
        }

        /// <summary>
        /// Updates given results with parent info if results are greater than 1.
        /// Add parent results as first result in updated result.
        /// </summary>
        /// <param name="results">Results.</param>
        /// <param name="executionId">Current execution id.</param>
        /// <returns>Updated results which contains parent result as first result. All other results contains parent result info.</returns>
        private List<UTF.TestResult> UpdateResultsWithParentInfo(List<UTF.TestResult> results, Guid executionId)
        {
            // Return results in case there are no results.
            if (!results.Any())
            {
                return results;
            }

            // UpdatedResults contain parent result at first position and remaining results has parent info updated.
            var updatedResults = new List<UTF.TestResult>();

            foreach (var result in results)
            {
                result.ExecutionId = Guid.NewGuid();
                result.ParentExecId = executionId;

                updatedResults.Add(result);
            }

            return updatedResults;
        }
    }
}
