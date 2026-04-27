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

    /// <summary>
    /// Tests whether the specified values are equal and throws an exception
    /// if the two values are not equal.
    /// The equality is computed using the default <see cref="EqualityComparer{T}"/>.
    /// </summary>
    /// <typeparam name="T">
    /// The type of values to compare.
    /// </typeparam>
    /// <param name="expected">
    /// The first value to compare. This is the value the tests expects.
    /// </param>
    /// <param name="actual">
    /// The second value to compare. This is the value produced by the code under test.
    /// </param>
    /// <param name="message">
    /// The message to include in the exception when <paramref name="actual"/>
    /// is not equal to <paramref name="expected"/>. The message is shown in
    /// test results.
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
    public static void AreEqual<T>(T? expected, T? actual, string? message = "", [CallerArgumentExpression(nameof(expected))] string expectedExpression = "", [CallerArgumentExpression(nameof(actual))] string actualExpression = "")
        => AreEqual(expected, actual, EqualityComparer<T>.Default, message, expectedExpression, actualExpression);

    /// <summary>
    /// Tests whether the specified values are equal and throws an exception
    /// if the two values are not equal.
    /// The equality is computed using the default <see cref="EqualityComparer{T}"/>.
    /// </summary>
    /// <typeparam name="T">
    /// The type of values to compare.
    /// </typeparam>
    /// <param name="expected">
    /// The first value to compare. This is the value the tests expects.
    /// </param>
    /// <param name="actual">
    /// The second value to compare. This is the value produced by the code under test.
    /// </param>
    /// <param name="message">
    /// The message to include in the exception when <paramref name="actual"/>
    /// is not equal to <paramref name="expected"/>. The message is shown in
    /// test results.
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
#pragma warning disable IDE0060 // Remove unused parameter - https://github.com/dotnet/roslyn/issues/76578
    public static void AreEqual<T>(T? expected, T? actual, [InterpolatedStringHandlerArgument(nameof(expected), nameof(actual))] ref AssertAreEqualInterpolatedStringHandler<T> message, [CallerArgumentExpression(nameof(expected))] string expectedExpression = "", [CallerArgumentExpression(nameof(actual))] string actualExpression = "")
#pragma warning restore IDE0060 // Remove unused parameter
        => message.ComputeAssertion(expectedExpression, actualExpression);

    /// <inheritdoc cref="AreEqual{T}(T, T, IEqualityComparer{T}?, string, string, string)" />
#pragma warning disable IDE0060 // Remove unused parameter - https://github.com/dotnet/roslyn/issues/76578
    public static void AreEqual<T>(T? expected, T? actual, IEqualityComparer<T>? comparer, [InterpolatedStringHandlerArgument(nameof(expected), nameof(actual), nameof(comparer))] ref AssertAreEqualInterpolatedStringHandler<T> message, [CallerArgumentExpression(nameof(expected))] string expectedExpression = "", [CallerArgumentExpression(nameof(actual))] string actualExpression = "")
