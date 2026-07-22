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
                !AssertionValueFormatterRegistry.HasFormatters
                && !expectedWindow.MismatchRequiresPlaceholder
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

    private static int GetRenderedStringLengthUpToBudget(string value)
    {
        int length = 2;
        for (int i = 0; i < value.Length && length <= StringDifferencePreviewBudget; i++)
        {
            char c = value[i];
            length += GetEscapedCharacterLength(value, i);
            if (char.IsHighSurrogate(c) && i + 1 < value.Length && char.IsLowSurrogate(value[i + 1]))
            {
                i++;
            }
        }

        return length;
    }

    private static int GetShortPrefixLength(StringTokenWindow window)
    {
        int length = 1;
        foreach (StringToken token in window.Before)
        {
            length += token.RenderedLength;
        }

        return length;
    }

    private static string RenderMismatchFragment(StringTokenWindow window)
        => $"[[{RenderMismatch(window)}]]";

    private static StringPreview CreatePreview(StringTokenWindow window, bool useInlineMarker)
    {
        string mismatch = RenderMismatch(window);
        if (useInlineMarker)
        {
            mismatch = $"[[{mismatch}]]";
        }

        int beforeStart = window.Before.Length;
        int afterCount = 0;
        bool tryBefore = true;
        bool beforeBlocked = false;
        bool afterBlocked = false;

        while (!beforeBlocked || !afterBlocked)
        {
            if (tryBefore && !beforeBlocked)
            {
                if (beforeStart == 0)
                {
                    beforeBlocked = true;
                }
                else if (GetPreviewLength(window, beforeStart - 1, afterCount, mismatch.Length) <= StringDifferencePreviewBudget)
                {
                    beforeStart--;
                }
                else
                {
                    beforeBlocked = true;
                }
            }
            else if (!tryBefore && !afterBlocked)
            {
                if (afterCount == window.After.Length)
                {
                    afterBlocked = true;
                }
                else if (GetPreviewLength(window, beforeStart, afterCount + 1, mismatch.Length) <= StringDifferencePreviewBudget)
                {
                    afterCount++;
                }
                else
                {
                    afterBlocked = true;
                }
            }

            tryBefore = !tryBefore;
            if (beforeBlocked && !afterBlocked)
            {
                tryBefore = false;
            }
            else if (afterBlocked && !beforeBlocked)
            {
                tryBefore = true;
            }
        }

        bool omittedBefore = IsBeforeContextOmitted(window, beforeStart);
        bool omittedAfter = IsAfterContextOmitted(window, afterCount);
        StringBuilder builder = new(GetPreviewLength(window, beforeStart, afterCount, mismatch.Length));
        builder.Append('"');
        if (omittedBefore)
        {
            builder.Append("...");
        }

        bool isPrefixSafe = true;
        for (int i = beforeStart; i < window.Before.Length; i++)
        {
            StringToken token = window.Before[i];
            AppendEscapedToken(builder, window.Value, token);
            isPrefixSafe &= token.IsSafePrefix;
        }

        int mismatchColumn = builder.Length;
        builder.Append(mismatch);

        for (int i = 0; i < afterCount; i++)
        {
            AppendEscapedToken(builder, window.Value, window.After[i]);
        }

        if (omittedAfter)
        {
            builder.Append("...");
        }

        builder.Append('"');
        return new StringPreview(builder.ToString(), mismatchColumn, isPrefixSafe);
    }

    private static int GetPreviewLength(StringTokenWindow window, int beforeStart, int afterCount, int mismatchLength)
    {
        int length = 2 + mismatchLength;
        if (IsBeforeContextOmitted(window, beforeStart))
        {
            length += 3;
        }

        for (int i = beforeStart; i < window.Before.Length; i++)
        {
            length += window.Before[i].RenderedLength;
        }

        for (int i = 0; i < afterCount; i++)
        {
            length += window.After[i].RenderedLength;
        }

        if (IsAfterContextOmitted(window, afterCount))
        {
            length += 3;
        }

        return length;
    }

    private static bool IsBeforeContextOmitted(StringTokenWindow window, int beforeStart)
    {
        int firstIncludedIndex = beforeStart < window.Before.Length
            ? window.Before[beforeStart].Start
            : window.Mismatch?.Start ?? window.Value.Length;
        return firstIncludedIndex > 0;
    }

    private static bool IsAfterContextOmitted(StringTokenWindow window, int afterCount)
    {
        int lastIncludedIndex = afterCount > 0
            ? window.After[afterCount - 1].End
            : window.Mismatch?.End ?? window.Value.Length;
        return lastIncludedIndex < window.Value.Length;
    }

    private static string RenderMismatch(StringTokenWindow window)
    {
        if (window.Mismatch is not StringToken mismatch)
        {
            return "<end>";
        }

        if (window.MismatchRequiresPlaceholder)
        {
            return "<text element>";
        }

        StringBuilder builder = new(mismatch.RenderedLength);
        AppendEscapedToken(builder, window.Value, mismatch);
        return builder.ToString();
    }

    private static StringTokenWindow CreateTokenWindow(string value, int mismatchIndex)
    {
        Queue<StringToken> before = new();
        List<StringToken> after = [];
        int beforeLength = 0;
        int afterLength = 0;
        StringToken? mismatch = null;

        foreach (StringToken token in EnumerateStringTokens(value))
        {
            if (mismatch is null && mismatchIndex >= token.End)
            {
                before.Enqueue(token);
                beforeLength += token.RenderedLength;
                while (beforeLength > StringDifferenceContextBudget && before.Count > 1)
                {
                    beforeLength -= before.Dequeue().RenderedLength;
                }

                continue;
            }

            if (mismatch is null)
            {
                mismatch = token;
                continue;
            }

            after.Add(token);
            afterLength += token.RenderedLength;
            if (afterLength > StringDifferenceContextBudget)
            {
                break;
            }
        }

        return new StringTokenWindow(value, before.ToArray(), mismatch, after.ToArray());
    }

    private static IEnumerable<StringToken> EnumerateStringTokens(string value)
    {
        TextElementEnumerator enumerator = StringInfo.GetTextElementEnumerator(value);
        if (!enumerator.MoveNext())
        {
            yield break;
        }

        int currentStart = enumerator.ElementIndex;
        while (enumerator.MoveNext())
        {
            int nextStart = enumerator.ElementIndex;
            if (ShouldMergeTextElements(value, currentStart, nextStart))
            {
                continue;
            }

            yield return CreateStringToken(value, currentStart, nextStart - currentStart);
            currentStart = nextStart;
        }

        yield return CreateStringToken(value, currentStart, value.Length - currentStart);
    }

    private static bool ShouldMergeTextElements(string value, int currentStart, int nextStart)
    {
        ScalarInfo previous = GetPreviousScalar(value, nextStart);
        ScalarInfo next = GetScalar(value, nextStart);

        return previous.Value == 0x200D
            || next.Value == 0x200D
            || (previous.Value == '\r' && next.Value == '\n')
            || IsEmojiModifier(next.Value)
            || IsEmojiTag(next.Value)
            || IsVariationSelector(next.Value)
            || IsCombiningCharacter(value, nextStart, next)
            || (IsRegionalIndicator(previous.Value)
                && IsRegionalIndicator(next.Value)
                && previous.Start == currentStart);
    }

    private static bool IsCombiningCharacter(string value, int index, ScalarInfo scalar)
    {
        if (scalar.IsUnpairedSurrogate)
        {
            return false;
        }

        UnicodeCategory category = CharUnicodeInfo.GetUnicodeCategory(value, index);
        return category is UnicodeCategory.NonSpacingMark
            or UnicodeCategory.SpacingCombiningMark
            or UnicodeCategory.EnclosingMark;
    }

    private static bool IsEmojiModifier(int value) => value is >= 0x1F3FB and <= 0x1F3FF;

    private static bool IsEmojiTag(int value) => value is >= 0xE0020 and <= 0xE007F;

    private static bool IsVariationSelector(int value)
        => value is >= 0xFE00 and <= 0xFE0F
            or >= 0xE0100 and <= 0xE01EF;

    private static bool IsRegionalIndicator(int value) => value is >= 0x1F1E6 and <= 0x1F1FF;

    private static StringToken CreateStringToken(string value, int start, int length)
    {
        int renderedLength = 0;
        bool isSafePrefix = true;
        int scalarCount = 0;
        bool hasUnpairedSurrogate = false;

        for (int i = start; i < start + length;)
        {
            ScalarInfo scalar = GetScalar(value, i);
            renderedLength += GetEscapedCharacterLength(value, i);
            isSafePrefix &= scalar.Value <= 0x7F && !scalar.IsUnpairedSurrogate;
            hasUnpairedSurrogate |= scalar.IsUnpairedSurrogate;
            scalarCount++;
            i += scalar.Length;
        }

        return new StringToken(start, length, renderedLength, isSafePrefix, scalarCount, hasUnpairedSurrogate);
    }

    private static int GetEscapedCharacterLength(string value, int index)
    {
        char c = value[index];
        return char.IsHighSurrogate(c) && index + 1 < value.Length && char.IsLowSurrogate(value[index + 1])
            ? 2
            : char.IsSurrogate(c)
                ? 6
                : c switch
                {
                    '"' or '\\' or '\n' or '\r' or '\t' or '\0' => 2,
                    _ when char.IsControl(c) => 6,
                    _ => 1,
                };
    }

    private static void AppendEscapedToken(StringBuilder builder, string value, StringToken token)
    {
        for (int i = token.Start; i < token.End; i++)
        {
            char c = value[i];
            switch (c)
            {
                case '"':
                    builder.Append("\\\"");
                    break;
                case '\\':
                    builder.Append("\\\\");
                    break;
                case '\n':
                    builder.Append("\\n");
                    break;
                case '\r':
                    builder.Append("\\r");
                    break;
                case '\t':
                    builder.Append("\\t");
                    break;
                case '\0':
                    builder.Append("\\0");
                    break;
                default:
                    if (char.IsControl(c) || IsUnpairedSurrogate(value, i))
                    {
                        builder.Append("\\u");
                        builder.Append(((int)c).ToString("X4", CultureInfo.InvariantCulture));
                    }
                    else
                    {
                        builder.Append(c);
                    }

                    break;
            }
        }
    }

    private static bool IsUnpairedSurrogate(string value, int index)
    {
        char c = value[index];
        return char.IsHighSurrogate(c)
            ? index + 1 >= value.Length || !char.IsLowSurrogate(value[index + 1])
            : char.IsLowSurrogate(c)
                && (index == 0 || !char.IsHighSurrogate(value[index - 1]));
    }

    private static string? CreateCodePointDiagnostic(StringTokenWindow expected, StringTokenWindow actual)
        => NeedsCodePointDiagnostic(expected) || NeedsCodePointDiagnostic(actual)
            ? $"expected {FormatCodePoints(expected)}; actual {FormatCodePoints(actual)}"
            : null;

    private static bool NeedsCodePointDiagnostic(StringTokenWindow window)
        => window.Mismatch is not StringToken token
            || token.ScalarCount != 1
            || token.HasUnpairedSurrogate
            || GetScalar(window.Value, token.Start).Value > 0x7F;

    private static string FormatCodePoints(StringTokenWindow window)
    {
        if (window.Mismatch is not StringToken token)
        {
            return "<end>";
        }

        StringBuilder builder = new();
        int displayed = 0;
        int total = 0;
        for (int i = token.Start; i < token.End;)
        {
            ScalarInfo scalar = GetScalar(window.Value, i);
            if (displayed < MaximumCodePointsToDisplay)
            {
                if (displayed > 0)
                {
                    builder.Append(' ');
                }

                builder.Append("U+");
                builder.Append(scalar.Value.ToString("X4", CultureInfo.InvariantCulture));
                displayed++;
            }

            total++;
            i += scalar.Length;
        }

        if (total > displayed)
        {
            builder.Append(" ... (+");
            builder.Append(total - displayed);
            builder.Append(" code points)");
        }

        return builder.ToString();
    }

    private static ScalarInfo GetPreviousScalar(string value, int end)
    {
        int start = end - 1;
        if (start > 0 && char.IsLowSurrogate(value[start]) && char.IsHighSurrogate(value[start - 1]))
        {
            start--;
        }

        return GetScalar(value, start);
    }

    private static ScalarInfo GetScalar(string value, int start)
    {
        char first = value[start];
        return char.IsHighSurrogate(first)
                && start + 1 < value.Length
                && char.IsLowSurrogate(value[start + 1])
            ? new ScalarInfo(
                start,
                2,
                0x10000 + ((first - 0xD800) << 10) + (value[start + 1] - 0xDC00),
                isUnpairedSurrogate: false)
            : new ScalarInfo(start, 1, first, char.IsSurrogate(first));
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

    private sealed class StringTokenWindow
    {
        internal StringTokenWindow(string value, StringToken[] before, StringToken? mismatch, StringToken[] after)
        {
            Value = value;
            Before = before;
            Mismatch = mismatch;
            After = after;
        }

        internal string Value { get; }

        internal StringToken[] Before { get; }

        internal StringToken? Mismatch { get; }

        internal StringToken[] After { get; }

        internal bool IsRetainedPrefixSafe
        {
            get
            {
                foreach (StringToken token in Before)
                {
                    if (!token.IsSafePrefix)
                    {
                        return false;
                    }
                }

                return Before.Length == 0 || Before[0].Start == 0;
            }
        }

        internal bool MismatchRequiresPlaceholder
            => Mismatch is StringToken token && token.RenderedLength > StringDifferencePreviewBudget / 2;
    }

    private readonly struct StringToken
    {
        internal StringToken(int start, int length, int renderedLength, bool isSafePrefix, int scalarCount, bool hasUnpairedSurrogate)
        {
            Start = start;
            Length = length;
            RenderedLength = renderedLength;
            IsSafePrefix = isSafePrefix;
            ScalarCount = scalarCount;
            HasUnpairedSurrogate = hasUnpairedSurrogate;
        }

        internal int Start { get; }

        internal int Length { get; }

        internal int End => Start + Length;

        internal int RenderedLength { get; }

        internal bool IsSafePrefix { get; }

        internal int ScalarCount { get; }

        internal bool HasUnpairedSurrogate { get; }
    }

    private readonly struct StringPreview
    {
        internal StringPreview(string text, int mismatchColumn, bool isPrefixSafe)
        {
            Text = text;
            MismatchColumn = mismatchColumn;
            IsPrefixSafe = isPrefixSafe;
        }

        internal string Text { get; }

        internal int MismatchColumn { get; }

        internal bool IsPrefixSafe { get; }
    }

    private readonly struct ScalarInfo
    {
        internal ScalarInfo(int start, int length, int value, bool isUnpairedSurrogate)
        {
            Start = start;
            Length = length;
            Value = value;
            IsUnpairedSurrogate = isUnpairedSurrogate;
        }

        internal int Start { get; }

        internal int Length { get; }

        internal int Value { get; }

        internal bool IsUnpairedSurrogate { get; }
    }
}
