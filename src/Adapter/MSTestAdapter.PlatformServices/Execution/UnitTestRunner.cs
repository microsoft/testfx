// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Security;

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Extensions;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Helpers;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Extensions;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.VisualStudio.TestTools.UnitTesting.Logging;

using UnitTestOutcome = Microsoft.VisualStudio.TestTools.UnitTesting.UnitTestOutcome;
using UTF = Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution;

/// <summary>
/// The runner that runs a single unit test. Also manages the assembly and class cleanup methods at the end of the run.
/// </summary>
internal sealed class UnitTestRunner : MarshalByRefObject
{
    private readonly ConcurrentDictionary<string, TestAssemblyInfo> _assemblyFixtureTests = new();
    private readonly ConcurrentDictionary<string, TestClassInfo> _classFixtureTests = new();
    private readonly TypeCache _typeCache;
    private readonly ClassCleanupManager _classCleanupManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="UnitTestRunner"/> class.
    /// </summary>
    /// <param name="settings"> Specifies adapter settings that need to be instantiated in the domain running these tests. </param>
    /// <param name="testsToRun"> The tests to run. </param>
    /// <param name="classCleanupLifecycle"> The class cleanup lifecycle. </param>
    public UnitTestRunner(MSTestSettings settings, UnitTestElement[] testsToRun, int? classCleanupLifecycle)
        : this(settings, testsToRun, classCleanupLifecycle, ReflectHelper.Instance)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="UnitTestRunner"/> class.
    /// </summary>
    /// <param name="settings"> Specifies adapter settings. </param>
    /// <param name="testsToRun"> The tests to run. </param>
    /// <param name="classCleanupLifecycle"> The class cleanup lifecycle. </param>
    /// <param name="reflectHelper"> The reflect Helper. </param>
    internal UnitTestRunner(MSTestSettings settings, UnitTestElement[] testsToRun, int? classCleanupLifecycle, ReflectHelper reflectHelper)
    {
        // Populate the settings into the domain(Desktop workflow) performing discovery.
        // This would just be resetting the settings to itself in non desktop workflows.
        MSTestSettings.PopulateSettings(settings);

        Logger.OnLogMessage += message => TestContextImplementation.CurrentTestContext?.WriteConsoleOut(message);
        if (MSTestSettings.CurrentSettings.CaptureDebugTraces)
        {
            Console.SetOut(new ConsoleOutRouter(Console.Out));
            Console.SetError(new ConsoleErrorRouter(Console.Error));
            Trace.Listeners.Add(new TextWriterTraceListener(new TraceTextWriter()));
        }

        PlatformServiceProvider.Instance.TestRunCancellationToken ??= new TestRunCancellationToken();
        _typeCache = new TypeCache(reflectHelper);

        // We can't transport the Enum across AppDomain boundaries because of backwards and forwards compatibility.
        // So we're converting here if we can, or falling back to the default.
        ClassCleanupBehavior lifecycle = ClassCleanupBehavior.EndOfAssembly;
        if (classCleanupLifecycle != null && Enum.IsDefined(typeof(ClassCleanupBehavior), classCleanupLifecycle))
        {
            lifecycle = (ClassCleanupBehavior)classCleanupLifecycle;
        }

        _classCleanupManager = new ClassCleanupManager(
            testsToRun,
            MSTestSettings.CurrentSettings.ClassCleanupLifecycle ?? lifecycle,
            reflectHelper);
    }

#pragma warning disable CA1822 // Mark members as static
    public void Cancel()
        => PlatformServiceProvider.Instance.TestRunCancellationToken?.Cancel();
#pragma warning restore CA1822 // Mark members as static

    /// <summary>
    /// Returns object to be used for controlling lifetime, null means infinite lifetime.
    /// </summary>
    /// <returns>
    /// The <see cref="object"/>.
    /// </returns>
    [SecurityCritical]
#if NET5_0_OR_GREATER
    [Obsolete]
#endif
    public override object InitializeLifetimeService() => null!;

