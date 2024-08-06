// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Globalization;

using Microsoft.Testing.Extensions.VSTestBridge.CommandLine;
using Microsoft.Testing.Platform;
using Microsoft.Testing.Platform.CommandLine;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Adapter;

namespace Microsoft.Testing.Extensions.VSTestBridge.ObjectModel;

internal abstract class ContextAdapterBase
{
    public ContextAdapterBase(ICommandLineOptions commandLineOptions)
    {
        if (commandLineOptions.TryGetOptionArgumentList(
            TestCaseFilterCommandLineOptionsProvider.TestCaseFilterOptionName,
            out string[]? filterExpressions)
            && filterExpressions is not null
            && filterExpressions.Length == 1)
        {
            FilterExpressionWrapper = new(filterExpressions[0]);
        }
    }

    protected FilterExpressionWrapper? FilterExpressionWrapper { get; set; }

    // NOTE: Implementation is borrowed from VSTest
    // MSTest relies on this method existing and access it through reflection: https://github.com/microsoft/testfx/blob/main/src/Adapter/MSTest.TestAdapter/TestMethodFilter.cs#L115
    public ITestCaseFilterExpression? GetTestCaseFilter(
        IEnumerable<string>? supportedProperties,
        Func<string, TestProperty?> propertyProvider)
    {
        if (FilterExpressionWrapper is null)
        {
            return null;
        }

        if (!RoslynString.IsNullOrEmpty(FilterExpressionWrapper.ParseError))
        {
            throw new TestPlatformFormatException(FilterExpressionWrapper.ParseError, FilterExpressionWrapper.FilterString);
        }

        var adapterSpecificTestCaseFilter = new TestCaseFilterExpression(FilterExpressionWrapper);
        string[]? invalidProperties = adapterSpecificTestCaseFilter.ValidForProperties(supportedProperties, propertyProvider);

        if (invalidProperties != null)
        {
            string validPropertiesString = supportedProperties == null
                ? string.Empty
                : string.Join(", ", supportedProperties);
            string errorMessage = string.Format(
                CultureInfo.CurrentCulture,
                "No tests matched the filter because it contains one or more properties that are not valid ({0}). Specify filter expression containing valid properties ({1}).",
                string.Join(", ", invalidProperties),
                validPropertiesString);

            // For unsupported property don’t throw exception, just log the message. Later it is going to handle properly with TestCaseFilterExpression.MatchTestCase().
            EqtTrace.Info(errorMessage);
        }

        return adapterSpecificTestCaseFilter;
    }
}