#pragma warning restore IDE0060 // Remove unused parameter
        => message.ComputeAssertion(expectedExpression, actualExpression);

    /// <summary>
    /// Tests whether the specified values are equal and throws an exception
    /// if the two values are not equal.
    /// The equality is computed using the provided <paramref name="comparer"/> parameter.
    /// </summary>
    /// <typeparam name="T">
    /// The type of values to compare.
    /// </typeparam>
    /// <param name="expected">
    /// The first value to compare. This is the value the tests expects.
    /// </param>
    /// <param name="actual">
    /// The second value to compare. This is the value produced by the code under test.
    /// </param>
    /// <param name="comparer">
    /// The <see cref="IEqualityComparer{T}"/> implementation to use when comparing keys,
    /// or null to use the default <see cref="EqualityComparer{T}"/>.
    /// </param>
    /// <param name="message">
    /// The message to include in the exception when <paramref name="actual"/>
    /// is not equal to <paramref name="expected"/>. The message is shown in
    /// test results.
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
    public static void AreEqual<T>(T? expected, T? actual, IEqualityComparer<T> comparer, string? message = "", [CallerArgumentExpression(nameof(expected))] string expectedExpression = "", [CallerArgumentExpression(nameof(actual))] string actualExpression = "")
    {
        if (!AreEqualFailing(expected, actual, comparer))
        {
            return;
        }

        string userMessage = BuildUserMessageForExpectedExpressionAndActualExpression(message, expectedExpression, actualExpression);
        ReportAssertAreEqualFailed(expected, actual, userMessage);
    }

    private static bool AreEqualFailing<T>(T? expected, T? actual, IEqualityComparer<T>? comparer)
        => !(comparer ?? EqualityComparer<T>.Default).Equals(expected!, actual!);

    private static string FormatStringComparisonMessage(string? expected, string? actual, string userMessage)
    {
        // Handle null cases
        if (expected is null && actual is null)
        {
            return string.Format(
                CultureInfo.CurrentCulture,
                FrameworkMessages.AreEqualFailMsg,
                userMessage,
                ReplaceNulls(expected),
                ReplaceNulls(actual));
        }

        if (expected is null || actual is null)
        {
            return string.Format(
                CultureInfo.CurrentCulture,
                FrameworkMessages.AreEqualFailMsg,
                userMessage,
                ReplaceNulls(expected),
                ReplaceNulls(actual));
        }

        // Find the first difference
        int diffIndex = FindFirstStringDifference(expected, actual);

        if (diffIndex == -1)
        {
            // Strings are equal - should not happen in practice, we call this method only when they are not equal.
            throw ApplicationStateGuard.Unreachable();
        }

        // Format the enhanced string comparison message
        return FormatStringDifferenceMessage(expected, actual, diffIndex, userMessage);
    }

    private static int FindFirstStringDifference(string expected, string actual)
    {
        int minLength = Math.Min(expected.Length, actual.Length);

        for (int i = 0; i < minLength; i++)
        {
            if (expected[i] != actual[i])
            {
                return i;
            }
        }

        // If we reach here, one string is a prefix of the other
        return expected.Length != actual.Length ? minLength : -1;
    }

    private static string FormatStringDifferenceMessage(string expected, string actual, int diffIndex, string userMessage)
    {
        string lengthInfo = expected.Length == actual.Length
            ? string.Format(CultureInfo.CurrentCulture, FrameworkMessages.AreEqualStringDiffLengthBothMsg, expected.Length, diffIndex)
            : string.Format(CultureInfo.CurrentCulture, FrameworkMessages.AreEqualStringDiffLengthDifferentMsg, expected.Length, actual.Length);

        // Create contextual preview around the difference
        const int contextLength = 41; // Show up to 20 characters of context on each side
        Tuple<string, string, int> tuple = StringPreviewHelper.CreateStringPreviews(expected, actual, diffIndex, contextLength);
        string expectedPreview = tuple.Item1;
        string actualPreview = tuple.Item2;
        int caretPosition = tuple.Item3;

        // Get localized prefixes
        string expectedPrefix = FrameworkMessages.AreEqualStringDiffExpectedPrefix;
        string actualPrefix = FrameworkMessages.AreEqualStringDiffActualPrefix;

        // Calculate the maximum prefix length to align the caret properly
        int maxPrefixLength = Math.Max(expectedPrefix.Length, actualPrefix.Length);

        // Pad shorter prefix to match the longer one for proper alignment
        string paddedExpectedPrefix = expectedPrefix.PadRight(maxPrefixLength);
        string paddedActualPrefix = actualPrefix.PadRight(maxPrefixLength);

        // Build the formatted lines with proper alignment
        string expectedLine = paddedExpectedPrefix + $"\"{expectedPreview}\"";
        string actualLine = paddedActualPrefix + $"\"{actualPreview}\"";

        // The caret should align under the difference in the string content
        // For localized prefixes with different lengths, we need to account for the longer prefix
        // to ensure proper alignment. But the caret position is relative to the string content.
        int adjustedCaretPosition = maxPrefixLength + 1 + caretPosition; // +1 for the opening quote

        // Format user message properly - add leading space if not empty, otherwise no extra formatting
        string formattedUserMessage = string.IsNullOrEmpty(userMessage) ? string.Empty : $" {userMessage}";

        return string.Format(
            CultureInfo.CurrentCulture,
            FrameworkMessages.AreEqualStringDiffFailMsg,
            lengthInfo,
            formattedUserMessage,
            expectedLine,
            actualLine,
            new string('-', adjustedCaretPosition) + "^");
    }

    [DoesNotReturn]
    private static void ReportAssertAreEqualFailed(object? expected, object? actual, string userMessage)
    {
        string finalMessage = actual != null && expected != null && !actual.GetType().Equals(expected.GetType())
            ? string.Format(
                CultureInfo.CurrentCulture,
                FrameworkMessages.AreEqualDifferentTypesFailMsg,
                userMessage,
                ReplaceNulls(expected),
                expected.GetType().FullName,
                ReplaceNulls(actual),
                actual.GetType().FullName)
            : expected is string expectedString && actual is string actualString
                ? FormatStringComparisonMessage(expectedString, actualString, userMessage)
                : string.Format(
                    CultureInfo.CurrentCulture,
                    FrameworkMessages.AreEqualFailMsg,
                    userMessage,
                    ReplaceNulls(expected),
                    ReplaceNulls(actual));
        ReportAssertFailed("Assert.AreEqual", finalMessage);
    }

    /// <summary>
    /// Tests whether the specified values are unequal and throws an exception
    /// if the two values are equal.
    /// The equality is computed using the default <see cref="EqualityComparer{T}"/>.
    /// </summary>
    /// <typeparam name="T">
    /// The type of values to compare.
    /// </typeparam>
    /// <param name="notExpected">
    /// The first value to compare. This is the value the test expects not
    /// to match <paramref name="actual"/>.
    /// </param>
    /// <param name="actual">
    /// The second value to compare. This is the value produced by the code under test.
    /// </param>
    /// <param name="message">
    /// The message to include in the exception when <paramref name="actual"/>
    /// is equal to <paramref name="notExpected"/>. The message is shown in
    /// test results.
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
    public static void AreNotEqual<T>(T? notExpected, T? actual, string? message = "", [CallerArgumentExpression(nameof(notExpected))] string notExpectedExpression = "", [CallerArgumentExpression(nameof(actual))] string actualExpression = "")
        => AreNotEqual(notExpected, actual, EqualityComparer<T>.Default, message, notExpectedExpression, actualExpression);

    /// <inheritdoc cref="AreNotEqual{T}(T, T, string, string, string)" />
