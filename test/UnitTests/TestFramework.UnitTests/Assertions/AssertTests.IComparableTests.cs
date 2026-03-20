// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using AwesomeAssertions;

using TestFramework.ForTestingMSTest;

namespace Microsoft.VisualStudio.TestPlatform.TestFramework.UnitTests;

public partial class AssertTests : TestContainer
{
    #region IsGreaterThan tests

    public void IsGreaterThanShouldNotThrowWhenActualIsGreater() =>
        Assert.IsGreaterThan(5, 10);

    public void IsGreaterThanShouldWorkWithReferenceTypes() =>
        Assert.IsGreaterThan("a", "b");

    public void IsGreaterThanShouldThrowWhenActualIsNotGreater()
    {
        // Act
        Action action = () => Assert.IsGreaterThan(10, 5);

        // Assert
        action.Should().Throw<AssertFailedException>();
    }

    public void IsGreaterThanShouldThrowWhenBothAreEqual()
    {
        // Act
        Action action = () => Assert.IsGreaterThan(5, 5);

        // Assert
        action.Should().Throw<AssertFailedException>();
    }

    public void IsGreaterThanShouldThrowWithMessage()
    {
        // Act
        Action action = () => Assert.IsGreaterThan(10, 5, "A Message");

        // Assert
        action.Should().Throw<AssertFailedException>()
            .WithMessage("""
                Assert.IsGreaterThan(10, 5)
                A Message
                Expected value 5 to be greater than 10.
                  lower bound: 10
                  value:       5
                """);
    }

    public void IsGreaterThanShouldWorkWithDoubles() =>
        Assert.IsGreaterThan(5.0, 5.5);

    public void IsGreaterThanShouldThrowWithDoubles()
    {
        // Act
        Action action = () => Assert.IsGreaterThan(5.5, 5.0);

        // Assert
        action.Should().Throw<AssertFailedException>();
    }

    #endregion

    #region IsGreaterThanOrEqualTo tests

    public void IsGreaterThanOrEqualToShouldNotThrowWhenActualIsGreater() =>
        Assert.IsGreaterThanOrEqualTo(5, 10);

    public void IsGreaterThanOrEqualToShouldWorkWithReferenceTypes() =>
        Assert.IsGreaterThanOrEqualTo("a", "b");

    public void IsGreaterThanOrEqualToShouldNotThrowWhenBothAreEqual() =>
        Assert.IsGreaterThanOrEqualTo(5, 5);

    public void IsGreaterThanOrEqualToShouldThrowWhenActualIsLess()
    {
        // Act
        Action action = () => Assert.IsGreaterThanOrEqualTo(10, 5);

        // Assert
        action.Should().Throw<AssertFailedException>();
    }

    public void IsGreaterThanOrEqualToShouldThrowWithMessage()
    {
        // Act
        Action action = () => Assert.IsGreaterThanOrEqualTo(10, 5, "A Message");

        // Assert
        action.Should().Throw<AssertFailedException>()
            .WithMessage("""
                Assert.IsGreaterThanOrEqualTo(10, 5)
                A Message
                Expected value 5 to be greater than or equal to 10.
                  lower bound: 10
                  value:       5
                """);
    }

    public void IsGreaterThanOrEqualToShouldWorkWithDoubles() =>
        Assert.IsGreaterThanOrEqualTo(5.0, 5.5);

    public void IsGreaterThanOrEqualToShouldWorkWithEqualDoubles() =>
        Assert.IsGreaterThanOrEqualTo(5.5, 5.5);

    #endregion

    #region IsLessThan tests

    public void IsLessThanShouldNotThrowWhenActualIsLess() =>
        Assert.IsLessThan(10, 5);

    public void IsLessThanShouldWorkWithReferenceTypes() =>
        Assert.IsLessThan("b", "a");

    public void IsLessThanShouldThrowWhenActualIsNotLess()
    {
        // Act
        Action action = () => Assert.IsLessThan(5, 10);

        // Assert
        action.Should().Throw<AssertFailedException>();
    }

