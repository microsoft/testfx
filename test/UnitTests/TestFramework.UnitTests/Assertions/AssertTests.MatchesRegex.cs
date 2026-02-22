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
                Assert.MatchesRegex failed.
                String does not match expected pattern.
                  pattern: \d+
                  value: "abc"
                """);
    }

    public void DoesNotMatchRegex_WhenValueDoesNotMatchPattern_ShouldPass()
        => Assert.DoesNotMatchRegex(@"\d+", "abc");

    public void DoesNotMatchRegex_WhenValueMatchesPattern_ShouldFail()
    {
        Action action = () => Assert.DoesNotMatchRegex(@"\d+", "abc123");
        action.Should().Throw<AssertFailedException>()
            .WithMessage("""
                Assert.DoesNotMatchRegex failed.
                String matches pattern but should not.
                  pattern: \d+
                  value: "abc123"
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
                Assert.MatchesRegex failed.
                String does not match expected pattern.
                  pattern: \d+
                  value (aVeryLongVariableNameThatExceedsOneHundredCharactersInLengthToTestTruncationBehaviorOfExpressionDisp...): "hello"
                """);
    }

    public void MatchesRegex_WithLongValue_ShouldTruncateValue()
    {
        string longValue = new string('x', 300);

        Action action = () => Assert.MatchesRegex(@"\d+", longValue);
        action.Should().Throw<AssertFailedException>()
            .WithMessage($"""
                Assert.MatchesRegex failed.
                String does not match expected pattern.
                  pattern: \d+
                  value (longValue): "{new string('x', 255)}... (302 chars)
                """);
    }

    public void MatchesRegex_WithNewlineInValue_ShouldEscapeNewlines()
    {
        Action action = () => Assert.MatchesRegex(@"^\d+$", "hello\r\nworld");
        action.Should().Throw<AssertFailedException>()
            .WithMessage("""
                Assert.MatchesRegex failed.
                String does not match expected pattern.
                  pattern: ^\d+$
                  value: "hello\r\nworld"
                """);
    }

    public void DoesNotMatchRegex_WithLongExpression_ShouldTruncateExpression()
    {
        string aVeryLongVariableNameThatExceedsOneHundredCharactersInLengthToTestTruncationBehaviorOfExpressionDisplayXYZ = "abc123";

        Action action = () => Assert.DoesNotMatchRegex(@"\d+", aVeryLongVariableNameThatExceedsOneHundredCharactersInLengthToTestTruncationBehaviorOfExpressionDisplayXYZ);
        action.Should().Throw<AssertFailedException>()
            .WithMessage("""
                Assert.DoesNotMatchRegex failed.
                String matches pattern but should not.
                  pattern: \d+
                  value (aVeryLongVariableNameThatExceedsOneHundredCharactersInLengthToTestTruncationBehaviorOfExpressionDisp...): "abc123"
                """);
    }

    public void DoesNotMatchRegex_WithLongValue_ShouldTruncateValue()
    {
        string longValue = new string('1', 300);

        Action action = () => Assert.DoesNotMatchRegex(@"\d+", longValue);
        action.Should().Throw<AssertFailedException>()
            .WithMessage($"""
                Assert.DoesNotMatchRegex failed.
                String matches pattern but should not.
                  pattern: \d+
                  value (longValue): "{new string('1', 255)}... (302 chars)
                """);
    }

    public void DoesNotMatchRegex_WithNewlineInValue_ShouldEscapeNewlines()
    {
        Action action = () => Assert.DoesNotMatchRegex(@"hello", "hello\r\nworld");
        action.Should().Throw<AssertFailedException>()
            .WithMessage("""
                Assert.DoesNotMatchRegex failed.
                String matches pattern but should not.
                  pattern: hello
                  value: "hello\r\nworld"
                """);
    }

    #endregion
}
