// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.VSTestBridge.CommandLine;
using Microsoft.Testing.Platform;
using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Requests;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Adapter;

namespace Microsoft.Testing.Extensions.VSTestBridge.ObjectModel;

internal abstract class ContextAdapterBase
{
    protected ContextAdapterBase(ICommandLineOptions commandLineOptions, ITestExecutionFilter filter)
    {
        if (commandLineOptions.TryGetOptionArgumentList(
            TestCaseFilterCommandLineOptionsProvider.TestCaseFilterOptionName,
            out string[]? filterExpressions)
            && filterExpressions is not null
            && filterExpressions.Length == 1)
        {
            FilterExpressionWrapper = new(filterExpressions[0]);
        }

        switch (filter)
        {
            case TestNodeUidListFilter uidListFilter:
                FilterExpressionWrapper = new(CreateFilter(uidListFilter.TestNodeUids));
                break;

            case TreeNodeFilter treeNodeFilter:
                TreeNodeFilter = treeNodeFilter;
                break;
        }
    }

    protected FilterExpressionWrapper? FilterExpressionWrapper { get; set; }

    protected TreeNodeFilter? TreeNodeFilter { get; set; }

    // NOTE: Implementation is borrowed from VSTest
    // MSTest relies on this method existing and access it through reflection: https://github.com/microsoft/testfx/blob/main/src/Adapter/MSTest.TestAdapter/TestMethodFilter.cs#L115
    public ITestCaseFilterExpression? GetTestCaseFilter(
        IEnumerable<string>? supportedProperties,
        Func<string, TestProperty?> propertyProvider)
    {
        if (TreeNodeFilter is not null)
        {
            return new TreeNodeFilterExpression(TreeNodeFilter, supportedProperties, propertyProvider);
        }

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

    // We use heuristic to understand if the filter should be a TestCaseId or FullyQualifiedName.
    // We know that in VSTest TestCaseId is a GUID and FullyQualifiedName is a string.
    private static string CreateFilter(TestNodeUid[] testNodesUid)
    {
        StringBuilder filter = new();

        for (int i = 0; i < testNodesUid.Length; i++)
        {
            if (Guid.TryParse(testNodesUid[i].Value, out Guid guid))
            {
                filter.Append("Id=");
                filter.Append(guid.ToString());
            }
            else
            {
                TestNodeUid currentTestNodeUid = testNodesUid[i];
                filter.Append("FullyQualifiedName=");
                for (int k = 0; k < currentTestNodeUid.Value.Length; k++)
                {
                    char currentChar = currentTestNodeUid.Value[k];
                    switch (currentChar)
                    {
                        case '\\':
                        case '(':
                        case ')':
                        case '&':
                        case '|':
                        case '=':
                        case '!':
                        case '~':
                            // If the symbol is not escaped, add an escape character.
                            if (i - 1 < 0 || currentTestNodeUid.Value[k - 1] != '\\')
                            {
                                filter.Append('\\');
                            }

                            filter.Append(currentChar);
                            break;

                        default:
                            filter.Append(currentChar);
                            break;
                    }
                }
            }

            if (i != testNodesUid.Length - 1)
            {
                filter.Append('|');
            }
        }

        return filter.ToString();
    }
}