    public void IsLessThanShouldThrowWhenBothAreEqual()
    {
        // Act
        Action action = () => Assert.IsLessThan(5, 5);

        // Assert
        action.Should().Throw<AssertFailedException>();
    }

    public void IsLessThanShouldThrowWithMessage()
    {
        // Act
        Action action = () => Assert.IsLessThan(5, 10, "A Message");

        // Assert
        action.Should().Throw<AssertFailedException>()
            .WithMessage("""
                Assert.IsLessThan(5, 10)
                A Message
                Expected value 10 to be less than 5.
                  upper bound: 5
                  value:       10
                """);
    }

    public void IsLessThanShouldWorkWithDoubles() =>
        Assert.IsLessThan(5.5, 5.0);

    public void IsLessThanShouldThrowWithDoubles()
    {
        // Act
        Action action = () => Assert.IsLessThan(5.0, 5.5);

        // Assert
        action.Should().Throw<AssertFailedException>();
    }

    #endregion

    #region IsLessThanOrEqualTo tests

    public void IsLessThanOrEqualToShouldNotThrowWhenActualIsLess() =>
        Assert.IsLessThanOrEqualTo(10, 5);

    public void IsLessThanOrEqualToShouldWorkWithReferenceTypes() =>
        Assert.IsLessThanOrEqualTo("b", "a");

    public void IsLessThanOrEqualToShouldNotThrowWhenBothAreEqual() =>
        Assert.IsLessThanOrEqualTo(5, 5);

    public void IsLessThanOrEqualToShouldThrowWhenActualIsGreater()
    {
        // Act
        Action action = () => Assert.IsLessThanOrEqualTo(5, 10);

        // Assert
        action.Should().Throw<AssertFailedException>();
    }

    public void IsLessThanOrEqualToShouldThrowWithMessage()
    {
        // Act
        Action action = () => Assert.IsLessThanOrEqualTo(5, 10, "A Message");

        // Assert
        action.Should().Throw<AssertFailedException>()
            .WithMessage("""
                Assert.IsLessThanOrEqualTo(5, 10)
                A Message
                Expected value 10 to be less than or equal to 5.
                  upper bound: 5
                  value:       10
                """);
    }

    public void IsLessThanOrEqualToShouldWorkWithDoubles() =>
        Assert.IsLessThanOrEqualTo(5.5, 5.0);

    public void IsLessThanOrEqualToShouldWorkWithEqualDoubles() =>
        Assert.IsLessThanOrEqualTo(5.5, 5.5);

    #endregion

    #region IsPositive tests

    public void IsPositiveShouldNotThrowForPositiveNumber() =>
        Assert.IsPositive(5);

    public void IsPositiveShouldThrowForZero()
    {
        // Act
        Action action = () => Assert.IsPositive(0);

        // Assert
        action.Should().Throw<AssertFailedException>();
    }

    public void IsPositiveShouldThrowForNegativeNumber()
    {
        // Act
        Action action = () => Assert.IsPositive(-5);

        // Assert
        action.Should().Throw<AssertFailedException>();
    }

    public void IsPositiveShouldThrowForNaN()
    {
        // Act
        Action action = () => Assert.IsPositive(float.NaN);

        // Assert
        action.Should().Throw<AssertFailedException>();
    }

    public void IsPositiveShouldThrowForDoubleNaN()
    {
        // Act
        Action action = () => Assert.IsPositive(double.NaN);

        // Assert
        action.Should().Throw<AssertFailedException>();
    }

    public void IsPositiveShouldThrowWithMessage()
    {
        // Act
        Action action = () => Assert.IsPositive(-5, "A Message");

        // Assert
        action.Should().Throw<AssertFailedException>()
            .WithMessage("""
                Assert.IsPositive(-5)
                A Message
                Expected a positive value.
                  value: -5
                """);
    }

    public void IsPositiveShouldWorkWithDoubles() =>
        Assert.IsPositive(5.5);

