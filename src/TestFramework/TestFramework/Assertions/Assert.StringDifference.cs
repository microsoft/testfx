// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestTools.UnitTesting;

public sealed partial class Assert
{
    // This leaves room for an evidence label on a 120-column display while keeping the preview itself bounded.
    private const int StringDifferencePreviewBudget = 101;
    private const int StringDifferenceContextBudget = StringDifferencePreviewBudget * 2;
    private const int MaximumCodePointsToDisplay = 8;

    private static StringDifference FindFirstStringDifference(string expected, string actual)
    {
        int minLength = Math.Min(expected.Length, actual.Length);

        for (int i = 0; i < minLength; i++)
        {
            if (expected[i] != actual[i])
            {
                return new StringDifference(i, i);
            }
        }

        return expected.Length != actual.Length
            ? new StringDifference(minLength, minLength)
            : StringDifference.None;
    }

    private static void AddStringComparisonEvidence(
        StructuredAssertionMessage structured,
        string expected,
        string actual,
        string expectedRendered,
        string actualRendered,
        StringDifference difference,
        bool ignoreCase,
        CultureInfo culture,
        bool cultureExplicit)
    {
        if (!AssertionValueRenderer.IsBuiltInStringRendering(expected, expectedRendered)
            || !AssertionValueRenderer.IsBuiltInStringRendering(actual, actualRendered))
        {
            structured.WithEvidence(CreateStringComparisonEvidence(
                "expected:",
                expectedRendered,
                actualRendered,
                ignoreCase,
                culture,
                cultureExplicit));
            return;
        }

        StringDifferenceDiagnostic diagnostic = CreateStringDifferenceDiagnostic(
            expected,
            actual,
            expectedRendered,
            actualRendered,
            difference);

        if (diagnostic.UsesPreview)
        {
            EvidenceBlock previewEvidence = EvidenceBlock.Create()
                .AddLine("expected near:", diagnostic.ExpectedPreview!)
                .AddLine("actual near:", diagnostic.ActualPreview!)
                .AddLine("difference:", diagnostic.Difference);

            if (diagnostic.CodePoints is not null)
            {
                previewEvidence.AddLine("code points:", diagnostic.CodePoints);
            }

            structured.WithEvidence(previewEvidence);
            structured.WithEvidence(CreateStringComparisonEvidence(
                "expected:",
                expectedRendered,
                actualRendered,
                ignoreCase,
                culture,
                cultureExplicit));
            return;
        }

        structured.WithEvidence(CreateStringComparisonEvidence(
            "expected:",
            expectedRendered,
            actualRendered,
            ignoreCase,
            culture,
            cultureExplicit,
            diagnostic));
    }

    private static StringDifferenceDiagnostic CreateStringDifferenceDiagnostic(
        string expected,
        string actual,
        string expectedRendered,
        string actualRendered,
        StringDifference difference)
    {
        StringTokenWindow expectedWindow = CreateTokenWindow(expected, difference.ExpectedIndex);
        StringTokenWindow actualWindow = CreateTokenWindow(actual, difference.ActualIndex);
        bool usesPreview =
            GetRenderedStringLengthUpToBudget(expected) > StringDifferencePreviewBudget
            || GetRenderedStringLengthUpToBudget(actual) > StringDifferencePreviewBudget
            || expectedRendered.Length > StringDifferencePreviewBudget
            || actualRendered.Length > StringDifferencePreviewBudget;

        string? codePoints = CreateCodePointDiagnostic(expectedWindow, actualWindow);

        if (!usesPreview)
        {
            int expectedPrefixLength = GetShortPrefixLength(expectedWindow);
            int actualPrefixLength = GetShortPrefixLength(actualWindow);
            bool useCaret =
                !expectedWindow.MismatchRequiresPlaceholder
                && !actualWindow.MismatchRequiresPlaceholder
                && expectedWindow.IsRetainedPrefixSafe
                && actualWindow.IsRetainedPrefixSafe
                && expectedPrefixLength == actualPrefixLength;

            string differenceText = useCaret
                ? new string('-', expectedPrefixLength) + "^"
                : $"expected {RenderMismatchFragment(expectedWindow)}; actual {RenderMismatchFragment(actualWindow)}";

            return new StringDifferenceDiagnostic(differenceText, codePoints);
        }

        StringPreview expectedPreview = CreatePreview(expectedWindow, useInlineMarker: false);
        StringPreview actualPreview = CreatePreview(actualWindow, useInlineMarker: false);
        bool previewUsesCaret =
            !expectedWindow.MismatchRequiresPlaceholder
            && !actualWindow.MismatchRequiresPlaceholder
            && expectedPreview.IsPrefixSafe
            && actualPreview.IsPrefixSafe
            && expectedPreview.MismatchColumn == actualPreview.MismatchColumn;

        if (previewUsesCaret)
        {
            return new StringDifferenceDiagnostic(
                expectedPreview.Text,
                actualPreview.Text,
                new string('-', expectedPreview.MismatchColumn) + "^",
                codePoints);
        }

        expectedPreview = CreatePreview(expectedWindow, useInlineMarker: true);
        actualPreview = CreatePreview(actualWindow, useInlineMarker: true);
        return new StringDifferenceDiagnostic(
            expectedPreview.Text,
            actualPreview.Text,
            "mismatch marked with [[...]]",
            codePoints);
    }

    private readonly struct StringDifference
    {
        internal static readonly StringDifference None = new(-1, -1);

        internal StringDifference(int expectedIndex, int actualIndex)
        {
            ExpectedIndex = expectedIndex;
            ActualIndex = actualIndex;
        }

        internal int ExpectedIndex { get; }

        internal int ActualIndex { get; }

        internal int SummaryIndex => Math.Min(ExpectedIndex, ActualIndex);

        internal bool Exists => ExpectedIndex >= 0;
    }

    private sealed class StringDifferenceDiagnostic
    {
        internal StringDifferenceDiagnostic(string difference, string? codePoints)
        {
            Difference = difference;
            CodePoints = codePoints;
        }

        internal StringDifferenceDiagnostic(string expectedPreview, string actualPreview, string difference, string? codePoints)
        {
            ExpectedPreview = expectedPreview;
            ActualPreview = actualPreview;
            Difference = difference;
            CodePoints = codePoints;
        }

        internal string? ExpectedPreview { get; }

        internal string? ActualPreview { get; }

        internal string Difference { get; }

        internal string? CodePoints { get; }

        internal bool UsesPreview => ExpectedPreview is not null;
    }
}
