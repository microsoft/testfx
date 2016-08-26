// Copyright (c) Microsoft. All rights reserved.

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
        /// The to unit test element.
        /// </summary>
        /// <param name="testCase"> The test case. </param>
        /// <param name="source"> The source. If deployed this is the full path of the source in the deployment directory. </param>
        /// <returns> The converted <see cref="UnitTestElement"/>. </returns>
        internal static UnitTestElement ToUnitTestElement(this TestCase testCase, string source)
        {
            var isAsync = (testCase.GetPropertyValue(Constants.AsyncTestProperty) as bool?) ?? false;
            var isEnabled = (testCase.GetPropertyValue(Constants.TestEnabledProperty) as bool?) ?? true;
            var testClassName = testCase.GetPropertyValue(Constants.TestClassNameProperty) as string;

            TestMethod testMethod = new TestMethod(testCase.DisplayName, testClassName, source, isAsync);
            
            UnitTestElement testElement = new UnitTestElement(testMethod)
                                        {
                                            Ignored = !isEnabled,
                                            IsAsync = isAsync,
                                            TestCategory = testCase.GetPropertyValue(Constants.TestCategoryProperty) as string[],
                                            Priority = testCase.GetPropertyValue(Constants.PriorityProperty) as int?
                                        };

            return testElement;
        }
    }
}
