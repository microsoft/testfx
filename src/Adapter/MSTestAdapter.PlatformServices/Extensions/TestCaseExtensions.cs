// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.TestPlatform.AdapterUtilities;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;
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
    internal static UnitTestElement ToUnitTestElementWithUpdatedSource(this TestCase testCase, string source)
    {
        if (testCase.LocalExtensionData is UnitTestElement unitTestElement)
        {
            // If the requested source is different, clone it with updated source.
            // This can happen when there are deployment items in tests.
            // In this case, the source would be the path of the deployment directory.
            // If we don't return UnitTestElement with the correct path to deployment directory, we will
            // end up trying to load the test assembly twice in the same appdomain, once with the default context and once in a LoadFrom context.
            // See https://github.com/microsoft/testfx/issues/6713
            return unitTestElement.TestMethod.AssemblyName != source
                ? unitTestElement.CloneWithUpdatedSource(source)
                : unitTestElement;
        }

        string? testClassName = testCase.GetPropertyValue(EngineConstants.TestClassNameProperty) as string;
        string name = testCase.GetTestName(testClassName);

        var testMethod = new TestMethod(testCase.GetManagedType(), testCase.GetManagedMethod(), testCase.GetHierarchy(), name, testClassName!, source, testCase.DisplayName, testCase.GetPropertyValue<string>(EngineConstants.ParameterTypesProperty, null));

        var dataType = (DynamicDataType)testCase.GetPropertyValue(EngineConstants.TestDynamicDataTypeProperty, (int)DynamicDataType.None);
        if (dataType != DynamicDataType.None)
        {
            string[]? data = testCase.GetPropertyValue<string[]>(EngineConstants.TestDynamicDataProperty, null);

            testMethod.DataType = dataType;
            testMethod.SerializedData = data;
            testMethod.TestCaseIndex = testCase.GetPropertyValue<int>(EngineConstants.TestCaseIndexProperty, 0);
            testMethod.TestDataSourceIgnoreMessage = testCase.GetPropertyValue(EngineConstants.TestDataSourceIgnoreMessageProperty) as string;
        }

        if (testCase.GetPropertyValue(EngineConstants.DeclaringClassNameProperty) is string declaringClassName && declaringClassName != testClassName)
        {
            testMethod.DeclaringClassFullName = declaringClassName;
        }

        UnitTestElement testElement = new(testMethod)
        {
            TestCategory = testCase.GetPropertyValue(EngineConstants.TestCategoryProperty) as string[],
            Priority = testCase.GetPropertyValue(EngineConstants.PriorityProperty) as int?,
            UnfoldingStrategy = (TestDataSourceUnfoldingStrategy)testCase.GetPropertyValue(EngineConstants.UnfoldingStrategy, (int)TestDataSourceUnfoldingStrategy.Auto),
        };

        if (testCase.Traits.Any())
        {
            testElement.Traits = [.. testCase.Traits];
        }

        string[]? workItemIds = testCase.GetPropertyValue<string[]>(EngineConstants.WorkItemIdsProperty, null);
        if (workItemIds is { Length: > 0 })
        {
            testElement.WorkItemIds = workItemIds;
        }

#if !WINDOWS_UWP && !WIN_UI
        KeyValuePair<string, string>[]? deploymentItems = testCase.GetPropertyValue<KeyValuePair<string, string>[]>(EngineConstants.DeploymentItemsProperty, null);
        if (deploymentItems is { Length: > 0 })
        {
            testElement.DeploymentItems = deploymentItems;
        }
#endif

        testElement.DoNotParallelize = testCase.GetPropertyValue(EngineConstants.DoNotParallelizeProperty, false);

        return testElement;
    }

    internal static string? GetManagedType(this TestCase testCase) => testCase.GetPropertyValue<string>(ManagedTypeProperty, null);

    internal static string? GetManagedMethod(this TestCase testCase) => testCase.GetPropertyValue<string>(ManagedMethodProperty, null);

    internal static string[]? GetHierarchy(this TestCase testCase) => testCase.GetPropertyValue<string[]>(HierarchyProperty, null);

    internal static void SetHierarchy(this TestCase testCase, string?[] value) => testCase.SetPropertyValue(HierarchyProperty, value);
}
