// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Runtime.Serialization;
using System.Security;

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Helpers;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.VisualStudio.TestTools.UnitTesting.Internal;

namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Discovery;

/// <summary>
/// Enumerates through all types in the assembly in search of valid test methods.
/// </summary>
[SuppressMessage("Performance", "CA1852: Seal internal types", Justification = "Overrides required for testability")]
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
    public AssemblyEnumerator(MSTestSettings settings) =>
        // Populate the settings into the domain(Desktop workflow) performing discovery.
        // This would just be resetting the settings to itself in non desktop workflows.
        MSTestSettings.PopulateSettings(settings);

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
    /// <returns>A collection of Test Elements.</returns>
    internal AssemblyEnumerationResult EnumerateAssembly(string assemblyFileName)
    {
        List<string> warnings = [];
        DebugEx.Assert(!StringEx.IsNullOrWhiteSpace(assemblyFileName), "Invalid assembly file name.");
        var tests = new List<UnitTestElement>();
        // Contains list of assembly/class names for which we have already added fixture tests.
        var fixturesTests = new HashSet<string>();

        Assembly assembly = PlatformServiceProvider.Instance.FileOperations.LoadAssembly(assemblyFileName);

        Type[] types = GetTypes(assembly);
        bool discoverInternals = ReflectHelper.GetDiscoverInternalsAttribute(assembly) != null;

        TestDataSourceUnfoldingStrategy dataSourcesUnfoldingStrategy = ReflectHelper.GetTestDataSourceOptions(assembly)?.UnfoldingStrategy switch
        {
            // When strategy is auto we want to unfold
            TestDataSourceUnfoldingStrategy.Auto => TestDataSourceUnfoldingStrategy.Unfold,
            // When strategy is set, let's use it
            { } value => value,
            // When the attribute is not set, let's look at the legacy attribute
            null => ReflectHelper.GetTestDataSourceDiscoveryOption(assembly) switch
            {
                TestDataSourceDiscoveryOption.DuringExecution => TestDataSourceUnfoldingStrategy.Fold,
                _ => TestDataSourceUnfoldingStrategy.Unfold,
            },
        };

        foreach (Type type in types)
        {
            List<UnitTestElement> testsInType = DiscoverTestsInType(assemblyFileName, type, warnings, discoverInternals,
                dataSourcesUnfoldingStrategy, fixturesTests);
            tests.AddRange(testsInType);
        }

        return new AssemblyEnumerationResult(tests, warnings);
    }

    /// <summary>
    /// Gets the types defined in an assembly.
    /// </summary>
    /// <param name="assembly">The reflected assembly.</param>
    /// <returns>Gets the types defined in the provided assembly.</returns>
    internal static Type[] GetTypes(Assembly assembly)
    {
        try
        {
            return PlatformServiceProvider.Instance.ReflectionOperations.GetDefinedTypes(assembly);
        }
        catch (ReflectionTypeLoadException ex)
        {
            if (ex.LoaderExceptions != null)
            {
                if (ex.LoaderExceptions.Length == 1 && ex.LoaderExceptions[0] is { } singleLoaderException)
                {
                    // This exception might be more clear than the ReflectionTypeLoadException, so we throw it.
                    throw singleLoaderException;
                }

                // If we have multiple loader exceptions, we log them all as errors, and then throw the original exception.
                foreach (Exception? loaderEx in ex.LoaderExceptions)
                {
                    PlatformServiceProvider.Instance.AdapterTraceLogger.LogError("{0}", loaderEx);
                }
            }

            throw;
        }
    }

    /// <summary>
    /// Returns an instance of the <see cref="TypeEnumerator"/> class.
    /// </summary>
    /// <param name="type">The type to enumerate.</param>
    /// <param name="assemblyFileName">The reflected assembly name.</param>
    /// <param name="discoverInternals">True to discover test classes which are declared internal in
    /// addition to test classes which are declared public.</param>
    /// <returns>a TypeEnumerator instance.</returns>
    internal virtual TypeEnumerator GetTypeEnumerator(Type type, string assemblyFileName, bool discoverInternals)
    {
        var typeValidator = new TypeValidator(ReflectHelper, discoverInternals);
        var testMethodValidator = new TestMethodValidator(ReflectHelper, discoverInternals);

        return new TypeEnumerator(type, assemblyFileName, ReflectHelper, typeValidator, testMethodValidator);
    }

    private List<UnitTestElement> DiscoverTestsInType(
        string assemblyFileName,
        Type type,
        List<string> warningMessages,
        bool discoverInternals,
        TestDataSourceUnfoldingStrategy dataSourcesUnfoldingStrategy,
        HashSet<string> fixturesTests)
    {
        string? typeFullName = null;
        var tests = new List<UnitTestElement>();

        try
        {
            typeFullName = type.FullName;
            TypeEnumerator testTypeEnumerator = GetTypeEnumerator(type, assemblyFileName, discoverInternals);
            List<UnitTestElement>? unitTestCases = testTypeEnumerator.Enumerate(warningMessages);

            if (unitTestCases != null)
            {
                foreach (UnitTestElement test in unitTestCases)
                {
                    if (_typeCache.GetTestMethodInfoForDiscovery(test.TestMethod) is { } testMethodInfo)
                    {
                        // Add fixture tests like AssemblyInitialize, AssemblyCleanup, ClassInitialize, ClassCleanup.
                        if (MSTestSettings.CurrentSettings.ConsiderFixturesAsSpecialTests)
                        {
                            AddFixtureTests(testMethodInfo, tests, fixturesTests);
                        }

                        if (TryUnfoldITestDataSources(test, testMethodInfo, dataSourcesUnfoldingStrategy, tests))
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

    private static void AddFixtureTests(DiscoveryTestMethodInfo testMethodInfo, List<UnitTestElement> tests, HashSet<string> fixtureTests)
    {
        string assemblyName = testMethodInfo.Parent.Parent.Assembly.GetName().Name!;
        string assemblyLocation = testMethodInfo.Parent.Parent.Assembly.Location;
        string classFullName = testMethodInfo.Parent.ClassType.FullName!;

        // Check if fixtures for this assembly has already been added.
        if (!fixtureTests.Contains(assemblyLocation))
        {
            _ = fixtureTests.Add(assemblyLocation);

            // Add AssemblyInitialize and AssemblyCleanup fixture tests if they exist.
            if (testMethodInfo.Parent.Parent.AssemblyInitializeMethod is not null)
            {
                tests.Add(GetAssemblyFixtureTest(testMethodInfo.Parent.Parent.AssemblyInitializeMethod, assemblyName,
                    classFullName, assemblyLocation, EngineConstants.AssemblyInitializeFixtureTrait));
            }

            if (testMethodInfo.Parent.Parent.AssemblyCleanupMethod is not null)
            {
                tests.Add(GetAssemblyFixtureTest(testMethodInfo.Parent.Parent.AssemblyCleanupMethod, assemblyName,
                    classFullName, assemblyLocation, EngineConstants.AssemblyCleanupFixtureTrait));
            }
        }

        // Check if fixtures for this class has already been added.
        if (!fixtureTests.Contains(assemblyLocation + classFullName))
        {
            _ = fixtureTests.Add(assemblyLocation + classFullName);

            // Add ClassInitialize and ClassCleanup fixture tests if they exist.
            if (testMethodInfo.Parent.ClassInitializeMethod is not null)
            {
                tests.Add(GetClassFixtureTest(testMethodInfo.Parent.ClassInitializeMethod, classFullName,
                    assemblyLocation, EngineConstants.ClassInitializeFixtureTrait));
            }

            if (testMethodInfo.Parent.ClassCleanupMethod is not null)
            {
                tests.Add(GetClassFixtureTest(testMethodInfo.Parent.ClassCleanupMethod, classFullName,
                    assemblyLocation, EngineConstants.ClassCleanupFixtureTrait));
            }
        }

        static UnitTestElement GetAssemblyFixtureTest(MethodInfo methodInfo, string assemblyName, string classFullName,
            string assemblyLocation, string fixtureType)
        {
            string methodName = GetMethodName(methodInfo);
            string[] hierarchy = [null!, assemblyName, EngineConstants.AssemblyFixturesHierarchyClassName, methodName];
            return GetFixtureTest(classFullName, assemblyLocation, fixtureType, methodName, hierarchy, methodInfo);
        }

        static UnitTestElement GetClassFixtureTest(MethodInfo methodInfo, string classFullName,
            string assemblyLocation, string fixtureType)
        {
            string methodName = GetMethodName(methodInfo);
            string[] hierarchy = [null!, classFullName, methodName];
            return GetFixtureTest(classFullName, assemblyLocation, fixtureType, methodName, hierarchy, methodInfo);
        }

        static string GetMethodName(MethodInfo methodInfo)
        {
            ParameterInfo[] args = methodInfo.GetParameters();
            return args.Length > 0
                ? $"{methodInfo.Name}({string.Join(',', args.Select(a => a.ParameterType.FullName))})"
                : methodInfo.Name;
        }

        static UnitTestElement GetFixtureTest(string classFullName, string assemblyLocation, string fixtureType, string methodName, string[] hierarchy, MethodInfo methodInfo)
        {
            string displayName = $"[{fixtureType}] {methodName}";
            var method = new TestMethod(classFullName, methodName, hierarchy, methodName, classFullName, assemblyLocation, displayName, null)
            {
                MethodInfo = methodInfo,
            };
            return new UnitTestElement(method)
            {
                Traits = [new Trait(EngineConstants.FixturesTestTrait, fixtureType)],
            };
        }
    }

    private static bool TryUnfoldITestDataSources(UnitTestElement test, DiscoveryTestMethodInfo testMethodInfo, TestDataSourceUnfoldingStrategy dataSourcesUnfoldingStrategy, List<UnitTestElement> tests)
    {
        // It should always be `true`, but if any part of the chain is obsolete; it might not contain those.
        // Since we depend on those properties, if they don't exist, we bail out early.
        if (!test.TestMethod.HasManagedMethodAndTypeProperties)
        {
            return false;
        }

        // If the global strategy is to fold and local uses Auto then return false
        if (dataSourcesUnfoldingStrategy == TestDataSourceUnfoldingStrategy.Fold
            && test.UnfoldingStrategy == TestDataSourceUnfoldingStrategy.Auto)
        {
            return false;
        }

        // If the data source specifies the unfolding strategy as fold then return false
        if (test.UnfoldingStrategy == TestDataSourceUnfoldingStrategy.Fold)
        {
            return false;
        }

        // We don't have a special method to filter attributes that are not derived from Attribute, so we take all
        // attributes and filter them. We don't have to care if there is one, because this method is only entered when
        // there is at least one (we determine this in TypeEnumerator.GetTestFromMethod.
        IEnumerable<ITestDataSource> testDataSources = ReflectHelper.Instance.GetAttributes<Attribute>(testMethodInfo.MethodInfo).OfType<ITestDataSource>();

        // We need to use a temporary list to avoid adding tests to the main list if we fail to expand any data source.
        List<UnitTestElement> tempListOfTests = [];

        try
        {
            bool isDataDriven = false;
            int globalTestCaseIndex = 0;
            foreach (ITestDataSource dataSource in testDataSources)
            {
                isDataDriven = true;
                if (!TryUnfoldITestDataSource(dataSource, test, new(testMethodInfo.MethodInfo, test.TestMethod.DisplayName), tempListOfTests, ref globalTestCaseIndex))
                {
                    // TODO: Improve multi-source design!
                    // Ideally we would want to consider each data source separately but when one source cannot be expanded,
                    // we will run all sources from the given method so we need to bail-out "globally".
                    return false;
                }
            }

            if (tempListOfTests.Count > 0)
            {
                tests.AddRange(tempListOfTests);
            }

            return isDataDriven;
        }
        catch (Exception ex)
        {
            string message = string.Format(CultureInfo.CurrentCulture, Resource.CannotEnumerateIDataSourceAttribute, test.TestMethod.ManagedTypeName, test.TestMethod.ManagedMethodName, ex);
            PlatformServiceProvider.Instance.AdapterTraceLogger.LogInfo($"DynamicDataEnumerator: {message}");

            if (tempListOfTests.Count > 0)
            {
                tests.AddRange(tempListOfTests);
            }

            return false;
        }
    }

    private static bool TryUnfoldITestDataSource(ITestDataSource dataSource, UnitTestElement test, ReflectionTestMethodInfo methodInfo, List<UnitTestElement> tests, ref int globalTestCaseIndex)
    {
        // Otherwise, unfold the data source and verify it can be serialized.
        IEnumerable<object?[]>? data;

        // This code is to discover tests. To run the tests code is in TestMethodRunner.ExecuteDataSourceBasedTests.
        // Any change made here should be reflected in TestMethodRunner.ExecuteDataSourceBasedTests as well.
        data = dataSource.GetData(methodInfo);
        string? testDataSourceIgnoreMessage = (dataSource as ITestDataSourceIgnoreCapability)?.IgnoreMessage;

        if (!data.Any())
        {
            if (!MSTestSettings.CurrentSettings.ConsiderEmptyDataSourceAsInconclusive)
            {
                throw dataSource.GetExceptionForEmptyDataSource(methodInfo);
            }

            UnitTestElement discoveredTest = test.Clone();
            // Make the test not data driven, because it had no data.
            discoveredTest.TestMethod.DataType = DynamicDataType.None;
            discoveredTest.TestMethod.TestDataSourceIgnoreMessage = testDataSourceIgnoreMessage;
            discoveredTest.TestMethod.DisplayName = dataSource.GetDisplayName(methodInfo, null) ?? discoveredTest.TestMethod.DisplayName;
            tests.Add(discoveredTest);

            return true;
        }

        var discoveredTests = new List<UnitTestElement>();

        foreach (object?[] dataOrTestDataRow in data)
        {
            object?[] d = dataOrTestDataRow;
            ParameterInfo[] parameters = methodInfo.GetParameters();
            if (TestDataSourceHelpers.TryHandleITestDataRow(d, parameters, out d, out string? ignoreMessageFromTestDataRow, out string? displayNameFromTestDataRow, out IList<string>? testCategoriesFromTestDataRow))
            {
                testDataSourceIgnoreMessage = ignoreMessageFromTestDataRow ?? testDataSourceIgnoreMessage;
            }
            else if (TestDataSourceHelpers.IsDataConsideredSingleArgumentValue(d, parameters))
            {
                // SPECIAL CASE:
                // This condition is a duplicate of the condition in GetInvokeResultAsync.
                //
                // The known scenario we know of that shows importance of that check is if we have DynamicData using this member
                //
                // public static IEnumerable<object[]> GetData()
                // {
                //     yield return new object[] { ("Hello", "World") };
                // }
                //
                // If the test method has a single parameter which is 'object[]', then we should pass the tuple array as is.
                // Note that normally, the array in this code path represents the arguments of the test method.
                // However, GetInvokeResultAsync uses the above check to mean "the whole array is the single argument to the test method"
            }
            else if (d?.Length == 1 && TestDataSourceHelpers.TryHandleTupleDataSource(d[0], parameters, out object?[] tupleExpandedToArray))
            {
                d = tupleExpandedToArray;
            }

            UnitTestElement discoveredTest = test.Clone();
            discoveredTest.TestMethod.DisplayName = displayNameFromTestDataRow
                ?? dataSource.GetDisplayName(methodInfo, d)
                ?? TestDataSourceUtilities.ComputeDefaultDisplayName(methodInfo, d)
                ?? discoveredTest.TestMethod.DisplayName;

            // Merge test categories from the test data row with the existing categories
            if (testCategoriesFromTestDataRow is { Count: > 0 })
            {
                discoveredTest.TestCategory = discoveredTest.TestCategory is { Length: > 0 }
                    ? [.. testCategoriesFromTestDataRow, .. discoveredTest.TestCategory]
                    : [.. testCategoriesFromTestDataRow];
            }

            try
            {
                discoveredTest.TestMethod.SerializedData = DataSerializationHelper.Serialize(d);
                discoveredTest.TestMethod.ActualData = d;
                discoveredTest.TestMethod.TestCaseIndex = globalTestCaseIndex;
                discoveredTest.TestMethod.TestDataSourceIgnoreMessage = testDataSourceIgnoreMessage;
                discoveredTest.TestMethod.DataType = DynamicDataType.ITestDataSource;
            }
            catch (SerializationException ex)
            {
                string warning = string.Format(CultureInfo.CurrentCulture, Resource.CannotExpandIDataSourceAttribute_CannotSerialize, globalTestCaseIndex, discoveredTest.TestMethod.DisplayName);
                warning += Environment.NewLine;
                warning += ex.ToString();
                warning = string.Format(CultureInfo.CurrentCulture, Resource.CannotExpandIDataSourceAttribute, test.TestMethod.ManagedTypeName, test.TestMethod.ManagedMethodName, warning);
                PlatformServiceProvider.Instance.AdapterTraceLogger.LogWarning($"DynamicDataEnumerator: {warning}");

                // Serialization failed for the type, bail out. Caller will handle adding the original test.
                return false;
            }

            discoveredTests.Add(discoveredTest);
            globalTestCaseIndex++;
        }

        tests.AddRange(discoveredTests);

        return true;
    }
}
