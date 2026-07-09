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

    private static bool AreEqualFailing(double expected, double actual, double delta)
        => AreEqualFailingFloatingPoint(
            expected,
            actual,
            delta,
            static value => double.IsNaN(value),
            static value => value < 0,
            static (left, right, allowedDelta) => Math.Abs(left - right) > allowedDelta);

    /// <inheritdoc cref="AreEqual(double, double, double, string, string, string)" />
#pragma warning disable IDE0060 // Remove unused parameter - https://github.com/dotnet/roslyn/issues/76578
    public static void AreEqual(double expected, double actual, double delta, [InterpolatedStringHandlerArgument(nameof(expected), nameof(actual), nameof(delta))] ref AssertNonGenericAreEqualInterpolatedStringHandler message, [CallerArgumentExpression(nameof(expected))] string expectedExpression = "", [CallerArgumentExpression(nameof(actual))] string actualExpression = "")
#pragma warning restore IDE0060 // Remove unused parameter
    {
        TelemetryCollector.TrackAssertionCall("Assert.AreEqual");
        message.ComputeAssertion(expectedExpression, actualExpression);
    }

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
        TelemetryCollector.TrackAssertionCall("Assert.AreEqual");

        if (AreEqualFailing(expected, actual, delta))
        {
            ReportAssertAreEqualFailed(expected, actual, delta, message, expectedExpression, actualExpression);
        }
    }

    /// <inheritdoc cref="AreNotEqual(double, double, double, string, string, string)" />
#pragma warning disable IDE0060 // Remove unused parameter - https://github.com/dotnet/roslyn/issues/76578
    public static void AreNotEqual(double notExpected, double actual, double delta, [InterpolatedStringHandlerArgument(nameof(notExpected), nameof(actual), nameof(delta))] ref AssertNonGenericAreNotEqualInterpolatedStringHandler message, [CallerArgumentExpression(nameof(notExpected))] string notExpectedExpression = "", [CallerArgumentExpression(nameof(actual))] string actualExpression = "")
#pragma warning restore IDE0060 // Remove unused parameter
    {
        TelemetryCollector.TrackAssertionCall("Assert.AreNotEqual");
        message.ComputeAssertion(notExpectedExpression, actualExpression);
    }

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
        TelemetryCollector.TrackAssertionCall("Assert.AreNotEqual");

        if (AreNotEqualFailing(notExpected, actual, delta))
        {
            ReportAssertAreNotEqualFailed(notExpected, actual, delta, message, notExpectedExpression, actualExpression);
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

#pragma warning restore RS0026 // Do not add multiple public overloads with optional parameters
#pragma warning restore RS0027 // API with optional parameter(s) should have the most parameters amongst its public overloads
}
