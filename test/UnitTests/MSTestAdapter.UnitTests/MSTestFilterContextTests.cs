// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using AwesomeAssertions;

using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Requests;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.TestingPlatformAdapter;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Adapter;

using Moq;

using TestFramework.ForTestingMSTest;

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests;

public sealed class MSTestFilterContextTests : TestContainer
{
    private const string EmptyRunSettings =
    """
    <RunSettings>
        <RunConfiguration>
        </RunConfiguration>
    </RunSettings>
    """;

    public void EmptyUidListBuildsMatchNoneFilter()
    {
        MSTestRunContext context = CreateContext(EmptyRunSettings, CreateUidFilter());

        GetFilterValue(context).Should().Be(
            "(FullyQualifiedName=__MTP_EMPTY_UID_FILTER__&FullyQualifiedName!=__MTP_EMPTY_UID_FILTER__)");
        MatchesFullyQualifiedName(context, "__MTP_EMPTY_UID_FILTER__").Should().BeFalse();
        MatchesFullyQualifiedName(context, "A.B.Test").Should().BeFalse();
    }

    public void AndCompositeTranslatesChildrenRecursively()
    {
        CompositeTestExecutionFilter filter = new(
            TestExecutionFilterOperator.And,
            CreateUidFilter("A.B.Test1", "A.B.Test2"),
            CreateUidFilter("A.B.Test2", "A.B.Test3"));

        MSTestRunContext context = CreateContext(EmptyRunSettings, filter);

        GetFilterValue(context).Should().Be(
            "(FullyQualifiedName=A.B.Test1|FullyQualifiedName=A.B.Test2) & (FullyQualifiedName=A.B.Test2|FullyQualifiedName=A.B.Test3)");
    }

    public void RunSettingsCommandLineAndCompositePreserveAndSemantics()
    {
        const string RunSettings =
        """
        <RunSettings>
            <RunConfiguration>
                <TestCaseFilter>Category=Fast</TestCaseFilter>
            </RunConfiguration>
        </RunSettings>
        """;
        CompositeTestExecutionFilter filter = new(
            TestExecutionFilterOperator.And,
            new NopFilter(),
            CreateUidFilter("A.B.Test"));

        MSTestRunContext context = CreateContext(RunSettings, filter, commandLineFilter: "Priority=1");

        GetFilterValue(context).Should().Be(
            "(Category=Fast) & (Priority=1) & (FullyQualifiedName=A.B.Test)");
    }

    public void TreeNodeFilterThrowsActionableError()
    {
        Action action = () => _ = CreateContext(EmptyRunSettings, new TreeNodeFilter("/Tests/**"));

        action.Should().Throw<NotSupportedException>()
            .Which.Message.Should().Contain(nameof(TreeNodeFilter))
            .And.Contain("MSTest");
    }

    public void CustomFilterThrowsActionableError()
    {
        Action action = () => _ = CreateContext(EmptyRunSettings, new CustomFilter());

        action.Should().Throw<NotSupportedException>()
            .Which.Message.Should().Contain(nameof(CustomFilter))
            .And.Contain("MSTest");
    }

    private static TestNodeUidListFilter CreateUidFilter(params string[] uids)
        => new([.. uids.Select(uid => new TestNodeUid(uid))]);

    private static string GetFilterValue(MSTestRunContext context)
    {
        ITestCaseFilterExpression? filterExpression = context.GetTestCaseFilter(null, _ => null);
        filterExpression.Should().NotBeNull();
        return filterExpression!.TestCaseFilterValue;
    }

    private static bool MatchesFullyQualifiedName(MSTestRunContext context, string fullyQualifiedName)
    {
        ITestCaseFilterExpression? filterExpression = context.GetTestCaseFilter(null, _ => null);
        filterExpression.Should().NotBeNull();
        var testCase = new TestCase(fullyQualifiedName, new Uri("executor://mstest"), "source.dll");
        return filterExpression!.MatchTestCase(
            testCase,
            propertyName => string.Equals(propertyName, "FullyQualifiedName", StringComparison.OrdinalIgnoreCase)
                ? fullyQualifiedName
                : null);
    }

    private static MSTestRunContext CreateContext(
        string runSettingsXml,
        ITestExecutionFilter filter,
        string? commandLineFilter = null)
    {
        var runSettings = new Mock<IRunSettings>();
        runSettings.Setup(settings => settings.SettingsXml).Returns(runSettingsXml);

        var commandLineOptions = new Mock<ICommandLineOptions>();
        string[]? commandLineFilterArguments = commandLineFilter is null ? null : [commandLineFilter];
        commandLineOptions
            .Setup(options => options.TryGetOptionArgumentList(
                MSTestTestCaseFilterCommandLineOptionsProvider.TestCaseFilterOptionName,
                out commandLineFilterArguments))
            .Returns(commandLineFilter is not null);

        return new MSTestRunContext(commandLineOptions.Object, runSettings.Object, filter);
    }

    private sealed class CustomFilter : ITestExecutionFilter;
}
