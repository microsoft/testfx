// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Requests;

using Moq;

namespace Microsoft.Testing.Platform.UnitTests;

[TestClass]
public sealed class ConsoleTestExecutionRequestFactoryTests
{
    [TestMethod]
    public async Task CreateRequestAsync_WhenFilterFactoryReportsFailure_ThrowsEvenIfItReturnedAFilter()
    {
        // A failed creation must be rejected even when a filter instance accompanies the failure: the failure flag,
        // not the nullability of the filter, decides whether a request can be created.
        ConsoleTestExecutionRequestFactory factory = CreateFactory(created: false, new TestNodeUidListFilter([new("a")]), isDiscovery: false);

        _ = await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => factory.CreateRequestAsync(CreateSession(), CancellationToken.None));
    }

    [TestMethod]
    public async Task CreateRequestAsync_WhenFilterFactoryReturnsNoFilter_Throws()
    {
        ConsoleTestExecutionRequestFactory factory = CreateFactory(created: true, filter: null, isDiscovery: false);

        _ = await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => factory.CreateRequestAsync(CreateSession(), CancellationToken.None));
    }

    [TestMethod]
    public async Task CreateRequestAsync_WithDiscoveryOption_CreatesDiscoveryRequestForTheSessionAndFilter()
    {
        Microsoft.Testing.Platform.TestHost.TestSessionContext session = CreateSession();
        TestNodeUidListFilter filter = new([new("a")]);
        ConsoleTestExecutionRequestFactory factory = CreateFactory(created: true, filter, isDiscovery: true);

        TestExecutionRequest request = await factory.CreateRequestAsync(session, CancellationToken.None);

        _ = Assert.IsInstanceOfType<DiscoverTestExecutionRequest>(request);
        Assert.IsNotInstanceOfType<RunTestExecutionRequest>(request);
        Assert.AreSame(session, request.Session);
        Assert.AreSame(filter, request.Filter);
    }

    [TestMethod]
    public async Task CreateRequestAsync_WithoutDiscoveryOption_CreatesRunRequestForTheSessionAndFilter()
    {
        Microsoft.Testing.Platform.TestHost.TestSessionContext session = CreateSession();
        TestNodeUidListFilter filter = new([new("a")]);
        ConsoleTestExecutionRequestFactory factory = CreateFactory(created: true, filter, isDiscovery: false);

        TestExecutionRequest request = await factory.CreateRequestAsync(session, CancellationToken.None);

        _ = Assert.IsInstanceOfType<RunTestExecutionRequest>(request);
        Assert.IsNotInstanceOfType<DiscoverTestExecutionRequest>(request);
        Assert.AreSame(session, request.Session);
        Assert.AreSame(filter, request.Filter);
    }

    [TestMethod]
    [DataRow(true, TestExecutionRequestKind.Discovery)]
    [DataRow(false, TestExecutionRequestKind.Run)]
    public async Task CreateRequestAsync_PassesRequestKindAndConsoleOriginToFilterProviders(bool isDiscovery, TestExecutionRequestKind expectedKind)
    {
        TestExecutionFilterContext? observedContext = null;
        Mock<ITestExecutionFilterProvider> provider = new();
        provider.SetupGet(p => p.Uid).Returns("provider-a");
        provider.Setup(p => p.GetFilterAsync(It.IsAny<TestExecutionFilterContext>(), It.IsAny<CancellationToken>()))
            .Callback<TestExecutionFilterContext, CancellationToken>((context, _) => observedContext = context)
            .ReturnsAsync((ITestExecutionFilter?)null);
        ConsoleTestExecutionRequestFactory factory = CreateFactory(created: true, new TestNodeUidListFilter([new("a")]), isDiscovery, provider.Object);

        _ = await factory.CreateRequestAsync(CreateSession(), CancellationToken.None);

        Assert.IsNotNull(observedContext);
        Assert.AreEqual(expectedKind, observedContext.RequestKind);
        Assert.AreEqual(TestExecutionRequestOrigin.Console, observedContext.Origin);
    }

    [TestMethod]
    public async Task CreateRequestAsync_ComposesProviderConstraintsIntoTheRequestFilter()
    {
        // The console origin allows providers to narrow the request: the result is the intersection of both uid lists.
        Mock<ITestExecutionFilterProvider> provider = new();
        provider.SetupGet(p => p.Uid).Returns("provider-a");
        provider.Setup(p => p.GetFilterAsync(It.IsAny<TestExecutionFilterContext>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ITestExecutionFilter?)new TestNodeUidListFilter([new("b"), new("c")]));
        ConsoleTestExecutionRequestFactory factory = CreateFactory(created: true, new TestNodeUidListFilter([new("a"), new("b")]), isDiscovery: false, provider.Object);

        TestExecutionRequest request = await factory.CreateRequestAsync(CreateSession(), CancellationToken.None);

        TestNodeUidListFilter composed = Assert.IsInstanceOfType<TestNodeUidListFilter>(request.Filter);
        Assert.HasCount(1, composed.TestNodeUids);
        Assert.AreEqual("b", composed.TestNodeUids[0].Value);
    }

    private static Microsoft.Testing.Platform.TestHost.TestSessionContext CreateSession() => new(new("session-17"));

    private static ConsoleTestExecutionRequestFactory CreateFactory(
        bool created,
        ITestExecutionFilter? filter,
        bool isDiscovery,
        params ITestExecutionFilterProvider[] providers)
    {
        Mock<ICommandLineOptions> options = new();
        options.Setup(o => o.IsOptionSet(PlatformCommandLineProvider.DiscoverTestsOptionKey)).Returns(isDiscovery);
        Mock<ITestExecutionFilterFactory> filterFactory = new();
        filterFactory.Setup(f => f.TryCreateAsync()).ReturnsAsync((created, filter));
        return new ConsoleTestExecutionRequestFactory(options.Object, filterFactory.Object, providers);
    }
}
