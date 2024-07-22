// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.ObjectModel;
using System.Globalization;
using System.Reflection;

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Extensions;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Helpers;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Discovery;

/// <summary>
/// Enumerates through the type looking for Valid Test Methods to execute.
/// </summary>
internal class TypeEnumerator
{
    private readonly Type _type;
    private readonly string _assemblyFilePath;
    private readonly TypeValidator _typeValidator;
    private readonly TestMethodValidator _testMethodValidator;
    private readonly TestIdGenerationStrategy _testIdGenerationStrategy;
    private readonly TestDataSourceDiscoveryOption _discoveryOption;
    private readonly ReflectHelper _reflectHelper;

    /// <summary>
    /// Initializes a new instance of the <see cref="TypeEnumerator"/> class.
    /// </summary>
    /// <param name="type"> The reflected type. </param>
    /// <param name="assemblyFilePath"> The name of the assembly being reflected. </param>
    /// <param name="reflectHelper"> An instance to reflection helper for type information. </param>
    /// <param name="typeValidator"> The validator for test classes. </param>
    /// <param name="testMethodValidator"> The validator for test methods. </param>
    /// <param name="testIdGenerationStrategy"><see cref="TestIdGenerationStrategy"/> to use when generating TestId.</param>
    internal TypeEnumerator(Type type, string assemblyFilePath, ReflectHelper reflectHelper, TypeValidator typeValidator, TestMethodValidator testMethodValidator, TestDataSourceDiscoveryOption discoveryOption, TestIdGenerationStrategy testIdGenerationStrategy)
    {
        _type = type;
        _assemblyFilePath = assemblyFilePath;
        _reflectHelper = reflectHelper;
        _typeValidator = typeValidator;
        _testMethodValidator = testMethodValidator;
        _testIdGenerationStrategy = testIdGenerationStrategy;
        _discoveryOption = discoveryOption;
    }

    /// <summary>
    /// Walk through all methods in the type, and find out the test methods.
    /// </summary>
    /// <param name="warnings"> Contains warnings if any, that need to be passed back to the caller. </param>
    /// <returns> list of test cases.</returns>
    internal virtual ICollection<UnitTestElement>? Enumerate(out ICollection<string> warnings)
    {
        warnings = new Collection<string>();

        if (!_typeValidator.IsValidTestClass(_type, warnings))
        {
            return null;
        }

        // If test class is valid, then get the tests
        return GetTests(warnings);
    }

    /// <summary>
    /// Gets a list of valid tests in a type.
    /// </summary>
    /// <param name="warnings"> Contains warnings if any, that need to be passed back to the caller. </param>
    /// <returns> List of Valid Tests. </returns>
    internal Collection<UnitTestElement> GetTests(ICollection<string> warnings)
    {
        bool foundDuplicateTests = false;
        var foundTests = new HashSet<string>();
        var tests = new Collection<UnitTestElement>();

        // Test class is already valid. Verify methods.
        // PERF: GetRuntimeMethods is used here to get all methods, including non-public, and static methods.
        // if we rely on analyzers to identify all invalid methods on build, we can change this to fit the current settings.
        foreach (MethodInfo method in _type.GetRuntimeMethods())
        {
            bool isMethodDeclaredInTestTypeAssembly = _reflectHelper.IsMethodDeclaredInSameAssemblyAsType(method, _type);
            bool enableMethodsFromOtherAssemblies = MSTestSettings.CurrentSettings.EnableBaseClassTestMethodsFromOtherAssemblies;

            if (!isMethodDeclaredInTestTypeAssembly && !enableMethodsFromOtherAssemblies)
            {
                continue;
            }

            if (_testMethodValidator.IsValidTestMethod(method, _type, warnings))
            {
                // ToString() outputs method name and its signature. This is necessary for overloaded methods to be recognized as distinct tests.
                foundDuplicateTests = foundDuplicateTests || !foundTests.Add(method.ToString() ?? method.Name);
                UnitTestElement testMethod = GetTestFromMethod(method, isMethodDeclaredInTestTypeAssembly, warnings);

                tests.Add(testMethod);
            }
        }

        if (!foundDuplicateTests)
        {
            return tests;
        }

        // Remove duplicate test methods by taking the first one of each name
        // that is declared closest to the test class in the hierarchy.
        var inheritanceDepths = new Dictionary<string, int>();
        Type? currentType = _type;
        int currentDepth = 0;

        while (currentType != null)
        {
            inheritanceDepths[currentType.FullName!] = currentDepth;
            ++currentDepth;
            currentType = currentType.BaseType;
        }

        return new Collection<UnitTestElement>(
            tests.GroupBy(
                t => t.TestMethod.Name,
                (_, elements) =>
                    elements.OrderBy(t => inheritanceDepths[t.TestMethod.DeclaringClassFullName ?? t.TestMethod.FullClassName]).First())
                .ToList());
    }

