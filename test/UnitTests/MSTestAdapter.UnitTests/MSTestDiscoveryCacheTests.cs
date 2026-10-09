// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if NETCOREAPP && !WIN_UI
using AwesomeAssertions;

using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Discovery;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Extensions;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.TestingPlatformAdapter;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.TestableImplementations;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using Moq;

using TestFramework.ForTestingMSTest;

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests;

public sealed class MSTestDiscoveryCacheTests : TestContainer
{
    public void NeutralResourceAssemblyMetadataAllowsCatalogHits()
    {
        using var fixture = new DiscoveryFixture(typeof(StaticA), typeof(StaticB));
        fixture.AssemblyAttributes = typeof(MSTestDiscoveryCacheTests).Assembly.GetCustomAttributesData()
            .Where(attribute => attribute.AttributeType == typeof(System.Resources.NeutralResourcesLanguageAttribute)).ToArray();
        fixture.AssemblyAttributes.Should().ContainSingle();
        using var cache = new MSTestDiscoveryCache();
        TestNodeUid[] selection = [fixture.Uid<StaticB>(nameof(StaticB.B))];

        fixture.Discover(cache, selection, out _);
        fixture.Discover(cache, selection, out int total).Select(test => test.TestMethod.Name).Should().Equal("B");

        total.Should().Be(2);
        fixture.FullScans.Should().Be(1);
    }

    public void MetadataInspectionFailureRetainsBaselineDiscoveryAndRecovery()
    {
        using var fixture = new DiscoveryFixture(typeof(StaticA), typeof(StaticB));
        fixture.MetadataFailure = new TypeLoadException("cache metadata inspection");
        using var cache = new MSTestDiscoveryCache();
        TestNodeUid[] selection = [fixture.Uid<StaticB>(nameof(StaticB.B))];

        fixture.Discover(cache, selection, out _).Select(test => test.TestMethod.Name).Should().Equal("A", "B");
        fixture.Discover(cache, selection, out int total).Select(test => test.TestMethod.Name).Should().Equal("A", "B");
        total.Should().Be(2);
        fixture.FullScans.Should().Be(2);

        fixture.MetadataFailure = null;
        fixture.Discover(cache, selection, out _);
        fixture.Discover(cache, selection, out _).Select(test => test.TestMethod.Name).Should().Equal("B");
        fixture.FullScans.Should().Be(3);
    }

    public void DisposedCacheFallsBackToCompleteBaselineDiscovery()
    {
        using var fixture = new DiscoveryFixture(typeof(StaticA), typeof(StaticB));
        using var cache = new MSTestDiscoveryCache();
        TestNodeUid[] selection = [fixture.Uid<StaticA>(nameof(StaticA.A))];
        fixture.Discover(cache, selection, out _);
        cache.Dispose();

        fixture.Discover(cache, selection, out int total).Select(test => test.TestMethod.Name).Should().Equal("A", "B");

        total.Should().Be(2);
        fixture.FullScans.Should().Be(2);
    }

    public void FirstSelectionDoesNotPoisonCatalogForLaterSelection()
    {
        using var fixture = new DiscoveryFixture(typeof(StaticA), typeof(StaticB));
        using var cache = new MSTestDiscoveryCache();

        UnitTestElement[] first = fixture.Discover(cache, [fixture.Uid<StaticA>(nameof(StaticA.A))], out int firstCount);
        // A cold request must scan the full source; filtering happens after this seam.
        first.Select(test => test.TestMethod.Name).Should().Equal("A", "B");
        firstCount.Should().Be(2);

        UnitTestElement[] second = fixture.Discover(cache, [fixture.Uid<StaticB>(nameof(StaticB.B))], out int secondCount);
        second.Select(test => test.TestMethod.Name).Should().Equal("B");
        second.Single().GetTestId().ToString().Should().Be(fixture.Uid<StaticB>(nameof(StaticB.B)).Value);
        secondCount.Should().Be(2);
        fixture.FullScans.Should().Be(1);
    }

