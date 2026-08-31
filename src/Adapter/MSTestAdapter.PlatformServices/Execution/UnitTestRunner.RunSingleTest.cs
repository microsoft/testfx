// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Helpers;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution;

internal sealed partial class UnitTestRunner
{
    // Task cannot cross app domains.
    // For now, TestExecutionManager will call this sync method which is hacky.
    internal TestResult[] RunSingleTest(UnitTestElement unitTestElement, IDictionary<string, object?> testContextProperties, IAdapterMessageLogger messageLogger)
        => RunSingleTestAsync(unitTestElement, testContextProperties, messageLogger).GetAwaiter().GetResult();

    // Task cannot cross app domains.
    // For now, TestExecutionManager will call this sync method which is hacky.
    internal TestResult[] RunSingleTest(
        UnitTestElement unitTestElement,
        IDictionary<string, object?> testContextProperties,
        IDictionary<string, object?> lifecycleContextProperties,
        IAdapterMessageLogger messageLogger)
        => RunSingleTestAsync(unitTestElement, testContextProperties, lifecycleContextProperties, messageLogger).GetAwaiter().GetResult();

    /// <summary>
    /// Runs a single test.
    /// </summary>
    /// <param name="unitTestElement"> The test Method. </param>
    /// <param name="testContextProperties"> The test context properties. </param>
    /// <param name="messageLogger"> The message logger. </param>
    /// <returns> The <see cref="TestResult"/>. </returns>
    internal async Task<TestResult[]> RunSingleTestAsync(UnitTestElement unitTestElement, IDictionary<string, object?> testContextProperties, IAdapterMessageLogger messageLogger)
        => await RunSingleTestAsync(unitTestElement, testContextProperties, testContextProperties, messageLogger).ConfigureAwait(false);

    // Task cannot cross app domains.
    // For now, TestExecutionManager will call this sync method which is hacky.
    internal TestResult[] NotifyTestNotRun(
        UnitTestElement unitTestElement,
        IDictionary<string, object?> testContextProperties,
        IDictionary<string, object?> lifecycleContextProperties,
        IAdapterMessageLogger messageLogger)
        => NotifyTestNotRunAsync(unitTestElement, testContextProperties, lifecycleContextProperties, messageLogger).GetAwaiter().GetResult();

    /// <summary>
    /// Tells the runner that a selected test is not going to execute, because the scheduler decided its
    /// outcome without running it - its prerequisite did not pass, or it is part of a dependency cycle.
    /// </summary>
    /// <remarks>
    /// The class-cleanup countdown is built from every <em>selected</em> test, so a test that never reaches
    /// <see cref="RunSingleTestAsync(UnitTestElement, IDictionary{string, object?}, IDictionary{string, object?}, IAdapterMessageLogger)"/>
    /// still owes its decrement. This performs exactly the bookkeeping a filtered-out test does, which is
    /// the same situation: selected, counted, never run.
    /// </remarks>
    /// <param name="unitTestElement">The test that will not run.</param>
    /// <param name="testContextProperties">Properties scoped to this test.</param>
    /// <param name="lifecycleContextProperties">Properties scoped to assembly and class lifecycle methods.</param>
    /// <param name="messageLogger">The message logger.</param>
    /// <returns>
    /// Any results produced by cleanup that this call triggered - empty unless a <c>[ClassCleanup]</c> or
    /// <c>[AssemblyCleanup]</c> ran and failed. The test's own "skipped" or "failed" result is reported by
    /// the caller, which is what decided the outcome.
    /// </returns>
    internal async Task<TestResult[]> NotifyTestNotRunAsync(
        UnitTestElement unitTestElement,
        IDictionary<string, object?> testContextProperties,
        IDictionary<string, object?> lifecycleContextProperties,
        IAdapterMessageLogger messageLogger)
    {
        if (unitTestElement is null)
        {
            throw new ArgumentNullException(nameof(unitTestElement));
        }

        ITestContext? testContextForTestExecution = null;
        try
        {
            testContextForTestExecution = PlatformServiceProvider.Instance.GetTestContext(unitTestElement.TestMethod, null, testContextProperties, messageLogger, UnitTestOutcome.InProgress);
            return await FinishTestThatDidNotRunAsync(
                unitTestElement.TestMethod,
                lifecycleContextProperties,
                messageLogger,
                [],
                testContextForTestExecution).ConfigureAwait(false);
        }
        finally
        {
            (testContextForTestExecution as IDisposable)?.Dispose();
        }
    }

