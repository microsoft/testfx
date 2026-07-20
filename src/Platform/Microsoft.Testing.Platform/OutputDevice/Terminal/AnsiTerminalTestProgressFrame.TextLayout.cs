// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.OutputDevice.Terminal;

internal sealed partial class AnsiTerminalTestProgressFrame
{
    private static void AppendProgressMessageToWidth(AnsiTerminal terminal, string text, int availableWidth)
    {
        List<TextElement> elements = [];
        int totalWidth = 0;
        bool textWasNormalized = false;
        TextElementEnumerator enumerator = StringInfo.GetTextElementEnumerator(text);
        while (enumerator.MoveNext())
        {
            string originalElement = enumerator.GetTextElement();
            TextElement element = CreateTextElement(originalElement);
            textWasNormalized |= !ReferenceEquals(originalElement, element.Text);
            if (IsRegionalIndicator(element.Text)
                && elements.Count > 0
                && IsRegionalIndicator(elements[^1].Text))
            {
                TextElement previousElement = elements[^1];
                elements[^1] = new TextElement(previousElement.Text + element.Text, Width: 2);
                totalWidth += 2 - previousElement.Width;
            }
            else
            {
                elements.Add(element);
                totalWidth += element.Width;
            }
        }

        if (totalWidth <= availableWidth)
        {
            if (textWasNormalized)
            {
                foreach (TextElement element in elements)
                {
                    terminal.Append(element.Text);
                }
            }
            else
            {
                terminal.Append(text);
            }

            return;
        }

        if (availableWidth < 3)
        {
            AppendPrefixToWidth(terminal, elements, availableWidth);
            return;
        }

        terminal.Append("...");
        int remainingWidth = availableWidth - 3;
        int firstElementToAppend = elements.Count;
        for (int i = elements.Count - 1; i >= 0; i--)
        {
            if (elements[i].Width > remainingWidth)
            {
                break;
            }

            remainingWidth -= elements[i].Width;
            firstElementToAppend = i;
        }

        for (int i = firstElementToAppend; i < elements.Count; i++)
        {
            terminal.Append(elements[i].Text);
        }
    }

    private static void AppendToWidth(AnsiTerminal terminal, string text, int width, ref int charsTaken)
    {
        if (charsTaken + text.Length < width)
        {
            terminal.Append(text);
            charsTaken += text.Length;
        }
        else
        {
            terminal.Append("...");
            charsTaken += 3;
            if (charsTaken < width)
            {
                int charsToTake = width - charsTaken;
                string cutText = text[^charsToTake..];
                terminal.Append(cutText);
                charsTaken += charsToTake;
            }
        }
    }

    private static void AppendPrefixToWidth(AnsiTerminal terminal, List<TextElement> elements, int availableWidth)
    {
        foreach (TextElement element in elements)
        {
            if (element.Width > availableWidth)
            {
                break;
            }

            terminal.Append(element.Text);
            availableWidth -= element.Width;
        }
    }

    private static TextElement CreateTextElement(string textElement)
    {
        int width = 0;
        bool hasEmojiPresentationSelector = false;
        StringBuilder? normalizedText = null;
        for (int i = 0; i < textElement.Length;)
        {
            int codePoint;
            int codeUnitCount;
            bool isMalformedSurrogate = char.IsSurrogate(textElement[i])
                && !char.IsSurrogatePair(textElement, i);
            if (isMalformedSurrogate)
            {
                normalizedText ??= new StringBuilder(textElement.Length).Append(textElement, 0, i);
                normalizedText.Append('\uFFFD');
                codePoint = 0xFFFD;
                codeUnitCount = 1;
            }
            else
            {
                codePoint = char.ConvertToUtf32(textElement, i);
                codeUnitCount = char.IsSurrogatePair(textElement, i) ? 2 : 1;
                normalizedText?.Append(textElement, i, codeUnitCount);
            }

            hasEmojiPresentationSelector |= codePoint == 0xFE0F;
            UnicodeCategory category = isMalformedSurrogate
                ? UnicodeCategory.OtherSymbol
                : CharUnicodeInfo.GetUnicodeCategory(textElement, i);
            if (category is not (UnicodeCategory.NonSpacingMark or UnicodeCategory.EnclosingMark or UnicodeCategory.Format))
            {
                width = Math.Max(width, IsWideCodePoint(codePoint) ? 2 : 1);
            }

            i += codeUnitCount;
        }

        return new TextElement(
            normalizedText?.ToString() ?? textElement,
            hasEmojiPresentationSelector ? Math.Max(width, 2) : width);
    }

    private static bool IsWideCodePoint(int codePoint)
        => codePoint is >= 0x1100
            and (<= 0x115F
                or 0x2329
                or 0x232A
                or (>= 0x2E80 and <= 0xA4CF and not 0x303F)
                or (>= 0xAC00 and <= 0xD7A3)
                or (>= 0xF900 and <= 0xFAFF)
                or (>= 0xFE10 and <= 0xFE19)
                or (>= 0xFE30 and <= 0xFE6F)
                or (>= 0xFF00 and <= 0xFF60)
                or (>= 0xFFE0 and <= 0xFFE6)
                or (>= 0x1F1E6 and <= 0x1F1FF)
                or (>= 0x1F300 and <= 0x1FAFF)
                or (>= 0x20000 and <= 0x3FFFD));

    private static bool IsRegionalIndicator(string textElement)
        => textElement.Length == 2
            && char.IsSurrogatePair(textElement, 0)
            && char.ConvertToUtf32(textElement, 0) is >= 0x1F1E6 and <= 0x1F1FF;

    private readonly record struct TextElement(string Text, int Width);
}
