// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Extensions;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Helpers;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface;
using Microsoft.VisualStudio.TestTools.UnitTesting;

// The programmatic test-filter types (ITestFilter, TestFilterContext, TestFilterResult,
// TestFilterAction, TestFilterProviderAttribute) are [Experimental] public API. This file is part
// of the adapter implementation of that feature, so consuming them here is intentional.
#pragma warning disable MSTESTEXP

namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution;

internal sealed partial class UnitTestRunner
{
    /// <summary>
    /// Invokes the user-supplied <see cref="ITestFilter"/> registered via
    /// <see cref="TestFilterProviderAttribute"/> for the test assembly, if any. Returns
    /// <see langword="null"/> if no filter is registered or the filter returned
    /// <see cref="TestFilterResult.Run"/> (test should run normally), an empty array if the
    /// filter returned <see cref="TestFilterResult.Drop"/>, or a single Skipped
    /// <see cref="TestResult"/> if the filter returned <see cref="TestFilterResult.Skip(string)"/>.
    /// </summary>
    /// <remarks>
    /// A filter exception is surfaced as an Error test result so the failure is visible to the
    /// user instead of silently affecting test selection. <see cref="TestFilterProviderAttribute"/>
    /// is single-per-assembly by design: callers that want to combine multiple strategies should
    /// compose them explicitly inside their <see cref="ITestFilter"/> implementation.
    /// </remarks>
    private TestResult[]? ApplyTestFilter(UnitTestElement unitTestElement)
    {
        ITestFilter? filter = _typeCache.GetOrLoadTestFilter(unitTestElement.TestMethod.AssemblyName);
        if (filter is null)
        {
            return null;
        }

        TestFilterContext context = CreateFilterContext(unitTestElement);

        TestFilterResult result;
        try
        {
            result = filter.Filter(context);
        }
        catch (Exception ex)
        {
            string message = string.Format(
                CultureInfo.CurrentCulture,
                Resource.UTA_TestFilterProviderThrew,
                filter.GetType().FullName,
                context.FullyQualifiedName,
                ex.Message);
            return
            [
                new TestResult
                {
                    Outcome = UnitTestOutcome.Error,
                    TestFailureException = new TestFailedException(UnitTestOutcome.Error, message, ex.TryGetStackTraceInformation()),
                }
            ];
        }

        return result.Action switch
        {
            TestFilterAction.Drop => [],
            TestFilterAction.Skip => [TestResult.CreateIgnoredResult(result.SkipReason)],
            _ => null,
        };
    }

    private static TestFilterContext CreateFilterContext(UnitTestElement element)
    {
        TestMethod testMethod = element.TestMethod;
        string[] categories = element.TestCategory ?? [];

        KeyValuePair<string, string?>[] traits;
        if (element.Traits is { Length: > 0 } source)
        {
            traits = new KeyValuePair<string, string?>[source.Length];
            for (int i = 0; i < source.Length; i++)
            {
                traits[i] = new KeyValuePair<string, string?>(source[i].Name, source[i].Value);
            }
        }
        else
        {
            traits = [];
        }

        // Pull namespace + simple class name from the hierarchy when available — this is the
        // same source the IDE / Test Explorer uses, so it correctly handles nested types and
        // generic classes (where naïve FullClassName splitting would lie).
        string? hierarchyNamespace = null;
        string? hierarchyClassName = null;
        if (testMethod.Hierarchy is IReadOnlyList<string?> hierarchy && hierarchy.Count > HierarchyConstants.Levels.ClassIndex)
        {
            hierarchyNamespace = hierarchy[HierarchyConstants.Levels.NamespaceIndex];
            hierarchyClassName = hierarchy[HierarchyConstants.Levels.ClassIndex];
        }

        // ManagedMethodName is an ECMA-335 string like `MyMethod`1(System.Int32)` — parse it
        // cheaply (no MethodInfo reflection) to surface arity and parameter type names.
        int? methodArity = null;
        IReadOnlyList<string>? parameterTypeFullNames = null;
        if (testMethod.ManagedMethodName is { } managedMethod)
        {
            try
            {
                ManagedNameParser.ParseManagedMethodName(managedMethod, out _, out int arity, out string[]? parameterTypes);
                methodArity = arity;
                parameterTypeFullNames = parameterTypes ?? (IReadOnlyList<string>)[];
            }
            catch (InvalidManagedNameException)
            {
                // Defensive: if the managed name is malformed for any reason, surface what we
                // can via the flat strings rather than failing the filter.
            }
        }

        return new TestFilterContext
        {
            FullyQualifiedName = $"{testMethod.FullClassName}.{testMethod.Name}",
            DisplayName = testMethod.DisplayName,
            MethodName = testMethod.Name,
            Source = testMethod.AssemblyName,
            Namespace = hierarchyNamespace,
            ClassName = hierarchyClassName,
            ManagedTypeName = testMethod.ManagedTypeName,
            ManagedMethodName = testMethod.ManagedMethodName,
            MethodArity = methodArity,
            ParameterTypeFullNames = parameterTypeFullNames,
            Categories = categories,
            Traits = traits,
            Priority = element.Priority,
        };
    }

