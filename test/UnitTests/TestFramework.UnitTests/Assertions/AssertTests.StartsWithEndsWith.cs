// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using AwesomeAssertions;

namespace Microsoft.VisualStudio.TestPlatform.TestFramework.UnitTests;

public partial class AssertTests
{
    public void StartsWith_WhenValueStartsWithPrefix_DoesNotThrow()
        => Assert.StartsWith("foo", "foobar");

    public void StartsWith_OnFailure_ShouldIncludeStructuredMessageAndPayload()
    {
        string value = "bar";
        Action action = () => Assert.StartsWith("foo", value, "User-provided message");

        AssertFailedException ex = action.Should().Throw<AssertFailedException>()
            .WithMessage(
                """
                Assertion failed. Expected string to start with the specified prefix.
                User-provided message

                expected prefix: "foo"
                actual:          "bar"
                comparison:      Ordinal

                Assert.StartsWith("foo", value)
                """)
            .Which;

        ex.ExpectedText.Should().Be("\"foo\"");
        ex.ActualText.Should().Be("\"bar\"");
        ex.Data["assert.expected"].Should().Be(ex.ExpectedText);
        ex.Data["assert.actual"].Should().Be(ex.ActualText);
    }

    public void DoesNotStartWith_WhenValueDoesNotStartWithPrefix_DoesNotThrow()
        => Assert.DoesNotStartWith("foo", "barfoo");

    public void DoesNotStartWith_OnFailure_ShouldIncludeStructuredMessageAndPayload()
    {
        string value = "foobar";
        Action action = () => Assert.DoesNotStartWith("foo", value, "User-provided message");

        AssertFailedException ex = action.Should().Throw<AssertFailedException>()
            .WithMessage(
                """
                Assertion failed. Expected string to not start with the specified prefix.
                User-provided message

                unexpected prefix: "foo"
                actual:            "foobar"
                comparison:        Ordinal

                Assert.DoesNotStartWith("foo", value)
                """)
            .Which;

        ex.ExpectedText.Should().Be("\"foo\"");
        ex.ActualText.Should().Be("\"foobar\"");
        ex.Data["assert.expected"].Should().Be(ex.ExpectedText);
        ex.Data["assert.actual"].Should().Be(ex.ActualText);
    }

    public void EndsWith_WhenValueEndsWithSuffix_DoesNotThrow()
        => Assert.EndsWith("bar", "foobar");

    public void EndsWith_OnFailure_ShouldIncludeStructuredMessageAndPayload()
    {
        string value = "foobar";
        Action action = () => Assert.EndsWith("baz", value, "User-provided message");

        AssertFailedException ex = action.Should().Throw<AssertFailedException>()
            .WithMessage(
                """
                Assertion failed. Expected string to end with the specified suffix.
                User-provided message

                expected suffix: "baz"
                actual:          "foobar"
                comparison:      Ordinal

                Assert.EndsWith("baz", value)
                """)
            .Which;

        ex.ExpectedText.Should().Be("\"baz\"");
        ex.ActualText.Should().Be("\"foobar\"");
        ex.Data["assert.expected"].Should().Be(ex.ExpectedText);
        ex.Data["assert.actual"].Should().Be(ex.ActualText);
    }

    public void DoesNotEndWith_WhenValueDoesNotEndWithSuffix_DoesNotThrow()
        => Assert.DoesNotEndWith("baz", "foobar");

    public void DoesNotEndWith_OnFailure_ShouldIncludeStructuredMessageAndPayload()
    {
        string value = "foobar";
        Action action = () => Assert.DoesNotEndWith("bar", value, "User-provided message");

        AssertFailedException ex = action.Should().Throw<AssertFailedException>()
            .WithMessage(
                """
                Assertion failed. Expected string to not end with the specified suffix.
                User-provided message

                unexpected suffix: "bar"
                actual:            "foobar"
                comparison:        Ordinal

                Assert.DoesNotEndWith("bar", value)
                """)
            .Which;

        ex.ExpectedText.Should().Be("\"bar\"");
        ex.ActualText.Should().Be("\"foobar\"");
        ex.Data["assert.expected"].Should().Be(ex.ExpectedText);
        ex.Data["assert.actual"].Should().Be(ex.ActualText);
    }

    public void StartsWith_WithOrdinalIgnoreCase_WhenMatchesCaseInsensitively_DoesNotThrow()
        => Assert.StartsWith("FOO", "foobar", StringComparison.OrdinalIgnoreCase);

    public void StartsWith_WithOrdinalIgnoreCase_WhenMatchesExactly_DoesNotThrow()
        => Assert.StartsWith("foo", "foobar", StringComparison.OrdinalIgnoreCase);

    public void StartsWith_WithOrdinalIgnoreCase_OnFailure_ShouldIncludeComparisonInMessage()
    {
        string value = "bar";
        Action action = () => Assert.StartsWith("foo", value, StringComparison.OrdinalIgnoreCase, "User-provided message");

        AssertFailedException ex = action.Should().Throw<AssertFailedException>()
            .WithMessage(
                """
                Assertion failed. Expected string to start with the specified prefix.
                User-provided message

                expected prefix: "foo"
                actual:          "bar"
                comparison:      OrdinalIgnoreCase

                Assert.StartsWith("foo", value)
                """)
            .Which;

        ex.ExpectedText.Should().Be("\"foo\"");
        ex.ActualText.Should().Be("\"bar\"");
    }

    public void StartsWith_WhenValueIsNull_ShouldThrow()
    {
        Action action = () => Assert.StartsWith("foo", null!);
        action.Should().Throw<AssertFailedException>()
            .WithMessage("Assert.StartsWith failed. The parameter 'value' is invalid. The value cannot be null.");
    }

