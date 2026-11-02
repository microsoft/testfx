// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Requests;
using Microsoft.Testing.Platform.Services;
using Microsoft.Testing.Platform.TestHost;

namespace Microsoft.Testing.Platform.UnitTests;

[TestClass]
public sealed class TestExecutionFilterComposerTests
{
    private static readonly TestExecutionFilterContext RunConsoleContext =
        new(TestExecutionRequestKind.Run, TestExecutionRequestOrigin.Console);

    [TestMethod]
    public async Task ComposeAsync_WithBuiltInFilterOnly_PreservesConstraint()
    {
        TreeNodeFilter builtInFilter = new("/Tests/**");

        ITestExecutionFilter result = await TestExecutionFilterComposer.ComposeAsync(
            builtInFilter,
            [],
            RunConsoleContext,
            allowProviderContributions: true,
            CancellationToken.None);

        Assert.AreSame(builtInFilter, result);
    }

    [TestMethod]
    public async Task ComposeAsync_WithoutProviders_ReturnsSameNopFilterInstance()
    {
        NopFilter builtInFilter = new();

        ITestExecutionFilter result = await TestExecutionFilterComposer.ComposeAsync(
            builtInFilter,
            [],
            RunConsoleContext,
            allowProviderContributions: true,
            CancellationToken.None);

        Assert.AreSame(builtInFilter, result);
    }

    [TestMethod]
    public async Task ComposeAsync_WithoutProviders_DoesNotNormalizeUidFilter()
    {
        TestNodeUidListFilter builtInFilter = new([new("B"), new("A"), new("B")]);

        ITestExecutionFilter result = await TestExecutionFilterComposer.ComposeAsync(
            builtInFilter,
            [],
            RunConsoleContext,
            allowProviderContributions: true,
            CancellationToken.None);

        Assert.AreSame(builtInFilter, result);
    }

    [TestMethod]
    public async Task ComposeAsync_WithoutProviders_DoesNotRejectCustomRequestFilter()
    {
        CustomFilter builtInFilter = new();

        ITestExecutionFilter result = await TestExecutionFilterComposer.ComposeAsync(
            builtInFilter,
            [],
            RunConsoleContext,
            allowProviderContributions: true,
            CancellationToken.None);

        Assert.AreSame(builtInFilter, result);
    }

    [TestMethod]
    public async Task ComposeAsync_WhenAllProvidersOptOut_ReturnsSameRequestFilterInstance()
    {
        StubFilterProvider nullProvider = new("provider-a", getFilter: (_, _) => Task.FromResult<ITestExecutionFilter?>(null));
        StubFilterProvider nopProvider = new("provider-b", new NopFilter());
        TestNodeUidListFilter builtInFilter = new([new("B"), new("A")]);

        ITestExecutionFilter result = await TestExecutionFilterComposer.ComposeAsync(
            builtInFilter,
            [nullProvider, nopProvider],
            RunConsoleContext,
            allowProviderContributions: true,
            CancellationToken.None);

        Assert.AreSame(builtInFilter, result);
    }

    [TestMethod]
    public async Task ComposeAsync_WithProviderContribution_RejectsCustomRequestFilter()
    {
        StubFilterProvider provider = new("provider-a", new TestNodeUidListFilter([new("A")]));

        InvalidOperationException exception = await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => TestExecutionFilterComposer.ComposeAsync(
                new CustomFilter(),
                [provider],
                RunConsoleContext,
                allowProviderContributions: true,
                CancellationToken.None));

