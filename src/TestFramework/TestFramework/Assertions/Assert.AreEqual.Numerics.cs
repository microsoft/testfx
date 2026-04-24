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
#pragma warning disable RS0026 // Do not add multiple public overloads with optional parameters
#pragma warning disable RS0027 // API with optional parameter(s) should have the most parameters amongst its public overloads

    private static bool AreEqualFailing(float expected, float actual, float delta)
    {
        if (float.IsNaN(delta) || delta < 0)
        {
            // NaN and negative values don't make sense as a delta value.
            throw new ArgumentOutOfRangeException(nameof(delta));
        }

        if (expected.Equals(actual))
        {
            return false;
        }

        // If both floats are NaN, then they were considered equal in the previous check.
        // If only one of them is NaN, then they are not equal regardless of the value of delta.
        // Then, the subtraction comparison to delta isn't involving NaNs.
        return float.IsNaN(expected) || float.IsNaN(actual) ||
                Math.Abs(expected - actual) > delta;
    }

    private static bool AreEqualFailing(double expected, double actual, double delta)
    {
        if (double.IsNaN(delta) || delta < 0)
        {
            // NaN and negative values don't make sense as a delta value.
            throw new ArgumentOutOfRangeException(nameof(delta));
        }

        if (expected.Equals(actual))
        {
            return false;
        }

        // If both doubles are NaN, then they were considered equal in the previous check.
        // If only one of them is NaN, then they are not equal regardless of the value of delta.
        // Then, the subtraction comparison to delta isn't involving NaNs.
        return double.IsNaN(expected) || double.IsNaN(actual) ||
                Math.Abs(expected - actual) > delta;
    }

    private static bool AreEqualFailing(decimal expected, decimal actual, decimal delta)
    {
        if (delta < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(delta));
        }

        return Math.Abs(expected - actual) > delta;
    }

    private static bool AreEqualFailing(long expected, long actual, long delta)
    {
        if (delta < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(delta));
        }

        return Math.Abs(expected - actual) > delta;
    }

    [DoesNotReturn]
    private static void ReportAssertAreEqualFailed<T>(T expected, T actual, T delta, string userMessage)
        where T : struct, IConvertible
    {
        string finalMessage = string.Format(
            CultureInfo.CurrentCulture,
            FrameworkMessages.AreEqualDeltaFailMsg,
            userMessage,
            expected.ToString(CultureInfo.CurrentCulture.NumberFormat),
            actual.ToString(CultureInfo.CurrentCulture.NumberFormat),
            delta.ToString(CultureInfo.CurrentCulture.NumberFormat));
        ReportAssertFailed("Assert.AreEqual", finalMessage);
    }

    /// <inheritdoc cref="AreEqual(float, float, float, string, string, string)"/>
#pragma warning disable IDE0060 // Remove unused parameter - https://github.com/dotnet/roslyn/issues/76578
    public static void AreEqual(float expected, float actual, float delta, [InterpolatedStringHandlerArgument(nameof(expected), nameof(actual), nameof(delta))] ref AssertNonGenericAreEqualInterpolatedStringHandler message, [CallerArgumentExpression(nameof(expected))] string expectedExpression = "", [CallerArgumentExpression(nameof(actual))] string actualExpression = "")
