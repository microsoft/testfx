// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Extensions
{
    using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;
    using Microsoft.VisualStudio.TestPlatform.ObjectModel;

    using Constants = Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Constants;
    using ManagedNameUtilities = Microsoft.TestPlatform.AdapterUtilities.ManagedNameUtilities;

    /// <summary>
    /// Extension Methods for TestCase Class
    /// </summary>
    internal static class TestCaseExtensions
    {
        internal static readonly TestProperty ManagedTypeProperty = TestProperty.Register(
            id: ManagedNameUtilities.Contants.ManagedTypePropertyId,
            label: ManagedNameUtilities.Contants.ManagedTypeLabel,
            category: string.Empty,
            description: string.Empty,
            valueType: typeof(string),
            validateValueCallback: o => !string.IsNullOrWhiteSpace(o as string),
            attributes: TestPropertyAttributes.Hidden,
            owner: typeof(TestCase));

        internal static readonly TestProperty ManagedMethodProperty = TestProperty.Register(
            id: ManagedNameUtilities.Contants.ManagedMethodPropertyId,
            label: ManagedNameUtilities.Contants.ManagedMethodLabel,
            category: string.Empty,
            description: string.Empty,
            valueType: typeof(string),
            validateValueCallback: o => !string.IsNullOrWhiteSpace(o as string),
            attributes: TestPropertyAttributes.Hidden,
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

            TestMethod testMethod = new TestMethod(name, testClassName, source, isAsync);

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

            return testElement;
        }

        internal static string GetManagedType(this TestCase testCase) => testCase.GetPropertyValue<string>(ManagedTypeProperty, null);

        internal static void SetManagedType(this TestCase testCase, string value) => testCase.SetPropertyValue<string>(ManagedTypeProperty, value);

        internal static string GetManagedMethod(this TestCase testCase) => testCase.GetPropertyValue<string>(ManagedMethodProperty, null);

        internal static void SetManagedMethod(this TestCase testCase, string value) => testCase.SetPropertyValue<string>(ManagedMethodProperty, value);

        internal static bool ContainsManagedMethodAndType(this TestCase testCase) => !string.IsNullOrWhiteSpace(testCase.GetManagedMethod()) && !string.IsNullOrWhiteSpace(testCase.GetManagedType());
    }
}
