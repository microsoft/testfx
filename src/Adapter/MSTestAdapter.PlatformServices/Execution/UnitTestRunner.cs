// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if NETFRAMEWORK
using System.Security;
#endif

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Extensions;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Helpers;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Extensions;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Helpers;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.VisualStudio.TestTools.UnitTesting.Logging;

// The programmatic test-filter types (ITestFilter, TestFilterContext, TestFilterResult,
// TestFilterAction, TestFilterProviderAttribute) are [Experimental] public API. This file is part
// of the adapter implementation of that feature, so consuming them here is intentional.
#pragma warning disable MSTESTEXP

namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution;

/// <summary>
/// The runner that runs a single unit test. Also manages the assembly and class cleanup methods at the end of the run.
/// </summary>
[StackTraceHidden]
internal sealed class UnitTestRunner
#if NETFRAMEWORK
    : MarshalByRefObject
#endif
{
    // Reusable TestResult returned for the assembly-init fast path (init already passed).
    // Safe to share across concurrent test runs because it is private to this type and treated
    // as immutable: the fast path only reads from it (Outcome plus the null Log/DebugTrace fields)
    // and the instance never escapes RunSingleTestAsync. Do not mutate it.
    private static readonly TestResult AssemblyInitPassedResult = new() { Outcome = UnitTestOutcome.Passed };

    private readonly TypeCache _typeCache;
    private readonly ClassCleanupManager _classCleanupManager;

    // Only needed to attach class cleanup failures to the right test.
    // So we only add to this dictionary if the class has a class cleanup.
    private readonly ConcurrentDictionary<string, UnitTestElement> _lastRunnableTestByClass = new();

    // Used to attach assembly cleanup failures to the right test.
    private UnitTestElement? _lastRunnableTestInWholeAssembly;

    // Tracks whether at least one test in this runner's lifetime triggered AssemblyInitialize.
    // Needed so that end-of-assembly cleanup still runs when the very last test of the assembly
    // was filtered out by a [TestFilterProvider] (in which case testMethodInfo is null for the
    // cleanup decision in RunSingleTestAsync).
    private bool _assemblyInitializeWasExecuted;

    /// <summary>
    /// Initializes a new instance of the <see cref="UnitTestRunner"/> class.
    /// </summary>
    /// <param name="settings"> Specifies adapter settings that need to be instantiated in the domain running these tests. </param>
    /// <param name="testsToRun"> The tests to run. </param>
    public UnitTestRunner(MSTestSettings settings, UnitTestElement[] testsToRun)
        : this(settings, testsToRun, ReflectHelper.Instance)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="UnitTestRunner"/> class.
    /// </summary>
    /// <param name="settings"> Specifies adapter settings. </param>
    /// <param name="testsToRun"> The tests to run. </param>
    /// <param name="reflectHelper"> The reflect Helper. </param>
    internal UnitTestRunner(MSTestSettings settings, UnitTestElement[] testsToRun, ReflectHelper reflectHelper)
    {
        // Populate the settings into the domain(Desktop workflow) performing discovery.
        // This would just be resetting the settings to itself in non desktop workflows.
        MSTestSettings.PopulateSettings(settings);

        // Bridge the adapter setting to the TestFramework for assertion failure behavior.
        AssertionFailureSettings.LaunchDebuggerOnAssertionFailure = MSTestSettings.CurrentSettings.LaunchDebuggerOnAssertionFailure;

        Logger.OnLogMessage += message => (TestContext.Current as TestContextImplementation)?.WriteConsoleOut(message);

        if (MSTestSettings.CurrentSettings.CaptureDebugTraces)
        {
            Console.SetOut(new ConsoleOutRouter(Console.Out));
            Console.SetError(new ConsoleErrorRouter(Console.Error));
            Trace.Listeners.Add(new TextWriterTraceListener(new TraceTextWriter()));
        }

        PlatformServiceProvider.Instance.TestRunCancellationToken ??= new TestRunCancellationToken();
        _typeCache = new TypeCache(reflectHelper);

        _classCleanupManager = new ClassCleanupManager(testsToRun);

        // Expose the planned (post-filter) test list so that user code (typically [AssemblyInitialize]
        // or fixtures) can query TestRun.Current.PlannedTests to decide whether expensive setup is
        // needed. Set here so the snapshot lives in the same AppDomain/process that will execute the
        // assembly initialize and the tests themselves.
        TestRun.SetCurrent(TestRunInfo.CreateFrom(testsToRun));
    }

#pragma warning disable CA1822 // Mark members as static
    public void Cancel()
        => PlatformServiceProvider.Instance.TestRunCancellationToken?.Cancel();
#pragma warning restore CA1822 // Mark members as static

#if NETFRAMEWORK
    /// <summary>
    /// Returns object to be used for controlling lifetime, null means infinite lifetime.
    /// </summary>
    /// <returns>
    /// The <see cref="object"/>.
    /// </returns>
    [SecurityCritical]
    public override object? InitializeLifetimeService() => null;
#endif

    // Task cannot cross app domains.
    // For now, TestExecutionManager will call this sync method which is hacky.
    internal TestResult[] RunSingleTest(UnitTestElement unitTestElement, IDictionary<string, object?> testContextProperties, IMessageLogger messageLogger)
        => RunSingleTestAsync(unitTestElement, testContextProperties, messageLogger).GetAwaiter().GetResult();

    /// <summary>
    /// Runs a single test.
    /// </summary>
    /// <param name="unitTestElement"> The test Method. </param>
    /// <param name="testContextProperties"> The test context properties. </param>
    /// <param name="messageLogger"> The message logger. </param>
    /// <returns> The <see cref="TestResult"/>. </returns>
    internal async Task<TestResult[]> RunSingleTestAsync(UnitTestElement unitTestElement, IDictionary<string, object?> testContextProperties, IMessageLogger messageLogger)
    {
        if (unitTestElement is null)
        {
            throw new ArgumentNullException(nameof(unitTestElement));
        }

        if (testContextProperties is null)
        {
            throw new ArgumentNullException(nameof(testContextProperties));
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
                return await FinishFilteredOutTestAsync(
                    testMethod,
                    testContextProperties,
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
                    testContextForAssemblyInit = PlatformServiceProvider.Instance.GetTestContext(testMethod: null, null, testContextProperties, messageLogger, testContextForTestExecution.Context.CurrentTestOutcome);
                    assemblyInitializeResult = await RunAssemblyInitializeIfNeededAsync(testMethodInfo, testContextForAssemblyInit).ConfigureAwait(false);
                }

                // Remember that assembly initialize ran for this assembly so the end-of-assembly cleanup
                // guard still fires even when the last test of the assembly is filtered out (and therefore
                // has no testMethodInfo of its own). See FinishFilteredOutTestAsync.
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
                        testContextForClassInit = PlatformServiceProvider.Instance.GetTestContext(testMethod: null, testMethod.FullClassName, testContextProperties, messageLogger, UnitTestOutcome.InProgress);

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

                            result = retryResult.TryGetLast() ?? throw ApplicationStateGuard.Unreachable();
                        }
                    }
                }
            }

            _classCleanupManager.MarkTestComplete(testMethod, out bool isLastTestInClass);
            if (isLastTestInClass)
            {
                // Defer TestContextImplementation allocation to only the last test in each class,
                // saving one dict-copy + CancellationTokenRegistration per non-last test.
                testContextForClassCleanup = PlatformServiceProvider.Instance.GetTestContext(testMethod: null, testMethod.FullClassName, testContextProperties, messageLogger, testContextForTestExecution.Context.CurrentTestOutcome);

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
                        if (notRunnableResult is not null)
                        {
                            // Current test is ignored, and we have a class cleanup failure. We need to attach to the right test.
                            if (_lastRunnableTestByClass.TryGetValue(testMethod.FullClassName, out UnitTestElement? lastRunnableUnitTest))
                            {
                                cleanupResult.AssociatedUnitTestElement = lastRunnableUnitTest;
                            }
                        }

                        result = [.. result, cleanupResult];
                    }
                }

                // Mark the class as complete when all class cleanups are complete. When all classes are complete we progress to running assembly cleanup.
                // Class is not complete until after all class cleanups are done, to prevent running assembly cleanup too early.
                // Do not mark the class as complete when the last test method in the class completed. That is too early, we need to run class cleanups before marking class as complete.
                _classCleanupManager.MarkClassComplete(testMethod.FullClassName);
            }

            if (testMethodInfo?.Parent.Parent.IsAssemblyInitializeExecuted == true &&
                _classCleanupManager.ShouldRunEndOfAssemblyCleanup)
            {
                // testContextForClassCleanup is guaranteed non-null here: ShouldRunEndOfAssemblyCleanup
                // becomes true only after MarkClassComplete, which is called exclusively inside the
                // isLastTestInClass block above — where testContextForClassCleanup is allocated.
                DebugEx.Assert(testContextForClassCleanup is not null, "testContextForClassCleanup should not be null when running assembly cleanup.");
                testContextForAssemblyCleanup = PlatformServiceProvider.Instance.GetTestContext(testMethod: null, null, testContextProperties, messageLogger, testContextForClassCleanup.Context.CurrentTestOutcome);

                TestResult? assemblyCleanupResult = await RunAssemblyCleanupAsync(testContextForAssemblyCleanup, _typeCache, result).ConfigureAwait(false);
                if (assemblyCleanupResult is not null)
                {
                    if (notRunnableResult is not null)
                    {
                        // Current test is ignored, and we have an assembly cleanup failure. We need to attach to the right test.
                        assemblyCleanupResult.AssociatedUnitTestElement = _lastRunnableTestInWholeAssembly;
                    }

                    result = [.. result, assemblyCleanupResult];
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

    internal void ForceCleanup(IDictionary<string, object?> sourceLevelParameters, IMessageLogger logger) => ClassCleanupManager.ForceCleanup(_typeCache, sourceLevelParameters, logger);

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
    /// <see cref="RunSingleTestAsync"/>. The filtered-out test never loaded its own type, but if a
    /// sibling test of the same class already ran in this worker the class was initialized and still
    /// owes its <c>[ClassCleanup]</c>, so it is executed here when this is the last test of the class.
    /// </summary>
    private async Task<TestResult[]> FinishFilteredOutTestAsync(
        TestMethod testMethod,
        IDictionary<string, object?> testContextProperties,
        IMessageLogger messageLogger,
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
                    ITestContext testContextForClassCleanup = PlatformServiceProvider.Instance.GetTestContext(testMethod: null, testMethod.FullClassName, testContextProperties, messageLogger, testContextForTestExecution.Context.CurrentTestOutcome);
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
                testContextForAssemblyCleanup = PlatformServiceProvider.Instance.GetTestContext(testMethod: null, null, testContextProperties, messageLogger, testContextForTestExecution.Context.CurrentTestOutcome);

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
