// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Discovery
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Globalization;
    using System.Linq;
    using System.Reflection;
    using System.Security;
    using System.Text;

    using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution;
    using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Helpers;
    using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;

    using ITestContext = Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface.ITestContext;
    using UTF = Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// Enumerates through all types in the assembly in search of valid test methods.
    /// </summary>
    internal class AssemblyEnumerator : MarshalByRefObject
    {
        /// <summary>
        /// Helper for reflection API's.
        /// </summary>
        private static readonly ReflectHelper ReflectHelper = new ReflectHelper();

        /// <summary>
        /// Type cache
        /// </summary>
        private readonly TypeCache typeCache = new TypeCache(ReflectHelper);

        /// <summary>
        /// Initializes a new instance of the <see cref="AssemblyEnumerator"/> class.
        /// </summary>
        public AssemblyEnumerator()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="AssemblyEnumerator"/> class.
        /// </summary>
        /// <param name="settings">The settings for the session.</param>
        /// <remarks>Use this constructor when creating this object in a new app domain so the settings for this app domain are set.</remarks>
        public AssemblyEnumerator(MSTestSettings settings)
        {
            // Populate the settings into the domain(Desktop workflow) performing discovery.
            // This would just be resettings the settings to itself in non desktop workflows.
            MSTestSettings.PopulateSettings(settings);
        }

        /// <summary>
        /// Sets run settings to use for current discovery session.
        /// </summary>
        public string RunSettingsXml { private get; set; }

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
        /// Enumerates through all types in the assembly in search of valid test methods.
        /// </summary>
        /// <param name="assemblyFileName">The assembly file name.</param>
        /// <param name="warnings">Contains warnings if any, that need to be passed back to the caller.</param>
        /// <returns>A collection of Test Elements.</returns>
        internal ICollection<UnitTestElement> EnumerateAssembly(string assemblyFileName, out ICollection<string> warnings)
        {
            Debug.Assert(!string.IsNullOrWhiteSpace(assemblyFileName), "Invalid assembly file name.");

            string runSettingsXml = this.RunSettingsXml;

            var warningMessages = new List<string>();
            var tests = new List<UnitTestElement>();

            Assembly assembly;
            if (assemblyFileName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            {
                // We only want to load the source assembly in reflection only context in UWP scenarios where it is always an exe.
                // For normal test assemblies continue loading it in the default context since:
                // 1. There isn't much benefit in terms of Performance loading the assembly in a Reflection Only context during discovery.
                // 2. Loading it in Reflection only context entails a bunch of custom logic to identify custom attributes which is over-kill for normal desktop users.
                assembly = PlatformServiceProvider.Instance.FileOperations.LoadAssembly(assemblyFileName, isReflectionOnly: true);
            }
            else
            {
                assembly = PlatformServiceProvider.Instance.FileOperations.LoadAssembly(assemblyFileName, isReflectionOnly: false);
            }

            var types = this.GetTypes(assembly, assemblyFileName, warningMessages);

            foreach (var type in types)
            {
                if (type == null)
                {
                    continue;
                }

                var testsInType = this.DiscoverTestsInType(assemblyFileName, runSettingsXml, assembly, type, warningMessages);
                tests.AddRange(testsInType);
            }

            warnings = warningMessages;
            return tests;
        }

        /// <summary>
        /// Gets the types defined in an assembly.
        /// </summary>
        /// <param name="assembly">The reflected assembly.</param>
        /// <param name="assemblyFileName">The file name of the assembly.</param>
        /// <param name="warningMessages">Contains warnings if any, that need to be passed back to the caller.</param>
        /// <returns>Gets the types defined in the provided assembly.</returns>
        internal Type[] GetTypes(Assembly assembly, string assemblyFileName, ICollection<string> warningMessages)
        {
            var types = new List<Type>();
            try
            {
                types.AddRange(assembly.DefinedTypes.Select(typeinfo => typeinfo.AsType()));
            }
            catch (ReflectionTypeLoadException ex)
            {
                PlatformServiceProvider.Instance.AdapterTraceLogger.LogWarning($"MSTestExecutor.TryGetTests: {Resource.TestAssembly_AssemblyDiscoveryFailure}", assemblyFileName, ex);
                PlatformServiceProvider.Instance.AdapterTraceLogger.LogWarning(Resource.ExceptionsThrown);

                if (ex.LoaderExceptions != null)
                {
                    // If not able to load all type, log a warning and continue with loaded types.
                    var message = string.Format(CultureInfo.CurrentCulture, Resource.TypeLoadFailed, assemblyFileName, this.GetLoadExceptionDetails(ex));

                    warningMessages?.Add(message);

                    foreach (var loaderEx in ex.LoaderExceptions)
                    {
                        PlatformServiceProvider.Instance.AdapterTraceLogger.LogWarning("{0}", loaderEx);
                    }
                }

                return ex.Types;
            }

            return types.ToArray();
        }

        /// <summary>
        /// Formats load exception as multi-line string, each line contains load error message.
        /// </summary>
        /// <param name="ex">The exception.</param>
        /// <returns>Returns loader exceptions as a multi-line string.</returns>
        internal string GetLoadExceptionDetails(ReflectionTypeLoadException ex)
        {
            Debug.Assert(ex != null, "exception should not be null.");

            var map = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase); // Exception -> null.
            var errorDetails = new StringBuilder();

            if (ex.LoaderExceptions != null)
            {
                // Loader exceptions can contain duplicates, leave only unique exceptions.
                foreach (var loaderException in ex.LoaderExceptions)
                {
                    Debug.Assert(loaderException != null, "loader exception should not be null.");
                    var line = string.Format(CultureInfo.CurrentCulture, Resource.EnumeratorLoadTypeErrorFormat, loaderException.GetType(), loaderException.Message);
                    if (!map.ContainsKey(line))
                    {
                        map.Add(line, null);
                        errorDetails.AppendLine(line);
                    }
                }
            }
            else
            {
                errorDetails.AppendLine(ex.Message);
            }

            return errorDetails.ToString();
        }

        /// <summary>
        /// Returns an instance of the <see cref="TypeEnumerator"/> class.
        /// </summary>
        /// <param name="type">The type to enumerate.</param>
        /// <param name="assemblyFileName">The reflected assembly name.</param>
        /// <returns>a TypeEnumerator instance.</returns>
        internal virtual TypeEnumerator GetTypeEnumerator(Type type, string assemblyFileName)
        {
            var typeValidator = new TypeValidator(ReflectHelper);
            var testMethodValidator = new TestMethodValidator(ReflectHelper);

            return new TypeEnumerator(type, assemblyFileName, ReflectHelper, typeValidator, testMethodValidator);
        }

        private IEnumerable<UnitTestElement> DiscoverTestsInType(string assemblyFileName, string runSettingsXml, Assembly assembly, Type type, List<string> warningMessages)
        {
            var sourceLevelParameters = PlatformServiceProvider.Instance.SettingsProvider.GetProperties(assemblyFileName);
            sourceLevelParameters = RunSettingsUtilities.GetTestRunParameters(runSettingsXml)?.ConcatWithOverwrites(sourceLevelParameters)
                ?? sourceLevelParameters
                ?? new Dictionary<string, object>();

            string typeFullName = null;
            var tests = new List<UnitTestElement>();

            try
            {
                typeFullName = type.FullName;
                var unitTestCases = this.GetTypeEnumerator(type, assemblyFileName).Enumerate(out var warningsFromTypeEnumerator);
                var typeIgnored = ReflectHelper.IsAttributeDefined(type, typeof(UTF.IgnoreAttribute), false);

                if (warningsFromTypeEnumerator != null)
                {
                    warningMessages.AddRange(warningsFromTypeEnumerator);
                }

                if (unitTestCases != null)
                {
                    foreach (var test in unitTestCases)
                    {
                        test.Ignored = typeIgnored || test.Ignored;
                        if (test.Ignored)
                        {
                            tests.Add(test);
                            continue;
                        }

                        if (this.DynamicDataAttached(sourceLevelParameters, assembly, test, tests))
                        {
                            continue;
                        }

                        tests.Add(test);
                    }
                }
            }
            catch (Exception exception)
            {
                // If we fail to discover type from a class, then don't abort the discovery
                // Move to the next type.
                string message = string.Format(CultureInfo.CurrentCulture, Resource.CouldNotInspectTypeDuringDiscovery, typeFullName, assemblyFileName, exception.Message);
                PlatformServiceProvider.Instance.AdapterTraceLogger.LogInfo($"AssemblyEnumerator: {message}");
                warningMessages.Add(message);
            }

            return tests;
        }

        private bool DynamicDataAttached(IDictionary<string, object> sourceLevelParameters, Assembly assembly, UnitTestElement test, List<UnitTestElement> tests)
        {
            if (test.TestMethod.HasManagedMethodAndTypeProperties == false)
            {
                return false;
            }

            using (var writer = new ThreadSafeStringWriter(CultureInfo.InvariantCulture))
            {
                var testMethod = test.TestMethod;
                var testContext = PlatformServiceProvider.Instance.GetTestContext(testMethod, writer, sourceLevelParameters);
                var testMethodInfo = this.typeCache.GetTestMethodInfo(testMethod, testContext, MSTestSettings.CurrentSettings.CaptureDebugTraces);
                if (testMethodInfo == null)
                {
                    return false;
                }

                return false /* DataSourceAttribute discovery is disabled for now, since we cannot serialize DataRow values.
                    || this.TryProcessDataSource(test, testMethodInfo, testContext, tests) */
                    || this.TryProcessTestDataSourceTests(test, testMethodInfo, tests);
            }
        }

        private bool TryProcessDataSource(UnitTestElement test, TestMethodInfo testMethodInfo, ITestContext testContext, List<UnitTestElement> tests)
        {
            UTF.DataSourceAttribute[] dataSourceAttributes = ReflectHelper.GetAttributes<UTF.DataSourceAttribute>(testMethodInfo.MethodInfo, false);
            if (dataSourceAttributes != null && dataSourceAttributes.Length == 1)
            {
                try
                {
                    return this.ProcessDataSourceTests(test, testMethodInfo, testContext, tests);
                }
                catch (Exception ex)
                {
                    var message = string.Format(CultureInfo.CurrentCulture, Resource.CannotEnumerateDataSourceAttribute, test.TestMethod.ManagedTypeName, test.TestMethod.ManagedMethodName, ex);
                    PlatformServiceProvider.Instance.AdapterTraceLogger.LogInfo($"DynamicDataEnumarator: {message}");
                    return false;
                }
            }
            else if (dataSourceAttributes != null && dataSourceAttributes.Length > 1)
            {
                var message = string.Format(CultureInfo.CurrentCulture, Resource.CannotEnumerateDataSourceAttribute_MoreThenOneDefined, test.TestMethod.ManagedTypeName, test.TestMethod.ManagedMethodName, dataSourceAttributes.Length);
                PlatformServiceProvider.Instance.AdapterTraceLogger.LogInfo($"DynamicDataEnumarator: {message}");
                throw new InvalidOperationException(message);
            }

            return false;
        }

        private bool ProcessDataSourceTests(UnitTestElement test, TestMethodInfo testMethodInfo, ITestContext testContext, List<UnitTestElement> tests)
        {
            var dataRows = PlatformServiceProvider.Instance.TestDataSource.GetData(testMethodInfo, testContext);
            if (dataRows == null || !dataRows.Any())
            {
                return false;
            }

            try
            {
                int rowIndex = 0;

                foreach (var dataRow in dataRows)
                {
                    // TODO: Test serialization
                    rowIndex++;

                    var displayName = string.Format(CultureInfo.CurrentCulture, Resource.DataDrivenResultDisplayName, test.DisplayName, rowIndex);
                    var discoveredTest = test.Clone();
                    discoveredTest.DisplayName = displayName;
                    discoveredTest.TestMethod.DataType = DynamicDataType.DataSourceAttribute;
                    discoveredTest.TestMethod.Data = new[] { (object)rowIndex };
                    tests.Add(discoveredTest);
                }

                return true;
            }
            catch
            {
                testContext.SetDataConnection(null);
                testContext.SetDataRow(null);
            }

            return false;
        }

        private bool TryProcessTestDataSourceTests(UnitTestElement test, TestMethodInfo testMethodInfo, List<UnitTestElement> tests)
        {
            var methodInfo = testMethodInfo.MethodInfo;

            UTF.ITestDataSource[] testDataSources = ReflectHelper.GetAttributes<Attribute>(methodInfo, false)?.Where(a => a is UTF.ITestDataSource).OfType<UTF.ITestDataSource>().ToArray();
            try
            {
                return this.ProcessTestDataSourceTests(test, (MethodInfo)methodInfo, testDataSources, tests);
            }
            catch (Exception ex)
            {
                var message = string.Format(CultureInfo.CurrentCulture, Resource.CannotEnumerateIDataSourceAttribute, test.TestMethod.ManagedTypeName, test.TestMethod.ManagedMethodName, ex);
                PlatformServiceProvider.Instance.AdapterTraceLogger.LogInfo($"DynamicDataEnumarator: {message}");
                return false;
            }
        }

        private bool ProcessTestDataSourceTests(UnitTestElement test, MethodInfo methodInfo, UTF.ITestDataSource[] testDataSources, List<UnitTestElement> tests)
        {
            if (testDataSources == null || testDataSources.Length == 0)
            {
                return false;
            }

            foreach (var dataSource in testDataSources)
            {
                var data = dataSource.GetData(methodInfo);

                foreach (var d in data)
                {
                    var discoveredTest = test.Clone();
                    discoveredTest.DisplayName = dataSource.GetDisplayName(methodInfo, d);

                    discoveredTest.TestMethod.DataType = DynamicDataType.ITestDataSource;
                    discoveredTest.TestMethod.Data = d;

                    tests.Add(discoveredTest);
                }
            }

            return true;
        }
    }
}
