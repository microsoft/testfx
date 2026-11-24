// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using AwesomeAssertions;

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Resources;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.TestableImplementations;

using Moq;

using TestFramework.ForTestingMSTest;

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Discovery;

public sealed class UnitTestDiscovererMutationTests : TestContainer
{
    private const string Source = "Acme.Tests.dll";
    private const string WarningTraceFormat = "MSTestDiscoverer: Warning during discovery from {0}. {1} ";

    private readonly TestablePlatformServiceProvider _testablePlatformServiceProvider = new();

    public UnitTestDiscovererMutationTests()
    {
        MSTestSettings.Reset();
        PlatformServiceProvider.Instance = _testablePlatformServiceProvider;
    }

    protected override void Dispose(bool disposing)
    {
        if (!IsDisposed)
        {
            base.Dispose(disposing);
            PlatformServiceProvider.Instance = null;
            MSTestSettings.Reset();
        }
    }

    public async Task DiscoveryWithoutWarningsDoesNotFailWhenWarningsAreTreatedAsErrors()
    {
        PopulateTreatDiscoveryWarningsAsErrors(true);
        UnitTestElement test = Element("Acme.CartTests", "Buy");
        List<UnitTestElement> published = [];
        var discoverer = new StubDiscoverer([test], [], 1);

        await discoverer.DiscoverTestsInSourceAsync(Source, new Mock<IAdapterMessageLogger>().Object, CreateSink(published).Object, null, null, true);

        published.Should().Equal(test);
    }

    public async Task EveryDiscoveryWarningIsTracedWithItsSourceWhenInfoTracingIsEnabled()
    {
        PopulateTreatDiscoveryWarningsAsErrors(false);
        _testablePlatformServiceProvider.MockTraceLogger.SetupGet(logger => logger.IsInfoEnabled).Returns(true);
        var messageLogger = new Mock<IAdapterMessageLogger>();
        var discoverer = new StubDiscoverer([], ["first warning", "second warning"], 0);

        await discoverer.DiscoverTestsInSourceAsync(Source, messageLogger.Object, CreateSink([]).Object, null, null, true);

        _testablePlatformServiceProvider.MockTraceLogger.Verify(logger => logger.Info(WarningTraceFormat, Source, "first warning"), Times.Once);
        _testablePlatformServiceProvider.MockTraceLogger.Verify(logger => logger.Info(WarningTraceFormat, Source, "second warning"), Times.Once);
        messageLogger.Verify(logger => logger.SendMessage(MessageLevel.Warning, string.Format(CultureInfo.CurrentCulture, Resource.DiscoveryWarning, Source, "first warning")), Times.Once);
        messageLogger.Verify(logger => logger.SendMessage(MessageLevel.Warning, string.Format(CultureInfo.CurrentCulture, Resource.DiscoveryWarning, Source, "second warning")), Times.Once);
    }

    public async Task DiscoveryWarningsAreNotTracedWhenInfoTracingIsDisabled()
    {
        PopulateTreatDiscoveryWarningsAsErrors(false);
        _testablePlatformServiceProvider.MockTraceLogger.SetupGet(logger => logger.IsInfoEnabled).Returns(false);
        var messageLogger = new Mock<IAdapterMessageLogger>();
        var discoverer = new StubDiscoverer([], ["first warning"], 0);

        await discoverer.DiscoverTestsInSourceAsync(Source, messageLogger.Object, CreateSink([]).Object, null, null, true);

        _testablePlatformServiceProvider.MockTraceLogger.Verify(logger => logger.Info(It.IsAny<string>(), It.IsAny<object?[]>()), Times.Never);
        messageLogger.Verify(logger => logger.SendMessage(MessageLevel.Warning, string.Format(CultureInfo.CurrentCulture, Resource.DiscoveryWarning, Source, "first warning")), Times.Once);
    }

