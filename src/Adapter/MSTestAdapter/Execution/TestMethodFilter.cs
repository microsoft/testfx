// Copyright (c) Microsoft. All rights reserved.

namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution
{
    using System.Diagnostics;
    using System;
    using System.Collections.Generic;
    using System.Linq;

    using Microsoft.VisualStudio.TestPlatform.ObjectModel;
    using Microsoft.VisualStudio.TestPlatform.ObjectModel.Adapter;
    using Microsoft.VisualStudio.TestPlatform.ObjectModel.Logging;

    using Constants = Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Constants;

    internal class TestMethodFilter
    {
        /// <summary>
        /// Supported properties for filtering
        /// </summary>
        private readonly Dictionary<string, TestProperty> supportedProperties;

        internal TestMethodFilter()
        {
            this.supportedProperties = new Dictionary<string, TestProperty>(StringComparer.OrdinalIgnoreCase)
            {
                [Constants.TestCategoryProperty.Label] = Constants.TestCategoryProperty,
                [Constants.PriorityProperty.Label] = Constants.PriorityProperty,
                [TestCaseProperties.FullyQualifiedName.Label] = TestCaseProperties.FullyQualifiedName,
                [TestCaseProperties.DisplayName.Label] = TestCaseProperties.DisplayName,
                [Constants.TestClassNameProperty.Label] = Constants.TestClassNameProperty
            };
        }

        /// <summary>
        /// Returns ITestCaseFilterExpression for TestProperties supported by adapter.
        /// </summary>
        internal ITestCaseFilterExpression GetFilterExpression(IRunContext runContext, IMessageLogger testExecutionRecorder, out bool filterHasError)
        {
            filterHasError = false;
            ITestCaseFilterExpression filter = null;
            if (null != runContext)
            {
                try
                {
                    filter = runContext.GetTestCaseFilter(this.supportedProperties.Keys, this.PropertyProvider);
                }
                catch (TestPlatformFormatException ex)
                {
                    filterHasError = true;
                    testExecutionRecorder.SendMessage(TestMessageLevel.Error, ex.Message);
                }
            }
            return filter;
        }

        /// <summary>
        /// Provides TestProperty for property name 'propertyName' as used in filter.
        /// </summary>
        internal TestProperty PropertyProvider(string propertyName)
        {
            TestProperty testProperty;
            this.supportedProperties.TryGetValue(propertyName, out testProperty);
            Debug.Assert(null != testProperty, "Invalid property queried");
            return testProperty;
        }

        /// <summary>
        /// Provides value of TestProperty corresponding to property name 'propertyName' as used in filter.
        /// </summary>
        internal object PropertyValueProvider(TestCase currentTest, string propertyName)
        {
            if (currentTest != null && propertyName != null)
            {
                TestProperty testProperty;
                if (this.supportedProperties.TryGetValue(propertyName, out testProperty))
                {
                    // Test case might not have defined this property. In that case GetPropertyValue()
                    // would return default value. For filtering, if property is not defined return null.
                    if (currentTest.Properties.Contains(testProperty))
                    {
                        return currentTest.GetPropertyValue(testProperty);
                    }
                }
            }
            return null;
        }
    }
}