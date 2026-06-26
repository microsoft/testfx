// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestTools.UnitTesting;

public sealed partial class Assert
{
    private static void AreSequenceEqualImpl<T>(IEnumerable<T>? expected, IEnumerable<T>? actual, IEqualityComparer<T> comparer, string? comparerName, bool hasComparerArgument, SequenceOrder order, bool hasOrderArgument, string? message, string expectedExpression, string actualExpression)
    {
        ValidateSequenceOrder(order);

        if (object.ReferenceEquals(expected, actual))
        {
            return;
        }

        if (expected is null || actual is null)
        {
            List<T?>? expectedList = expected is null ? null : [.. expected];
            List<T?>? actualList = actual is null ? null : [.. actual];
            ReportAssertAreSequenceEqualNullMismatch(expectedList, actualList, comparerName, hasComparerArgument, order, hasOrderArgument, message, expectedExpression, actualExpression);
            return;
        }

        switch (order)
        {
            case SequenceOrder.InOrder:
                {
                    List<T?> expectedList = expected as List<T?> ?? [.. expected];
                    List<T?> actualList = actual as List<T?> ?? [.. actual];
                    if (expectedList.Count != actualList.Count)
                    {
                        ReportAssertAreSequenceEqualInOrderLengthMismatch(expectedList, actualList, comparerName, hasComparerArgument, hasOrderArgument, message, expectedExpression, actualExpression);
                        return;
                    }

                    int differenceCount = 0;
                    int firstDifferenceIndex = -1;
                    for (int i = 0; i < expectedList.Count; i++)
                    {
                        if (AreSequenceElementsEqual(expectedList[i], actualList[i], comparer))
                        {
                            continue;
                        }

                        differenceCount++;
                        firstDifferenceIndex = firstDifferenceIndex < 0 ? i : firstDifferenceIndex;
                    }

                    if (differenceCount > 0)
                    {
                        ReportAssertAreSequenceEqualInOrderElementMismatch(expectedList, actualList, differenceCount, firstDifferenceIndex, comparerName, hasComparerArgument, hasOrderArgument, message, expectedExpression, actualExpression);
                    }

                    return;
                }

            case SequenceOrder.InAnyOrder:
                {
                    List<T?> expectedList = expected as List<T?> ?? [.. expected];
                    List<T?> actualList = actual as List<T?> ?? [.. actual];
                    if (HasAnyOrderDifferences(expectedList, actualList, comparer, out List<T?>? missing, out List<T?>? unexpected))
                    {
                        ReportAssertAreSequenceEqualInAnyOrderFailed(expectedList, actualList, missing, unexpected, comparerName, hasComparerArgument, hasOrderArgument, message, expectedExpression, actualExpression);
                    }

                    return;
                }

            default:
                throw new ArgumentOutOfRangeException(nameof(order));
        }
    }

    private static void AreNotSequenceEqualImpl<T>(IEnumerable<T>? notExpected, IEnumerable<T>? actual, IEqualityComparer<T> comparer, string? comparerName, bool hasComparerArgument, SequenceOrder order, bool hasOrderArgument, string? message, string notExpectedExpression, string actualExpression)
    {
        ValidateSequenceOrder(order);

        if (object.ReferenceEquals(notExpected, actual))
        {
            ReportAssertAreNotSequenceEqualFailed(notExpected, actual, comparerName, order, hasComparerArgument, hasOrderArgument, message, notExpectedExpression, actualExpression);
            return;
        }

        if (notExpected is null || actual is null)
        {
            return;
        }

        switch (order)
        {
            case SequenceOrder.InOrder:
                {
                    List<T?> notExpectedList = notExpected as List<T?> ?? [.. notExpected];
                    List<T?> actualList = actual as List<T?> ?? [.. actual];
                    if (notExpectedList.Count != actualList.Count)
                    {
                        return;
                    }

                    for (int i = 0; i < notExpectedList.Count; i++)
                    {
                        if (!AreSequenceElementsEqual(notExpectedList[i], actualList[i], comparer))
                        {
                            return;
                        }
                    }

                    ReportAssertAreNotSequenceEqualFailed(notExpectedList, actualList, comparerName, order, hasComparerArgument, hasOrderArgument, message, notExpectedExpression, actualExpression);
                    return;
                }

            case SequenceOrder.InAnyOrder:
                {
                    List<T?> notExpectedList = notExpected as List<T?> ?? [.. notExpected];
                    List<T?> actualList = actual as List<T?> ?? [.. actual];
                    if (!HasAnyOrderDifferences(notExpectedList, actualList, comparer, out _, out _))
                    {
                        ReportAssertAreNotSequenceEqualFailed(notExpectedList, actualList, comparerName, order, hasComparerArgument, hasOrderArgument, message, notExpectedExpression, actualExpression);
                    }

                    return;
                }

            default:
                throw new ArgumentOutOfRangeException(nameof(order));
        }
    }