    internal FixtureTestResult GetFixtureTestResult(TestMethod testMethod, string fixtureType)
    {
        // For the fixture methods, we need to return the appropriate result.
        // Get matching testMethodInfo from the cache and return UnitTestOutcome for the fixture test.
        if (fixtureType is EngineConstants.ClassInitializeFixtureTrait or EngineConstants.ClassCleanupFixtureTrait &&
            _classFixtureTests.TryGetValue(testMethod.AssemblyName + testMethod.FullClassName, out TestClassInfo? testClassInfo))
        {
            UnitTestOutcome outcome = fixtureType switch
            {
                EngineConstants.ClassInitializeFixtureTrait => testClassInfo.IsClassInitializeExecuted ? GetOutcome(testClassInfo.ClassInitializationException) : UnitTestOutcome.Inconclusive,
                EngineConstants.ClassCleanupFixtureTrait => testClassInfo.IsClassCleanupExecuted ? GetOutcome(testClassInfo.ClassCleanupException) : UnitTestOutcome.Inconclusive,
                _ => throw ApplicationStateGuard.Unreachable(),
            };

            return new FixtureTestResult(true, outcome);
        }
        else if (fixtureType is EngineConstants.AssemblyInitializeFixtureTrait or EngineConstants.AssemblyCleanupFixtureTrait &&
            _assemblyFixtureTests.TryGetValue(testMethod.AssemblyName, out TestAssemblyInfo? testAssemblyInfo))
        {
            Exception? exception = fixtureType switch
            {
                EngineConstants.AssemblyInitializeFixtureTrait => testAssemblyInfo.AssemblyInitializationException,
                EngineConstants.AssemblyCleanupFixtureTrait => testAssemblyInfo.AssemblyCleanupException,
                _ => throw ApplicationStateGuard.Unreachable(),
            };

            return new(true, GetOutcome(exception));
        }

        return new(false, UnitTestOutcome.Inconclusive);

        // Local functions
        static UnitTestOutcome GetOutcome(Exception? exception) => exception == null ? UnitTestOutcome.Passed : UnitTestOutcome.Failed;
    }

    // Task cannot cross app domains.
    // For now, TestExecutionManager will call this sync method which is hacky.
    // If we removed AppDomains in v4, we should use the async method and remove this one.
    internal TestResult[] RunSingleTest(TestMethod testMethod, IDictionary<string, object?> testContextProperties, IMessageLogger messageLogger)
        => RunSingleTestAsync(testMethod, testContextProperties, messageLogger).GetAwaiter().GetResult();

