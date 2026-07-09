// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if !WINDOWS_UWP
using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Requests;
using Microsoft.VisualStudio.TestPlatform.Common.Filtering;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Adapter;

namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.TestingPlatformAdapter;

/// <summary>
/// A native Microsoft.Testing.Platform (MTP) filter context for MSTest. It builds a VSTest
/// <see cref="ITestCaseFilterExpression"/> (which MSTest's <c>TestMethodFilter</c> evaluates over
/// <c>UnitTestElement</c>s) directly from the MTP <see cref="ITestExecutionFilter"/>, the <c>--filter</c>
/// command-line option and the runsettings <c>&lt;TestCaseFilter&gt;</c> — without going through the VSTest bridge's
/// context adapters.
/// </summary>
/// <remarks>
/// This mirrors, for the MSTest native path, the former bridge context-adapter behavior
/// (<c>ContextAdapterBase</c> / <c>RunContextAdapter</c> / <c>DiscoveryContextAdapter</c>). The
/// filter-expression parsing itself reuses the VSTest <c>Microsoft.TestPlatform.Filter.Source</c> package.
/// </remarks>
[SuppressMessage("ApiDesign", "RS0030:Do not use banned APIs", Justification = "We can use MTP from this folder")]
internal abstract class MSTestFilterContextBase
{
    // References the native --filter option provider's name.
    private const string TestCaseFilterOptionName = MSTestTestCaseFilterCommandLineOptionsProvider.TestCaseFilterOptionName;

    protected MSTestFilterContextBase(ICommandLineOptions commandLineOptions, IRunSettings runSettings, ITestExecutionFilter filter)
    {
        RunSettings = runSettings;

        string? filterFromRunsettings = runSettings.SettingsXml is null
            ? null
            : XDocument.Parse(runSettings.SettingsXml).Element("RunSettings")?.Element("RunConfiguration")?.Element("TestCaseFilter")?.Value;

        string? filterFromCommandLineOption = null;
        if (commandLineOptions.TryGetOptionArgumentList(TestCaseFilterOptionName, out string[]? filterExpressions)
            && filterExpressions is not null
            && filterExpressions.Length == 1)
        {
            filterFromCommandLineOption = filterExpressions[0];
        }

        HandleFilter(filter, filterFromRunsettings, filterFromCommandLineOption);
    }

    public IRunSettings? RunSettings { get; }

    private FilterExpressionWrapper? FilterExpressionWrapper { get; set; }

    private sealed class MSTestFilterExpression : ITestCaseFilterExpression
    {
        private readonly TestCaseFilterExpression _testCaseFilterExpression;

        public MSTestFilterExpression(TestCaseFilterExpression testCaseFilterExpression)
            => _testCaseFilterExpression = testCaseFilterExpression;

        public string TestCaseFilterValue
            => _testCaseFilterExpression.TestCaseFilterValue;

        public bool MatchTestCase(TestCase testCase, Func<string, object?> propertyValueProvider)
            => _testCaseFilterExpression.MatchTestCase(propertyValueProvider);
    }

    // NOTE: MSTest.TestAdapter's TestMethodFilter accesses this method via reflection on the discovery context
    // (see GetTestCaseFilterFromDiscoveryContext) and directly on the run context.
#pragma warning disable IDE0060 // Remove unused parameter - kept for the VSTest GetTestCaseFilter contract shape.
    public ITestCaseFilterExpression? GetTestCaseFilter(
        IEnumerable<string>? supportedProperties,
        Func<string, TestProperty?> propertyProvider)
#pragma warning restore IDE0060
        => FilterExpressionWrapper is null
            ? null
            : !string.IsNullOrEmpty(FilterExpressionWrapper.ParseError)
                ? throw new TestPlatformFormatException(FilterExpressionWrapper.ParseError, FilterExpressionWrapper.FilterString)
                : new MSTestFilterExpression(new TestCaseFilterExpression(FilterExpressionWrapper));

    private void HandleFilter(ITestExecutionFilter? filter, string? filterFromRunsettings, string? filterFromCommandLineOption)
    {
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
            FilterExpressionWrapper = new FilterExpressionWrapper(filterBuilder.ToString());
        }

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

    // Heuristic borrowed from the bridge: a GUID TestNodeUid maps to a VSTest Id filter, everything else to a
    // (escaped) FullyQualifiedName filter.
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
                        // Escape the special char if it is not already escaped. Note: index into k-1 (the previous
                        // character in this UID value), not i-1.
                        if (k - 1 < 0 || currentTestNodeUid.Value[k - 1] != '\\')
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

/// <summary>
/// Native MTP implementation of the VSTest <see cref="IRunContext"/> for the MSTest native path.
/// </summary>
[SuppressMessage("ApiDesign", "RS0030:Do not use banned APIs", Justification = "We can use MTP from this folder")]
internal sealed class MSTestRunContext : MSTestFilterContextBase, IRunContext
{
    public MSTestRunContext(ICommandLineOptions commandLineOptions, IRunSettings runSettings, ITestExecutionFilter filter)
        : base(commandLineOptions, runSettings, filter)
        => TestRunDirectory = runSettings.SettingsXml is null
            ? null
            : XDocument.Parse(runSettings.SettingsXml).Element("RunSettings")?.Element("RunConfiguration")?.Element("ResultsDirectory")?.Value;

    // TPv2-oriented flags that do not apply to the platform adapter (mirrors RunContextAdapter).
    public bool KeepAlive { get; }

    public bool InIsolation { get; }

    public bool IsDataCollectionEnabled { get; }

    public bool IsBeingDebugged => Debugger.IsAttached;

    public string? TestRunDirectory { get; }

    public string? SolutionDirectory { get; }
}

/// <summary>
/// Native MTP implementation of the VSTest <see cref="IDiscoveryContext"/> for the MSTest native path.
/// </summary>
[SuppressMessage("ApiDesign", "RS0030:Do not use banned APIs", Justification = "We can use MTP from this folder")]
internal sealed class MSTestDiscoveryContext : MSTestFilterContextBase, IDiscoveryContext
{
    public MSTestDiscoveryContext(ICommandLineOptions commandLineOptions, IRunSettings runSettings, ITestExecutionFilter filter)
        : base(commandLineOptions, runSettings, filter)
    {
    }
}
#endif
