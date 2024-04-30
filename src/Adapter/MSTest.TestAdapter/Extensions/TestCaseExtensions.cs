// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.TestPlatform.AdapterUtilities;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Extensions;

/// <summary>
/// Extension Methods for TestCase Class.
/// </summary>
internal static class TestCaseExtensions
{
    internal static readonly TestProperty ManagedTypeProperty = TestProperty.Register(
        id: ManagedNameConstants.ManagedTypePropertyId,
        label: ManagedNameConstants.ManagedTypeLabel,
        category: string.Empty,
        description: string.Empty,
        valueType: typeof(string),
        validateValueCallback: o => !StringEx.IsNullOrWhiteSpace(o as string),
        attributes: TestPropertyAttributes.Hidden,
        owner: typeof(TestCase));

    internal static readonly TestProperty ManagedMethodProperty = TestProperty.Register(
        id: ManagedNameConstants.ManagedMethodPropertyId,
        label: ManagedNameConstants.ManagedMethodLabel,
        category: string.Empty,
        description: string.Empty,
        valueType: typeof(string),
        validateValueCallback: o => !StringEx.IsNullOrWhiteSpace(o as string),
        attributes: TestPropertyAttributes.Hidden,
        owner: typeof(TestCase));

    internal static readonly TestProperty HierarchyProperty = TestProperty.Register(
        id: HierarchyConstants.HierarchyPropertyId,
        label: HierarchyConstants.HierarchyLabel,
        category: string.Empty,
        description: string.Empty,
        valueType: typeof(string[]),
        validateValueCallback: null,
        attributes: TestPropertyAttributes.Immutable,
        owner: typeof(TestCase));

    /// <summary>
    /// The test name.
    /// </summary>
    /// <param name="testCase"> The test case. </param>
    /// <param name="testClassName"> The test case's class name. </param>
    /// <returns> The test name, without the class name, if provided. </returns>
    internal static string GetTestName(this TestCase testCase, string? testClassName)
    {
        string fullyQualifiedName = testCase.FullyQualifiedName;

        // Not using Replace because there can be multiple instances of that string.
        string name = fullyQualifiedName.StartsWith($"{testClassName}.", StringComparison.Ordinal)
            ? fullyQualifiedName.Remove(0, $"{testClassName}.".Length)
            : fullyQualifiedName;

        return name;
    }

    /// <summary>
    /// The to unit test element.
    /// </summary>
    /// <param name="testCase"> The test case. </param>
    /// <param name="source"> The source. If deployed this is the full path of the source in the deployment directory. </param>
    /// <returns> The converted <see cref="UnitTestElement"/>. </returns>
    internal static UnitTestElement ToUnitTestElement(this TestCase testCase, string source)
    {
        bool isAsync = (testCase.GetPropertyValue(Constants.AsyncTestProperty) as bool?) ?? false;
        string? testClassName = testCase.GetPropertyValue(Constants.TestClassNameProperty) as string;
        string name = testCase.GetTestName(testClassName);
        var testIdGenerationStrategy = (TestIdGenerationStrategy)testCase.GetPropertyValue(
            Constants.TestIdGenerationStrategyProperty,
            (int)TestIdGenerationStrategy.FullyQualified);

        TestMethod testMethod = testCase.ContainsManagedMethodAndType()
            ? new(testCase.GetManagedType(), testCase.GetManagedMethod(), testCase.GetHierarchy()!, name, testClassName!, source, isAsync, testIdGenerationStrategy)
            : new(name, testClassName!, source, isAsync, testIdGenerationStrategy);
        var dataType = (DynamicDataType)testCase.GetPropertyValue(Constants.TestDynamicDataTypeProperty, (int)DynamicDataType.None);
        if (dataType != DynamicDataType.None)
        {
            string[]? data = testCase.GetPropertyValue<string[]>(Constants.TestDynamicDataProperty, null);

            testMethod.DataType = dataType;
            testMethod.SerializedData = data;
        }

        testMethod.DisplayName = testCase.DisplayName;

        if (testCase.GetPropertyValue(Constants.DeclaringClassNameProperty) is string declaringClassName && declaringClassName != testClassName)
        {
            testMethod.DeclaringClassFullName = declaringClassName;
        }

        UnitTestElement testElement = new(testMethod)
        {
            IsAsync = isAsync,
            TestCategory = testCase.GetPropertyValue(Constants.TestCategoryProperty) as string[],
            Priority = testCase.GetPropertyValue(Constants.PriorityProperty) as int?,
            DisplayName = testCase.DisplayName,
        };

        if (testCase.Traits.Any())
        {
            testElement.Traits = testCase.Traits.ToArray();
        }

        string? cssIteration = testCase.GetPropertyValue<string>(Constants.CssIterationProperty, null);
        if (!StringEx.IsNullOrWhiteSpace(cssIteration))
        {
            testElement.CssIteration = cssIteration;
        }

        string? cssProjectStructure = testCase.GetPropertyValue<string>(Constants.CssProjectStructureProperty, null);
        if (!StringEx.IsNullOrWhiteSpace(cssProjectStructure))
        {
            testElement.CssProjectStructure = cssProjectStructure;
        }

        string? description = testCase.GetPropertyValue<string>(Constants.DescriptionProperty, null);
        if (!StringEx.IsNullOrWhiteSpace(description))
        {
            testElement.Description = description;
        }

        string[]? workItemIds = testCase.GetPropertyValue<string[]>(Constants.WorkItemIdsProperty, null);
        if (workItemIds != null && workItemIds.Length > 0)
        {
            testElement.WorkItemIds = workItemIds;
        }

        KeyValuePair<string, string>[]? deploymentItems = testCase.GetPropertyValue<KeyValuePair<string, string>[]>(Constants.DeploymentItemsProperty, null);
        if (deploymentItems != null && deploymentItems.Length > 0)
        {
            testElement.DeploymentItems = deploymentItems;
        }

        testElement.DoNotParallelize = testCase.GetPropertyValue(Constants.DoNotParallelizeProperty, false);

        return testElement;
    }

    internal static string? GetManagedType(this TestCase testCase) => testCase.GetPropertyValue<string>(ManagedTypeProperty, null);

    internal static void SetManagedType(this TestCase testCase, string value) => testCase.SetPropertyValue(ManagedTypeProperty, value);

    internal static string? GetManagedMethod(this TestCase testCase) => testCase.GetPropertyValue<string>(ManagedMethodProperty, null);

    internal static void SetManagedMethod(this TestCase testCase, string value) => testCase.SetPropertyValue(ManagedMethodProperty, value);

    internal static bool ContainsManagedMethodAndType(this TestCase testCase) => !StringEx.IsNullOrWhiteSpace(testCase.GetManagedMethod()) && !StringEx.IsNullOrWhiteSpace(testCase.GetManagedType());

    internal static string[]? GetHierarchy(this TestCase testCase) => testCase.GetPropertyValue<string[]>(HierarchyProperty, null);

    internal static void SetHierarchy(this TestCase testCase, params string?[] value) => testCase.SetPropertyValue(HierarchyProperty, value);
}
