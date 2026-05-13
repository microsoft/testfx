// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// Represents the evidence block of a structured assertion message, containing labeled value lines
/// such as expected/actual values and assertion-specific details.
/// </summary>
internal readonly struct EvidenceBlock
{
    private readonly List<EvidenceLine> _lines;

    private EvidenceBlock(List<EvidenceLine> lines)
    {
        _lines = lines;
    }

    internal static EvidenceBlock Create() => new([]);

    internal IReadOnlyList<EvidenceLine> Lines => _lines;

    internal EvidenceBlock AddLine(string label, string value)
    {
        _lines.Add(new EvidenceLine(label, value));
        return this;
    }

    /// <summary>
    /// Formats the evidence block as aligned label: value lines.
    /// Labels are right-padded so all values start at the same column.
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

        StringBuilder sb = new();
        for (int i = 0; i < _lines.Count; i++)
        {
            if (i > 0)
            {
                sb.Append(Environment.NewLine);
            }

            EvidenceLine line = _lines[i];

            // Pad label (which includes trailing colon) to align values, then append a space and value
            sb.Append(line.Label.PadRight(maxLabelLength));
            sb.Append(' ');
            sb.Append(line.Value);
        }

        return sb.ToString();
    }
}