        Assert.Contains(typeof(CustomFilter).FullName!, exception.Message);
    }

    [TestMethod]
    public async Task ComposeAsync_WithOneProvider_UsesProviderConstraint()
    {
        StubFilterProvider provider = new("provider-a", new TestNodeUidListFilter([new("B"), new("A")]));

        ITestExecutionFilter result = await TestExecutionFilterComposer.ComposeAsync(
            new NopFilter(),
            [provider],
            RunConsoleContext,
            allowProviderContributions: true,
            CancellationToken.None);

        AssertUidFilter(result, "A", "B");
    }

    [TestMethod]
    public async Task ComposeAsync_WithTwoUidProviders_IntersectsIndependentlyOfRegistrationOrder()
    {
        StubFilterProvider providerA = new("provider-a", new TestNodeUidListFilter([new("A"), new("B")]));
        StubFilterProvider providerB = new("provider-b", new TestNodeUidListFilter([new("B"), new("C")]));

        ITestExecutionFilter resultAB = await TestExecutionFilterComposer.ComposeAsync(
            new NopFilter(),
            [providerA, providerB],
            RunConsoleContext,
            allowProviderContributions: true,
            CancellationToken.None);
        ITestExecutionFilter resultBA = await TestExecutionFilterComposer.ComposeAsync(
            new NopFilter(),
            [providerB, providerA],
            RunConsoleContext,
            allowProviderContributions: true,
            CancellationToken.None);

        AssertUidFilter(resultAB, "B");
        AssertUidFilter(resultBA, "B");
    }

    [TestMethod]
    public async Task ComposeAsync_WithDisjointUidProviders_ReturnsEmptyUidFilter()
    {
        StubFilterProvider providerA = new("provider-a", new TestNodeUidListFilter([new("A")]));
        StubFilterProvider providerB = new("provider-b", new TestNodeUidListFilter([new("B")]));

        ITestExecutionFilter result = await TestExecutionFilterComposer.ComposeAsync(
            new NopFilter(),
            [providerA, providerB],
            RunConsoleContext,
            allowProviderContributions: true,
            CancellationToken.None);

        AssertUidFilter(result);
    }

    [TestMethod]
    public async Task ComposeAsync_WithBuiltInAndProviderUidFilters_IntersectsConstraints()
    {
        TestNodeUidListFilter builtInFilter = new([new("A"), new("B")]);
        StubFilterProvider provider = new("provider-a", new TestNodeUidListFilter([new("B"), new("C")]));

        ITestExecutionFilter result = await TestExecutionFilterComposer.ComposeAsync(
            builtInFilter,
            [provider],
            RunConsoleContext,
            allowProviderContributions: true,
            CancellationToken.None);

        AssertUidFilter(result, "B");
    }

    [TestMethod]
    public async Task ComposeAsync_WithTreeAndUidConstraints_ReturnsAndComposite()
    {
        TreeNodeFilter treeFilter = new("/Tests/**");
        StubFilterProvider provider = new("provider-a", new TestNodeUidListFilter([new("A")]));

        ITestExecutionFilter result = await TestExecutionFilterComposer.ComposeAsync(
            treeFilter,
            [provider],
            RunConsoleContext,
            allowProviderContributions: true,
            CancellationToken.None);

        CompositeTestExecutionFilter composite = Assert.IsInstanceOfType<CompositeTestExecutionFilter>(result);
        Assert.AreEqual(TestExecutionFilterOperator.And, composite.Operator);
        Assert.HasCount(2, composite.Filters);
        Assert.AreSame(treeFilter, Assert.ContainsSingle(composite.Filters.OfType<TreeNodeFilter>()));
        _ = Assert.ContainsSingle(composite.Filters.OfType<TestNodeUidListFilter>());
    }

    [TestMethod]
    public async Task ComposeAsync_WithNestedAndComposite_FlattensAndIntersectsUidConstraints()
    {
        TreeNodeFilter treeFilter = new("/Tests/**");
        CompositeTestExecutionFilter providerFilter = new(
            TestExecutionFilterOperator.And,
            treeFilter,
            new TestNodeUidListFilter([new("A"), new("B")]));
        StubFilterProvider provider = new("provider-a", providerFilter);

        ITestExecutionFilter result = await TestExecutionFilterComposer.ComposeAsync(
            new TestNodeUidListFilter([new("B"), new("C")]),
            [provider],
            RunConsoleContext,
            allowProviderContributions: true,
            CancellationToken.None);

        CompositeTestExecutionFilter composite = Assert.IsInstanceOfType<CompositeTestExecutionFilter>(result);
        Assert.HasCount(2, composite.Filters);
        Assert.AreSame(treeFilter, Assert.ContainsSingle(composite.Filters.OfType<TreeNodeFilter>()));
        AssertUidFilter(Assert.ContainsSingle(composite.Filters.OfType<TestNodeUidListFilter>()), "B");
    }

    [TestMethod]
    public async Task ComposeAsync_WithUnsupportedProviderFilter_ThrowsActionableError()
    {
        StubFilterProvider provider = new("provider-a", new CustomFilter());

        InvalidOperationException exception = await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => TestExecutionFilterComposer.ComposeAsync(
                new NopFilter(),
                [provider],
                RunConsoleContext,
                allowProviderContributions: true,
                CancellationToken.None));

        Assert.Contains("provider-a", exception.Message);
        Assert.Contains(typeof(CustomFilter).FullName!, exception.Message);
    }

    [TestMethod]
    public async Task ComposeAsync_ForServerWithNullContribution_PreservesRequestFilterAndContext()
    {
        TestExecutionFilterContext? observedContext = null;
        StubFilterProvider provider = new(
            "provider-a",
            getFilter: (context, _) =>
            {
                observedContext = context;
                return Task.FromResult<ITestExecutionFilter?>(null);
            });
        TestNodeUidListFilter requestFilter = new([new("A")]);

        ITestExecutionFilter result = await TestExecutionFilterComposer.ComposeAsync(
            requestFilter,
            [provider],
            new(TestExecutionRequestKind.Discovery, TestExecutionRequestOrigin.Server),
            allowProviderContributions: false,
            CancellationToken.None);

        AssertUidFilter(result, "A");
        Assert.AreSame(requestFilter, result);
        Assert.IsNotNull(observedContext);
        Assert.AreEqual(TestExecutionRequestKind.Discovery, observedContext.RequestKind);
        Assert.AreEqual(TestExecutionRequestOrigin.Server, observedContext.Origin);
    }

    [TestMethod]
    public async Task ComposeAsync_ForServerWithProviderConstraint_ThrowsActionableError()
    {
        StubFilterProvider provider = new("provider-a", new TestNodeUidListFilter([new("A")]));

        InvalidOperationException exception = await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => TestExecutionFilterComposer.ComposeAsync(
                new NopFilter(),
                [provider],
                new(TestExecutionRequestKind.Run, TestExecutionRequestOrigin.Server),
                allowProviderContributions: false,
                CancellationToken.None));

        Assert.Contains("provider-a", exception.Message);
        Assert.Contains("return null", exception.Message);
    }

    [TestMethod]
    public async Task ComposeAsync_PassesRequestContextAndCancellationToken()
    {
        using CancellationTokenSource cancellationTokenSource = new();
        TestExecutionFilterContext? observedContext = null;
        CancellationToken observedCancellationToken = default;
        StubFilterProvider provider = new(
            "provider-a",
            getFilter: (context, cancellationToken) =>
            {
                observedContext = context;
                observedCancellationToken = cancellationToken;
                return Task.FromResult<ITestExecutionFilter?>(null);
            });
        TestExecutionFilterContext expectedContext = new(TestExecutionRequestKind.Discovery, TestExecutionRequestOrigin.Console);

        _ = await TestExecutionFilterComposer.ComposeAsync(
            new NopFilter(),
            [provider],
            expectedContext,
            allowProviderContributions: true,
            cancellationTokenSource.Token);

        Assert.AreSame(expectedContext, observedContext);
        Assert.AreEqual(cancellationTokenSource.Token, observedCancellationToken);
    }

    [TestMethod]
    public async Task ComposeAsync_WithCanceledToken_DoesNotInvokeProvider()
    {
        bool wasInvoked = false;
        StubFilterProvider provider = new(
            "provider-a",
            getFilter: (_, _) =>
            {
                wasInvoked = true;
                return Task.FromResult<ITestExecutionFilter?>(null);
            });
        CancellationToken canceledToken = new(canceled: true);

        _ = await Assert.ThrowsExactlyAsync<OperationCanceledException>(
            () => TestExecutionFilterComposer.ComposeAsync(
                new NopFilter(),
                [provider],
                RunConsoleContext,
                allowProviderContributions: true,
                canceledToken));

        Assert.IsFalse(wasInvoked);
    }

    [TestMethod]
    public async Task BuildTestExecutionFilterProvidersAsync_SkipsDisabledAndInitializesAllEnabledProviders()
    {
        TestHostManager manager = new();
        StubFilterProvider enabledA = new("enabled-a", new NopFilter());
        StubFilterProvider disabled = new("disabled", new NopFilter(), isEnabled: false);
        StubFilterProvider enabledB = new("enabled-b", new NopFilter());
        manager.AddTestExecutionFilterProvider(_ => enabledA);
        manager.AddTestExecutionFilterProvider(_ => disabled);
        manager.AddTestExecutionFilterProvider(_ => enabledB);

        ITestExecutionFilterProvider[] providers = await manager.BuildTestExecutionFilterProvidersAsync(new ServiceProvider());

        Assert.HasCount(2, providers);
        Assert.AreSame(enabledA, providers[0]);
        Assert.AreSame(enabledB, providers[1]);
        Assert.IsTrue(enabledA.IsInitialized);
        Assert.IsFalse(disabled.IsInitialized);
        Assert.IsTrue(enabledB.IsInitialized);
    }

    [TestMethod]
    public void CompositeFilter_WithFewerThanTwoChildren_Throws()
        => Assert.ThrowsExactly<ArgumentException>(
            () => new CompositeTestExecutionFilter(TestExecutionFilterOperator.And, new NopFilter()));

    [TestMethod]
    public void CompositeFilter_WithNullChild_Throws()
        => Assert.ThrowsExactly<ArgumentException>(
            () => new CompositeTestExecutionFilter(TestExecutionFilterOperator.And, new NopFilter(), null!));

    [TestMethod]
    public async Task ComposeAsync_UidIntersection_IsOrdinalDeduplicatedAndDeterministicallyOrdered()
    {
        TestNodeUidListFilter request = new([new("a"), new("A"), new("B"), new("B"), new("b")]);
        StubFilterProvider provider = new("provider", new TestNodeUidListFilter([new("a"), new("B"), new("c")]));

        ITestExecutionFilter result = await TestExecutionFilterComposer.ComposeAsync(
            request, [provider], RunConsoleContext, allowProviderContributions: true, CancellationToken.None);

        AssertUidFilter(result, "B", "a");
        Assert.AreSequenceEqual(new[] { "a", "A", "B", "B", "b" }, request.TestNodeUids.Select(uid => uid.Value));
    }

    [TestMethod]
    public async Task ComposeAsync_CancellationDuringProvider_DoesNotUseItsResultOrInvokeLaterProviders()
    {
        using CancellationTokenSource cancellation = new();
        StubFilterProvider first = new("first", (_, _) =>
        {
            cancellation.Cancel();
            return Task.FromResult<ITestExecutionFilter?>(new TestNodeUidListFilter([new("would constrain")]));
        });
        bool laterInvoked = false;
        StubFilterProvider later = new("later", (_, _) =>
        {
            laterInvoked = true;
            return Task.FromResult<ITestExecutionFilter?>(new NopFilter());
        });

        OperationCanceledException exception = await Assert.ThrowsExactlyAsync<OperationCanceledException>(() =>
            TestExecutionFilterComposer.ComposeAsync(new NopFilter(), [first, later], RunConsoleContext, true, cancellation.Token));

        Assert.AreEqual(cancellation.Token, exception.CancellationToken);
        Assert.IsFalse(laterInvoked);
    }

    [TestMethod]
    public async Task ComposeAsync_ConjunctionContainingOnlyNopLeaves_DoesNotDiscardRequestConstraint()
    {
        StubFilterProvider provider = new("empty-conjunction", new CompositeTestExecutionFilter(
            TestExecutionFilterOperator.And, new NopFilter(), new NopFilter()));

        ITestExecutionFilter result = await TestExecutionFilterComposer.ComposeAsync(
            new TestNodeUidListFilter([new("z"), new("A"), new("z")]), [provider], RunConsoleContext, true, CancellationToken.None);

        AssertUidFilter(result, "A", "z");
    }

    private static void AssertUidFilter(ITestExecutionFilter filter, params string[] expectedUids)
    {
        TestNodeUidListFilter uidFilter = Assert.IsInstanceOfType<TestNodeUidListFilter>(filter);
        Assert.HasCount(expectedUids.Length, uidFilter.TestNodeUids);
        for (int i = 0; i < expectedUids.Length; i++)
        {
            Assert.AreEqual(expectedUids[i], uidFilter.TestNodeUids[i].Value);
        }
    }

    private sealed class CustomFilter : ITestExecutionFilter;

    private sealed class StubFilterProvider : ITestExecutionFilterProvider, IAsyncInitializableExtension
    {
        private readonly bool _isEnabled;
        private readonly Func<TestExecutionFilterContext, CancellationToken, Task<ITestExecutionFilter?>> _getFilter;

        public StubFilterProvider(string uid, ITestExecutionFilter filter, bool isEnabled = true)
            : this(uid, (_, _) => Task.FromResult<ITestExecutionFilter?>(filter), isEnabled)
        {
        }

        public StubFilterProvider(
            string uid,
            Func<TestExecutionFilterContext, CancellationToken, Task<ITestExecutionFilter?>> getFilter,
            bool isEnabled = true)
        {
            Uid = uid;
            _getFilter = getFilter;
            _isEnabled = isEnabled;
        }

        public string Uid { get; }

        public string Version => "1.0.0";

        public string DisplayName => Uid;

        public string Description => Uid;

        public bool IsInitialized { get; private set; }

        public Task<ITestExecutionFilter?> GetFilterAsync(TestExecutionFilterContext context, CancellationToken cancellationToken)
            => _getFilter(context, cancellationToken);

        public Task InitializeAsync()
        {
            IsInitialized = true;
            return Task.CompletedTask;
        }

        public Task<bool> IsEnabledAsync() => Task.FromResult(_isEnabled);
    }

    [TestMethod]
    public void GetLeafFilters_NestedConjunctionPreservesDepthFirstIdentityAndOrder()
    {
        TreeNodeFilter first = new("/Assembly/**");
        TestNodeUidListFilter second = new([new("uid-b"), new("uid-a")]);
        NopFilter third = new();
        TreeNodeFilter fourth = new("/Assembly/Namespace/Class/Case");
        CompositeTestExecutionFilter root = new(
            TestExecutionFilterOperator.And,
            first,
            new CompositeTestExecutionFilter(
                TestExecutionFilterOperator.And,
                second,
                new CompositeTestExecutionFilter(TestExecutionFilterOperator.And, third, fourth)));

        Assert.AreSequenceEqual<ITestExecutionFilter>([first, second, third, fourth], TestExecutionFilterComposer.GetLeafFilters(root));
        Assert.AreSame(first, Assert.ContainsSingle(TestExecutionFilterComposer.GetLeafFilters(first)));
        Assert.AreSequenceEqual(["uid-b", "uid-a"], second.TestNodeUids.Select(uid => uid.Value));
    }

    [TestMethod]
    public async Task ComposeAsync_OnlyNopConstraintsProducesUnconstrainedFilter()
    {
        StubFilterProvider provider = new(
            "unconstrained",
            new CompositeTestExecutionFilter(TestExecutionFilterOperator.And, new NopFilter(), new NopFilter()));

        ITestExecutionFilter result = await TestExecutionFilterComposer.ComposeAsync(
            new NopFilter(), [provider], RunConsoleContext, true, CancellationToken.None);

        Assert.IsInstanceOfType<NopFilter>(result);
        Assert.AreSame(result, Assert.ContainsSingle(TestExecutionFilterComposer.GetLeafFilters(result)));
    }
}
