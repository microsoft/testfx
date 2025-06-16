// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using TestFramework.ForTestingMSTest;

namespace Microsoft.VisualStudio.TestPlatform.TestFramework.UnitTests;

public partial class AssertTests : TestContainer
{
    #region IsGreaterThan tests

    public void IsGreaterThanShouldNotThrowWhenFirstIsGreater() =>
        Assert.IsGreaterThan(10, 5);

    public void IsGreaterThanShouldThrowWhenFirstIsNotGreater() =>
        VerifyThrows<AssertFailedException>(() => Assert.IsGreaterThan(5, 10));

    public void IsGreaterThanShouldThrowWhenBothAreEqual() =>
        VerifyThrows<AssertFailedException>(() => Assert.IsGreaterThan(5, 5));

    public void IsGreaterThanShouldThrowWithMessage()
    {
        Exception ex = VerifyThrows(() => Assert.IsGreaterThan(5, 10, "A Message"));
        Verify(ex.Message == "Expected value <5> to be greater than actual value <10>. A Message");
    }

    public void IsGreaterThanShouldThrowWithMessageAndParameters()
    {
        Exception ex = VerifyThrows(() => Assert.IsGreaterThan(5, 10, "A Message {0}", "param"));
        Verify(ex.Message == "Expected value <5> to be greater than actual value <10>. A Message param");
    }

    public void IsGreaterThanShouldWorkWithDoubles() =>
        Assert.IsGreaterThan(5.5, 5.0);

    public void IsGreaterThanShouldThrowWithDoubles() =>
        VerifyThrows<AssertFailedException>(() => Assert.IsGreaterThan(5.0, 5.5));

    #endregion

    #region IsGreaterThanOrEqualTo tests

    public void IsGreaterThanOrEqualToShouldNotThrowWhenFirstIsGreater() =>
        Assert.IsGreaterThanOrEqualTo(10, 5);

    public void IsGreaterThanOrEqualToShouldNotThrowWhenBothAreEqual() =>
        Assert.IsGreaterThanOrEqualTo(5, 5);

    public void IsGreaterThanOrEqualToShouldThrowWhenFirstIsLess() =>
        VerifyThrows<AssertFailedException>(() => Assert.IsGreaterThanOrEqualTo(5, 10));

    public void IsGreaterThanOrEqualToShouldThrowWithMessage()
    {
        Exception ex = VerifyThrows(() => Assert.IsGreaterThanOrEqualTo(5, 10, "A Message"));
        Verify(ex.Message == "Expected value <5> to be greater than or equal to actual value <10>. A Message");
    }

    public void IsGreaterThanOrEqualToShouldThrowWithMessageAndParameters()
    {
        Exception ex = VerifyThrows(() => Assert.IsGreaterThanOrEqualTo(5, 10, "A Message {0}", "param"));
        Verify(ex.Message == "Expected value <5> to be greater than or equal to actual value <10>. A Message param");
    }

    public void IsGreaterThanOrEqualToShouldWorkWithDoubles() =>
        Assert.IsGreaterThanOrEqualTo(5.5, 5.0);

    public void IsGreaterThanOrEqualToShouldWorkWithEqualDoubles() =>
        Assert.IsGreaterThanOrEqualTo(5.5, 5.5);

    #endregion

    #region IsLessThan tests

    public void IsLessThanShouldNotThrowWhenFirstIsLess() =>
        Assert.IsLessThan(5, 10);

    public void IsLessThanShouldThrowWhenFirstIsNotLess() =>
        VerifyThrows<AssertFailedException>(() => Assert.IsLessThan(10, 5));

    public void IsLessThanShouldThrowWhenBothAreEqual() =>
        VerifyThrows<AssertFailedException>(() => Assert.IsLessThan(5, 5));

    public void IsLessThanShouldThrowWithMessage()
    {
        Exception ex = VerifyThrows(() => Assert.IsLessThan(10, 5, "A Message"));
        Verify(ex.Message == "Expected value <10> to be less than actual value <5>. A Message");
    }

    public void IsLessThanShouldThrowWithMessageAndParameters()
    {
        Exception ex = VerifyThrows(() => Assert.IsLessThan(10, 5, "A Message {0}", "param"));
        Verify(ex.Message == "Expected value <10> to be less than actual value <5>. A Message param");
    }

    public void IsLessThanShouldWorkWithDoubles() =>
        Assert.IsLessThan(5.0, 5.5);

    public void IsLessThanShouldThrowWithDoubles() =>
        VerifyThrows<AssertFailedException>(() => Assert.IsLessThan(5.5, 5.0));

