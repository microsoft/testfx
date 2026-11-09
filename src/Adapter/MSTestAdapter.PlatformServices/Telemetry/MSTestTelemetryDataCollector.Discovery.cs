// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if !WINDOWS_UWP && !WIN_UI
using System.Security.Cryptography;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter;

internal sealed partial class MSTestTelemetryDataCollector
{
    internal Dictionary<string, long> GetDiscoveryAttributeCounts()
        => _attributeCounts.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);

    internal void AddDiscoveryAttributeCounts(IReadOnlyDictionary<string, long> counts)
    {
        foreach (KeyValuePair<string, long> count in counts)
        {
            _attributeCounts.AddOrUpdate(count.Key, count.Value, (_, existing) => existing + count.Value);
        }
    }

    /// <summary>
    /// Records the attributes found on a test method during discovery. Safe to call concurrently
    /// from multiple discovery threads — counters use a <see cref="ConcurrentDictionary{TKey,TValue}"/>
    /// and the custom-type sets are protected by an internal lock.
    /// </summary>
    /// <param name="attributes">The cached attributes from the method.</param>
    internal void TrackDiscoveredMethod(Attribute[] attributes)
    {
        foreach (Attribute attribute in attributes)
        {
            Type attributeType = attribute.GetType();
            string attributeName = attributeType.Name;

            // Track custom/inherited TestMethodAttribute types (store anonymized hash)
            if (attribute is TestMethodAttribute && attributeType != typeof(TestMethodAttribute))
            {
                AddCustomType(_customTestMethodTypes, AnonymizeString(attributeType.FullName ?? attributeName));
            }

            // Track custom/inherited TestClassAttribute types (store anonymized hash)
            if (attribute is TestClassAttribute && attributeType != typeof(TestClassAttribute))
            {
                AddCustomType(_customTestClassTypes, AnonymizeString(attributeType.FullName ?? attributeName));
            }

            // Track attribute usage counts by base type name (only known MSTest attributes)
            string? trackingName = attribute switch
            {
                TestMethodAttribute => nameof(TestMethodAttribute),
                TestClassAttribute => nameof(TestClassAttribute),
                DataRowAttribute => nameof(DataRowAttribute),
                DynamicDataAttribute => nameof(DynamicDataAttribute),
                TimeoutAttribute => nameof(TimeoutAttribute),
                IgnoreAttribute => nameof(IgnoreAttribute),
                DoNotParallelizeAttribute => nameof(DoNotParallelizeAttribute),
                ResourceLockAttribute => nameof(ResourceLockAttribute),
                DependsOnAttribute => nameof(DependsOnAttribute),
                RetryBaseAttribute => nameof(RetryBaseAttribute),
                ConditionBaseAttribute => nameof(ConditionBaseAttribute),
                TestCategoryAttribute => nameof(TestCategoryAttribute),
#if !WIN_UI
                DeploymentItemAttribute => nameof(DeploymentItemAttribute),
#endif
                _ => null,
            };

            if (trackingName is not null)
            {
                _attributeCounts.AddOrUpdate(trackingName, 1, static (_, count) => count + 1);
            }
        }
    }

    /// <summary>
    /// Records the attributes found on a test class during discovery. Safe to call concurrently
    /// from multiple discovery threads — counters use a <see cref="ConcurrentDictionary{TKey,TValue}"/>
    /// and the custom-type sets are protected by an internal lock.
    /// </summary>
    /// <param name="attributes">The cached attributes from the class.</param>
    internal void TrackDiscoveredClass(Attribute[] attributes)
    {
        foreach (Attribute attribute in attributes)
        {
            Type attributeType = attribute.GetType();

            // Track custom/inherited TestClassAttribute types (store anonymized hash)
            if (attribute is TestClassAttribute && attributeType != typeof(TestClassAttribute))
            {
                AddCustomType(_customTestClassTypes, AnonymizeString(attributeType.FullName ?? attributeType.Name));
            }

            string? trackingName = attribute switch
            {
                TestClassAttribute => nameof(TestClassAttribute),
                ParallelizeAttribute => nameof(ParallelizeAttribute),
                DoNotParallelizeAttribute => nameof(DoNotParallelizeAttribute),
                ResourceLockAttribute => nameof(ResourceLockAttribute),
                DependsOnAttribute => nameof(DependsOnAttribute),
                _ => null,
            };

            if (trackingName is not null)
            {
                _attributeCounts.AddOrUpdate(trackingName, 1, static (_, count) => count + 1);
            }
        }
    }

    private void AddCustomType(HashSet<string> set, string value)
    {
        lock (_customTypesGate)
        {
            set.Add(value);
        }
    }

    private static string AnonymizeString(string value)
    {
#if NET
        byte[] hash = SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(hash);
#else
        using var sha256 = SHA256.Create();
        byte[] hash = sha256.ComputeHash(System.Text.Encoding.UTF8.GetBytes(value));
        return BitConverter.ToString(hash).Replace("-", string.Empty);
#endif
    }
}
#endif
