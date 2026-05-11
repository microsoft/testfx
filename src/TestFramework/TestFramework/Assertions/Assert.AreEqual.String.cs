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

    private static bool AreEqualFailing(string? expected, string? actual, bool ignoreCase, CultureInfo culture)
        => CompareInternal(expected, actual, ignoreCase, culture) != 0;

    [DoesNotReturn]
    private static void ReportAssertAreEqualFailed(string? expected, string? actual, bool ignoreCase, CultureInfo culture, string userMessage)
    {
        string finalMessage;

        // If the user requested to match case, and the difference between expected/actual is casing only, then we use a different message.
        if (!ignoreCase && CompareInternal(expected, actual, ignoreCase: true, culture) == 0)
        {
            finalMessage = string.Format(
                CultureInfo.CurrentCulture,
                FrameworkMessages.AreEqualCaseFailMsg,
                userMessage,
                ReplaceNulls(expected),
                ReplaceNulls(actual));
        }
        else
        {
            // Use enhanced string comparison for string-specific failures
            finalMessage = FormatStringComparisonMessage(expected, actual, userMessage);
        }

        ReportAssertFailed("Assert.AreEqual", finalMessage);
    }

    private static bool AreNotEqualFailing(string? notExpected, string? actual, bool ignoreCase, CultureInfo culture)
        => CompareInternal(notExpected, actual, ignoreCase, culture) == 0;

    /// <summary>
    /// Tests whether the specified strings are equal and throws an exception
    /// if they are not equal. The invariant culture is used for the comparison.
    /// </summary>
    /// <param name="expected">
    /// The first string to compare. This is the string the tests expects.
    /// </param>
    /// <param name="actual">
    /// The second string to compare. This is the string produced by the code under test.
    /// </param>
    /// <param name="ignoreCase">
    /// A Boolean indicating a case-sensitive or insensitive comparison. (true
    /// indicates a case-insensitive comparison.)
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
    /// Thrown if <paramref name="expected"/> is not equal to <paramref name="actual"/>.
    /// </exception>
    public static void AreEqual(string? expected, string? actual, bool ignoreCase, string? message = "", [CallerArgumentExpression(nameof(expected))] string expectedExpression = "", [CallerArgumentExpression(nameof(actual))] string actualExpression = "")
        => AreEqual(expected, actual, ignoreCase, CultureInfo.InvariantCulture, message, expectedExpression, actualExpression);

    /// <inheritdoc cref="AreEqual(string?, string?, bool, string, string, string)" />
#pragma warning disable IDE0060 // Remove unused parameter - https://github.com/dotnet/roslyn/issues/76578
    public static void AreEqual(string? expected, string? actual, bool ignoreCase, [InterpolatedStringHandlerArgument(nameof(expected), nameof(actual), nameof(ignoreCase))] ref AssertNonGenericAreEqualInterpolatedStringHandler message, [CallerArgumentExpression(nameof(expected))] string expectedExpression = "", [CallerArgumentExpression(nameof(actual))] string actualExpression = "")
#pragma warning restore IDE0060 // Remove unused parameter
        => message.ComputeAssertion(expectedExpression, actualExpression);

    /// <inheritdoc cref="AreEqual(string?, string?, bool, CultureInfo, string, string, string)" />