    public void ColdResultsAndRepeatedHitsHaveIndependentMutableMetadata()
    {
        using var fixture = new DiscoveryFixture(typeof(StaticA), typeof(StaticB));
        using var cache = new MSTestDiscoveryCache();
        TestNodeUid[] selection = [fixture.Uid<StaticA>(nameof(StaticA.A))];

        UnitTestElement cold = fixture.Discover(cache, selection, out _).First();
        MutateMetadata(cold);
        UnitTestElement firstHit = fixture.Discover(cache, selection, out _).Single();
        AssertOriginalMetadata(firstHit);
        firstHit.Should().NotBeSameAs(cold);
        firstHit.TestMethod.Should().NotBeSameAs(cold.TestMethod);
        MutateMetadata(firstHit);

        UnitTestElement secondHit = fixture.Discover(cache, selection, out int total).Single();
        AssertOriginalMetadata(secondHit);
        secondHit.Should().NotBeSameAs(firstHit);
        secondHit.TestMethod.Should().NotBeSameAs(firstHit.TestMethod);
        secondHit.TestCategory.Should().NotBeSameAs(firstHit.TestCategory);
        secondHit.Traits.Should().NotBeSameAs(firstHit.Traits);
        secondHit.ResourceLocks.Should().NotBeSameAs(firstHit.ResourceLocks);
        secondHit.Dependencies.Should().NotBeSameAs(firstHit.Dependencies);
        secondHit.WorkItemIds.Should().NotBeSameAs(firstHit.WorkItemIds);
        secondHit.GetTestId().ToString().Should().Be(selection[0].Value);
        total.Should().Be(2);
        fixture.FullScans.Should().Be(1);
    }

    public void DuplicateAndUnknownSelectionsPreserveBaselineTypeOrder()
    {
        using var fixture = new DiscoveryFixture(typeof(StaticB), typeof(StaticA));
        using var cache = new MSTestDiscoveryCache();
        UnitTestElement[] baseline = fixture.Discover(cache, [], out _);

        UnitTestElement[] selected = fixture.Discover(
            cache,
            [fixture.Uid<StaticA>(nameof(StaticA.A)), UnknownUid(), fixture.Uid<StaticB>(nameof(StaticB.B)), fixture.Uid<StaticA>(nameof(StaticA.A))],
            out int total);

        baseline.Select(test => test.TestMethod.Name).Should().Equal("B", "A");
        selected.Select(test => test.TestMethod.Name).Should().Equal("B", "A");
        total.Should().Be(2);
        fixture.FullScans.Should().Be(1);
    }

    public void GuidMultimapRetainsAllCatalogEntriesWithTheSameUid()
    {
        // Repeating a type deterministically supplies two catalog entries with the same GUID.
        // This checks the multimap without relying on a hash collision or unrelated discovery.
        using var fixture = new DiscoveryFixture(typeof(StaticA), typeof(StaticA));
        using var cache = new MSTestDiscoveryCache();
        UnitTestElement[] baseline = fixture.Discover(cache, [], out _);
        TestNodeUid uid = fixture.Uid<StaticA>(nameof(StaticA.A));

        UnitTestElement[] selected = fixture.Discover(cache, [uid, uid], out int total);

        baseline.Should().HaveCount(2);
        selected.Select(test => test.TestMethod.Name).Should().Equal("A", "A");
        selected.Select(test => test.GetTestId().ToString()).Should().Equal(uid.Value, uid.Value);
        selected[0].Should().NotBeSameAs(selected[1]);
        total.Should().Be(2);
        fixture.FullScans.Should().Be(1);
    }

    public void EmptySelectionReturnsNoReusableCandidatesButRetainsFullCount()
    {
        using var fixture = new DiscoveryFixture(typeof(StaticA), typeof(StaticB));
        using var cache = new MSTestDiscoveryCache();
        fixture.Discover(cache, [], out _);

        fixture.Discover(cache, [], out int total).Should().BeEmpty();

        total.Should().Be(2);
        fixture.FullScans.Should().Be(1);
    }

    public void UnknownSelectionReturnsNoReusableCandidatesButRetainsFullCount()
    {
        using var fixture = new DiscoveryFixture(typeof(StaticA), typeof(StaticB));
        using var cache = new MSTestDiscoveryCache();
        fixture.Discover(cache, [], out _);

        fixture.Discover(cache, [UnknownUid()], out int total).Should().BeEmpty();

        total.Should().Be(2);
        fixture.FullScans.Should().Be(1);
    }