#pragma warning restore IDE0060 // Remove unused parameter
        => message.ComputeAssertion(expectedExpression, actualExpression);

    /// <summary>
    /// Tests whether the specified floats are equal and throws an exception
    /// if they are not equal.
    /// </summary>
    /// <param name="expected">
    /// The first float to compare. This is the float the tests expects.
    /// </param>
    /// <param name="actual">
    /// The second float to compare. This is the float produced by the code under test.
    /// </param>
    /// <param name="delta">
    /// The required accuracy. An exception will be thrown only if
    /// <paramref name="actual"/> is different than <paramref name="expected"/>
    /// by more than <paramref name="delta"/>.
    /// </param>
    /// <param name="message">
    /// The message to include in the exception when <paramref name="actual"/>
    /// is different than <paramref name="expected"/> by more than
    /// <paramref name="delta"/>. The message is shown in test results.
    /// </param>
    /// <param name="expectedExpression">
    /// The syntactic expression of expected as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    /// <param name="actualExpression">
    /// The syntactic expression of actual as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// Thrown if <paramref name="expected"/> is not equal to
    /// <paramref name="actual"/>.
    /// </exception>
    public static void AreEqual(float expected, float actual, float delta, string? message = "", [CallerArgumentExpression(nameof(expected))] string expectedExpression = "", [CallerArgumentExpression(nameof(actual))] string actualExpression = "")
    {
        if (AreEqualFailing(expected, actual, delta))
        {
            string userMessage = BuildUserMessageForExpectedExpressionAndActualExpression(message, expectedExpression, actualExpression);
            ReportAssertAreEqualFailed(expected, actual, delta, userMessage);
        }
    }

    /// <inheritdoc cref="AreNotEqual(float, float, float, string, string, string)" />
#pragma warning disable IDE0060 // Remove unused parameter - https://github.com/dotnet/roslyn/issues/76578
    public static void AreNotEqual(float notExpected, float actual, float delta, [InterpolatedStringHandlerArgument(nameof(notExpected), nameof(actual), nameof(delta))] ref AssertNonGenericAreNotEqualInterpolatedStringHandler message, [CallerArgumentExpression(nameof(notExpected))] string notExpectedExpression = "", [CallerArgumentExpression(nameof(actual))] string actualExpression = "")
#pragma warning restore IDE0060 // Remove unused parameter
        => message.ComputeAssertion(notExpectedExpression, actualExpression);

    /// <summary>
    /// Tests whether the specified floats are unequal and throws an exception
    /// if they are equal.
    /// </summary>
    /// <param name="notExpected">
    /// The first float to compare. This is the float the test expects not to
    /// match <paramref name="actual"/>.
    /// </param>
    /// <param name="actual">
    /// The second float to compare. This is the float produced by the code under test.
    /// </param>
    /// <param name="delta">
    /// The required accuracy. An exception will be thrown only if
    /// <paramref name="actual"/> is different than <paramref name="notExpected"/>
    /// by at most <paramref name="delta"/>.
    /// </param>
    /// <param name="message">
    /// The message to include in the exception when <paramref name="actual"/>
    /// is equal to <paramref name="notExpected"/> or different by less than
    /// <paramref name="delta"/>. The message is shown in test results.
    /// </param>
    /// <param name="notExpectedExpression">
    /// The syntactic expression of notExpected as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    /// <param name="actualExpression">
    /// The syntactic expression of actual as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// Thrown if <paramref name="notExpected"/> is equal to <paramref name="actual"/>.
    /// </exception>
    public static void AreNotEqual(float notExpected, float actual, float delta, string? message = "", [CallerArgumentExpression(nameof(notExpected))] string notExpectedExpression = "", [CallerArgumentExpression(nameof(actual))] string actualExpression = "")
    {
        if (AreNotEqualFailing(notExpected, actual, delta))
        {
            string userMessage = BuildUserMessageForNotExpectedExpressionAndActualExpression(message, notExpectedExpression, actualExpression);
            ReportAssertAreNotEqualFailed(notExpected, actual, delta, userMessage);
        }
    }

    private static bool AreNotEqualFailing(float notExpected, float actual, float delta)
    {
        if (float.IsNaN(delta) || delta < 0)
        {
            // NaN and negative values don't make sense as a delta value.
            throw new ArgumentOutOfRangeException(nameof(delta));
        }

        if (float.IsNaN(notExpected) && float.IsNaN(actual))
        {
            // If both notExpected and actual are NaN, then AreNotEqual should fail.
            return true;
        }

        // Note: if both notExpected and actual are NaN, that was handled separately above.
        // Now, if both are numerics, then the logic is good.
        // And, if only one of them is NaN, we know they are not equal, meaning AreNotEqual shouldn't fail.
        // And in this case we will correctly be returning false, because NaN <= anything is always false.
        return Math.Abs(notExpected - actual) <= delta;
    }

    /// <inheritdoc cref="AreEqual(decimal, decimal, decimal, string, string, string)" />
