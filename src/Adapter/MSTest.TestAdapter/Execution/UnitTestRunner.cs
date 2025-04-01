﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Security;

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Extensions;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Helpers;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Logging;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using UnitTestOutcome = Microsoft.VisualStudio.TestTools.UnitTesting.UnitTestOutcome;
using UTF = Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution;

/// <summary>
/// The runner that runs a single unit test. Also manages the assembly and class cleanup methods at the end of the run.
/// </summary>
internal sealed class UnitTestRunner : MarshalByRefObject
{
    private readonly ConcurrentDictionary<string, TestMethodInfo> _fixtureTests = new();
    private readonly TypeCache _typeCache;
    private readonly ReflectHelper _reflectHelper;
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

        PlatformServiceProvider.Instance.TestRunCancellationToken ??= new TestRunCancellationToken();

        _reflectHelper = reflectHelper;
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
            MSTestSettings.CurrentSettings.ClassCleanupLifecycle,
            lifecycle,
            _reflectHelper);
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
        if (_fixtureTests.TryGetValue(testMethod.AssemblyName + testMethod.FullClassName, out TestMethodInfo? testMethodInfo))
        {
            if (fixtureType == Constants.ClassInitializeFixtureTrait)
            {
                return testMethodInfo.Parent.IsClassInitializeExecuted
                    ? new(true, GetOutcome(testMethodInfo.Parent.ClassInitializationException))
                    : new(true, UnitTestOutcome.Inconclusive);
            }

            if (fixtureType == Constants.ClassCleanupFixtureTrait)
            {
                return testMethodInfo.Parent.IsClassInitializeExecuted
                ? new(true, GetOutcome(testMethodInfo.Parent.ClassCleanupException))
                : new(true, UnitTestOutcome.Inconclusive);
            }
        }

        if (_fixtureTests.TryGetValue(testMethod.AssemblyName, out testMethodInfo))
        {
            if (fixtureType == Constants.AssemblyInitializeFixtureTrait)
            {
                return new(true, GetOutcome(testMethodInfo.Parent.Parent.AssemblyInitializationException));
            }
            else if (fixtureType == Constants.AssemblyCleanupFixtureTrait)
            {
                return new(true, GetOutcome(testMethodInfo.Parent.Parent.AssemblyCleanupException));
            }
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

        try
        {
            using var writer = new ThreadSafeStringWriter(CultureInfo.InvariantCulture, "context");
            var properties = new Dictionary<string, object?>(testContextProperties);
            ITestContext testContextForTestExecution = PlatformServiceProvider.Instance.GetTestContext(testMethod, writer, properties, messageLogger, UTF.UnitTestOutcome.InProgress);

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
                _fixtureTests.TryAdd(testMethod.AssemblyName, testMethodInfo);
                _fixtureTests.TryAdd(testMethod.AssemblyName + testMethod.FullClassName, testMethodInfo);

                ITestContext testContextForAssemblyInit = PlatformServiceProvider.Instance.GetTestContext(testMethod, writer, properties, messageLogger, testContextForTestExecution.Context.CurrentTestOutcome);

                TestResult assemblyInitializeResult = RunAssemblyInitializeIfNeeded(testMethodInfo, testContextForAssemblyInit);

                if (assemblyInitializeResult.Outcome != UTF.UnitTestOutcome.Passed)
                {
                    result = [assemblyInitializeResult];
                }
                else
                {
                    ITestContext testContextForClassInit = PlatformServiceProvider.Instance.GetTestContext(testMethod, writer, properties, messageLogger, testContextForAssemblyInit.Context.CurrentTestOutcome);

                    TestResult classInitializeResult = testMethodInfo.Parent.GetResultOrRunClassInitialize(testContextForClassInit, assemblyInitializeResult.LogOutput!, assemblyInitializeResult.LogError!, assemblyInitializeResult.DebugTrace!, assemblyInitializeResult.TestContextMessages!);
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
                        result = testMethodRunner.Execute(classInitializeResult.LogOutput!, classInitializeResult.LogError!, classInitializeResult.DebugTrace!, classInitializeResult.TestContextMessages!);
                        if (retryAttribute is not null && !RetryBaseAttribute.IsAcceptableResultForRetry(result))
                        {
                            RetryResult retryResult = await retryAttribute.ExecuteAsync(
                                new RetryContext(
                                    () => Task.FromResult(
                                        testMethodRunner.Execute(
                                            classInitializeResult.LogOutput!,
                                            classInitializeResult.LogError!,
                                            classInitializeResult.DebugTrace!,
                                            classInitializeResult.TestContextMessages!)),
                                    result));

                            result = retryResult.TryGetLast() ?? throw ApplicationStateGuard.Unreachable();
                        }
                    }
                }
            }

            ITestContext testContextForClassCleanup = PlatformServiceProvider.Instance.GetTestContext(testMethod, writer, properties, messageLogger, testContextForTestExecution.Context.CurrentTestOutcome);
            testMethodInfo?.Parent.RunClassCleanup(testContextForClassCleanup, _classCleanupManager, testMethodInfo, testMethod, result);

            if (testMethodInfo?.Parent.Parent.IsAssemblyInitializeExecuted == true)
            {
                ITestContext testContextForAssemblyCleanup = PlatformServiceProvider.Instance.GetTestContext(testMethod, writer, properties, messageLogger, testContextForClassCleanup.Context.CurrentTestOutcome);
                RunAssemblyCleanupIfNeeded(testContextForAssemblyCleanup, _classCleanupManager, _typeCache, result);
            }

            return result;
        }
        catch (TypeInspectionException ex)
        {
            // Catch any exception thrown while inspecting the test method and return failure.
            return
            [
                new TestResult()
                {
                    Outcome = UTF.UnitTestOutcome.Failed,
                    IgnoreReason = ex.Message,
                }
            ];
        }
    }

    private static TestResult RunAssemblyInitializeIfNeeded(TestMethodInfo testMethodInfo, ITestContext testContext)
    {
        string? initializationLogs = string.Empty;
        string? initializationErrorLogs = string.Empty;
        string? initializationTrace = string.Empty;
        string? initializationTestContextMessages = string.Empty;
        var result = new TestResult() { Outcome = UTF.UnitTestOutcome.Passed };

        try
        {
            LogMessageListener? logListener = null;
            try
            {
                testMethodInfo.Parent.Parent.RunAssemblyInitialize(testContext.Context, out logListener);
            }
            finally
            {
                if (logListener is not null)
                {
                    FixtureMethodRunner.RunOnContext(testMethodInfo.Parent.Parent.ExecutionContext, () =>
                    {
                        initializationLogs = logListener.GetAndClearStandardOutput();
                        initializationErrorLogs = logListener.GetAndClearStandardError();
                        initializationTrace = logListener.GetAndClearDebugTrace();
                        initializationTestContextMessages = testContext.GetAndClearDiagnosticMessages();
                        logListener.Dispose();
                    });
                }
            }
        }
        catch (TestFailedException ex)
        {
            result = new TestResult() { TestFailureException = ex, Outcome = ex.Outcome };
        }
        catch (Exception ex)
        {
            var testFailureException = new TestFailedException(UnitTestOutcome.Error, ex.TryGetMessage(), ex.TryGetStackTraceInformation());
            result = new TestResult() { TestFailureException = testFailureException, Outcome = UnitTestOutcome.Error };
        }
        finally
        {
            result.LogOutput = initializationLogs;
            result.LogError = initializationErrorLogs;
            result.DebugTrace = initializationTrace;
            result.TestContextMessages = initializationTestContextMessages;
        }

        return result;
    }

    private static void RunAssemblyCleanupIfNeeded(ITestContext testContext, ClassCleanupManager classCleanupManager, TypeCache typeCache, TestResult[] results)
    {
        if (!classCleanupManager.ShouldRunEndOfAssemblyCleanup)
        {
            return;
        }

        string? initializationLogs = string.Empty;
        string? initializationErrorLogs = string.Empty;
        string? initializationTrace = string.Empty;
        string? initializationTestContextMessages = string.Empty;
        try
        {
            LogMessageListener? logListener = null;
            // TODO: We are using the same TestContext here for ClassCleanup and AssemblyCleanup.
            // They should be different.
            IEnumerable<TestClassInfo> classInfoCache = typeCache.ClassInfoListWithExecutableCleanupMethods;
            foreach (TestClassInfo classInfo in classInfoCache)
            {
                TestFailedException? ex = classInfo.ExecuteClassCleanup(testContext.Context, out logListener);
                if (logListener is not null)
                {
                    FixtureMethodRunner.RunOnContext(classInfo.ExecutionContext, () =>
                    {
                        initializationLogs += logListener.GetAndClearStandardOutput();
                        initializationErrorLogs += logListener.GetAndClearStandardError();
                        initializationTrace += logListener.GetAndClearDebugTrace();
                        initializationTestContextMessages += testContext.GetAndClearDiagnosticMessages();
                        logListener.Dispose();
                        logListener = null;
                    });
                }

                if (ex is not null && results.Length > 0)
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
                TestFailedException? ex = assemblyInfo.ExecuteAssemblyCleanup(testContext.Context, ref logListener);
                if (logListener is not null)
                {
                    FixtureMethodRunner.RunOnContext(assemblyInfo.ExecutionContext, () =>
                    {
                        initializationLogs += logListener.GetAndClearStandardOutput();
                        initializationErrorLogs += logListener.GetAndClearStandardError();
                        initializationTrace += logListener.GetAndClearDebugTrace();
                        initializationTestContextMessages += testContext.GetAndClearDiagnosticMessages();
                        logListener.Dispose();
                        logListener = null;
                    });
                }

                if (ex is not null && results.Length > 0)
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
                lastResult.LogOutput += initializationLogs;
                lastResult.LogError += initializationErrorLogs;
                lastResult.DebugTrace += initializationTrace;
                lastResult.TestContextMessages += initializationTestContextMessages;
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
                    new TestResult()
                    {
                        Outcome = UTF.UnitTestOutcome.NotFound,
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
                    new TestResult()
                    {
                        Outcome = UTF.UnitTestOutcome.NotRunnable,
                        IgnoreReason = testMethodInfo.NotRunnableReason,
                    },
                ];
                return false;
            }
        }

        bool shouldIgnoreClass = testMethodInfo.Parent.ClassType.IsIgnored(out string? ignoreMessageOnClass);
        bool shouldIgnoreMethod = testMethodInfo.TestMethod.IsIgnored(out string? ignoreMessageOnMethod);

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
