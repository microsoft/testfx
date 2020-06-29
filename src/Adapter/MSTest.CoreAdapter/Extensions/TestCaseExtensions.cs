// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Extensions
{
    using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;
    using Microsoft.VisualStudio.TestPlatform.ObjectModel;
    using Constants = Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Constants;

    /// <summary>
    /// Extension Methods for TestCase Class
    /// </summary>
    internal static class TestCaseExtensions
    {
        /// <summary>
        /// The test name
        /// </summary>
        /// <param name="testCase"> The test case. </param>
        /// <param name="testClassName"> The test case's class name. </param>
        /// <returns> The test name, without the class name, if provided. </returns>
        internal static string GetTestName(this TestCase testCase, string testClassName)
        {
            var fullyQualifiedName = testCase.FullyQualifiedName;

            // Not using Replace because there can be multiple instances of that string.
            var name = fullyQualifiedName.StartsWith($"{testClassName}.")
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
            var isAsync = (testCase.GetPropertyValue(Constants.AsyncTestProperty) as bool?) ?? false;
            var testClassName = testCase.GetPropertyValue(Constants.TestClassNameProperty) as string;
            var declaringClassName = testCase.GetPropertyValue(Constants.DeclaringClassNameProperty) as string;
            var name = testCase.GetTestName(testClassName);

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
    }
}
