// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestTools.UnitTesting;

public sealed partial class Assert
{
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
            => (Before.Length == 0 || Before[0].Start == 0)
                && Before.All(token => token.IsSafePrefix);

        internal bool MismatchRequiresPlaceholder
            => Mismatch is StringToken token && token.RenderedLength > StringDifferencePreviewBudget / 2;
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
}