    public void IsPositiveShouldThrowForZeroDouble()
    {
        // Act
        Action action = () => Assert.IsPositive(0.0);

        // Assert
        action.Should().Throw<AssertFailedException>();
    }

    #endregion

    #region IsNegative tests

    public void IsNegativeShouldNotThrowForNegativeNumber() =>
        Assert.IsNegative(-5);

    public void IsNegativeShouldThrowForZero()
    {
        // Act
        Action action = () => Assert.IsNegative(0);

        // Assert
        action.Should().Throw<AssertFailedException>();
    }

    public void IsNegativeShouldThrowForPositiveNumber()
    {
        // Act
        Action action = () => Assert.IsNegative(5);

        // Assert
        action.Should().Throw<AssertFailedException>();
    }

    public void IsNegativeShouldThrowForNaN()
    {
        // Act
        Action action = () => Assert.IsNegative(float.NaN);

        // Assert
        action.Should().Throw<AssertFailedException>();
    }

    public void IsNegativeShouldThrowForDoubleNaN()
    {
        // Act
        Action action = () => Assert.IsNegative(double.NaN);

        // Assert
        action.Should().Throw<AssertFailedException>();
    }

    public void IsNegativeShouldThrowWithMessage()
    {
        // Act
        Action action = () => Assert.IsNegative(5, "A Message");

        // Assert
        action.Should().Throw<AssertFailedException>()
            .WithMessage("""
                Assert.IsNegative(5)
                A Message
                Expected a negative value.
                  value: 5
                """);
    }

    public void IsNegativeShouldWorkWithDoubles() =>
        Assert.IsNegative(-5.5);

    public void IsNegativeShouldThrowForZeroDouble()
    {
        // Act
        Action action = () => Assert.IsNegative(0.0);

        // Assert
        action.Should().Throw<AssertFailedException>();
    }

    #endregion

    #region IComparable truncation and newline escaping

    public void IsGreaterThan_WithLongExpression_ShouldTruncateExpression()
    {
        int aVeryLongVariableNameThatExceedsOneHundredCharactersInLengthToTestTruncationBehaviorOfExpressionDisplayXYZ = 10;

        Action action = () => Assert.IsGreaterThan(aVeryLongVariableNameThatExceedsOneHundredCharactersInLengthToTestTruncationBehaviorOfExpressionDisplayXYZ, 5);
        action.Should().Throw<AssertFailedException>()
            .WithMessage("""
                Assert.IsGreaterThan(aVeryLongVariableNameThatExceedsOneHundredCharacte..., 5)
                Expected value 5 to be greater than 10.
                  lower bound: 10
                  value:       5
                """);
    }

    public void IsGreaterThan_WithLongToStringValue_ShouldTruncateValue()
    {
        var lowerBound = new ComparableWithLongToString(10);
        var value = new ComparableWithLongToString(5);

        Action action = () => Assert.IsGreaterThan(lowerBound, value);
        action.Should().Throw<AssertFailedException>()
            .WithMessage($"""
                Assert.IsGreaterThan(lowerBound, value)
                Expected value {new string('V', 256)}... 44 more to be greater than {new string('V', 256)}... 44 more.
                  lower bound: {new string('V', 256)}... 44 more
                  value:       {new string('V', 256)}... 44 more
                """);
    }

    public void IsGreaterThan_WithNewlineInToString_ShouldEscapeNewlines()
    {
        var lowerBound = new ComparableWithNewlineToString(10);
        var value = new ComparableWithNewlineToString(5);

        Action action = () => Assert.IsGreaterThan(lowerBound, value);
        action.Should().Throw<AssertFailedException>()
            .WithMessage("""
                Assert.IsGreaterThan(lowerBound, value)
                Expected value line1\r\nline2 to be greater than line1\r\nline2.
                  lower bound: line1\r\nline2
                  value:       line1\r\nline2
                """);
    }