    public void StartsWith_WhenPrefixIsNull_ShouldThrow()
    {
        Action action = () => Assert.StartsWith(null!, "foobar");
        action.Should().Throw<AssertFailedException>()
            .WithMessage("Assert.StartsWith failed. The parameter 'expectedPrefix' is invalid. The value cannot be null.");
    }

    public void DoesNotStartWith_WithOrdinalIgnoreCase_WhenDoesNotMatchCaseInsensitively_DoesNotThrow()
        => Assert.DoesNotStartWith("baz", "foobar", StringComparison.OrdinalIgnoreCase);

    public void DoesNotStartWith_WithOrdinalIgnoreCase_OnFailure_ShouldIncludeComparisonInMessage()
    {
        string value = "foobar";
        Action action = () => Assert.DoesNotStartWith("FOO", value, StringComparison.OrdinalIgnoreCase, "User-provided message");

        AssertFailedException ex = action.Should().Throw<AssertFailedException>()
            .WithMessage(
                """
                Assertion failed. Expected string to not start with the specified prefix.
                User-provided message

                unexpected prefix: "FOO"
                actual:            "foobar"
                comparison:        OrdinalIgnoreCase

                Assert.DoesNotStartWith("FOO", value)
                """)
            .Which;

        ex.ExpectedText.Should().Be("\"FOO\"");
        ex.ActualText.Should().Be("\"foobar\"");
    }

    public void DoesNotStartWith_WhenValueIsNull_ShouldThrow()
    {
        Action action = () => Assert.DoesNotStartWith("foo", null!);
        action.Should().Throw<AssertFailedException>()
            .WithMessage("Assert.DoesNotStartWith failed. The parameter 'value' is invalid. The value cannot be null.");
    }

    public void DoesNotStartWith_WhenPrefixIsNull_ShouldThrow()
    {
        Action action = () => Assert.DoesNotStartWith(null!, "foobar");
        action.Should().Throw<AssertFailedException>()
            .WithMessage("Assert.DoesNotStartWith failed. The parameter 'notExpectedPrefix' is invalid. The value cannot be null.");
    }

    public void EndsWith_WithOrdinalIgnoreCase_WhenMatchesCaseInsensitively_DoesNotThrow()
        => Assert.EndsWith("BAR", "foobar", StringComparison.OrdinalIgnoreCase);

    public void EndsWith_WithOrdinalIgnoreCase_WhenMatchesExactly_DoesNotThrow()
        => Assert.EndsWith("bar", "foobar", StringComparison.OrdinalIgnoreCase);

    public void EndsWith_WithOrdinalIgnoreCase_OnFailure_ShouldIncludeComparisonInMessage()
    {
        string value = "foobar";
        Action action = () => Assert.EndsWith("baz", value, StringComparison.OrdinalIgnoreCase, "User-provided message");

        AssertFailedException ex = action.Should().Throw<AssertFailedException>()
            .WithMessage(
                """
                Assertion failed. Expected string to end with the specified suffix.
                User-provided message

                expected suffix: "baz"
                actual:          "foobar"
                comparison:      OrdinalIgnoreCase

                Assert.EndsWith("baz", value)
                """)
            .Which;

        ex.ExpectedText.Should().Be("\"baz\"");
        ex.ActualText.Should().Be("\"foobar\"");
    }

    public void EndsWith_WhenValueIsNull_ShouldThrow()
    {
        Action action = () => Assert.EndsWith("bar", null!);
        action.Should().Throw<AssertFailedException>()
            .WithMessage("Assert.EndsWith failed. The parameter 'value' is invalid. The value cannot be null.");
    }

    public void EndsWith_WhenSuffixIsNull_ShouldThrow()
    {
        Action action = () => Assert.EndsWith(null!, "foobar");
        action.Should().Throw<AssertFailedException>()
            .WithMessage("Assert.EndsWith failed. The parameter 'expectedSuffix' is invalid. The value cannot be null.");
    }

    public void DoesNotEndWith_WithOrdinalIgnoreCase_WhenDoesNotMatchCaseInsensitively_DoesNotThrow()
        => Assert.DoesNotEndWith("baz", "foobar", StringComparison.OrdinalIgnoreCase);

    public void DoesNotEndWith_WithOrdinalIgnoreCase_OnFailure_ShouldIncludeComparisonInMessage()
    {
        string value = "foobar";
        Action action = () => Assert.DoesNotEndWith("BAR", value, StringComparison.OrdinalIgnoreCase, "User-provided message");

        AssertFailedException ex = action.Should().Throw<AssertFailedException>()
            .WithMessage(
                """
                Assertion failed. Expected string to not end with the specified suffix.
                User-provided message

                unexpected suffix: "BAR"
                actual:            "foobar"
                comparison:        OrdinalIgnoreCase

                Assert.DoesNotEndWith("BAR", value)
                """)
            .Which;

        ex.ExpectedText.Should().Be("\"BAR\"");
        ex.ActualText.Should().Be("\"foobar\"");
    }

    public void DoesNotEndWith_WhenValueIsNull_ShouldThrow()
    {
        Action action = () => Assert.DoesNotEndWith("bar", null!);
        action.Should().Throw<AssertFailedException>()
            .WithMessage("Assert.DoesNotEndWith failed. The parameter 'value' is invalid. The value cannot be null.");
    }

    public void DoesNotEndWith_WhenSuffixIsNull_ShouldThrow()
    {
        Action action = () => Assert.DoesNotEndWith(null!, "foobar");
        action.Should().Throw<AssertFailedException>()
            .WithMessage("Assert.DoesNotEndWith failed. The parameter 'notExpectedSuffix' is invalid. The value cannot be null.");
    }
}