#pragma warning disable IDE0060 // Remove unused parameter - https://github.com/dotnet/roslyn/issues/76578
    public static void AreEqual(string? expected, string? actual, bool ignoreCase,
#pragma warning restore IDE0060 // Remove unused parameter
        CultureInfo culture, [InterpolatedStringHandlerArgument(nameof(expected), nameof(actual), nameof(ignoreCase), nameof(culture))] ref AssertNonGenericAreEqualInterpolatedStringHandler message, [CallerArgumentExpression(nameof(expected))] string expectedExpression = "", [CallerArgumentExpression(nameof(actual))] string actualExpression = "")
    {
        CheckParameterNotNull(culture, "Assert.AreEqual", nameof(culture));
        message.ComputeAssertion(expectedExpression, actualExpression);
    }

    /// <summary>
    /// Tests whether the specified strings are equal and throws an exception
    /// if they are not equal.
    /// </summary>
    /// <param name="expected">
    /// The first string to compare. This is the string the tests expects.
    /// </param>
    /// <param name="actual">
    /// The second string to compare. This is the string produced by the code under test.
    /// </param>
    /// <param name="ignoreCase">
    /// A Boolean indicating a case-sensitive or insensitive comparison. (true
    /// indicates a case-insensitive comparison.)
    /// </param>
    /// <param name="culture">
    /// A CultureInfo object that supplies culture-specific comparison information. If culture is null, the current culture is used.
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
    /// Thrown if <paramref name="expected"/> is not equal to <paramref name="actual"/>.
    /// </exception>
    public static void AreEqual(string? expected, string? actual, bool ignoreCase, CultureInfo culture, string? message = "", [CallerArgumentExpression(nameof(expected))] string expectedExpression = "", [CallerArgumentExpression(nameof(actual))] string actualExpression = "")
    {
        CheckParameterNotNull(culture, "Assert.AreEqual", nameof(culture));
        if (!AreEqualFailing(expected, actual, ignoreCase, culture))
        {
            return;
        }

        string userMessage = BuildUserMessageForExpectedExpressionAndActualExpression(message, expectedExpression, actualExpression);
        ReportAssertAreEqualFailed(expected, actual, ignoreCase, culture, userMessage);
    }

    /// <summary>
    /// Tests whether the specified strings are unequal and throws an exception
    /// if they are equal. The invariant culture is used for the comparison.
    /// </summary>
    /// <param name="notExpected">
    /// The first string to compare. This is the string the test expects not to
    /// match <paramref name="actual"/>.
    /// </param>
    /// <param name="actual">
    /// The second string to compare. This is the string produced by the code under test.
    /// </param>
    /// <param name="ignoreCase">
    /// A Boolean indicating a case-sensitive or insensitive comparison. (true
    /// indicates a case-insensitive comparison.)
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
    public static void AreNotEqual(string? notExpected, string? actual, bool ignoreCase, string? message = "", [CallerArgumentExpression(nameof(notExpected))] string notExpectedExpression = "", [CallerArgumentExpression(nameof(actual))] string actualExpression = "")
        => AreNotEqual(notExpected, actual, ignoreCase, CultureInfo.InvariantCulture, message, notExpectedExpression, actualExpression);

    /// <inheritdoc cref="AreNotEqual(string?, string?, bool, string, string, string)" />
#pragma warning disable IDE0060 // Remove unused parameter - https://github.com/dotnet/roslyn/issues/76578
    public static void AreNotEqual(string? notExpected, string? actual, bool ignoreCase, [InterpolatedStringHandlerArgument(nameof(notExpected), nameof(actual), nameof(ignoreCase))] ref AssertNonGenericAreNotEqualInterpolatedStringHandler message, [CallerArgumentExpression(nameof(notExpected))] string notExpectedExpression = "", [CallerArgumentExpression(nameof(actual))] string actualExpression = "")
#pragma warning restore IDE0060 // Remove unused parameter
        => message.ComputeAssertion(notExpectedExpression, actualExpression);

    /// <inheritdoc cref="AreNotEqual(string?, string?, bool, CultureInfo, string, string, string)" />