#pragma warning disable IDE0060 // Remove unused parameter - https://github.com/dotnet/roslyn/issues/76578
    public static void AreEqual(decimal expected, decimal actual, decimal delta, [InterpolatedStringHandlerArgument(nameof(expected), nameof(actual), nameof(delta))] ref AssertNonGenericAreEqualInterpolatedStringHandler message, [CallerArgumentExpression(nameof(expected))] string expectedExpression = "", [CallerArgumentExpression(nameof(actual))] string actualExpression = "")
#pragma warning restore IDE0060 // Remove unused parameter
        => message.ComputeAssertion(expectedExpression, actualExpression);

    /// <summary>
    /// Tests whether the specified decimals are equal and throws an exception
    /// if they are not equal.
    /// </summary>
    /// <param name="expected">
    /// The first decimal to compare. This is the decimal the tests expects.
    /// </param>
    /// <param name="actual">
    /// The second decimal to compare. This is the decimal produced by the code under test.
    /// </param>
    /// <param name="delta">
    /// The required accuracy. An exception will be thrown only if
    /// <paramref name="actual"/> is different than <paramref name="expected"/>
    /// by more than <paramref name="delta"/>.
    /// </param>
    /// <param name="message">
    /// The message to include in the exception when <paramref name="actual"/>
    /// is different than <paramref name="expected"/> by more than
    /// <paramref name="delta"/>. The message is shown in test results.
    /// </param>
    /// <param name="expectedExpression">
    /// The syntactic expression of expected as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    /// <param name="actualExpression">
    /// The syntactic expression of actual as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// Thrown if <paramref name="expected"/> is not equal to
    /// <paramref name="actual"/>.
    /// </exception>
    public static void AreEqual(decimal expected, decimal actual, decimal delta, string? message = "", [CallerArgumentExpression(nameof(expected))] string expectedExpression = "", [CallerArgumentExpression(nameof(actual))] string actualExpression = "")
    {
        if (AreEqualFailing(expected, actual, delta))
        {
            string userMessage = BuildUserMessageForExpectedExpressionAndActualExpression(message, expectedExpression, actualExpression);
            ReportAssertAreEqualFailed(expected, actual, delta, userMessage);
        }
    }

    /// <inheritdoc cref="AreNotEqual(decimal, decimal, decimal, string, string, string)" />
#pragma warning disable IDE0060 // Remove unused parameter - https://github.com/dotnet/roslyn/issues/76578
    public static void AreNotEqual(decimal notExpected, decimal actual, decimal delta, [InterpolatedStringHandlerArgument(nameof(notExpected), nameof(actual), nameof(delta))] ref AssertNonGenericAreNotEqualInterpolatedStringHandler message, [CallerArgumentExpression(nameof(notExpected))] string notExpectedExpression = "", [CallerArgumentExpression(nameof(actual))] string actualExpression = "")
