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
    using System.Runtime.Serialization;
    using System.Security;
    using System.Text;

    using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution;
    using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Helpers;
    using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;
    using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;
    using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface;

    using UTF = Microsoft.VisualStudio.TestTools.UnitTesting;

    /// <summary>
    /// Enumerates through all types in the assembly in search of valid test methods.
    /// </summary>
    internal class AssemblyEnumerator : MarshalByRefObject
    {
        /// <summary>
        /// Helper for reflection API's.
        /// </summary>
        private static readonly ReflectHelper ReflectHelper = ReflectHelper.Instance;

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
        /// Gets or sets the run settings to use for current discovery session.
        /// </summary>
        public string RunSettingsXml { get; set; }

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

            var runSettingsXml = this.RunSettingsXml;
            var warningMessages = new List<string>();
            var tests = new List<UnitTestElement>();

            var assembly = PlatformServiceProvider.Instance.FileOperations.LoadAssembly(assemblyFileName, isReflectionOnly: false);

            var types = this.GetTypes(assembly, assemblyFileName, warningMessages);
            var discoverInternals = assembly.GetCustomAttribute<UTF.DiscoverInternalsAttribute>() != null;
            var testDataSourceDiscovery = assembly.GetCustomAttribute<UTF.TestDataSourceDiscoveryAttribute>()?.DiscoveryOption ?? UTF.TestDataSourceDiscoveryOption.DuringDiscovery;

            foreach (var type in types)
            {
                if (type == null)
                {
                    continue;
                }

                var testsInType = this.DiscoverTestsInType(assemblyFileName, runSettingsXml, assembly, type, warningMessages, discoverInternals, testDataSourceDiscovery);
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
        /// <param name="discoverInternals">True to discover test classes which are declared internal in
        /// addition to test classes which are declared public.</param>
        /// <returns>a TypeEnumerator instance.</returns>
        internal virtual TypeEnumerator GetTypeEnumerator(Type type, string assemblyFileName, bool discoverInternals = false)
        {
            var typeValidator = new TypeValidator(ReflectHelper, discoverInternals);
            var testMethodValidator = new TestMethodValidator(ReflectHelper, discoverInternals);

            return new TypeEnumerator(type, assemblyFileName, ReflectHelper, typeValidator, testMethodValidator);
        }

        private IEnumerable<UnitTestElement> DiscoverTestsInType(string assemblyFileName, string runSettingsXml, Assembly assembly, Type type, List<string> warningMessages, bool discoverInternals = false, UTF.TestDataSourceDiscoveryOption discoveryOption = UTF.TestDataSourceDiscoveryOption.DuringExecution)
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
                var unitTestCases = this.GetTypeEnumerator(type, assemblyFileName, discoverInternals).Enumerate(out var warningsFromTypeEnumerator);
                var typeIgnored = ReflectHelper.IsAttributeDefined(type, typeof(UTF.IgnoreAttribute), false);

                if (warningsFromTypeEnumerator != null)
                {
                    warningMessages.AddRange(warningsFromTypeEnumerator);
                }

                if (unitTestCases != null)
                {
                    foreach (var test in unitTestCases)
                    {
                        if (discoveryOption == UTF.TestDataSourceDiscoveryOption.DuringDiscovery)
                        {
                            if (this.DynamicDataAttached(sourceLevelParameters, assembly, test, tests))
                            {
                                continue;
                            }
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
            // It should always be `true`, but if any part of the chain is obsolete; it might not contain those.
            // Since we depend on those properties, if they don't exist, we bail out early.
            if (!test.TestMethod.HasManagedMethodAndTypeProperties)
            {
                return false;
            }

            using (var writer = new ThreadSafeStringWriter(CultureInfo.InvariantCulture, "all"))
            {
                var testMethod = test.TestMethod;
                var testContext = PlatformServiceProvider.Instance.GetTestContext(testMethod, writer, sourceLevelParameters);
                var testMethodInfo = this.typeCache.GetTestMethodInfo(testMethod, testContext, MSTestSettings.CurrentSettings.CaptureDebugTraces);
                if (testMethodInfo == null)
                {
                    return false;
                }

                return /* DataSourceAttribute discovery is disabled for now, since we cannot serialize DataRow values.
                       this.TryProcessDataSource(test, testMethodInfo, testContext, tests) || */
                       this.TryProcessTestDataSourceTests(test, testMethodInfo, tests);
            }
        }

        private bool TryProcessDataSource(UnitTestElement test, TestMethodInfo testMethodInfo, ITestContext testContext, List<UnitTestElement> tests)
        {
            var dataSourceAttributes = ReflectHelper.GetAttributes<UTF.DataSourceAttribute>(testMethodInfo.MethodInfo, false);
            if (dataSourceAttributes == null)
            {
                return false;
            }

            if (dataSourceAttributes.Length > 1)
            {
                var message = string.Format(CultureInfo.CurrentCulture, Resource.CannotEnumerateDataSourceAttribute_MoreThenOneDefined, test.TestMethod.ManagedTypeName, test.TestMethod.ManagedMethodName, dataSourceAttributes.Length);
                PlatformServiceProvider.Instance.AdapterTraceLogger.LogInfo($"DynamicDataEnumarator: {message}");
                throw new InvalidOperationException(message);
            }

            // dataSourceAttributes.Length == 1
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
                    discoveredTest.TestMethod.SerializedData = DataSerializationHelper.Serialize(new[] { (object)rowIndex });
                    tests.Add(discoveredTest);
                }

                return true;
            }
            finally
            {
                testContext.SetDataConnection(null);
                testContext.SetDataRow(null);
            }
        }

        private bool TryProcessTestDataSourceTests(UnitTestElement test, TestMethodInfo testMethodInfo, List<UnitTestElement> tests)
        {
            var methodInfo = testMethodInfo.MethodInfo;
            var testDataSources = ReflectHelper.GetAttributes<Attribute>(methodInfo, false)?.Where(a => a is UTF.ITestDataSource).OfType<UTF.ITestDataSource>().ToArray();
            if (testDataSources == null || testDataSources.Length == 0)
            {
                return false;
            }

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
            foreach (var dataSource in testDataSources)
            {
                var data = dataSource.GetData(methodInfo);
                var discoveredTests = new Dictionary<string, UnitTestElement>();
                var discoveredIndex = new Dictionary<string, int>();
                var serializationFailed = false;
                var index = 0;

                foreach (var d in data)
                {
                    var discoveredTest = test.Clone();
                    discoveredTest.DisplayName = dataSource.GetDisplayName(methodInfo, d) ?? discoveredTest.DisplayName;

                    // if we have a duplicate test name don't expand the test, bail out.
                    if (discoveredTests.ContainsKey(discoveredTest.DisplayName))
                    {
                        var firstSeen = discoveredIndex[discoveredTest.DisplayName];
                        var warning = string.Format(CultureInfo.CurrentCulture, Resource.CannotExpandIDataSourceAttribute_DuplicateDisplayName, firstSeen, index, discoveredTest.DisplayName);
                        warning = string.Format(CultureInfo.CurrentUICulture, Resource.CannotExpandIDataSourceAttribute, test.TestMethod.ManagedTypeName, test.TestMethod.ManagedMethodName, warning);
                        PlatformServiceProvider.Instance.AdapterTraceLogger.LogWarning($"DynamicDataEnumarator: {warning}");

                        serializationFailed = true;
                        break;
                    }

                    try
                    {
                        discoveredTest.TestMethod.SerializedData = DataSerializationHelper.Serialize(d);
                        discoveredTest.TestMethod.DataType = DynamicDataType.ITestDataSource;
                    }
                    catch (SerializationException)
                    {
                        var firstSeen = discoveredIndex[discoveredTest.DisplayName];
                        var warning = string.Format(CultureInfo.CurrentCulture, Resource.CannotExpandIDataSourceAttribute_CannotSerialize, index, discoveredTest.DisplayName);
                        warning = string.Format(CultureInfo.CurrentUICulture, Resource.CannotExpandIDataSourceAttribute, test.TestMethod.ManagedTypeName, test.TestMethod.ManagedMethodName, warning);
                        PlatformServiceProvider.Instance.AdapterTraceLogger.LogWarning($"DynamicDataEnumarator: {warning}");

                        serializationFailed = true;
                        break;
                    }

                    discoveredTests[discoveredTest.DisplayName] = discoveredTest;
                    discoveredIndex[discoveredTest.DisplayName] = index++;
                }

                // Serialization failed for the type, bail out.
                if (serializationFailed)
                {
                    tests.Add(test);
                    break;
                }

                tests.AddRange(discoveredTests.Values);
            }

            return true;
        }
    }
}