    #endregion

    #region IsLessThanOrEqualTo tests

    public void IsLessThanOrEqualToShouldNotThrowWhenFirstIsLess() =>
        Assert.IsLessThanOrEqualTo(5, 10);

    public void IsLessThanOrEqualToShouldNotThrowWhenBothAreEqual() =>
        Assert.IsLessThanOrEqualTo(5, 5);

    public void IsLessThanOrEqualToShouldThrowWhenFirstIsGreater() =>
        VerifyThrows<AssertFailedException>(() => Assert.IsLessThanOrEqualTo(10, 5));

    public void IsLessThanOrEqualToShouldThrowWithMessage()
    {
        Exception ex = VerifyThrows(() => Assert.IsLessThanOrEqualTo(10, 5, "A Message"));
        Verify(ex.Message == "Expected value <10> to be less than or equal to actual value <5>. A Message");
    }

    public void IsLessThanOrEqualToShouldThrowWithMessageAndParameters()
    {
        Exception ex = VerifyThrows(() => Assert.IsLessThanOrEqualTo(10, 5, "A Message {0}", "param"));
        Verify(ex.Message == "Expected value <10> to be less than or equal to actual value <5>. A Message param");
    }

    public void IsLessThanOrEqualToShouldWorkWithDoubles() =>
        Assert.IsLessThanOrEqualTo(5.0, 5.5);

    public void IsLessThanOrEqualToShouldWorkWithEqualDoubles() =>
        Assert.IsLessThanOrEqualTo(5.5, 5.5);

    #endregion

    #region IsPositive tests

    public void IsPositiveShouldNotThrowForPositiveNumber() =>
        Assert.IsPositive(5);

    public void IsPositiveShouldThrowForZero() =>
        VerifyThrows<AssertFailedException>(() => Assert.IsPositive(0));

    public void IsPositiveShouldThrowForNegativeNumber() =>
        VerifyThrows<AssertFailedException>(() => Assert.IsPositive(-5));

    public void IsPositiveShouldThrowForNaN() =>
        VerifyThrows<AssertFailedException>(() => Assert.IsPositive(float.NaN));

    public void IsPositiveShouldThrowForDoubleNaN() =>
        VerifyThrows<AssertFailedException>(() => Assert.IsPositive(double.NaN));

    public void IsPositiveShouldThrowWithMessage()
    {
        Exception ex = VerifyThrows(() => Assert.IsPositive(-5, "A Message"));
        Verify(ex.Message == "Expected value <-5> to be positive. A Message");
    }

    public void IsPositiveShouldThrowWithMessageAndParameters()
    {
        Exception ex = VerifyThrows(() => Assert.IsPositive(-5, "A Message {0}", "param"));
        Verify(ex.Message == "Expected value <-5> to be positive. A Message param");
    }

    public void IsPositiveShouldWorkWithDoubles() =>
        Assert.IsPositive(5.5);

    public void IsPositiveShouldThrowForZeroDouble() =>
        VerifyThrows<AssertFailedException>(() => Assert.IsPositive(0.0));

    #endregion

    #region IsNegative tests

    public void IsNegativeShouldNotThrowForNegativeNumber() =>
        Assert.IsNegative(-5);

    public void IsNegativeShouldThrowForZero() =>
        VerifyThrows<AssertFailedException>(() => Assert.IsNegative(0));

    public void IsNegativeShouldThrowForPositiveNumber() =>
        VerifyThrows<AssertFailedException>(() => Assert.IsNegative(5));

    public void IsNegativeShouldThrowForNaN() =>
        VerifyThrows<AssertFailedException>(() => Assert.IsNegative(float.NaN));

    public void IsNegativeShouldThrowForDoubleNaN() =>
        VerifyThrows<AssertFailedException>(() => Assert.IsNegative(double.NaN));

    public void IsNegativeShouldThrowWithMessage()
    {
        Exception ex = VerifyThrows(() => Assert.IsNegative(5, "A Message"));
        Verify(ex.Message == "Expected value <5> to be negative. A Message");
    }

    public void IsNegativeShouldThrowWithMessageAndParameters()
    {
        Exception ex = VerifyThrows(() => Assert.IsNegative(5, "A Message {0}", "param"));
        Verify(ex.Message == "Expected value <5> to be negative. A Message param");
    }

    public void IsNegativeShouldWorkWithDoubles() =>
        Assert.IsNegative(-5.5);

    public void IsNegativeShouldThrowForZeroDouble() =>
        VerifyThrows<AssertFailedException>(() => Assert.IsNegative(0.0));

    #endregion
}