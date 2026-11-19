// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// Represents the evidence block of a structured assertion message, containing labeled value lines
/// such as expected/actual values and assertion-specific details.
/// </summary>
[StackTraceHidden]
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
        int len = value.Length;
        int i = 0;
        while (i < len)
        {
            char c = value[i];
            if (c is '\n' or '\r')
            {
                sb.Append(value, start, i - start);
                int next = i + 1;
                if (c == '\r' && next < len && value[next] == '\n')
                {
                    next++;
                }

                sb.Append(Environment.NewLine);

                // Skip the continuation indent when the line break is the last thing in the value, or when the next
                // character is itself another line break. The latter avoids emitting an indent on a line that is
                // intentionally blank, which would leave whitespace-only continuation lines.
                if (next < len && value[next] is not '\n' and not '\r')
                {
                    sb.Append(continuationIndent);
                }

                start = next;
                i = next;
                continue;
            }

            i++;
        }

        if (start < len)
        {
            sb.Append(value, start, len - start);
        }
    }
}
