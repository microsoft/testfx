// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using FluentAssertions;

using TestFramework.ForTestingMSTest;

namespace Microsoft.VisualStudio.TestTools.UnitTesting.UnitTests;

/// <summary>
/// Unit tests for the <see cref="Assert.IsInRange{T}"/> methods.
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

        // Act & Assert
        Exception ex = VerifyThrows(() => Assert.IsInRange(minValue, maxValue, value, customMessage));
        Verify(ex.Message.Contains("Value '10' is not within the expected range [1, 5]"));
        Verify(ex.Message.Contains(customMessage));
    }

    public void IsInRange_WithMessageAndParameters_FormatsMessage()
    {
        // Arrange
        int minValue = 1;
        int maxValue = 5;
        int value = 10;
        string messageFormat = "Test with parameter: {0}";
        string parameter = "TestValue";

        // Act & Assert
        Exception ex = VerifyThrows(() => Assert.IsInRange(minValue, maxValue, value, messageFormat, parameter));
        Verify(ex.Message.Contains("Value '10' is not within the expected range [1, 5]"));
        Verify(ex.Message.Contains("Test with parameter: TestValue"));
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
        DateTime minValue = new DateTime(2023, 1, 1);
        DateTime maxValue = new DateTime(2023, 12, 31);
        DateTime valueInRange = new DateTime(2023, 6, 15);
        DateTime valueOutOfRange = new DateTime(2024, 1, 1);

        // Act & Assert
        Assert.IsInRange(minValue, maxValue, valueInRange);
        Exception ex = VerifyThrows(() => Assert.IsInRange(minValue, maxValue, valueOutOfRange));
        Verify(ex.Message.Contains("is not within the expected range"));
    }

    public void IsInRange_WithCharValues_WorksCorrectly()
    {
        // Arrange
        char minValue = 'A';
        char maxValue = 'Z';
        char valueInRange = 'M';
        char valueOutOfRange = 'a';

        // Act & Assert
        Assert.IsInRange(minValue, maxValue, valueInRange);
        Exception ex = VerifyThrows(() => Assert.IsInRange(minValue, maxValue, valueOutOfRange));
        Verify(ex.Message.Contains("Value 'a' is not within the expected range [A, Z]"));
    }

    public void IsInRange_WithNullMessage_DoesNotThrow()
    {
        // Arrange
        int minValue = 1;
        int maxValue = 5;
        int value = 3;

        // Act & Assert
        Assert.IsInRange(minValue, maxValue, value, null);
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

    public void IsInRange_WithNullParameters_DoesNotThrow()
    {
        // Arrange
        int minValue = 1;
        int maxValue = 5;
        int value = 3;

        // Act & Assert
        Assert.IsInRange(minValue, maxValue, value, "Test message", null);
    }

    public void IsInRange_WithMinValueGreaterThanMaxValue_StillValidatesAgainstRange()
    {
        // This tests the behavior when minValue > maxValue
        // The implementation should still work by checking if value is outside both bounds
        // Arrange
        int minValue = 10;
        int maxValue = 5;
        int value = 7; // Between max and min, but outside the "range"

        // Act & Assert
        Exception ex = VerifyThrows(() => Assert.IsInRange(minValue, maxValue, value));
        Verify(ex.Message.Contains("Value '7' is not within the expected range [10, 5]"));
    }

    #endregion // IsInRange Tests
}