    /// <summary>
    /// Runs a single test.
    /// </summary>
    /// <param name="testMethod"> The test Method. </param>
    /// <param name="testContextProperties"> The test context properties. </param>
    /// <param name="messageLogger"> The message logger. </param>
    /// <returns> The <see cref="TestResult"/>. </returns>
    internal async Task<TestResult[]> RunSingleTestAsync(TestMethod testMethod, IDictionary<string, object?> testContextProperties, IMessageLogger messageLogger)
    {
        Guard.NotNull(testMethod);

        ITestContext? testContextForTestExecution = null;
        ITestContext? testContextForAssemblyInit = null;
        ITestContext? testContextForClassInit = null;
        ITestContext? testContextForClassCleanup = null;
        ITestContext? testContextForAssemblyCleanup = null;

        try
        {
            var properties = new Dictionary<string, object?>(testContextProperties);
            testContextForTestExecution = PlatformServiceProvider.Instance.GetTestContext(testMethod, properties, messageLogger, UTF.UnitTestOutcome.InProgress);

            // Get the testMethod
            TestMethodInfo? testMethodInfo = _typeCache.GetTestMethodInfo(
                testMethod,
                testContextForTestExecution);

            TestResult[] result;
            if (!IsTestMethodRunnable(testMethod, testMethodInfo, out TestResult[]? notRunnableResult))
            {
                result = notRunnableResult;
            }
            else
            {
                DebugEx.Assert(testMethodInfo is not null, "testMethodInfo should not be null.");

                // Keep track of all non-runnable methods so that we can return the appropriate result at the end.
                _assemblyFixtureTests.TryAdd(testMethod.AssemblyName, testMethodInfo.Parent.Parent);
                _classFixtureTests.TryAdd(testMethod.AssemblyName + testMethod.FullClassName, testMethodInfo.Parent);

                testContextForAssemblyInit = PlatformServiceProvider.Instance.GetTestContext(testMethod, properties, messageLogger, testContextForTestExecution.Context.CurrentTestOutcome);

                TestResult assemblyInitializeResult = RunAssemblyInitializeIfNeeded(testMethodInfo, testContextForAssemblyInit);

                if (assemblyInitializeResult.Outcome != UTF.UnitTestOutcome.Passed)
                {
                    result = [assemblyInitializeResult];
                }
                else
                {
                    testContextForClassInit = PlatformServiceProvider.Instance.GetTestContext(testMethod, properties, messageLogger, testContextForAssemblyInit.Context.CurrentTestOutcome);

                    TestResult classInitializeResult = testMethodInfo.Parent.GetResultOrRunClassInitialize(testContextForClassInit, assemblyInitializeResult.LogOutput, assemblyInitializeResult.LogError, assemblyInitializeResult.DebugTrace, assemblyInitializeResult.TestContextMessages);
                    DebugEx.Assert(testMethodInfo.Parent.IsClassInitializeExecuted, "IsClassInitializeExecuted should be true after attempting to run it.");
                    if (classInitializeResult.Outcome != UTF.UnitTestOutcome.Passed)
                    {
                        result = [classInitializeResult];
                    }
                    else
                    {
                        // Run the test method
                        testContextForTestExecution.SetOutcome(testContextForClassInit.Context.CurrentTestOutcome);
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

            testContextForClassCleanup = PlatformServiceProvider.Instance.GetTestContext(testMethod, properties, messageLogger, testContextForTestExecution.Context.CurrentTestOutcome);
            testMethodInfo?.Parent.RunClassCleanup(testContextForClassCleanup, _classCleanupManager, testMethodInfo, result);

            if (testMethodInfo?.Parent.Parent.IsAssemblyInitializeExecuted == true)
            {
                testContextForAssemblyCleanup = PlatformServiceProvider.Instance.GetTestContext(testMethod, properties, messageLogger, testContextForClassCleanup.Context.CurrentTestOutcome);
                RunAssemblyCleanupIfNeeded(testContextForAssemblyCleanup, _classCleanupManager, _typeCache, result);
            }

            return result;
        }
        catch (TypeInspectionException ex)
        {
            // Catch any exception thrown while inspecting the test method and return failure.
            return
            [
                new TestResult
                {
                    Outcome = UnitTestOutcome.Failed,
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

    private static TestResult RunAssemblyInitializeIfNeeded(TestMethodInfo testMethodInfo, ITestContext testContext)
    {
        var result = new TestResult { Outcome = UnitTestOutcome.Passed };

        try
        {
            testMethodInfo.Parent.Parent.RunAssemblyInitialize(testContext.Context);
        }
        catch (TestFailedException ex)
        {
            result = new TestResult { TestFailureException = ex, Outcome = ex.Outcome };
        }
        catch (Exception ex)
        {
            var testFailureException = new TestFailedException(UnitTestOutcome.Error, ex.TryGetMessage(), ex.TryGetStackTraceInformation());
            result = new TestResult { TestFailureException = testFailureException, Outcome = UnitTestOutcome.Error };
        }
        finally
        {
            var testContextImpl = testContext.Context as TestContextImplementation;
            result.LogOutput = testContextImpl?.GetOut();
            result.LogError = testContextImpl?.GetErr();
            result.DebugTrace = testContextImpl?.GetTrace();
            result.TestContextMessages = testContext.GetAndClearDiagnosticMessages();
        }

        return result;
    }

    private static void RunAssemblyCleanupIfNeeded(ITestContext testContext, ClassCleanupManager classCleanupManager, TypeCache typeCache, TestResult[] results)
    {
        if (!classCleanupManager.ShouldRunEndOfAssemblyCleanup)
        {
            return;
        }

        try
        {
            // TODO: We are using the same TestContext here for ClassCleanup and AssemblyCleanup.
            // They should be different.
            IEnumerable<TestClassInfo> classInfoCache = typeCache.ClassInfoListWithExecutableCleanupMethods;
            foreach (TestClassInfo classInfo in classInfoCache)
            {
                TestFailedException? ex = classInfo.ExecuteClassCleanup(testContext.Context);

                if (results.Length > 0 && ex is not null)
                {
#pragma warning disable IDE0056 // Use index operator
                    TestResult lastResult = results[results.Length - 1];
#pragma warning restore IDE0056 // Use index operator
                    lastResult.Outcome = UTF.UnitTestOutcome.Error;
                    lastResult.TestFailureException = ex;
                    return;
                }
            }

            IEnumerable<TestAssemblyInfo> assemblyInfoCache = typeCache.AssemblyInfoListWithExecutableCleanupMethods;
            foreach (TestAssemblyInfo assemblyInfo in assemblyInfoCache)
            {
                TestFailedException? ex = assemblyInfo.ExecuteAssemblyCleanup(testContext.Context);

                if (results.Length > 0 && ex is not null)
                {
#pragma warning disable IDE0056 // Use index operator
                    TestResult lastResult = results[results.Length - 1];
#pragma warning restore IDE0056 // Use index operator
                    lastResult.Outcome = UTF.UnitTestOutcome.Error;
                    lastResult.TestFailureException = ex;
                    return;
                }
            }
        }
        finally
        {
            if (results.Length > 0)
            {
#pragma warning disable IDE0056 // Use index operator
                TestResult lastResult = results[results.Length - 1];
#pragma warning restore IDE0056 // Use index operator
                var testContextImpl = testContext as TestContextImplementation;
                lastResult.LogOutput += testContextImpl?.GetOut();
                lastResult.LogError += testContextImpl?.GetErr();
                lastResult.DebugTrace += testContextImpl?.GetTrace();
                lastResult.TestContextMessages += testContext.GetAndClearDiagnosticMessages();
            }
        }
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
        if (testMethodInfo == null)
        {
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
        }

        // If test cannot be executed, then bail out.
        if (!testMethodInfo.IsRunnable)
        {
            {
                notRunnableResult =
                [
                    new TestResult
                    {
                        Outcome = UnitTestOutcome.NotRunnable,
                        IgnoreReason = testMethodInfo.NotRunnableReason,
                    },
                ];
                return false;
            }
        }

        bool shouldIgnoreClass = testMethodInfo.Parent.ClassType.IsIgnored(out string? ignoreMessageOnClass);
        bool shouldIgnoreMethod = testMethodInfo.MethodInfo.IsIgnored(out string? ignoreMessageOnMethod);

        string? ignoreMessage = ignoreMessageOnClass;
        if (StringEx.IsNullOrEmpty(ignoreMessage) && shouldIgnoreMethod)
        {
            ignoreMessage = ignoreMessageOnMethod;
        }

        if (shouldIgnoreClass || shouldIgnoreMethod)
        {
            notRunnableResult =
                [TestResult.CreateIgnoredResult(ignoreMessage)];
            return false;
        }

        notRunnableResult = null;
        return true;
    }

    internal void ForceCleanup(IDictionary<string, object?> sourceLevelParameters, IMessageLogger logger) => ClassCleanupManager.ForceCleanup(_typeCache, sourceLevelParameters, logger);
}
