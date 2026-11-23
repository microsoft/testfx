// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Reflection.Emit;

using AwesomeAssertions;

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Execution;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Resources;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.TestableImplementations;

using Moq;

using TestFramework.ForTestingMSTest;

#pragma warning disable MSTESTEXP // Programmatic test-filter API is experimental; exercised here under test.

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.Execution;

public class TypeCacheFilterDiscoveryMutationTests : TestContainer
{
    // Only keeps a regression from hanging the suite: a passing run never waits for it to elapse.
    private static readonly TimeSpan FailSafeTimeout = TimeSpan.FromSeconds(30);

    private readonly TestablePlatformServiceProvider _testablePlatformServiceProvider;

    public TypeCacheFilterDiscoveryMutationTests()
    {
        _testablePlatformServiceProvider = new TestablePlatformServiceProvider();
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

    // The cache promises that discovery runs once per source "even under contention". Park the first caller
    // inside the assembly load, then ask for the same source from a second thread: the second caller must
    // wait for the first result instead of failing or loading the assembly again.
    public void ConcurrentRequestsForTheSameSourceWaitForTheSingleAssemblyLoad()
    {
        const string Source = "contended-filter-source";
        Assembly assembly = CreateDynamicAssemblyWithTestFilterProviderAttribute();
        using var firstLoadStarted = new ManualResetEventSlim();
        using var firstLoadMayComplete = new ManualResetEventSlim();
        using var secondCallerRunning = new ManualResetEventSlim();
        int loadCount = 0;
        _testablePlatformServiceProvider.MockFileOperations
            .Setup(value => value.LoadAssembly(Source))
            .Returns(() =>
            {
                if (Interlocked.Increment(ref loadCount) == 1)
                {
                    firstLoadStarted.Set();
                    firstLoadMayComplete.Wait(FailSafeTimeout);
                }

                return assembly;
            });
        var cache = new TypeCache();
        ITestFilter? firstFilter = null;
        ITestFilter? secondFilter = null;
        Exception? firstFailure = null;
        Exception? secondFailure = null;
        var firstCaller = new Thread(() =>
        {
            try
            {
                firstFilter = cache.GetOrLoadTestFilter(Source);
            }
            catch (Exception ex)
            {
                firstFailure = ex;
            }
        })
        {
            IsBackground = true,
        };
        var secondCaller = new Thread(() =>
        {
            secondCallerRunning.Set();
            try
            {
                secondFilter = cache.GetOrLoadTestFilter(Source);
            }
            catch (Exception ex)
            {
                secondFailure = ex;
            }
        })
        {
            IsBackground = true,
        };

        firstCaller.Start();
        firstLoadStarted.Wait(FailSafeTimeout).Should().BeTrue();
        secondCaller.Start();
        secondCallerRunning.Wait(FailSafeTimeout).Should().BeTrue();

        // The second caller is either parked behind the first load or already gone: both states are
        // observable immediately, so there is nothing to sleep for.
        SpinWait.SpinUntil(() => IsBlockedOrFinished(secondCaller), FailSafeTimeout);
        firstLoadMayComplete.Set();
        firstCaller.Join(FailSafeTimeout).Should().BeTrue();
        secondCaller.Join(FailSafeTimeout).Should().BeTrue();

        firstFailure.Should().BeNull();
        secondFailure.Should().BeNull();
        loadCount.Should().Be(1);
        firstFilter.Should().BeOfType<NoOpFilter>();
        secondFilter.Should().BeSameAs(firstFilter);
    }

    // Probing is metadata-only: an assembly whose attribute data shows no marker must be answered with
    // "no filter" without asking the reflection layer to materialize any marker attribute.
    public void AssemblyWithoutMarkerMetadataIsNotAskedToMaterializeMarkers()
    {
        const string Source = "no-marker-metadata";
        AssemblyBuilder assembly = DefineDynamicAssembly();
        _testablePlatformServiceProvider.SetupMockReflectionOperations();
        _testablePlatformServiceProvider.MockFileOperations.Setup(value => value.LoadAssembly(Source)).Returns(assembly);
        _testablePlatformServiceProvider.MockReflectionOperations
            .Setup(value => value.GetCustomAttributes(assembly, typeof(TestFilterProviderAttribute)))
            .Returns([new TestFilterProviderAttribute(typeof(NoOpFilter))]);

        new TypeCache().GetOrLoadTestFilter(Source).Should().BeNull();

        _testablePlatformServiceProvider.MockReflectionOperations.Verify(
            value => value.GetCustomAttributes(assembly, typeof(TestFilterProviderAttribute)),
            Times.Never);
    }

    public void MultipleMarkersOnAnAssemblyWhoseNameCannotBeReadAreReportedAsUnknown()
    {
        const string Source = "multiple-markers-unreadable-name";
        var assembly = new NameUnavailableAssembly(CreateDynamicAssemblyWithTestFilterProviderAttribute());
        var marker = new TestFilterProviderAttribute(typeof(NoOpFilter));
        _testablePlatformServiceProvider.SetupMockReflectionOperations();
        _testablePlatformServiceProvider.MockFileOperations.Setup(value => value.LoadAssembly(Source)).Returns(assembly);
        _testablePlatformServiceProvider.MockReflectionOperations
            .Setup(value => value.GetCustomAttributes(assembly, typeof(TestFilterProviderAttribute)))
            .Returns([marker, marker]);
        Action load = () => new TypeCache().GetOrLoadTestFilter(Source);

        load.Should().ThrowExactly<TypeInspectionException>().Which.Message.Should().Be(string.Format(
            CultureInfo.CurrentCulture,
            Resource.UTA_TestFilterProviderMultipleDeclared,
            "<unknown>"));
    }

    public void MarkerMaterializationFailureOnAnAssemblyWhoseNameCannotBeReadIsReportedAsUnknown()
    {
        const string Source = "unmaterializable-marker-unreadable-name";
        var assembly = new NameUnavailableAssembly(CreateDynamicAssemblyWithTestFilterProviderAttribute());
        var cause = new TypeLoadException("filter dependency unavailable");
        _testablePlatformServiceProvider.SetupMockReflectionOperations();
        _testablePlatformServiceProvider.MockFileOperations.Setup(value => value.LoadAssembly(Source)).Returns(assembly);
        _testablePlatformServiceProvider.MockReflectionOperations
            .Setup(value => value.GetCustomAttributes(assembly, typeof(TestFilterProviderAttribute)))
            .Throws(cause);
        Action load = () => new TypeCache().GetOrLoadTestFilter(Source);

        TypeInspectionException failure = load.Should().ThrowExactly<TypeInspectionException>().Which;

        failure.Message.Should().Be(string.Format(
            CultureInfo.CurrentCulture,
            Resource.UTA_TestFilterProviderLoadFailed,
            "<unknown>",
            "filter dependency unavailable"));
        failure.InnerException.Should().BeSameAs(cause);
    }

#if NET
    // One marker of each shape is two declarations: the count has to add both shapes up.
    public void NonGenericAndGenericMarkersTogetherAreRejectedAsMultipleDeclarations()
    {
        const string Source = "mixed-filter-markers";
        AssemblyBuilder assembly = DefineDynamicAssembly();
        assembly.SetCustomAttribute(new CustomAttributeBuilder(
            typeof(TestFilterProviderAttribute).GetConstructor([typeof(Type)])!, [typeof(NoOpFilter)]));
        assembly.SetCustomAttribute(new CustomAttributeBuilder(
            typeof(TestFilterProviderAttribute<NoOpFilter>).GetConstructor(Type.EmptyTypes)!, []));
        _testablePlatformServiceProvider.MockFileOperations.Setup(value => value.LoadAssembly(Source)).Returns(assembly);
        Action load = () => new TypeCache().GetOrLoadTestFilter(Source);

        load.Should().ThrowExactly<TypeInspectionException>().Which.Message.Should().Be(string.Format(
            CultureInfo.CurrentCulture,
            Resource.UTA_TestFilterProviderMultipleDeclared,
            assembly.GetName().Name));
    }

    // Every generic marker seen in metadata keeps the "a generic marker is present" flag set; two of them
    // must not cancel each other out and hide the generic markers from the lookup.
    public void RepeatedGenericMarkersAreRejectedAsMultipleDeclarations()
    {
        const string Source = "repeated-generic-filter-markers";
        AssemblyBuilder assembly = DefineDynamicAssembly();
        ConstructorInfo genericMarker = typeof(TestFilterProviderAttribute<NoOpFilter>).GetConstructor(Type.EmptyTypes)!;
        assembly.SetCustomAttribute(new CustomAttributeBuilder(genericMarker, []));
        assembly.SetCustomAttribute(new CustomAttributeBuilder(genericMarker, []));
        _testablePlatformServiceProvider.MockFileOperations.Setup(value => value.LoadAssembly(Source)).Returns(assembly);
        Action load = () => new TypeCache().GetOrLoadTestFilter(Source);

        load.Should().ThrowExactly<TypeInspectionException>().Which.Message.Should().Be(string.Format(
            CultureInfo.CurrentCulture,
            Resource.UTA_TestFilterProviderMultipleDeclared,
            assembly.GetName().Name));
    }
#endif

    private static bool IsBlockedOrFinished(Thread thread)
        => (thread.ThreadState & (System.Threading.ThreadState.WaitSleepJoin | System.Threading.ThreadState.Stopped)) != 0;

    private static AssemblyBuilder CreateDynamicAssemblyWithTestFilterProviderAttribute()
    {
        AssemblyBuilder assemblyBuilder = DefineDynamicAssembly();
        ConstructorInfo constructor = typeof(TestFilterProviderAttribute).GetConstructor([typeof(Type)])!;
        assemblyBuilder.SetCustomAttribute(new CustomAttributeBuilder(constructor, [typeof(NoOpFilter)]));

        return assemblyBuilder;
    }

#if NETFRAMEWORK
    private static AssemblyBuilder DefineDynamicAssembly()
        => AppDomain.CurrentDomain.DefineDynamicAssembly(
            new AssemblyName("TypeCacheFilterDiscoveryMutation" + Guid.NewGuid().ToString("N")),
            AssemblyBuilderAccess.Run);
#else
    private static AssemblyBuilder DefineDynamicAssembly()
        => AssemblyBuilder.DefineDynamicAssembly(
            new AssemblyName("TypeCacheFilterDiscoveryMutation" + Guid.NewGuid().ToString("N")),
            AssemblyBuilderAccess.Run);
#endif

    // Reports the attribute metadata of a real assembly but cannot tell its own name.
    private sealed class NameUnavailableAssembly(Assembly metadataSource) : Assembly
    {
        public override string FullName => "name-unavailable-filter-source";

        public override IList<CustomAttributeData> GetCustomAttributesData() => metadataSource.GetCustomAttributesData();

        public override AssemblyName GetName() => throw new FileLoadException("name unavailable");
    }

    public sealed class NoOpFilter : ITestFilter
    {
        public TestFilterResult Filter(TestFilterContext context) => TestFilterResult.Run;
    }
}