    public void UnselectedDynamicProviderProducesFreshRowsInBaselineTypeOrder()
    {
        using var fixture = new DiscoveryFixture(typeof(StaticB), typeof(LiveData), typeof(StaticA));
        using var cache = new MSTestDiscoveryCache();
        TestNodeUid[] selection = [fixture.Uid<StaticA>(nameof(StaticA.A)), fixture.Uid<StaticB>(nameof(StaticB.B))];
        UnitTestElement[] cold = fixture.Discover(cache, selection, out int coldCount);
        cold.Select(test => test.TestMethod.Name).Should().Equal("B", "Data", "A");
        cold.Single(test => test.TestMethod.Name == "Data").TestMethod.ActualData.Should().Equal(10);
        coldCount.Should().Be(3);

        LiveData.Value = 20;
        LiveData.RowCount = 2;
        UnitTestElement[] hit = fixture.Discover(cache, selection, out int hitCount);

        hit.Select(test => test.TestMethod.Name).Should().Equal("B", "Data", "Data", "A");
        hit.Where(test => test.TestMethod.Name == "Data")
            .Select(test => test.TestMethod.ActualData![0]).Should().Equal(20, 21);
        hit.Where(test => test.TestMethod.Name == "Data")
            .Select(test => test.TestMethod.TestCaseIndex).Should().OnlyHaveUniqueItems();
        LiveData.Calls.Should().Be(2);
        hitCount.Should().Be(4);
        fixture.FullScans.Should().Be(1);
    }

    public void EmptyAndUnknownSelectionsStillInvokeLiveProviders()
    {
        using var fixture = new DiscoveryFixture(typeof(StaticA), typeof(LiveData));
        using var cache = new MSTestDiscoveryCache();
        fixture.Discover(cache, [], out _);

        LiveData.Value = 30;
        UnitTestElement[] emptySelection = fixture.Discover(cache, [], out int emptyCount);
        emptySelection.Select(test => test.TestMethod.Name).Should().Equal("Data");
        emptySelection.Single().TestMethod.ActualData.Should().Equal(30);
        LiveData.Calls.Should().Be(2);

        LiveData.Value = 40;
        UnitTestElement[] unknownSelection = fixture.Discover(cache, [UnknownUid()], out int unknownCount);
        unknownSelection.Select(test => test.TestMethod.Name).Should().Equal("Data");
        unknownSelection.Single().TestMethod.ActualData.Should().Equal(40);
        LiveData.Calls.Should().Be(3);
        emptyCount.Should().Be(2);
        unknownCount.Should().Be(2);
        fixture.FullScans.Should().Be(1);
    }

    public void DeploymentClassMetadataIsRediscoveredEvenWhenUnselected()
    {
        using var fixture = new DiscoveryFixture(typeof(StaticA), typeof(LiveDeployment));
        using var cache = new MSTestDiscoveryCache();
        string deploymentPath = "first.txt";
        int deploymentReads = 0;
        fixture.Provider.MockTestDeployment
            .Setup(deployment => deployment.GetDeploymentItems(
                It.IsAny<MethodInfo>(),
                It.Is<Type>(type => type.FullName == typeof(LiveDeployment).FullName),
                It.IsAny<ICollection<string>>()))
            .Returns(() =>
            {
                deploymentReads++;
                return [new KeyValuePair<string, string>(deploymentPath, "output")];
            });
        fixture.Discover(cache, [], out _);
        deploymentReads.Should().Be(1);

        deploymentPath = "second.txt";
        UnitTestElement[] hit = fixture.Discover(cache, [], out int total);

        hit.Select(test => test.TestMethod.Name).Should().Equal("Deployed");
        hit.Single().DeploymentItems.Should().Equal([new KeyValuePair<string, string>("second.txt", "output")]);
        deploymentReads.Should().Be(2);
        total.Should().Be(2);
        fixture.FullScans.Should().Be(1);
    }

    public void CustomAttributeGetterRemainsLiveWithoutAnyDataSource()
    {
        using var fixture = new DiscoveryFixture(typeof(StaticA), typeof(LiveCustomAttribute));
        using var cache = new MSTestDiscoveryCache();
        UnitTestElement cold = fixture.Discover(cache, [], out _).Single(test => test.TestMethod.Name == "Custom");
        cold.TestCategory.Should().Equal("10");
        cold.TestMethod.DataType.Should().Be(DynamicDataType.None);
        cold.TestMethod.ActualData.Should().BeNull();

        LiveData.Value = 20;
        UnitTestElement[] hit = fixture.Discover(cache, [], out int total);

        hit.Select(test => test.TestMethod.Name).Should().Equal("Custom");
        hit.Single().TestCategory.Should().Equal("20");
        total.Should().Be(2);
        fixture.FullScans.Should().Be(1);
    }

