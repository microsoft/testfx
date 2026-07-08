// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Helpers;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.VisualStudio.TestTools.UnitTesting.Internal;

using UTF = Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution;

/// <summary>
/// Handles <see cref="DataSourceAttribute"/>-attributed data-driven tests.
/// </summary>
internal sealed partial class TestMethodRunner
{
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

    private async Task ExecuteTestFromDataSourceAttributeAsync(List<TestResult> results)
    {
        var watch = Stopwatch.StartNew();

        try
        {
            IEnumerable<object>? dataRows = PlatformServiceProvider.Instance.TestDataSource.GetData(_testMethodInfo, _testContext);
            if (dataRows is null)
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

        // PERF: Extract ReflectionTestMethodInfo to avoid allocating it twice when testDataSource is not null
        // and both GetDisplayName and ComputeDefaultDisplayName need to be consulted.
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
}
