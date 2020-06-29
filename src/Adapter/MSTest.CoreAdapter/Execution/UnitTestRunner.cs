// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Globalization;
    using System.Linq;
    using System.Reflection;
    using System.Security;

    using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Extensions;
    using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Helpers;
    using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using TPOM = Microsoft.VisualStudio.TestPlatform.ObjectModel;
    using UTF = Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// The runner that runs a single unit test. Also manages the assembly and class cleanup methods at the end of the run.
    /// </summary>
    internal class UnitTestRunner : MarshalByRefObject
    {
        /// <summary>
        /// Type cache
        /// </summary>
        private readonly TypeCache typeCache;

        /// <summary>
        /// Reflect helper
        /// </summary>
        private readonly ReflectHelper reflectHelper;

        /// <summary>
        /// Class cleanup manager
        /// </summary>
        private ClassCleanupManager classCleanupManager;

        /// <summary>
        /// Initializes a new instance of the <see cref="UnitTestRunner"/> class.
        /// </summary>
        /// <param name="settings"> Specifies adapter settings that need to be instantiated in the domain running these tests. </param>
        public UnitTestRunner(MSTestSettings settings)
            : this(settings, new ReflectHelper())
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="UnitTestRunner"/> class.
        /// </summary>
        /// <param name="settings"> Specifies adapter settings. </param>
        /// <param name="reflectHelper"> The reflect Helper. </param>
        internal UnitTestRunner(MSTestSettings settings, ReflectHelper reflectHelper)
        {
            this.reflectHelper = reflectHelper;
            this.typeCache = new TypeCache(reflectHelper);

            // Populate the settings into the domain(Desktop workflow) performing discovery.
            // This would just be resettings the settings to itself in non desktop workflows.
            MSTestSettings.PopulateSettings(settings);
        }

        /// <summary>
        /// Returns object to be used for controlling lifetime, null means infinite lifetime.
        /// </summary>
        /// <returns>
        /// The <see cref="object"/>.
        /// </returns>
        [SecurityCritical]
        public override object InitializeLifetimeService()
        {
            return null;
        }

        /// <summary>
        /// Initialized the class cleanup manager for the unit test runner. Note, this can run over process-isolation,
        /// and all inputs must be serializable from host process.
        /// </summary>
        /// <param name="testsToRun">the list of tests that will be run in this execution</param>
        /// <param name="classCleanupLifecycle">The assembly level class cleanup lifecycle</param>
        internal void InitializeClassCleanupManager(ICollection<UnitTestElement> testsToRun, ClassCleanupLifecycle classCleanupLifecycle)
        {
            this.classCleanupManager = new ClassCleanupManager(
                testsToRun,
                MSTestSettings.CurrentSettings.ClassCleanupLifecycle,
                classCleanupLifecycle,
                this.reflectHelper);
        }

        /// <summary>
        /// Runs a single test.
        /// </summary>
        /// <param name="testMethod"> The test Method. </param>
        /// <param name="testContextProperties"> The test context properties. </param>
        /// <returns> The <see cref="UnitTestResult"/>. </returns>
        internal UnitTestResult[] RunSingleTest(TestMethod testMethod, IDictionary<string, object> testContextProperties)
        {
            if (testMethod == null)
            {
                throw new ArgumentNullException("testMethod");
            }

            try
            {
                using (var writer = new ThreadSafeStringWriter(CultureInfo.InvariantCulture))
                {
                    var properties = new Dictionary<string, object>(testContextProperties);
                    var testContext = PlatformServiceProvider.Instance.GetTestContext(testMethod, writer, properties);
                    testContext.SetOutcome(TestTools.UnitTesting.UnitTestOutcome.InProgress);

                    // Get the testMethod
                    var testMethodInfo = this.typeCache.GetTestMethodInfo(
                        testMethod,
                        testContext,
                        MSTestSettings.CurrentSettings.CaptureDebugTraces);

                    if (!this.IsTestMethodRunnable(testMethod, testMethodInfo, out var notRunnableResult))
                    {
                        bool shouldRunClassCleanup = false;
                        this.classCleanupManager?.MarkTestComplete(testMethodInfo, out shouldRunClassCleanup);
                        if (shouldRunClassCleanup)
                        {
                            testMethodInfo.Parent.RunClassCleanup(ClassCleanupLifecycle.EndOfClass);
                        }

                        return notRunnableResult;
                    }

                    var result = new TestMethodRunner(
                        testMethodInfo,
                        testMethod,
                        testContext,
                        MSTestSettings.CurrentSettings.CaptureDebugTraces,
                        this.reflectHelper).Execute();
                    this.RunClassCleanupIfEndOfClass(testMethodInfo, result);
                    return result;
                }
            }
            catch (TypeInspectionException ex)
            {
                // Catch any exception thrown while inspecting the test method and return failure.
                return new UnitTestResult[] { new UnitTestResult(ObjectModel.UnitTestOutcome.Failed, ex.Message) };
            }
        }

        /// <summary>
        /// Runs the class cleanup method.
        /// It returns any error information during the execution of the cleanup method
        /// </summary>
        /// <returns> The <see cref="RunCleanupResult"/>. </returns>
        internal RunCleanupResult RunCleanup()
        {
            // No cleanup methods to execute, then return.
            var assemblyInfoCache = this.typeCache.AssemblyInfoListWithExecutableCleanupMethods;
            var classInfoCache = this.typeCache.ClassInfoListWithExecutableCleanupMethods;
            if (!assemblyInfoCache.Any() && !classInfoCache.Any())
            {
                return null;
            }

            var result = new RunCleanupResult { Warnings = new List<string>() };

            using (var redirector = new LogMessageListener(MSTestSettings.CurrentSettings.CaptureDebugTraces))
            {
                try
                {
                    this.RunClassCleanupMethods(classInfoCache, result.Warnings);
                    this.RunAssemblyCleanup(assemblyInfoCache, result.Warnings);
                }
                finally
                {
                    // Replacing the null character with a string.replace should work.
                    // If this does not work for a specific dotnet version a custom function doing the same needs to be put in place.
                    result.StandardOut = redirector.StandardOutput?.Replace("\0", "\\0");
                    result.StandardError = redirector.StandardError?.Replace("\0", "\\0");
                    result.DebugTrace = redirector.DebugTrace?.Replace("\0", "\\0");
                }
            }

            return result;
        }

        private void RunClassCleanupIfEndOfClass(TestMethodInfo testMethodInfo, UnitTestResult[] results)
        {
            bool shouldRunClassCleanup = false;
            this.classCleanupManager?.MarkTestComplete(testMethodInfo, out shouldRunClassCleanup);
            if (shouldRunClassCleanup)
            {
                string cleanupLogs = string.Empty;
                string cleanupTrace = string.Empty;
                string cleanupErrorLogs = string.Empty;

                try
                {
                    using (LogMessageListener logListener =
                        new LogMessageListener(MSTestSettings.CurrentSettings.CaptureDebugTraces))
                    {
                        try
                        {
                            // Class cleanup can throw exceptions in which case we need to ensure that we fail the test.
                            testMethodInfo.Parent.RunClassCleanup(ClassCleanupLifecycle.EndOfClass);
                        }
                        finally
                        {
                            cleanupLogs = logListener.StandardOutput;
                            cleanupTrace = logListener.DebugTrace;
                            cleanupErrorLogs = logListener.StandardError;
                            var lastResult = results[results.Length - 1];
                            lastResult.StandardOut = cleanupLogs + lastResult.StandardOut;
                            lastResult.StandardError = cleanupErrorLogs + lastResult.StandardError;
                            lastResult.DebugTrace = cleanupTrace + lastResult.DebugTrace;
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
        }

        /// <summary>
        /// Whether the given testMethod is runnable
        /// </summary>
        /// <param name="testMethod">The testMethod</param>
        /// <param name="testMethodInfo">The testMethodInfo</param>
        /// <param name="notRunnableResult">The results to return if the test method is not runnable</param>
        /// <returns>whether the given testMethod is runnable</returns>
        private bool IsTestMethodRunnable(
            TestMethod testMethod,
            TestMethodInfo testMethodInfo,
            out UnitTestResult[] notRunnableResult)
        {
            // If the specified TestMethod could not be found, return a NotFound result.
            if (testMethodInfo == null)
            {
                {
                    notRunnableResult = new UnitTestResult[]
                    {
                        new UnitTestResult(
                            ObjectModel.UnitTestOutcome.NotFound,
                            string.Format(CultureInfo.CurrentCulture, Resource.TestNotFound, testMethod.Name))
                    };
                    return false;
                }
            }

            // If test cannot be executed, then bail out.
            if (!testMethodInfo.IsRunnable)
            {
                {
                    notRunnableResult = new UnitTestResult[]
                        { new UnitTestResult(ObjectModel.UnitTestOutcome.NotRunnable, testMethodInfo.NotRunnableReason) };
                    return false;
                }
            }

            string ignoreMessage = null;
            var isIgnoreAttributeOnClass =
                this.reflectHelper.IsAttributeDefined(testMethodInfo.Parent.ClassType, typeof(UTF.IgnoreAttribute), false);
            var isIgnoreAttributeOnMethod =
                this.reflectHelper.IsAttributeDefined(testMethodInfo.TestMethod, typeof(UTF.IgnoreAttribute), false);

            if (isIgnoreAttributeOnClass)
            {
                ignoreMessage = this.reflectHelper.GetIgnoreMessage(testMethodInfo.Parent.ClassType.GetTypeInfo());
            }

            if (string.IsNullOrEmpty(ignoreMessage) && isIgnoreAttributeOnMethod)
            {
                ignoreMessage = this.reflectHelper.GetIgnoreMessage(testMethodInfo.TestMethod);
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
        /// Run assembly cleanup methods
        /// </summary>
        /// <param name="assemblyInfoCache"> The assembly Info Cache. </param>
        /// <param name="warnings"> The warnings. </param>
        private void RunAssemblyCleanup(IEnumerable<TestAssemblyInfo> assemblyInfoCache, IList<string> warnings)
        {
            foreach (var assemblyInfo in assemblyInfoCache)
            {
                Debug.Assert(assemblyInfo.HasExecutableCleanupMethod, "HasExecutableCleanupMethod should be true.");

                var warning = assemblyInfo.RunAssemblyCleanup();
                if (warning != null)
                {
                    warnings.Add(warning);
                }
            }
        }

        /// <summary>
        /// Run class cleanup methods
        /// </summary>
        /// <param name="classInfoCache"> The class Info Cache. </param>
        /// <param name="warnings"> The warnings. </param>
        private void RunClassCleanupMethods(IEnumerable<TestClassInfo> classInfoCache, IList<string> warnings)
        {
            foreach (var classInfo in classInfoCache)
            {
                Debug.Assert(classInfo.HasExecutableCleanupMethod, "HasExecutableCleanupMethod should be true.");

                var warning = classInfo.RunClassCleanup();
                if (warning != null)
                {
                    warnings.Add(warning);
                }
            }
        }

        private class ClassCleanupManager
        {
            private readonly ClassCleanupLifecycle? lifecycleFromMsTest;
            private readonly ClassCleanupLifecycle lifecycleFromAssembly;
            private readonly ReflectHelper reflectHelper;
            private readonly Dictionary<string, HashSet<string>> remainingTestsByClass;

            public ClassCleanupManager(
                IEnumerable<UnitTestElement> testsToRun,
                ClassCleanupLifecycle? lifecycleFromMsTest,
                ClassCleanupLifecycle lifecycleFromAssembly,
                ReflectHelper reflectHelper = null)
            {
                this.remainingTestsByClass = testsToRun.GroupBy(t => t.TestMethod.FullClassName)
                    .ToDictionary(
                        g => g.Key,
                        g => new HashSet<string>(g.Select(t => t.DisplayName)));
                this.lifecycleFromMsTest = lifecycleFromMsTest;
                this.lifecycleFromAssembly = lifecycleFromAssembly;
                this.reflectHelper = reflectHelper ?? new ReflectHelper();
            }

            public void MarkTestComplete(TestMethodInfo testMethod, out bool shouldCleanup)
            {
                shouldCleanup = false;
                var testsByClass = this.remainingTestsByClass[testMethod.TestClassName];
                lock (testsByClass)
                {
                    testsByClass.Remove(testMethod.TestMethodName);
                    if (testsByClass.Count == 0 && testMethod.Parent.HasExecutableCleanupMethod)
                    {
                        var cleanupLifecycle = this.reflectHelper.GetClassCleanupSequence(testMethod.Parent.ClassType.GetTypeInfo())
                            ?? this.lifecycleFromMsTest ?? this.lifecycleFromAssembly;
                        shouldCleanup = cleanupLifecycle == ClassCleanupLifecycle.EndOfClass;
                    }
                }
            }

            private static string ClassNameForTest(TPOM.TestCase testCase) =>
                testCase.GetPropertyValue(Constants.TestClassNameProperty) as string;
        }
    }
}