    public void InheritedCustomAttributeGetterRemainsLiveWhenUnselected()
    {
        using var fixture = new DiscoveryFixture(typeof(StaticA), typeof(LiveInheritedAttribute));
        using var cache = new MSTestDiscoveryCache();
        UnitTestElement cold = fixture.Discover(cache, [], out _).Single(test => test.TestMethod.Name == "Custom");
        cold.TestMethod.FullClassName.Should().Be(typeof(LiveInheritedAttribute).FullName);
        cold.TestCategory.Should().Equal("10");

        LiveData.Value = 20;
        UnitTestElement[] hit = fixture.Discover(cache, [], out int total);

        hit.Should().ContainSingle().Which.TestCategory.Should().Equal("20");
        total.Should().Be(2);
        fixture.FullScans.Should().Be(1);
    }

    public void ChangedSettingsXmlInvalidatesTheCatalog()
    {
        using var fixture = new DiscoveryFixture(typeof(StaticA), typeof(StaticB));
        using var cache = new MSTestDiscoveryCache();
        const string firstSettings = "<RunSettings />";
        const string secondSettings = "<RunSettings><MSTest><MapInconclusiveToFailed>true</MapInconclusiveToFailed></MSTest></RunSettings>";
        TestNodeUid[] selection = [fixture.Uid<StaticA>(nameof(StaticA.A))];
        fixture.Discover(cache, selection, out _, settingsXml: firstSettings);
        fixture.Discover(cache, selection, out _, settingsXml: firstSettings)
            .Select(test => test.TestMethod.Name).Should().Equal("A");
        fixture.FullScans.Should().Be(1);

        fixture.Discover(cache, selection, out int total, settingsXml: secondSettings)
            .Select(test => test.TestMethod.Name).Should().Equal("A", "B");
        fixture.FullScans.Should().Be(2);
        fixture.Discover(cache, selection, out _, settingsXml: secondSettings)
            .Select(test => test.TestMethod.Name).Should().Equal("A");
        total.Should().Be(2);
        fixture.FullScans.Should().Be(2);
    }

    public void AlreadyCancelledRequestDoesNotPreventLaterDiscovery()
    {
        using var fixture = new DiscoveryFixture(typeof(StaticA), typeof(StaticB));
        using var cache = new MSTestDiscoveryCache();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        fixture.Discover(cache, [], out _, cancellationToken: cancellation.Token)
            .Select(test => test.TestMethod.Name).Should().Equal("A", "B");
        fixture.FullScans.Should().Be(1);

        fixture.Discover(cache, [], out int total).Select(test => test.TestMethod.Name).Should().Equal("A", "B");
        total.Should().Be(2);
        fixture.FullScans.Should().Be(2);
    }

    public void CancellationDuringColdScanDoesNotPublishAPartialCatalog()
    {
        using var fixture = new DiscoveryFixture(typeof(StaticA), typeof(StaticB));
        using var cache = new MSTestDiscoveryCache();
        using var cancellation = new CancellationTokenSource();
        fixture.OnFullScan = cancellation.Cancel;

        fixture.Discover(cache, [], out _, cancellationToken: cancellation.Token)
            .Select(test => test.TestMethod.Name).Should().Equal("A", "B");
        fixture.FullScans.Should().Be(1);
        fixture.OnFullScan = null;

        TestNodeUid[] selection = [fixture.Uid<StaticB>(nameof(StaticB.B))];
        fixture.Discover(cache, selection, out int total).Select(test => test.TestMethod.Name).Should().Equal("A", "B");
        fixture.FullScans.Should().Be(2);
        fixture.Discover(cache, selection, out _).Select(test => test.TestMethod.Name).Should().Equal("B");
        total.Should().Be(2);
        fixture.FullScans.Should().Be(2);
    }

