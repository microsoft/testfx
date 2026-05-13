// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using AwesomeAssertions;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using TestFramework.ForTestingMSTest;

namespace Microsoft.VisualStudio.TestPlatform.TestFramework.UnitTests;

public class StructuredAssertionMessageTests : TestContainer
{
    public void Format_SummaryOnly_ReturnsAssertionPrefix()
    {
        StructuredAssertionMessage message = new("Expected values to be equal.");

        string result = message.Format();

        result.Should().Be("Assertion failed. Expected values to be equal.");
    }

    public void Format_EmptySummary_ReturnsJustPrefix()
    {
        StructuredAssertionMessage message = new(string.Empty);

        string result = message.Format();

        result.Should().Be("Assertion failed.");
    }

    public void Format_WithUserMessage_ShowsMessageAfterSummary()
    {
        StructuredAssertionMessage message = new("Expected values to be equal.");
        message.WithUserMessage("Discount should be applied after tax");

        string result = message.Format();

        result.Should().Be(
            """
            Assertion failed. Expected values to be equal.
            Discount should be applied after tax
            """);
    }

    public void Format_WithEvidenceBlock_SeparatedByBlankLine()
    {
        EvidenceBlock evidence = EvidenceBlock.Create()
            .AddLine("expected:", "42")
            .AddLine("actual:", "37");

        StructuredAssertionMessage message = new("Expected values to be equal.");
        message.WithEvidence(evidence);

        string result = message.Format();

        result.Should().Be(
            """
            Assertion failed. Expected values to be equal.

            expected: 42
            actual:   37
            """);
    }

    public void Format_WithUserMessageAndEvidence_CorrectLayout()
    {
        EvidenceBlock evidence = EvidenceBlock.Create()
            .AddLine("expected:", "42")
            .AddLine("actual:", "37");

        StructuredAssertionMessage message = new("Expected values to be equal.");
        message.WithUserMessage("Discount should be applied after tax");
        message.WithEvidence(evidence);

        string result = message.Format();

        result.Should().Be(
            """
            Assertion failed. Expected values to be equal.
            Discount should be applied after tax

            expected: 42
            actual:   37
            """);
    }

    public void Format_WithCallSiteExpression_SeparatedByBlankLine()
    {
        EvidenceBlock evidence = EvidenceBlock.Create()
            .AddLine("expected:", "42")
            .AddLine("actual:", "37");

        StructuredAssertionMessage message = new("Expected values to be equal.");
        message.WithEvidence(evidence);
        message.WithCallSiteExpression("Assert.AreEqual(expectedCount, actualCount)");

        string result = message.Format();

        result.Should().Be(
            """
            Assertion failed. Expected values to be equal.

            expected: 42
            actual:   37

            Assert.AreEqual(expectedCount, actualCount)
            """);
    }

    public void Format_FullMessage_CorrectLayout()
    {
        EvidenceBlock evidence = EvidenceBlock.Create()
            .AddLine("expected:", "42")
            .AddLine("actual:", "37");

        StructuredAssertionMessage message = new("Expected values to be equal.");
        message.WithAdditionalSummaryLine("Values differ at position 3.");
        message.WithUserMessage("Check the discount logic");
        message.WithEvidence(evidence);
        message.WithCallSiteExpression("Assert.AreEqual(expected, actual)");

        string result = message.Format();

        result.Should().Be(
            """
            Assertion failed. Expected values to be equal.
            Values differ at position 3.
            Check the discount logic

            expected: 42
            actual:   37

            Assert.AreEqual(expected, actual)
            """);
    }

    public void Format_NullUserMessage_OmitsUserMessageLine()
    {
        StructuredAssertionMessage message = new("Expected values to be equal.");
        message.WithUserMessage(null);

        string result = message.Format();

        result.Should().Be("Assertion failed. Expected values to be equal.");
    }

    public void Format_WhitespaceUserMessage_OmitsUserMessageLine()
    {
        StructuredAssertionMessage message = new("Expected values to be equal.");
        message.WithUserMessage("   ");

        string result = message.Format();

        result.Should().Be("Assertion failed. Expected values to be equal.");
    }

    public void WithExpectedAndActual_SetsProperties()
    {
        StructuredAssertionMessage message = new("Expected values to be equal.");
        message.WithExpectedAndActual("42", "37");

        message.ExpectedText.Should().Be("42");
        message.ActualText.Should().Be("37");
    }

    public void ToString_ReturnsSameAsFormat()
    {
        StructuredAssertionMessage message = new("Expected values to be equal.");

        message.ToString().Should().Be(message.Format());
    }

    public void Format_WithMultipleEvidenceBlocks_SeparatedByBlankLines()
    {
        EvidenceBlock evidence1 = EvidenceBlock.Create()
            .AddLine("expected:", "42")
            .AddLine("actual:", "37");

        EvidenceBlock evidence2 = EvidenceBlock.Create()
            .AddLine("ignore case:", "true")
            .AddLine("culture:", "tr-TR");

        StructuredAssertionMessage message = new("Expected values to be equal.");
        message.WithEvidence(evidence1);
        message.WithEvidence(evidence2);

        string result = message.Format();

        result.Should().Be(
            """
            Assertion failed. Expected values to be equal.

            expected: 42
            actual:   37

            ignore case: true
            culture:     tr-TR
            """);
    }
}
