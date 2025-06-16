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
    /// <summary>
    /// Tests whether the first value is greater than the second value and throws an exception
    /// if it is not.
    /// </summary>
    /// <typeparam name="T">
    /// The type of values to compare.
    /// </typeparam>
    /// <param name="expected">
    /// The first value to compare. This is the value the test expects to be greater.
    /// </param>
    /// <param name="actual">
    /// The second value to compare. This is the value produced by the code under test.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// Thrown if <paramref name="expected"/> is not greater than <paramref name="actual"/>.
    /// </exception>
    public static void IsGreaterThan<T>(T expected, T actual)
        where T : struct, IComparable<T>
        => IsGreaterThan(expected, actual, string.Empty, null);

    /// <summary>
    /// Tests whether the first value is greater than the second value and throws an exception
    /// if it is not.
    /// </summary>
    /// <typeparam name="T">
    /// The type of values to compare.
    /// </typeparam>
    /// <param name="expected">
    /// The first value to compare. This is the value the test expects to be greater.
    /// </param>
    /// <param name="actual">
    /// The second value to compare. This is the value produced by the code under test.
    /// </param>
    /// <param name="message">
    /// The message to include in the exception when <paramref name="expected"/>
    /// is not greater than <paramref name="actual"/>. The message is shown in
    /// test results.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// Thrown if <paramref name="expected"/> is not greater than <paramref name="actual"/>.
    /// </exception>
    public static void IsGreaterThan<T>(T expected, T actual, string? message)
        where T : struct, IComparable<T>
        => IsGreaterThan(expected, actual, message, null);

    /// <summary>
    /// Tests whether the first value is greater than the second value and throws an exception
    /// if it is not.
    /// </summary>
    /// <typeparam name="T">
    /// The type of values to compare.
    /// </typeparam>
    /// <param name="expected">
    /// The first value to compare. This is the value the test expects to be greater.
    /// </param>
    /// <param name="actual">
    /// The second value to compare. This is the value produced by the code under test.
    /// </param>
    /// <param name="message">
    /// The message to include in the exception when <paramref name="expected"/>
    /// is not greater than <paramref name="actual"/>. The message is shown in
    /// test results.
    /// </param>
    /// <param name="parameters">
    /// An array of parameters to use when formatting <paramref name="message"/>.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// Thrown if <paramref name="expected"/> is not greater than <paramref name="actual"/>.
    /// </exception>
    public static void IsGreaterThan<T>(T expected, T actual, [StringSyntax(StringSyntaxAttribute.CompositeFormat)] string? message, params object?[]? parameters)
        where T : struct, IComparable<T>
    {
        if (expected.CompareTo(actual) > 0)
        {
            return;
        }

        string userMessage = BuildUserMessage(message, parameters);
        ThrowAssertIsGreaterThanFailed(expected, actual, userMessage);
    }

    /// <summary>
    /// Tests whether the first value is greater than or equal to the second value and throws an exception
    /// if it is not.
    /// </summary>
    /// <typeparam name="T">
    /// The type of values to compare.
    /// </typeparam>
    /// <param name="expected">
    /// The first value to compare. This is the value the test expects to be greater than or equal.
    /// </param>
    /// <param name="actual">
    /// The second value to compare. This is the value produced by the code under test.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// Thrown if <paramref name="expected"/> is not greater than or equal to <paramref name="actual"/>.
    /// </exception>
    public static void IsGreaterThanOrEqualTo<T>(T expected, T actual)
        where T : struct, IComparable<T>
        => IsGreaterThanOrEqualTo(expected, actual, string.Empty, null);

    /// <summary>
    /// Tests whether the first value is greater than or equal to the second value and throws an exception
    /// if it is not.
    /// </summary>
    /// <typeparam name="T">
    /// The type of values to compare.
    /// </typeparam>
    /// <param name="expected">
    /// The first value to compare. This is the value the test expects to be greater than or equal.
    /// </param>
    /// <param name="actual">
    /// The second value to compare. This is the value produced by the code under test.
    /// </param>
    /// <param name="message">
    /// The message to include in the exception when <paramref name="expected"/>
    /// is not greater than or equal to <paramref name="actual"/>. The message is shown in
    /// test results.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// Thrown if <paramref name="expected"/> is not greater than or equal to <paramref name="actual"/>.
    /// </exception>
    public static void IsGreaterThanOrEqualTo<T>(T expected, T actual, string? message)
        where T : struct, IComparable<T>
        => IsGreaterThanOrEqualTo(expected, actual, message, null);

    /// <summary>
    /// Tests whether the first value is greater than or equal to the second value and throws an exception
    /// if it is not.
    /// </summary>
    /// <typeparam name="T">
    /// The type of values to compare.
    /// </typeparam>
    /// <param name="expected">
    /// The first value to compare. This is the value the test expects to be greater than or equal.
    /// </param>
    /// <param name="actual">
    /// The second value to compare. This is the value produced by the code under test.
    /// </param>
    /// <param name="message">
    /// The message to include in the exception when <paramref name="expected"/>
    /// is not greater than or equal to <paramref name="actual"/>. The message is shown in
    /// test results.
    /// </param>
    /// <param name="parameters">
    /// An array of parameters to use when formatting <paramref name="message"/>.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// Thrown if <paramref name="expected"/> is not greater than or equal to <paramref name="actual"/>.
    /// </exception>
    public static void IsGreaterThanOrEqualTo<T>(T expected, T actual, [StringSyntax(StringSyntaxAttribute.CompositeFormat)] string? message, params object?[]? parameters)
        where T : struct, IComparable<T>
    {
        if (expected.CompareTo(actual) >= 0)
        {
            return;
        }

        string userMessage = BuildUserMessage(message, parameters);
        ThrowAssertIsGreaterThanOrEqualToFailed(expected, actual, userMessage);
    }

    /// <summary>
    /// Tests whether the first value is less than the second value and throws an exception
    /// if it is not.
    /// </summary>
    /// <typeparam name="T">
    /// The type of values to compare.
    /// </typeparam>
    /// <param name="expected">
    /// The first value to compare. This is the value the test expects to be less.
    /// </param>
    /// <param name="actual">
    /// The second value to compare. This is the value produced by the code under test.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// Thrown if <paramref name="expected"/> is not less than <paramref name="actual"/>.
    /// </exception>
    public static void IsLessThan<T>(T expected, T actual)
        where T : struct, IComparable<T>
        => IsLessThan(expected, actual, string.Empty, null);

    /// <summary>
    /// Tests whether the first value is less than the second value and throws an exception
    /// if it is not.
    /// </summary>
    /// <typeparam name="T">
    /// The type of values to compare.
    /// </typeparam>
    /// <param name="expected">
    /// The first value to compare. This is the value the test expects to be less.
    /// </param>
    /// <param name="actual">
    /// The second value to compare. This is the value produced by the code under test.
    /// </param>
    /// <param name="message">
    /// The message to include in the exception when <paramref name="expected"/>
    /// is not less than <paramref name="actual"/>. The message is shown in
    /// test results.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// Thrown if <paramref name="expected"/> is not less than <paramref name="actual"/>.
    /// </exception>
    public static void IsLessThan<T>(T expected, T actual, string? message)
        where T : struct, IComparable<T>
        => IsLessThan(expected, actual, message, null);

    /// <summary>
    /// Tests whether the first value is less than the second value and throws an exception
    /// if it is not.
    /// </summary>
    /// <typeparam name="T">
    /// The type of values to compare.
    /// </typeparam>
    /// <param name="expected">
    /// The first value to compare. This is the value the test expects to be less.
    /// </param>
    /// <param name="actual">
    /// The second value to compare. This is the value produced by the code under test.
    /// </param>
    /// <param name="message">
    /// The message to include in the exception when <paramref name="expected"/>
    /// is not less than <paramref name="actual"/>. The message is shown in
    /// test results.
    /// </param>
    /// <param name="parameters">
    /// An array of parameters to use when formatting <paramref name="message"/>.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// Thrown if <paramref name="expected"/> is not less than <paramref name="actual"/>.
    /// </exception>
    public static void IsLessThan<T>(T expected, T actual, [StringSyntax(StringSyntaxAttribute.CompositeFormat)] string? message, params object?[]? parameters)
        where T : struct, IComparable<T>
    {
        if (expected.CompareTo(actual) < 0)
        {
            return;
        }

        string userMessage = BuildUserMessage(message, parameters);
        ThrowAssertIsLessThanFailed(expected, actual, userMessage);
    }

    /// <summary>
    /// Tests whether the first value is less than or equal to the second value and throws an exception
    /// if it is not.
    /// </summary>
    /// <typeparam name="T">
    /// The type of values to compare.
    /// </typeparam>
    /// <param name="expected">
    /// The first value to compare. This is the value the test expects to be less than or equal.
    /// </param>
    /// <param name="actual">
    /// The second value to compare. This is the value produced by the code under test.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// Thrown if <paramref name="expected"/> is not less than or equal to <paramref name="actual"/>.
    /// </exception>
    public static void IsLessThanOrEqualTo<T>(T expected, T actual)
        where T : struct, IComparable<T>
        => IsLessThanOrEqualTo(expected, actual, string.Empty, null);

    /// <summary>
    /// Tests whether the first value is less than or equal to the second value and throws an exception
    /// if it is not.
    /// </summary>
    /// <typeparam name="T">
    /// The type of values to compare.
    /// </typeparam>
    /// <param name="expected">
    /// The first value to compare. This is the value the test expects to be less than or equal.
    /// </param>
    /// <param name="actual">
    /// The second value to compare. This is the value produced by the code under test.
    /// </param>
    /// <param name="message">
    /// The message to include in the exception when <paramref name="expected"/>
    /// is not less than or equal to <paramref name="actual"/>. The message is shown in
    /// test results.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// Thrown if <paramref name="expected"/> is not less than or equal to <paramref name="actual"/>.
    /// </exception>
    public static void IsLessThanOrEqualTo<T>(T expected, T actual, string? message)
        where T : struct, IComparable<T>
        => IsLessThanOrEqualTo(expected, actual, message, null);

    /// <summary>
    /// Tests whether the first value is less than or equal to the second value and throws an exception
    /// if it is not.
    /// </summary>
    /// <typeparam name="T">
    /// The type of values to compare.
    /// </typeparam>
    /// <param name="expected">
    /// The first value to compare. This is the value the test expects to be less than or equal.
    /// </param>
    /// <param name="actual">
    /// The second value to compare. This is the value produced by the code under test.
    /// </param>
    /// <param name="message">
    /// The message to include in the exception when <paramref name="expected"/>
    /// is not less than or equal to <paramref name="actual"/>. The message is shown in
    /// test results.
    /// </param>
    /// <param name="parameters">
    /// An array of parameters to use when formatting <paramref name="message"/>.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// Thrown if <paramref name="expected"/> is not less than or equal to <paramref name="actual"/>.
    /// </exception>
    public static void IsLessThanOrEqualTo<T>(T expected, T actual, [StringSyntax(StringSyntaxAttribute.CompositeFormat)] string? message, params object?[]? parameters)
        where T : struct, IComparable<T>
    {
        if (expected.CompareTo(actual) <= 0)
        {
            return;
        }

        string userMessage = BuildUserMessage(message, parameters);
        ThrowAssertIsLessThanOrEqualToFailed(expected, actual, userMessage);
    }

    /// <summary>
    /// Tests whether the specified value is positive and throws an exception
    /// if it is not.
    /// </summary>
    /// <typeparam name="T">
    /// The type of value to test.
    /// </typeparam>
    /// <param name="actual">
    /// The value to test. This is the value produced by the code under test.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// Thrown if <paramref name="actual"/> is not positive.
    /// </exception>
    public static void IsPositive<T>(T actual)
        where T : struct, IComparable<T>
        => IsPositive(actual, string.Empty, null);

    /// <summary>
    /// Tests whether the specified value is positive and throws an exception
    /// if it is not.
    /// </summary>
    /// <typeparam name="T">
    /// The type of value to test.
    /// </typeparam>
    /// <param name="actual">
    /// The value to test. This is the value produced by the code under test.
    /// </param>
    /// <param name="message">
    /// The message to include in the exception when <paramref name="actual"/>
    /// is not positive. The message is shown in test results.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// Thrown if <paramref name="actual"/> is not positive.
    /// </exception>
    public static void IsPositive<T>(T actual, string? message)
        where T : struct, IComparable<T>
        => IsPositive(actual, message, null);

    /// <summary>
    /// Tests whether the specified value is positive and throws an exception
    /// if it is not.
    /// </summary>
    /// <typeparam name="T">
    /// The type of value to test.
    /// </typeparam>
    /// <param name="actual">
    /// The value to test. This is the value produced by the code under test.
    /// </param>
    /// <param name="message">
    /// The message to include in the exception when <paramref name="actual"/>
    /// is not positive. The message is shown in test results.
    /// </param>
    /// <param name="parameters">
    /// An array of parameters to use when formatting <paramref name="message"/>.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// Thrown if <paramref name="actual"/> is not positive.
    /// </exception>
    public static void IsPositive<T>(T actual, [StringSyntax(StringSyntaxAttribute.CompositeFormat)] string? message, params object?[]? parameters)
        where T : struct, IComparable<T>
    {
        var zero = default(T);
        
        // Handle special case for floating point NaN values
        if (actual is float floatValue && float.IsNaN(floatValue))
        {
            string userMessage = BuildUserMessage(message, parameters);
            ThrowAssertIsPositiveFailed(actual, userMessage);
            return;
        }
        
        if (actual is double doubleValue && double.IsNaN(doubleValue))
        {
            string userMessage = BuildUserMessage(message, parameters);
            ThrowAssertIsPositiveFailed(actual, userMessage);
            return;
        }
        
        if (actual.CompareTo(zero) > 0)
        {
            return;
        }

        string userMessage2 = BuildUserMessage(message, parameters);
        ThrowAssertIsPositiveFailed(actual, userMessage2);
    }

    /// <summary>
    /// Tests whether the specified value is negative and throws an exception
    /// if it is not.
    /// </summary>
    /// <typeparam name="T">
    /// The type of value to test.
    /// </typeparam>
    /// <param name="actual">
    /// The value to test. This is the value produced by the code under test.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// Thrown if <paramref name="actual"/> is not negative.
    /// </exception>
    public static void IsNegative<T>(T actual)
        where T : struct, IComparable<T>
        => IsNegative(actual, string.Empty, null);

    /// <summary>
    /// Tests whether the specified value is negative and throws an exception
    /// if it is not.
    /// </summary>
    /// <typeparam name="T">
    /// The type of value to test.
    /// </typeparam>
    /// <param name="actual">
    /// The value to test. This is the value produced by the code under test.
    /// </param>
    /// <param name="message">
    /// The message to include in the exception when <paramref name="actual"/>
    /// is not negative. The message is shown in test results.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// Thrown if <paramref name="actual"/> is not negative.
    /// </exception>
    public static void IsNegative<T>(T actual, string? message)
        where T : struct, IComparable<T>
        => IsNegative(actual, message, null);

    /// <summary>
    /// Tests whether the specified value is negative and throws an exception
    /// if it is not.
    /// </summary>
    /// <typeparam name="T">
    /// The type of value to test.
    /// </typeparam>
    /// <param name="actual">
    /// The value to test. This is the value produced by the code under test.
    /// </param>
    /// <param name="message">
    /// The message to include in the exception when <paramref name="actual"/>
    /// is not negative. The message is shown in test results.
    /// </param>
    /// <param name="parameters">
    /// An array of parameters to use when formatting <paramref name="message"/>.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// Thrown if <paramref name="actual"/> is not negative.
    /// </exception>
    public static void IsNegative<T>(T actual, [StringSyntax(StringSyntaxAttribute.CompositeFormat)] string? message, params object?[]? parameters)
        where T : struct, IComparable<T>
    {
        var zero = default(T);
        
        // Handle special case for floating point NaN values
        if (actual is float floatValue && float.IsNaN(floatValue))
        {
            string userMessage = BuildUserMessage(message, parameters);
            ThrowAssertIsNegativeFailed(actual, userMessage);
            return;
        }
        
        if (actual is double doubleValue && double.IsNaN(doubleValue))
        {
            string userMessage = BuildUserMessage(message, parameters);
            ThrowAssertIsNegativeFailed(actual, userMessage);
            return;
        }
        
        if (actual.CompareTo(zero) < 0)
        {
            return;
        }

        string userMessage2 = BuildUserMessage(message, parameters);
        ThrowAssertIsNegativeFailed(actual, userMessage2);
    }

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