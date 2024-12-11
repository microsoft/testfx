// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Security;

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Extensions;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Helpers;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using UnitTestOutcome = Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel.UnitTestOutcome;
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
    public UnitTestRunner(MSTestSettings settings, UnitTestElement[] testsToRun, int? classCleanupLifecycle)
        : this(settings, testsToRun, classCleanupLifecycle, ReflectHelper.Instance)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="UnitTestRunner"/> class.
    /// </summary>
    /// <param name="settings"> Specifies adapter settings. </param>
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
                    ? new(true, GetOutcome(testMethodInfo.Parent.ClassInitializationException), testMethodInfo.Parent.ClassInitializationException?.Message)
                    : new(true, UnitTestOutcome.Inconclusive, null);
            }

            if (fixtureType == Constants.ClassCleanupFixtureTrait)
            {
                return testMethodInfo.Parent.IsClassInitializeExecuted
                ? new(testMethodInfo.Parent.IsClassInitializeExecuted, GetOutcome(testMethodInfo.Parent.ClassCleanupException), testMethodInfo.Parent.ClassCleanupException?.Message)
                : new(true, UnitTestOutcome.Inconclusive, null);
            }
        }

        if (_fixtureTests.TryGetValue(testMethod.AssemblyName, out testMethodInfo))
        {
            if (fixtureType == Constants.AssemblyInitializeFixtureTrait)
            {
                return new(true, GetOutcome(testMethodInfo.Parent.Parent.AssemblyInitializationException), testMethodInfo.Parent.Parent.AssemblyInitializationException?.Message);
            }
            else if (fixtureType == Constants.AssemblyCleanupFixtureTrait)
            {
                return new(true, GetOutcome(testMethodInfo.Parent.Parent.AssemblyCleanupException), testMethodInfo.Parent.Parent.AssemblyInitializationException?.Message);
            }
        }

        return new(false, UnitTestOutcome.Inconclusive, null);

        // Local functions
        static UnitTestOutcome GetOutcome(Exception? exception) => exception == null ? UnitTestOutcome.Passed : UnitTestOutcome.Failed;
    }

    /// <summary>
    /// Runs a single test.
    /// </summary>
    /// <param name="testMethod"> The test Method. </param>
    /// <param name="testContextProperties"> The test context properties. </param>
    /// <returns> The <see cref="UnitTestResult"/>. </returns>
    internal UnitTestResult[] RunSingleTest(TestMethod testMethod, IDictionary<string, object?> testContextProperties)
    {
        Guard.NotNull(testMethod);

        try
        {
            using var writer = new ThreadSafeStringWriter(CultureInfo.InvariantCulture, "context");
            var properties = new Dictionary<string, object?>(testContextProperties);
            ITestContext testContext = PlatformServiceProvider.Instance.GetTestContext(testMethod, writer, properties);
            testContext.SetOutcome(UTF.UnitTestOutcome.InProgress);

            // Get the testMethod
            TestMethodInfo? testMethodInfo = _typeCache.GetTestMethodInfo(
                testMethod,
                testContext,
                MSTestSettings.CurrentSettings.CaptureDebugTraces);

            UnitTestResult[] result;
            if (!IsTestMethodRunnable(testMethod, testMethodInfo, out UnitTestResult[]? notRunnableResult))
            {
                result = notRunnableResult;
            }
            else
            {
                DebugEx.Assert(testMethodInfo is not null, "testMethodInfo should not be null.");

                // Keep track of all non-runnable methods so that we can return the appropriate result at the end.
                _fixtureTests.TryAdd(testMethod.AssemblyName, testMethodInfo);
                _fixtureTests.TryAdd(testMethod.AssemblyName + testMethod.FullClassName, testMethodInfo);

                UnitTestResult assemblyInitializeResult = RunAssemblyInitializeIfNeeded(testMethodInfo, testContext);

                if (assemblyInitializeResult.Outcome != UnitTestOutcome.Passed)
                {
                    result = [assemblyInitializeResult];
                }
                else
                {
                    UnitTestResult classInitializeResult = testMethodInfo.Parent.GetResultOrRunClassInitialize(testContext, assemblyInitializeResult.StandardOut!, assemblyInitializeResult.StandardError!, assemblyInitializeResult.DebugTrace!, assemblyInitializeResult.TestContextMessages!);
                    DebugEx.Assert(testMethodInfo.Parent.IsClassInitializeExecuted, "IsClassInitializeExecuted should be true after attempting to run it.");
                    if (classInitializeResult.Outcome != UnitTestOutcome.Passed)
                    {
                        result = [classInitializeResult];
                    }
                    else
                    {
                        // Run the test method
                        var testMethodRunner = new TestMethodRunner(testMethodInfo, testMethod, testContext);
                        result = testMethodRunner.Execute(classInitializeResult.StandardOut!, classInitializeResult.StandardError!, classInitializeResult.DebugTrace!, classInitializeResult.TestContextMessages!);
                    }
                }
            }

            if (testMethodInfo?.Parent.Parent.IsAssemblyInitializeExecuted == true)
            {
                testMethodInfo.Parent.RunClassCleanup(testContext, _classCleanupManager, testMethodInfo, testMethod, result);
                RunAssemblyCleanupIfNeeded(testContext, _classCleanupManager, _typeCache, result);
            }

            return result;
        }
        catch (TypeInspectionException ex)
        {
            // Catch any exception thrown while inspecting the test method and return failure.
            return [new(UnitTestOutcome.Failed, ex.Message)];
        }
    }

    private static UnitTestResult RunAssemblyInitializeIfNeeded(TestMethodInfo testMethodInfo, ITestContext testContext)
    {
        string? initializationLogs = string.Empty;
        string? initializationErrorLogs = string.Empty;
        string? initializationTrace = string.Empty;
        string? initializationTestContextMessages = string.Empty;
        UnitTestResult result = new(UnitTestOutcome.Passed, null);

        try
        {
            using LogMessageListener logListener = new(MSTestSettings.CurrentSettings.CaptureDebugTraces);
            try
            {
                testMethodInfo.Parent.Parent.RunAssemblyInitialize(testContext.Context);
            }
            finally
            {
                initializationLogs = logListener.GetAndClearStandardOutput();
                initializationErrorLogs = logListener.GetAndClearStandardError();
                initializationTrace = logListener.GetAndClearDebugTrace();
                initializationTestContextMessages = testContext.GetAndClearDiagnosticMessages();
            }
        }
        catch (TestFailedException ex)
        {
            result = new(ex);
        }
        catch (Exception ex)
        {
            result = new UnitTestResult(new TestFailedException(UnitTestOutcome.Error, ex.TryGetMessage(), ex.TryGetStackTraceInformation()));
        }
        finally
        {
            result.StandardOut = initializationLogs;
            result.StandardError = initializationErrorLogs;
            result.DebugTrace = initializationTrace;
            result.TestContextMessages = initializationTestContextMessages;
        }

        return result;
    }

    private static void RunAssemblyCleanupIfNeeded(ITestContext testContext, ClassCleanupManager classCleanupManager, TypeCache typeCache, UnitTestResult[] results)
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
            using LogMessageListener logListener = new(MSTestSettings.CurrentSettings.CaptureDebugTraces);
            try
            {
                IEnumerable<TestClassInfo> classInfoCache = typeCache.ClassInfoListWithExecutableCleanupMethods;
                foreach (TestClassInfo classInfo in classInfoCache)
                {
                    classInfo.ExecuteClassCleanup();
                }

                IEnumerable<TestAssemblyInfo> assemblyInfoCache = typeCache.AssemblyInfoListWithExecutableCleanupMethods;
                foreach (TestAssemblyInfo assemblyInfo in assemblyInfoCache)
                {
                    assemblyInfo.ExecuteAssemblyCleanup();
                }
            }
            finally
            {
                initializationLogs = logListener.GetAndClearStandardOutput();
                initializationErrorLogs = logListener.GetAndClearStandardError();
                initializationTrace = logListener.GetAndClearDebugTrace();
                initializationTestContextMessages = testContext.GetAndClearDiagnosticMessages();
            }
        }
        catch (Exception ex)
        {
            if (results.Length > 0)
            {
#pragma warning disable IDE0056 // Use index operator
                UnitTestResult lastResult = results[results.Length - 1];
#pragma warning restore IDE0056 // Use index operator
                lastResult.Outcome = UnitTestOutcome.Error;
                lastResult.ErrorMessage = ex.Message;
                lastResult.ErrorStackTrace = ex.StackTrace;
            }
        }
        finally
        {
            if (results.Length > 0)
            {
#pragma warning disable IDE0056 // Use index operator
                UnitTestResult lastResult = results[results.Length - 1];
#pragma warning restore IDE0056 // Use index operator
                lastResult.StandardOut += initializationLogs;
                lastResult.StandardError += initializationErrorLogs;
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
    private bool IsTestMethodRunnable(
        TestMethod testMethod,
        TestMethodInfo? testMethodInfo,
        [NotNullWhen(false)] out UnitTestResult[]? notRunnableResult)
    {
        // If the specified TestMethod could not be found, return a NotFound result.
        if (testMethodInfo == null)
        {
            {
                notRunnableResult =
                [
                    new(
                        UnitTestOutcome.NotFound,
                        string.Format(CultureInfo.CurrentCulture, Resource.TestNotFound, testMethod.Name)),
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
                    new(UnitTestOutcome.NotRunnable, testMethodInfo.NotRunnableReason),
                ];
                return false;
            }
        }

        IgnoreAttribute? ignoreAttributeOnClass =
            _reflectHelper.GetFirstNonDerivedAttributeOrDefault<IgnoreAttribute>(testMethodInfo.Parent.ClassType, inherit: false);
        string? ignoreMessage = ignoreAttributeOnClass?.IgnoreMessage;

        IgnoreAttribute? ignoreAttributeOnMethod =
            _reflectHelper.GetFirstNonDerivedAttributeOrDefault<IgnoreAttribute>(testMethodInfo.TestMethod, inherit: false);

        if (StringEx.IsNullOrEmpty(ignoreMessage) && ignoreAttributeOnMethod is not null)
        {
            ignoreMessage = ignoreAttributeOnMethod.IgnoreMessage;
        }

        if (ignoreAttributeOnClass is not null || ignoreAttributeOnMethod is not null)
        {
            notRunnableResult = [new UnitTestResult(UnitTestOutcome.Ignored, ignoreMessage)];
            return false;
        }

        notRunnableResult = null;
        return true;
    }

    internal void ForceCleanup() => ClassCleanupManager.ForceCleanup(_typeCache);
}
