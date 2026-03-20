// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using AwesomeAssertions;

namespace Microsoft.VisualStudio.TestPlatform.TestFramework.UnitTests;

public partial class AssertTests
{
    #region MatchesRegex

    public void MatchesRegex_WhenValueMatchesPattern_ShouldPass()
        => Assert.MatchesRegex(@"\d+", "abc123");

    public void MatchesRegex_WhenValueDoesNotMatchPattern_ShouldFail()
    {
        Action action = () => Assert.MatchesRegex(@"\d+", "abc");
        action.Should().Throw<AssertFailedException>()
            .WithMessage("""
                Assert.MatchesRegex(@"\d+", "abc")
                Expected string to match the specified pattern.
                  pattern: \d+
                  value:   "abc"
                """);
    }

    public void DoesNotMatchRegex_WhenValueDoesNotMatchPattern_ShouldPass()
        => Assert.DoesNotMatchRegex(@"\d+", "abc");

    public void DoesNotMatchRegex_WhenValueMatchesPattern_ShouldFail()
    {
        Action action = () => Assert.DoesNotMatchRegex(@"\d+", "abc123");
        action.Should().Throw<AssertFailedException>()
            .WithMessage("""
                Assert.DoesNotMatchRegex(@"\d+", "abc123")
                Expected string to not match the specified pattern.
                  pattern: \d+
                  value:   "abc123"
                """);
    }

    #endregion

    #region MatchesRegex/DoesNotMatchRegex truncation and newline escaping

    public void MatchesRegex_WithLongExpression_ShouldTruncateExpression()
    {
        string aVeryLongVariableNameThatExceedsOneHundredCharactersInLengthToTestTruncationBehaviorOfExpressionDisplayXYZ = "hello";

        Action action = () => Assert.MatchesRegex(@"\d+", aVeryLongVariableNameThatExceedsOneHundredCharactersInLengthToTestTruncationBehaviorOfExpressionDisplayXYZ);
        action.Should().Throw<AssertFailedException>()
            .WithMessage("""
                Assert.MatchesRegex(@"\d+", aVeryLongVariableNameThatExceedsOneHundredCharacte...)
                Expected string to match the specified pattern.
                  pattern: \d+
                  value:   "hello"
                """);
    }

    public void MatchesRegex_WithLongValue_ShouldTruncateValue()
    {
        string longValue = new string('x', 300);

        Action action = () => Assert.MatchesRegex(@"\d+", longValue);
        action.Should().Throw<AssertFailedException>()
            .WithMessage($"""
                Assert.MatchesRegex(@"\d+", longValue)
                Expected string to match the specified pattern.
                  pattern: \d+
                  value:   "{new string('x', 255)}... 46 more
                """);
    }

    public void MatchesRegex_WithNewlineInValue_ShouldEscapeNewlines()
    {
        Action action = () => Assert.MatchesRegex(@"^\d+$", "hello\r\nworld");
        action.Should().Throw<AssertFailedException>()
            .WithMessage("""
                Assert.MatchesRegex(@"^\d+$", "hello\r\nworld")
                Expected string to match the specified pattern.
                  pattern: ^\d+$
                  value:   "hello\r\nworld"
                """);
    }

    public void DoesNotMatchRegex_WithLongExpression_ShouldTruncateExpression()
    {
        string aVeryLongVariableNameThatExceedsOneHundredCharactersInLengthToTestTruncationBehaviorOfExpressionDisplayXYZ = "abc123";

        Action action = () => Assert.DoesNotMatchRegex(@"\d+", aVeryLongVariableNameThatExceedsOneHundredCharactersInLengthToTestTruncationBehaviorOfExpressionDisplayXYZ);
        action.Should().Throw<AssertFailedException>()
            .WithMessage("""
                Assert.DoesNotMatchRegex(@"\d+", aVeryLongVariableNameThatExceedsOneHundredCharacte...)
                Expected string to not match the specified pattern.
                  pattern: \d+
                  value:   "abc123"
                """);
    }

    public void DoesNotMatchRegex_WithLongValue_ShouldTruncateValue()
    {
        string longValue = new string('1', 300);

        Action action = () => Assert.DoesNotMatchRegex(@"\d+", longValue);
        action.Should().Throw<AssertFailedException>()
            .WithMessage($"""
                Assert.DoesNotMatchRegex(@"\d+", longValue)
                Expected string to not match the specified pattern.
                  pattern: \d+
                  value:   "{new string('1', 255)}... 46 more
                """);
    }

    public void DoesNotMatchRegex_WithNewlineInValue_ShouldEscapeNewlines()
    {
        Action action = () => Assert.DoesNotMatchRegex(@"hello", "hello\r\nworld");
        action.Should().Throw<AssertFailedException>()
            .WithMessage("""
                Assert.DoesNotMatchRegex(@"hello", "hello\r\nworld")
                Expected string to not match the specified pattern.
                  pattern: hello
                  value:   "hello\r\nworld"
                """);
    }

    #endregion
}
