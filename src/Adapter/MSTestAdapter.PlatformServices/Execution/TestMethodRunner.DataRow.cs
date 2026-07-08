// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using UTF = Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution;

/// <summary>
/// Handles folded / <c>DynamicData</c>-style (<see cref="UTF.ITestDataSource"/>) data-driven tests.
/// </summary>
internal sealed partial class TestMethodRunner
{
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
}
