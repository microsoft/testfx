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
        foreach (var method in _type.GetRuntimeMethods())
        {
            var isMethodDeclaredInTestTypeAssembly = _reflectHelper.IsMethodDeclaredInSameAssemblyAsType(method, _type);
            var enableMethodsFromOtherAssemblies = MSTestSettings.CurrentSettings.EnableBaseClassTestMethodsFromOtherAssemblies;

            if (!isMethodDeclaredInTestTypeAssembly && !enableMethodsFromOtherAssemblies)
            {
                continue;
            }

            if (_testMethodValidator.IsValidTestMethod(method, _type, warnings))
            {
                foundDuplicateTests = foundDuplicateTests || !foundTests.Add(method.Name);
                var testMethod = GetTestFromMethod(method, isMethodDeclaredInTestTypeAssembly, warnings);

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
        var currentType = _type;
        int currentDepth = 0;

        while (currentType != null)
        {
            inheritanceDepths[currentType.FullName!] = currentDepth;
            ++currentDepth;
            currentType = currentType.GetTypeInfo().BaseType;
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
        var isAsync = ReflectHelper.MatchReturnType(method, typeof(Task));

        var testMethod = new TestMethod(method, method.Name, _type.FullName!, _assemblyFilePath, isAsync, _testIdGenerationStrategy);

        if (!string.Equals(method.DeclaringType!.FullName, _type.FullName, StringComparison.Ordinal))
        {
            testMethod.DeclaringClassFullName = method.DeclaringType.FullName;
        }

        if (!isDeclaredInTestTypeAssembly)
        {
            testMethod.DeclaringAssemblyName =
                PlatformServiceProvider.Instance.FileOperations.GetAssemblyPath(
                    method.DeclaringType.GetTypeInfo().Assembly);
        }

        var testElement = new UnitTestElement(testMethod)
        {
            // Get compiler generated type name for async test method (either void returning or task returning).
            AsyncTypeName = method.GetAsyncTypeName(),
            TestCategory = _reflectHelper.GetCategories(method, _type),
            DoNotParallelize = _reflectHelper.IsDoNotParallelizeSet(method, _type),
            Priority = _reflectHelper.GetPriority(method),
            Ignored = _reflectHelper.IsAttributeDefined<IgnoreAttribute>(method, false),
            DeploymentItems = PlatformServiceProvider.Instance.TestDeployment.GetDeploymentItems(method, _type, warnings),
        };

        var traits = _reflectHelper.GetTestPropertiesAsTraits(method);

        var ownerTrait = _reflectHelper.GetTestOwnerAsTraits(method);
        if (ownerTrait != null)
        {
            traits = traits.Concat(new[] { ownerTrait });
        }

        var priorityTrait = _reflectHelper.GetTestPriorityAsTraits(testElement.Priority);
        if (priorityTrait != null)
        {
            traits = traits.Concat(new[] { priorityTrait });
        }

        testElement.Traits = traits.ToArray();

        if (_reflectHelper.GetCustomAttribute<CssIterationAttribute>(method) is CssIterationAttribute cssIteration)
        {
            testElement.CssIteration = cssIteration.CssIteration;
        }

        if (_reflectHelper.GetCustomAttribute<CssProjectStructureAttribute>(method) is CssProjectStructureAttribute cssProjectStructure)
        {
            testElement.CssProjectStructure = cssProjectStructure.CssProjectStructure;
        }

        if (_reflectHelper.GetCustomAttribute<DescriptionAttribute>(method) is DescriptionAttribute descriptionAttribute)
        {
            testElement.Description = descriptionAttribute.Description;
        }

        var workItemAttributes = _reflectHelper.GetCustomAttributes<WorkItemAttribute>(method);
        if (workItemAttributes.Length != 0)
        {
            testElement.WorkItemIds = workItemAttributes.Select(x => x.Id.ToString(CultureInfo.InvariantCulture)).ToArray();
        }

        // get DisplayName from TestMethodAttribute (or any inherited attribute)
        var testMethodAttribute = _reflectHelper.GetCustomAttribute<TestMethodAttribute>(method);
        testElement.DisplayName = testMethodAttribute?.DisplayName ?? method.Name;

        return testElement;
    }
}
