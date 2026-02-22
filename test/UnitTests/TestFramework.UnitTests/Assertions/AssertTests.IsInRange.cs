// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using AwesomeAssertions;

using TestFramework.ForTestingMSTest;

namespace Microsoft.VisualStudio.TestTools.UnitTesting.UnitTests;

/// <summary>
/// Unit tests for the Assert.IsInRange methods.
/// </summary>
public partial class AssertTests : TestContainer
{
    #region IsInRange Tests

    public void IsInRange_WithValueInRange_DoesNotThrow()
    {
        // Arrange
        int minValue = 1;
        int maxValue = 10;
        int value = 5;

        // Act & Assert
        Assert.IsInRange(minValue, maxValue, value);
    }

    public void IsInRange_WithValueEqualToMin_DoesNotThrow()
    {
        // Arrange
        int minValue = 1;
        int maxValue = 10;
        int value = 1;

        // Act & Assert
        Assert.IsInRange(minValue, maxValue, value);
    }

    public void IsInRange_WithValueEqualToMax_DoesNotThrow()
    {
        // Arrange
        int minValue = 1;
        int maxValue = 10;
        int value = 10;

        // Act & Assert
        Assert.IsInRange(minValue, maxValue, value);
    }

    public void IsInRange_WithValueBelowRange_ThrowsAssertFailedException()
    {
        // Arrange
        int minValue = 5;
        int maxValue = 10;
        int value = 3;

        // Act & Assert
        Action action = () => Assert.IsInRange(minValue, maxValue, value);
        action.Should().Throw<Exception>()
            .WithMessage("""
                Assert.IsInRange failed.
                Value is not within the expected range.
                  minValue: 5
                  maxValue: 10
                  value: 3
                """);
    }

    public void IsInRange_WithValueAboveRange_ThrowsAssertFailedException()
    {
        // Arrange
        int minValue = 1;
        int maxValue = 5;
        int value = 8;

        // Act & Assert
        Action action = () => Assert.IsInRange(minValue, maxValue, value);
        action.Should().Throw<Exception>()
            .WithMessage("""
                Assert.IsInRange failed.
                Value is not within the expected range.
                  minValue: 1
                  maxValue: 5
                  value: 8
                """);
    }

    public void IsInRange_WithCustomMessage_IncludesCustomMessage()
    {
        // Arrange
        int minValue = 1;
        int maxValue = 5;
        int value = 10;
        string customMessage = "Custom error message";

        // Act
        Action action = () => Assert.IsInRange(minValue, maxValue, value, customMessage);

        // Assert
        action.Should().ThrowExactly<AssertFailedException>()
            .WithMessage("""
                Assert.IsInRange failed. Custom error message
                Value is not within the expected range.
                  minValue: 1
                  maxValue: 5
                  value: 10
                """);
    }

    public void IsInRange_WithDoubleValues_WorksCorrectly()
    {
        // Arrange
        double minValue = 1.5;
        double maxValue = 5.5;
        double valueInRange = 3.0;
        double valueOutOfRange = 6.0;

        // Act & Assert
        Assert.IsInRange(minValue, maxValue, valueInRange);
        Action action = () => Assert.IsInRange(minValue, maxValue, valueOutOfRange);
        action.Should().Throw<Exception>()
            .WithMessage("Assert.IsInRange failed.*Value is not within the expected range*");
    }

    public void IsInRange_WithDateTimeValues_WorksCorrectly()
    {
        // Arrange
        var minValue = new DateTime(2023, 1, 1);
        var maxValue = new DateTime(2023, 12, 31);
        var valueInRange = new DateTime(2023, 6, 15);
        var valueOutOfRange = new DateTime(2024, 1, 1);

        // Act
        Assert.IsInRange(minValue, maxValue, valueInRange);
        Action action = () => Assert.IsInRange(minValue, maxValue, valueOutOfRange);

        // Assert
        action.Should().ThrowExactly<AssertFailedException>()
            .WithMessage("Assert.IsInRange failed.*Value is not within the expected range*");
    }

    public void IsInRange_WithCharValues_WorksCorrectly()
    {
        // Arrange
        char minValue = 'A';
        char maxValue = 'Z';
        char valueInRange = 'M';
        char valueOutOfRange = 'a';

        // Act
        Assert.IsInRange(minValue, maxValue, valueInRange);
        Action action = () => Assert.IsInRange(minValue, maxValue, valueOutOfRange);

        // Assert
        action.Should().ThrowExactly<AssertFailedException>()
            .WithMessage("Assert.IsInRange failed.*Value is not within the expected range*");
    }

    public void IsInRange_WithNullMessage_DoesNotThrow()
    {
        // Arrange
        int minValue = 1;
        int maxValue = 5;
        int value = 3;

        // Act & Assert
        Assert.IsInRange(minValue, maxValue, value, null!);
    }

    public void IsInRange_WithEmptyMessage_DoesNotThrow()
    {
        // Arrange
        int minValue = 1;
        int maxValue = 5;
        int value = 3;

        // Act & Assert
        Assert.IsInRange(minValue, maxValue, value, string.Empty);
    }

