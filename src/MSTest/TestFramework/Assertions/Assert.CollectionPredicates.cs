// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestTools.UnitTesting;

public sealed partial class Assert
{
    private static bool TryFindPredicateMismatch<T>(IEnumerable<T> source, Func<T, bool> predicate, bool expectedPredicateResult, out long index, [NotNullWhen(true)] out string? itemText)
    {
        // Preserve Enumerable.Any's validation and argument names for existing DoesNotContain calls.
        if (source is null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        if (predicate is null)
        {
            throw new ArgumentNullException(nameof(predicate));
        }

        index = 0;
        foreach (T item in source)
        {
            if (predicate(item) != expectedPredicateResult)
            {
                itemText = AssertionValueRenderer.RenderValueWithoutEnumeratingCollections(item);
                return true;
            }

            index++;
        }

        itemText = null;
        return false;
    }

#if NETCOREAPP3_1_OR_GREATER
    private static bool TryFindPredicateMismatch<T>(ReadOnlySpan<T> source, Func<T, bool> predicate, bool expectedPredicateResult, out long index, [NotNullWhen(true)] out string? itemText)
    {
        index = 0;
        foreach (T item in source)
        {
            if (predicate(item) != expectedPredicateResult)
            {
                itemText = AssertionValueRenderer.RenderValueWithoutEnumeratingCollections(item);
                return true;
            }

            index++;
        }

        itemText = null;
        return false;
    }
#endif

    [DoesNotReturn]
    private static void ReportPredicateMismatchFailed(bool expectedPredicateResult, long index, string itemText, string? userMessage, string predicateExpression, string collectionExpression)
    {
        string assertMethodName = expectedPredicateResult ? "Assert.ContainsOnly" : "Assert.DoesNotContain";
        string summary = expectedPredicateResult
            ? FrameworkMessages.ContainsOnlyPredicateFailedSummary
            : FrameworkMessages.DoesNotContainPredicateFailedSummary;
        EvidenceBlock evidence = EvidenceBlock.Create()
            .AddLine(FrameworkMessages.PredicateMismatchIndexLabel, index.ToString(CultureInfo.CurrentCulture))
            .AddLine(FrameworkMessages.PredicateMismatchItemLabel, itemText);
        StructuredAssertionMessage structured = new(summary);
        structured.WithUserMessage(userMessage);
        structured.WithEvidence(evidence);
        structured.WithExpectedAndActual(expectedPredicateResult ? "true" : "false", expectedPredicateResult ? "false" : "true");
        structured.WithCallSiteExpression(FormatCallSiteExpression(assertMethodName, predicateExpression, collectionExpression, "<predicate>", "<collection>"));
        ReportAssertFailed(structured);
    }
}
