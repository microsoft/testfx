// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Globalization;

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Extensions;

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Helpers;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using UnitTestOutcome = Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel.UnitTestOutcome;
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
    {
        DebugEx.Assert(testMethodInfo != null, "testMethodInfo should not be null");
        DebugEx.Assert(testMethod != null, "testMethod should not be null");
        DebugEx.Assert(testContext != null, "testContext should not be null");

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
        string? initializationLogs = string.Empty;
        string? initializationTrace = string.Empty;
        string? initializationErrorLogs = string.Empty;
        string? initializationTestContextMessages = string.Empty;

        UnitTestResult[]? result = null;

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
            result = [new UnitTestResult(ex)];
        }
        catch (Exception ex)
        {
            if (result == null || result.Length == 0)
            {
                result = [new UnitTestResult()];
            }

#pragma warning disable IDE0056 // Use index operator
            var newResult = new UnitTestResult(new TestFailedException(UnitTestOutcome.Error, ex.TryGetMessage(), ex.TryGetStackTraceInformation()))
            {
                StandardOut = result[result.Length - 1].StandardOut,
                StandardError = result[result.Length - 1].StandardError,
                DebugTrace = result[result.Length - 1].DebugTrace,
                TestContextMessages = result[result.Length - 1].TestContextMessages,
                Duration = result[result.Length - 1].Duration,
            };
            result[result.Length - 1] = newResult;
#pragma warning restore IDE0056 // Use index operator
        }
        finally
        {
            UnitTestResult firstResult = result![0];
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
        DebugEx.Assert(_test != null, "Test should not be null.");
        DebugEx.Assert(_testMethodInfo.TestMethod != null, "Test method should not be null.");

        List<TestResult> results = [];
        bool isDataDriven = false;
        var parentStopwatch = Stopwatch.StartNew();

        if (_testMethodInfo.TestMethodOptions.Executor != null)
        {
            if (_test.DataType == DynamicDataType.ITestDataSource)
            {
                object?[]? data = DataSerializationHelper.Deserialize(_test.SerializedData);
                TestResult[] testResults = ExecuteTestWithDataSource(null, data);
                results.AddRange(testResults);
            }
            else if (ExecuteDataSourceBasedTests(results))
            {
                isDataDriven = true;
            }
            else
            {
                TestResult[] testResults = ExecuteTest(_testMethodInfo);

                foreach (TestResult testResult in testResults)
                {
                    if (StringEx.IsNullOrWhiteSpace(testResult.DisplayName))
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
        UTF.UnitTestOutcome aggregateOutcome = GetAggregateOutcome(results);
        _testContext.SetOutcome(aggregateOutcome);

        // In case of data driven, set parent info in results.
        if (isDataDriven)
        {
            // In legacy scenario
#pragma warning disable CS0618 // Type or member is obsolete
            if (_test.TestIdGenerationStrategy == UTF.TestIdGenerationStrategy.Legacy)
            {
                parentStopwatch.Stop();
                var parentResult = new TestResult
                {
                    Outcome = aggregateOutcome,
                    Duration = parentStopwatch.Elapsed,
                    ExecutionId = Guid.NewGuid(),
                };

                results = UpdateResultsWithParentInfo(results, parentResult);
            }
#pragma warning restore CS0618 // Type or member is obsolete
            else
            {
                results = UpdateResultsWithParentInfo(results);
            }
        }

        // Set a result in case no result is present.
        if (results.Count == 0)
        {
            TestResult emptyResult = new()
            {
                Outcome = aggregateOutcome,
                TestFailureException = new TestFailedException(UnitTestOutcome.Error, Resource.UTA_NoTestResult),
            };

            results.Add(emptyResult);
        }

        return results.ToUnitTestResults();
    }

    private bool ExecuteDataSourceBasedTests(List<TestResult> results)
    {
        bool isDataDriven = false;

        DataSourceAttribute[] dataSourceAttribute = _testMethodInfo.GetAttributes<DataSourceAttribute>(false);
        if (dataSourceAttribute is { Length: 1 })
        {
            isDataDriven = true;
            Stopwatch watch = new();
            watch.Start();

            try
            {
                IEnumerable<object>? dataRows = PlatformServiceProvider.Instance.TestDataSource.GetData(_testMethodInfo, _testContext);

                if (dataRows == null)
                {
                    var inconclusiveResult = new TestResult
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
                            TestResult[] testResults = ExecuteTestWithDataRow(dataRow, rowIndex++);
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
                var failedResult = new TestResult
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
            IEnumerable<UTF.ITestDataSource>? testDataSources = _testMethodInfo.GetAttributes<Attribute>(false)?.OfType<UTF.ITestDataSource>();

            if (testDataSources != null)
            {
                foreach (UTF.ITestDataSource testDataSource in testDataSources)
                {
                    isDataDriven = true;
                    IEnumerable<object?[]>? dataSource;
                    try
                    {
                        // This code is to execute tests. To discover the tests code is in AssemblyEnumerator.ProcessTestDataSourceTests.
                        // Any change made here should be reflected in AssemblyEnumerator.ProcessTestDataSourceTests as well.
                        dataSource = testDataSource.GetData(_testMethodInfo.MethodInfo);

                        if (!dataSource.Any())
                        {
                            throw new ArgumentException(string.Format(CultureInfo.CurrentCulture, FrameworkMessages.DynamicDataIEnumerableEmpty, "GetData", testDataSource.GetType().Name));
                        }
                    }
                    catch (Exception ex) when (ex is ArgumentException && MSTestSettings.CurrentSettings.ConsiderEmptyDataSourceAsInconclusive)
                    {
                        var inconclusiveResult = new TestResult
                        {
                            Outcome = UTF.UnitTestOutcome.Inconclusive,
                        };
                        results.Add(inconclusiveResult);
                        continue;
                    }

                    foreach (object?[] data in dataSource)
                    {
                        try
                        {
                            TestResult[] testResults = ExecuteTestWithDataSource(testDataSource, data);

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

    private TestResult[] ExecuteTestWithDataSource(UTF.ITestDataSource? testDataSource, object?[]? data)
    {
        var stopwatch = Stopwatch.StartNew();

        _testMethodInfo.SetArguments(data);
        TestResult[] testResults = ExecuteTest(_testMethodInfo);
        stopwatch.Stop();

        bool hasDisplayName = !StringEx.IsNullOrWhiteSpace(_test.DisplayName);
        foreach (TestResult testResult in testResults)
        {
            if (testResult.Duration == TimeSpan.Zero)
            {
                testResult.Duration = stopwatch.Elapsed;
            }

            string? displayName = _test.Name;
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

    private TestResult[] ExecuteTestWithDataRow(object dataRow, int rowIndex)
    {
        string displayName = string.Format(CultureInfo.CurrentCulture, Resource.DataDrivenResultDisplayName, _test.DisplayName, rowIndex);
        Stopwatch? stopwatch = null;

        TestResult[]? testResults;
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

        foreach (TestResult testResult in testResults)
        {
            testResult.DisplayName = displayName;
            testResult.DatarowIndex = rowIndex;
            testResult.Duration = stopwatch.Elapsed;
        }

        return testResults;
    }

    private TestResult[] ExecuteTest(TestMethodInfo testMethodInfo)
    {
        try
        {
            return _testMethodInfo.TestMethodOptions.Executor!.Execute(testMethodInfo);
        }
        catch (Exception ex)
        {
            return
            [
                new TestResult()
                {
                    // TODO: We need to change the exception type to more specific one.
#pragma warning disable CA2201 // Do not raise reserved exception types
                    TestFailureException = new Exception(string.Format(CultureInfo.CurrentCulture, Resource.UTA_ExecuteThrewException, ex?.Message, ex?.StackTrace), ex),
#pragma warning restore CA2201 // Do not raise reserved exception types
                },
            ];
        }
    }

    /// <summary>
    /// Gets aggregate outcome.
    /// </summary>
    /// <param name="results">Results.</param>
    /// <returns>Aggregate outcome.</returns>
    private static UTF.UnitTestOutcome GetAggregateOutcome(List<TestResult> results)
    {
        // In case results are not present, set outcome as unknown.
        if (results.Count == 0)
        {
            return UTF.UnitTestOutcome.Unknown;
        }

        // Get aggregate outcome.
        UTF.UnitTestOutcome aggregateOutcome = results[0].Outcome;
        foreach (TestResult result in results)
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
    private static List<TestResult> UpdateResultsWithParentInfo(List<TestResult> results)
    {
        // Return results in case there are no results.
        if (results.Count == 0)
        {
            return results;
        }

        // UpdatedResults contain parent result at first position and remaining results has parent info updated.
        var updatedResults = new List<TestResult>();

        foreach (TestResult result in results)
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
    private static List<TestResult> UpdateResultsWithParentInfo(
        List<TestResult> results,
        TestResult parentResult)
    {
        // Return results in case there are no results.
        if (results.Count == 0)
        {
            return results;
        }

        // UpdatedResults contain parent result at first position and remaining results has parent info updated.
        List<TestResult> updatedResults = [parentResult];

        foreach (TestResult result in results)
        {
            result.ExecutionId = Guid.NewGuid();
            result.ParentExecId = parentResult.ExecutionId;
            parentResult.InnerResultsCount++;

            updatedResults.Add(result);
        }

        return updatedResults;
    }
}