    /// <summary>
    /// Gets a UnitTestElement from a MethodInfo object filling it up with appropriate values.
    /// </summary>
    /// <param name="method">The reflected method.</param>
    /// <param name="isDeclaredInTestTypeAssembly">True if the reflected method is declared in the same assembly as the current type.</param>
    /// <param name="warnings">Contains warnings if any, that need to be passed back to the caller.</param>
    /// <returns> Returns a UnitTestElement.</returns>
    internal UnitTestElement GetTestFromMethod(MethodInfo method, bool isDeclaredInTestTypeAssembly, ICollection<string> warnings)
    {
        // null if the current instance represents a generic type parameter.
        DebugEx.Assert(_type.AssemblyQualifiedName != null, "AssemblyQualifiedName for method is null.");

        // This allows void returning async test method to be valid test method. Though they will be executed similar to non-async test method.
        bool isAsync = ReflectHelper.MatchReturnType(method, typeof(Task));

        var testMethod = new TestMethod(method, method.Name, _type.FullName!, _assemblyFilePath, isAsync, _testIdGenerationStrategy);

        if (!string.Equals(method.DeclaringType!.FullName, _type.FullName, StringComparison.Ordinal))
        {
            testMethod.DeclaringClassFullName = method.DeclaringType.FullName;
        }

        if (!isDeclaredInTestTypeAssembly)
        {
            testMethod.DeclaringAssemblyName =
                PlatformServiceProvider.Instance.FileOperations.GetAssemblyPath(
                    method.DeclaringType.Assembly);
        }

        // PERF: When discovery option is set to DuringDiscovery, we will expand data on tests to one test case
        // per data item. This will happen in AssemblyEnumerator. But AssemblyEnumerator does not have direct access to
        // the method info or method attributes, so it would create a TestMethodInfo to see if the test is data driven.
        // Creating TestMethodInfo is expensive and should be done only for a test that we know is data driven.
        //
        // So to optimize this we check if we have some data source attribute. Because here we have access to all attributes
        // and we store that info in DataType. AssemblyEnumerator will pick this up and will get the real test data in the expensive way
        // or it will skip over getting the data cheaply, when DataType = DynamicDataType.None.
        //
        // This needs to be done only when DuringDiscovery is set, because otherwise we would populate the DataType, but we would not populate
        // and execution would not try to re-populate the data, because DataType is already set to data driven, so it would just throw error about empty data.
        if (_discoveryOption == TestDataSourceDiscoveryOption.DuringDiscovery)
        {
            testMethod.DataType = GetDynamicDataType(method);
        }

        var testElement = new UnitTestElement(testMethod)
        {
            // Get compiler generated type name for async test method (either void returning or task returning).
            AsyncTypeName = method.GetAsyncTypeName(),
            TestCategory = _reflectHelper.GetTestCategories(method, _type),
            DoNotParallelize = _reflectHelper.IsDoNotParallelizeSet(method, _type),
            Priority = _reflectHelper.GetPriority(method),
            Ignored = _reflectHelper.IsNonDerivedAttributeDefined<IgnoreAttribute>(method, inherit: false),
            DeploymentItems = PlatformServiceProvider.Instance.TestDeployment.GetDeploymentItems(method, _type, warnings),
        };

        var traits = _reflectHelper.GetTestPropertiesAsTraits(method).ToList();

        TestPlatform.ObjectModel.Trait? ownerTrait = _reflectHelper.GetTestOwnerAsTraits(method);
        if (ownerTrait != null)
        {
            traits.Add(ownerTrait);
        }

        TestPlatform.ObjectModel.Trait? priorityTrait = _reflectHelper.GetTestPriorityAsTraits(testElement.Priority);
        if (priorityTrait != null)
        {
            traits.Add(priorityTrait);
        }

        testElement.Traits = traits.ToArray();

        if (_reflectHelper.GetFirstDerivedAttributeOrDefault<CssIterationAttribute>(method, inherit: true) is CssIterationAttribute cssIteration)
        {
            testElement.CssIteration = cssIteration.CssIteration;
        }

        if (_reflectHelper.GetFirstDerivedAttributeOrDefault<CssProjectStructureAttribute>(method, inherit: true) is CssProjectStructureAttribute cssProjectStructure)
        {
            testElement.CssProjectStructure = cssProjectStructure.CssProjectStructure;
        }

        if (_reflectHelper.GetFirstDerivedAttributeOrDefault<DescriptionAttribute>(method, inherit: true) is DescriptionAttribute descriptionAttribute)
        {
            testElement.Description = descriptionAttribute.Description;
        }

        WorkItemAttribute[] workItemAttributes = _reflectHelper.GetDerivedAttributes<WorkItemAttribute>(method, inherit: true).ToArray();
        if (workItemAttributes.Length != 0)
        {
            testElement.WorkItemIds = workItemAttributes.Select(x => x.Id.ToString(CultureInfo.InvariantCulture)).ToArray();
        }

        // get DisplayName from TestMethodAttribute (or any inherited attribute)
        TestMethodAttribute? testMethodAttribute = _reflectHelper.GetFirstDerivedAttributeOrDefault<TestMethodAttribute>(method, inherit: true);
        testElement.DisplayName = testMethodAttribute?.DisplayName ?? method.Name;

        return testElement;
    }

    private DynamicDataType GetDynamicDataType(MethodInfo method)
    {
        foreach (Attribute attribute in _reflectHelper.GetDerivedAttributes<Attribute>(method, inherit: true))
        {
            if (AttributeComparer.IsDerived<ITestDataSource>(attribute))
            {
                return DynamicDataType.ITestDataSource;
            }

            if (AttributeComparer.IsDerived<DataSourceAttribute>(attribute))
            {
                return DynamicDataType.DataSourceAttribute;
            }
        }

        return DynamicDataType.None;
    }
}
