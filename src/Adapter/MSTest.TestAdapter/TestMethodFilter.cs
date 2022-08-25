// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Reflection;
    using Microsoft.VisualStudio.TestPlatform.ObjectModel;
    using Microsoft.VisualStudio.TestPlatform.ObjectModel.Adapter;
    using Microsoft.VisualStudio.TestPlatform.ObjectModel.Logging;

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
        /// <param name="context">The current context of the run.</param>
        /// <param name="logger">Handler to report test messages/start/end and results.</param>
        /// <param name="filterHasError">Indicates that the filter is unsupported/has an error.</param>
        /// <returns>A filter expression.</returns>
        internal ITestCaseFilterExpression GetFilterExpression(IDiscoveryContext context, IMessageLogger logger, out bool filterHasError)
        {
            filterHasError = false;
            ITestCaseFilterExpression filter = null;
            if (context != null)
            {
                try
                {
                    filter = (context is IRunContext) ? this.GetTestCaseFilterFromRunContext(context as IRunContext) : this.GetTestCaseFilterFromDiscoveryContext(context, logger);
                }
                catch (TestPlatformFormatException ex)
                {
                    filterHasError = true;
                    logger.SendMessage(TestMessageLevel.Error, ex.Message);
                }
            }

            return filter;
        }

        /// <summary>
        /// Provides TestProperty for property name 'propertyName' as used in filter.
        /// </summary>
        /// <param name="propertyName">The property name.</param>
        /// <returns>a TestProperty instance.</returns>
        internal TestProperty PropertyProvider(string propertyName)
        {
            this.supportedProperties.TryGetValue(propertyName, out var testProperty);
            Debug.Assert(testProperty != null, "Invalid property queried");
            return testProperty;
        }

        /// <summary>
        /// Provides value of TestProperty corresponding to property name 'propertyName' as used in filter.
        /// </summary>
        /// <param name="currentTest">The current test case.</param>
        /// <param name="propertyName">Property name.</param>
        /// <returns>The property value.</returns>
        internal object PropertyValueProvider(TestCase currentTest, string propertyName)
        {
            if (currentTest != null && propertyName != null)
            {
                if (this.supportedProperties.TryGetValue(propertyName, out var testProperty))
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

        /// <summary>
        /// Gets filter expression from run context.
        /// </summary>
        /// <param name="context">Run context</param>
        /// <returns>Filter expression.</returns>
        private ITestCaseFilterExpression GetTestCaseFilterFromRunContext(IRunContext context)
        {
            return context.GetTestCaseFilter(this.supportedProperties.Keys, this.PropertyProvider);
        }

        /// <summary>
        /// Gets filter expression from discovery context.
        /// </summary>
        /// <param name="context">Discovery context</param>
        /// <param name="logger">The logger to log exception messages too.</param>
        /// <returns>Filter expression.</returns>
        private ITestCaseFilterExpression GetTestCaseFilterFromDiscoveryContext(IDiscoveryContext context, IMessageLogger logger)
        {
            try
            {
                // GetTestCaseFilter is present in DiscoveryContext but not in IDiscoveryContext interface.
                MethodInfo methodGetTestCaseFilter = context.GetType().GetRuntimeMethod("GetTestCaseFilter", new[] { typeof(IEnumerable<string>), typeof(Func<string, TestProperty>) });
                return (ITestCaseFilterExpression)methodGetTestCaseFilter?.Invoke(context, new object[] { this.supportedProperties.Keys, (Func<string, TestProperty>)this.PropertyProvider });
            }
            catch (Exception ex)
            {
                // In case of UWP .Net Native Tool Chain compilation. Invoking methods via Reflection doesn't work, hence discovery always fails.
                // Hence throwing exception only if it is of type TargetInvocationException(i.e. Method got invoked but something went wrong in GetTestCaseFilter Method)
                if (ex is TargetInvocationException)
                {
                    throw ex.InnerException;
                }

                logger.SendMessage(TestMessageLevel.Warning, ex.Message);
            }

            return null;
        }
    }
}