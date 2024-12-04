// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Reflection;

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

#pragma warning disable TPEXP

namespace Microsoft.Testing.Platform.UnitTests.Filters;

[TestGroup]
public sealed class FiltersTests
{
    public async Task Zero_Registered_Filters_Builds_Nop_Filter()
    {
        ITestExecutionFilter testExecutionFilter = await GetBuiltFilter(_ => { });

        Assert.IsTrue(testExecutionFilter is NopFilter);
    }

    public async Task Zero_Available_Filters_Builds_Nop_Filter()
    {
        ITestExecutionFilter testExecutionFilter = await GetBuiltFilter(testHost =>
        {
            testHost.RegisterTestExecutionFilter(_ => new Filter1
            {
                IsAvailable = false,
            });
            testHost.RegisterTestExecutionFilter(_ => new Filter2
            {
                IsAvailable = false,
            });
        });

        Assert.IsTrue(testExecutionFilter is NopFilter);
    }

    public async Task Single_Registered_Filter_Builds_Single_Filter()
    {
        ITestExecutionFilter testExecutionFilter = await GetBuiltFilter(testHost =>
            testHost.RegisterTestExecutionFilter(_ => new Filter1()));

        Assert.IsTrue(testExecutionFilter is Filter1);
    }

    public async Task Single_Available_Filter_Builds_Single_Filter()
    {
        ITestExecutionFilter testExecutionFilter = await GetBuiltFilter(testHost =>
        {
            testHost.RegisterTestExecutionFilter(_ => new Filter1
            {
                IsAvailable = false,
            });
            testHost.RegisterTestExecutionFilter(_ => new Filter2());
        });

        Assert.IsTrue(testExecutionFilter is Filter2);
    }

    public async Task Two_Registered_Filters_Builds_Aggregate_Filter()
    {
        ITestExecutionFilter testExecutionFilter = await GetBuiltFilter(testHost =>
        {
            testHost.RegisterTestExecutionFilter(_ => new Filter1());
            testHost.RegisterTestExecutionFilter(_ => new Filter2());
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
        public bool IsAvailable { get; set; } = true;
    }

    private class Filter2 : ITestExecutionFilter
    {
        public bool IsAvailable { get; set; } = true;
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
