// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Extensions;

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Helpers;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.VisualStudio.TestTools.UnitTesting.Internal;

using UnitTestOutcome = Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel.UnitTestOutcome;
using UTF = Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution;

/// <summary>
/// This class is responsible to running tests and converting framework TestResults to adapter TestResults.
/// </summary>
internal sealed class TestMethodRunner
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
    public TestMethodRunner(TestMethodInfo testMethodInfo, TestMethod testMethod, ITestContext testContext)
    {
        DebugEx.Assert(testMethodInfo != null, "testMethodInfo should not be null");
        DebugEx.Assert(testMethod != null, "testMethod should not be null");
        DebugEx.Assert(testContext != null, "testContext should not be null");

        _testMethodInfo = testMethodInfo;
        _test = testMethod;
        _testContext = testContext;
    }

    /// <summary>
    /// Executes a test.
    /// </summary>
    /// <returns>The test results.</returns>
    internal UnitTestResult[] Execute(string initializationLogs, string initializationErrorLogs, string initializationTrace, string initializationTestContextMessages)
    {
        bool isSTATestClass = AttributeComparer.IsDerived<STATestClassAttribute>(_testMethodInfo.Parent.ClassAttribute);
        bool isSTATestMethod = AttributeComparer.IsDerived<STATestMethodAttribute>(_testMethodInfo.TestMethodOptions.Executor);
        bool isSTARequested = isSTATestClass || isSTATestMethod;
        bool isWindowsOS = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
        if (isSTARequested && isWindowsOS && Thread.CurrentThread.GetApartmentState() != ApartmentState.STA)
        {
            UnitTestResult[] results = Array.Empty<UnitTestResult>();
            Thread entryPointThread = new(() => results = SafeRunTestMethod(initializationLogs, initializationErrorLogs, initializationTrace, initializationTestContextMessages))
            {
                Name = (isSTATestClass, isSTATestMethod) switch
                {
                    (true, _) => "MSTest STATestClass",
                    (_, true) => "MSTest STATestMethod",
                    _ => throw ApplicationStateGuard.Unreachable(),
                },
            };

            entryPointThread.SetApartmentState(ApartmentState.STA);
            entryPointThread.Start();

            try
            {
                entryPointThread.Join();
            }
            catch (Exception ex)
            {
                PlatformServiceProvider.Instance.AdapterTraceLogger.LogError(ex.ToString());
            }

            return results;
        }
        else
        {
            // If the requested apartment state is STA and the OS is not Windows, then warn the user.
            if (!isWindowsOS && isSTARequested)
            {
                PlatformServiceProvider.Instance.AdapterTraceLogger.LogWarning(Resource.STAIsOnlySupportedOnWindowsWarning);
            }

            return SafeRunTestMethod(initializationLogs, initializationErrorLogs, initializationTrace, initializationTestContextMessages);
        }

        // Local functions
        UnitTestResult[] SafeRunTestMethod(string initializationLogs, string initializationErrorLogs, string initializationTrace, string initializationTestContextMessages)
        {
            UnitTestResult[]? result = null;

            try
            {
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
                    result = [new()];
                }

#pragma warning disable IDE0056 // Use index operator
                result[result.Length - 1] = new UnitTestResult(new TestFailedException(UnitTestOutcome.Error, ex.TryGetMessage(), ex.TryGetStackTraceInformation()))
                {
                    StandardOut = result[result.Length - 1].StandardOut,
                    StandardError = result[result.Length - 1].StandardError,
                    DebugTrace = result[result.Length - 1].DebugTrace,
                    TestContextMessages = result[result.Length - 1].TestContextMessages,
                    Duration = result[result.Length - 1].Duration,
                };
#pragma warning restore IDE0056 // Use index operator
            }
            finally
            {
                // Assembly initialize and class initialize logs are pre-pended to the first result.
                UnitTestResult firstResult = result![0];
                firstResult.StandardOut = initializationLogs + firstResult.StandardOut;
                firstResult.StandardError = initializationErrorLogs + firstResult.StandardError;
                firstResult.DebugTrace = initializationTrace + firstResult.DebugTrace;
                firstResult.TestContextMessages = initializationTestContextMessages + firstResult.TestContextMessages;
            }

            return result;
        }
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
        if (_testMethodInfo.TestMethodOptions.Executor == null)
        {
            throw ApplicationStateGuard.Unreachable();
        }

        bool isDataDriven = false;
        var parentStopwatch = Stopwatch.StartNew();
        if (_test.DataType == DynamicDataType.ITestDataSource)
        {
            if (_test.TestDataSourceIgnoreMessage is not null)
            {
                return [new(UnitTestOutcome.Ignored, _test.TestDataSourceIgnoreMessage)];
            }

            object?[]? data = DataSerializationHelper.Deserialize(_test.SerializedData);
            TestResult[] testResults = ExecuteTestWithDataSource(null, data);
            results.AddRange(testResults);
        }
        else if (TryExecuteDataSourceBasedTests(results))
        {
            isDataDriven = true;
        }
        else if (TryExecuteFoldedDataDrivenTests(results))
        {
            isDataDriven = true;
        }
        else
        {
            _testContext.SetDisplayName(_test.DisplayName);
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

        // Get aggregate outcome.
        UTF.UnitTestOutcome aggregateOutcome = GetAggregateOutcome(results);
        _testContext.SetOutcome(aggregateOutcome);

        // In case of data driven, set parent info in results.
        if (isDataDriven)
        {
            // In legacy scenario
#pragma warning disable CS0618 // Type or member is obsolete
            if (_test.TestIdGenerationStrategy == TestIdGenerationStrategy.Legacy)
#pragma warning restore CS0618 // Type or member is obsolete
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

    private bool TryExecuteDataSourceBasedTests(List<TestResult> results)
    {
        DataSourceAttribute[] dataSourceAttribute = _testMethodInfo.GetAttributes<DataSourceAttribute>(false);
        if (dataSourceAttribute is { Length: 1 })
        {
            ExecuteTestFromDataSourceAttribute(results);
            return true;
        }

        return false;
    }

    private bool TryExecuteFoldedDataDrivenTests(List<TestResult> results)
    {
        IEnumerable<UTF.ITestDataSource>? testDataSources = _testMethodInfo.GetAttributes<Attribute>(false)?.OfType<UTF.ITestDataSource>();
        if (testDataSources?.Any() != true)
        {
            return false;
        }

        foreach (UTF.ITestDataSource testDataSource in testDataSources)
        {
            if (testDataSource is ITestDataSourceIgnoreCapability { IgnoreMessage: { } ignoreMessage })
            {
                results.Add(new()
                {
                    Outcome = UTF.UnitTestOutcome.Ignored,
                    IgnoreReason = ignoreMessage,
                });
                continue;
            }

            IEnumerable<object?[]>? dataSource;

            // This code is to execute tests. To discover the tests code is in AssemblyEnumerator.ProcessTestDataSourceTests.
            // Any change made here should be reflected in AssemblyEnumerator.ProcessTestDataSourceTests as well.
            dataSource = testDataSource.GetData(_testMethodInfo.MethodInfo);

            if (!dataSource.Any())
            {
                if (!MSTestSettings.CurrentSettings.ConsiderEmptyDataSourceAsInconclusive)
                {
                    throw testDataSource.GetExceptionForEmptyDataSource(_testMethodInfo.MethodInfo);
                }

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

        return true;
    }

    private void ExecuteTestFromDataSourceAttribute(List<TestResult> results)
    {
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
                return;
            }

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

    private TestResult[] ExecuteTestWithDataSource(UTF.ITestDataSource? testDataSource, object?[]? data)
    {
        string? displayName = StringEx.IsNullOrWhiteSpace(_test.DisplayName)
            ? _test.Name
            : _test.DisplayName;
        if (testDataSource != null)
        {
            displayName = testDataSource.GetDisplayName(new ReflectionTestMethodInfo(_testMethodInfo.MethodInfo, _test.DisplayName), data);
        }

        var stopwatch = Stopwatch.StartNew();
        _testMethodInfo.SetArguments(data);
        _testContext.SetTestData(data);
        _testContext.SetDisplayName(displayName);
        TestResult[] testResults = ExecuteTest(_testMethodInfo);
        stopwatch.Stop();

        foreach (TestResult testResult in testResults)
        {
            if (testResult.Duration == TimeSpan.Zero)
            {
                testResult.Duration = stopwatch.Elapsed;
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
            return _testMethodInfo.TestMethodOptions.Executor.Execute(testMethodInfo);
        }
        catch (Exception ex)
        {
            return
            [
                new TestResult()
                {
                    // TODO: We need to change the exception type to more specific one.
#pragma warning disable CA2201 // Do not raise reserved exception types
                    TestFailureException = new Exception(string.Format(CultureInfo.CurrentCulture, Resource.UTA_ExecuteThrewException, ex.Message, ex.StackTrace), ex),
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
            aggregateOutcome = aggregateOutcome.GetMoreImportantOutcome(result.Outcome);
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