#pragma warning disable IDE0060 // Remove unused parameter - https://github.com/dotnet/roslyn/issues/76578
    public static void AreNotEqual(string? notExpected, string? actual, bool ignoreCase,
#pragma warning restore IDE0060 // Remove unused parameter
        CultureInfo culture, [InterpolatedStringHandlerArgument(nameof(notExpected), nameof(actual), nameof(ignoreCase), nameof(culture))] ref AssertNonGenericAreNotEqualInterpolatedStringHandler message, [CallerArgumentExpression(nameof(notExpected))] string notExpectedExpression = "", [CallerArgumentExpression(nameof(actual))] string actualExpression = "")
    {
        CheckParameterNotNull(culture, "Assert.AreNotEqual", nameof(culture));
        message.ComputeAssertion(notExpectedExpression, actualExpression);
    }

    /// <summary>
    /// Tests whether the specified strings are unequal and throws an exception
    /// if they are equal.
    /// </summary>
    /// <param name="notExpected">
    /// The first string to compare. This is the string the test expects not to
    /// match <paramref name="actual"/>.
    /// </param>
    /// <param name="actual">
    /// The second string to compare. This is the string produced by the code under test.
    /// </param>
    /// <param name="ignoreCase">
    /// A Boolean indicating a case-sensitive or insensitive comparison. (true
    /// indicates a case-insensitive comparison.)
    /// </param>
    /// <param name="culture">
    /// A CultureInfo object that supplies culture-specific comparison information. If culture is null, the current culture is used.
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
    public static void AreNotEqual(string? notExpected, string? actual, bool ignoreCase, CultureInfo culture, string? message = "", [CallerArgumentExpression(nameof(notExpected))] string notExpectedExpression = "", [CallerArgumentExpression(nameof(actual))] string actualExpression = "")
    {
        CheckParameterNotNull(culture, "Assert.AreNotEqual", "culture");
        if (!AreNotEqualFailing(notExpected, actual, ignoreCase, culture))
        {
            return;
        }

        string userMessage = BuildUserMessageForNotExpectedExpressionAndActualExpression(message, notExpectedExpression, actualExpression);
        ReportAssertAreNotEqualFailed(notExpected, actual, userMessage);
    }

#pragma warning restore RS0026 // Do not add multiple public overloads with optional parameters
#pragma warning restore RS0027 // API with optional parameter(s) should have the most parameters amongst its public overloads
}

