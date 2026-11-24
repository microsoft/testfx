// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using AwesomeAssertions;

using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Requests;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.TestingPlatformAdapter;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface;
using Microsoft.VisualStudio.TestPlatform.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.ObjectModel.Adapter;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using Moq;

using TestFramework.ForTestingMSTest;

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests;

public sealed class MSTestPartitionSelectionTests : TestContainer
{
    private const string Source = "Acme.Tests.dll";
    private const string Checkout = "Acme.CheckoutTests";
    private const string Cart = "Acme.CartTests";
    private const string EmptySettings = "<RunSettings />";
    private const string FastSettings = "<RunSettings><RunConfiguration><TestCaseFilter>TestCategory=Fast</TestCaseFilter></RunConfiguration></RunSettings>";

    public void GetUnitTestDiscoverer_WithPartition_BypassesPotentiallyUidNarrowedFactory()
    {
        var sourceHandler = new Mock<ITestSourceHandler>();
        var narrowed = new CompleteDiscoverer([Element(Cart, "Buy")], null, _ => { });
        int factoryCalls = 0;
        var manager = new TestExecutionManager
        {
            Partition = new TestPartition(1, 4),
            UnitTestDiscovererFactory = _ =>
            {
                factoryCalls++;
                return narrowed;
            },
        };

        UnitTestDiscoverer discoverer = manager.GetUnitTestDiscoverer(sourceHandler.Object);

        factoryCalls.Should().Be(0, "a cached UID subset cannot define complete partition components");
        discoverer.Should().NotBeSameAs(narrowed);
        discoverer.Should().BeOfType<UnitTestDiscoverer>();
    }

    public void GetUnitTestDiscoverer_WithoutPartition_PreservesExistingFactoryAndSourceHandler()
    {
        var sourceHandler = new Mock<ITestSourceHandler>();
        var narrowed = new CompleteDiscoverer([Element(Cart, "Buy")], null, _ => { });
        ITestSourceHandler? suppliedSourceHandler = null;
        int factoryCalls = 0;
        var manager = new TestExecutionManager
        {
            UnitTestDiscovererFactory = handler =>
            {
                factoryCalls++;
                suppliedSourceHandler = handler;
                return narrowed;
            },
        };

        UnitTestDiscoverer discoverer = manager.GetUnitTestDiscoverer(sourceHandler.Object);

        factoryCalls.Should().Be(1);
        suppliedSourceHandler.Should().BeSameAs(sourceHandler.Object);
        discoverer.Should().BeSameAs(narrowed, "the ordinary no-partition discovery factory remains authoritative");
    }

    public async Task DiscoverTests_PartitionAndCliRunsettingsUidProviderFilters_UseIntersection()
    {
        UnitTestElement selected = Element(Checkout, "Selected");
        UnitTestElement wrongCli = Element(Checkout, "WrongCli", priority: 2);
        UnitTestElement wrongSettings = Element(Checkout, "WrongSettings", category: "Slow");
        UnitTestElement wrongFirstUid = Element(Checkout, "WrongFirstUid");
        UnitTestElement wrongSecondUid = Element(Checkout, "WrongSecondUid");
        UnitTestElement wrongPartition = Element(Cart, "WrongPartition");
        UnitTestElement[] tests = [wrongCli, wrongFirstUid, selected, wrongPartition, wrongSettings, wrongSecondUid];
        object handle = new();
        selected.HostRecordingHandle = handle;
        CompositeTestExecutionFilter contributions = new(
            TestExecutionFilterOperator.And,
            Uids(selected, wrongCli, wrongSettings, wrongSecondUid, wrongPartition),
            Uids(selected, wrongCli, wrongSettings, wrongFirstUid, wrongPartition));

        (UnitTestElement[] published, UnitTestElement[] observed, RecordingLogger logger) = await DiscoverAsync(
            tests, new TestPartition(1, 4), FastSettings, contributions, "Priority=1");

        published.Should().Equal(selected);
        published[0].Should().BeSameAs(selected);
        UnitTestElement[] expectedObserved = [selected, wrongPartition];
        observed.Should().Equal(expectedObserved, "the observer is before partition but after every user constraint");
        selected.HostRecordingHandle.Should().BeSameAs(handle);
        logger.Messages.Should().BeEmpty();
    }

    public async Task DiscoverTests_DisjointEmptyOrInvalidHostFilter_PublishesNothing()
    {
        UnitTestElement checkout = Element(Checkout, "Prepare");
        UnitTestElement cart = Element(Cart, "Buy");
        UnitTestElement[] tests = [checkout, cart];
        foreach ((ITestExecutionFilter filter, string? expression, UnitTestElement[] expectedObserved) in new (ITestExecutionFilter, string?, UnitTestElement[])[]
        {
            (Uids(cart), null, [cart]),
            (new TestNodeUidListFilter([]), null, []),
            (new NopFilter(), "ClassName=Acme.MissingTests", []),
        })
        {
            (UnitTestElement[] published, UnitTestElement[] observed, RecordingLogger logger) = await DiscoverAsync(
                tests, new TestPartition(1, 4), EmptySettings, filter, expression);
            published.Should().BeEmpty();
            observed.Should().Equal(expectedObserved);
            logger.Messages.Should().BeEmpty();
        }

        const string InvalidExpression = "FullyQualifiedName=Acme&";
        MSTestRunContext context = Context(EmptySettings, new NopFilter(), InvalidExpression);
        Action parse = () => context.GetTestCaseFilter(null, _ => null);
        string diagnostic = parse.Should().ThrowExactly<TestPlatformFormatException>().Which.Message;
        (UnitTestElement[] invalidPublished, UnitTestElement[] invalidObserved, RecordingLogger invalidLogger) = await DiscoverAsync(
            tests, new TestPartition(1, 4), EmptySettings, new NopFilter(), InvalidExpression);
        invalidPublished.Should().BeEmpty();
        invalidObserved.Should().BeEmpty();
        invalidLogger.Messages.Should().Equal((MessageLevel.Error, diagnostic));
    }

