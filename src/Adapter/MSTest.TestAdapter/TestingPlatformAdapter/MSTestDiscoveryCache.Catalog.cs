// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.Extensions;
using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;
using Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.Interface;

namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.TestingPlatformAdapter;

internal sealed partial class MSTestDiscoveryCache
{
    private static UnitTestElement CloneMetadata(UnitTestElement element)
    {
        UnitTestElement clone = element.Clone();
        clone.TestCategory = element.TestCategory?.ToArray();
        clone.Traits = element.Traits?.ToArray();
        clone.ResourceLocks = element.ResourceLocks?.ToArray();
        clone.Dependencies = element.Dependencies?.ToArray();
        clone.WorkItemIds = element.WorkItemIds?.ToArray();
#if !WINDOWS_UWP && !WIN_UI
        clone.DeploymentItems = element.DeploymentItems?.ToArray();
#endif
        clone.ExecutionContextProperties = null;
        clone.ExecutionActivityLease = null;
        clone.HostRecordingHandle = null;
        clone.SupportsExecutionActivityLease = true;
        clone.CachedTestNodeUid = element.GetTestId();
        return clone;
    }

    private sealed record CacheKey(
        Assembly Assembly,
        IReflectionOperations ReflectionOperations,
        string? SettingsXml,
        string Culture,
        string UICulture,
        long Length,
        DateTime LastWriteTime,
        bool TreatWarningsAsErrors,
        bool ConsiderEmptyDataSourceAsInconclusive,
        bool CollectTelemetry)
    {
        internal bool Matches(CacheKey other)
            => ReferenceEquals(Assembly, other.Assembly)
                && ReferenceEquals(ReflectionOperations, other.ReflectionOperations)
                && SettingsXml == other.SettingsXml
                && Culture == other.Culture
                && UICulture == other.UICulture
                && Length == other.Length
                && LastWriteTime == other.LastWriteTime
                && TreatWarningsAsErrors == other.TreatWarningsAsErrors
                && ConsiderEmptyDataSourceAsInconclusive == other.ConsiderEmptyDataSourceAsInconclusive
                && CollectTelemetry == other.CollectTelemetry;
    }

    private sealed record TypeSlice(Type Type, UnitTestElement[]? Templates, Dictionary<string, long> TelemetryCounts);

    private sealed record IndexedTest(int TypeIndex, int Order, UnitTestElement Template);

    private sealed class Catalog
    {
        internal Catalog(CacheKey key, List<TypeSlice> slices)
        {
            Key = key;
            Slices = slices.ToArray();
            UnsafeTypes = slices.Where(slice => slice.Templates is null).Select(slice => slice.Type).ToArray();
            int order = 0;
            for (int typeIndex = 0; typeIndex < slices.Count; typeIndex++)
            {
                TypeSlice slice = slices[typeIndex];
                if (slice.Templates is not { } templates)
                {
                    continue;
                }

                foreach (KeyValuePair<string, long> count in slice.TelemetryCounts)
                {
                    TelemetryCounts[count.Key] = (TelemetryCounts.TryGetValue(count.Key, out long previous) ? previous : 0) + count.Value;
                }

                foreach (UnitTestElement template in templates)
                {
                    Guid uid = template.GetTestId();
                    if (!Index.TryGetValue(uid, out List<IndexedTest>? matches))
                    {
                        Index[uid] = matches = [];
                    }

                    matches.Add(new IndexedTest(typeIndex, order++, template));
                }
            }

            StaticTestCount = order;
        }

        internal CacheKey Key { get; }

        internal TypeSlice[] Slices { get; }

        internal Type[] UnsafeTypes { get; }

        internal int StaticTestCount { get; }

        internal Dictionary<Guid, List<IndexedTest>> Index { get; } = [];

        internal Dictionary<string, long> TelemetryCounts { get; } = [];
    }
}
