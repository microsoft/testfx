// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Discovery;
#if NETCOREAPP && !WIN_UI && !WINDOWS_UWP
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Helpers;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices;
#endif
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface;

namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.TestingPlatformAdapter;

// A registration owns this cache, not an RPC's framework or test session. Only metadata with no
// discovery-time user code is retained; data providers and custom attributes are rediscovered.
[SuppressMessage("ApiDesign", "RS0030:Do not use banned APIs", Justification = "Native MTP discovery cache.")]
internal sealed partial class MSTestDiscoveryCache : IDisposable
{
    internal const string DisableEnvironmentVariable = "MSTEST_DISABLE_DISCOVERY_CACHE";
    internal const string EnableEnvironmentVariable = "MSTEST_EXPERIMENTAL_DISCOVERY_CACHE";

#if NET9_0_OR_GREATER
    private readonly Lock _gate = new();
#else
    private readonly object _gate = new();
#endif
    private readonly Dictionary<string, Catalog> _catalogs = [];
    private bool _disposed;

    internal UnitTestDiscoverer CreateDiscoverer(
        ITestSourceHandler sourceHandler,
        Assembly[] assemblies,
        TestNodeUid[] selectedUids,
        CancellationToken cancellationToken)
        => new CachedDiscoverer(this, sourceHandler, assemblies, selectedUids, cancellationToken);

    internal void SetSources(string[] sources)
    {
        lock (_gate)
        {
            foreach (string source in _catalogs.Keys.ToArray())
            {
                if (!sources.Contains(source, StringComparer.Ordinal))
                {
                    _catalogs.Remove(source);
                }
            }
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            _disposed = true;
            _catalogs.Clear();
        }
    }

    internal static bool IsSupported(Assembly assembly)
        =>
#if NETCOREAPP && !WIN_UI && !WINDOWS_UWP
        !assembly.IsDynamic && !assembly.IsCollectible
            && RuntimeFeature.IsDynamicCodeSupported
            && !RuntimeContext.IsHotReloadEnabled
            && PlatformServiceProvider.Instance.ReflectionOperations is ReflectionOperations;
#else
        false;
#endif

    private ICollection<UnitTestElement>? GetTests(
        string source,
        string? settingsXml,
        ITestSourceHandler sourceHandler,
        Assembly assembly,
        TestNodeUid[] selectedUids,
        CancellationToken cancellationToken,
        out List<string> warnings,
        out int discoveredTestCount)
    {
        var file = new FileInfo(source);
        // Configuration/CLI belong to the application; XML can change between requests.
        // Also compare the resolved discovery settings read from IConfiguration.
        var key = new CacheKey(
            assembly,
            PlatformServiceProvider.Instance.ReflectionOperations,
            settingsXml,
            CultureInfo.CurrentCulture.Name,
            CultureInfo.CurrentUICulture.Name,
            file.Exists ? file.Length : -1,
            file.LastWriteTimeUtc,
            MSTestSettings.CurrentSettings.TreatDiscoveryWarningsAsErrors,
            MSTestSettings.CurrentSettings.ConsiderEmptyDataSourceAsInconclusive,
#if !WINDOWS_UWP && !WIN_UI
            MSTestTelemetryDataCollector.Current is not null);
#else
            false);
#endif

        Catalog? catalog;
        bool disposed;
        lock (_gate)
        {
            disposed = _disposed;
            _catalogs.TryGetValue(source, out catalog);
            if (catalog is not null && !catalog.Key.Matches(key))
            {
                _catalogs.Remove(source);
                catalog = null;
            }
        }

        if (disposed)
        {
            ICollection<UnitTestElement>? baseline = AssemblyEnumeratorWrapper.GetTests(
                source, settingsXml, sourceHandler, isMTP: true, out warnings);
            discoveredTestCount = baseline?.Count ?? 0;
            return baseline;
        }