    /// <summary>
    /// Runs a single test.
    /// </summary>
    /// <param name="unitTestElement">The test method.</param>
    /// <param name="testContextProperties">Properties scoped to this test.</param>
    /// <param name="lifecycleContextProperties">Properties scoped to assembly and class lifecycle methods.</param>
    /// <param name="messageLogger">The message logger.</param>
    /// <returns>The <see cref="TestResult"/>.</returns>
    internal async Task<TestResult[]> RunSingleTestAsync(
        UnitTestElement unitTestElement,
        IDictionary<string, object?> testContextProperties,
        IDictionary<string, object?> lifecycleContextProperties,
        IAdapterMessageLogger messageLogger)
    {
        if (unitTestElement is null)
        {
            throw new ArgumentNullException(nameof(unitTestElement));
        }

        if (testContextProperties is null)
        {
            throw new ArgumentNullException(nameof(testContextProperties));
        }

        if (lifecycleContextProperties is null)
        {
            throw new ArgumentNullException(nameof(lifecycleContextProperties));
        }

        TestMethod testMethod = unitTestElement.TestMethod;
        ITestContext? testContextForTestExecution = null;
        ITestContext? testContextForAssemblyInit = null;
        ITestContext? testContextForClassInit = null;
        ITestContext? testContextForClassCleanup = null;
        ITestContext? testContextForAssemblyCleanup = null;

        try
        {
            testContextForTestExecution = PlatformServiceProvider.Instance.GetTestContext(testMethod, null, testContextProperties, messageLogger, UnitTestOutcome.InProgress);

            // Apply user-supplied [TestFilterProvider] filter BEFORE loading the test type, BEFORE
            // running [AssemblyInitialize] and BEFORE [ClassInitialize]. This is the whole point of
            // the feature: a Drop or Skip here pays none of those costs. See
            // https://github.com/microsoft/testfx/issues/8894 for the design.
            TestResult[]? filterResult = ApplyTestFilter(unitTestElement);
            if (filterResult is not null)
            {
                return await FinishTestThatDidNotRunAsync(
                    testMethod,
                    lifecycleContextProperties,
                    messageLogger,
                    filterResult,
                    testContextForTestExecution).ConfigureAwait(false);
            }

            // Get the testMethod
            TestMethodInfo? testMethodInfo = _typeCache.GetTestMethodInfo(testMethod);

            TestResult[] result;
            if (!IsTestMethodRunnable(testMethod, testMethodInfo, out TestResult[]? notRunnableResult))
            {
                result = notRunnableResult;
            }
            else
            {
                DebugEx.Assert(testMethodInfo is not null, "testMethodInfo should not be null.");

                TestAssemblyInfo assemblyInfo = testMethodInfo.Parent.Parent;
                TestResult assemblyInitializeResult;
                if (assemblyInfo.IsAssemblyInitializeExecuted && assemblyInfo.AssemblyInitializationException is null)
                {
                    // Fast path: assembly init already ran and succeeded.
                    // Skip the TestContextImplementation allocation (dictionary copy + cancellation registration).
                    assemblyInitializeResult = AssemblyInitPassedResult;
                }
                else
                {
                    testContextForAssemblyInit = PlatformServiceProvider.Instance.GetTestContext(testMethod: null, null, lifecycleContextProperties, messageLogger, testContextForTestExecution.Context.CurrentTestOutcome);
                    assemblyInitializeResult = await RunAssemblyInitializeIfNeededAsync(testMethodInfo, testContextForAssemblyInit).ConfigureAwait(false);
                }

                // Remember that assembly initialize ran for this assembly so the end-of-assembly cleanup
                // guard still fires even when the last test of the assembly is filtered out (and therefore
                // has no testMethodInfo of its own). See FinishTestThatDidNotRunAsync.
                _assemblyInitializeWasExecuted |= assemblyInfo.IsAssemblyInitializeExecuted;

                if (assemblyInitializeResult.Outcome != UnitTestOutcome.Passed)
                {
                    result = [assemblyInitializeResult];
                }
                else
                {
                    if (testMethodInfo.Parent.HasExecutableCleanupMethod)
                    {
                        _lastRunnableTestByClass[testMethod.FullClassName] = unitTestElement;
                    }

                    _lastRunnableTestInWholeAssembly = unitTestElement;

                    // Fast path: class init already ran; skip the TestContextImplementation allocation
                    // (dictionary copy + cancellation registration). The cached result is a lightweight clone.
                    TestResult? cachedClassInit = testMethodInfo.Parent.TryGetClonedCachedClassInitializeResult();
                    TestResult classInitializeResult;
                    if (cachedClassInit is not null)
                    {
                        classInitializeResult = cachedClassInit;
                    }
                    else
                    {
                        testContextForClassInit = PlatformServiceProvider.Instance.GetTestContext(testMethod: null, testMethod.FullClassName, lifecycleContextProperties, messageLogger, UnitTestOutcome.InProgress);

                        // Flow properties set during AssemblyInitialize into the class-init context so the
                        // ClassInitialize method observes them.
                        ((TestContextImplementation)testContextForClassInit.Context).MergeProperties(assemblyInfo.PostAssemblyInitProperties);

                        classInitializeResult = await testMethodInfo.Parent.GetResultOrRunClassInitializeAsync(testContextForClassInit, assemblyInitializeResult.LogOutput, assemblyInitializeResult.LogError, assemblyInitializeResult.DebugTrace, assemblyInitializeResult.TestContextMessages).ConfigureAwait(false);
                    }

                    DebugEx.Assert(testMethodInfo.Parent.IsClassInitializeExecuted, "IsClassInitializeExecuted should be true after attempting to run it.");
                    if (classInitializeResult.Outcome != UnitTestOutcome.Passed)
                    {
                        result = [classInitializeResult];
                    }
                    else
                    {
                        // Run the test method
                        // When testContextForClassInit is null (class init fast path), its outcome
                        // would have been InProgress (unchanged), making SetOutcome a no-op; skip it.
                        if (testContextForClassInit is not null)
                        {
                            testContextForTestExecution.SetOutcome(testContextForClassInit.Context.CurrentTestOutcome);
                        }

                        // Flow properties set during AssemblyInitialize and ClassInitialize into the
                        // per-test execution context so the test class constructor, [TestInitialize],
                        // the test method itself and [TestCleanup] observe them.
                        // Note: when a test method has multiple data rows, the merge is applied once
                        // before all rows; data rows share the same execution context (and bag).
                        var testExecImpl = (TestContextImplementation)testContextForTestExecution.Context;
                        testExecImpl.MergeProperties(assemblyInfo.PostAssemblyInitProperties);
                        testExecImpl.MergeProperties(testMethodInfo.Parent.PostClassInitProperties);

                        RetryBaseAttribute? retryAttribute = testMethodInfo.RetryAttribute;
                        var testMethodRunner = new TestMethodRunner(testMethodInfo, testMethod, testContextForTestExecution);
                        result = await testMethodRunner.ExecuteAsync(classInitializeResult.LogOutput, classInitializeResult.LogError, classInitializeResult.DebugTrace, classInitializeResult.TestContextMessages).ConfigureAwait(false);
                        if (retryAttribute is not null && !RetryBaseAttribute.IsAcceptableResultForRetry(result))
                        {
                            RetryResult retryResult = await retryAttribute.ExecuteAsync(
                                new RetryContext(
                                    async () => await testMethodRunner.ExecuteAsync(classInitializeResult.LogOutput, classInitializeResult.LogError, classInitializeResult.DebugTrace, classInitializeResult.TestContextMessages).ConfigureAwait(false),
                                    result)).ConfigureAwait(false);

                            result = CombineRetryAttempts(result, retryResult);
                        }
                    }
                }
            }

            _classCleanupManager.MarkTestComplete(testMethod, out bool isLastTestInClass);
            if (isLastTestInClass)
            {
                // Defer TestContextImplementation allocation to only the last test in each class,
                // saving one dict-copy + CancellationTokenRegistration per non-last test.
                testContextForClassCleanup = PlatformServiceProvider.Instance.GetTestContext(testMethod: null, testMethod.FullClassName, lifecycleContextProperties, messageLogger, testContextForTestExecution.Context.CurrentTestOutcome);

                if (testMethodInfo is not null)
                {
                    // Flow properties set during AssemblyInitialize and ClassInitialize so the
                    // ClassCleanup method observes them. Done here rather than on every test to
                    // avoid wasted dictionary copies for non-last tests.
                    var classCleanupImpl = (TestContextImplementation)testContextForClassCleanup.Context;
                    classCleanupImpl.MergeProperties(testMethodInfo.Parent.Parent.PostAssemblyInitProperties);
                    classCleanupImpl.MergeProperties(testMethodInfo.Parent.PostClassInitProperties);

                    TestResult? cleanupResult = await testMethodInfo.Parent.RunClassCleanupAsync(testContextForClassCleanup, result).ConfigureAwait(false);
                    if (cleanupResult is not null)
                    {
                        // Current test is ignored, and we have a class cleanup failure. We need to attach to the right test.
                        if (notRunnableResult is not null &&
                            _lastRunnableTestByClass.TryGetValue(testMethod.FullClassName, out UnitTestElement? lastRunnableUnitTest))
                        {
                            cleanupResult.AssociatedUnitTestElement = lastRunnableUnitTest;
                        }

                        result = [.. result, AlignAttemptNumber(cleanupResult, result)];
                    }
                }

                // Mark the class as complete when all class cleanups are complete. When all classes are complete we progress to running assembly cleanup.
                // Class is not complete until after all class cleanups are done, to prevent running assembly cleanup too early.
                // Do not mark the class as complete when the last test method in the class completed. That is too early, we need to run class cleanups before marking class as complete.
                _classCleanupManager.MarkClassComplete(testMethod.FullClassName);
            }

            if (testMethodInfo?.Parent.Parent.IsAssemblyInitializeExecuted == true &&
                _classCleanupManager.ShouldRunEndOfAssemblyCleanup &&
                testContextForClassCleanup is not null)
            {
                // testContextForClassCleanup is guaranteed non-null here: ShouldRunEndOfAssemblyCleanup
                // becomes true only after MarkClassComplete, which is called exclusively inside the
                // isLastTestInClass block above — where testContextForClassCleanup is allocated.
                testContextForAssemblyCleanup = PlatformServiceProvider.Instance.GetTestContext(testMethod: null, null, lifecycleContextProperties, messageLogger, testContextForClassCleanup.Context.CurrentTestOutcome);

                TestResult? assemblyCleanupResult = await RunAssemblyCleanupAsync(testContextForAssemblyCleanup, _typeCache, result).ConfigureAwait(false);
                if (assemblyCleanupResult is not null)
                {
                    if (notRunnableResult is not null)
                    {
                        // Current test is ignored, and we have an assembly cleanup failure. We need to attach to the right test.
                        assemblyCleanupResult.AssociatedUnitTestElement = _lastRunnableTestInWholeAssembly;
                    }

                    result = [.. result, AlignAttemptNumber(assemblyCleanupResult, result)];
                }
            }

            return result;
        }
        catch (Exception ex)
        {
            // Catch any exception thrown while inspecting the test method and return failure.
            return
            [
                new TestResult
                {
                    Outcome = UnitTestOutcome.Error,
                    IgnoreReason = ex.Message,
                }
            ];
        }
        finally
        {
            (testContextForTestExecution as IDisposable)?.Dispose();
            (testContextForAssemblyInit as IDisposable)?.Dispose();
            (testContextForClassInit as IDisposable)?.Dispose();
            (testContextForClassCleanup as IDisposable)?.Dispose();
            (testContextForAssemblyCleanup as IDisposable)?.Dispose();
        }
    }

