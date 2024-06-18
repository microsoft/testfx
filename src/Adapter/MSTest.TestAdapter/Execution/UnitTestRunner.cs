// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Reflection;
using System.Security;

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Extensions;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Helpers;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using UTF = Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution;

/// <summary>
/// The runner that runs a single unit test. Also manages the assembly and class cleanup methods at the end of the run.
/// </summary>
internal class UnitTestRunner : MarshalByRefObject
{
    private readonly Dictionary<string, TestMethodInfo> _fixtureTests = new();

    /// <summary>
    /// Type cache.
    /// </summary>
    private readonly TypeCache _typeCache;

    /// <summary>
    /// Reflect helper.
    /// </summary>
    private readonly ReflectHelper _reflectHelper;

    /// <summary>
    /// Class cleanup manager.
    /// </summary>
    private ClassCleanupManager? _classCleanupManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="UnitTestRunner"/> class.
    /// </summary>
    /// <param name="settings"> Specifies adapter settings that need to be instantiated in the domain running these tests. </param>
    public UnitTestRunner(MSTestSettings settings)
        : this(settings, ReflectHelper.Instance)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="UnitTestRunner"/> class.
    /// </summary>
    /// <param name="settings"> Specifies adapter settings. </param>
    /// <param name="reflectHelper"> The reflect Helper. </param>
    internal UnitTestRunner(MSTestSettings settings, ReflectHelper reflectHelper)
    {
        // Populate the settings into the domain(Desktop workflow) performing discovery.
        // This would just be resetting the settings to itself in non desktop workflows.
        MSTestSettings.PopulateSettings(settings);

        _reflectHelper = reflectHelper;
        _typeCache = new TypeCache(reflectHelper);
    }

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

    /// <summary>
    /// Initialized the class cleanup manager for the unit test runner. Note, this can run over process-isolation,
    /// and all inputs must be serializable from host process.
    /// </summary>
    /// <param name="testsToRun">the list of tests that will be run in this execution.</param>
    /// <param name="classCleanupLifecycle">The assembly level class cleanup lifecycle.</param>
    internal void InitializeClassCleanupManager(IEnumerable<UnitTestElement> testsToRun, int? classCleanupLifecycle)
    {
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
                    : new(true, ObjectModel.UnitTestOutcome.Inconclusive, null);
            }

            if (fixtureType == Constants.ClassCleanupFixtureTrait)
            {
                return testMethodInfo.Parent.IsClassInitializeExecuted
                ? new(testMethodInfo.Parent.IsClassInitializeExecuted, GetOutcome(testMethodInfo.Parent.ClassCleanupException), testMethodInfo.Parent.ClassCleanupException?.Message)
                : new(true, ObjectModel.UnitTestOutcome.Inconclusive, null);
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

        return new(false, ObjectModel.UnitTestOutcome.Inconclusive, null);

        static ObjectModel.UnitTestOutcome GetOutcome(Exception? exception) => exception == null ? ObjectModel.UnitTestOutcome.Passed : ObjectModel.UnitTestOutcome.Failed;
    }

