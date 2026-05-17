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
}