    public void IsGreaterThanOrEqualTo_WithLongExpression_ShouldTruncateExpression()
    {
        int aVeryLongVariableNameThatExceedsOneHundredCharactersInLengthToTestTruncationBehaviorOfExpressionDisplayXYZ = 10;

        Action action = () => Assert.IsGreaterThanOrEqualTo(aVeryLongVariableNameThatExceedsOneHundredCharactersInLengthToTestTruncationBehaviorOfExpressionDisplayXYZ, 5);
        action.Should().Throw<AssertFailedException>()
            .WithMessage("""
                Assert.IsGreaterThanOrEqualTo(aVeryLongVariableNameThatExceedsOneHundredCharacte..., 5)
                Expected value 5 to be greater than or equal to 10.
                  lower bound: 10
                  value:       5
                """);
    }

    public void IsGreaterThanOrEqualTo_WithLongToStringValue_ShouldTruncateValue()
    {
        var lowerBound = new ComparableWithLongToString(10);
        var value = new ComparableWithLongToString(5);

        Action action = () => Assert.IsGreaterThanOrEqualTo(lowerBound, value);
        action.Should().Throw<AssertFailedException>()
            .WithMessage($"""
                Assert.IsGreaterThanOrEqualTo(lowerBound, value)
                Expected value {new string('V', 256)}... 44 more to be greater than or equal to {new string('V', 256)}... 44 more.
                  lower bound: {new string('V', 256)}... 44 more
                  value:       {new string('V', 256)}... 44 more
                """);
    }

    public void IsGreaterThanOrEqualTo_WithNewlineInToString_ShouldEscapeNewlines()
    {
        var lowerBound = new ComparableWithNewlineToString(10);
        var value = new ComparableWithNewlineToString(5);

        Action action = () => Assert.IsGreaterThanOrEqualTo(lowerBound, value);
        action.Should().Throw<AssertFailedException>()
            .WithMessage("""
                Assert.IsGreaterThanOrEqualTo(lowerBound, value)
                Expected value line1\r\nline2 to be greater than or equal to line1\r\nline2.
                  lower bound: line1\r\nline2
                  value:       line1\r\nline2
                """);
    }

    public void IsLessThan_WithLongExpression_ShouldTruncateExpression()
    {
        int aVeryLongVariableNameThatExceedsOneHundredCharactersInLengthToTestTruncationBehaviorOfExpressionDisplayXYZ = 5;

        Action action = () => Assert.IsLessThan(aVeryLongVariableNameThatExceedsOneHundredCharactersInLengthToTestTruncationBehaviorOfExpressionDisplayXYZ, 10);
        action.Should().Throw<AssertFailedException>()
            .WithMessage("""
                Assert.IsLessThan(aVeryLongVariableNameThatExceedsOneHundredCharacte..., 10)
                Expected value 10 to be less than 5.
                  upper bound: 5
                  value:       10
                """);
    }

    public void IsLessThan_WithLongToStringValue_ShouldTruncateValue()
    {
        var upperBound = new ComparableWithLongToString(5);
        var value = new ComparableWithLongToString(10);

        Action action = () => Assert.IsLessThan(upperBound, value);
        action.Should().Throw<AssertFailedException>()
            .WithMessage($"""
                Assert.IsLessThan(upperBound, value)
                Expected value {new string('V', 256)}... 44 more to be less than {new string('V', 256)}... 44 more.
                  upper bound: {new string('V', 256)}... 44 more
                  value:       {new string('V', 256)}... 44 more
                """);
    }

    public void IsLessThan_WithNewlineInToString_ShouldEscapeNewlines()
    {
        var upperBound = new ComparableWithNewlineToString(5);
        var value = new ComparableWithNewlineToString(10);

        Action action = () => Assert.IsLessThan(upperBound, value);
        action.Should().Throw<AssertFailedException>()
            .WithMessage("""
                Assert.IsLessThan(upperBound, value)
                Expected value line1\r\nline2 to be less than line1\r\nline2.
                  upper bound: line1\r\nline2
                  value:       line1\r\nline2
                """);
    }