    /// <summary>
    /// Runs a single test.
    /// </summary>
    /// <param name="testMethod"> The test Method. </param>
    /// <param name="testContextProperties"> The test context properties. </param>
    /// <returns> The <see cref="UnitTestResult"/>. </returns>
    internal UnitTestResult[] RunSingleTest(TestMethod testMethod, IDictionary<string, object?> testContextProperties)
    {
        if (testMethod == null)
        {
            throw new ArgumentNullException(nameof(testMethod));
        }

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

            if (_classCleanupManager == null && testMethodInfo is { Parent.HasExecutableCleanupMethod: true })
            {
                PlatformServiceProvider.Instance.AdapterTraceLogger.LogWarning(Resource.OlderTFMVersionFoundClassCleanup);
            }

            if (!IsTestMethodRunnable(testMethod, testMethodInfo, out UnitTestResult[]? notRunnableResult))
            {
                if (_classCleanupManager is null)
                {
                    return notRunnableResult;
                }

                RunRequiredCleanups(testContext, testMethodInfo, testMethod, notRunnableResult);

                return notRunnableResult;
            }

            DebugEx.Assert(testMethodInfo is not null, "testMethodInfo should not be null.");

            // Keep track of all non-runnable methods so that we can return the appropriate result at the end.
            _fixtureTests[testMethod.AssemblyName] = testMethodInfo;
            _fixtureTests[testMethod.AssemblyName + testMethod.FullClassName] = testMethodInfo;

            var testMethodRunner = new TestMethodRunner(testMethodInfo, testMethod, testContext, MSTestSettings.CurrentSettings.CaptureDebugTraces);
            UnitTestResult[] result = testMethodRunner.Execute();
            RunRequiredCleanups(testContext, testMethodInfo, testMethod, result);
            return result;
        }
        catch (TypeInspectionException ex)
        {
            // Catch any exception thrown while inspecting the test method and return failure.
            return [new(ObjectModel.UnitTestOutcome.Failed, ex.Message)];
        }
    }

    private void RunRequiredCleanups(ITestContext testContext, TestMethodInfo? testMethodInfo, TestMethod testMethod, UnitTestResult[] results)
    {
        bool shouldRunClassCleanup = false;
        bool shouldRunClassAndAssemblyCleanup = false;
        if (testMethodInfo is not null)
        {
            _classCleanupManager?.MarkTestComplete(testMethodInfo, testMethod, out shouldRunClassCleanup, out shouldRunClassAndAssemblyCleanup);
        }

        using LogMessageListener logListener = new(MSTestSettings.CurrentSettings.CaptureDebugTraces);
        try
        {
            if (shouldRunClassCleanup)
            {
                testMethodInfo?.Parent.ExecuteClassCleanup();
            }

            if (shouldRunClassAndAssemblyCleanup)
            {
                IEnumerable<TestClassInfo> classInfoCache = _typeCache.ClassInfoListWithExecutableCleanupMethods;
                foreach (TestClassInfo classInfo in classInfoCache)
                {
                    classInfo.ExecuteClassCleanup();
                }

                IEnumerable<TestAssemblyInfo> assemblyInfoCache = _typeCache.AssemblyInfoListWithExecutableCleanupMethods;
                foreach (TestAssemblyInfo assemblyInfo in assemblyInfoCache)
                {
                    assemblyInfo.ExecuteAssemblyCleanup();
                }
            }
        }
        catch (Exception ex)
        {
            // We mainly expect TestFailedException here as each cleanup method is executed in a try-catch block but
            // for the sake of the catch-all mechanism, let's keep it as Exception.
            if (results.Length == 0)
            {
                return;
            }

            UnitTestResult lastResult = results[results.Length - 1];
            lastResult.Outcome = ObjectModel.UnitTestOutcome.Failed;
            lastResult.ErrorMessage = ex.Message;
            lastResult.ErrorStackTrace = ex.StackTrace;
        }
        finally
        {
            string? cleanupTestContextMessages = testContext.GetAndClearDiagnosticMessages();

            if (results.Length > 0)
            {
                UnitTestResult lastResult = results[results.Length - 1];
                lastResult.StandardOut += logListener.StandardOutput;
                lastResult.StandardError += logListener.StandardError;
                lastResult.DebugTrace += logListener.DebugTrace;
                lastResult.TestContextMessages += cleanupTestContextMessages;
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
                        ObjectModel.UnitTestOutcome.NotFound,
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
                    new(ObjectModel.UnitTestOutcome.NotRunnable, testMethodInfo.NotRunnableReason),
                ];
                return false;
            }
        }

        string? ignoreMessage = null;
        bool isIgnoreAttributeOnClass =
            _reflectHelper.IsNonDerivedAttributeDefined<IgnoreAttribute>(testMethodInfo.Parent.ClassType, false);
        bool isIgnoreAttributeOnMethod =
            _reflectHelper.IsNonDerivedAttributeDefined<IgnoreAttribute>(testMethodInfo.TestMethod, false);

        if (isIgnoreAttributeOnClass)
        {
            ignoreMessage = _reflectHelper.GetIgnoreMessage(testMethodInfo.Parent.ClassType.GetTypeInfo());
        }

        if (StringEx.IsNullOrEmpty(ignoreMessage) && isIgnoreAttributeOnMethod)
        {
            ignoreMessage = _reflectHelper.GetIgnoreMessage(testMethodInfo.TestMethod);
        }

        if (isIgnoreAttributeOnClass || isIgnoreAttributeOnMethod)
        {
            {
                notRunnableResult = [new UnitTestResult(ObjectModel.UnitTestOutcome.Ignored, ignoreMessage)];
                return false;
            }
        }

        notRunnableResult = null;
        return true;
    }

    private class ClassCleanupManager
    {
        private readonly ClassCleanupBehavior? _lifecycleFromMsTest;
        private readonly ClassCleanupBehavior _lifecycleFromAssembly;
        private readonly ReflectHelper _reflectHelper;
        private readonly ConcurrentDictionary<string, HashSet<string>> _remainingTestsByClass;

        public ClassCleanupManager(
            IEnumerable<UnitTestElement> testsToRun,
            ClassCleanupBehavior? lifecycleFromMsTest,
            ClassCleanupBehavior lifecycleFromAssembly,
            ReflectHelper reflectHelper)
        {
            IEnumerable<UnitTestElement> runnableTests = testsToRun.Where(t => t.Traits is null || !t.Traits.Any(t => t.Name == Constants.FixturesTestTrait));
            _remainingTestsByClass =
                new(runnableTests.GroupBy(t => t.TestMethod.FullClassName)
                    .ToDictionary(
                        g => g.Key,
                        g => new HashSet<string>(g.Select(t => t.TestMethod.UniqueName))));
            _lifecycleFromMsTest = lifecycleFromMsTest;
            _lifecycleFromAssembly = lifecycleFromAssembly;
            _reflectHelper = reflectHelper;
        }

        public void MarkTestComplete(TestMethodInfo testMethodInfo, TestMethod testMethod, out bool shouldRunEndOfClassCleanup,
            out bool shouldRunEndOfAssemblyCleanup)
        {
            shouldRunEndOfClassCleanup = false;
            shouldRunEndOfAssemblyCleanup = false;
            if (!_remainingTestsByClass.TryGetValue(testMethodInfo.TestClassName, out HashSet<string>? testsByClass))
            {
                return;
            }

            lock (testsByClass)
            {
                testsByClass.Remove(testMethod.UniqueName);
                if (testsByClass.Count == 0)
                {
                    _remainingTestsByClass.TryRemove(testMethodInfo.TestClassName, out _);
                    if (testMethodInfo.Parent.HasExecutableCleanupMethod)
                    {
                        ClassCleanupBehavior cleanupLifecycle = _reflectHelper.GetClassCleanupBehavior(testMethodInfo.Parent)
                            ?? _lifecycleFromMsTest
                            ?? _lifecycleFromAssembly;

                        shouldRunEndOfClassCleanup = cleanupLifecycle == ClassCleanupBehavior.EndOfClass;
                    }
                }

                shouldRunEndOfAssemblyCleanup = _remainingTestsByClass.IsEmpty;
            }
        }
    }
}
