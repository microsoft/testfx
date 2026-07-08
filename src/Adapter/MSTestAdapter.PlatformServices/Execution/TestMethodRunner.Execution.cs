// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Helpers;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution;

/// <summary>
/// Core single-test execution logic.
/// </summary>
internal sealed partial class TestMethodRunner
{
    private async Task<TestResult[]> ExecuteTestAsync(ITestContext executionContext, TestMethodInfo testMethodInfo)
    {
        try
        {
            ExecutionContext? capturedContext = testMethodInfo.Parent.ExecutionContext
                ?? testMethodInfo.Parent.Parent.ExecutionContext;

            // Fast path: when no ExecutionContext was captured (the common case),
            // ExecutionContextHelpers.RunOnContext would simply call the action inline.
            // Skip the TaskCompletionSource bridge, async-lambda closure, and Action
            // delegate allocations entirely.
            if (capturedContext is null)
            {
                using (TestContextImplementation.SetCurrentTestContext(executionContext as TestContext))
                {
                    testMethodInfo.TestContext = executionContext;
                    return await _testMethodInfo.Executor.ExecuteAsync(testMethodInfo).ConfigureAwait(false);
                }
            }

            var tcs = new TaskCompletionSource<TestResult[]>();

#pragma warning disable VSTHRD101 // Avoid unsupported async delegates
            ExecutionContextHelpers.RunOnContext(
                capturedContext,
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
