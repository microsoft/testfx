// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Capabilities.TestFramework;
using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Extensions.TestFramework;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Hosts;
using Microsoft.Testing.Platform.Logging;
using Microsoft.Testing.Platform.Requests;
using Microsoft.Testing.Platform.Services;
using Microsoft.Testing.Platform.TestHost;

using Moq;

using TestNode = Microsoft.Testing.Platform.Extensions.Messages.TestNode;

#pragma warning disable TPEXP

namespace Microsoft.Testing.Platform.UnitTests.Filters;

[TestClass]
public sealed class FiltersTests
{
    [TestMethod]
    public async Task Zero_Registered_Filters_Builds_Nop_Filter()
    {
        ITestExecutionFilter testExecutionFilter = await GetBuiltFilter(_ => { });

        Assert.IsTrue(testExecutionFilter is NopFilter);
    }

    [TestMethod]
    public async Task Zero_Available_Filters_Builds_Nop_Filter()
    {
        ITestExecutionFilter testExecutionFilter = await GetBuiltFilter(testHost =>
        {
            testHost.AddTestExecutionFilter(_ => new Filter1
            {
                IsEnabled = false,
            });
            testHost.AddTestExecutionFilter(_ => new Filter2
            {
                IsEnabled = false,
            });
        });

        Assert.IsTrue(testExecutionFilter is NopFilter);
    }

    [TestMethod]
    public async Task Single_Registered_Filter_Builds_Single_Filter()
    {
        ITestExecutionFilter testExecutionFilter = await GetBuiltFilter(testHost =>
            testHost.AddTestExecutionFilter(_ => new Filter1()));

        Assert.IsTrue(testExecutionFilter is Filter1);
    }

    [TestMethod]
    public async Task Single_Available_Filter_Builds_Single_Filter()
    {
        ITestExecutionFilter testExecutionFilter = await GetBuiltFilter(testHost =>
        {
            testHost.AddTestExecutionFilter(_ => new Filter1
            {
                IsEnabled = false,
            });
            testHost.AddTestExecutionFilter(_ => new Filter2());
        });

        Assert.IsTrue(testExecutionFilter is Filter2);
    }

    [TestMethod]
    public async Task Two_Registered_Filters_Builds_Aggregate_Filter()
    {
        ITestExecutionFilter testExecutionFilter = await GetBuiltFilter(testHost =>
        {
            testHost.AddTestExecutionFilter(_ => new Filter1());
            testHost.AddTestExecutionFilter(_ => new Filter2());
        });

        Assert.IsTrue(testExecutionFilter is AggregateFilter);
        Assert.IsTrue(((AggregateFilter)testExecutionFilter).InnerFilters[0] is Filter1);
        Assert.IsTrue(((AggregateFilter)testExecutionFilter).InnerFilters[1] is Filter2);
    }

    [TestMethod]
    public async Task AggregateFilter_CallsInner_IReceivesAllTestNodesExtension_When_Called_Itself()
    {
        ITestExecutionFilter testExecutionFilter = await GetBuiltFilter(testHost =>
        {
            testHost.AddTestExecutionFilter(_ => new ReceivableTestNodesFilter());
            testHost.AddTestExecutionFilter(_ => new ReceivableTestNodesFilter());
            testHost.AddTestExecutionFilter(_ => new ReceivableTestNodesFilter());
            testHost.AddTestExecutionFilter(_ => new ReceivableTestNodesFilter());
            testHost.AddTestExecutionFilter(_ => new ReceivableTestNodesFilter());
        });

        Assert.IsTrue(testExecutionFilter is AggregateFilter);

        ((AggregateFilter)testExecutionFilter).ReceiveTestNodes([]);

        Assert.IsTrue(((AggregateFilter)testExecutionFilter).InnerFilters.Cast<ReceivableTestNodesFilter>().All(x => x.ReceivedTestNodes));
    }