    public void NewCacheOwnerDoesNotReuseAnotherOwnersCatalog()
    {
        using var fixture = new DiscoveryFixture(typeof(StaticA), typeof(StaticB));
        using var firstOwner = new MSTestDiscoveryCache();
        using var secondOwner = new MSTestDiscoveryCache();
        TestNodeUid[] selection = [fixture.Uid<StaticA>(nameof(StaticA.A))];
        fixture.Discover(firstOwner, selection, out _);
        fixture.Discover(firstOwner, selection, out _).Select(test => test.TestMethod.Name).Should().Equal("A");

        fixture.Discover(secondOwner, selection, out int total).Select(test => test.TestMethod.Name).Should().Equal("A", "B");
        firstOwner.Dispose();
        fixture.Discover(secondOwner, selection, out _).Select(test => test.TestMethod.Name).Should().Equal("A");

        using var newOwner = new MSTestDiscoveryCache();
        fixture.Discover(newOwner, selection, out _).Select(test => test.TestMethod.Name).Should().Equal("A", "B");
        total.Should().Be(2);
        fixture.FullScans.Should().Be(3);
    }

    public void RemovingSourceEvictsItsCatalog()
    {
        using var fixture = new DiscoveryFixture(typeof(StaticA), typeof(StaticB));
        using var cache = new MSTestDiscoveryCache();
        TestNodeUid[] selection = [fixture.Uid<StaticA>(nameof(StaticA.A))];
        fixture.Discover(cache, selection, out _);
        cache.SetSources([fixture.Source]);
        fixture.Discover(cache, selection, out _).Select(test => test.TestMethod.Name).Should().Equal("A");
        cache.SetSources([]);

        fixture.Discover(cache, selection, out int total).Select(test => test.TestMethod.Name).Should().Equal("A", "B");
        total.Should().Be(2);
        fixture.FullScans.Should().Be(2);
    }

    public void DiscoveryWarningsRejectTheEntireCatalog()
    {
        using var fixture = new DiscoveryFixture(typeof(StaticA), typeof(LiveDeployment));
        using var cache = new MSTestDiscoveryCache();
        TestNodeUid[] selection = [fixture.Uid<StaticA>(nameof(StaticA.A))];
        fixture.Provider.MockTestDeployment
            .Setup(deployment => deployment.GetDeploymentItems(
                It.IsAny<MethodInfo>(), It.IsAny<Type>(), It.IsAny<ICollection<string>>()))
            .Returns((MethodInfo method, Type type, ICollection<string> warnings) =>
            {
                if (type.FullName == typeof(LiveDeployment).FullName)
                {
                    warnings.Add("discovery warning");
                }

                return [];
            });

        UnitTestElement[] first = fixture.DiscoverWithWarnings(cache, selection, out List<string> firstWarnings, out int firstCount);
        UnitTestElement[] second = fixture.DiscoverWithWarnings(cache, selection, out List<string> secondWarnings, out int secondCount);

        first.Select(test => test.TestMethod.Name).Should().Equal("A", "Deployed");
        second.Select(test => test.TestMethod.Name).Should().Equal("A", "Deployed");
        firstWarnings.Should().Equal("discovery warning");
        secondWarnings.Should().Equal(firstWarnings);
        firstCount.Should().Be(2);
        secondCount.Should().Be(2);
        fixture.FullScans.Should().Be(2);
    }

    public void CacheHitReplaysFullSourceAttributeTelemetryIncludingUnselectedTests()
    {
        using var fixture = new DiscoveryFixture(typeof(StaticA), typeof(LiveData), typeof(StaticB));
        using var cache = new MSTestDiscoveryCache();
        MSTestTelemetryDataCollector? previous = MSTestTelemetryDataCollector.Current;
        try
        {
            var coldCollector = new MSTestTelemetryDataCollector();
            MSTestTelemetryDataCollector.Current = coldCollector;
            fixture.Discover(cache, [], out _);
            Dictionary<string, long> coldCounts = coldCollector.GetDiscoveryAttributeCounts();
            coldCounts[nameof(TestMethodAttribute)].Should().Be(3);
            coldCounts[nameof(DynamicDataAttribute)].Should().Be(1);
            coldCounts[nameof(TestCategoryAttribute)].Should().Be(1);

            var hitCollector = new MSTestTelemetryDataCollector();
            MSTestTelemetryDataCollector.Current = hitCollector;
            fixture.Discover(cache, [fixture.Uid<StaticA>(nameof(StaticA.A))], out int total)
                .Select(test => test.TestMethod.Name).Should().Equal("A", "Data");

            hitCollector.GetDiscoveryAttributeCounts().Should().BeEquivalentTo(coldCounts);
            total.Should().Be(3);
            fixture.FullScans.Should().Be(1);
        }
        finally
        {
            MSTestTelemetryDataCollector.Current = previous;
        }
    }

