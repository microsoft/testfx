// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestTools.UnitTesting;

#pragma warning disable RS0026 // Do not add multiple public overloads with optional parameters
#pragma warning disable RS0027 // API with optional parameter(s) should have the most parameters amongst its public overloads
public sealed partial class Assert
{
    private static void ContainsAllImpl<T>(IEnumerable<T?> expected, IEnumerable<T?> collection, IEqualityComparer<T> comparer, string? comparerName, string? message, string expectedExpression, string collectionExpression)
    {
        // Snapshot once so we don't enumerate twice (counting + rendering on failure)
        // and so lazy/single-pass enumerables behave deterministically.
        List<T?> expectedList = expected is List<T?> el ? el : [.. expected];
        List<T?> collectionList = collection is List<T?> cl ? cl : [.. collection];

        if (TryFindMissingElements(expectedList, collectionList, comparer, out List<T?>? missing))
        {
            ReportAssertContainsAllFailed(expectedList, collectionList, missing, comparerName, message, expectedExpression, collectionExpression);
        }
    }

    /// <summary>
    /// Determines whether <paramref name="expected"/> contains any element not present
    /// (with sufficient multiplicity) in <paramref name="collection"/>.
    /// </summary>
    /// <returns>
    /// <see langword="true"/> if at least one element is missing — in which case <paramref name="missing"/>
    /// holds the excess elements (in their first-seen order in <paramref name="expected"/>) — and
    /// <see langword="false"/> when every element of <paramref name="expected"/> is matched in
    /// <paramref name="collection"/>.
    /// </returns>
    private static bool TryFindMissingElements<T>(IEnumerable<T?> expected, IEnumerable<T?> collection, IEqualityComparer<T> comparer, [NotNullWhen(true)] out List<T?>? missing)
    {
#pragma warning disable CS8714 // The type cannot be used as type parameter in the generic type or method. Nullability of type argument doesn't match 'notnull' constraint.
        Dictionary<T, int> collectionCounts = CountElements(collection, comparer, out int collectionNulls);
#pragma warning restore CS8714

        missing = null;

        // Walk the expected items in source order so excess elements appear in first-seen positional order
        // (with multiplicity preserved). For each element, decrement its remaining quota in the collection;
        // when the quota reaches zero, additional occurrences are reported as missing.
        foreach (T? element in expected)
        {
            if (element is null)
            {
                if (collectionNulls > 0)
                {
                    collectionNulls--;
                }
                else
                {
                    missing ??= [];
                    missing.Add(default);
                }

                continue;
            }

            if (collectionCounts.TryGetValue(element, out int remaining) && remaining > 0)
            {
                collectionCounts[element] = remaining - 1;
            }
            else
            {
                missing ??= [];
                missing.Add(element);
            }
        }

        return missing is not null;
    }

#pragma warning disable CS8714 // The type cannot be used as type parameter in the generic type or method. Nullability of type argument doesn't match 'notnull' constraint.
    private static Dictionary<T, int> CountElements<T>(IEnumerable<T?> collection, IEqualityComparer<T> comparer, out int nullCount)
    {
#pragma warning disable IDE0028 // Collection initialization can be simplified - target-typed new with constructor argument is preferred over collection expression here
        Dictionary<T, int> counts = new(comparer);
#pragma warning restore IDE0028
        nullCount = 0;
        foreach (T? element in collection)
        {
            if (element is null)
            {
                nullCount++;
                continue;
            }

            counts.TryGetValue(element, out int count);
            counts[element] = count + 1;
        }

        return counts;
    }
#pragma warning restore CS8714

    [DoesNotReturn]
    private static void ReportAssertContainsAllFailed<T>(IEnumerable<T?> expected, IEnumerable<T?> collection, List<T?> missing, string? comparerName, string? message, string expectedExpression, string collectionExpression)
    {
        string expectedText = AssertionValueRenderer.RenderValue(expected);
        string collectionText = AssertionValueRenderer.RenderValue(collection);
        string missingText = AssertionValueRenderer.RenderValue(missing);

        EvidenceBlock evidence = EvidenceBlock.Create()
            .AddLine("missing:", missingText)
            .AddLine("expected:", expectedText)
            .AddLine("collection:", collectionText);

        if (comparerName is not null)
        {
            evidence.AddLine("comparer:", comparerName);
        }

        StructuredAssertionMessage structured = new(FrameworkMessages.ContainsAllFailedSummary);
        structured.WithUserMessage(message);
        structured.WithEvidence(evidence);
        structured.WithExpectedAndActual(expectedText, collectionText);
        structured.WithCallSiteExpression(BuildCallSiteWithComparer("Assert.ContainsAll", expectedExpression, collectionExpression, comparerName is not null));

        ReportAssertFailed(structured);
    }

    private static string? BuildCallSiteWithComparer(string assertionMethodName, string firstArgExpression, string secondArgExpression, bool hasComparer, string firstArgPlaceholder = "<expected>")
        => hasComparer
            // No [CallerArgumentExpression] is captured for the comparer parameter, so the third
            // expression slot is always unavailable. Pass the "<comparer>" placeholder directly as
            // the third expression to ensure the call site is rendered (e.g. as
            // "Assert.ContainsAll(<expected>, <collection>, <comparer>)" or
            // "Assert.DoesNotContainAll(<notExpected>, <collection>, <comparer>)") even when callers
            // do not support [CallerArgumentExpression] and the other expressions are also empty.
            ? FormatCallSiteExpression(assertionMethodName, firstArgExpression, secondArgExpression, expression3: "<comparer>", firstArgPlaceholder, "<collection>", "<comparer>")
            : FormatCallSiteExpression(assertionMethodName, firstArgExpression, secondArgExpression, firstArgPlaceholder, "<collection>");
}