#pragma warning restore IDE0060 // Remove unused parameter
        => message.ComputeAssertion(notExpectedExpression, actualExpression);

    /// <summary>
    /// Tests whether the specified decimals are unequal and throws an exception
    /// if they are equal.
    /// </summary>
    /// <param name="notExpected">
    /// The first decimal to compare. This is the decimal the test expects not to
    /// match <paramref name="actual"/>.
    /// </param>
    /// <param name="actual">
    /// The second decimal to compare. This is the decimal produced by the code under test.
    /// </param>
    /// <param name="delta">
    /// The required accuracy. An exception will be thrown only if
    /// <paramref name="actual"/> is different than <paramref name="notExpected"/>
    /// by at most <paramref name="delta"/>.
    /// </param>
    /// <param name="message">
    /// The message to include in the exception when <paramref name="actual"/>
    /// is equal to <paramref name="notExpected"/> or different by less than
    /// <paramref name="delta"/>. The message is shown in test results.
    /// </param>
    /// <param name="notExpectedExpression">
    /// The syntactic expression of notExpected as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    /// <param name="actualExpression">
    /// The syntactic expression of actual as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// Thrown if <paramref name="notExpected"/> is equal to <paramref name="actual"/>.
    /// </exception>
    public static void AreNotEqual(decimal notExpected, decimal actual, decimal delta, string? message = "", [CallerArgumentExpression(nameof(notExpected))] string notExpectedExpression = "", [CallerArgumentExpression(nameof(actual))] string actualExpression = "")
    {
        if (AreNotEqualFailing(notExpected, actual, delta))
        {
            string userMessage = BuildUserMessageForNotExpectedExpressionAndActualExpression(message, notExpectedExpression, actualExpression);
            ReportAssertAreNotEqualFailed(notExpected, actual, delta, userMessage);
        }
    }

    private static bool AreNotEqualFailing(decimal notExpected, decimal actual, decimal delta)
        => Math.Abs(notExpected - actual) <= delta;

    /// <inheritdoc cref="AreEqual(long, long, long, string, string, string)" />
#pragma warning disable IDE0060 // Remove unused parameter - https://github.com/dotnet/roslyn/issues/76578
    public static void AreEqual(long expected, long actual, long delta, [InterpolatedStringHandlerArgument(nameof(expected), nameof(actual), nameof(delta))] ref AssertNonGenericAreEqualInterpolatedStringHandler message, [CallerArgumentExpression(nameof(expected))] string expectedExpression = "", [CallerArgumentExpression(nameof(actual))] string actualExpression = "")
#pragma warning restore IDE0060 // Remove unused parameter
        => message.ComputeAssertion(expectedExpression, actualExpression);

    /// <summary>
    /// Tests whether the specified longs are equal and throws an exception
    /// if they are not equal.
    /// </summary>
    /// <param name="expected">
    /// The first long to compare. This is the long the tests expects.
    /// </param>
    /// <param name="actual">
    /// The second long to compare. This is the long produced by the code under test.
    /// </param>
    /// <param name="delta">
    /// The required accuracy. An exception will be thrown only if
    /// <paramref name="actual"/> is different than <paramref name="expected"/>
    /// by more than <paramref name="delta"/>.
    /// </param>
    /// <param name="message">
    /// The message to include in the exception when <paramref name="actual"/>
    /// is different than <paramref name="expected"/> by more than
    /// <paramref name="delta"/>. The message is shown in test results.
    /// </param>
    /// <param name="expectedExpression">
    /// The syntactic expression of expected as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    /// <param name="actualExpression">
    /// The syntactic expression of actual as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// Thrown if <paramref name="expected"/> is not equal to
    /// <paramref name="actual"/>.
    /// </exception>
    public static void AreEqual(long expected, long actual, long delta, string? message = "", [CallerArgumentExpression(nameof(expected))] string expectedExpression = "", [CallerArgumentExpression(nameof(actual))] string actualExpression = "")
    {
        if (AreEqualFailing(expected, actual, delta))
        {
            string userMessage = BuildUserMessageForExpectedExpressionAndActualExpression(message, expectedExpression, actualExpression);
            ReportAssertAreEqualFailed(expected, actual, delta, userMessage);
        }
    }

    /// <inheritdoc cref="AreNotEqual(long, long, long, string, string, string)" />
#pragma warning disable IDE0060 // Remove unused parameter - https://github.com/dotnet/roslyn/issues/76578
    public static void AreNotEqual(long notExpected, long actual, long delta, [InterpolatedStringHandlerArgument(nameof(notExpected), nameof(actual), nameof(delta))] ref AssertNonGenericAreNotEqualInterpolatedStringHandler message, [CallerArgumentExpression(nameof(notExpected))] string notExpectedExpression = "", [CallerArgumentExpression(nameof(actual))] string actualExpression = "")
