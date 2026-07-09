// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Extensions;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Helpers;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;
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
internal sealed partial class TestMethodRunner
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
    /// Cached <see cref="ReflectionTestMethodInfo"/> wrapper reused across all data rows of a
    /// data-driven test. Both <see cref="TestMethodInfo.MethodInfo"/> and <see cref="TestMethod.DisplayName"/>
    /// are immutable for the lifetime of this <see cref="TestMethodRunner"/>, so the wrapper can be
    /// safely shared instead of allocating a new one per row.
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
}
