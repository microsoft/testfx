// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Adapter;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter;

internal sealed class TestMethodFilter
{
    /// <summary>
    /// Supported properties for filtering.
    /// </summary>
    private readonly Dictionary<string, TestProperty> _supportedProperties;

    internal TestMethodFilter() => _supportedProperties = new Dictionary<string, TestProperty>(StringComparer.OrdinalIgnoreCase)
    {
        [EngineConstants.TestCategoryProperty.Label] = EngineConstants.TestCategoryProperty,
        [EngineConstants.PriorityProperty.Label] = EngineConstants.PriorityProperty,
        [TestCaseProperties.FullyQualifiedName.Label] = TestCaseProperties.FullyQualifiedName,
        [TestCaseProperties.DisplayName.Label] = TestCaseProperties.DisplayName,
        [TestCaseProperties.Id.Label] = TestCaseProperties.Id,
        [EngineConstants.TestClassNameProperty.Label] = EngineConstants.TestClassNameProperty,
    };

    /// <summary>
    /// Returns ITestCaseFilterExpression for TestProperties supported by adapter.
    /// </summary>
    /// <param name="context">The current context of the run.</param>
    /// <param name="logger">Logger used to report diagnostic messages (for example, filter parse errors).</param>
    /// <param name="filterHasError">Indicates that the filter is unsupported/has an error.</param>
    /// <returns>A filter expression.</returns>
    internal ITestCaseFilterExpression? GetFilterExpression(IDiscoveryContext? context, IAdapterMessageLogger logger, out bool filterHasError)
    {
        filterHasError = false;
        if (context == null)
        {
            return null;
        }

        ITestCaseFilterExpression? filter = null;
        try
        {
            filter = context is IRunContext runContext
                ? GetTestCaseFilterFromRunContext(runContext)
                : GetTestCaseFilterFromDiscoveryContext(context, logger);
        }
        catch (TestPlatformFormatException ex)
        {
            filterHasError = true;
            logger.SendMessage(MessageLevel.Error, ex.Message);
        }

        return filter;
    }

    /// <summary>
    /// Builds a platform-agnostic <see cref="ITestElementFilter"/> for the properties supported by the adapter.
    /// </summary>
    /// <param name="context">The current context of the run.</param>
    /// <param name="logger">Logger used to report diagnostic messages (for example, filter parse errors).</param>
    /// <param name="filterHasError">Indicates that the filter is unsupported/has an error.</param>
    /// <returns>
    /// A filter that evaluates <see cref="UnitTestElement"/> instances, or <see langword="null"/> when no
    /// filter applies (in which case every element is included).
    /// </returns>
    internal ITestElementFilter? GetTestElementFilter(IDiscoveryContext? context, IAdapterMessageLogger logger, out bool filterHasError)
    {
        ITestCaseFilterExpression? filterExpression = GetFilterExpression(context, logger, out filterHasError);
        return filterExpression is null
            ? null
            : new TestElementFilter(this, filterExpression);
    }

    /// <summary>
    /// Provides TestProperty for property name 'propertyName' as used in filter.
    /// </summary>
    /// <param name="propertyName">The property name.</param>
    /// <returns>a TestProperty instance.</returns>
    internal TestProperty PropertyProvider(string propertyName)
    {
        _supportedProperties.TryGetValue(propertyName, out TestProperty? testProperty);
        DebugEx.Assert(testProperty != null, "Invalid property queried");
        return testProperty;
    }

    /// <summary>
    /// Provides value of TestProperty corresponding to property name 'propertyName' as used in filter.
    /// </summary>
    /// <param name="currentTest">The current test case.</param>
    /// <param name="propertyName">Property name.</param>
    /// <returns>The property value.</returns>
    internal object? PropertyValueProvider(TestCase? currentTest, string? propertyName)
    {
        if (currentTest != null && propertyName != null)
        {
            if (_supportedProperties.TryGetValue(propertyName, out TestProperty? testProperty))
            {
                // Test case might not have defined this property. In that case GetPropertyValue()
                // would return default value. For filtering, if property is not defined return null.
                if (currentTest.Properties.Contains(testProperty))
                {
                    return currentTest.GetPropertyValue(testProperty);
                }
            }
            else
            {
                // Everything that it's not a supported property we use traits
                foreach (Trait trait in currentTest.Traits)
                {
                    if (trait.Name.Equals(propertyName, StringComparison.OrdinalIgnoreCase))
                    {
                        return trait.Value;
                    }
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Gets filter expression from run context.
    /// </summary>
    /// <param name="context">Run context.</param>
    /// <returns>Filter expression.</returns>
    private ITestCaseFilterExpression? GetTestCaseFilterFromRunContext(IRunContext context) => context.GetTestCaseFilter(_supportedProperties.Keys, PropertyProvider);

    /// <summary>
    /// Gets filter expression from discovery context.
    /// </summary>
    /// <param name="context">Discovery context.</param>
    /// <param name="logger">The logger to log exception messages too.</param>
    /// <returns>Filter expression.</returns>
    [UnconditionalSuppressMessage("ReflectionAnalysis", "IL2072:'target parameter' argument does not satisfy 'DynamicallyAccessedMembersAttribute' in call to target method.", Justification = "GetTestCaseFilter is part of the VSTest discovery contract on the concrete DiscoveryContext type; the runtime guarantees the method exists on supported hosts.")]
    private ITestCaseFilterExpression? GetTestCaseFilterFromDiscoveryContext(IDiscoveryContext context, IAdapterMessageLogger logger)
    {
        try
        {
            // GetTestCaseFilter is present in DiscoveryContext but not in IDiscoveryContext interface.
            MethodInfo? methodGetTestCaseFilter = context.GetType().GetRuntimeMethod("GetTestCaseFilter", [typeof(IEnumerable<string>), typeof(Func<string, TestProperty>)]);
            return (ITestCaseFilterExpression?)methodGetTestCaseFilter?.Invoke(context, [_supportedProperties.Keys, (Func<string, TestProperty>)PropertyProvider]);
        }
        catch (TargetInvocationException ex)
        {
            // In case of UWP .Net Native Tool Chain compilation. Invoking methods via Reflection doesn't work, hence discovery always fails.
            // Hence throwing exception only if it is of type TargetInvocationException(i.e. Method got invoked but something went wrong in GetTestCaseFilter Method)
            throw ex.InnerException!;
        }
        catch (Exception ex)
        {
            logger.SendMessage(MessageLevel.Warning, ex.Message);
        }

        return null;
    }

    /// <summary>
    /// Platform-agnostic filter that evaluates a <see cref="UnitTestElement"/> against a VSTest
    /// <see cref="ITestCaseFilterExpression"/>. This is the single point where the neutral element model is
    /// translated to the VSTest test case in order to reuse the host's filter matching.
    /// </summary>
    private sealed class TestElementFilter : ITestElementFilter
    {
        private readonly TestMethodFilter _testMethodFilter;
        private readonly ITestCaseFilterExpression _filterExpression;

        public TestElementFilter(TestMethodFilter testMethodFilter, ITestCaseFilterExpression filterExpression)
        {
            _testMethodFilter = testMethodFilter;
            _filterExpression = filterExpression;
        }

        public bool Matches(UnitTestElement testElement)
        {
            // Reuse the element's cached host TestCase so that a matched test is not converted a second time
            // when it is subsequently reported to the discovery sink.
            TestCase testCase = testElement.GetOrCreateHostTestCase();
            return _filterExpression.MatchTestCase(testCase, propertyName => _testMethodFilter.PropertyValueProvider(testCase, propertyName));
        }
    }
}