    public async Task ConcurrentHitsReturnIndependentMetadataWithoutChangingTheCatalog()
    {
        using var fixture = new DiscoveryFixture(typeof(StaticA), typeof(StaticB));
        using var cache = new MSTestDiscoveryCache();
        TestNodeUid[] selection = [fixture.Uid<StaticA>(nameof(StaticA.A))];
        fixture.Discover(cache, selection, out _);

        await Task.WhenAll(Enumerable.Range(0, 16).Select(_ => Task.Run(() =>
        {
            UnitTestElement hit = fixture.Discover(cache, selection, out int total).Single();
            AssertOriginalMetadata(hit);
            total.Should().Be(2);
            MutateMetadata(hit);
        })));

        AssertOriginalMetadata(fixture.Discover(cache, selection, out _).Single());
        fixture.FullScans.Should().Be(1);
    }

    public void LiveDiscoveryWarningsDoNotReplaceOrMutateTheCatalog()
    {
        using var fixture = new DiscoveryFixture(typeof(StaticA), typeof(LiveDeployment));
        using var cache = new MSTestDiscoveryCache();
        bool warn = false;
        fixture.Provider.MockTestDeployment
            .Setup(deployment => deployment.GetDeploymentItems(
                It.IsAny<MethodInfo>(), It.IsAny<Type>(), It.IsAny<ICollection<string>>()))
            .Returns((MethodInfo method, Type type, ICollection<string> warnings) =>
            {
                if (warn && type.FullName == typeof(LiveDeployment).FullName)
                {
                    warnings.Add("live deployment warning");
                }

                return [];
            });
        fixture.Discover(cache, [], out _);
        warn = true;

        fixture.DiscoverWithWarnings(cache, [], out List<string> warnings, out int total)
            .Select(test => test.TestMethod.Name).Should().Equal("Deployed");
        warnings.Should().Equal("live deployment warning");
        total.Should().Be(2);
        warn = false;

        UnitTestElement[] recovered = fixture.Discover(cache, [fixture.Uid<StaticA>(nameof(StaticA.A))], out _);
        recovered.Select(test => test.TestMethod.Name).Should().Equal("A", "Deployed");
        AssertOriginalMetadata(recovered[0]);
        fixture.FullScans.Should().Be(1);
    }

    private static TestNodeUid UnknownUid() => new("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");

    private static void MutateMetadata(UnitTestElement element)
    {
        element.TestCategory![0] = "changed";
        element.Traits![0] = new TestTrait("changed", "changed");
        element.WorkItemIds![0] = "changed";
        element.ResourceLocks![0] = new ResourceLockInfo("changed", ResourceAccessMode.ReadWrite);
        element.Dependencies![0] = new TestDependencyInfo(null, "changed", true);
        element.Priority = 99;
        element.TestMethod.DisplayName = "changed";
        element.TestMethod.ActualData = [99];
        element.HostRecordingHandle = new object();
        element.ExecutionContextProperties = new Dictionary<string, object?> { ["run"] = 99 };
        element.SupportsExecutionActivityLease = false;
    }

    private static void AssertOriginalMetadata(UnitTestElement element)
    {
        element.TestCategory.Should().Equal("original");
        element.Traits!.Select(trait => (trait.Name, trait.Value)).Should().Equal(("key", "value"), ("Priority", "7"));
        element.WorkItemIds.Should().Equal("42");
        element.ResourceLocks.Should().ContainSingle().Which.Resource.Should().Be("cache-fixture");
        element.Dependencies.Should().ContainSingle().Which.TargetMethodName.Should().Be(nameof(StaticB.B));
        element.Priority.Should().Be(7);
        element.TestMethod.DisplayName.Should().Be(nameof(StaticA.A));
        element.TestMethod.ActualData.Should().BeNull();
        element.TestMethod.SerializedData.Should().BeNull();
        element.TestMethod.DataType.Should().Be(DynamicDataType.None);
        element.HostRecordingHandle.Should().BeNull();
        element.ExecutionContextProperties.Should().BeNull();
        element.ExecutionActivityLease.Should().BeNull();
        element.SupportsExecutionActivityLease.Should().BeTrue();
    }