    public void IsInRange_WithMessage_DoesNotThrow()
    {
        // Arrange
        int minValue = 1;
        int maxValue = 5;
        int value = 3;

        // Act & Assert
        Assert.IsInRange(minValue, maxValue, value, "Test message");
    }

    public void IsInRange_WithAllNegativeValuesInRange_DoesNotThrow()
    {
        // Arrange
        int minValue = -10;
        int maxValue = -5;
        int value = -7;

        // Act & Assert
        Assert.IsInRange(minValue, maxValue, value);
    }

    public void IsInRange_WithAllNegativeValuesBelowRange_ThrowsAssertFailedException()
    {
        // Arrange
        int minValue = -10;
        int maxValue = -5;
        int value = -12;

        // Act & Assert
        Action action = () => Assert.IsInRange(minValue, maxValue, value);

        // Assert
        action.Should().ThrowExactly<AssertFailedException>()
            .WithMessage("""
                Assert.IsInRange failed.
                Value is not within the expected range.
                  minValue: -10
                  maxValue: -5
                  value: -12
                """);
    }

    public void IsInRange_WithAllNegativeValuesAboveRange_ThrowsAssertFailedException()
    {
        // Arrange
        int minValue = -10;
        int maxValue = -5;
        int value = -3;

        // Act
        Action action = () => Assert.IsInRange(minValue, maxValue, value);

        // Assert
        action.Should().ThrowExactly<AssertFailedException>()
            .WithMessage("""
                Assert.IsInRange failed.
                Value is not within the expected range.
                  minValue: -10
                  maxValue: -5
                  value: -3
                """);
    }

    public void IsInRange_WithRangeSpanningNegativeToPositive_ValueInRange_DoesNotThrow()
    {
        // Arrange
        int minValue = -5;
        int maxValue = 5;
        int value = 0;

        // Act & Assert
        Assert.IsInRange(minValue, maxValue, value);
    }

    public void IsInRange_WithRangeSpanningNegativeToPositive_NegativeValueInRange_DoesNotThrow()
    {
        // Arrange
        int minValue = -5;
        int maxValue = 5;
        int value = -3;

        // Act & Assert
        Assert.IsInRange(minValue, maxValue, value);
    }

    public void IsInRange_WithRangeSpanningNegativeToPositive_PositiveValueInRange_DoesNotThrow()
    {
        // Arrange
        int minValue = -5;
        int maxValue = 5;
        int value = 3;

        // Act & Assert
        Assert.IsInRange(minValue, maxValue, value);
    }

    public void IsInRange_WithRangeSpanningNegativeToPositive_ValueBelowRange_ThrowsAssertFailedException()
    {
        // Arrange
        int minValue = -5;
        int maxValue = 5;
        int value = -7;

        // Act
        Action action = () => Assert.IsInRange(minValue, maxValue, value);

        // Assert
        action.Should().ThrowExactly<AssertFailedException>()
            .WithMessage("""
                Assert.IsInRange failed.
                Value is not within the expected range.
                  minValue: -5
                  maxValue: 5
                  value: -7
                """);
    }

    public void IsInRange_WithRangeSpanningNegativeToPositive_ValueAboveRange_ThrowsAssertFailedException()
    {
        // Arrange
        int minValue = -5;
        int maxValue = 5;
        int value = 7;

        // Act
        Action action = () => Assert.IsInRange(minValue, maxValue, value);

        // Assert
        action.Should().ThrowExactly<AssertFailedException>()
            .WithMessage("""
                Assert.IsInRange failed.
                Value is not within the expected range.
                  minValue: -5
                  maxValue: 5
                  value: 7
                """);
    }

    public void IsInRange_WithNegativeDoubleValues_WorksCorrectly()
    {
        // Arrange
        double minValue = -10.5;
        double maxValue = -2.5;
        double valueInRange = -5.0;

        // Act & Assert
        Assert.IsInRange(minValue, maxValue, valueInRange);
    }

    public void IsInRange_WithMaxValueLessThanMinValue_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        int minValue = 10;
        int maxValue = 5;
        int value = 7;

        // Act
        Action action = () => Assert.IsInRange(minValue, maxValue, value);

