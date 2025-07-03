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
    protected ContextAdapterBase(ICommandLineOptions commandLineOptions, IRunSettings runSettings, ITestExecutionFilter filter)
    {
        RunSettings = runSettings;

        RoslynDebug.Assert(runSettings.SettingsXml is not null);

        string? filterFromRunsettings = XDocument.Parse(runSettings.SettingsXml).Element("RunSettings")?.Element("RunConfiguration")?.Element("TestCaseFilter")?.Value;
        string? filterFromCommandLineOption = null;
        if (commandLineOptions.TryGetOptionArgumentList(
            TestCaseFilterCommandLineOptionsProvider.TestCaseFilterOptionName,
            out string[]? filterExpressions)
            && filterExpressions is not null
            && filterExpressions.Length == 1)
        {
            filterFromCommandLineOption = filterExpressions[0];
        }

        HandleFilter(filter, filterFromRunsettings, filterFromCommandLineOption);
    }

    public IRunSettings? RunSettings { get; }

    private FilterExpressionWrapper? FilterExpressionWrapper { get; set; }

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

            // For unsupported property don’t throw exception, just log the message. Later it is going to handle properly with TestCaseFilterExpression.MatchTestCase().
            EqtTrace.Info($"No tests matched the filter because it contains one or more properties that are not valid ({string.Join(", ", invalidProperties)}). Specify filter expression containing valid properties ({validPropertiesString}).");
        }

        return adapterSpecificTestCaseFilter;
    }

    private void HandleFilter(ITestExecutionFilter? filter, string? filterFromRunsettings, string? filterFromCommandLineOption)
    {
        // No filters at all, we can return immediately as there is nothing to do.
        if (filter is null or NopFilter
            && filterFromRunsettings is null
            && filterFromCommandLineOption is null)
        {
            return;
        }

        var filterBuilder = new StringBuilder();

        AppendFilter(filterFromRunsettings, filterBuilder);
        AppendFilter(filterFromCommandLineOption, filterBuilder);

        if (filter is TestNodeUidListFilter testNodeUidListFilter)
        {
            StartFilter(filterBuilder);
            BuildFilter(testNodeUidListFilter.TestNodeUids, filterBuilder);
            EndFilter(filterBuilder);
        }

        if (filterBuilder.Length > 0)
        {
            FilterExpressionWrapper = new(filterBuilder.ToString());
        }

        // Local functions
        static void AppendFilter(string? filter, StringBuilder builder)
        {
            if (filter is null)
            {
                return;
            }

            StartFilter(builder);
            builder.Append(filter);
            EndFilter(builder);
        }

        static void StartFilter(StringBuilder builder)
        {
            if (builder.Length > 0)
            {
                builder.Append(" & (");
            }
            else
            {
                builder.Append('(');
            }
        }

        static void EndFilter(StringBuilder builder)
            => builder.Append(')');
    }

    // We use heuristic to understand if the filter should be a TestCaseId or FullyQualifiedName.
    // We know that in VSTest TestCaseId is a GUID and FullyQualifiedName is a string.
    private static void BuildFilter(TestNodeUid[] testNodesUid, StringBuilder filter)
    {
        for (int i = 0; i < testNodesUid.Length; i++)
        {
            if (i > 0)
            {
                filter.Append('|');
            }

            if (Guid.TryParse(testNodesUid[i].Value, out Guid guid))
            {
                filter.Append("Id=");
                filter.Append(guid.ToString());
                continue;
            }

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
    }
}