    // These are discovery-only fixtures, not TestContainers. The mocked assembly below
    // exposes just the explicitly arranged types, never the whole unit-test assembly.
    [TestClass]
    public class StaticA
    {
        [TestMethod]
        [TestCategory("original")]
        [TestProperty("key", "value")]
        [WorkItem(42)]
        [Priority(7)]
        [ResourceLock("cache-fixture", Mode = ResourceAccessMode.Read)]
        [DependsOn(typeof(StaticB), nameof(StaticB.B))]
        public void A()
        {
        }
    }

    [TestClass]
    public class StaticB
    {
        [TestMethod]
        public void B()
        {
        }
    }

    [TestClass]
    public class LiveData
    {
        internal static int Value { get; set; } = 10;

        internal static int RowCount { get; set; } = 1;

        internal static int Calls { get; set; }

        public static IEnumerable<object[]> Rows()
        {
            Calls++;
            return Enumerable.Range(0, RowCount).Select(index => new object[] { Value + index }).ToArray();
        }

        [TestMethod]
        [DynamicData(nameof(Rows), DynamicDataSourceType.Method)]
        public void Data(int value) => _ = value;
    }

    [TestClass]
    [DeploymentItem("fixture.txt")]
    public class LiveDeployment
    {
        [TestMethod]
        public void Deployed()
        {
        }
    }

    [TestClass]
    public class LiveCustomAttribute
    {
        [TestMethod]
        [LiveCategory]
        public void Custom()
        {
        }
    }

    [TestClass]
    public class LiveInheritedAttribute : LiveCustomAttribute
    {
    }

    [AttributeUsage(AttributeTargets.Method)]
    private sealed class LiveCategoryAttribute : TestCategoryBaseAttribute
    {
        public override IList<string> TestCategories => [LiveData.Value.ToString(CultureInfo.InvariantCulture)];
    }

    private sealed class DiscoveryFixture : IDisposable
    {
        private readonly IPlatformServiceProvider _previousProvider = PlatformServiceProvider.Instance;
        private readonly MSTestTelemetryDataCollector? _previousTelemetry = MSTestTelemetryDataCollector.Current;
        private readonly (int Value, int RowCount, int Calls) _previousData = (LiveData.Value, LiveData.RowCount, LiveData.Calls);
        private readonly Mock<Assembly> _assembly = new();
        private readonly Mock<ITestSourceHandler> _sourceHandler = new();
        private bool _typeEnumerationStarted;

        internal DiscoveryFixture(params Type[] types)
        {
            Provider.SetReflectionOperations(new ReflectionOperations());
            _assembly.SetupGet(assembly => assembly.Location).Returns(Source);
            _assembly.SetupGet(assembly => assembly.IsDynamic).Returns(false);
            _assembly.SetupGet(assembly => assembly.IsCollectible).Returns(false);
            _assembly.Setup(assembly => assembly.GetCustomAttributesData()).Returns(() =>
                MetadataFailure is { } failure ? throw failure : AssemblyAttributes);
            _assembly.Setup(assembly => assembly.GetCustomAttributes(It.IsAny<Type>(), It.IsAny<bool>())).Returns(Array.Empty<Attribute>());
            _assembly.Setup(assembly => assembly.GetReferencedAssemblies()).Returns([]);
            Type[] scopedTypes = types.Select(type => (Type)new ScopedType(type, _assembly.Object)).ToArray();
            _assembly.Setup(assembly => assembly.GetTypes()).Returns(() =>
            {
                // TypeCache also asks for assembly types while resolving lifecycle metadata.
                // Count only the initial source catalog scan, before its first type is visited.
                if (!_typeEnumerationStarted)
                {
                    FullScans++;
                    OnFullScan?.Invoke();
                }

                return scopedTypes;
            });
            _assembly.Setup(assembly => assembly.GetType(It.IsAny<string>()))
                .Returns((string name) => scopedTypes.FirstOrDefault(type => type.FullName == name));
            _assembly.Setup(assembly => assembly.GetName()).Returns(typeof(MSTestDiscoveryCacheTests).Assembly.GetName());
            Provider.MockFileOperations.Setup(files => files.GetFullFilePath(Source)).Returns(Source);
            Provider.MockFileOperations.Setup(files => files.DoesFileExist(Source)).Returns(true);
            Provider.MockFileOperations.Setup(files => files.LoadAssembly(Source)).Returns(_assembly.Object);
            Provider.MockTestSourceHost.Setup(host => host.CreateInstanceForType(typeof(AssemblyEnumerator), It.IsAny<object[]>()))
                .Returns(() =>
                {
                    _typeEnumerationStarted = false;
                    return new ScopedEnumerator(() => _typeEnumerationStarted = true);
                });
            _sourceHandler.Setup(handler => handler.IsAssemblyReferenced(It.IsAny<AssemblyName>(), Source)).Returns(true);
            LiveData.Value = 10;
            LiveData.RowCount = 1;
            LiveData.Calls = 0;
            PlatformServiceProvider.Instance = Provider;
            MSTestTelemetryDataCollector.Current = null;
        }