        if (catalog is null)
        {
            var slices = new List<TypeSlice>();
            bool safeAssembly = IsMetadataSafe(() => AreAttributesSafe(assembly.GetCustomAttributesData()));
            bool safeType = false;
            int liveEmptyTypeCount = 0;
#if !WINDOWS_UWP && !WIN_UI
            Dictionary<string, long>? before = null;
#endif

            ICollection<UnitTestElement>? elements = AssemblyEnumeratorWrapper.GetTests(
                source, settingsXml, sourceHandler, isMTP: true, types: null, ObserveType, out warnings);
            discoveredTestCount = elements?.Count ?? 0;

            if (elements is not null && warnings.Count == 0 && !cancellationToken.IsCancellationRequested
                && slices.Any(slice => slice.Templates is not null))
            {
                var candidate = new Catalog(key, slices);
                lock (_gate)
                {
                    if (!_disposed && !cancellationToken.IsCancellationRequested)
                    {
                        _catalogs[source] = candidate;
                    }
                }

                if (PlatformServiceProvider.Instance.AdapterTraceLogger.IsInfoEnabled)
                {
                    PlatformServiceProvider.Instance.AdapterTraceLogger.Info(
                        "MSTest discovery cache catalog: {0}/{1} reusable types, {2} reusable tests, {3} live zero-test types.",
                        candidate.Slices.Length - candidate.UnsafeTypes.Length, candidate.Slices.Length, candidate.StaticTestCount, liveEmptyTypeCount);
                }
            }

            return elements;

            void ObserveType(Type type, IReadOnlyList<UnitTestElement>? tests)
            {
                if (tests is null)
                {
                    safeType = safeAssembly && IsMetadataSafe(() => IsTypeSafe(type));
#if !WINDOWS_UWP && !WIN_UI
                    before = safeType ? MSTestTelemetryDataCollector.Current?.GetDiscoveryAttributeCounts() : null;
#endif
                    return;
                }

                safeType &= tests.All(test => test.TestMethod.DataType == DynamicDataType.None
                    && test.TestMethod.ActualData is null && test.TestMethod.SerializedData is null);
                if (!safeType && tests.Count == 0)
                {
                    liveEmptyTypeCount++;
                }

                var counts = new Dictionary<string, long>(StringComparer.Ordinal);
#if !WINDOWS_UWP && !WIN_UI
                if (safeType && before is not null && MSTestTelemetryDataCollector.Current is { } telemetry)
                {
                    foreach (KeyValuePair<string, long> count in telemetry.GetDiscoveryAttributeCounts())
                    {
                        long difference = count.Value - (before.TryGetValue(count.Key, out long previous) ? previous : 0);
                        if (difference != 0)
                        {
                            counts[count.Key] = difference;
                        }
                    }
                }
#endif
                slices.Add(new TypeSlice(type, safeType ? tests.Select(CloneMetadata).ToArray() : null, counts));
            }
        }

        // Keep source validation, assembly metadata, resolver and working-directory lifetime on hits.
        // Unsafe types run even for empty/unknown selections: discovery side effects are observable.
        var liveSlices = new List<IReadOnlyList<UnitTestElement>>(catalog.UnsafeTypes.Length);
        ICollection<UnitTestElement>? fresh = AssemblyEnumeratorWrapper.GetTests(
            source, settingsXml, sourceHandler, isMTP: true, catalog.UnsafeTypes,
            (_, tests) =>
            {
                if (tests is not null)
                {
                    liveSlices.Add(tests);
                }
            }, out warnings);
        if (fresh is null)
        {
            discoveredTestCount = 0;
            return null;
        }

        // The enumerator must report one complete slice per type, including empty slices.
        // This corruption-only fallback repeats live discovery rather than risking lost tests.
        if (liveSlices.Count != catalog.UnsafeTypes.Length || liveSlices.Sum(slice => slice.Count) != fresh.Count)
        {
            lock (_gate)
            {
                if (_catalogs.TryGetValue(source, out Catalog? current) && ReferenceEquals(current, catalog))
                {
                    _catalogs.Remove(source);
                }
            }

            PlatformServiceProvider.Instance.AdapterTraceLogger.Error(
                "MSTest discovery cache: live type slices do not match discovery results for '{0}'. Rediscovering the full source.", source);
            ICollection<UnitTestElement>? baseline = AssemblyEnumeratorWrapper.GetTests(
                source, settingsXml, sourceHandler, isMTP: true, out warnings);
            discoveredTestCount = baseline?.Count ?? 0;
            return baseline;
        }

#if !WINDOWS_UWP && !WIN_UI
        MSTestTelemetryDataCollector.Current?.AddDiscoveryAttributeCounts(catalog.TelemetryCounts);
#endif
        var candidates = new SortedDictionary<int, (int TypeIndex, UnitTestElement Element)>();
        foreach (TestNodeUid uid in selectedUids)
        {
            if (catalog.Index.TryGetValue(Guid.Parse(uid.Value), out List<IndexedTest>? matches))
            {
                foreach (IndexedTest match in matches)
                {
                    if (!candidates.ContainsKey(match.Order))
                    {
                        candidates[match.Order] = (match.TypeIndex, CloneMetadata(match.Template));
                    }
                }
            }
        }

        var selectedByType = candidates.Values.GroupBy(candidate => candidate.TypeIndex)
            .ToDictionary(group => group.Key, group => group.Select(candidate => candidate.Element).ToArray());
        var result = new List<UnitTestElement>(candidates.Count + fresh.Count);
        int liveTypeIndex = 0;
        for (int typeIndex = 0; typeIndex < catalog.Slices.Length; typeIndex++)
        {
            TypeSlice slice = catalog.Slices[typeIndex];
            if (slice.Templates is not null)
            {
                if (selectedByType.TryGetValue(typeIndex, out UnitTestElement[]? selected))
                {
                    result.AddRange(selected);
                }
            }
            else
            {
                result.AddRange(liveSlices[liveTypeIndex++]);
            }
        }

        discoveredTestCount = catalog.StaticTestCount + fresh.Count;
        if (PlatformServiceProvider.Instance.AdapterTraceLogger.IsInfoEnabled)
        {
            PlatformServiceProvider.Instance.AdapterTraceLogger.Info(
                "MSTest discovery cache hit: {0} reusable tests, {1} live types, {2} candidates.",
                catalog.StaticTestCount, catalog.UnsafeTypes.Length, result.Count);
        }

        return result;
    }
}
