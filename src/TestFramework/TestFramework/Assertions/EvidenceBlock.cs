// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// Represents the evidence block of a structured assertion message, containing labeled value lines
/// such as expected/actual values and assertion-specific details.
/// </summary>
internal sealed class EvidenceBlock
{
    private readonly List<EvidenceLine> _lines = [];

    internal static EvidenceBlock Create() => new();

    internal IReadOnlyList<EvidenceLine> Lines => _lines;

    internal EvidenceBlock AddLine(string label, string value)
    {
        _lines.Add(new EvidenceLine(label, value));
        return this;
    }

    /// <summary>
    /// Formats the evidence block as aligned label: value lines.
    /// Labels are right-padded so all values start at the same column. Values that span multiple lines
    /// are re-indented so continuation lines align under the first value column.
    /// </summary>
    internal string Format()
    {
        if (_lines.Count == 0)
        {
            return string.Empty;
        }

        int maxLabelLength = 0;
        foreach (EvidenceLine line in _lines)
        {
            if (line.Label.Length > maxLabelLength)
            {
                maxLabelLength = line.Label.Length;
            }
        }

        string continuationIndent = new(' ', maxLabelLength + 1);

        StringBuilder sb = new();
        for (int i = 0; i < _lines.Count; i++)
        {
            if (i > 0)
            {
                sb.Append(Environment.NewLine);
            }

            EvidenceLine line = _lines[i];

            sb.Append(line.Label.PadRight(maxLabelLength));
            sb.Append(' ');
            AppendValue(sb, line.Value, continuationIndent);
        }

        return sb.ToString();
    }

    private static void AppendValue(StringBuilder sb, string value, string continuationIndent)
    {
        int start = 0;
        for (int i = 0; i < value.Length; i++)
        {
            char c = value[i];
            if (c is '\n' or '\r')
            {
                sb.Append(value, start, i - start);
                sb.Append(Environment.NewLine);
                sb.Append(continuationIndent);

                if (c == '\r' && i + 1 < value.Length && value[i + 1] == '\n')
                {
                    i++;
                }

                start = i + 1;
            }
        }

        if (start < value.Length)
        {
            sb.Append(value, start, value.Length - start);
        }
    }
}