    public async Task DiscoverTests_FullDependencyClosure_PrecedesUidNarrowing()
    {
        UnitTestElement cart = Element(Cart, "Buy");
        UnitTestElement checkout = Element(Checkout, "Prepare");
        cart.Dependencies = [new(Checkout, "Prepare", proceedOnFailure: false)];
        UnitTestElement[] tests = [cart, checkout];

        // Full component => 4/4. Singleton Checkout => 1/4; singleton Cart => 2/4.
        foreach (UnitTestElement requested in tests)
        {
            foreach (int index in new[] { 1, 2, 4 })
            {
                (UnitTestElement[] published, UnitTestElement[] observed, RecordingLogger logger) = await DiscoverAsync(
                    tests, new TestPartition(index, 4), EmptySettings, Uids(requested));
                UnitTestElement[] expected = index == 4 ? [requested] : [];
                published.Should().Equal(expected);
                observed.Should().Equal(requested);
                logger.Messages.Should().BeEmpty();
            }
        }

        cart.Dependencies.Should().ContainSingle();
        checkout.Dependencies.Should().BeNull();
    }

    public async Task DiscoverTests_PrePartitionObserver_SeesUserSelectedElementsAcrossBuckets()
    {
        UnitTestElement cart = Element(Cart, "Buy");
        UnitTestElement rejectedByUser = Element(Checkout, "Slow", category: "Slow");
        UnitTestElement checkout = Element(Checkout, "Prepare");

        (UnitTestElement[] published, UnitTestElement[] observed, RecordingLogger logger) = await DiscoverAsync(
            [cart, rejectedByUser, checkout], new TestPartition(1, 4), FastSettings, new NopFilter());

        published.Should().Equal(checkout);
        observed.Should().Equal(cart, checkout);
        logger.Messages.Should().BeEmpty();
    }

    public async Task DiscoverTests_AbsentPartition_PreservesExistingFilteredElements()
    {
        UnitTestElement cart = Element(Cart, "Buy");
        UnitTestElement excluded = Element(Checkout, "Slow", category: "Slow");
        UnitTestElement checkout = Element(Checkout, "Prepare");

        (UnitTestElement[] published, UnitTestElement[] observed, RecordingLogger logger) = await DiscoverAsync(
            [cart, excluded, checkout], null, FastSettings, new NopFilter());

        published.Should().Equal(cart, checkout);
        observed.Should().Equal(cart, checkout);
        published[0].Should().BeSameAs(cart);
        published[1].Should().BeSameAs(checkout);
        logger.Messages.Should().BeEmpty();
    }

    private static UnitTestElement Element(string className, string method, string category = "Fast", int priority = 1)
        => new(new TestMethod(method, className, Source, displayName: null))
        {
            TestCategory = [category],
            Priority = priority,
        };

    private static TestNodeUidListFilter Uids(params UnitTestElement[] elements)
        => new([.. elements.Select(element => new TestNodeUid(element.TestMethod.FullyQualifiedName))]);

    private static MSTestRunContext Context(string xml, ITestExecutionFilter filter, string? expression)
    {
        var settings = new Mock<IRunSettings>();
        settings.Setup(value => value.SettingsXml).Returns(xml);
        var options = new Mock<ICommandLineOptions>();
        string[]? arguments = expression is null ? null : [expression];
        options.Setup(value => value.TryGetOptionArgumentList("filter", out arguments)).Returns(expression is not null);
        return new MSTestRunContext(options.Object, settings.Object, filter);
    }

    private static async Task<(UnitTestElement[] Published, UnitTestElement[] Observed, RecordingLogger Logger)> DiscoverAsync(
        ICollection<UnitTestElement> elements,
        TestPartition? partition,
        string xml,
        ITestExecutionFilter filter,
        string? expression = null)
    {
        List<UnitTestElement> published = [];
        List<UnitTestElement> observed = [];
        var sink = new Mock<IUnitTestElementSink>();
        sink.Setup(value => value.SendTestElementAsync(It.IsAny<UnitTestElement>()))
            .Callback<UnitTestElement>(published.Add).Returns(Task.CompletedTask);
        var logger = new RecordingLogger();
        var discoverer = new CompleteDiscoverer(elements, partition, observed.Add);
        await discoverer.DiscoverTestsInSourceAsync(
            Source, logger, sink.Object, xml, new MtpTestElementFilterProvider(Context(xml, filter, expression)), isMTP: true);
        return ([.. published], [.. observed], logger);
    }

    private sealed class CompleteDiscoverer(
        ICollection<UnitTestElement> elements,
        TestPartition? partition,
        Action<UnitTestElement> observer) : UnitTestDiscoverer(Mock.Of<ITestSourceHandler>(), partition, observer)
    {
        internal override ICollection<UnitTestElement>? GetTestElements(
            string source, string? settingsXml, bool isMTP, out List<string> warnings, out int discoveredTestCount)
        {
            warnings = [];
            discoveredTestCount = elements.Count;
            return elements;
        }
    }

    private sealed class RecordingLogger : IAdapterMessageLogger
    {
        internal List<(MessageLevel Level, string Message)> Messages { get; } = [];

        public void SendMessage(MessageLevel level, string message) => Messages.Add((level, message));
    }
}