    public void IsLessThanOrEqualTo_WithLongExpression_ShouldTruncateExpression()
    {
        int aVeryLongVariableNameThatExceedsOneHundredCharactersInLengthToTestTruncationBehaviorOfExpressionDisplayXYZ = 5;

        Action action = () => Assert.IsLessThanOrEqualTo(aVeryLongVariableNameThatExceedsOneHundredCharactersInLengthToTestTruncationBehaviorOfExpressionDisplayXYZ, 10);
        action.Should().Throw<AssertFailedException>()
            .WithMessage("""
                Assert.IsLessThanOrEqualTo(aVeryLongVariableNameThatExceedsOneHundredCharacte..., 10)
                Expected value 10 to be less than or equal to 5.
                  upper bound: 5
                  value:       10
                """);
    }

    public void IsLessThanOrEqualTo_WithLongToStringValue_ShouldTruncateValue()
    {
        var upperBound = new ComparableWithLongToString(5);
        var value = new ComparableWithLongToString(10);

        Action action = () => Assert.IsLessThanOrEqualTo(upperBound, value);
        action.Should().Throw<AssertFailedException>()
            .WithMessage($"""
                Assert.IsLessThanOrEqualTo(upperBound, value)
                Expected value {new string('V', 256)}... 44 more to be less than or equal to {new string('V', 256)}... 44 more.
                  upper bound: {new string('V', 256)}... 44 more
                  value:       {new string('V', 256)}... 44 more
                """);
    }

    public void IsLessThanOrEqualTo_WithNewlineInToString_ShouldEscapeNewlines()
    {
        var upperBound = new ComparableWithNewlineToString(5);
        var value = new ComparableWithNewlineToString(10);

        Action action = () => Assert.IsLessThanOrEqualTo(upperBound, value);
        action.Should().Throw<AssertFailedException>()
            .WithMessage("""
                Assert.IsLessThanOrEqualTo(upperBound, value)
                Expected value line1\r\nline2 to be less than or equal to line1\r\nline2.
                  upper bound: line1\r\nline2
                  value:       line1\r\nline2
                """);
    }

    public void IsPositive_WithLongExpression_ShouldTruncateExpression()
    {
        int aVeryLongVariableNameThatExceedsOneHundredCharactersInLengthToTestTruncationBehaviorOfExpressionDisplayXYZ = -5;

        Action action = () => Assert.IsPositive(aVeryLongVariableNameThatExceedsOneHundredCharactersInLengthToTestTruncationBehaviorOfExpressionDisplayXYZ);
        action.Should().Throw<AssertFailedException>()
            .WithMessage("""
                Assert.IsPositive(aVeryLongVariableNameThatExceedsOneHundredCharacte...)
                Expected a positive value.
                  value: -5
                """);
    }

    public void IsNegative_WithLongExpression_ShouldTruncateExpression()
    {
        int aVeryLongVariableNameThatExceedsOneHundredCharactersInLengthToTestTruncationBehaviorOfExpressionDisplayXYZ = 5;

        Action action = () => Assert.IsNegative(aVeryLongVariableNameThatExceedsOneHundredCharactersInLengthToTestTruncationBehaviorOfExpressionDisplayXYZ);
        action.Should().Throw<AssertFailedException>()
            .WithMessage("""
                Assert.IsNegative(aVeryLongVariableNameThatExceedsOneHundredCharacte...)
                Expected a negative value.
                  value: 5
                """);
    }

    #endregion

    private sealed class ComparableWithLongToString : IComparable<ComparableWithLongToString>
    {
        private readonly int _value;

        public ComparableWithLongToString(int value) => _value = value;

        public int CompareTo(ComparableWithLongToString? other) => _value.CompareTo(other?._value ?? 0);

        public override string ToString() => new string('V', 300);
    }

    private sealed class ComparableWithNewlineToString : IComparable<ComparableWithNewlineToString>
    {
        private readonly int _value;

        public ComparableWithNewlineToString(int value) => _value = value;

        public int CompareTo(ComparableWithNewlineToString? other) => _value.CompareTo(other?._value ?? 0);

        public override string ToString() => "line1\r\nline2";
    }
}
