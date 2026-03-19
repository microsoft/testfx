// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using AwesomeAssertions;

namespace Microsoft.VisualStudio.TestPlatform.TestFramework.UnitTests;

public partial class AssertTests
{
    #region EndsWith

    public void EndsWith_WhenValueEndsWithSuffix_ShouldPass()
        => Assert.EndsWith("world", "hello world");

    public void EndsWith_WhenValueDoesNotEndWithSuffix_ShouldFail()
    {
        Action action = () => Assert.EndsWith("hello", "world");
        action.Should().Throw<AssertFailedException>()
            .WithMessage("""
                Assert.EndsWith("hello", "world")
                String does not end with expected suffix.
                  expected suffix: "hello"
                  value:           "world"
                """);
    }

    public void EndsWith_WithMessage_WhenValueDoesNotEndWithSuffix_ShouldFail()
    {
        Action action = () => Assert.EndsWith("hello", "world", "User message");
        action.Should().Throw<AssertFailedException>()
            .WithMessage("""
                Assert.EndsWith("hello", "world")
                String does not end with expected suffix.
                  expected suffix: "hello"
                  value:           "world"
                User message: User message
                """);
    }

    public void DoesNotEndWith_WhenValueDoesNotEndWithSuffix_ShouldPass()
        => Assert.DoesNotEndWith("hello", "world");

    public void DoesNotEndWith_WhenValueEndsWithSuffix_ShouldFail()
    {
        Action action = () => Assert.DoesNotEndWith("world", "hello world");
        action.Should().Throw<AssertFailedException>()
            .WithMessage("""
                Assert.DoesNotEndWith("world", "hello world")
                String ends with unexpected suffix.
                  unwanted suffix: "world"
                  value:           "hello world"
                """);
    }

    #endregion

    #region EndsWith/DoesNotEndWith truncation and newline escaping

    public void EndsWith_WithLongExpression_ShouldTruncateExpression()
    {
        string aVeryLongVariableNameThatExceedsOneHundredCharactersInLengthToTestTruncationBehaviorOfExpressionDisplayXYZ = "hello world";

        Action action = () => Assert.EndsWith("hello", aVeryLongVariableNameThatExceedsOneHundredCharactersInLengthToTestTruncationBehaviorOfExpressionDisplayXYZ);
        action.Should().Throw<AssertFailedException>()
            .WithMessage("""
                Assert.EndsWith("hello", aVeryLongVariableNameThatExceedsOneHundredCharacte...)
                String does not end with expected suffix.
                  expected suffix: "hello"
                  value:           "hello world"
                """);
    }

    public void EndsWith_WithLongValue_ShouldTruncateValue()
    {
        string longValue = new string('x', 300);

        Action action = () => Assert.EndsWith("world", longValue);
        action.Should().Throw<AssertFailedException>()
            .WithMessage($"""
                Assert.EndsWith("world", longValue)
                String does not end with expected suffix.
                  expected suffix: "world"
                  value:           "{new string('x', 255)}... 46 more
                """);
    }

    public void EndsWith_WithNewlineInValue_ShouldEscapeNewlines()
    {
        Action action = () => Assert.EndsWith("world", "hello\r\nfoo");
        action.Should().Throw<AssertFailedException>()
            .WithMessage("""
                Assert.EndsWith("world", "hello\r\nfoo")
                String does not end with expected suffix.
                  expected suffix: "world"
                  value:           "hello\r\nfoo"
                """);
    }

    public void DoesNotEndWith_WithLongExpression_ShouldTruncateExpression()
    {
        string aVeryLongVariableNameThatExceedsOneHundredCharactersInLengthToTestTruncationBehaviorOfExpressionDisplayXYZ = "hello world";

        Action action = () => Assert.DoesNotEndWith("world", aVeryLongVariableNameThatExceedsOneHundredCharactersInLengthToTestTruncationBehaviorOfExpressionDisplayXYZ);
        action.Should().Throw<AssertFailedException>()
            .WithMessage("""
                Assert.DoesNotEndWith("world", aVeryLongVariableNameThatExceedsOneHundredCharacte...)
                String ends with unexpected suffix.
                  unwanted suffix: "world"
                  value:           "hello world"
                """);
    }

    public void DoesNotEndWith_WithLongValue_ShouldTruncateValue()
    {
        string longValue = new string('x', 300) + "world";

        Action action = () => Assert.DoesNotEndWith("world", longValue);
        action.Should().Throw<AssertFailedException>()
            .WithMessage($"""
                Assert.DoesNotEndWith("world", longValue)
                String ends with unexpected suffix.
                  unwanted suffix: "world"
                  value:           "{new string('x', 255)}... 51 more
                """);
    }

    public void DoesNotEndWith_WithNewlineInValue_ShouldEscapeNewlines()
    {
        Action action = () => Assert.DoesNotEndWith("world", "hello\r\nworld");
        action.Should().Throw<AssertFailedException>()
            .WithMessage("""
                Assert.DoesNotEndWith("world", "hello\r\nworld")
                String ends with unexpected suffix.
                  unwanted suffix: "world"
                  value:           "hello\r\nworld"
                """);
    }

    #endregion
}
