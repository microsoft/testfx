// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Extensions;

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Helpers;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface;

using UTF = Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution;

/// <summary>
/// This class is responsible to running tests and converting framework TestResults to adapter TestResults.
/// </summary>
internal class TestMethodRunner
{
    /// <summary>
    /// Test context which needs to be passed to the various methods of the test.
    /// </summary>
    private readonly ITestContext _testContext;

    /// <summary>
    /// TestMethod that needs to be executed.
    /// </summary>
    private readonly TestMethod _test;

    /// <summary>
    /// TestMethod referred by the above test element.
    /// </summary>
    private readonly TestMethodInfo _testMethodInfo;

    /// <summary>
    /// Specifies whether debug traces should be captured or not.
    /// </summary>
    private readonly bool _captureDebugTraces;

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

        _testMethodInfo = testMethodInfo;
        _test = testMethod;
        _testContext = testContext;
        _captureDebugTraces = captureDebugTraces;
    }

    /// <summary>
    /// Executes a test.
    /// </summary>
    /// <returns>The test results.</returns>
    internal UnitTestResult[] Execute()
    {
        string initializationLogs = string.Empty;
        string initializationTrace = string.Empty;
        string initializationErrorLogs = string.Empty;
        string initializationTestContextMessages = string.Empty;

        UnitTestResult[] result = null;

        try
        {
            using (LogMessageListener logListener = new(_captureDebugTraces))
            {
                try
                {
                    // Run the assembly and class Initialize methods if required.
                    // Assembly or class initialize can throw exceptions in which case we need to ensure that we fail the test.
                    _testMethodInfo.Parent.Parent.RunAssemblyInitialize(_testContext.Context);
                    _testMethodInfo.Parent.RunClassInitialize(_testContext.Context);
                }
                finally
                {
                    initializationLogs = logListener.GetAndClearStandardOutput();
                    initializationTrace = logListener.GetAndClearDebugTrace();
                    initializationErrorLogs = logListener.GetAndClearStandardError();
                    initializationTestContextMessages = _testContext.GetAndClearDiagnosticMessages();
                }
            }

            // Listening to log messages when running the test method with its Test Initialize and cleanup later on in the stack.
            // This allows us to differentiate logging when data driven methods are used.
            result = RunTestMethod();
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
                Duration = result[result.Length - 1].Duration,
            };
            result[result.Length - 1] = newResult;
        }
        finally
        {
            var firstResult = result[0];
            firstResult.StandardOut = initializationLogs + firstResult.StandardOut;
            firstResult.StandardError = initializationErrorLogs + firstResult.StandardError;
            firstResult.DebugTrace = initializationTrace + firstResult.DebugTrace;
            firstResult.TestContextMessages = initializationTestContextMessages + firstResult.TestContextMessages;
        }

        return result;
    }

    /// <summary>
    /// Runs the test method.
    /// </summary>
    /// <returns>The test results.</returns>
    internal UnitTestResult[] RunTestMethod()
    {
        Debug.Assert(_test != null, "Test should not be null.");
        Debug.Assert(_testMethodInfo.TestMethod != null, "Test method should not be null.");

        List<UTF.TestResult> results = new();
        var isDataDriven = false;
        var parentStopwatch = Stopwatch.StartNew();

        if (_testMethodInfo.TestMethodOptions.Executor != null)
        {
            if (_test.DataType == DynamicDataType.ITestDataSource)
            {
                var data = DataSerializationHelper.Deserialize(_test.SerializedData);
                var testResults = ExecuteTestWithDataSource(null, data);
                results.AddRange(testResults);
            }
            else if (ExecuteDataSourceBasedTests(results))
            {
                isDataDriven = true;
            }
            else
            {
                var testResults = ExecuteTest(_testMethodInfo);

                foreach (var testResult in testResults)
                {
                    if (string.IsNullOrWhiteSpace(testResult.DisplayName))
                    {
                        testResult.DisplayName = _test.DisplayName;
                    }
                }

                results.AddRange(testResults);
            }
        }
        else
        {
            PlatformServiceProvider.Instance.AdapterTraceLogger.LogError(
            "Not able to get executor for method {0}.{1}",
            _testMethodInfo.TestClassName,
            _testMethodInfo.TestMethodName);
        }

        // Get aggregate outcome.
        var aggregateOutcome = GetAggregateOutcome(results);
        _testContext.SetOutcome(aggregateOutcome);

        // In case of data driven, set parent info in results.
        if (isDataDriven)
        {
            // In legacy scenario
            if (_test.TestIdGenerationStrategy == UTF.TestIdGenerationStrategy.Legacy)
            {
                parentStopwatch.Stop();
                var parentResult = new UTF.TestResult
                {
                    Outcome = aggregateOutcome,
                    Duration = parentStopwatch.Elapsed,
                    ExecutionId = Guid.NewGuid(),
                };

                results = UpdateResultsWithParentInfo(results, parentResult);
            }
            else
            {
                results = UpdateResultsWithParentInfo(results);
            }
        }

        // Set a result in case no result is present.
        if (results.Count == 0)
        {
            UTF.TestResult emptyResult = new()
            {
                Outcome = aggregateOutcome,
                TestFailureException = new TestFailedException(UnitTestOutcome.Error, Resource.UTA_NoTestResult),
            };

            results.Add(emptyResult);
        }

        var unitTestResults = results
            .ToArray()
            .ToUnitTestResults();

        return unitTestResults;
    }

    private bool ExecuteDataSourceBasedTests(List<UTF.TestResult> results)
    {
        var isDataDriven = false;

        UTF.DataSourceAttribute[] dataSourceAttribute = _testMethodInfo.GetAttributes<UTF.DataSourceAttribute>(false);
        if (dataSourceAttribute != null && dataSourceAttribute.Length == 1)
        {
            isDataDriven = true;
            Stopwatch watch = new();
            watch.Start();

            try
            {
                IEnumerable<object> dataRows = PlatformServiceProvider.Instance.TestDataSource.GetData(_testMethodInfo, _testContext);

                if (dataRows == null)
                {
                    watch.Stop();
                    var inconclusiveResult = new UTF.TestResult
                    {
                        Outcome = UTF.UnitTestOutcome.Inconclusive,
                        Duration = watch.Elapsed,
                    };
                    results.Add(inconclusiveResult);
                }
                else
                {
                    try
                    {
                        int rowIndex = 0;

                        foreach (object dataRow in dataRows)
                        {
                            UTF.TestResult[] testResults = ExecuteTestWithDataRow(dataRow, rowIndex++);
                            results.AddRange(testResults);
                        }
                    }
                    finally
                    {
                        _testContext.SetDataConnection(null);
                        _testContext.SetDataRow(null);
                    }
                }
            }
            catch (Exception ex)
            {
                watch.Stop();
                var failedResult = new UTF.TestResult
                {
                    Outcome = UTF.UnitTestOutcome.Error,
                    TestFailureException = ex,
                    Duration = watch.Elapsed,
                };
                results.Add(failedResult);
            }
        }
        else
        {
            UTF.ITestDataSource[] testDataSources = _testMethodInfo.GetAttributes<Attribute>(false)?.Where(a => a is UTF.ITestDataSource).OfType<UTF.ITestDataSource>().ToArray();

            if (testDataSources != null && testDataSources.Length > 0)
            {
                isDataDriven = true;
                foreach (var testDataSource in testDataSources)
                {
                    foreach (var data in testDataSource.GetData(_testMethodInfo.MethodInfo))
                    {
                        try
                        {
                            var testResults = ExecuteTestWithDataSource(testDataSource, data);

                            results.AddRange(testResults);
                        }
                        finally
                        {
                            _testMethodInfo.SetArguments(null);
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

        _testMethodInfo.SetArguments(data);
        var testResults = ExecuteTest(_testMethodInfo);
        stopwatch.Stop();

        var hasDisplayName = !string.IsNullOrWhiteSpace(_test.DisplayName);
        foreach (var testResult in testResults)
        {
            if (testResult.Duration == TimeSpan.Zero)
            {
                testResult.Duration = stopwatch.Elapsed;
            }

            var displayName = _test.Name;
            if (testDataSource != null)
            {
                displayName = testDataSource.GetDisplayName(_testMethodInfo.MethodInfo, data);
            }
            else if (hasDisplayName)
            {
                displayName = _test.DisplayName;
            }

            testResult.DisplayName = displayName;
        }

        return testResults;
    }

    private UTF.TestResult[] ExecuteTestWithDataRow(object dataRow, int rowIndex)
    {
        var displayName = string.Format(CultureInfo.CurrentCulture, Resource.DataDrivenResultDisplayName, _test.DisplayName, rowIndex);
        Stopwatch stopwatch = null;

        UTF.TestResult[] testResults = null;
        try
        {
            stopwatch = Stopwatch.StartNew();
            _testContext.SetDataRow(dataRow);
            testResults = ExecuteTest(_testMethodInfo);
        }
        finally
        {
            stopwatch?.Stop();
            _testContext.SetDataRow(null);
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
            return _testMethodInfo.TestMethodOptions.Executor.Execute(testMethodInfo);
        }
        catch (Exception ex)
        {
            return new[]
            {
                new UTF.TestResult()
                {
                    TestFailureException = new Exception(string.Format(CultureInfo.CurrentCulture, Resource.UTA_ExecuteThrewException, ex?.Message, ex?.StackTrace), ex),
                },
            };
        }
    }

    /// <summary>
    /// Gets aggregate outcome.
    /// </summary>
    /// <param name="results">Results.</param>
    /// <returns>Aggregate outcome.</returns>
    private static UTF.UnitTestOutcome GetAggregateOutcome(List<UTF.TestResult> results)
    {
        // In case results are not present, set outcome as unknown.
        if (results.Count == 0)
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
    /// <returns>Updated results which contains parent result as first result. All other results contains parent result info.</returns>
    private static List<UTF.TestResult> UpdateResultsWithParentInfo(List<UTF.TestResult> results)
    {
        // Return results in case there are no results.
        if (results.Count == 0)
        {
            return results;
        }

        // UpdatedResults contain parent result at first position and remaining results has parent info updated.
        var updatedResults = new List<UTF.TestResult>();

        foreach (var result in results)
        {
            result.ExecutionId = Guid.NewGuid();
            result.ParentExecId = Guid.NewGuid();

            updatedResults.Add(result);
        }

        return updatedResults;
    }

    /// <summary>
    /// Updates given results with parent info if results are greater than 1.
    /// Add parent results as first result in updated result.
    /// </summary>
    /// <param name="results">Results.</param>
    /// <param name="parentResult">Parent results.</param>
    /// <returns>Updated results which contains parent result as first result. All other results contains parent result info.</returns>
    private static List<UTF.TestResult> UpdateResultsWithParentInfo(
        List<UTF.TestResult> results,
        UTF.TestResult parentResult)
    {
        // Return results in case there are no results.
        if (results.Count == 0)
        {
            return results;
        }

        // UpdatedResults contain parent result at first position and remaining results has parent info updated.
        List<UTF.TestResult> updatedResults = new() { parentResult };

        foreach (var result in results)
        {
            result.ExecutionId = Guid.NewGuid();
            result.ParentExecId = parentResult.ExecutionId;
            parentResult.InnerResultsCount++;

            updatedResults.Add(result);
        }

        return updatedResults;
    }
}