    [TestMethod]
    public async Task Batch_ReceivesAllTestNodes_Filter_Example()
    {
        TestNode[] testNodes =
        [
            new() { Uid = "1", DisplayName = "Test1", },
            new() { Uid = "2", DisplayName = "Test2", },
            new() { Uid = "3", DisplayName = "Test3", },
            new() { Uid = "4", DisplayName = "Test4", },
            new() { Uid = "5", DisplayName = "Test5", },
            new() { Uid = "6", DisplayName = "Test6", },
            new() { Uid = "7", DisplayName = "Test7", },
            new() { Uid = "8", DisplayName = "Test8", },
            new() { Uid = "9", DisplayName = "Test9", },
            new() { Uid = "10", DisplayName = "Test10", },
        ];

        ITestExecutionFilter testExecutionFilter = await GetBuiltFilter(testHost => testHost.AddTestExecutionFilter(_ => new DummyBatchFilter()));

        Assert.IsTrue(testExecutionFilter is DummyBatchFilter);

        ((DummyBatchFilter)testExecutionFilter).ReceiveTestNodes(testNodes);

        Assert.IsFalse(testExecutionFilter.MatchesFilter(testNodes[0]));
        Assert.IsFalse(testExecutionFilter.MatchesFilter(testNodes[1]));
        Assert.IsFalse(testExecutionFilter.MatchesFilter(testNodes[2]));
        Assert.IsFalse(testExecutionFilter.MatchesFilter(testNodes[3]));
        Assert.IsTrue(testExecutionFilter.MatchesFilter(testNodes[4]));
        Assert.IsTrue(testExecutionFilter.MatchesFilter(testNodes[5]));
        Assert.IsFalse(testExecutionFilter.MatchesFilter(testNodes[6]));
        Assert.IsFalse(testExecutionFilter.MatchesFilter(testNodes[7]));
        Assert.IsFalse(testExecutionFilter.MatchesFilter(testNodes[8]));
        Assert.IsFalse(testExecutionFilter.MatchesFilter(testNodes[9]));
    }

    private static async Task<ITestExecutionFilter> GetBuiltFilter(Action<ITestHostManager> action)
    {
        TestApplicationBuilder builder = CreateTestBuilder();
        builder.RegisterTestFramework(
            _ => new TestFrameworkCapabilities(),
            (_, _) => new DummyFramework());

        action(builder.TestHost);

        ITestApplication testApplication = await builder.BuildAsync();

        var app = (TestApplication)testApplication;

        await app.RunAsync();

        var framework = (DummyFramework)GetInnerFramework((TestFrameworkProxy)app.ServiceProvider.GetTestFramework());

        ITestExecutionFilter testExecutionFilter = framework.Filter;
        return testExecutionFilter;
    }

    private static TestApplicationBuilder CreateTestBuilder()
    {
        var applicationLoggingState = new ApplicationLoggingState(LogLevel.Trace, new CommandLineParseResult(null, [], []));
        return new TestApplicationBuilder(applicationLoggingState, DateTimeOffset.Now, new TestApplicationOptions(), new Mock<IUnhandledExceptionsHandler>().Object);
    }

    private class Filter1 : ITestExecutionFilter
    {
        public bool IsEnabled { get; init; } = true;

        public bool MatchesFilter(TestNode testNode) => true;
    }

    private class Filter2 : ITestExecutionFilter
    {
        public bool IsEnabled { get; init; } = true;

        public bool MatchesFilter(TestNode testNode) => true;
    }

    private class DummyBatchFilter : ITestExecutionFilter, IReceivesAllTestNodesExtension
    {
        private TestNode[][]? _batches;

        private const int BatchIndex = 2;

        private const int BatchSplitSize = 5;

        public bool IsEnabled => true;

        public bool MatchesFilter(TestNode testNode) =>
            _batches == null
                ? throw new InvalidOperationException("Test nodes not received from test framework.")
                : _batches[BatchIndex].Contains(testNode);

        public void ReceiveTestNodes(IReadOnlyList<TestNode> allTestNodes) => _batches = allTestNodes.Chunk(allTestNodes.Count / BatchSplitSize).ToArray();
    }

    private class ReceivableTestNodesFilter : ITestExecutionFilter, IReceivesAllTestNodesExtension
    {
        public bool ReceivedTestNodes { get; set; }

        public bool IsEnabled { get; init; } = true;

        public bool MatchesFilter(TestNode testNode) => true;

        public void ReceiveTestNodes(IReadOnlyList<TestNode> testNodes) => ReceivedTestNodes = true;
    }

    private class DummyFramework : ITestFramework
    {
        public string Uid => string.Empty;

        public string Version => string.Empty;

        public string DisplayName => string.Empty;

        public string Description => string.Empty;

        public Task<bool> IsEnabledAsync() => Task.FromResult(true);

        public Task<CreateTestSessionResult> CreateTestSessionAsync(CreateTestSessionContext context) => Task.FromResult(new CreateTestSessionResult
        {
            IsSuccess = true,
        });

        public Task ExecuteRequestAsync(ExecuteRequestContext context)
        {
            var request = (RunTestExecutionRequest)context.Request;

            Filter = request.Filter;

            context.Complete();

            return Task.CompletedTask;
        }

        public Task<CloseTestSessionResult> CloseTestSessionAsync(CloseTestSessionContext context) => Task.FromResult(new CloseTestSessionResult
        {
            IsSuccess = true,
        });

        public ITestExecutionFilter Filter { get; private set; } = null!;
    }

    private static ITestFramework GetInnerFramework(TestFrameworkProxy proxy)
        => (ITestFramework)proxy.GetType().GetField(
                "_testFramework",
                BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(proxy)!;
}