#pragma warning restore IDE0060 // Remove unused parameter
        => message.ComputeAssertion(notExpectedExpression, actualExpression);

    /// <summary>
    /// Tests whether the specified longs are unequal and throws an exception
    /// if they are equal.
    /// </summary>
    /// <param name="notExpected">
    /// The first long to compare. This is the long the test expects not to
    /// match <paramref name="actual"/>.
    /// </param>
    /// <param name="actual">
    /// The second long to compare. This is the long produced by the code under test.
    /// </param>
    /// <param name="delta">
    /// The required accuracy. An exception will be thrown only if
    /// <paramref name="actual"/> is different than <paramref name="notExpected"/>
    /// by at most <paramref name="delta"/>.
    /// </param>
    /// <param name="message">
    /// The message to include in the exception when <paramref name="actual"/>
    /// is equal to <paramref name="notExpected"/> or different by less than
    /// <paramref name="delta"/>. The message is shown in test results.
    /// </param>
    /// <param name="notExpectedExpression">
    /// The syntactic expression of notExpected as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    /// <param name="actualExpression">
    /// The syntactic expression of actual as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// Thrown if <paramref name="notExpected"/> is equal to <paramref name="actual"/>.
    /// </exception>
    public static void AreNotEqual(long notExpected, long actual, long delta, string? message = "", [CallerArgumentExpression(nameof(notExpected))] string notExpectedExpression = "", [CallerArgumentExpression(nameof(actual))] string actualExpression = "")
    {
        if (AreNotEqualFailing(notExpected, actual, delta))
        {
            string userMessage = BuildUserMessageForNotExpectedExpressionAndActualExpression(message, notExpectedExpression, actualExpression);
            ReportAssertAreNotEqualFailed(notExpected, actual, delta, userMessage);
        }
    }

    private static bool AreNotEqualFailing(long notExpected, long actual, long delta)
        => Math.Abs(notExpected - actual) <= delta;

    /// <inheritdoc cref="AreEqual(double, double, double, string, string, string)" />
#pragma warning disable IDE0060 // Remove unused parameter - https://github.com/dotnet/roslyn/issues/76578
    public static void AreEqual(double expected, double actual, double delta, [InterpolatedStringHandlerArgument(nameof(expected), nameof(actual), nameof(delta))] ref AssertNonGenericAreEqualInterpolatedStringHandler message, [CallerArgumentExpression(nameof(expected))] string expectedExpression = "", [CallerArgumentExpression(nameof(actual))] string actualExpression = "")
#pragma warning restore IDE0060 // Remove unused parameter
        => message.ComputeAssertion(expectedExpression, actualExpression);

    /// <summary>
    /// Tests whether the specified doubles are equal and throws an exception
    /// if they are not equal.
    /// </summary>
    /// <param name="expected">
    /// The first double to compare. This is the double the tests expects.
    /// </param>
    /// <param name="actual">
    /// The second double to compare. This is the double produced by the code under test.
    /// </param>
    /// <param name="delta">
    /// The required accuracy. An exception will be thrown only if
    /// <paramref name="actual"/> is different than <paramref name="expected"/>
    /// by more than <paramref name="delta"/>.
    /// </param>
    /// <param name="message">
    /// The message to include in the exception when <paramref name="actual"/>
    /// is different than <paramref name="expected"/> by more than
    /// <paramref name="delta"/>. The message is shown in test results.
    /// </param>
    /// <param name="expectedExpression">
    /// The syntactic expression of expected as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    /// <param name="actualExpression">
    /// The syntactic expression of actual as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// Thrown if <paramref name="expected"/> is not equal to <paramref name="actual"/>.
    /// </exception>
    public static void AreEqual(double expected, double actual, double delta, string? message = "", [CallerArgumentExpression(nameof(expected))] string expectedExpression = "", [CallerArgumentExpression(nameof(actual))] string actualExpression = "")
    {
        if (AreEqualFailing(expected, actual, delta))
        {
            string userMessage = BuildUserMessageForExpectedExpressionAndActualExpression(message, expectedExpression, actualExpression);
            ReportAssertAreEqualFailed(expected, actual, delta, userMessage);
        }
    }

    /// <inheritdoc cref="AreNotEqual(double, double, double, string, string, string)" />
