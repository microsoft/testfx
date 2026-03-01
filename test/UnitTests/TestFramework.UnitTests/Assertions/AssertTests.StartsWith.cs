// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using AwesomeAssertions;

namespace Microsoft.VisualStudio.TestPlatform.TestFramework.UnitTests;

public partial class AssertTests
{
    #region StartsWith

    public void StartsWith_WhenValueStartsWithPrefix_ShouldPass()
        => Assert.StartsWith("hello", "hello world");

    public void StartsWith_WhenValueDoesNotStartWithPrefix_ShouldFail()
    {
        Action action = () => Assert.StartsWith("world", "hello");
        action.Should().Throw<AssertFailedException>()
            .WithMessage("""
                Assert.StartsWith failed.
                String does not start with expected prefix.
                  expectedPrefix: "world"
                  value: "hello"
                """);
    }

    public void StartsWith_WithMessage_WhenValueDoesNotStartWithPrefix_ShouldFail()
    {
        Action action = () => Assert.StartsWith("world", "hello", "User message");
        action.Should().Throw<AssertFailedException>()
            .WithMessage("""
                Assert.StartsWith failed. User message
                String does not start with expected prefix.
                  expectedPrefix: "world"
                  value: "hello"
                """);
    }

    public void DoesNotStartWith_WhenValueDoesNotStartWithPrefix_ShouldPass()
        => Assert.DoesNotStartWith("world", "hello");

    public void DoesNotStartWith_WhenValueStartsWithPrefix_ShouldFail()
    {
        Action action = () => Assert.DoesNotStartWith("hello", "hello world");
        action.Should().Throw<AssertFailedException>()
            .WithMessage("""
                Assert.DoesNotStartWith failed.
                String starts with unexpected prefix.
                  notExpectedPrefix: "hello"
                  value: "hello world"
                """);
    }

    #endregion

    #region StartsWith/DoesNotStartWith truncation and newline escaping

    public void StartsWith_WithLongExpression_ShouldTruncateExpression()
    {
        string aVeryLongVariableNameThatExceedsOneHundredCharactersInLengthToTestTruncationBehaviorOfExpressionDisplayXYZ = "hello world";

        Action action = () => Assert.StartsWith("world", aVeryLongVariableNameThatExceedsOneHundredCharactersInLengthToTestTruncationBehaviorOfExpressionDisplayXYZ);
        action.Should().Throw<AssertFailedException>()
            .WithMessage("""
                Assert.StartsWith failed.
                String does not start with expected prefix.
                  expectedPrefix: "world"
                  value (aVeryLongVariableNameThatExceedsOneHundredCharactersInLengthToTestTruncationBehaviorOfExpressionDisp...): "hello world"
                """);
    }

    public void StartsWith_WithLongValue_ShouldTruncateValue()
    {
        string longValue = new string('x', 300);

        Action action = () => Assert.StartsWith("world", longValue);
        action.Should().Throw<AssertFailedException>()
            .WithMessage($"""
                Assert.StartsWith failed.
                String does not start with expected prefix.
                  expectedPrefix: "world"
                  value (longValue): "{new string('x', 255)}... (302 chars)
                """);
    }

    public void StartsWith_WithNewlineInValue_ShouldEscapeNewlines()
    {
        Action action = () => Assert.StartsWith("world", "hello\r\nworld");
        action.Should().Throw<AssertFailedException>()
            .WithMessage("""
                Assert.StartsWith failed.
                String does not start with expected prefix.
                  expectedPrefix: "world"
                  value: "hello\r\nworld"
                """);
    }

    public void DoesNotStartWith_WithLongExpression_ShouldTruncateExpression()
    {
        string aVeryLongVariableNameThatExceedsOneHundredCharactersInLengthToTestTruncationBehaviorOfExpressionDisplayXYZ = "hello world";

        Action action = () => Assert.DoesNotStartWith("hello", aVeryLongVariableNameThatExceedsOneHundredCharactersInLengthToTestTruncationBehaviorOfExpressionDisplayXYZ);
        action.Should().Throw<AssertFailedException>()
            .WithMessage("""
                Assert.DoesNotStartWith failed.
                String starts with unexpected prefix.
                  notExpectedPrefix: "hello"
                  value (aVeryLongVariableNameThatExceedsOneHundredCharactersInLengthToTestTruncationBehaviorOfExpressionDisp...): "hello world"
                """);
    }

    public void DoesNotStartWith_WithLongValue_ShouldTruncateValue()
    {
        string longValue = "hello" + new string('x', 300);

        Action action = () => Assert.DoesNotStartWith("hello", longValue);
        action.Should().Throw<AssertFailedException>()
            .WithMessage($"""
                Assert.DoesNotStartWith failed.
                String starts with unexpected prefix.
                  notExpectedPrefix: "hello"
                  value (longValue): "hello{new string('x', 250)}... (307 chars)
                """);
    }

    public void DoesNotStartWith_WithNewlineInValue_ShouldEscapeNewlines()
    {
        Action action = () => Assert.DoesNotStartWith("hello", "hello\r\nworld");
        action.Should().Throw<AssertFailedException>()
            .WithMessage("""
                Assert.DoesNotStartWith failed.
                String starts with unexpected prefix.
                  notExpectedPrefix: "hello"
                  value: "hello\r\nworld"
                """);
    }

    #endregion
}
