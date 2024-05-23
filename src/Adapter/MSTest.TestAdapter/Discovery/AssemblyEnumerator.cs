// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Globalization;
using System.Reflection;
using System.Runtime.Serialization;
using System.Security;
using System.Text;

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Helpers;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using FrameworkITestDataSource = Microsoft.VisualStudio.TestTools.UnitTesting.ITestDataSource;

namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Discovery;

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
    /// Type cache.
    /// </summary>
    private readonly TypeCache _typeCache = new(ReflectHelper);

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
        // This would just be resetting the settings to itself in non desktop workflows.
        MSTestSettings.PopulateSettings(settings);
    }

    /// <summary>
    /// Gets or sets the run settings to use for current discovery session.
    /// </summary>
    public string? RunSettingsXml { get; set; }

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
    /// Enumerates through all types in the assembly in search of valid test methods.
    /// </summary>
    /// <param name="assemblyFileName">The assembly file name.</param>
    /// <param name="warnings">Contains warnings if any, that need to be passed back to the caller.</param>
    /// <returns>A collection of Test Elements.</returns>
    internal ICollection<UnitTestElement> EnumerateAssembly(string assemblyFileName, out ICollection<string> warnings)
    {
        DebugEx.Assert(!StringEx.IsNullOrWhiteSpace(assemblyFileName), "Invalid assembly file name.");

        var warningMessages = new List<string>();
        var tests = new List<UnitTestElement>();

        Assembly assembly = PlatformServiceProvider.Instance.FileOperations.LoadAssembly(assemblyFileName, isReflectionOnly: false);

        IReadOnlyList<Type> types = GetTypes(assembly, assemblyFileName, warningMessages);
        bool discoverInternals = assembly.GetCustomAttribute<DiscoverInternalsAttribute>() != null;
        TestIdGenerationStrategy testIdGenerationStrategy = assembly.GetCustomAttribute<TestIdGenerationStrategyAttribute>()?.Strategy
            ?? TestIdGenerationStrategy.FullyQualified;

        TestDataSourceDiscoveryOption testDataSourceDiscovery = assembly.GetCustomAttribute<TestDataSourceDiscoveryAttribute>()?.DiscoveryOption
#pragma warning disable CS0618 // Type or member is obsolete

            // When using legacy strategy, there is no point in trying to "read" data during discovery
            // as the ID generator will ignore it.
            ?? (testIdGenerationStrategy == TestIdGenerationStrategy.Legacy
                ? TestDataSourceDiscoveryOption.DuringExecution
                : TestDataSourceDiscoveryOption.DuringDiscovery);
#pragma warning restore CS0618 // Type or member is obsolete
        foreach (Type type in types)
        {
            if (type == null)
            {
                continue;
            }

            List<UnitTestElement> testsInType = DiscoverTestsInType(assemblyFileName, RunSettingsXml, type, warningMessages, discoverInternals,
                testDataSourceDiscovery, testIdGenerationStrategy);
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
    internal static IReadOnlyList<Type> GetTypes(Assembly assembly, string assemblyFileName, ICollection<string>? warningMessages)
    {
        try
        {
            return assembly.DefinedTypes.Select(typeInfo => typeInfo.AsType()).ToList();
        }
        catch (ReflectionTypeLoadException ex)
        {
            PlatformServiceProvider.Instance.AdapterTraceLogger.LogWarning($"MSTestExecutor.TryGetTests: {Resource.TestAssembly_AssemblyDiscoveryFailure}", assemblyFileName, ex);
            PlatformServiceProvider.Instance.AdapterTraceLogger.LogWarning(Resource.ExceptionsThrown);

            if (ex.LoaderExceptions != null)
            {
                // If not able to load all type, log a warning and continue with loaded types.
                string message = string.Format(CultureInfo.CurrentCulture, Resource.TypeLoadFailed, assemblyFileName, GetLoadExceptionDetails(ex));

                warningMessages?.Add(message);

                foreach (Exception? loaderEx in ex.LoaderExceptions)
                {
                    PlatformServiceProvider.Instance.AdapterTraceLogger.LogWarning("{0}", loaderEx);
                }
            }

            return ex.Types!;
        }
    }

    /// <summary>
    /// Formats load exception as multi-line string, each line contains load error message.
    /// </summary>
    /// <param name="ex">The exception.</param>
    /// <returns>Returns loader exceptions as a multi-line string.</returns>
    internal static string GetLoadExceptionDetails(ReflectionTypeLoadException ex)
    {
        DebugEx.Assert(ex != null, "exception should not be null.");

        var map = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase); // Exception -> null.
        var errorDetails = new StringBuilder();

        if (ex.LoaderExceptions?.Length > 0)
        {
            // Loader exceptions can contain duplicates, leave only unique exceptions.
            foreach (Exception? loaderException in ex.LoaderExceptions)
            {
                DebugEx.Assert(loaderException != null, "loader exception should not be null.");
                string line = string.Format(CultureInfo.CurrentCulture, Resource.EnumeratorLoadTypeErrorFormat, loaderException.GetType(), loaderException.Message);
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
    /// <param name="testIdGenerationStrategy"><see cref="TestIdGenerationStrategy"/> to use when generating TestId.</param>
    /// <returns>a TypeEnumerator instance.</returns>
    internal virtual TypeEnumerator GetTypeEnumerator(Type type, string assemblyFileName, bool discoverInternals, TestIdGenerationStrategy testIdGenerationStrategy)
    {
        var typeValidator = new TypeValidator(ReflectHelper, discoverInternals);
        var testMethodValidator = new TestMethodValidator(ReflectHelper, discoverInternals);

        return new TypeEnumerator(type, assemblyFileName, ReflectHelper, typeValidator, testMethodValidator, testIdGenerationStrategy);
    }

    private List<UnitTestElement> DiscoverTestsInType(string assemblyFileName, string? runSettingsXml, Type type,
        List<string> warningMessages, bool discoverInternals, TestDataSourceDiscoveryOption discoveryOption,
        TestIdGenerationStrategy testIdGenerationStrategy)
    {
        IDictionary<string, object> tempSourceLevelParameters = PlatformServiceProvider.Instance.SettingsProvider.GetProperties(assemblyFileName);
        tempSourceLevelParameters = RunSettingsUtilities.GetTestRunParameters(runSettingsXml)?.ConcatWithOverwrites(tempSourceLevelParameters)
            ?? tempSourceLevelParameters
            ?? new Dictionary<string, object>();
        var sourceLevelParameters = tempSourceLevelParameters.ToDictionary(x => x.Key, x => (object?)x.Value);

        string? typeFullName = null;
        var tests = new List<UnitTestElement>();

        try
        {
            typeFullName = type.FullName;
            TypeEnumerator testTypeEnumerator = GetTypeEnumerator(type, assemblyFileName, discoverInternals, testIdGenerationStrategy);
            ICollection<UnitTestElement>? unitTestCases = testTypeEnumerator.Enumerate(out ICollection<string>? warningsFromTypeEnumerator);
            bool typeIgnored = ReflectHelper.IsAttributeDefined<IgnoreAttribute>(type, false);

            if (warningsFromTypeEnumerator != null)
            {
                warningMessages.AddRange(warningsFromTypeEnumerator);
            }

            if (unitTestCases != null)
            {
                foreach (UnitTestElement test in unitTestCases)
                {
                    if (discoveryOption == TestDataSourceDiscoveryOption.DuringDiscovery)
                    {
                        if (DynamicDataAttached(sourceLevelParameters, test, tests))
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

    private bool DynamicDataAttached(IDictionary<string, object?> sourceLevelParameters, UnitTestElement test, List<UnitTestElement> tests)
    {
        // It should always be `true`, but if any part of the chain is obsolete; it might not contain those.
        // Since we depend on those properties, if they don't exist, we bail out early.
        if (!test.TestMethod.HasManagedMethodAndTypeProperties)
        {
            return false;
        }

        // NOTE: From this place we don't have any path that would let the user write a message on the TestContext and we don't do
        // anything with what would be printed anyway so we can simply use a simple StringWriter.
        using var writer = new StringWriter();
        TestMethod testMethod = test.TestMethod;
        MSTestAdapter.PlatformServices.Interface.ITestContext testContext = PlatformServiceProvider.Instance.GetTestContext(testMethod, writer, sourceLevelParameters);
        TestMethodInfo? testMethodInfo = _typeCache.GetTestMethodInfo(testMethod, testContext, MSTestSettings.CurrentSettings.CaptureDebugTraces);
        return testMethodInfo != null && TryProcessTestDataSourceTests(test, testMethodInfo, tests);
    }

    private static bool TryProcessTestDataSourceTests(UnitTestElement test, TestMethodInfo testMethodInfo, List<UnitTestElement> tests)
    {
        MethodInfo methodInfo = testMethodInfo.MethodInfo;
        IEnumerable<FrameworkITestDataSource>? testDataSources = ReflectHelper.GetAttributes<Attribute>(methodInfo, false)?.OfType<FrameworkITestDataSource>();
        if (testDataSources == null || !testDataSources.Any())
        {
            return false;
        }

        try
        {
            return ProcessTestDataSourceTests(test, methodInfo, testDataSources, tests);
        }
        catch (Exception ex)
        {
            string message = string.Format(CultureInfo.CurrentCulture, Resource.CannotEnumerateIDataSourceAttribute, test.TestMethod.ManagedTypeName, test.TestMethod.ManagedMethodName, ex);
            PlatformServiceProvider.Instance.AdapterTraceLogger.LogInfo($"DynamicDataEnumerator: {message}");
            return false;
        }
    }

    private static bool ProcessTestDataSourceTests(UnitTestElement test, MethodInfo methodInfo, IEnumerable<FrameworkITestDataSource> testDataSources,
        List<UnitTestElement> tests)
    {
        foreach (FrameworkITestDataSource dataSource in testDataSources)
        {
            IEnumerable<object?[]>? data = null;

            // This code is to discover tests. To run the tests code is in TestMethodRunner.ExecuteDataSourceBasedTests.
            // Any change made here should be reflected in TestMethodRunner.ExecuteDataSourceBasedTests as well.
            try
            {
                data = dataSource.GetData(methodInfo);

                if (!data.Any())
                {
                    throw new ArgumentException(string.Format(CultureInfo.CurrentCulture, FrameworkMessages.DynamicDataIEnumerableEmpty, "GetData", dataSource.GetType().Name));
                }
            }
            catch (Exception ex) when (ex is ArgumentException && MSTestSettings.CurrentSettings.ConsiderEmptyDataSourceAsInconclusive)
            {
                UnitTestElement discoveredTest = test.Clone();
                discoveredTest.DisplayName = dataSource.GetDisplayName(methodInfo, null) ?? discoveredTest.DisplayName;
                tests.Add(discoveredTest);
                continue;
            }

            var testDisplayNameFirstSeen = new Dictionary<string, int>();
            var discoveredTests = new List<UnitTestElement>();
            int index = 0;

            foreach (object?[] d in data)
            {
                UnitTestElement discoveredTest = test.Clone();
                discoveredTest.DisplayName = dataSource.GetDisplayName(methodInfo, d) ?? discoveredTest.DisplayName;

                // If strategy is DisplayName and we have a duplicate test name don't expand the test, bail out.
#pragma warning disable CS0618 // Type or member is obsolete
                if (test.TestMethod.TestIdGenerationStrategy == TestIdGenerationStrategy.DisplayName
                    && testDisplayNameFirstSeen.TryGetValue(discoveredTest.DisplayName!, out int firstIndexSeen))
                {
                    string warning = string.Format(CultureInfo.CurrentCulture, Resource.CannotExpandIDataSourceAttribute_DuplicateDisplayName, firstIndexSeen, index, discoveredTest.DisplayName);
                    warning = string.Format(CultureInfo.CurrentCulture, Resource.CannotExpandIDataSourceAttribute, test.TestMethod.ManagedTypeName, test.TestMethod.ManagedMethodName, warning);
                    PlatformServiceProvider.Instance.AdapterTraceLogger.LogWarning($"DynamicDataEnumerator: {warning}");

                    // Duplicated display name so bail out. Caller will handle adding the original test.
                    return false;
                }
#pragma warning restore CS0618 // Type or member is obsolete

                try
                {
                    discoveredTest.TestMethod.SerializedData = DataSerializationHelper.Serialize(d);
                    discoveredTest.TestMethod.DataType = DynamicDataType.ITestDataSource;
                }
                catch (SerializationException ex)
                {
                    string warning = string.Format(CultureInfo.CurrentCulture, Resource.CannotExpandIDataSourceAttribute_CannotSerialize, index, discoveredTest.DisplayName);
                    warning += Environment.NewLine;
                    warning += ex.ToString();
                    warning = string.Format(CultureInfo.CurrentCulture, Resource.CannotExpandIDataSourceAttribute, test.TestMethod.ManagedTypeName, test.TestMethod.ManagedMethodName, warning);
                    PlatformServiceProvider.Instance.AdapterTraceLogger.LogWarning($"DynamicDataEnumerator: {warning}");

                    // Serialization failed for the type, bail out. Caller will handle adding the original test.
                    return false;
                }

                discoveredTests.Add(discoveredTest);
                testDisplayNameFirstSeen[discoveredTest.DisplayName!] = index++;
            }

            tests.AddRange(discoveredTests);
        }

        return true;
    }
}
