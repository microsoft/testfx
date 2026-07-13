// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if !WINDOWS_UWP
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Extensions;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Adapter;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.TestingPlatformAdapter;

/// <summary>
/// A filter expression that can be evaluated directly against a <see cref="UnitTestElement"/> using a
/// property-value provider, without materializing (or reading properties off) a vstest <c>TestCase</c>.
/// </summary>
/// <remarks>
/// This is what lets the native Microsoft.Testing.Platform filter path avoid the vstest <c>TestObject</c>
/// property converters (<c>TypeDescriptor.GetConverter</c>), which are trim/AOT-unsafe (IL2026/IL2072). See
/// https://github.com/microsoft/testfx/issues/9769.
/// </remarks>
internal interface IUnitTestElementFilterExpression
{
    /// <summary>
    /// Evaluates the filter using the supplied property-value provider, which sources values straight from the
    /// neutral element model rather than from a vstest <c>TestCase</c>.
    /// </summary>
    bool MatchTestElement(Func<string, object?> propertyValueProvider);
}

/// <summary>
/// The native Microsoft.Testing.Platform (MTP) <see cref="ITestElementFilterProvider"/>. It builds the MTP
/// filter expression from a <see cref="MSTestFilterContextBase"/> and produces an <see cref="ITestElementFilter"/>
/// that evaluates the filter straight from the neutral <see cref="UnitTestElement"/> model.
/// </summary>
/// <remarks>
/// This is deliberately a separate type from the VSTest <c>TestElementFilterProvider</c> / <c>TestMethodFilter</c>
/// so the MTP reachability graph never includes the VSTest filter's <c>UnitTestElement</c>-to-<c>TestCase</c>
/// materialization (<c>GetOrCreateHostTestCase</c> / <c>ToTestCase</c>) nor the <c>AdapterTestProperties</c>
/// registrations. As a result the trimmer can drop those from a native-AOT MTP app, and neither the vstest
/// <c>TestObject</c> property converters nor the custom <c>TypeConverter</c>s remain reachable on this path.
/// See https://github.com/microsoft/testfx/issues/9769.
/// </remarks>
[SuppressMessage("ApiDesign", "RS0030:Do not use banned APIs", Justification = "We can use MTP from this folder")]
internal sealed class MtpTestElementFilterProvider : ITestElementFilterProvider
{
    // The filter grammar addresses these built-in vstest property names plus MSTest's ClassName / TestCategory /
    // Priority. They are hard-coded (rather than read from the vstest TestCaseProperties / AdapterTestProperties
    // TestProperty registrations) precisely so this path does not root those registrations — which would pull in
    // the trim/AOT-unsafe custom TypeConverters. Everything else is treated as a trait.
    // NOTE: the vstest label for the test's display name is "Name" (that is TestCaseProperties.DisplayName.Label),
    // not "DisplayName" — matching the VSTest filter property that users write as 'Name=...'.
    private const string FullyQualifiedNameLabel = "FullyQualifiedName";
    private const string DisplayNameLabel = "Name";
    private const string IdLabel = "Id";
    private const string ClassNameLabel = "ClassName";
    private const string TestCategoryLabel = "TestCategory";
    private const string PriorityLabel = "Priority";

    private static readonly string[] SupportedPropertyNames =
    [
        FullyQualifiedNameLabel,
        DisplayNameLabel,
        IdLabel,
        ClassNameLabel,
        TestCategoryLabel,
        PriorityLabel,
    ];

    private readonly MSTestFilterContextBase _filterContext;

    public MtpTestElementFilterProvider(MSTestFilterContextBase filterContext)
        => _filterContext = filterContext;

    public ITestElementFilter? GetTestElementFilter(IAdapterMessageLogger logger, out bool filterHasError)
    {
        filterHasError = false;
        try
        {
            // GetTestCaseFilter ignores the supported-properties/property-provider arguments on the MTP path; the
            // filter is parsed from the command-line / runsettings filter string in the context's constructor.
            ITestCaseFilterExpression? filterExpression = _filterContext.GetTestCaseFilter(SupportedPropertyNames, static _ => null);
            return filterExpression is IUnitTestElementFilterExpression elementFilterExpression
                ? new MtpTestElementFilter(elementFilterExpression)
                : null;
        }
        catch (TestPlatformFormatException ex)
        {
            filterHasError = true;
            logger.SendMessage(MessageLevel.Error, ex.Message);
            return null;
        }
    }

    /// <summary>
    /// Evaluates an <see cref="IUnitTestElementFilterExpression"/> against a <see cref="UnitTestElement"/> using an
    /// element-sourced property provider — never touching a vstest <c>TestCase</c>.
    /// </summary>
    private sealed class MtpTestElementFilter : ITestElementFilter
    {
        private readonly IUnitTestElementFilterExpression _filterExpression;

        public MtpTestElementFilter(IUnitTestElementFilterExpression filterExpression)
            => _filterExpression = filterExpression;

        public bool Matches(UnitTestElement testElement)
            => _filterExpression.MatchTestElement(propertyName => GetPropertyValue(testElement, propertyName));

        /// <summary>
        /// Returns the value the filter's supported properties (or traits) carry for <paramref name="element"/>,
        /// reproducing — field-for-field — what <c>UnitTestElementExtensions.ToTestCase</c> stores on a vstest test
        /// case, so filtering behaves identically to the VSTest path without materializing one.
        /// </summary>
        private static object? GetPropertyValue(UnitTestElement element, string? propertyName)
        {
            if (propertyName is null)
            {
                return null;
            }

            TestMethod testMethod = element.TestMethod;

            if (propertyName.Equals(FullyQualifiedNameLabel, StringComparison.OrdinalIgnoreCase))
            {
                return $"{testMethod.FullClassName}.{testMethod.Name}";
            }

            if (propertyName.Equals(DisplayNameLabel, StringComparison.OrdinalIgnoreCase))
            {
                return testMethod.DisplayName;
            }

            if (propertyName.Equals(IdLabel, StringComparison.OrdinalIgnoreCase))
            {
                return element.GetTestId();
            }

            if (propertyName.Equals(ClassNameLabel, StringComparison.OrdinalIgnoreCase))
            {
                return testMethod.FullClassName;
            }

            if (propertyName.Equals(TestCategoryLabel, StringComparison.OrdinalIgnoreCase))
            {
                // ToTestCase only sets the category property when at least one category is present.
                return element.TestCategory is { Length: > 0 } categories ? categories : null;
            }

            if (propertyName.Equals(PriorityLabel, StringComparison.OrdinalIgnoreCase))
            {
                // element.Priority is already null when no priority is present.
                return element.Priority;
            }

            // Everything that is not a supported property is matched against traits. TestTrait is a value
            // type, so a LINQ FirstOrDefault(...)?.Value is not available here; scan for the first match.
            if (element.Traits is { Length: > 0 } traits)
            {
                foreach (TestTrait trait in traits)
                {
                    if (trait.Name.Equals(propertyName, StringComparison.OrdinalIgnoreCase))
                    {
                        return trait.Value;
                    }
                }
            }

            return null;
        }
    }
}
#endif
