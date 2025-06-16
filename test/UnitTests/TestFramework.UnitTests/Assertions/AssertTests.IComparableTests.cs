// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using FluentAssertions;

using TestFramework.ForTestingMSTest;

namespace Microsoft.VisualStudio.TestPlatform.TestFramework.UnitTests;

public partial class AssertTests : TestContainer
{
    #region IsGreaterThan tests

    public void IsGreaterThanShouldNotThrowWhenFirstIsGreater() =>
        Assert.IsGreaterThan(10, 5);

    public void IsGreaterThanShouldThrowWhenFirstIsNotGreater()
    {
        // Act
        Action action = () => Assert.IsGreaterThan(5, 10);

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
        Action action = () => Assert.IsGreaterThan(5, 10, "A Message");

        // Assert
        action.Should().Throw<AssertFailedException>()
            .WithMessage("Expected value <5> to be greater than actual value <10>. A Message");
    }

    public void IsGreaterThanShouldThrowWithMessageAndParameters()
    {
        // Act
        Action action = () => Assert.IsGreaterThan(5, 10, "A Message {0}", "param");

        // Assert
        action.Should().Throw<AssertFailedException>()
            .WithMessage("Expected value <5> to be greater than actual value <10>. A Message param");
    }

    public void IsGreaterThanShouldWorkWithDoubles() =>
        Assert.IsGreaterThan(5.5, 5.0);

    public void IsGreaterThanShouldThrowWithDoubles()
    {
        // Act
        Action action = () => Assert.IsGreaterThan(5.0, 5.5);

        // Assert
        action.Should().Throw<AssertFailedException>();
    }

    #endregion

    #region IsGreaterThanOrEqualTo tests

    public void IsGreaterThanOrEqualToShouldNotThrowWhenFirstIsGreater() =>
        Assert.IsGreaterThanOrEqualTo(10, 5);

    public void IsGreaterThanOrEqualToShouldNotThrowWhenBothAreEqual() =>
        Assert.IsGreaterThanOrEqualTo(5, 5);

    public void IsGreaterThanOrEqualToShouldThrowWhenFirstIsLess()
    {
        // Act
        Action action = () => Assert.IsGreaterThanOrEqualTo(5, 10);

        // Assert
        action.Should().Throw<AssertFailedException>();
    }

    public void IsGreaterThanOrEqualToShouldThrowWithMessage()
    {
        // Act
        Action action = () => Assert.IsGreaterThanOrEqualTo(5, 10, "A Message");

        // Assert
        action.Should().Throw<AssertFailedException>()
            .WithMessage("Expected value <5> to be greater than or equal to actual value <10>. A Message");
    }

    public void IsGreaterThanOrEqualToShouldThrowWithMessageAndParameters()
    {
        // Act
        Action action = () => Assert.IsGreaterThanOrEqualTo(5, 10, "A Message {0}", "param");

        // Assert
        action.Should().Throw<AssertFailedException>()
            .WithMessage("Expected value <5> to be greater than or equal to actual value <10>. A Message param");
    }

    public void IsGreaterThanOrEqualToShouldWorkWithDoubles() =>
        Assert.IsGreaterThanOrEqualTo(5.5, 5.0);

    public void IsGreaterThanOrEqualToShouldWorkWithEqualDoubles() =>
        Assert.IsGreaterThanOrEqualTo(5.5, 5.5);

    #endregion

    #region IsLessThan tests

    public void IsLessThanShouldNotThrowWhenFirstIsLess() =>
        Assert.IsLessThan(5, 10);

    public void IsLessThanShouldThrowWhenFirstIsNotLess()
    {
        // Act
        Action action = () => Assert.IsLessThan(10, 5);

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
        Action action = () => Assert.IsLessThan(10, 5, "A Message");

        // Assert
        action.Should().Throw<AssertFailedException>()
            .WithMessage("Expected value <10> to be less than actual value <5>. A Message");
    }

    public void IsLessThanShouldThrowWithMessageAndParameters()
    {
        // Act
        Action action = () => Assert.IsLessThan(10, 5, "A Message {0}", "param");

        // Assert
        action.Should().Throw<AssertFailedException>()
            .WithMessage("Expected value <10> to be less than actual value <5>. A Message param");
    }

    public void IsLessThanShouldWorkWithDoubles() =>
        Assert.IsLessThan(5.0, 5.5);

    public void IsLessThanShouldThrowWithDoubles()
    {
        // Act
        Action action = () => Assert.IsLessThan(5.5, 5.0);

        // Assert
        action.Should().Throw<AssertFailedException>();
    }

    #endregion

    #region IsLessThanOrEqualTo tests

    public void IsLessThanOrEqualToShouldNotThrowWhenFirstIsLess() =>
        Assert.IsLessThanOrEqualTo(5, 10);

    public void IsLessThanOrEqualToShouldNotThrowWhenBothAreEqual() =>
        Assert.IsLessThanOrEqualTo(5, 5);

    public void IsLessThanOrEqualToShouldThrowWhenFirstIsGreater()
    {
        // Act
        Action action = () => Assert.IsLessThanOrEqualTo(10, 5);

        // Assert
        action.Should().Throw<AssertFailedException>();
    }

    public void IsLessThanOrEqualToShouldThrowWithMessage()
    {
        // Act
        Action action = () => Assert.IsLessThanOrEqualTo(10, 5, "A Message");

        // Assert
        action.Should().Throw<AssertFailedException>()
            .WithMessage("Expected value <10> to be less than or equal to actual value <5>. A Message");
    }

    public void IsLessThanOrEqualToShouldThrowWithMessageAndParameters()
    {
        // Act
        Action action = () => Assert.IsLessThanOrEqualTo(10, 5, "A Message {0}", "param");

        // Assert
        action.Should().Throw<AssertFailedException>()
            .WithMessage("Expected value <10> to be less than or equal to actual value <5>. A Message param");
    }

    public void IsLessThanOrEqualToShouldWorkWithDoubles() =>
        Assert.IsLessThanOrEqualTo(5.0, 5.5);

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
            .WithMessage("Expected value <-5> to be positive. A Message");
    }

    public void IsPositiveShouldThrowWithMessageAndParameters()
    {
        // Act
        Action action = () => Assert.IsPositive(-5, "A Message {0}", "param");

        // Assert
        action.Should().Throw<AssertFailedException>()
            .WithMessage("Expected value <-5> to be positive. A Message param");
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
            .WithMessage("Expected value <5> to be negative. A Message");
    }

    public void IsNegativeShouldThrowWithMessageAndParameters()
    {
        // Act
        Action action = () => Assert.IsNegative(5, "A Message {0}", "param");

        // Assert
        action.Should().Throw<AssertFailedException>()
            .WithMessage("Expected value <5> to be negative. A Message param");
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
}