internal static class StringPreviewHelper
{
    public static Tuple<string, string, int> CreateStringPreviews(string expected, string actual, int diffIndex, int fullPreviewLength)
    {
        int ellipsisLength = 3; // Length of the ellipsis "..."

        if (fullPreviewLength % 2 == 0)
        {
            // Being odd makes it easier to calculate the context length, and center the marker, this is not user customizable.
            throw new ArgumentException($"{nameof(fullPreviewLength)} must be odd, but it was even.");
        }

        // This is arbitrary number that is 2 times the size of the ellipsis,
        // plus 3 chars to make it easier to check the tests are correct when part of string is masked.
        // Preview length is not user customizable, just makes it harder to break the tests, and avoids few ifs we would need to write otherwise.
        if (fullPreviewLength < 9)
        {
            throw new ArgumentException($"{nameof(fullPreviewLength)} cannot be shorter than 9.");
        }

        // In case we want to instead count runes or text elements we can change it just here.
        int expectedLength = expected.Length;
        int actualLength = actual.Length;

        if (diffIndex < 0 || diffIndex > Math.Min(expectedLength, actualLength)) // Not -1 here because the difference can be right after the end of the shorter string.
        {
            throw new ArgumentOutOfRangeException(nameof(diffIndex), "diffIndex must be within the bounds of both strings.");
        }

        int contextLength = (fullPreviewLength - 1) / 2;

        // Diff index must point into the string, the start of the strings will always be shortened the same amount,
        // because otherwise the diff would happen at the beginning of the string.
        // So we just care about how far we are from the end of the string, so we can show the maximum amount of info to the user
        // when diff is really close to the end.
        string shorterString = expectedLength < actualLength ? expected : actual;
        string longerString = expectedLength < actualLength ? actual : expected;
        bool expectedIsShorter = expectedLength < actualLength;

        int shorterStringLength = shorterString.Length;
        int longerStringLength = longerString.Length;

        // End marker will point to the end of the shorter string, but the end of the longer string will be replaced by ... when it reaches the end of the preview.
        // Make sure we don't point at the dots. To do this we need to make sure the strings are cut at the beginning, rather than preferring the maximum context shown.
        //
        // Marker needs to point where ellipsis would be when we shorten the longer string.
        bool markerPointsAtTheEnd = shorterStringLength - diffIndex <= ellipsisLength;
        // Strings need to have different lengths, for same length strings we don't add ellipsis.
        bool stringsHaveDifferentLength = longerStringLength > shorterStringLength;
        // Shorter string needs to be long enough to fill the preview window to the point where ellipsis shows up (last 3 chars).
        bool shorterStringIsLongEnoughToFillPreviewWindow = shorterStringLength >= fullPreviewLength - ellipsisLength;
        bool markerPointsAtEllipsis = markerPointsAtTheEnd && stringsHaveDifferentLength && shorterStringIsLongEnoughToFillPreviewWindow;
        int ellipsisSpaceOrZero = markerPointsAtEllipsis ? ellipsisLength + 2 : 0;

        // Find the end of the string that we will show, either the end of the shorter string, or the end of the preview window.
        int endOfString = Math.Min(diffIndex + contextLength, shorterStringLength);

        // Then calculate the start of the preview from that. This makes sure that if diff is close end of the string we show as much as we can.
        int start = endOfString - fullPreviewLength + ellipsisSpaceOrZero;

        // If the string is shorter than the preview, start cutting from 0, otherwise start cutting from the calculated start.
        int cutStart = Math.Max(0, start);
        // From here we need to handle longer and shorter string separately, because one of the can be shorter,
        // and we want to show the maximum we can that fits in the preview window.
        int cutEndShort = Math.Min(cutStart + fullPreviewLength, shorterStringLength);
        int cutEndLong = Math.Min(cutStart + fullPreviewLength, longerStringLength);

        string shorterStringPreview = shorterString.Substring(cutStart, cutEndShort - cutStart);
        string longerStringPreview = longerString.Substring(cutStart, cutEndLong - cutStart);

        // We cut something from the start of the string, so we need to add ellipsis there.
        // We know if one string is cut then both must be cut, otherwise the diff would be at the beginning of the string.
        if (cutStart > 0)
        {
            shorterStringPreview = EllipsisStart(shorterStringPreview);
            longerStringPreview = EllipsisStart(longerStringPreview);
        }

        // We cut something from the end of the string, so we need to add ellipsis there.
        // We don't know if both strings are cut, so we need to check them separately.
        if (cutEndShort < shorterStringLength)
        {
            shorterStringPreview = EllipsisEnd(shorterStringPreview);
        }

        // We cut something from the end of the string, so we need to add ellipsis there.
        if (cutEndLong < longerStringLength)
        {
            longerStringPreview = EllipsisEnd(longerStringPreview);
        }

        string escapedShorterStringPreview = MakeControlCharactersVisible(shorterStringPreview);
        string escapedLongerStringPreview = MakeControlCharactersVisible(longerStringPreview);

        return new Tuple<string, string, int>(
            expectedIsShorter ? escapedShorterStringPreview : escapedLongerStringPreview,
            expectedIsShorter ? escapedLongerStringPreview : escapedShorterStringPreview,
            diffIndex - cutStart);
    }

    private static string EllipsisEnd(string text)
        => $"{text.Substring(0, text.Length - 3)}...";

    private static string EllipsisStart(string text)
        => $"...{text.Substring(3)}";

    private static string MakeControlCharactersVisible(string text)
    {
        var stringBuilder = new StringBuilder(text.Length);
        foreach (char ch in text)
        {
            if (char.IsControl(ch))
            {
                stringBuilder.Append((char)(0x2400 + ch));
            }
            else
            {
                stringBuilder.Append(ch);
            }
        }

        return stringBuilder.ToString();
    }
}
