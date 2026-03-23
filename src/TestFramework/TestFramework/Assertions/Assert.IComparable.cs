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
    /// <param name="lowerBoundExpression">
    /// The syntactic expression of lowerBound as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    /// <param name="valueExpression">
    /// The syntactic expression of value as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// Thrown if <paramref name="value"/> is not greater than <paramref name="lowerBound"/>.
    /// </exception>
    public static void IsGreaterThan<T>(T lowerBound, T value, string? message = "", [CallerArgumentExpression(nameof(lowerBound))] string lowerBoundExpression = "", [CallerArgumentExpression(nameof(value))] string valueExpression = "")
        where T : IComparable<T>
    {
        if (value.CompareTo(lowerBound) > 0)
        {
            return;
        }

        ThrowAssertIsGreaterThanFailed(lowerBound, value, message, lowerBoundExpression, valueExpression);
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
    /// <param name="lowerBoundExpression">
    /// The syntactic expression of lowerBound as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    /// <param name="valueExpression">
    /// The syntactic expression of value as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// Thrown if <paramref name="value"/> is not greater than or equal to <paramref name="lowerBound"/>.
    /// </exception>
    public static void IsGreaterThanOrEqualTo<T>(T lowerBound, T value, string? message = "", [CallerArgumentExpression(nameof(lowerBound))] string lowerBoundExpression = "", [CallerArgumentExpression(nameof(value))] string valueExpression = "")
        where T : IComparable<T>
    {
        if (value.CompareTo(lowerBound) >= 0)
        {
            return;
        }

        ThrowAssertIsGreaterThanOrEqualToFailed(lowerBound, value, message, lowerBoundExpression, valueExpression);
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
    /// <param name="upperBoundExpression">
    /// The syntactic expression of upperBound as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    /// <param name="valueExpression">
    /// The syntactic expression of value as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// Thrown if <paramref name="value"/> is not less than <paramref name="upperBound"/>.
    /// </exception>
    public static void IsLessThan<T>(T upperBound, T value, string? message = "", [CallerArgumentExpression(nameof(upperBound))] string upperBoundExpression = "", [CallerArgumentExpression(nameof(value))] string valueExpression = "")
        where T : IComparable<T>
    {
        if (value.CompareTo(upperBound) < 0)
        {
            return;
        }

        ThrowAssertIsLessThanFailed(upperBound, value, message, upperBoundExpression, valueExpression);
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
    /// <param name="upperBoundExpression">
    /// The syntactic expression of upperBound as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    /// <param name="valueExpression">
    /// The syntactic expression of value as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// Thrown if <paramref name="value"/> is not less than or equal to <paramref name="upperBound"/>.
    /// </exception>
    public static void IsLessThanOrEqualTo<T>(T upperBound, T value, string? message = "", [CallerArgumentExpression(nameof(upperBound))] string upperBoundExpression = "", [CallerArgumentExpression(nameof(value))] string valueExpression = "")
        where T : IComparable<T>
    {
        if (value.CompareTo(upperBound) <= 0)
        {
            return;
        }

        ThrowAssertIsLessThanOrEqualToFailed(upperBound, value, message, upperBoundExpression, valueExpression);
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
    /// <param name="valueExpression">
    /// The syntactic expression of value as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// Thrown if <paramref name="value"/> is not positive.
    /// </exception>
    public static void IsPositive<T>(T value, string? message = "", [CallerArgumentExpression(nameof(value))] string valueExpression = "")
        where T : struct, IComparable<T>
    {
        var zero = default(T);

        // Handle special case for floating point NaN values
        if (value is float.NaN)
        {
            ThrowAssertIsPositiveFailed(value, message, valueExpression);
            return;
        }

        if (value is double.NaN)
        {
            ThrowAssertIsPositiveFailed(value, message, valueExpression);
            return;
        }

        if (value.CompareTo(zero) > 0)
        {
            return;
        }

        ThrowAssertIsPositiveFailed(value, message, valueExpression);
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
    /// <param name="valueExpression">
    /// The syntactic expression of value as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// Thrown if <paramref name="value"/> is not negative.
    /// </exception>
    public static void IsNegative<T>(T value, string? message = "", [CallerArgumentExpression(nameof(value))] string valueExpression = "")
        where T : struct, IComparable<T>
    {
        var zero = default(T);

        // Handle special case for floating point NaN values
        if (value is float.NaN)
        {
            ThrowAssertIsNegativeFailed(value, message, valueExpression);
            return;
        }

        if (value is double.NaN)
        {
            ThrowAssertIsNegativeFailed(value, message, valueExpression);
            return;
        }

        if (value.CompareTo(zero) < 0)
        {
            return;
        }

        ThrowAssertIsNegativeFailed(value, message, valueExpression);
    }

    #endregion // IsNegative

    [DoesNotReturn]
    private static void ThrowAssertIsGreaterThanFailed<T>(T lowerBound, T value, string? userMessage, string lowerBoundExpression, string valueExpression)
    {
        string callSite = FormatCallSite("Assert.IsGreaterThan", new StringPair(nameof(lowerBound), lowerBoundExpression), new StringPair(nameof(value), valueExpression));
        string msg = string.Format(CultureInfo.CurrentCulture, FrameworkMessages.IsGreaterThanFailNew, FormatValue(value), FormatValue(lowerBound));
        msg += FormatAlignedParameters(
            new StringPair("lower bound", FormatValue(lowerBound)),
            new StringPair(nameof(value), FormatValue(value)));
        msg = AppendUserMessage(msg, userMessage);
        ThrowAssertFailed(callSite, msg);
    }

    [DoesNotReturn]
    private static void ThrowAssertIsGreaterThanOrEqualToFailed<T>(T lowerBound, T value, string? userMessage, string lowerBoundExpression, string valueExpression)
    {
        string callSite = FormatCallSite("Assert.IsGreaterThanOrEqualTo", new StringPair(nameof(lowerBound), lowerBoundExpression), new StringPair(nameof(value), valueExpression));
        string msg = string.Format(CultureInfo.CurrentCulture, FrameworkMessages.IsGreaterThanOrEqualToFailNew, FormatValue(value), FormatValue(lowerBound));
        msg += FormatAlignedParameters(
            new StringPair("lower bound", FormatValue(lowerBound)),
            new StringPair(nameof(value), FormatValue(value)));
        msg = AppendUserMessage(msg, userMessage);
        ThrowAssertFailed(callSite, msg);
    }

    [DoesNotReturn]
    private static void ThrowAssertIsLessThanFailed<T>(T upperBound, T value, string? userMessage, string upperBoundExpression, string valueExpression)
    {
        string callSite = FormatCallSite("Assert.IsLessThan", new StringPair(nameof(upperBound), upperBoundExpression), new StringPair(nameof(value), valueExpression));
        string msg = string.Format(CultureInfo.CurrentCulture, FrameworkMessages.IsLessThanFailNew, FormatValue(value), FormatValue(upperBound));
        msg += FormatAlignedParameters(
            new StringPair("upper bound", FormatValue(upperBound)),
            new StringPair(nameof(value), FormatValue(value)));
        msg = AppendUserMessage(msg, userMessage);
        ThrowAssertFailed(callSite, msg);
    }

    [DoesNotReturn]
    private static void ThrowAssertIsLessThanOrEqualToFailed<T>(T upperBound, T value, string? userMessage, string upperBoundExpression, string valueExpression)
    {
        string callSite = FormatCallSite("Assert.IsLessThanOrEqualTo", new StringPair(nameof(upperBound), upperBoundExpression), new StringPair(nameof(value), valueExpression));
        string msg = string.Format(CultureInfo.CurrentCulture, FrameworkMessages.IsLessThanOrEqualToFailNew, FormatValue(value), FormatValue(upperBound));
        msg += FormatAlignedParameters(
            new StringPair("upper bound", FormatValue(upperBound)),
            new StringPair(nameof(value), FormatValue(value)));
        msg = AppendUserMessage(msg, userMessage);
        ThrowAssertFailed(callSite, msg);
    }

    [DoesNotReturn]
    private static void ThrowAssertIsPositiveFailed<T>(T value, string? userMessage, string valueExpression)
    {
        string callSite = FormatCallSite("Assert.IsPositive", new StringPair(nameof(value), valueExpression));
        string msg = FrameworkMessages.IsPositiveFailNew;
        msg += Environment.NewLine + FormatParameter(nameof(value), valueExpression, value);
        msg = AppendUserMessage(msg, userMessage);
        ThrowAssertFailed(callSite, msg);
    }

    [DoesNotReturn]
    private static void ThrowAssertIsNegativeFailed<T>(T value, string? userMessage, string valueExpression)
    {
        string callSite = FormatCallSite("Assert.IsNegative", new StringPair(nameof(value), valueExpression));
        string msg = FrameworkMessages.IsNegativeFailNew;
        msg += Environment.NewLine + FormatParameter(nameof(value), valueExpression, value);
        msg = AppendUserMessage(msg, userMessage);
        ThrowAssertFailed(callSite, msg);
    }
}
