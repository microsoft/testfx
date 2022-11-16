// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
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
        _reflectHelper = reflectHelper;
        _typeCache = new TypeCache(reflectHelper);

        // Populate the settings into the domain(Desktop workflow) performing discovery.
        // This would just be resetting the settings to itself in non desktop workflows.
        MSTestSettings.PopulateSettings(settings);
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
    public override object InitializeLifetimeService()
    {
        return null!;
    }

    /// <summary>
    /// Initialized the class cleanup manager for the unit test runner. Note, this can run over process-isolation,
    /// and all inputs must be serializable from host process.
    /// </summary>
    /// <param name="testsToRun">the list of tests that will be run in this execution.</param>
    /// <param name="classCleanupLifecycle">The assembly level class cleanup lifecycle.</param>
    internal void InitializeClassCleanupManager(ICollection<UnitTestElement> testsToRun, int? classCleanupLifecycle)
    {
        // We can't transport the Enum across AppDomain boundaries because of backwards and forwards compatibility.
        // So we're converting here if we can, or falling back to the default.
        var lifecycle = ClassCleanupBehavior.EndOfAssembly;
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
            var testContext = PlatformServiceProvider.Instance.GetTestContext(testMethod, writer, properties);
            testContext.SetOutcome(UTF.UnitTestOutcome.InProgress);

            // Get the testMethod
            var testMethodInfo = _typeCache.GetTestMethodInfo(
                testMethod,
                testContext,
                MSTestSettings.CurrentSettings.CaptureDebugTraces);

            if (_classCleanupManager == null && testMethodInfo != null && testMethodInfo.Parent.HasExecutableCleanupMethod)
            {
                PlatformServiceProvider.Instance.AdapterTraceLogger.LogWarning(Resource.OlderTFMVersionFoundClassCleanup);
            }

            if (!IsTestMethodRunnable(testMethod, testMethodInfo, out var notRunnableResult))
            {
                bool shouldRunClassCleanup = false;
                bool shouldRunCleanup = false;
                if (_classCleanupManager is null)
                {
                    return notRunnableResult;
                }

                DebugEx.Assert(testMethodInfo is not null, "testMethodInfo should not be null.");
                _classCleanupManager?.MarkTestComplete(testMethodInfo, testMethod, out shouldRunClassCleanup, out shouldRunCleanup);
                try
                {
                    // Class cleanup can throw exceptions in which case we need to ensure that we fail the test.
                    if (shouldRunClassCleanup)
                    {
                        testMethodInfo.Parent.RunClassCleanup(ClassCleanupBehavior.EndOfClass);
                    }

                    if (shouldRunCleanup)
                    {
                        RunCleanup();
                    }
                }
                finally
                {
                    using LogMessageListener logListener = new(MSTestSettings.CurrentSettings.CaptureDebugTraces);
                    var cleanupTestContextMessages = testContext.GetAndClearDiagnosticMessages();
                    string cleanupLogs = logListener.StandardOutput;
                    string? cleanupTrace = logListener.DebugTrace;
                    string cleanupErrorLogs = logListener.StandardError;

                    var lastResult = notRunnableResult[notRunnableResult.Length - 1];
                    lastResult.StandardOut += cleanupLogs;
                    lastResult.StandardError += cleanupErrorLogs;
                    lastResult.DebugTrace += cleanupTrace;
                    lastResult.TestContextMessages += cleanupTestContextMessages;
                }

                return notRunnableResult;
            }

            DebugEx.Assert(testMethodInfo is not null, "testMethodInfo should not be null.");
            var testMethodRunner = new TestMethodRunner(testMethodInfo, testMethod, testContext, MSTestSettings.CurrentSettings.CaptureDebugTraces);
            var result = testMethodRunner.Execute();
            TryToRunCleanups(testContext, testMethodInfo, testMethod, result);
            return result;
        }
        catch (TypeInspectionException ex)
        {
            // Catch any exception thrown while inspecting the test method and return failure.
            return new UnitTestResult[] { new UnitTestResult(ObjectModel.UnitTestOutcome.Failed, ex.Message) };
        }
    }

    /// <summary>
    /// Runs the class cleanup method.
    /// It returns any error information during the execution of the cleanup method.
    /// </summary>
    /// <returns> The <see cref="RunCleanupResult"/>. </returns>
    internal RunCleanupResult? RunCleanup()
    {
        // No cleanup methods to execute, then return.
        var assemblyInfoCache = _typeCache.AssemblyInfoListWithExecutableCleanupMethods;
        var classInfoCache = _typeCache.ClassInfoListWithExecutableCleanupMethods;
        if (!assemblyInfoCache.Any() && !classInfoCache.Any())
        {
            return null;
        }

        var result = new RunCleanupResult { Warnings = new List<string>() };

        using (var redirector = new LogMessageListener(MSTestSettings.CurrentSettings.CaptureDebugTraces))
        {
            try
            {
                RunClassCleanupMethods(classInfoCache, result.Warnings);
                RunAssemblyCleanup(assemblyInfoCache, result.Warnings);
            }
            finally
            {
                // Replacing the null character with a string.replace should work.
                // If this does not work for a specific dotnet version a custom function doing the same needs to be put in place.
                result.StandardOut = redirector.GetAndClearStandardOutput()?.Replace("\0", "\\0");
                result.StandardError = redirector.GetAndClearStandardError()?.Replace("\0", "\\0");
                result.DebugTrace = redirector.GetAndClearDebugTrace()?.Replace("\0", "\\0");
            }
        }

        return result;
    }

    private void TryToRunCleanups(ITestContext testContext, TestMethodInfo testMethodInfo, TestMethod testMethod, UnitTestResult[] results)
    {
        bool shouldRunClassCleanup = false;
        bool shouldRunCleanup = false;
        _classCleanupManager?.MarkTestComplete(testMethodInfo, testMethod, out shouldRunClassCleanup, out shouldRunCleanup);

        try
        {
            using LogMessageListener logListener =
              new(MSTestSettings.CurrentSettings.CaptureDebugTraces);
            if (shouldRunClassCleanup)
            {
                try
                {
                    // Class cleanup can throw exceptions in which case we need to ensure that we fail the test.
                    testMethodInfo.Parent.RunClassCleanup(ClassCleanupBehavior.EndOfClass);
                }
                finally
                {
                    string cleanupLogs = logListener.StandardOutput;
                    string? cleanupTrace = logListener.DebugTrace;
                    string cleanupErrorLogs = logListener.StandardError;
                    var cleanupTestContextMessages = testContext.GetAndClearDiagnosticMessages();

                    var lastResult = results[results.Length - 1];
                    lastResult.StandardOut += cleanupLogs;
                    lastResult.StandardError += cleanupErrorLogs;
                    lastResult.DebugTrace += cleanupTrace;
                    lastResult.TestContextMessages += cleanupTestContextMessages;
                }
            }

            if (shouldRunCleanup)
            {
                try
                {
                    RunCleanup();
                }
                finally
                {
                    string cleanupLogs = logListener.StandardOutput;
                    string? cleanupTrace = logListener.DebugTrace;
                    string cleanupErrorLogs = logListener.StandardError;
                    var cleanupTestContextMessages = testContext.GetAndClearDiagnosticMessages();

                    var lastResult = results[results.Length - 1];
                    lastResult.StandardOut += cleanupLogs;
                    lastResult.StandardError += cleanupErrorLogs;
                    lastResult.DebugTrace += cleanupTrace;
                    lastResult.TestContextMessages += cleanupTestContextMessages;
                }
            }
        }
        catch (Exception e)
        {
            results[results.Length - 1].Outcome = ObjectModel.UnitTestOutcome.Failed;
            results[results.Length - 1].ErrorMessage = e.Message;
            results[results.Length - 1].ErrorStackTrace = e.StackTrace;
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
                notRunnableResult = new UnitTestResult[]
                {
                    new UnitTestResult(
                        ObjectModel.UnitTestOutcome.NotFound,
                        string.Format(CultureInfo.CurrentCulture, Resource.TestNotFound, testMethod.Name)),
                };
                return false;
            }
        }

        // If test cannot be executed, then bail out.
        if (!testMethodInfo.IsRunnable)
        {
            {
                notRunnableResult = new UnitTestResult[]
                {
                    new UnitTestResult(ObjectModel.UnitTestOutcome.NotRunnable, testMethodInfo.NotRunnableReason),
                };
                return false;
            }
        }

        string? ignoreMessage = null;
        var isIgnoreAttributeOnClass =
            _reflectHelper.IsAttributeDefined<IgnoreAttribute>(testMethodInfo.Parent.ClassType, false);
        var isIgnoreAttributeOnMethod =
            _reflectHelper.IsAttributeDefined<IgnoreAttribute>(testMethodInfo.TestMethod, false);

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
                notRunnableResult = new[] { new UnitTestResult(ObjectModel.UnitTestOutcome.Ignored, ignoreMessage) };
                return false;
            }
        }

        notRunnableResult = null;
        return true;
    }

    /// <summary>
    /// Run assembly cleanup methods.
    /// </summary>
    /// <param name="assemblyInfoCache"> The assembly Info Cache. </param>
    /// <param name="warnings"> The warnings. </param>
    private static void RunAssemblyCleanup(IEnumerable<TestAssemblyInfo> assemblyInfoCache, IList<string> warnings)
    {
        foreach (var assemblyInfo in assemblyInfoCache)
        {
            DebugEx.Assert(assemblyInfo.HasExecutableCleanupMethod, "HasExecutableCleanupMethod should be true.");

            var warning = assemblyInfo.RunAssemblyCleanup();
            if (warning != null)
            {
                warnings.Add(warning);
            }
        }
    }

    /// <summary>
    /// Run class cleanup methods.
    /// </summary>
    /// <param name="classInfoCache"> The class Info Cache. </param>
    /// <param name="warnings"> The warnings. </param>
    private static void RunClassCleanupMethods(IEnumerable<TestClassInfo> classInfoCache, IList<string> warnings)
    {
        foreach (var classInfo in classInfoCache)
        {
            DebugEx.Assert(classInfo.HasExecutableCleanupMethod, "HasExecutableCleanupMethod should be true.");

            var warning = classInfo.RunClassCleanup();
            if (warning != null)
            {
                warnings.Add(warning);
            }
        }
    }

    private class ClassCleanupManager
    {
        private readonly ClassCleanupBehavior? _lifecycleFromMsTest;
        private readonly ClassCleanupBehavior _lifecycleFromAssembly;
        private readonly ReflectHelper _reflectHelper;
        private readonly Dictionary<string, HashSet<string>> _remainingTestsByClass;

        public ClassCleanupManager(
            IEnumerable<UnitTestElement> testsToRun,
            ClassCleanupBehavior? lifecycleFromMsTest,
            ClassCleanupBehavior lifecycleFromAssembly,
            ReflectHelper? reflectHelper = null)
        {
            _remainingTestsByClass = testsToRun.GroupBy(t => t.TestMethod.FullClassName)
                .ToDictionary(
                    g => g.Key,
                    g => new HashSet<string>(g.Select(t => t.DisplayName!)));
            _lifecycleFromMsTest = lifecycleFromMsTest;
            _lifecycleFromAssembly = lifecycleFromAssembly;
            _reflectHelper = reflectHelper ?? new ReflectHelper();
        }

        public void MarkTestComplete(TestMethodInfo testMethodInfo, TestMethod testMethod, out bool shouldClassCleanup, out bool shouldAssimplyCleanup)
        {
            shouldClassCleanup = false;
            var testsByClass = _remainingTestsByClass[testMethodInfo.TestClassName];
            lock (testsByClass)
            {
                testsByClass.Remove(testMethod.DisplayName!);
                if (testsByClass.Count == 0 && testMethodInfo.Parent.HasExecutableCleanupMethod)
                {
                    var cleanupLifecycle = _reflectHelper.GetClassCleanupBehavior(testMethodInfo.Parent)
                        ?? _lifecycleFromMsTest
                        ?? _lifecycleFromAssembly;

                    shouldClassCleanup = cleanupLifecycle == ClassCleanupBehavior.EndOfClass;
                    _remainingTestsByClass.Remove(testMethodInfo.TestClassName);
                }

                shouldAssimplyCleanup = _remainingTestsByClass.Count == 0;
            }
        }
    }
}
