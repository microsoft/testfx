// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestTools.UnitTesting;

public sealed partial class CollectionAssert
{
    internal static bool TryFindMissingAndUnexpectedElements<T>(
        IEnumerable<T?> expected,
        IEnumerable<T?> actual,
        IEqualityComparer<T> comparer,
        out List<T?>? missing,
        out List<T?>? unexpected)
    {
#pragma warning disable CS8714 // The type cannot be used as type parameter in the generic type or method. Nullability of type argument doesn't match 'notnull' constraint.
        Dictionary<T, int> actualCounts = GetElementCounts(actual, comparer, out int actualNulls);
#pragma warning restore CS8714
        missing = null;

        foreach (T? element in expected)
        {
            if (element is null)
            {
                if (actualNulls > 0)
                {
                    actualNulls--;
                }
                else
                {
                    missing ??= [];
                    missing.Add(default);
                }

                continue;
            }

            if (actualCounts.TryGetValue(element, out int remaining) && remaining > 0)
            {
                actualCounts[element] = remaining - 1;
            }
            else
            {
                missing ??= [];
                missing.Add(element);
            }
        }

#pragma warning disable CS8714 // The type cannot be used as type parameter in the generic type or method. Nullability of type argument doesn't match 'notnull' constraint.
        Dictionary<T, int> expectedCounts = GetElementCounts(expected, comparer, out int expectedNulls);
#pragma warning restore CS8714
        unexpected = null;

        foreach (T? element in actual)
        {
            if (element is null)
            {
                if (expectedNulls > 0)
                {
                    expectedNulls--;
                }
                else
                {
                    unexpected ??= [];
                    unexpected.Add(default);
                }

                continue;
            }

            if (expectedCounts.TryGetValue(element, out int remaining) && remaining > 0)
            {
                expectedCounts[element] = remaining - 1;
            }
            else
            {
                unexpected ??= [];
                unexpected.Add(element);
            }
        }

        return missing is not null || unexpected is not null;
    }
}
