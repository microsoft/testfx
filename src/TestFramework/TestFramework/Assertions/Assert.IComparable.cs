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
    /// Tests whether the actual value is greater than the expected value and throws an exception
    /// if it is not.
    /// </summary>
    /// <typeparam name="T">
    /// The type of values to compare.
    /// </typeparam>
    /// <param name="expected">
    /// The first value to compare. This is the baseline value that the actual value should exceed.
    /// </param>
    /// <param name="actual">
    /// The second value to compare. This is the value produced by the code under test.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// Thrown if <paramref name="actual"/> is not greater than <paramref name="expected"/>.
    /// </exception>
    public static void IsGreaterThan<T>(T expected, T actual)
        where T : IComparable<T>
        => IsGreaterThan(expected, actual, string.Empty, null);

    /// <summary>
    /// Tests whether the actual value is greater than the expected value and throws an exception
    /// if it is not.
    /// </summary>
    /// <typeparam name="T">
    /// The type of values to compare.
    /// </typeparam>
    /// <param name="expected">
    /// The first value to compare. This is the baseline value that the actual value should exceed.
    /// </param>
    /// <param name="actual">
    /// The second value to compare. This is the value produced by the code under test.
    /// </param>
    /// <param name="message">
    /// The message to include in the exception when <paramref name="actual"/>
    /// is not greater than <paramref name="expected"/>. The message is shown in
    /// test results.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// Thrown if <paramref name="actual"/> is not greater than <paramref name="expected"/>.
    /// </exception>
    public static void IsGreaterThan<T>(T expected, T actual, string? message)
        where T : IComparable<T>
        => IsGreaterThan(expected, actual, message, null);

    /// <summary>
    /// Tests whether the actual value is greater than the expected value and throws an exception
    /// if it is not.
    /// </summary>
    /// <typeparam name="T">
    /// The type of values to compare.
    /// </typeparam>
    /// <param name="expected">
    /// The first value to compare. This is the baseline value that the actual value should exceed.
    /// </param>
    /// <param name="actual">
    /// The second value to compare. This is the value produced by the code under test.
    /// </param>
    /// <param name="message">
    /// The message to include in the exception when <paramref name="actual"/>
    /// is not greater than <paramref name="expected"/>. The message is shown in
    /// test results.
    /// </param>
    /// <param name="parameters">
    /// An array of parameters to use when formatting <paramref name="message"/>.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// Thrown if <paramref name="actual"/> is not greater than <paramref name="expected"/>.
    /// </exception>
    public static void IsGreaterThan<T>(T expected, T actual, [StringSyntax(StringSyntaxAttribute.CompositeFormat)] string? message, params object?[]? parameters)
        where T : IComparable<T>
    {
        if (actual.CompareTo(expected) > 0)
        {
            return;
        }

        string userMessage = BuildUserMessage(message, parameters);
        ThrowAssertIsGreaterThanFailed(expected, actual, userMessage);
    }

    #endregion // IsGreaterThan

    #region IsGreaterThanOrEqualTo

    /// <summary>
    /// Tests whether the actual value is greater than or equal to the expected value and throws an exception
    /// if it is not.
    /// </summary>
    /// <typeparam name="T">
    /// The type of values to compare.
    /// </typeparam>
    /// <param name="expected">
    /// The first value to compare. This is the baseline value that the actual value should meet or exceed.
    /// </param>
    /// <param name="actual">
    /// The second value to compare. This is the value produced by the code under test.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// Thrown if <paramref name="actual"/> is not greater than or equal to <paramref name="expected"/>.
    /// </exception>
    public static void IsGreaterThanOrEqualTo<T>(T expected, T actual)
        where T : IComparable<T>
        => IsGreaterThanOrEqualTo(expected, actual, string.Empty, null);

    /// <summary>
    /// Tests whether the actual value is greater than or equal to the expected value and throws an exception
    /// if it is not.
    /// </summary>
    /// <typeparam name="T">
    /// The type of values to compare.
    /// </typeparam>
    /// <param name="expected">
    /// The first value to compare. This is the baseline value that the actual value should meet or exceed.
    /// </param>
    /// <param name="actual">
    /// The second value to compare. This is the value produced by the code under test.
    /// </param>
    /// <param name="message">
    /// The message to include in the exception when <paramref name="actual"/>
    /// is not greater than or equal to <paramref name="expected"/>. The message is shown in
    /// test results.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// Thrown if <paramref name="actual"/> is not greater than or equal to <paramref name="expected"/>.
    /// </exception>
    public static void IsGreaterThanOrEqualTo<T>(T expected, T actual, string? message)
        where T : IComparable<T>
        => IsGreaterThanOrEqualTo(expected, actual, message, null);

    /// <summary>
    /// Tests whether the actual value is greater than or equal to the expected value and throws an exception
    /// if it is not.
    /// </summary>
    /// <typeparam name="T">
    /// The type of values to compare.
    /// </typeparam>
    /// <param name="expected">
    /// The first value to compare. This is the baseline value that the actual value should meet or exceed.
    /// </param>
    /// <param name="actual">
    /// The second value to compare. This is the value produced by the code under test.
    /// </param>
    /// <param name="message">
    /// The message to include in the exception when <paramref name="actual"/>
    /// is not greater than or equal to <paramref name="expected"/>. The message is shown in
    /// test results.
    /// </param>
    /// <param name="parameters">
    /// An array of parameters to use when formatting <paramref name="message"/>.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// Thrown if <paramref name="actual"/> is not greater than or equal to <paramref name="expected"/>.
    /// </exception>
    public static void IsGreaterThanOrEqualTo<T>(T expected, T actual, [StringSyntax(StringSyntaxAttribute.CompositeFormat)] string? message, params object?[]? parameters)
        where T : IComparable<T>
    {
        if (actual.CompareTo(expected) >= 0)
        {
            return;
        }

        string userMessage = BuildUserMessage(message, parameters);
        ThrowAssertIsGreaterThanOrEqualToFailed(expected, actual, userMessage);
    }

    #endregion // IsGreaterThanOrEqualTo

    #region IsLessThan

    /// <summary>
    /// Tests whether the actual value is less than the expected value and throws an exception
    /// if it is not.
    /// </summary>
    /// <typeparam name="T">
    /// The type of values to compare.
    /// </typeparam>
    /// <param name="expected">
    /// The first value to compare. This is the baseline value that the actual value should be less than.
    /// </param>
    /// <param name="actual">
    /// The second value to compare. This is the value produced by the code under test.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// Thrown if <paramref name="actual"/> is not less than <paramref name="expected"/>.
    /// </exception>
    public static void IsLessThan<T>(T expected, T actual)
        where T : IComparable<T>
        => IsLessThan(expected, actual, string.Empty, null);

    /// <summary>
    /// Tests whether the actual value is less than the expected value and throws an exception
    /// if it is not.
    /// </summary>
    /// <typeparam name="T">
    /// The type of values to compare.
    /// </typeparam>
    /// <param name="expected">
    /// The first value to compare. This is the baseline value that the actual value should be less than.
    /// </param>
    /// <param name="actual">
    /// The second value to compare. This is the value produced by the code under test.
    /// </param>
    /// <param name="message">
    /// The message to include in the exception when <paramref name="actual"/>
    /// is not less than <paramref name="expected"/>. The message is shown in
    /// test results.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// Thrown if <paramref name="actual"/> is not less than <paramref name="expected"/>.
    /// </exception>
    public static void IsLessThan<T>(T expected, T actual, string? message)
        where T : IComparable<T>
        => IsLessThan(expected, actual, message, null);

    /// <summary>
    /// Tests whether the actual value is less than the expected value and throws an exception
    /// if it is not.
    /// </summary>
    /// <typeparam name="T">
    /// The type of values to compare.
    /// </typeparam>
    /// <param name="expected">
    /// The first value to compare. This is the baseline value that the actual value should be less than.
    /// </param>
    /// <param name="actual">
    /// The second value to compare. This is the value produced by the code under test.
    /// </param>
    /// <param name="message">
    /// The message to include in the exception when <paramref name="actual"/>
    /// is not less than <paramref name="expected"/>. The message is shown in
    /// test results.
    /// </param>
    /// <param name="parameters">
    /// An array of parameters to use when formatting <paramref name="message"/>.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// Thrown if <paramref name="actual"/> is not less than <paramref name="expected"/>.
    /// </exception>
    public static void IsLessThan<T>(T expected, T actual, [StringSyntax(StringSyntaxAttribute.CompositeFormat)] string? message, params object?[]? parameters)
        where T : IComparable<T>
    {
        if (actual.CompareTo(expected) < 0)
        {
            return;
        }

        string userMessage = BuildUserMessage(message, parameters);
        ThrowAssertIsLessThanFailed(expected, actual, userMessage);
    }

    #endregion // IsLessThan

    #region IsLessThanOrEqualTo

    /// <summary>
    /// Tests whether the actual value is less than or equal to the expected value and throws an exception
    /// if it is not.
    /// </summary>
    /// <typeparam name="T">
    /// The type of values to compare.
    /// </typeparam>
    /// <param name="expected">
    /// The first value to compare. This is the baseline value that the actual value should not exceed.
    /// </param>
    /// <param name="actual">
    /// The second value to compare. This is the value produced by the code under test.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// Thrown if <paramref name="actual"/> is not less than or equal to <paramref name="expected"/>.
    /// </exception>
    public static void IsLessThanOrEqualTo<T>(T expected, T actual)
        where T : IComparable<T>
        => IsLessThanOrEqualTo(expected, actual, string.Empty, null);

    /// <summary>
    /// Tests whether the actual value is less than or equal to the expected value and throws an exception
    /// if it is not.
    /// </summary>
    /// <typeparam name="T">
    /// The type of values to compare.
    /// </typeparam>
    /// <param name="expected">
    /// The first value to compare. This is the baseline value that the actual value should not exceed.
    /// </param>
    /// <param name="actual">
    /// The second value to compare. This is the value produced by the code under test.
    /// </param>
    /// <param name="message">
    /// The message to include in the exception when <paramref name="actual"/>
    /// is not less than or equal to <paramref name="expected"/>. The message is shown in
    /// test results.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// Thrown if <paramref name="actual"/> is not less than or equal to <paramref name="expected"/>.
    /// </exception>
    public static void IsLessThanOrEqualTo<T>(T expected, T actual, string? message)
        where T : IComparable<T>
        => IsLessThanOrEqualTo(expected, actual, message, null);

    /// <summary>
    /// Tests whether the actual value is less than or equal to the expected value and throws an exception
    /// if it is not.
    /// </summary>
    /// <typeparam name="T">
    /// The type of values to compare.
    /// </typeparam>
    /// <param name="expected">
    /// The first value to compare. This is the baseline value that the actual value should not exceed.
    /// </param>
    /// <param name="actual">
    /// The second value to compare. This is the value produced by the code under test.
    /// </param>
    /// <param name="message">
    /// The message to include in the exception when <paramref name="actual"/>
    /// is not less than or equal to <paramref name="expected"/>. The message is shown in
    /// test results.
    /// </param>
    /// <param name="parameters">
    /// An array of parameters to use when formatting <paramref name="message"/>.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// Thrown if <paramref name="actual"/> is not less than or equal to <paramref name="expected"/>.
    /// </exception>
    public static void IsLessThanOrEqualTo<T>(T expected, T actual, [StringSyntax(StringSyntaxAttribute.CompositeFormat)] string? message, params object?[]? parameters)
        where T : IComparable<T>
    {
        if (actual.CompareTo(expected) <= 0)
        {
            return;
        }

        string userMessage = BuildUserMessage(message, parameters);
        ThrowAssertIsLessThanOrEqualToFailed(expected, actual, userMessage);
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
    /// <exception cref="AssertFailedException">
    /// Thrown if <paramref name="value"/> is not positive.
    /// </exception>
    public static void IsPositive<T>(T value)
        where T : struct, IComparable<T>
        => IsPositive(value, string.Empty, null);

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
    public static void IsPositive<T>(T value, string? message)
        where T : struct, IComparable<T>
        => IsPositive(value, message, null);

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
    /// <param name="parameters">
    /// An array of parameters to use when formatting <paramref name="message"/>.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// Thrown if <paramref name="value"/> is not positive.
    /// </exception>
    public static void IsPositive<T>(T value, [StringSyntax(StringSyntaxAttribute.CompositeFormat)] string? message, params object?[]? parameters)
        where T : struct, IComparable<T>
    {
        var zero = default(T);

        // Handle special case for floating point NaN values
        if (value is float floatValue && float.IsNaN(floatValue))
        {
            string userMessage = BuildUserMessage(message, parameters);
            ThrowAssertIsPositiveFailed(value, userMessage);
            return;
        }

        if (value is double doubleValue && double.IsNaN(doubleValue))
        {
            string userMessage = BuildUserMessage(message, parameters);
            ThrowAssertIsPositiveFailed(value, userMessage);
            return;
        }

        if (value.CompareTo(zero) > 0)
        {
            return;
        }

        string userMessage2 = BuildUserMessage(message, parameters);
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
    /// <exception cref="AssertFailedException">
    /// Thrown if <paramref name="value"/> is not negative.
    /// </exception>
    public static void IsNegative<T>(T value)
        where T : struct, IComparable<T>
        => IsNegative(value, string.Empty, null);

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
    public static void IsNegative<T>(T value, string? message)
        where T : struct, IComparable<T>
        => IsNegative(value, message, null);

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
    /// <param name="parameters">
    /// An array of parameters to use when formatting <paramref name="message"/>.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// Thrown if <paramref name="value"/> is not negative.
    /// </exception>
    public static void IsNegative<T>(T value, [StringSyntax(StringSyntaxAttribute.CompositeFormat)] string? message, params object?[]? parameters)
        where T : struct, IComparable<T>
    {
        var zero = default(T);

        // Handle special case for floating point NaN values
        if (value is float floatValue && float.IsNaN(floatValue))
        {
            string userMessage = BuildUserMessage(message, parameters);
            ThrowAssertIsNegativeFailed(value, userMessage);
            return;
        }

        if (value is double doubleValue && double.IsNaN(doubleValue))
        {
            string userMessage = BuildUserMessage(message, parameters);
            ThrowAssertIsNegativeFailed(value, userMessage);
            return;
        }

        if (value.CompareTo(zero) < 0)
        {
            return;
        }

        string userMessage2 = BuildUserMessage(message, parameters);
        ThrowAssertIsNegativeFailed(value, userMessage2);
    }

    #endregion // IsNegative

    [DoesNotReturn]
    private static void ThrowAssertIsGreaterThanFailed<T>(T expected, T actual, string userMessage)
    {
        string finalMessage = string.Format(
            CultureInfo.CurrentCulture,
            FrameworkMessages.IsGreaterThanFailMsg,
            userMessage,
            ReplaceNulls(expected),
            ReplaceNulls(actual));
        ThrowAssertFailed("Assert.IsGreaterThan", finalMessage);
    }

    [DoesNotReturn]
    private static void ThrowAssertIsGreaterThanOrEqualToFailed<T>(T expected, T actual, string userMessage)
    {
        string finalMessage = string.Format(
            CultureInfo.CurrentCulture,
            FrameworkMessages.IsGreaterThanOrEqualToFailMsg,
            userMessage,
            ReplaceNulls(expected),
            ReplaceNulls(actual));
        ThrowAssertFailed("Assert.IsGreaterThanOrEqualTo", finalMessage);
    }

    [DoesNotReturn]
    private static void ThrowAssertIsLessThanFailed<T>(T expected, T actual, string userMessage)
    {
        string finalMessage = string.Format(
            CultureInfo.CurrentCulture,
            FrameworkMessages.IsLessThanFailMsg,
            userMessage,
            ReplaceNulls(expected),
            ReplaceNulls(actual));
        ThrowAssertFailed("Assert.IsLessThan", finalMessage);
    }

    [DoesNotReturn]
    private static void ThrowAssertIsLessThanOrEqualToFailed<T>(T expected, T actual, string userMessage)
    {
        string finalMessage = string.Format(
            CultureInfo.CurrentCulture,
            FrameworkMessages.IsLessThanOrEqualToFailMsg,
            userMessage,
            ReplaceNulls(expected),
            ReplaceNulls(actual));
        ThrowAssertFailed("Assert.IsLessThanOrEqualTo", finalMessage);
    }

    [DoesNotReturn]
    private static void ThrowAssertIsPositiveFailed<T>(T actual, string userMessage)
    {
        string finalMessage = string.Format(
            CultureInfo.CurrentCulture,
            FrameworkMessages.IsPositiveFailMsg,
            userMessage,
            ReplaceNulls(actual));
        ThrowAssertFailed("Assert.IsPositive", finalMessage);
    }

    [DoesNotReturn]
    private static void ThrowAssertIsNegativeFailed<T>(T actual, string userMessage)
    {
        string finalMessage = string.Format(
            CultureInfo.CurrentCulture,
            FrameworkMessages.IsNegativeFailMsg,
            userMessage,
            ReplaceNulls(actual));
        ThrowAssertFailed("Assert.IsNegative", finalMessage);
    }
}
