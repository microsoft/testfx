// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Discovery;

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Extensions;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Helpers;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;
using Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// Enumerates through the type looking for Valid Test Methods to execute.
/// </summary>
internal class TypeEnumerator
{
    private readonly Type type;
    private readonly string assemblyName;
    private readonly TypeValidator typeValidator;
    private readonly TestMethodValidator testMethodValidator;
    private readonly ReflectHelper reflectHelper;

    /// <summary>
    /// Initializes a new instance of the <see cref="TypeEnumerator"/> class.
    /// </summary>
    /// <param name="type"> The reflected type. </param>
    /// <param name="assemblyName"> The name of the assembly being reflected. </param>
    /// <param name="reflectHelper"> An instance to reflection helper for type information. </param>
    /// <param name="typeValidator"> The validator for test classes. </param>
    /// <param name="testMethodValidator"> The validator for test methods. </param>
    internal TypeEnumerator(Type type, string assemblyName, ReflectHelper reflectHelper, TypeValidator typeValidator, TestMethodValidator testMethodValidator)
    {
        this.type = type;
        this.assemblyName = assemblyName;
        this.reflectHelper = reflectHelper;
        this.typeValidator = typeValidator;
        this.testMethodValidator = testMethodValidator;
    }

    /// <summary>
    /// Walk through all methods in the type, and find out the test methods
    /// </summary>
    /// <param name="warnings"> Contains warnings if any, that need to be passed back to the caller. </param>
    /// <returns> list of test cases.</returns>
    internal virtual ICollection<UnitTestElement> Enumerate(out ICollection<string> warnings)
    {
        warnings = new Collection<string>();

        if (!typeValidator.IsValidTestClass(type, warnings))
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
        foreach (var method in type.GetRuntimeMethods())
        {
            var isMethodDeclaredInTestTypeAssembly = reflectHelper.IsMethodDeclaredInSameAssemblyAsType(method, type);
            var enableMethodsFromOtherAssemblies = MSTestSettings.CurrentSettings.EnableBaseClassTestMethodsFromOtherAssemblies;

            if (!isMethodDeclaredInTestTypeAssembly && !enableMethodsFromOtherAssemblies)
            {
                continue;
            }

            if (testMethodValidator.IsValidTestMethod(method, type, warnings))
            {
                foundDuplicateTests = foundDuplicateTests || !foundTests.Add(method.Name);
                tests.Add(GetTestFromMethod(method, isMethodDeclaredInTestTypeAssembly, warnings));
            }
        }

        if (!foundDuplicateTests)
        {
            return tests;
        }

        // Remove duplicate test methods by taking the first one of each name
        // that is declared closest to the test class in the hierarchy.
        var inheritanceDepths = new Dictionary<string, int>();
        var currentType = type;
        int currentDepth = 0;

        while (currentType != null)
        {
            inheritanceDepths[currentType.FullName] = currentDepth;
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
        Debug.Assert(type.AssemblyQualifiedName != null, "AssemblyQualifiedName for method is null.");

        // This allows void returning async test method to be valid test method. Though they will be executed similar to non-async test method.
        var isAsync = ReflectHelper.MatchReturnType(method, typeof(Task));

        var testMethod = new TestMethod(method, method.Name, type.FullName, assemblyName, isAsync);

        if (!method.DeclaringType.FullName.Equals(type.FullName))
        {
            testMethod.DeclaringClassFullName = method.DeclaringType.FullName;
        }

        if (!isDeclaredInTestTypeAssembly)
        {
            testMethod.DeclaringAssemblyName =
                PlatformServiceProvider.Instance.FileOperations.GetAssemblyPath(
                    method.DeclaringType.GetTypeInfo().Assembly);
        }

        var testElement = new UnitTestElement(testMethod);

        // Get compiler generated type name for async test method (either void returning or task returning).
        var asyncTypeName = method.GetAsyncTypeName();
        testElement.AsyncTypeName = asyncTypeName;

        testElement.TestCategory = reflectHelper.GetCategories(method, type);

        testElement.DoNotParallelize = reflectHelper.IsDoNotParallelizeSet(method, type);

        var traits = reflectHelper.GetTestPropertiesAsTraits(method);

        var ownerTrait = reflectHelper.GetTestOwnerAsTraits(method);
        if (ownerTrait != null)
        {
            traits = traits.Concat(new[] { ownerTrait });
        }

        testElement.Priority = reflectHelper.GetPriority(method);

        var priorityTrait = reflectHelper.GetTestPriorityAsTraits(testElement.Priority);
        if (priorityTrait != null)
        {
            traits = traits.Concat(new[] { priorityTrait });
        }

        testElement.Traits = traits.ToArray();

        if (reflectHelper.GetCustomAttribute(method, typeof(CssIterationAttribute)) is CssIterationAttribute cssIteration)
        {
            testElement.CssIteration = cssIteration.CssIteration;
        }

        if (reflectHelper.GetCustomAttribute(method, typeof(CssProjectStructureAttribute)) is CssProjectStructureAttribute cssProjectStructure)
        {
            testElement.CssProjectStructure = cssProjectStructure.CssProjectStructure;
        }

        if (reflectHelper.GetCustomAttribute(method, typeof(DescriptionAttribute)) is DescriptionAttribute descriptionAttribute)
        {
            testElement.Description = descriptionAttribute.Description;
        }

        var workItemAttributes = reflectHelper.GetCustomAttributes(method, typeof(WorkItemAttribute)).Cast<WorkItemAttribute>().ToArray();
        if (workItemAttributes.Any())
        {
            testElement.WorkItemIds = workItemAttributes.Select(x => x.Id.ToString()).ToArray();
        }

        testElement.Ignored = reflectHelper.IsAttributeDefined(method, typeof(IgnoreAttribute), false);

        // Get Deployment items if any.
        testElement.DeploymentItems = PlatformServiceProvider.Instance.TestDeployment.GetDeploymentItems(method, type, warnings);

        // get DisplayName from TestMethodAttribute
        var testMethodAttribute = reflectHelper.GetCustomAttribute(method, typeof(TestMethodAttribute)) as TestMethodAttribute;
        testElement.DisplayName = testMethodAttribute?.DisplayName ?? method.Name;

        return testElement;
    }
}