    /// <summary>
    /// Flattens an in-process retry sequence into the single result array the rest of the pipeline works with:
    /// <c>[...attempt 1, ...attempt 2, ..., ...attempt N]</c>, where every attempt but the last is marked as
    /// superseded.
    /// </summary>
    /// <remarks>
    /// Historically only the last attempt survived, which made an in-process retry invisible to every consumer -
    /// the terminal, the reports, and notably CTRF's <c>flaky</c> flag (see
    /// https://github.com/microsoft/testfx/issues/10292). The earlier attempts are kept here and tagged so each
    /// recorder can decide what to do with them: the Microsoft.Testing.Platform recorder reports them all (tagged
    /// with <c>RetryAttemptProperty</c>), while VSTest - which has no notion of attempts - drops the superseded
    /// ones and keeps its historical single-result-per-test behavior.
    /// <para>
    /// The final attempt stays last so callers that look at <c>results[^1]</c> (class and assembly cleanup) and
    /// consumers that collapse per test node uid keep observing the test's real outcome.
    /// </para>
    /// </remarks>
    internal static TestResult[] CombineRetryAttempts(TestResult[] firstAttempt, RetryResult retryResult)
    {
        IReadOnlyList<TestResult[]> retryAttempts = retryResult.AllResults;
        if (retryAttempts.Count == 0)
        {
            // A RetryBaseAttribute implementation is expected to add at least one attempt.
            throw ApplicationStateGuard.Unreachable();
        }

        // An attempt that produced no result at all is an execution error, which the pipeline signals by an empty
        // array (SendTestResultsAsync turns it into RecordEmptyResultAsync). Returning the earlier attempts here
        // would hide that: the array would be non-empty, so the empty-result path would be skipped, every entry
        // would then be filtered out as superseded, and AllPassed would vacuously return true - reporting a missing
        // result as a passing test and unblocking its dependents. Preserve the empty-result contract instead.
        if (retryAttempts[retryAttempts.Count - 1].Length == 0)
        {
            return [];
        }

        int totalCount = firstAttempt.Length;
        foreach (TestResult[] attempt in retryAttempts)
        {
            totalCount += attempt.Length;
        }

        var combined = new List<TestResult>(totalCount);
        AddAttempt(combined, firstAttempt, attemptNumber: 1, isSuperseded: true);
        for (int i = 0; i < retryAttempts.Count; i++)
        {
            AddAttempt(combined, retryAttempts[i], attemptNumber: i + 2, isSuperseded: i != retryAttempts.Count - 1);
        }

        return [.. combined];

        static void AddAttempt(List<TestResult> combined, TestResult[] attempt, int attemptNumber, bool isSuperseded)
        {
            foreach (TestResult result in attempt)
            {
                result.RetryAttemptNumber = attemptNumber;
                result.IsSupersededRetryAttempt = isSuperseded;
                combined.Add(result);
            }
        }
    }

