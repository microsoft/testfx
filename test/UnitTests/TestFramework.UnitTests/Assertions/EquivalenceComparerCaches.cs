// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections;
using System.Reflection;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.VisualStudio.TestPlatform.TestFramework.UnitTests;

/// <summary>
/// Resets the static reflection caches that <c>Assert.AreEquivalent</c> uses so a test observes type resolution from scratch.
/// </summary>
/// <remarks>
/// The comparer memoizes reflection results per type. A defect inside a cache-populating lambda is only observable while
/// the entry for the tested type is still cold, so tests that target those lambdas reset the caches before and after
/// every test instead of depending on which tests ran earlier in the same process.
/// </remarks>
internal static class EquivalenceComparerCaches
{
    private const BindingFlags NonPublicStatic = BindingFlags.NonPublic | BindingFlags.Static;

    public static void Reset()
    {
        ClearCaches("EquivalenceComparer", "MemberCache", "IEquatableEqualsCache", "IsPrimitiveLikeCache", "EnumerableElementTypeCache", "DictionaryValueTypeCache");
        ClearCaches("GenericDictionaryView", "AccessorCache");
    }

    private static void ClearCaches(string nestedTypeName, params string[] cacheNames)
    {
        Type nestedType = typeof(Assert).GetNestedType(nestedTypeName, BindingFlags.NonPublic)
            ?? throw new InvalidOperationException($"Assert.{nestedTypeName} was not found; update {nameof(EquivalenceComparerCaches)}.");

        foreach (string cacheName in cacheNames)
        {
            FieldInfo cache = nestedType.GetField(cacheName, NonPublicStatic)
                ?? throw new InvalidOperationException($"Assert.{nestedTypeName}.{cacheName} was not found; update {nameof(EquivalenceComparerCaches)}.");

            ((IDictionary)cache.GetValue(null)!).Clear();
        }
    }
}