#pragma warning disable IDE0060 // Remove unused parameter - https://github.com/dotnet/roslyn/issues/76578
    public static void AreNotEqual(double notExpected, double actual, double delta, [InterpolatedStringHandlerArgument(nameof(notExpected), nameof(actual), nameof(delta))] ref AssertNonGenericAreNotEqualInterpolatedStringHandler message, [CallerArgumentExpression(nameof(notExpected))] string notExpectedExpression = "", [CallerArgumentExpression(nameof(actual))] string actualExpression = "")
#pragma warning restore IDE0060 // Remove unused parameter
        => message.ComputeAssertion(notExpectedExpression, actualExpression);

    /// <summary>
    /// Tests whether the specified doubles are unequal and throws an exception
    /// if they are equal.
    /// </summary>
    /// <param name="notExpected">
    /// The first double to compare. This is the double the test expects not to
    /// match <paramref name="actual"/>.
    /// </param>
    /// <param name="actual">
    /// The second double to compare. This is the double produced by the code under test.
    /// </param>
    /// <param name="delta">
    /// The required accuracy. An exception will be thrown only if
    /// <paramref name="actual"/> is different than <paramref name="notExpected"/>
    /// by at most <paramref name="delta"/>.
    /// </param>
    /// <param name="message">
    /// The message to include in the exception when <paramref name="actual"/>
    /// is equal to <paramref name="notExpected"/> or different by less than
    /// <paramref name="delta"/>. The message is shown in test results.
    /// </param>
    /// <param name="notExpectedExpression">
    /// The syntactic expression of notExpected as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    /// <param name="actualExpression">
    /// The syntactic expression of actual as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// Thrown if <paramref name="notExpected"/> is equal to <paramref name="actual"/>.
    /// </exception>
    public static void AreNotEqual(double notExpected, double actual, double delta, string? message = "", [CallerArgumentExpression(nameof(notExpected))] string notExpectedExpression = "", [CallerArgumentExpression(nameof(actual))] string actualExpression = "")
    {
        if (AreNotEqualFailing(notExpected, actual, delta))
        {
            string userMessage = BuildUserMessageForNotExpectedExpressionAndActualExpression(message, notExpectedExpression, actualExpression);
            ReportAssertAreNotEqualFailed(notExpected, actual, delta, userMessage);
        }
    }

    private static bool AreNotEqualFailing(double notExpected, double actual, double delta)
    {
        if (double.IsNaN(delta) || delta < 0)
        {
            // NaN and negative values don't make sense as a delta value.
            throw new ArgumentOutOfRangeException(nameof(delta));
        }

        if (double.IsNaN(notExpected) && double.IsNaN(actual))
        {
            // If both notExpected and actual are NaN, then AreNotEqual should fail.
            return true;
        }

        // Note: if both notExpected and actual are NaN, that was handled separately above.
        // Now, if both are numerics, then the logic is good.
        // And, if only one of them is NaN, we know they are not equal, meaning AreNotEqual shouldn't fail.
        // And in this case we will correctly be returning false, because NaN <= anything is always false.
        return Math.Abs(notExpected - actual) <= delta;
    }

    [DoesNotReturn]
    private static void ReportAssertAreNotEqualFailed<T>(T notExpected, T actual, T delta, string userMessage)
        where T : struct, IConvertible
    {
        string finalMessage = string.Format(
            CultureInfo.CurrentCulture,
            FrameworkMessages.AreNotEqualDeltaFailMsg,
            userMessage,
            notExpected.ToString(CultureInfo.CurrentCulture.NumberFormat),
            actual.ToString(CultureInfo.CurrentCulture.NumberFormat),
            delta.ToString(CultureInfo.CurrentCulture.NumberFormat));
        ReportAssertFailed("Assert.AreNotEqual", finalMessage);
    }

#pragma warning restore RS0026 // Do not add multiple public overloads with optional parameters
#pragma warning restore RS0027 // API with optional parameter(s) should have the most parameters amongst its public overloads
}
