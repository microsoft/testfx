// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// A collection of helper classes to test various conditions within
/// unit tests. If the condition being tested is not met, an exception
/// is thrown.
/// </summary>
public sealed partial class Assert
{
    #region IsGreaterThan

    /// <summary>
    /// Tests whether the value is greater than the lower bound and throws an exception
    /// if it is not.
    /// </summary>
    /// <typeparam name="T">
    /// The type of values to compare.
    /// </typeparam>
    /// <param name="lowerBound">
    /// The lower bound value that the value should exceed.
    /// </param>
    /// <param name="value">
    /// The value to compare. This is the value produced by the code under test.
    /// </param>
    /// <param name="message">
    /// The message to include in the exception when <paramref name="value"/>
    /// is not greater than <paramref name="lowerBound"/>. The message is shown in
    /// test results.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// Thrown if <paramref name="value"/> is not greater than <paramref name="lowerBound"/>.
    /// </exception>
    public static void IsGreaterThan<T>(T lowerBound, T value, string message = "")
        where T : IComparable<T>
    {
        if (value.CompareTo(lowerBound) > 0)
        {
            return;
        }

        string userMessage = BuildUserMessage(message);
        ThrowAssertIsGreaterThanFailed(lowerBound, value, userMessage);
    }

    #endregion // IsGreaterThan

    #region IsGreaterThanOrEqualTo

    /// <summary>
    /// Tests whether the value is greater than or equal to the lower bound and throws an exception
    /// if it is not.
    /// </summary>
    /// <typeparam name="T">
    /// The type of values to compare.
    /// </typeparam>
    /// <param name="lowerBound">
    /// The lower bound value that the value should meet or exceed.
    /// </param>
    /// <param name="value">
    /// The value to compare. This is the value produced by the code under test.
    /// </param>
    /// <param name="message">
    /// The message to include in the exception when <paramref name="value"/>
    /// is not greater than or equal to <paramref name="lowerBound"/>. The message is shown in
    /// test results.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// Thrown if <paramref name="value"/> is not greater than or equal to <paramref name="lowerBound"/>.
    /// </exception>
    public static void IsGreaterThanOrEqualTo<T>(T lowerBound, T value, string message = "")
        where T : IComparable<T>
    {
        if (value.CompareTo(lowerBound) >= 0)
        {
            return;
        }

        string userMessage = BuildUserMessage(message);
        ThrowAssertIsGreaterThanOrEqualToFailed(lowerBound, value, userMessage);
    }

    #endregion // IsGreaterThanOrEqualTo

    #region IsLessThan

    /// <summary>
    /// Tests whether the value is less than the upper bound and throws an exception
    /// if it is not.
    /// </summary>
    /// <typeparam name="T">
    /// The type of values to compare.
    /// </typeparam>
    /// <param name="upperBound">
    /// The upper bound value that the value should be less than.
    /// </param>
    /// <param name="value">
    /// The value to compare. This is the value produced by the code under test.
    /// </param>
    /// <param name="message">
    /// The message to include in the exception when <paramref name="value"/>
    /// is not less than <paramref name="upperBound"/>. The message is shown in
    /// test results.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// Thrown if <paramref name="value"/> is not less than <paramref name="upperBound"/>.
    /// </exception>
    public static void IsLessThan<T>(T upperBound, T value, string message = "")
        where T : IComparable<T>
    {
        if (value.CompareTo(upperBound) < 0)
        {
            return;
        }

        string userMessage = BuildUserMessage(message);
        ThrowAssertIsLessThanFailed(upperBound, value, userMessage);
    }

    #endregion // IsLessThan

    #region IsLessThanOrEqualTo

    /// <summary>
    /// Tests whether the value is less than or equal to the upper bound and throws an exception
    /// if it is not.
    /// </summary>
    /// <typeparam name="T">
    /// The type of values to compare.
    /// </typeparam>
    /// <param name="upperBound">
    /// The upper bound value that the value should not exceed.
    /// </param>
    /// <param name="value">
    /// The value to compare. This is the value produced by the code under test.
    /// </param>
    /// <param name="message">
    /// The message to include in the exception when <paramref name="value"/>
    /// is not less than or equal to <paramref name="upperBound"/>. The message is shown in
    /// test results.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// Thrown if <paramref name="value"/> is not less than or equal to <paramref name="upperBound"/>.
    /// </exception>
    public static void IsLessThanOrEqualTo<T>(T upperBound, T value, string message = "")
        where T : IComparable<T>
    {
        if (value.CompareTo(upperBound) <= 0)
        {
            return;
        }

        string userMessage = BuildUserMessage(message);
        ThrowAssertIsLessThanOrEqualToFailed(upperBound, value, userMessage);
    }

    #endregion // IsLessThanOrEqualTo

    #region IsPositive