    private static bool AreSequenceElementsEqual<T>(T? expected, T? actual, IEqualityComparer<T> comparer)
        => expected is null || actual is null
            ? expected is null && actual is null
            : comparer.Equals(expected, actual);

    private static bool HasAnyOrderDifferences<T>(List<T?> expected, List<T?> actual, IEqualityComparer<T> comparer, out List<T?>? missing, out List<T?>? unexpected)
    {
#pragma warning disable CS8714 // The type cannot be used as type parameter in the generic type or method. Nullability of type argument doesn't match 'notnull' constraint.
#pragma warning disable IDE0028 // Collection initialization can be simplified - target-typed `new` cannot pass the comparer in the same syntactic form expected.
        Dictionary<T, int> expectedCounts = new(comparer);
#pragma warning restore IDE0028
#pragma warning restore CS8714
        int missingNullCount = 0;

        foreach (T? element in expected)
        {
            if (element is null)
            {
                missingNullCount++;
                continue;
            }

            expectedCounts.TryGetValue(element, out int count);
            expectedCounts[element] = count + 1;
        }

        unexpected = null;
        foreach (T? element in actual)
        {
            if (element is null)
            {
                if (missingNullCount > 0)
                {
                    missingNullCount--;
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
                if (remaining == 1)
                {
                    expectedCounts.Remove(element);
                }
                else
                {
                    expectedCounts[element] = remaining - 1;
                }
            }
            else
            {
                unexpected ??= [];
                unexpected.Add(element);
            }
        }

        missing = null;
        int remainingNullCount = missingNullCount;
        foreach (T? element in expected)
        {
            if (element is null)
            {
                if (remainingNullCount > 0)
                {
                    missing ??= [];
                    missing.Add(default);
                    remainingNullCount--;
                }

                continue;
            }

            if (expectedCounts.TryGetValue(element, out int remaining) && remaining > 0)
            {
                missing ??= [];
                missing.Add(element);
                if (remaining == 1)
                {
                    expectedCounts.Remove(element);
                }
                else
                {
                    expectedCounts[element] = remaining - 1;
                }
            }
        }

        return missing is not null || unexpected is not null;
    }

    private static void ValidateSequenceOrder(SequenceOrder order)
    {
        if (order is SequenceOrder.InOrder or SequenceOrder.InAnyOrder)
        {
            return;
        }

        throw new ArgumentOutOfRangeException(nameof(order));
    }

    [DoesNotReturn]
    private static void ReportAssertAreSequenceEqualNullMismatch<T>(IEnumerable<T>? expected, IEnumerable<T>? actual, string? comparerName, bool hasComparerArgument, SequenceOrder order, bool hasOrderArgument, string? message, string expectedExpression, string actualExpression)
    {
        string expectedText = AssertionValueRenderer.RenderValue(expected);
        string actualText = AssertionValueRenderer.RenderValue(actual);
        EvidenceBlock evidence = EvidenceBlock.Create()
            .AddLine("expected:", expectedText)
            .AddLine("actual:", actualText);

        if (comparerName is not null)
        {
            evidence.AddLine("comparer:", comparerName);
        }

        StructuredAssertionMessage structured = new(order == SequenceOrder.InAnyOrder ? FrameworkMessages.AreSequenceEqualInAnyOrderFailedSummary : FrameworkMessages.AreSequenceEqualInOrderFailedSummary);
        structured.WithAdditionalSummaryLine(FrameworkMessages.AreEquivalentMismatchNull);
        structured.WithUserMessage(message);
        structured.WithEvidence(evidence);
        structured.WithExpectedAndActual(expectedText, actualText);
        structured.WithCallSiteExpression(BuildSequenceEqualCallSite("Assert.AreSequenceEqual", expectedExpression, actualExpression, "<expected>", hasComparerArgument, hasOrderArgument));

        ReportAssertFailed(structured);
    }

    [DoesNotReturn]
    private static void ReportAssertAreSequenceEqualInOrderLengthMismatch<T>(IReadOnlyList<T?> expected, IReadOnlyList<T?> actual, string? comparerName, bool hasComparerArgument, bool hasOrderArgument, string? message, string expectedExpression, string actualExpression)
    {
        string expectedText = AssertionValueRenderer.RenderValue(expected);
        string actualText = AssertionValueRenderer.RenderValue(actual);
        EvidenceBlock evidence = EvidenceBlock.Create()
            .AddLine("expected:", expectedText)
            .AddLine("actual:", actualText);

        if (comparerName is not null)
        {
            evidence.AddLine("comparer:", comparerName);
        }

        StructuredAssertionMessage structured = new(FrameworkMessages.AreSequenceEqualInOrderFailedSummary);
        structured.WithAdditionalSummaryLine(string.Format(CultureInfo.CurrentCulture, FrameworkMessages.AreSequenceEqualLengthDiffMsg, expected.Count, actual.Count));
        structured.WithUserMessage(message);
        structured.WithEvidence(evidence);
        structured.WithExpectedAndActual(expectedText, actualText);
        structured.WithCallSiteExpression(BuildSequenceEqualCallSite("Assert.AreSequenceEqual", expectedExpression, actualExpression, "<expected>", hasComparerArgument, hasOrderArgument));

        ReportAssertFailed(structured);
    }

    [DoesNotReturn]
    private static void ReportAssertAreSequenceEqualInOrderElementMismatch<T>(IReadOnlyList<T?> expected, IReadOnlyList<T?> actual, int differenceCount, int firstDifferenceIndex, string? comparerName, bool hasComparerArgument, bool hasOrderArgument, string? message, string expectedExpression, string actualExpression)
    {
        string expectedText = AssertionValueRenderer.RenderValue(expected);
        string actualText = AssertionValueRenderer.RenderValue(actual);
        EvidenceBlock evidence = EvidenceBlock.Create()
            .AddLine("expected:", expectedText)
            .AddLine("actual:", actualText);

        if (comparerName is not null)
        {
            evidence.AddLine("comparer:", comparerName);
        }

        StructuredAssertionMessage structured = new(FrameworkMessages.AreSequenceEqualInOrderFailedSummary);
        structured.WithAdditionalSummaryLine(string.Format(CultureInfo.CurrentCulture, FrameworkMessages.AreSequenceEqualSameLengthDiffMsg, expected.Count, differenceCount, firstDifferenceIndex));
        structured.WithUserMessage(message);
        structured.WithEvidence(evidence);
        structured.WithExpectedAndActual(expectedText, actualText);
        structured.WithCallSiteExpression(BuildSequenceEqualCallSite("Assert.AreSequenceEqual", expectedExpression, actualExpression, "<expected>", hasComparerArgument, hasOrderArgument));

        ReportAssertFailed(structured);
    }

    [DoesNotReturn]
    private static void ReportAssertAreSequenceEqualInAnyOrderFailed<T>(IReadOnlyList<T?> expected, IReadOnlyList<T?> actual, List<T?>? missing, List<T?>? unexpected, string? comparerName, bool hasComparerArgument, bool hasOrderArgument, string? message, string expectedExpression, string actualExpression)
    {
        List<T?> missingItems = missing ?? [];
        List<T?> unexpectedItems = unexpected ?? [];
        string expectedText = AssertionValueRenderer.RenderValue(expected);
        string actualText = AssertionValueRenderer.RenderValue(actual);
        EvidenceBlock evidence = EvidenceBlock.Create()
            .AddLine("missing:", AssertionValueRenderer.RenderValue(missingItems))
            .AddLine("unexpected:", AssertionValueRenderer.RenderValue(unexpectedItems));

        if (comparerName is not null)
        {
            evidence.AddLine("comparer:", comparerName);
        }

        // The summary already says "in any order", so repeating the order as an evidence line would add noise.
        StructuredAssertionMessage structured = new(FrameworkMessages.AreSequenceEqualInAnyOrderFailedSummary);
        structured.WithAdditionalSummaryLine(string.Format(CultureInfo.CurrentCulture, FrameworkMessages.AreSequenceEqualMissingUnexpectedMsg, missingItems.Count, unexpectedItems.Count));
        structured.WithUserMessage(message);
        structured.WithEvidence(evidence);
        structured.WithExpectedAndActual(expectedText, actualText);
        structured.WithCallSiteExpression(BuildSequenceEqualCallSite("Assert.AreSequenceEqual", expectedExpression, actualExpression, "<expected>", hasComparerArgument, hasOrderArgument));

        ReportAssertFailed(structured);
    }

    [DoesNotReturn]
    private static void ReportAssertAreNotSequenceEqualFailed<T>(IEnumerable<T>? notExpected, IEnumerable<T>? actual, string? comparerName, SequenceOrder order, bool hasComparerArgument, bool hasOrderArgument, string? message, string notExpectedExpression, string actualExpression)
    {
        string notExpectedText = AssertionValueRenderer.RenderValue(notExpected);
        string actualText = AssertionValueRenderer.RenderValue(actual);
        EvidenceBlock evidence = EvidenceBlock.Create()
            .AddLine("notExpected:", notExpectedText)
            .AddLine("actual:", actualText);

        if (comparerName is not null)
        {
            evidence.AddLine("comparer:", comparerName);
        }

        StructuredAssertionMessage structured = new(order == SequenceOrder.InAnyOrder ? FrameworkMessages.AreNotSequenceEqualInAnyOrderFailedSummary : FrameworkMessages.AreNotSequenceEqualInOrderFailedSummary);
        structured.WithUserMessage(message);
        structured.WithEvidence(evidence);
        structured.WithExpectedAndActual(notExpectedText, actualText);
        structured.WithCallSiteExpression(BuildSequenceEqualCallSite("Assert.AreNotSequenceEqual", notExpectedExpression, actualExpression, "<notExpected>", hasComparerArgument, hasOrderArgument));

        ReportAssertFailed(structured);
    }

    private static string? BuildSequenceEqualCallSite(string assertionMethodName, string leftExpression, string rightExpression, string leftPlaceholder, bool hasComparerArgument, bool hasOrderArgument)
    {
        string? callSite = FormatCallSiteExpression(assertionMethodName, leftExpression, rightExpression, leftPlaceholder, "<actual>");
        if (callSite is null)
        {
            return null;
        }

        if (!hasComparerArgument && !hasOrderArgument)
        {
            return callSite;
        }

        Debug.Assert(callSite.Length > 0 && callSite[callSite.Length - 1] == ')', "FormatCallSiteExpression contract: rendered call-site must end with ')'.");

        string suffix = hasComparerArgument && hasOrderArgument
            ? ", <comparer>, <order>)"
            : hasComparerArgument
                ? ", <comparer>)"
                : ", <order>)";

        return string.Concat(callSite.Remove(callSite.Length - 1), suffix);
    }
}