        internal TestablePlatformServiceProvider Provider { get; } = new();

        internal string Source { get; } = typeof(MSTestDiscoveryCacheTests).Assembly.Location;

        internal int FullScans { get; private set; }

        internal Action? OnFullScan { get; set; }

        internal IList<CustomAttributeData> AssemblyAttributes { get; set; } = Array.Empty<CustomAttributeData>();

        internal Exception? MetadataFailure { get; set; }

        internal TestNodeUid Uid<T>(string method)
        {
            var element = new UnitTestElement(new TestMethod(method, null, method, typeof(T).FullName!, Source, null, string.Empty));
            return new TestNodeUid(element.GetTestId().ToString());
        }

        internal UnitTestElement[] Discover(
            MSTestDiscoveryCache cache,
            TestNodeUid[] selection,
            out int total,
            string? settingsXml = null,
            CancellationToken cancellationToken = default)
        {
            UnitTestElement[] tests = DiscoverWithWarnings(cache, selection, out List<string> warnings, out total, settingsXml, cancellationToken);
            warnings.Should().BeEmpty();
            return tests;
        }

        internal UnitTestElement[] DiscoverWithWarnings(
            MSTestDiscoveryCache cache,
            TestNodeUid[] selection,
            out List<string> warnings,
            out int total,
            string? settingsXml = null,
            CancellationToken cancellationToken = default)
        {
            MSTestDiscoveryCache.IsSupported(_assembly.Object).Should().BeTrue("these tests must exercise the cache, not its fallback");
            _assembly.Object.Location.Should().Be(Source);
            UnitTestDiscoverer discoverer = cache.CreateDiscoverer(_sourceHandler.Object, [_assembly.Object], selection, cancellationToken);
            discoverer.GetType().Name.Should().Be("CachedDiscoverer");
            ICollection<UnitTestElement>? tests = discoverer.GetTestElements(Source, settingsXml, isMTP: true, out warnings, out total);
            tests.Should().NotBeNull();
            return tests!.ToArray();
        }

        public void Dispose()
        {
            PlatformServiceProvider.Instance = _previousProvider;
            MSTestTelemetryDataCollector.Current = _previousTelemetry;
            (LiveData.Value, LiveData.RowCount, LiveData.Calls) = _previousData;
        }
    }

    // TypeCache uses classType.Assembly to discover lifecycle methods. Redirect that
    // secondary scan to the same isolated assembly, rather than the real test assembly.
    private sealed class ScopedType(Type type, Assembly assembly) : TypeDelegator(type)
    {
        public override Assembly Assembly => assembly;

        public override Type? DeclaringType => UnderlyingSystemType.DeclaringType;

        public override IList<CustomAttributeData> GetCustomAttributesData() => UnderlyingSystemType.GetCustomAttributesData();
    }

    private sealed class ScopedEnumerator(Action onTypeEnumeration) : AssemblyEnumerator
    {
        internal override TypeEnumerator GetTypeEnumerator(Type type, string assemblyFileName, bool discoverInternals)
        {
            onTypeEnumeration();
            return base.GetTypeEnumerator(type, assemblyFileName, discoverInternals);
        }
    }
}
#endif