    /// <summary>
    /// Tests whether the specified value is positive and throws an exception
    /// if it is not.
    /// </summary>
    /// <typeparam name="T">
    /// The type of value to test.
    /// </typeparam>
    /// <param name="value">
    /// The value to test.
    /// </param>
    /// <param name="message">
    /// The message to include in the exception when <paramref name="value"/>
    /// is not positive. The message is shown in test results.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// Thrown if <paramref name="value"/> is not positive.
    /// </exception>
    public static void IsPositive<T>(T value, string message = "")
        where T : struct, IComparable<T>
    {
        var zero = default(T);

        // Handle special case for floating point NaN values
        if (value is float floatValue && float.IsNaN(floatValue))
        {
            string userMessage = BuildUserMessage(message);
            ThrowAssertIsPositiveFailed(value, userMessage);
            return;
        }

        if (value is double doubleValue && double.IsNaN(doubleValue))
        {
            string userMessage = BuildUserMessage(message);
            ThrowAssertIsPositiveFailed(value, userMessage);
            return;
        }

        if (value.CompareTo(zero) > 0)
        {
            return;
        }

        string userMessage2 = BuildUserMessage(message);
        ThrowAssertIsPositiveFailed(value, userMessage2);
    }

    #endregion // IsPositive

    #region IsNegative

    /// <summary>
    /// Tests whether the specified value is negative and throws an exception
    /// if it is not.
    /// </summary>
    /// <typeparam name="T">
    /// The type of value to test.
    /// </typeparam>
    /// <param name="value">
    /// The value to test.
    /// </param>
    /// <param name="message">
    /// The message to include in the exception when <paramref name="value"/>
    /// is not negative. The message is shown in test results.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// Thrown if <paramref name="value"/> is not negative.
    /// </exception>
    public static void IsNegative<T>(T value, string message = "")
        where T : struct, IComparable<T>
    {
        var zero = default(T);

        // Handle special case for floating point NaN values
        if (value is float floatValue && float.IsNaN(floatValue))
        {
            string userMessage = BuildUserMessage(message);
            ThrowAssertIsNegativeFailed(value, userMessage);
            return;
        }

        if (value is double doubleValue && double.IsNaN(doubleValue))
        {
            string userMessage = BuildUserMessage(message);
            ThrowAssertIsNegativeFailed(value, userMessage);
            return;
        }

        if (value.CompareTo(zero) < 0)
        {
            return;
        }

        string userMessage2 = BuildUserMessage(message);
        ThrowAssertIsNegativeFailed(value, userMessage2);
    }

    #endregion // IsNegative

    [DoesNotReturn]
    private static void ThrowAssertIsGreaterThanFailed<T>(T lowerBound, T value, string userMessage)
    {
        string finalMessage = string.Format(
            CultureInfo.CurrentCulture,
            FrameworkMessages.IsGreaterThanFailMsg,
            userMessage,
            ReplaceNulls(lowerBound),
            ReplaceNulls(value));
        ThrowAssertFailed("Assert.IsGreaterThan", finalMessage);
    }

    [DoesNotReturn]
    private static void ThrowAssertIsGreaterThanOrEqualToFailed<T>(T lowerBound, T value, string userMessage)
    {
        string finalMessage = string.Format(
            CultureInfo.CurrentCulture,
            FrameworkMessages.IsGreaterThanOrEqualToFailMsg,
            userMessage,
            ReplaceNulls(lowerBound),
            ReplaceNulls(value));
        ThrowAssertFailed("Assert.IsGreaterThanOrEqualTo", finalMessage);
    }

    [DoesNotReturn]
    private static void ThrowAssertIsLessThanFailed<T>(T upperBound, T value, string userMessage)
    {
        string finalMessage = string.Format(
            CultureInfo.CurrentCulture,
            FrameworkMessages.IsLessThanFailMsg,
            userMessage,
            ReplaceNulls(upperBound),
            ReplaceNulls(value));
        ThrowAssertFailed("Assert.IsLessThan", finalMessage);
    }

    [DoesNotReturn]
    private static void ThrowAssertIsLessThanOrEqualToFailed<T>(T upperBound, T value, string userMessage)
    {
        string finalMessage = string.Format(
            CultureInfo.CurrentCulture,
            FrameworkMessages.IsLessThanOrEqualToFailMsg,
            userMessage,
            ReplaceNulls(upperBound),
            ReplaceNulls(value));
        ThrowAssertFailed("Assert.IsLessThanOrEqualTo", finalMessage);
    }

    [DoesNotReturn]
    private static void ThrowAssertIsPositiveFailed<T>(T value, string userMessage)
    {
        string finalMessage = string.Format(
            CultureInfo.CurrentCulture,
            FrameworkMessages.IsPositiveFailMsg,
            userMessage,
            ReplaceNulls(value));
        ThrowAssertFailed("Assert.IsPositive", finalMessage);
    }

    [DoesNotReturn]
    private static void ThrowAssertIsNegativeFailed<T>(T value, string userMessage)
    {
        string finalMessage = string.Format(
            CultureInfo.CurrentCulture,
            FrameworkMessages.IsNegativeFailMsg,
            userMessage,
            ReplaceNulls(value));
        ThrowAssertFailed("Assert.IsNegative", finalMessage);
    }
}
