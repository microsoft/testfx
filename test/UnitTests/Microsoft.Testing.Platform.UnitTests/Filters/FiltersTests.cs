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

        Assert.IsInstanceOfType<AggregateFilter>(testExecutionFilter);

        ITestExecutionFilter filter1 = ((AggregateFilter)testExecutionFilter).InnerFilters[0];
        Assert.IsInstanceOfType<Filter1>(filter1);
        Assert.IsFalse(await filter1.IsEnabledAsync());

        ITestExecutionFilter filter2 = ((AggregateFilter)testExecutionFilter).InnerFilters[0];
        Assert.IsInstanceOfType<Filter2>(filter2);
        Assert.IsTrue(await filter2.IsEnabledAsync());
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

        return framework.Filter;
    }

    private static TestApplicationBuilder CreateTestBuilder()
    {
        var applicationLoggingState = new ApplicationLoggingState(LogLevel.Trace, new CommandLineParseResult(null, [], []));
        return new TestApplicationBuilder(applicationLoggingState, DateTimeOffset.Now, new TestApplicationOptions(), new Mock<IUnhandledExceptionsHandler>().Object);
    }

    private class Filter1 : ITestExecutionFilter
    {
        private readonly bool _isEnabled = true;

        public bool IsEnabled
        {
            init => _isEnabled = value;
        }

        public Task<bool> IsEnabledAsync() => Task.FromResult(_isEnabled);

        public Task<bool> MatchesFilterAsync(TestNode testNode) => Task.FromResult(true);
    }

    private class Filter2 : ITestExecutionFilter
    {
        private readonly bool _isEnabled = true;

        public bool IsEnabled
        {
            init => _isEnabled = value;
        }

        public Task<bool> IsEnabledAsync() => Task.FromResult(_isEnabled);

        public Task<bool> MatchesFilterAsync(TestNode testNode) => Task.FromResult(true);
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