        // Assert
        action.Should().ThrowExactly<ArgumentOutOfRangeException>()
            .WithMessage("The maximum value must be greater than or equal to the minimum value*");
    }

    public void IsInRange_WithMaxValueEqualToMinValue_Int_ShouldPassIfValueIsEqual()
    {
        // Arrange
        int minValue = 5;
        int maxValue = 5;
        int value = 5;

        // Act
        Assert.IsInRange(minValue, maxValue, value);
    }

    public void IsInRange_WithMaxValueEqualToMinValue_Int_ShouldFailIfValueIsSmaller()
    {
        // Arrange
        int minValue = 5;
        int maxValue = 5;
        int value = 4;

        Action action = () => Assert.IsInRange(minValue, maxValue, value);
        action.Should().ThrowExactly<AssertFailedException>()
            .WithMessage("""
                Assert.IsInRange failed.
                Value is not within the expected range.
                  minValue: 5
                  maxValue: 5
                  value: 4
                """);
    }

    public void IsInRange_WithMaxValueEqualToMinValue_Int_ShouldFailIfValueIsLarger()
    {
        // Arrange
        int minValue = 5;
        int maxValue = 5;
        int value = 6;

        Action action = () => Assert.IsInRange(minValue, maxValue, value);
        action.Should().ThrowExactly<AssertFailedException>()
            .WithMessage("""
                Assert.IsInRange failed.
                Value is not within the expected range.
                  minValue: 5
                  maxValue: 5
                  value: 6
                """);
    }

    public void IsInRange_WithMaxValueEqualToMinValue_Float_ShouldPassIfValueIsEqual()
    {
        // Arrange
        float minValue = 5.0f;
        float maxValue = 5.0f;
        float value = 5.0f;

        // Act
        Assert.IsInRange(minValue, maxValue, value);
    }

    public void IsInRange_WithMaxValueEqualToMinValue_Float_ShouldFailIfValueIsSmaller()
    {
        // Arrange
        float minValue = 5.0f;
        float maxValue = 5.0f;
        float value = 4.0f;

        Action action = () => Assert.IsInRange(minValue, maxValue, value);
        action.Should().ThrowExactly<AssertFailedException>()
            .WithMessage("""
                Assert.IsInRange failed.
                Value is not within the expected range.
                  minValue: 5
                  maxValue: 5
                  value: 4
                """);
    }

    public void IsInRange_WithMaxValueEqualToMinValue_Float_ShouldFailIfValueIsLarger()
    {
        // Arrange
        float minValue = 5.0f;
        float maxValue = 5.0f;
        float value = 6.0f;

        Action action = () => Assert.IsInRange(minValue, maxValue, value);
        action.Should().ThrowExactly<AssertFailedException>()
            .WithMessage("""
                Assert.IsInRange failed.
                Value is not within the expected range.
                  minValue: 5
                  maxValue: 5
                  value: 6
                """);
    }

    #endregion // IsInRange Tests

    #region IsInRange truncation and newline escaping

    public void IsInRange_WithLongExpression_ShouldTruncateExpression()
    {
        int aVeryLongVariableNameThatExceedsOneHundredCharactersInLengthToTestTruncationBehaviorOfExpressionDisplayXYZ = 20;

        Action action = () => Assert.IsInRange(1, 10, aVeryLongVariableNameThatExceedsOneHundredCharactersInLengthToTestTruncationBehaviorOfExpressionDisplayXYZ);
        action.Should().Throw<AssertFailedException>()
            .WithMessage("""
                Assert.IsInRange failed.
                Value is not within the expected range.
                  minValue: 1
                  maxValue: 10
                  value (aVeryLongVariableNameThatExceedsOneHundredCharactersInLengthToTestTruncationBehaviorOfExpressionDisp...): 20
                """);
    }

    public void IsInRange_WithLongToStringValue_ShouldTruncateValue()
    {
        var min = new IsInRangeComparableWithLongToString(1);
        var max = new IsInRangeComparableWithLongToString(10);
        var value = new IsInRangeComparableWithLongToString(20);

        Action action = () => Assert.IsInRange(min, max, value);
        action.Should().Throw<AssertFailedException>()
            .WithMessage($"""
                Assert.IsInRange failed.
                Value is not within the expected range.
                  minValue (min): {new string('R', 256)}... (300 chars)
                  maxValue (max): {new string('R', 256)}... (300 chars)
                  value: {new string('R', 256)}... (300 chars)
                """);
    }

    public void IsInRange_WithNewlineInToString_ShouldEscapeNewlines()
    {
        var min = new IsInRangeComparableWithNewlineToString(1);
        var max = new IsInRangeComparableWithNewlineToString(10);
        var value = new IsInRangeComparableWithNewlineToString(20);

        Action action = () => Assert.IsInRange(min, max, value);
        action.Should().Throw<AssertFailedException>()
            .WithMessage("""
                Assert.IsInRange failed.
                Value is not within the expected range.
                  minValue (min): line1\r\nline2
                  maxValue (max): line1\r\nline2
                  value: line1\r\nline2
                """);
    }

    #endregion

    private readonly struct IsInRangeComparableWithLongToString : IComparable<IsInRangeComparableWithLongToString>
    {
        private readonly int _value;

        public IsInRangeComparableWithLongToString(int value) => _value = value;

        public int CompareTo(IsInRangeComparableWithLongToString other) => _value.CompareTo(other._value);

        public override string ToString() => new string('R', 300);
    }

    private readonly struct IsInRangeComparableWithNewlineToString : IComparable<IsInRangeComparableWithNewlineToString>
    {
        private readonly int _value;

        public IsInRangeComparableWithNewlineToString(int value) => _value = value;

        public int CompareTo(IsInRangeComparableWithNewlineToString other) => _value.CompareTo(other._value);

        public override string ToString() => "line1\r\nline2";
    }
}