#pragma warning disable IDE0060 // Remove unused parameter - https://github.com/dotnet/roslyn/issues/76578
    public static void AreNotEqual<T>(T? notExpected, T? actual, [InterpolatedStringHandlerArgument(nameof(notExpected), nameof(actual))] ref AssertAreNotEqualInterpolatedStringHandler<T> message, [CallerArgumentExpression(nameof(notExpected))] string notExpectedExpression = "", [CallerArgumentExpression(nameof(actual))] string actualExpression = "")
#pragma warning restore IDE0060 // Remove unused parameter
        => message.ComputeAssertion(notExpectedExpression, actualExpression);

    /// <inheritdoc cref="AreNotEqual{T}(T, T, string, string, string)" />
#pragma warning disable IDE0060 // Remove unused parameter - https://github.com/dotnet/roslyn/issues/76578
    public static void AreNotEqual<T>(T? notExpected, T? actual, IEqualityComparer<T> comparer, [InterpolatedStringHandlerArgument(nameof(notExpected), nameof(actual), nameof(comparer))] ref AssertAreNotEqualInterpolatedStringHandler<T> message, [CallerArgumentExpression(nameof(notExpected))] string notExpectedExpression = "", [CallerArgumentExpression(nameof(actual))] string actualExpression = "")
#pragma warning restore IDE0060 // Remove unused parameter
        => message.ComputeAssertion(notExpectedExpression, actualExpression);

    /// <summary>
    /// Tests whether the specified values are unequal and throws an exception
    /// if the two values are equal.
    /// The equality is computed using the provided <paramref name="comparer"/> parameter.
    /// </summary>
    /// <typeparam name="T">
    /// The type of values to compare.
    /// </typeparam>
    /// <param name="notExpected">
    /// The first value to compare. This is the value the test expects not
    /// to match <paramref name="actual"/>.
    /// </param>
    /// <param name="actual">
    /// The second value to compare. This is the value produced by the code under test.
    /// </param>
    /// <param name="comparer">
    /// The <see cref="IEqualityComparer{T}"/> implementation to use when comparing keys,
    /// or null to use the default <see cref="EqualityComparer{T}"/>.
    /// </param>
    /// <param name="message">
    /// The message to include in the exception when <paramref name="actual"/>
    /// is equal to <paramref name="notExpected"/>. The message is shown in
    /// test results.
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
    public static void AreNotEqual<T>(T? notExpected, T? actual, IEqualityComparer<T> comparer, string? message = "", [CallerArgumentExpression(nameof(notExpected))] string notExpectedExpression = "", [CallerArgumentExpression(nameof(actual))] string actualExpression = "")
    {
        if (!AreNotEqualFailing(notExpected, actual, comparer))
        {
            return;
        }

        string userMessage = BuildUserMessageForNotExpectedExpressionAndActualExpression(message, notExpectedExpression, actualExpression);
        ReportAssertAreNotEqualFailed(notExpected, actual, userMessage);
    }

    private static bool AreNotEqualFailing<T>(T? notExpected, T? actual, IEqualityComparer<T>? comparer)
        => (comparer ?? EqualityComparer<T>.Default).Equals(notExpected!, actual!);

    [DoesNotReturn]
    private static void ReportAssertAreNotEqualFailed(object? notExpected, object? actual, string userMessage)
    {
        string finalMessage = string.Format(
            CultureInfo.CurrentCulture,
            FrameworkMessages.AreNotEqualFailMsg,
            userMessage,
            ReplaceNulls(notExpected),
            ReplaceNulls(actual));
        ReportAssertFailed("Assert.AreNotEqual", finalMessage);
    }

#pragma warning restore RS0026 // Do not add multiple public overloads with optional parameters
#pragma warning restore RS0027 // API with optional parameter(s) should have the most parameters amongst its public overloads
}
