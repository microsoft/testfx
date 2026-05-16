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
        TelemetryCollector.TrackAssertionCall("Assert.IsGreaterThan");

        if (value.CompareTo(lowerBound) > 0)
        {
            return;
        }

        ReportAssertIsGreaterThanFailed(lowerBound, value, message, lowerBoundExpression, valueExpression);
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
        TelemetryCollector.TrackAssertionCall("Assert.IsGreaterThanOrEqualTo");

        if (value.CompareTo(lowerBound) >= 0)
        {
            return;
        }

        ReportAssertIsGreaterThanOrEqualToFailed(lowerBound, value, message, lowerBoundExpression, valueExpression);
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
        TelemetryCollector.TrackAssertionCall("Assert.IsLessThan");

        if (value.CompareTo(upperBound) < 0)
        {
            return;
        }

        ReportAssertIsLessThanFailed(upperBound, value, message, upperBoundExpression, valueExpression);
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
        TelemetryCollector.TrackAssertionCall("Assert.IsLessThanOrEqualTo");

        if (value.CompareTo(upperBound) <= 0)
        {
            return;
        }

        ReportAssertIsLessThanOrEqualToFailed(upperBound, value, message, upperBoundExpression, valueExpression);
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
        TelemetryCollector.TrackAssertionCall("Assert.IsPositive");

        var zero = default(T);

        // Handle special case for floating point NaN values
        if (value is float.NaN or double.NaN)
        {
            ReportAssertIsPositiveFailed(value, message, valueExpression);
            return;
        }

        if (value.CompareTo(zero) > 0)
        {
            return;
        }

        ReportAssertIsPositiveFailed(value, message, valueExpression);
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
        TelemetryCollector.TrackAssertionCall("Assert.IsNegative");

        var zero = default(T);

        // Handle special case for floating point NaN values
        if (value is float.NaN or double.NaN)
        {
            ReportAssertIsNegativeFailed(value, message, valueExpression);
            return;
        }

        if (value.CompareTo(zero) < 0)
        {
            return;
        }

        ReportAssertIsNegativeFailed(value, message, valueExpression);
    }

    #endregion // IsNegative

    [DoesNotReturn]
    private static void ReportAssertIsGreaterThanFailed<T>(T lowerBound, T value, string? userMessage, string lowerBoundExpression, string valueExpression)
        => ReportComparisonFailed("Assert.IsGreaterThan", FrameworkMessages.IsGreaterThanFailedSummary, "lower bound:", lowerBound, value, userMessage, lowerBoundExpression, valueExpression, "<lowerBound>");

    [DoesNotReturn]
    private static void ReportAssertIsGreaterThanOrEqualToFailed<T>(T lowerBound, T value, string? userMessage, string lowerBoundExpression, string valueExpression)
        => ReportComparisonFailed("Assert.IsGreaterThanOrEqualTo", FrameworkMessages.IsGreaterThanOrEqualToFailedSummary, "lower bound:", lowerBound, value, userMessage, lowerBoundExpression, valueExpression, "<lowerBound>");

    [DoesNotReturn]
    private static void ReportAssertIsLessThanFailed<T>(T upperBound, T value, string? userMessage, string upperBoundExpression, string valueExpression)
        => ReportComparisonFailed("Assert.IsLessThan", FrameworkMessages.IsLessThanFailedSummary, "upper bound:", upperBound, value, userMessage, upperBoundExpression, valueExpression, "<upperBound>");

    [DoesNotReturn]
    private static void ReportAssertIsLessThanOrEqualToFailed<T>(T upperBound, T value, string? userMessage, string upperBoundExpression, string valueExpression)
        => ReportComparisonFailed("Assert.IsLessThanOrEqualTo", FrameworkMessages.IsLessThanOrEqualToFailedSummary, "upper bound:", upperBound, value, userMessage, upperBoundExpression, valueExpression, "<upperBound>");

    [DoesNotReturn]
    private static void ReportComparisonFailed<T>(string assertionName, string summary, string boundLabel, T bound, T value, string? userMessage, string boundExpression, string valueExpression, string boundPlaceholder)
    {
        string boundText = AssertionValueRenderer.RenderValue(bound);
        string actualText = AssertionValueRenderer.RenderValue(value);
        EvidenceBlock evidence = EvidenceBlock.Create()
            .AddLine(boundLabel, boundText)
            .AddLine("actual:", actualText);

        StructuredAssertionMessage structured = new(summary);
        structured.WithUserMessage(userMessage);
        structured.WithEvidence(evidence);
        structured.WithExpectedAndActual(boundText, actualText);
        structured.WithCallSiteExpression(FormatCallSiteExpression(assertionName, boundExpression, valueExpression, boundPlaceholder, "<value>"));

        ReportAssertFailed(structured);
    }

    [DoesNotReturn]
    private static void ReportAssertIsPositiveFailed<T>(T value, string? userMessage, string valueExpression)
        => ReportSignFailed("Assert.IsPositive", FrameworkMessages.IsPositiveFailedSummary, "> 0", value, userMessage, valueExpression);

    [DoesNotReturn]
    private static void ReportAssertIsNegativeFailed<T>(T value, string? userMessage, string valueExpression)
        => ReportSignFailed("Assert.IsNegative", FrameworkMessages.IsNegativeFailedSummary, "< 0", value, userMessage, valueExpression);

    [DoesNotReturn]
    private static void ReportSignFailed<T>(string assertionName, string summary, string expectedText, T value, string? userMessage, string valueExpression)
    {
        string actualText = AssertionValueRenderer.RenderValue(value);
        EvidenceBlock evidence = EvidenceBlock.Create()
            .AddLine("expected:", expectedText)
            .AddLine("actual:", actualText);

        StructuredAssertionMessage structured = new(summary);
        structured.WithUserMessage(userMessage);
        structured.WithEvidence(evidence);
        structured.WithExpectedAndActual(expectedText, actualText);
        structured.WithCallSiteExpression(FormatCallSiteExpression(assertionName, valueExpression, nameof(value)));

        ReportAssertFailed(structured);
    }
}
