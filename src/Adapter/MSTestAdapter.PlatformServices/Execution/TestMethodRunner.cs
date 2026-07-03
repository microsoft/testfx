// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Extensions;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Helpers;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Extensions;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.VisualStudio.TestTools.UnitTesting.Internal;

using UTF = Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution;

/// <summary>
/// This class is responsible to running tests and converting framework TestResults to adapter TestResults.
/// </summary>
[StackTraceHidden]
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
    /// Cached <see cref="ReflectionTestMethodInfo"/> wrapper reused across data rows.
    /// <see cref="_testMethodInfo"/>.MethodInfo and <see cref="_test"/>.DisplayName are constant
    /// for the lifetime of this runner, so a single instance is sufficient.
    /// </summary>
    private ReflectionTestMethodInfo? _cachedReflectionMethodInfo;

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
    internal async Task<TestResult[]> ExecuteAsync(string? initializationLogs, string? initializationErrorLogs, string? initializationTrace, string? initializationTestContextMessages)
    {
        _testContext.Context.TestRunCount++;

        TestResult[]? result = null;

        try
        {
            result = await RunTestMethodAsync().ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            // NOTE: We intentionally don't have any special casing for TestFailedException in this code path.
            // It's handled down by TestMethodInfo which also unwraps TargetInvocationException.
            // RunTestMethodAsync is not supposed to throw any exceptions. So it's always an **error** if we got an exception here.
            result =
            [
                new TestResult
                {
                    Outcome = UnitTestOutcome.Error,
                    TestFailureException = new TestFailedException(UnitTestOutcome.Error, ex.TryGetMessage(), ex.TryGetStackTraceInformation()),
                },
            ];
        }
        finally
        {
            // Assembly initialize and class initialize logs are pre-pended to the first result.
            TestResult firstResult = result![0];
            firstResult.LogOutput = initializationLogs + firstResult.LogOutput;
            firstResult.LogError = initializationErrorLogs + firstResult.LogError;
            firstResult.DebugTrace = initializationTrace + firstResult.DebugTrace;
            firstResult.TestContextMessages = initializationTestContextMessages + firstResult.TestContextMessages;
        }

        return result;
    }

    /// <summary>
    /// Runs the test method.
    /// </summary>
    /// <returns>The test results.</returns>
    internal async Task<TestResult[]> RunTestMethodAsync()
    {
        DebugEx.Assert(_test != null, "Test should not be null.");
        DebugEx.Assert(_testMethodInfo.MethodInfo != null, "Test method should not be null.");

        if (_testMethodInfo.Executor == null)
        {
            throw ApplicationStateGuard.Unreachable();
        }

        // Fast path for non-data-driven tests (the common case).
        // A single attribute scan replaces the two scans done by TryExecuteDataSourceBasedTestsAsync and
        // TryExecuteFoldedDataDrivenTestsAsync, and avoids allocating a List<TestResult> that would
        // immediately be spread back into a TestResult[].
        if (_test.DataType != DynamicDataType.ITestDataSource && !IsDataDrivenTest())
        {
            _testContext.SetDisplayName(_test.DisplayName);
            TestResult[] testResults = await ExecuteTestAsync(_testContext, _testMethodInfo).ConfigureAwait(false);

            foreach (TestResult testResult in testResults)
            {
                if (StringEx.IsNullOrWhiteSpace(testResult.DisplayName))
                {
                    testResult.DisplayName = _test.DisplayName;
                }
            }

            UnitTestOutcome fastPathOutcome = GetAggregateOutcome(testResults);
            _testContext.SetOutcome(fastPathOutcome);

            // Set a result in case no result is present, preserving the safeguard from the slow path
            // (ExecuteAsync dereferences result[0] in its finally block).
            return testResults.Length == 0
                ?
                [
                    new TestResult
                    {
                        Outcome = fastPathOutcome,
                        TestFailureException = new TestFailedException(UnitTestOutcome.Error, Resource.UTA_NoTestResult),
                    },
                ]
                : testResults;
        }

        // Slow path for data-driven tests.
        List<TestResult> results = [];
        bool isDataDriven = false;
        if (_test.DataType == DynamicDataType.ITestDataSource)
        {
            if (_test.TestDataSourceIgnoreMessage is not null)
            {
                _testContext.SetOutcome(UnitTestOutcome.Ignored);
                return [TestResult.CreateIgnoredResult(_test.TestDataSourceIgnoreMessage)];
            }

            object?[]? data = _test.ActualData ?? DataSerializationHelper.Deserialize(_test.SerializedData);
            TestResult[] testResults = await ExecuteTestWithDataSourceAsync(_testContext, null, data, actualDataAlreadyHandledDuringDiscovery: true).ConfigureAwait(false);
            results.AddRange(testResults);
        }
        else if (await TryExecuteDataSourceBasedTestsAsync(results).ConfigureAwait(false))
        {
            isDataDriven = true;
        }
        else if (await TryExecuteFoldedDataDrivenTestsAsync(results).ConfigureAwait(false))
        {
            isDataDriven = true;
        }
        else
        {
            // IsDataDrivenTest() returned true but neither Try...Async method handled the test
            // (e.g., multiple DataSourceAttributes → TryExecuteDataSourceBasedTestsAsync returns false).
            // Execute as a normal non-data-driven test, preserving existing behavior.
            _testContext.SetDisplayName(_test.DisplayName);
            TestResult[] testResults = await ExecuteTestAsync(_testContext, _testMethodInfo).ConfigureAwait(false);

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
        UnitTestOutcome aggregateOutcome = GetAggregateOutcome(results);
        _testContext.SetOutcome(aggregateOutcome);

        // In case of data driven, set parent info in results.
        if (isDataDriven)
        {
            UpdateResultsWithParentInfo(results);
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

        return [.. results];
    }

    /// <summary>
    /// Returns <see langword="true"/> if this test method has a <see cref="DataSourceAttribute"/> or
    /// implements <see cref="UTF.ITestDataSource"/>, indicating it requires the data-driven execution path.
    /// A single attribute-cache pass checks for both, replacing the two separate scans that would otherwise
    /// be performed by <see cref="TryExecuteDataSourceBasedTestsAsync"/> and
    /// <see cref="TryExecuteFoldedDataDrivenTestsAsync"/>.
    /// </summary>
    private bool IsDataDrivenTest()
    {
        foreach (Attribute attribute in PlatformServiceProvider.Instance.ReflectionOperations.GetCustomAttributesCached(_testMethodInfo.MethodInfo))
        {
            if (attribute is DataSourceAttribute or UTF.ITestDataSource)
            {
                return true;
            }
        }

        return false;
    }

    private async Task<bool> TryExecuteDataSourceBasedTestsAsync(List<TestResult> results)
    {
        // Iterate cached attributes directly to preserve the previous semantics:
        // execute only when there is exactly one DataSourceAttribute.
        bool hasSingleDataSource = false;
        foreach (Attribute attribute in PlatformServiceProvider.Instance.ReflectionOperations.GetCustomAttributesCached(_testMethodInfo.MethodInfo))
        {
            if (attribute is not DataSourceAttribute)
            {
                continue;
            }

            if (hasSingleDataSource)
            {
                return false;
            }

            hasSingleDataSource = true;
        }

        if (!hasSingleDataSource)
        {
            return false;
        }

        await ExecuteTestFromDataSourceAttributeAsync(results).ConfigureAwait(false);
        return true;
    }

    private async Task<bool> TryExecuteFoldedDataDrivenTestsAsync(List<TestResult> results)
    {
        bool hasTestDataSource = false;
        var outerContext = (TestContextImplementation)_testContext.Context;

        foreach (Attribute attribute in PlatformServiceProvider.Instance.ReflectionOperations.GetCustomAttributesCached(_testMethodInfo.MethodInfo))
        {
            if (attribute is not UTF.ITestDataSource testDataSource)
            {
                continue;
            }

            hasTestDataSource = true;
            if (testDataSource is ITestDataSourceIgnoreCapability { IgnoreMessage: { } ignoreMessage })
            {
                results.Add(TestResult.CreateIgnoredResult(ignoreMessage));
                continue;
            }

            // This code is to execute tests. To discover the tests code is in AssemblyEnumerator.TryUnfoldITestDataSource.
            // Any change made here should be reflected in AssemblyEnumerator.TryUnfoldITestDataSource as well.
            bool dataSourceHasData = false;
            foreach (object?[] data in testDataSource.GetData(_testMethodInfo.MethodInfo))
            {
                dataSourceHasData = true;

                // Create a fresh TestContextImplementation per iteration so the folded path is
                // structurally equivalent to the unfolded path (where each row gets its own
                // context). This isolates per-row state (captured output, diagnostic messages,
                // result files, outcome, exception, property bag mutations, ...) so a leak in
                // any current or future TestContextImplementation field cannot accumulate across
                // rows. See https://github.com/microsoft/testfx/issues/7933.
                TestContextImplementation iterationContext = outerContext.CloneForDataDrivenIteration();
                try
                {
                    TestResult[] testResults = await ExecuteTestWithDataSourceAsync(iterationContext, testDataSource, data, actualDataAlreadyHandledDuringDiscovery: false).ConfigureAwait(false);

                    results.AddRange(testResults);
                }
                finally
                {
                    _testMethodInfo.SetArguments(null);
                    iterationContext.Dispose();
                }
            }

            if (!dataSourceHasData)
            {
                if (!MSTestSettings.CurrentSettings.ConsiderEmptyDataSourceAsInconclusive)
                {
                    throw testDataSource.GetExceptionForEmptyDataSource(_testMethodInfo.MethodInfo);
                }

                var inconclusiveResult = new TestResult
                {
                    Outcome = UnitTestOutcome.Inconclusive,
                };
                results.Add(inconclusiveResult);
            }
        }

        return hasTestDataSource;
    }

    private async Task ExecuteTestFromDataSourceAttributeAsync(List<TestResult> results)
    {
        var watch = Stopwatch.StartNew();

        try
        {
            IEnumerable<object>? dataRows = PlatformServiceProvider.Instance.TestDataSource.GetData(_testMethodInfo, _testContext);
            if (dataRows == null)
            {
                var inconclusiveResult = new TestResult
                {
                    Outcome = UnitTestOutcome.Inconclusive,
                    Duration = watch.Elapsed,
                };
                results.Add(inconclusiveResult);
                return;
            }

            try
            {
                int rowIndex = 0;
                var outerContext = (TestContextImplementation)_testContext.Context;

                foreach (object dataRow in dataRows)
                {
                    // Create a fresh TestContextImplementation per row for the same structural
                    // reason as in TryExecuteFoldedDataDrivenTestsAsync — each row should
                    // start with no accumulated per-test state.
                    TestContextImplementation iterationContext = outerContext.CloneForDataDrivenIteration();
                    try
                    {
                        TestResult[] testResults = await ExecuteTestWithDataRowAsync(iterationContext, dataRow, rowIndex++).ConfigureAwait(false);
                        results.AddRange(testResults);
                    }
                    finally
                    {
                        iterationContext.Dispose();
                    }
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
                Outcome = UnitTestOutcome.Error,
                TestFailureException = ex,
                Duration = watch.Elapsed,
            };
            results.Add(failedResult);
        }
    }

    private async Task<TestResult[]> ExecuteTestWithDataSourceAsync(ITestContext executionContext, UTF.ITestDataSource? testDataSource, object?[]? data, bool actualDataAlreadyHandledDuringDiscovery)
    {
        string? displayName = StringEx.IsNullOrWhiteSpace(_test.DisplayName)
            ? _test.Name
            : _test.DisplayName;

        string? displayNameFromTestDataRow = null;
        string? ignoreFromTestDataRow = null;
        if (!actualDataAlreadyHandledDuringDiscovery && data is not null &&
            TestDataSourceHelpers.TryHandleITestDataRow(data, _testMethodInfo.ParameterTypes, out data, out ignoreFromTestDataRow, out displayNameFromTestDataRow))
        {
            // Handled already.
        }
        else if (!actualDataAlreadyHandledDuringDiscovery && TestDataSourceHelpers.IsDataConsideredSingleArgumentValue(data, _testMethodInfo.ParameterTypes))
        {
            // SPECIAL CASE:
            // This condition is a duplicate of the condition in GetInvokeResultAsync.
            //
            // The known scenario we know of that shows importance of that check is if we have DynamicData using this member
            //
            // public static IEnumerable<object[]> GetData()
            // {
            //     yield return new object[] { ("Hello", "World") };
            // }
            //
            // If the test method has a single parameter which is 'object[]', then we should pass the tuple array as is.
            // Note that normally, the array in this code path represents the arguments of the test method.
            // However, GetInvokeResultAsync uses the above check to mean "the whole array is the single argument to the test method"
        }
        else if (!actualDataAlreadyHandledDuringDiscovery && data?.Length == 1 && TestDataSourceHelpers.TryHandleTupleDataSource(data[0], _testMethodInfo.ParameterTypes, out object?[] tupleExpandedToArray))
        {
            data = tupleExpandedToArray;
        }

        // PERF: Reuse the same ReflectionTestMethodInfo instance across all data rows — _testMethodInfo.MethodInfo
        // and _test.DisplayName are constant for the lifetime of this TestMethodRunner, so creating a new wrapper
        // per row is wasteful for data-driven tests with many rows. GetParameters() is also cached on the wrapper
        // (see ReflectionTestMethodInfo), so the underlying MethodInfo.GetParameters() array is allocated once total.
        if (displayNameFromTestDataRow is null && testDataSource is not null)
        {
            ReflectionTestMethodInfo reflectionMethodInfo = _cachedReflectionMethodInfo ??= new ReflectionTestMethodInfo(_testMethodInfo.MethodInfo, _test.DisplayName);
            displayName = testDataSource.GetDisplayName(reflectionMethodInfo, data)
                ?? TestDataSourceUtilities.ComputeDefaultDisplayName(reflectionMethodInfo, data)
                ?? displayName;
        }
        else
        {
            displayName = displayNameFromTestDataRow ?? displayName;
        }

        var stopwatch = Stopwatch.StartNew();
        _testMethodInfo.SetArguments(data);
        executionContext.SetTestData(data);
        executionContext.SetDisplayName(displayName);

        TestResult[] testResults = ignoreFromTestDataRow is not null
            ? [TestResult.CreateIgnoredResult(ignoreFromTestDataRow)]
            : await ExecuteTestAsync(executionContext, _testMethodInfo).ConfigureAwait(false);

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

    private async Task<TestResult[]> ExecuteTestWithDataRowAsync(ITestContext executionContext, object dataRow, int rowIndex)
    {
        string displayName = string.Format(CultureInfo.CurrentCulture, Resource.DataDrivenResultDisplayName, _test.DisplayName, rowIndex);
        Stopwatch? stopwatch = null;

        TestResult[]? testResults;
        try
        {
            stopwatch = Stopwatch.StartNew();
            executionContext.SetDataRow(dataRow);
            testResults = await ExecuteTestAsync(executionContext, _testMethodInfo).ConfigureAwait(false);
        }
        finally
        {
            stopwatch?.Stop();
            executionContext.SetDataRow(null);
        }

        foreach (TestResult testResult in testResults)
        {
            testResult.DisplayName = displayName;
            testResult.Duration = stopwatch.Elapsed;
        }

        return testResults;
    }

    private async Task<TestResult[]> ExecuteTestAsync(ITestContext executionContext, TestMethodInfo testMethodInfo)
    {
        try
        {
            var tcs = new TaskCompletionSource<TestResult[]>();

#pragma warning disable VSTHRD101 // Avoid unsupported async delegates
            ExecutionContextHelpers.RunOnContext(
                testMethodInfo.Parent.ExecutionContext ?? testMethodInfo.Parent.Parent.ExecutionContext,
                async () =>
                {
                    try
                    {
                        using (TestContextImplementation.SetCurrentTestContext(executionContext as TestContext))
                        {
                            testMethodInfo.TestContext = executionContext;
                            tcs.SetResult(await _testMethodInfo.Executor.ExecuteAsync(testMethodInfo).ConfigureAwait(false));
                        }
                    }
                    catch (Exception e)
                    {
                        tcs.SetException(e);
                    }
                });
#pragma warning restore VSTHRD101 // Avoid unsupported async delegates
            return await tcs.Task.ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            return
            [
                new TestResult
                {
                    TestFailureException = new InvalidOperationException(
                        string.Format(
                            CultureInfo.CurrentCulture,
                            Resource.UTA_ExecuteThrewException,
                            _testMethodInfo.Executor.GetType().FullName,
                            ex.ToString()),
                        ex),
                },
            ];
        }
    }

    /// <summary>
    /// Gets aggregate outcome.
    /// </summary>
    /// <param name="results">Results.</param>
    /// <returns>Aggregate outcome.</returns>
    private static UnitTestOutcome GetAggregateOutcome(IReadOnlyList<TestResult> results)
    {
        // In case results are not present, set outcome as unknown.
        if (results.Count == 0)
        {
            return UnitTestOutcome.Unknown;
        }

        // Get aggregate outcome.
        UnitTestOutcome aggregateOutcome = results[0].Outcome;
        for (int i = 1; i < results.Count; i++)
        {
            aggregateOutcome = aggregateOutcome.GetMoreImportantOutcome(results[i].Outcome);
        }

        return aggregateOutcome;
    }

    /// <summary>
    /// Updates each given result with new execution and parent execution identifiers.
    /// </summary>
    /// <param name="results">Results.</param>
    private static void UpdateResultsWithParentInfo(List<TestResult> results)
    {
        foreach (TestResult result in results)
        {
            result.ExecutionId = Guid.NewGuid();
            result.ParentExecId = Guid.NewGuid();
        }
    }
}