    public async Task DiscoveryPublishesNothingWhenTheDiscoveredTestCountIsZero()
    {
        _testablePlatformServiceProvider.MockTraceLogger.SetupGet(logger => logger.IsInfoEnabled).Returns(true);
        List<UnitTestElement> published = [];
        var discoverer = new StubDiscoverer([Element("Acme.CartTests", "Buy")], [], 0);

        await discoverer.DiscoverTestsInSourceAsync(Source, new Mock<IAdapterMessageLogger>().Object, CreateSink(published).Object, null, null, true);

        published.Should().BeEmpty();
        _testablePlatformServiceProvider.MockTraceLogger.Verify(logger => logger.Info(It.IsAny<string>(), It.IsAny<object?[]>()), Times.Never);
    }

    public async Task DiscoveryPublishesNothingWhenNoTestElementCollectionIsReturned()
    {
        _testablePlatformServiceProvider.MockTraceLogger.SetupGet(logger => logger.IsInfoEnabled).Returns(true);
        List<UnitTestElement> published = [];
        var discoverer = new StubDiscoverer(null, [], 3);

        await discoverer.DiscoverTestsInSourceAsync(Source, new Mock<IAdapterMessageLogger>().Object, CreateSink(published).Object, null, null, true);

        published.Should().BeEmpty();
        _testablePlatformServiceProvider.MockTraceLogger.Verify(logger => logger.Info(It.IsAny<string>(), It.IsAny<object?[]>()), Times.Never);
    }

    public async Task PartitionedDiscoveryPublishesOnlyTheSelectedTestsAndReportsEveryTestToTheObserver()
    {
        UnitTestElement[] tests =
        [
            Element("Acme.AlphaTests", "Run"),
            Element("Acme.BetaTests", "Run"),
            Element("Acme.GammaTests", "Run"),
            Element("Acme.DeltaTests", "Run"),
            Element("Acme.EpsilonTests", "Run"),
            Element("Acme.ZetaTests", "Run"),
        ];
        var partition = new TestPartition(1, 2);
        UnitTestElement[] selected = [.. partition.SelectTests(tests, null)];
        selected.Should().NotBeEmpty("the partition must select something for the test to observe it");
        selected.Length.Should().BeLessThan(tests.Length, "the partition must leave some tests out for the test to observe it");
        List<UnitTestElement> observed = [];
        List<UnitTestElement> published = [];
        var discoverer = new StubDiscoverer(tests, [], tests.Length, partition, observed.Add);

        await discoverer.DiscoverTestsInSourceAsync(Source, new Mock<IAdapterMessageLogger>().Object, CreateSink(published).Object, null, null, true);

        observed.Should().Equal(tests, "the observer sees every test before the partition is applied");
        published.Should().Equal(selected, "only the tests of the selected partition are published");
    }

    private static void PopulateTreatDiscoveryWarningsAsErrors(bool value)
        => MSTestSettings.PopulateSettings(
            $"<RunSettings><MSTest><TreatDiscoveryWarningsAsErrors>{(value ? "true" : "false")}</TreatDiscoveryWarningsAsErrors></MSTest></RunSettings>",
            null,
            null);

    private static UnitTestElement Element(string className, string methodName)
        => new(new TestMethod(methodName, className, Source, displayName: null));

    private static Mock<IUnitTestElementSink> CreateSink(List<UnitTestElement> published)
    {
        var sink = new Mock<IUnitTestElementSink>();
        sink.Setup(value => value.SendTestElementAsync(It.IsAny<UnitTestElement>()))
            .Callback<UnitTestElement>(published.Add)
            .Returns(Task.CompletedTask);
        return sink;
    }

    private sealed class StubDiscoverer(
        ICollection<UnitTestElement>? elements,
        string[] discoveryWarnings,
        int discoveredCount,
        TestPartition? partition = null,
        Action<UnitTestElement>? unpartitionedTestObserver = null)
        : UnitTestDiscoverer(new Mock<ITestSourceHandler>().Object, partition, unpartitionedTestObserver)
    {
        internal override ICollection<UnitTestElement>? GetTestElements(
            string source,
            string? settingsXml,
            bool isMTP,
            out List<string> warnings,
            out int discoveredTestCount)
        {
            warnings = [.. discoveryWarnings];
            discoveredTestCount = discoveredCount;
            return elements;
        }
    }
}
