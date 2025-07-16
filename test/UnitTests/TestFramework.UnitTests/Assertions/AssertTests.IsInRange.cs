// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using FluentAssertions;

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
        Exception ex = VerifyThrows(() => Assert.IsInRange(minValue, maxValue, value));
        Verify(ex.Message.Contains("Value '3' is not within the expected range [5, 10]"));
    }

    public void IsInRange_WithValueAboveRange_ThrowsAssertFailedException()
    {
        // Arrange
        int minValue = 1;
        int maxValue = 5;
        int value = 8;

        // Act & Assert
        Exception ex = VerifyThrows(() => Assert.IsInRange(minValue, maxValue, value));
        Verify(ex.Message.Contains("Value '8' is not within the expected range [1, 5]"));
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
            .And.Message.Should().Contain("Value '10' is not within the expected range [1, 5]")
            .And.Contain(customMessage);
    }

    public void IsInRange_WithMessage_FormatsMessage()
    {
        // Arrange
        int minValue = 1;
        int maxValue = 5;
        int value = 10;
        string message = "Test with parameter: TestValue";

        // Act
        Action action = () => Assert.IsInRange(minValue, maxValue, value, message);

        // Assert
        action.Should().ThrowExactly<AssertFailedException>()
            .And.Message.Should().Contain("Value '10' is not within the expected range [1, 5]")
            .And.Contain("Test with parameter: TestValue");
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
        Exception ex = VerifyThrows(() => Assert.IsInRange(minValue, maxValue, valueOutOfRange));
        Verify(ex.Message.Contains("Value '6' is not within the expected range [1.5, 5.5]"));
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
            .And.Message.Should().Contain("is not within the expected range");
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
            .And.Message.Should().Contain("Value 'a' is not within the expected range [A, Z]");
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
            .And.Message.Should().Contain("Value '-12' is not within the expected range [-10, -5]");
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
            .And.Message.Should().Contain("Value '-3' is not within the expected range [-10, -5]");
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
            .And.Message.Should().Contain("Value '-7' is not within the expected range [-5, 5]");
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
            .And.Message.Should().Contain("Value '7' is not within the expected range [-5, 5]");
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
            .And.Message.Should().Contain("The maximum value must be greater than the minimum value");
    }

    public void IsInRange_WithMaxValueEqualToMinValue_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        int minValue = 5;
        int maxValue = 5;
        int value = 5;

        // Act
        Action action = () => Assert.IsInRange(minValue, maxValue, value);

        // Assert
        action.Should().ThrowExactly<ArgumentOutOfRangeException>()
            .And.Message.Should().Contain("The maximum value must be greater than the minimum value");
    }

    #endregion // IsInRange Tests
}
