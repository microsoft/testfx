// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text.RegularExpressions;

using AwesomeAssertions;

namespace Microsoft.VisualStudio.TestPlatform.TestFramework.UnitTests;

public partial class AssertTests
{
    public void MatchesRegex_WithRegexPattern_OnSuccess_DoesNotThrow()
        => FluentActions.Invoking(() => Assert.MatchesRegex(new Regex("^he"), "hello"))
            .Should().NotThrow();

    public void MatchesRegex_WithStringPattern_OnSuccess_DoesNotThrow()
        => FluentActions.Invoking(() => Assert.MatchesRegex("^he", "hello"))
            .Should().NotThrow();

    public void MatchesRegex_WithRegexPattern_OnFailure_UsesStructuredMessageAndPayload()
    {
        Action action = () => Assert.MatchesRegex(new Regex("^foo"), "hello", "User-provided message");

        AssertFailedException ex = action.Should().Throw<AssertFailedException>()
            .WithMessage(
                """
                Assertion failed. Expected string to match the specified pattern.
                User-provided message

                expected pattern: "^foo"
                actual:           "hello"

                Assert.MatchesRegex(new Regex("^foo"), "hello")
                """)
            .Which;

        ex.ExpectedText.Should().Be("\"^foo\"");
        ex.ActualText.Should().Be("\"hello\"");
        ex.Data["assert.expected"].Should().Be(ex.ExpectedText);
        ex.Data["assert.actual"].Should().Be(ex.ActualText);
    }

    public void MatchesRegex_WithStringPattern_OnFailure_UsesStructuredMessage()
    {
        string pattern = "^foo";
        string value = "hello";
        Action action = () => Assert.MatchesRegex(pattern, value);

        action.Should().Throw<AssertFailedException>()
            .WithMessage(
                """
                Assertion failed. Expected string to match the specified pattern.

                expected pattern: "^foo"
                actual:           "hello"

                Assert.MatchesRegex(pattern, value)
                """);
    }

    public void DoesNotMatchRegex_WithRegexPattern_OnSuccess_DoesNotThrow()
        => FluentActions.Invoking(() => Assert.DoesNotMatchRegex(new Regex("world"), "hello"))
            .Should().NotThrow();

    public void DoesNotMatchRegex_WithStringPattern_OnSuccess_DoesNotThrow()
        => FluentActions.Invoking(() => Assert.DoesNotMatchRegex("world", "hello"))
            .Should().NotThrow();

    public void DoesNotMatchRegex_WithRegexPattern_OnFailure_UsesStructuredMessageAndPayload()
    {
        Action action = () => Assert.DoesNotMatchRegex(new Regex("world"), "hello world", "User-provided message");

        AssertFailedException ex = action.Should().Throw<AssertFailedException>()
            .WithMessage(
                """
                Assertion failed. Expected string to not match the specified pattern.
                User-provided message

                unexpected pattern: "world"
                actual:             "hello world"

                Assert.DoesNotMatchRegex(new Regex("world"), "hello world")
                """)
            .Which;

        ex.ExpectedText.Should().Be("\"world\"");
        ex.ActualText.Should().Be("\"hello world\"");
        ex.Data["assert.expected"].Should().Be(ex.ExpectedText);
        ex.Data["assert.actual"].Should().Be(ex.ActualText);
    }

    public void DoesNotMatchRegex_WithStringPattern_OnFailure_UsesStructuredMessage()
    {
        string pattern = "world";
        string value = "hello world";
        Action action = () => Assert.DoesNotMatchRegex(pattern, value);

        action.Should().Throw<AssertFailedException>()
            .WithMessage(
                """
                Assertion failed. Expected string to not match the specified pattern.

                unexpected pattern: "world"
                actual:             "hello world"

                Assert.DoesNotMatchRegex(pattern, value)
                """);
    }
}
