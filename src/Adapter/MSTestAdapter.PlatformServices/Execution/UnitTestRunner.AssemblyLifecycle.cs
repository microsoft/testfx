// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Extensions;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Extensions;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution;

internal sealed partial class UnitTestRunner
{
    private static async Task<TestResult> RunAssemblyInitializeIfNeededAsync(TestMethodInfo testMethodInfo, ITestContext testContext)
    {
        TestResult? result = null;

        try
        {
            result = await testMethodInfo.Parent.Parent.RunAssemblyInitializeAsync(testContext.Context).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            var testFailureException = new TestFailedException(UnitTestOutcome.Error, ex.TryGetMessage(), ex.TryGetStackTraceInformation());
            result = new TestResult { TestFailureException = testFailureException, Outcome = UnitTestOutcome.Error };
        }
        finally
        {
            var testContextImpl = testContext.Context as TestContextImplementation;
            result!.LogOutput = testContextImpl?.GetAndClearOutput();
            result.LogError = testContextImpl?.GetAndClearError();
            result.DebugTrace = testContextImpl?.GetAndClearTrace();
            result.TestContextMessages = testContext.GetAndClearDiagnosticMessages();
        }

        return result;
    }

    private static async Task<TestResult?> RunAssemblyCleanupAsync(ITestContext testContext, TypeCache typeCache, TestResult[] results)
    {
        var testContextImpl = testContext.Context as TestContextImplementation;
        IEnumerable<TestAssemblyInfo> assemblyInfoCache = typeCache.AssemblyInfoListWithExecutableCleanupMethods;
        foreach (TestAssemblyInfo assemblyInfo in assemblyInfoCache)
        {
            // Flow properties set during AssemblyInitialize so the AssemblyCleanup method observes
            // them. Class-init properties are intentionally NOT flowed here because AssemblyCleanup
            // is assembly-scoped and runs once across many classes; picking a single class's
            // snapshot would be arbitrary.
            testContextImpl?.MergeProperties(assemblyInfo.PostAssemblyInitProperties);

            TestFailedException? ex = await assemblyInfo.ExecuteAssemblyCleanupAsync(testContext.Context).ConfigureAwait(false);

            if (ex is not null)
            {
                return new TestResult()
                {
                    Outcome = UnitTestOutcome.Failed,
                    TestFailureException = ex,
                    LogOutput = testContextImpl?.GetAndClearOutput(),
                    LogError = testContextImpl?.GetAndClearError(),
                    DebugTrace = testContextImpl?.GetAndClearTrace(),
                    TestContextMessages = testContext.GetAndClearDiagnosticMessages(),
                };
            }

            if (results.Length > 0)
            {
                TestResult lastResult = results[results.Length - 1];
                lastResult.LogOutput += testContextImpl?.GetAndClearOutput();
                lastResult.LogError += testContextImpl?.GetAndClearError();
                lastResult.DebugTrace += testContextImpl?.GetAndClearTrace();
                lastResult.TestContextMessages += testContext.GetAndClearDiagnosticMessages();
            }
        }

        return null;
    }
}
