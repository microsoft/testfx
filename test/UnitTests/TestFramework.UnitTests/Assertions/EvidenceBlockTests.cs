// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using AwesomeAssertions;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using TestFramework.ForTestingMSTest;

namespace Microsoft.VisualStudio.TestPlatform.TestFramework.UnitTests;

public class EvidenceBlockTests : TestContainer
{
    public void Format_EmptyBlock_ReturnsEmptyString()
    {
        var block = EvidenceBlock.Create();

        string result = block.Format();

        result.Should().BeEmpty();
    }

    public void Format_SingleLine_FormatsLabelAndValue()
    {
        EvidenceBlock block = EvidenceBlock.Create()
            .AddLine("expected:", "42");

        string result = block.Format();

        result.Should().Be("expected: 42");
    }

    public void Format_TwoLines_AlignsLabels()
    {
        EvidenceBlock block = EvidenceBlock.Create()
            .AddLine("expected:", "42")
            .AddLine("actual:", "37");

        string result = block.Format();

        // "expected:" is 9 chars, "actual:" is 7 chars
        // "actual:" should be padded to 9 chars for alignment
        result.Should().Be(
            $"""
            expected: 42
            actual:   37
            """);
    }

    public void Format_MultipleLines_AlignsToLongestLabel()
    {
        EvidenceBlock block = EvidenceBlock.Create()
            .AddLine("expected:", "42")
            .AddLine("actual:", "37")
            .AddLine("ignore case:", "true")
            .AddLine("culture:", "tr-TR");

        string result = block.Format();

        result.Should().Be(
            $"""
            expected:    42
            actual:      37
            ignore case: true
            culture:     tr-TR
            """);
    }

    public void Lines_ReturnsAddedLines()
    {
        EvidenceBlock block = EvidenceBlock.Create()
            .AddLine("expected:", "42")
            .AddLine("actual:", "37");

        block.Lines.Should().HaveCount(2);
        block.Lines[0].Label.Should().Be("expected:");
        block.Lines[0].Value.Should().Be("42");
        block.Lines[1].Label.Should().Be("actual:");
        block.Lines[1].Value.Should().Be("37");
    }

    public void Format_ValueWithLF_IndentsContinuationToValueColumn()
    {
        EvidenceBlock block = EvidenceBlock.Create()
            .AddLine("expected:", "line1\nline2");

        string result = block.Format();

        // "expected:" is 9 chars + 1 space = 10 chars of indent.
        result.Should().Be(
            $"""
            expected: line1
                      line2
            """);
    }

    public void Format_ValueWithCRLF_IndentsContinuationOnce()
    {
        EvidenceBlock block = EvidenceBlock.Create()
            .AddLine("expected:", "line1\r\nline2");

        string result = block.Format();

        result.Should().Be(
            $"""
            expected: line1
                      line2
            """);
    }

    public void Format_ValueWithCROnly_IndentsContinuation()
    {
        EvidenceBlock block = EvidenceBlock.Create()
            .AddLine("expected:", "line1\rline2");

        string result = block.Format();

        result.Should().Be(
            $"""
            expected: line1
                      line2
            """);
    }

    public void Format_ValueWithMultipleNewlines_IndentsAllContinuations()
    {
        EvidenceBlock block = EvidenceBlock.Create()
            .AddLine("expected:", "a\nb\nc");

        string result = block.Format();

        result.Should().Be(
            $"""
            expected: a
                      b
                      c
            """);
    }

    public void Format_ValueEndingWithNewline_DoesNotEmitTrailingIndent()
    {
        EvidenceBlock block = EvidenceBlock.Create()
            .AddLine("expected:", "abc\n")
            .AddLine("actual:", "37");

        string result = block.Format();

        // No row of pure whitespace between the two lines.
        result.Should().Be(
            $"""
            expected: abc

            actual:   37
            """);
    }

    public void Format_ValueWithConsecutiveNewlines_DoesNotEmitWhitespaceOnlyLines()
    {
        EvidenceBlock block = EvidenceBlock.Create()
            .AddLine("expected:", "a\n\nb");

        string result = block.Format();

        // The blank line between "a" and "b" must not contain the continuation indent.
        result.Should().Be(
            $"""
            expected: a

                      b
            """);
    }

    public void Format_MixedSingleAndMultiLineValues_AlignsToValueColumn()
    {
        EvidenceBlock block = EvidenceBlock.Create()
            .AddLine("expected type:", "Foo")
            .AddLine("actual exception:", "System.InvalidOperationException: line1\nline2");

        string result = block.Format();

        // Longest label is "actual exception:" (17 chars) + 1 space = 18 chars indent for continuation.
        result.Should().Be(
            $"""
            expected type:    Foo
            actual exception: System.InvalidOperationException: line1
                              line2
            """);
    }
}
