// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Extensions
{
    using System.Collections.Generic;
    using System.Linq;

    using Microsoft.TestPlatform.AdapterUtilities;
    using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;
    using Microsoft.VisualStudio.TestPlatform.ObjectModel;

    using Constants = Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Constants;

    /// <summary>
    /// Extension Methods for TestCase Class
    /// </summary>
    internal static class TestCaseExtensions
    {
        internal static readonly TestProperty ManagedTypeProperty = TestProperty.Register(
            id: ManagedNameConstants.ManagedTypePropertyId,
            label: ManagedNameConstants.ManagedTypeLabel,
            category: string.Empty,
            description: string.Empty,
            valueType: typeof(string),
            validateValueCallback: o => !string.IsNullOrWhiteSpace(o as string),
            attributes: TestPropertyAttributes.Hidden,
            owner: typeof(TestCase));

        internal static readonly TestProperty ManagedMethodProperty = TestProperty.Register(
            id: ManagedNameConstants.ManagedMethodPropertyId,
            label: ManagedNameConstants.ManagedMethodLabel,
            category: string.Empty,
            description: string.Empty,
            valueType: typeof(string),
            validateValueCallback: o => !string.IsNullOrWhiteSpace(o as string),
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
        /// The to unit test element.
        /// </summary>
        /// <param name="testCase"> The test case. </param>
        /// <param name="source"> The source. If deployed this is the full path of the source in the deployment directory. </param>
        /// <returns> The converted <see cref="UnitTestElement"/>. </returns>
        internal static UnitTestElement ToUnitTestElement(this TestCase testCase, string source)
        {
            var isAsync = (testCase.GetPropertyValue(Constants.AsyncTestProperty) as bool?) ?? false;
            var testClassName = testCase.GetPropertyValue(Constants.TestClassNameProperty) as string;
            var declaringClassName = testCase.GetPropertyValue(Constants.DeclaringClassNameProperty) as string;

            var fullyQualifiedName = testCase.FullyQualifiedName;

            // Not using Replace because there can be multiple instances of that string.
            var name = fullyQualifiedName.StartsWith($"{testClassName}.")
                ? fullyQualifiedName.Remove(0, $"{testClassName}.".Length)
                : fullyQualifiedName;

            TestMethod testMethod;
            if (testCase.ContainsManagedMethodAndType())
            {
                testMethod = new TestMethod(testCase.GetManagedType(), testCase.GetManagedMethod(), testCase.GetHierarchy(), name, testClassName, source, isAsync);
            }
            else
            {
                testMethod = new TestMethod(name, testClassName, source, isAsync);
            }

            var dataType = (DynamicDataType)testCase.GetPropertyValue(Constants.TestDynamicDataTypeProperty, (int)DynamicDataType.None);
            if (dataType != DynamicDataType.None)
            {
                var data = testCase.GetPropertyValue<string[]>(Constants.TestDynamicDataProperty, null);

                testMethod.DataType = dataType;
                testMethod.SerializedData = data;
            }

            testMethod.DisplayName = testCase.DisplayName;

            if (declaringClassName != null && declaringClassName != testClassName)
            {
                testMethod.DeclaringClassFullName = declaringClassName;
            }

            UnitTestElement testElement = new UnitTestElement(testMethod)
            {
                IsAsync = isAsync,
                TestCategory = testCase.GetPropertyValue(Constants.TestCategoryProperty) as string[],
                Priority = testCase.GetPropertyValue(Constants.PriorityProperty) as int?,
                DisplayName = testCase.DisplayName
            };

            if (testCase.Traits.Any())
            {
                testElement.Traits = testCase.Traits.ToArray();
            }

            var cssIteration = testCase.GetPropertyValue<string>(Constants.CssIterationProperty, null);
            if (!string.IsNullOrWhiteSpace(cssIteration))
            {
                testElement.CssIteration = cssIteration;
            }

            var cssProjectStructure = testCase.GetPropertyValue<string>(Constants.CssProjectStructureProperty, null);
            if (!string.IsNullOrWhiteSpace(cssIteration))
            {
                testElement.CssProjectStructure = cssProjectStructure;
            }

            var description = testCase.GetPropertyValue<string>(Constants.DescriptionProperty, null);
            if (!string.IsNullOrWhiteSpace(description))
            {
                testElement.Description = description;
            }

            var workItemIds = testCase.GetPropertyValue<string[]>(Constants.WorkItemIdsProperty, null);
            if (workItemIds != null && workItemIds.Length > 0)
            {
                testElement.WorkItemIds = workItemIds;
            }

            var deploymentItems = testCase.GetPropertyValue<KeyValuePair<string, string>[]>(Constants.DeploymentItemsProperty, null);
            if (deploymentItems != null && deploymentItems.Length > 0)
            {
                testElement.DeploymentItems = deploymentItems;
            }

            testElement.DoNotParallelize = testCase.GetPropertyValue(Constants.DoNotParallelizeProperty, false);

            return testElement;
        }

        internal static string GetManagedType(this TestCase testCase) => testCase.GetPropertyValue<string>(ManagedTypeProperty, null);

        internal static void SetManagedType(this TestCase testCase, string value) => testCase.SetPropertyValue<string>(ManagedTypeProperty, value);

        internal static string GetManagedMethod(this TestCase testCase) => testCase.GetPropertyValue<string>(ManagedMethodProperty, null);

        internal static void SetManagedMethod(this TestCase testCase, string value) => testCase.SetPropertyValue<string>(ManagedMethodProperty, value);

        internal static bool ContainsManagedMethodAndType(this TestCase testCase) => !string.IsNullOrWhiteSpace(testCase.GetManagedMethod()) && !string.IsNullOrWhiteSpace(testCase.GetManagedType());

        internal static string[] GetHierarchy(this TestCase testCase) => testCase.GetPropertyValue<string[]>(HierarchyProperty, null);

        internal static void SetHierarchy(this TestCase testCase, params string[] value) => testCase.SetPropertyValue<string[]>(HierarchyProperty, value);
    }
}
