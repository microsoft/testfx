// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestTools.UnitTesting;

public sealed partial class Assert
{
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