    /// <summary>
    /// Stamps a result produced outside the retry sequence (a class or assembly cleanup failure appended after the
    /// test itself completed) with the attempt the test finished on.
    /// </summary>
    /// <remarks>
    /// Cleanup results are reported under the same test node uid as the test, so a cleanup result left at the
    /// default attempt 1 would look like an <em>earlier</em> attempt than a test that finished on attempt 2+. The
    /// terminal's attempt ordering treats that as out-of-order arrival and throws, so the attempt number is carried
    /// over here. The result is not marked superseded: it is part of the test's final outcome.
    /// </remarks>
    private static TestResult AlignAttemptNumber(TestResult result, TestResult[] precedingResults)
    {
        for (int i = precedingResults.Length - 1; i >= 0; i--)
        {
            if (!precedingResults[i].IsSupersededRetryAttempt)
            {
                result.RetryAttemptNumber = precedingResults[i].RetryAttemptNumber;
                break;
            }
        }

        return result;
    }

    /// <summary>
    /// Whether the given testMethod is runnable.
    /// </summary>
    /// <param name="testMethod">The testMethod.</param>
    /// <param name="testMethodInfo">The testMethodInfo.</param>
    /// <param name="notRunnableResult">The results to return if the test method is not runnable.</param>
    /// <returns>whether the given testMethod is runnable.</returns>
    private static bool IsTestMethodRunnable(
        TestMethod testMethod,
        TestMethodInfo? testMethodInfo,
        [NotNullWhen(false)] out TestResult[]? notRunnableResult)
    {
        // If the specified TestMethod could not be found, return a NotFound result.
        if (testMethodInfo is null)
        {
            notRunnableResult =
            [
                new TestResult
                {
                    Outcome = UnitTestOutcome.NotFound,
                    IgnoreReason = string.Format(CultureInfo.CurrentCulture, Resource.TestNotFound, testMethod.Name),
                },
            ];
            return false;
        }

        bool shouldIgnoreClass = testMethodInfo.Parent.ClassType.IsIgnored(out string? ignoreMessageOnClass);
        bool shouldIgnoreMethod = testMethodInfo.MethodInfo.IsIgnored(out string? ignoreMessageOnMethod);

        if (shouldIgnoreClass || shouldIgnoreMethod)
        {
            string? ignoreMessage = shouldIgnoreMethod && StringEx.IsNullOrEmpty(ignoreMessageOnClass) ? ignoreMessageOnMethod : ignoreMessageOnClass;
            notRunnableResult =
                [TestResult.CreateIgnoredResult(ignoreMessage)];
            return false;
        }

        notRunnableResult = null;
        return true;
    }

    internal void ForceCleanup(IDictionary<string, object?> sourceLevelParameters, IAdapterMessageLogger logger) => ClassCleanupManager.ForceCleanup(_typeCache, sourceLevelParameters, logger);
}
