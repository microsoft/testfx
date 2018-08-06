// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Globalization;
    using System.Linq;
    using System.Security;

    using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Helpers;
    using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;

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
                    var testMethodInfo = this.typeCache.GetTestMethodInfo(testMethod, testContext, MSTestSettings.CurrentSettings.CaptureDebugTraces);

                    // If the specified TestMethod could not be found, return a NotFound result.
                    if (testMethodInfo == null)
                    {
                        return new UnitTestResult[] { new UnitTestResult(UnitTestOutcome.NotFound, string.Format(CultureInfo.CurrentCulture, Resource.TestNotFound, testMethod.Name)) };
                    }

                    // If test cannot be executed, then bail out.
                    if (!testMethodInfo.IsRunnable)
                    {
                        return new UnitTestResult[] { new UnitTestResult(UnitTestOutcome.NotRunnable, testMethodInfo.NotRunnableReason) };
                    }

                    return new TestMethodRunner(testMethodInfo, testMethod, testContext, MSTestSettings.CurrentSettings.CaptureDebugTraces).Execute();
                }
            }
            catch (TypeInspectionException ex)
            {
                // Catch any exception thrown while inspecting the test method and return failure.
                return new UnitTestResult[] { new UnitTestResult(UnitTestOutcome.Failed, ex.Message) };
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
    }
}