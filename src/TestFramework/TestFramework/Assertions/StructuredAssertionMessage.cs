// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// Builds a structured assertion failure message following the format:
/// <code>
/// Assertion failed. &lt;summary&gt;
/// &lt;user message&gt;
///
/// &lt;evidence block&gt;
///
/// &lt;call-site expression&gt;
/// </code>
/// </summary>
internal sealed class StructuredAssertionMessage
{
    private const string AssertionPrefix = "Assertion failed.";

    private readonly string _summary;
    private readonly List<string> _additionalSummaryLines = [];
    private readonly List<EvidenceBlock> _evidenceBlocks = [];
    private string? _userMessage;
    private string? _callSiteExpression;

    internal StructuredAssertionMessage(string summary)
    {
        _summary = summary;
    }

    internal string? ExpectedText { get; private set; }

    internal string? ActualText { get; private set; }

    internal StructuredAssertionMessage WithAdditionalSummaryLine(string line)
    {
        _additionalSummaryLines.Add(line);
        return this;
    }

    internal StructuredAssertionMessage WithUserMessage(string? userMessage)
    {
        if (!string.IsNullOrWhiteSpace(userMessage))
        {
            _userMessage = userMessage;
        }

        return this;
    }

    internal StructuredAssertionMessage WithEvidence(EvidenceBlock evidenceBlock)
    {
        _evidenceBlocks.Add(evidenceBlock);
        return this;
    }

    internal StructuredAssertionMessage WithExpectedAndActual(string? expectedText, string? actualText)
    {
        ExpectedText = expectedText;
        ActualText = actualText;
        return this;
    }

    internal StructuredAssertionMessage WithCallSiteExpression(string? callSiteExpression)
    {
        if (!string.IsNullOrWhiteSpace(callSiteExpression))
        {
            _callSiteExpression = callSiteExpression;
        }

        return this;
    }

    /// <summary>
    /// Formats the structured message as a multi-line string following the RFC 012 layout.
    /// </summary>
    internal string Format()
    {
        StringBuilder sb = new();

        // Line 1: Assertion prefix + summary
        sb.Append(AssertionPrefix);
        if (!string.IsNullOrEmpty(_summary))
        {
            sb.Append(' ');
            sb.Append(_summary);
        }

        // Additional summary lines
        foreach (string additionalLine in _additionalSummaryLines)
        {
            sb.Append(Environment.NewLine);
            sb.Append(additionalLine);
        }

        // User message (on its own line, no label)
        if (_userMessage is not null)
        {
            sb.Append(Environment.NewLine);
            sb.Append(_userMessage);
        }

        // Evidence blocks (each separated by blank line)
        foreach (EvidenceBlock evidence in _evidenceBlocks)
        {
            string formattedEvidence = evidence.Format();
            if (!string.IsNullOrEmpty(formattedEvidence))
            {
                sb.Append(Environment.NewLine);
                sb.Append(Environment.NewLine);
                sb.Append(formattedEvidence);
            }
        }

        // Call-site expression (separated by blank line)
        if (_callSiteExpression is not null)
        {
            sb.Append(Environment.NewLine);
            sb.Append(Environment.NewLine);
            sb.Append(_callSiteExpression);
        }

        return sb.ToString();
    }

    /// <inheritdoc/>
    public override string ToString() => Format();
}
