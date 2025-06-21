// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using FluentAssertions;

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
            .WithMessage("Assert.IsGreaterThan failed. Actual value <5> is not greater than expected value <10>. A Message");
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
            .WithMessage("Assert.IsGreaterThanOrEqualTo failed. Actual value <5> is not greater than or equal to expected value <10>. A Message");
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
            .WithMessage("Assert.IsLessThan failed. Actual value <10> is not less than expected value <5>. A Message");
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
            .WithMessage("Assert.IsLessThanOrEqualTo failed. Actual value <10> is not less than or equal to expected value <5>. A Message");
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
            .WithMessage("Assert.IsPositive failed. Expected value <-5> to be positive. A Message");
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
            .WithMessage("Assert.IsNegative failed. Expected value <5> to be negative. A Message");
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
