// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using AwesomeAssertions;

using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Requests;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.TestingPlatformAdapter;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Adapter;

using Moq;

using TestFramework.ForTestingMSTest;

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests;

public sealed class MSTestFilterContextTests : TestContainer
{
    public void NativeElementProviderUsesEveryBuiltinAndFirstTraitWithoutMaterializingHostCase()
    {
        object opaqueHandle = new();
        UnitTestElement element = new(new TestMethod("Method", "Example.Namespace.Widget", "source.dll", "Readable label"))
        {
            CachedTestNodeUid = Guid.Parse("71e451e4-8a84-4526-ae58-ea1800667a84"),
            TestCategory = ["Fast", "Smoke"],
            Priority = 7,
            Traits = [new("Platform", "first"), new("platform", "second"), new("Unrelated", "ignored")],
            HostRecordingHandle = opaqueHandle,
        };
        foreach ((string expression, bool expected) in new[]
        {
            ("fUlLyQuAlIfIeDnAmE=Example.Namespace.Widget.Method", true),
            ("Name=Readable label", true),
            ("cLaSsNaMe=Example.Namespace.Widget", true),
            ("iD=71e451e4-8a84-4526-ae58-ea1800667a84", true),
            ("tEsTcAtEgOrY=Smoke", true),
            ("priority=7", true),
            ("PLATFORM=first", true),
            ("platform=second", false),
            ("Unknown=anything", false),
            ("Name=Wrong label", false),
            ("TestCategory=Slow", false),
            ("Priority=99", false),
        })
        {
            var logger = new Mock<IAdapterMessageLogger>(MockBehavior.Strict);
            var provider = new MtpTestElementFilterProvider(CreateContext(EmptyRunSettings, new NopFilter(), expression));
            ITestElementFilter? filter = provider.GetTestElementFilter(logger.Object, out bool error);
            error.Should().BeFalse(expression);
            filter.Should().NotBeNull(expression);
            filter!.Matches(element).Should().Be(expected, expression);
            element.HostRecordingHandle.Should().BeSameAs(opaqueHandle);
            logger.VerifyNoOtherCalls();
        }
    }

    public void NativeElementProviderAbsentCategoriesPriorityAndTraitsNeverInventMatches()
    {
        foreach (bool emptyArrays in new[] { false, true })
        {
            UnitTestElement element = new(new TestMethod("Method", "Example.Widget", "source.dll", "Readable"))
            {
                TestCategory = emptyArrays ? [] : null,
                Traits = emptyArrays ? [] : null,
                Priority = null,
            };
            foreach (string expression in new[] { "TestCategory=Fast", "Priority=0", "Platform=any" })
            {
                var provider = new MtpTestElementFilterProvider(CreateContext(EmptyRunSettings, new NopFilter(), expression));
                ITestElementFilter? filter = provider.GetTestElementFilter(new Mock<IAdapterMessageLogger>(MockBehavior.Strict).Object, out bool error);
                error.Should().BeFalse();
                filter.Should().NotBeNull();
                filter!.Matches(element).Should().BeFalse();
                element.HostRecordingHandle.Should().BeNull();
            }
        }
    }

    public void NativeElementProviderSeparatesNoFilterFromReportedParseFailure()
    {
        var logger = new Mock<IAdapterMessageLogger>(MockBehavior.Strict);
        var absent = new MtpTestElementFilterProvider(CreateContext(EmptyRunSettings, new NopFilter()));
        absent.GetTestElementFilter(logger.Object, out bool noError).Should().BeNull();
        noError.Should().BeFalse();
        logger.VerifyNoOtherCalls();

        MSTestRunContext context = CreateContext(EmptyRunSettings, new NopFilter(), "FullyQualifiedName=Example&");
        Action parse = () => context.GetTestCaseFilter(null, _ => null);
        string parserMessage = parse.Should().ThrowExactly<TestPlatformFormatException>().Which.Message;
        var invalid = new MtpTestElementFilterProvider(context);
        logger.Setup(value => value.SendMessage(MessageLevel.Error, parserMessage));
        invalid.GetTestElementFilter(logger.Object, out bool hasError).Should().BeNull();
        hasError.Should().BeTrue();
        logger.Verify(value => value.SendMessage(MessageLevel.Error, parserMessage), Times.Once);
        logger.VerifyNoOtherCalls();
    }

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
