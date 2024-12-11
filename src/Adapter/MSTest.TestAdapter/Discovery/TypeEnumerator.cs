// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Reflection;

using Microsoft.TestPlatform.AdapterUtilities;
using Microsoft.TestPlatform.AdapterUtilities.ManagedNameUtilities;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Extensions;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Helpers;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Discovery;

/// <summary>
/// Enumerates through the type looking for Valid Test Methods to execute.
/// </summary>
[SuppressMessage("Performance", "CA1852: Seal internal types", Justification = "Overrides required for testability")]
internal class TypeEnumerator
{
    private readonly Type _type;
    private readonly string _assemblyFilePath;
    private readonly TypeValidator _typeValidator;
    private readonly TestMethodValidator _testMethodValidator;
    private readonly TestIdGenerationStrategy _testIdGenerationStrategy;
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
    internal TypeEnumerator(Type type, string assemblyFilePath, ReflectHelper reflectHelper, TypeValidator typeValidator, TestMethodValidator testMethodValidator, TestIdGenerationStrategy testIdGenerationStrategy)
    {
        _type = type;
        _assemblyFilePath = assemblyFilePath;
        _reflectHelper = reflectHelper;
        _typeValidator = typeValidator;
        _testMethodValidator = testMethodValidator;
        _testIdGenerationStrategy = testIdGenerationStrategy;
    }

    /// <summary>
    /// Walk through all methods in the type, and find out the test methods.
    /// </summary>
    /// <param name="warnings"> Contains warnings if any, that need to be passed back to the caller. </param>
    /// <returns> list of test cases.</returns>
    internal virtual List<UnitTestElement>? Enumerate(List<string> warnings)
    {
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
    internal List<UnitTestElement> GetTests(List<string> warnings)
    {
        bool foundDuplicateTests = false;
        var foundTests = new HashSet<string>();
        var tests = new List<UnitTestElement>();

        // Test class is already valid. Verify methods.
        // PERF: GetRuntimeMethods is used here to get all methods, including non-public, and static methods.
        // if we rely on analyzers to identify all invalid methods on build, we can change this to fit the current settings.
        foreach (MethodInfo method in PlatformServiceProvider.Instance.ReflectionOperations.GetRuntimeMethods(_type))
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

        return tests.GroupBy(
            t => t.TestMethod.Name,
            (_, elements) =>
                elements.OrderBy(t => inheritanceDepths[t.TestMethod.DeclaringClassFullName ?? t.TestMethod.FullClassName]).First())
            .ToList();
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

        ManagedNameHelper.GetManagedName(method, out string managedType, out string managedMethod, out string?[]? hierarchyValues);
        hierarchyValues[HierarchyConstants.Levels.ContainerIndex] = null; // This one will be set by test windows to current test project name.
        var testMethod = new TestMethod(managedType, managedMethod, hierarchyValues, method.Name, _type.FullName!, _assemblyFilePath, isAsync, null, _testIdGenerationStrategy);

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

        Attribute[] attributes = _reflectHelper.GetCustomAttributesCached(method, inherit: true);
        TestMethodAttribute? testMethodAttribute = null;

        // Backward looping for backcompat. This used to be calls to _reflectHelper.GetFirstDerivedAttributeOrDefault
        // So, to make sure the first attribute always wins, we loop from end to start.
        for (int i = attributes.Length - 1; i >= 0; i--)
        {
            if (attributes[i] is TestMethodAttribute tma)
            {
                testMethodAttribute = tma;
            }
            else if (attributes[i] is CssIterationAttribute cssIteration)
            {
                testElement.CssIteration = cssIteration.CssIteration;
            }
            else if (attributes[i] is CssProjectStructureAttribute cssProjectStructure)
            {
                testElement.CssProjectStructure = cssProjectStructure.CssProjectStructure;
            }
            else if (attributes[i] is DescriptionAttribute descriptionAttribute)
            {
                testElement.Description = descriptionAttribute.Description;
            }
        }

        IEnumerable<WorkItemAttribute> workItemAttributes = attributes.OfType<WorkItemAttribute>();
        if (workItemAttributes.Any())
        {
            testElement.WorkItemIds = workItemAttributes.Select(x => x.Id.ToString(CultureInfo.InvariantCulture)).ToArray();
        }

        // In production, we always have a TestMethod attribute because GetTestFromMethod is called under IsValidTestMethod
        // In unit tests, we may not have the test to have TestMethodAttribute.
        // TODO: Adjust all unit tests to properly have the attribute and uncomment the assert.
        // DebugEx.Assert(testMethodAttribute is not null, "Expected to find a 'TestMethod' attribute.");

        // get DisplayName from TestMethodAttribute (or any inherited attribute)
        testElement.DisplayName = testMethodAttribute?.DisplayName ?? method.Name;

        return testElement;
    }
}