    /// <summary>
    /// Handles the bookkeeping (class-cleanup countdown, class cleanup, end-of-assembly cleanup) for a
    /// test that was filtered out by a <see cref="ITestFilter"/>. Mirrors the tail of
    /// normal test execution path. The filtered-out test never loaded its own type, but if a
    /// sibling test of the same class already ran in this worker the class was initialized and still
    /// owes its <c>[ClassCleanup]</c>, so it is executed here when this is the last test of the class.
    /// </summary>
    private async Task<TestResult[]> FinishFilteredOutTestAsync(
        TestMethod testMethod,
        IDictionary<string, object?> lifecycleContextProperties,
        IAdapterMessageLogger messageLogger,
        TestResult[] filterResult,
        ITestContext testContextForTestExecution)
    {
        _classCleanupManager.MarkTestComplete(testMethod, out bool isLastTestInClass);
        if (isLastTestInClass)
        {
            // The class-cleanup countdown spans the full (pre-filter) set of tests, so the "last test
            // in class" can land on a filtered-out test. The filtered-out test itself never loaded the
            // type, but a SIBLING test of the same class may have run earlier in this worker — which
            // means [ClassInitialize] already executed and [ClassCleanup] is still owed. We must run it
            // here; otherwise the class leaks its cleanup whenever its last-in-order test is dropped.
            //
            // _lastRunnableTestByClass is populated only for classes that both have an executable
            // cleanup method AND ran at least one non-filtered test in this worker, so its presence is
            // exactly the signal that the type is already loaded and cleanup is pending. Resolving the
            // test method info therefore hits the TypeCache and never loads a new type.
            if (_lastRunnableTestByClass.TryGetValue(testMethod.FullClassName, out UnitTestElement? lastRunnableTest))
            {
                TestMethodInfo? testMethodInfo = _typeCache.GetTestMethodInfo(lastRunnableTest.TestMethod);
                if (testMethodInfo is not null)
                {
                    ITestContext testContextForClassCleanup = PlatformServiceProvider.Instance.GetTestContext(testMethod: null, testMethod.FullClassName, lifecycleContextProperties, messageLogger, testContextForTestExecution.Context.CurrentTestOutcome);
                    try
                    {
                        // Flow properties set during AssemblyInitialize and ClassInitialize so the
                        // ClassCleanup method observes them, mirroring the run path in RunSingleTestAsync.
                        var classCleanupImpl = (TestContextImplementation)testContextForClassCleanup.Context;
                        classCleanupImpl.MergeProperties(testMethodInfo.Parent.Parent.PostAssemblyInitProperties);
                        classCleanupImpl.MergeProperties(testMethodInfo.Parent.PostClassInitProperties);

                        // Note: filterResult is empty for a dropped test, so any TestContext output
                        // written by a *successful* [ClassCleanup] is not attached to a result here
                        // (RunClassCleanupAsync only flushes TestContext output onto an existing result).
                        // Console output is unaffected — it goes to process stdout, which the test host
                        // still surfaces. A *failing* cleanup produces its own result, handled below.
                        TestResult? cleanupResult = await testMethodInfo.Parent.RunClassCleanupAsync(testContextForClassCleanup, filterResult).ConfigureAwait(false);
                        if (cleanupResult is not null)
                        {
                            // The current test was filtered out (no result of its own), so a class
                            // cleanup failure must be attached to the last real test that ran in the class.
                            cleanupResult.AssociatedUnitTestElement = lastRunnableTest;
                            filterResult = [.. filterResult, cleanupResult];
                        }
                    }
                    finally
                    {
                        (testContextForClassCleanup as IDisposable)?.Dispose();
                    }
                }
            }

            // Mark the class as complete so end-of-assembly cleanup is gated correctly. Done after the
            // class cleanup above so assembly cleanup never runs before this class is fully torn down.
            _classCleanupManager.MarkClassComplete(testMethod.FullClassName);
        }

        if (_assemblyInitializeWasExecuted && _classCleanupManager.ShouldRunEndOfAssemblyCleanup)
        {
            ITestContext? testContextForAssemblyCleanup = null;
            try
            {
                testContextForAssemblyCleanup = PlatformServiceProvider.Instance.GetTestContext(testMethod: null, null, lifecycleContextProperties, messageLogger, testContextForTestExecution.Context.CurrentTestOutcome);

                TestResult? assemblyCleanupResult = await RunAssemblyCleanupAsync(testContextForAssemblyCleanup, _typeCache, filterResult).ConfigureAwait(false);
                if (assemblyCleanupResult is not null)
                {
                    // Current test was filtered (no result), so an assembly cleanup failure needs to
                    // be associated with the last real test that ran in the assembly.
                    assemblyCleanupResult.AssociatedUnitTestElement = _lastRunnableTestInWholeAssembly;
                    filterResult = [.. filterResult, assemblyCleanupResult];
                }
            }
            finally
            {
                (testContextForAssemblyCleanup as IDisposable)?.Dispose();
            }
        }

        return filterResult;
    }
}
