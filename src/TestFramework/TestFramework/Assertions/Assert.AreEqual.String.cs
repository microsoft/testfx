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
    private static void ReportAssertAreEqualFailed(string? expected, string? actual, bool ignoreCase, CultureInfo culture, bool cultureExplicit, string? message, string expectedExpression, string actualExpression)
    {
        string expectedRendered = AssertionValueRenderer.RenderValue(expected);
        string actualRendered = AssertionValueRenderer.RenderValue(actual);

        StructuredAssertionMessage structured = new(FrameworkMessages.AreEqualStringsFailedSummary);
        if (!ignoreCase && expected is not null && actual is not null && CompareInternal(expected, actual, ignoreCase: true, culture) == 0)
        {
            structured.WithAdditionalSummaryLine(FrameworkMessages.AreEqualStringsCaseOnlyDifferenceMsg);
        }

        if (expected is not null && actual is not null)
        {
            int diffIndex = FindFirstStringDifference(expected, actual, ignoreCase, culture);
            if (diffIndex >= 0)
            {
                AppendStringDiffSummary(structured, expected, actual, diffIndex);
            }
        }

        structured.WithUserMessage(message);
        structured.WithEvidence(CreateStringComparisonEvidence("expected:", expectedRendered, actualRendered, ignoreCase, culture, cultureExplicit));
        structured.WithExpectedAndActual(expectedRendered, actualRendered);
        structured.WithCallSiteExpression(FormatCallSiteExpression("Assert.AreEqual", expectedExpression, actualExpression, "<expected>", "<actual>"));

        ReportAssertFailed(structured);
    }

    [DoesNotReturn]
    private static void ReportAssertAreNotEqualFailed(string? notExpected, string? actual, bool ignoreCase, CultureInfo culture, bool cultureExplicit, string? message, string notExpectedExpression, string actualExpression)
    {
        string notExpectedRendered = AssertionValueRenderer.RenderValue(notExpected);
        string actualRendered = AssertionValueRenderer.RenderValue(actual);

        StructuredAssertionMessage structured = new(ignoreCase
            ? FrameworkMessages.AreNotEqualStringsCaseInsensitiveFailedSummary
            : FrameworkMessages.AreNotEqualStringsFailedSummary);
        structured.WithUserMessage(message);
        structured.WithEvidence(CreateStringComparisonEvidence("not expected:", notExpectedRendered, actualRendered, ignoreCase, culture, cultureExplicit));
        structured.WithExpectedAndActual($"not {notExpectedRendered}", actualRendered);
        structured.WithCallSiteExpression(FormatCallSiteExpression("Assert.AreNotEqual", notExpectedExpression, actualExpression, "<notExpected>", "<actual>"));

        ReportAssertFailed(structured);
    }

    private static EvidenceBlock CreateStringComparisonEvidence(string firstLabel, string firstValue, string actualValue, bool ignoreCase, CultureInfo culture, bool cultureExplicit)
    {
        EvidenceBlock evidence = EvidenceBlock.Create()
            .AddLine(firstLabel, firstValue)
            .AddLine("actual:", actualValue);

        if (ignoreCase)
        {
            evidence.AddLine("ignore case:", "true");
        }

        // Track whether the culture overload was used so explicit CultureInfo.InvariantCulture remains self-describing as culture: "".
        if (cultureExplicit)
        {
            evidence.AddLine("culture:", culture.Name);
        }

        return evidence;
    }

    private static bool AreNotEqualFailing(string? notExpected, string? actual, bool ignoreCase, CultureInfo culture)
        => CompareInternal(notExpected, actual, ignoreCase, culture) == 0;

    private static int FindFirstStringDifference(string expected, string actual, bool ignoreCase, CultureInfo culture)
    {
        if (!ignoreCase && culture.Equals(CultureInfo.InvariantCulture))
        {
            return FindFirstStringDifference(expected, actual);
        }

        int expectedIndex = 0;
        int actualIndex = 0;

        while (expectedIndex < expected.Length && actualIndex < actual.Length)
        {
            if (CompareInternal(expected[expectedIndex].ToString(), actual[actualIndex].ToString(), ignoreCase, culture) == 0)
            {
                expectedIndex++;
                actualIndex++;
                continue;
            }

            if (TryAdvanceMatchingWindow(expected, actual, ref expectedIndex, ref actualIndex, ignoreCase, culture))
            {
                continue;
            }

            return Math.Min(expectedIndex, actualIndex);
        }

        return expectedIndex != expected.Length || actualIndex != actual.Length
            ? Math.Min(expectedIndex, actualIndex)
            : -1;
    }

    private static bool TryAdvanceMatchingWindow(string expected, string actual, ref int expectedIndex, ref int actualIndex, bool ignoreCase, CultureInfo culture)
    {
        const int MaxWindowLength = 3;

        for (int expectedWindow = 1; expectedWindow <= MaxWindowLength && expectedIndex + expectedWindow <= expected.Length; expectedWindow++)
        {
            for (int actualWindow = 1; actualWindow <= MaxWindowLength && actualIndex + actualWindow <= actual.Length; actualWindow++)
            {
                if (expectedWindow == 1 && actualWindow == 1)
                {
                    continue;
                }

                if (CompareInternal(expected.Substring(expectedIndex, expectedWindow), actual.Substring(actualIndex, actualWindow), ignoreCase, culture) == 0)
                {
                    expectedIndex += expectedWindow;
                    actualIndex += actualWindow;
                    return true;
                }
            }
        }

        return false;
    }

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
    {
        TelemetryCollector.TrackAssertionCall("Assert.AreEqual");

        if (!AreEqualFailing(expected, actual, ignoreCase, CultureInfo.InvariantCulture))
        {
            return;
        }

        ReportAssertAreEqualFailed(expected, actual, ignoreCase, CultureInfo.InvariantCulture, cultureExplicit: false, message, expectedExpression, actualExpression);
    }

    /// <inheritdoc cref="AreEqual(string?, string?, bool, string, string, string)" />
#pragma warning disable IDE0060 // Remove unused parameter - https://github.com/dotnet/roslyn/issues/76578
    public static void AreEqual(string? expected, string? actual, bool ignoreCase, [InterpolatedStringHandlerArgument(nameof(expected), nameof(actual), nameof(ignoreCase))] ref AssertNonGenericAreEqualInterpolatedStringHandler message, [CallerArgumentExpression(nameof(expected))] string expectedExpression = "", [CallerArgumentExpression(nameof(actual))] string actualExpression = "")
#pragma warning restore IDE0060 // Remove unused parameter
    {
        TelemetryCollector.TrackAssertionCall("Assert.AreEqual");
        message.ComputeAssertion(expectedExpression, actualExpression);
    }

    /// <inheritdoc cref="AreEqual(string?, string?, bool, CultureInfo, string, string, string)" />
#pragma warning disable IDE0060 // Remove unused parameter - https://github.com/dotnet/roslyn/issues/76578
    public static void AreEqual(string? expected, string? actual, bool ignoreCase,
#pragma warning restore IDE0060 // Remove unused parameter
        CultureInfo culture, [InterpolatedStringHandlerArgument(nameof(expected), nameof(actual), nameof(ignoreCase), nameof(culture))] ref AssertNonGenericAreEqualInterpolatedStringHandler message, [CallerArgumentExpression(nameof(expected))] string expectedExpression = "", [CallerArgumentExpression(nameof(actual))] string actualExpression = "")
    {
        TelemetryCollector.TrackAssertionCall("Assert.AreEqual");
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
        TelemetryCollector.TrackAssertionCall("Assert.AreEqual");

        CheckParameterNotNull(culture, "Assert.AreEqual", nameof(culture));
        if (!AreEqualFailing(expected, actual, ignoreCase, culture))
        {
            return;
        }

        ReportAssertAreEqualFailed(expected, actual, ignoreCase, culture, cultureExplicit: true, message, expectedExpression, actualExpression);
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
    {
        TelemetryCollector.TrackAssertionCall("Assert.AreNotEqual");

        if (!AreNotEqualFailing(notExpected, actual, ignoreCase, CultureInfo.InvariantCulture))
        {
            return;
        }

        ReportAssertAreNotEqualFailed(notExpected, actual, ignoreCase, CultureInfo.InvariantCulture, cultureExplicit: false, message, notExpectedExpression, actualExpression);
    }

    /// <inheritdoc cref="AreNotEqual(string?, string?, bool, string, string, string)" />
#pragma warning disable IDE0060 // Remove unused parameter - https://github.com/dotnet/roslyn/issues/76578
    public static void AreNotEqual(string? notExpected, string? actual, bool ignoreCase, [InterpolatedStringHandlerArgument(nameof(notExpected), nameof(actual), nameof(ignoreCase))] ref AssertNonGenericAreNotEqualInterpolatedStringHandler message, [CallerArgumentExpression(nameof(notExpected))] string notExpectedExpression = "", [CallerArgumentExpression(nameof(actual))] string actualExpression = "")
#pragma warning restore IDE0060 // Remove unused parameter
    {
        TelemetryCollector.TrackAssertionCall("Assert.AreNotEqual");
        message.ComputeAssertion(notExpectedExpression, actualExpression);
    }

    /// <inheritdoc cref="AreNotEqual(string?, string?, bool, CultureInfo, string, string, string)" />
#pragma warning disable IDE0060 // Remove unused parameter - https://github.com/dotnet/roslyn/issues/76578
    public static void AreNotEqual(string? notExpected, string? actual, bool ignoreCase,
#pragma warning restore IDE0060 // Remove unused parameter
        CultureInfo culture, [InterpolatedStringHandlerArgument(nameof(notExpected), nameof(actual), nameof(ignoreCase), nameof(culture))] ref AssertNonGenericAreNotEqualInterpolatedStringHandler message, [CallerArgumentExpression(nameof(notExpected))] string notExpectedExpression = "", [CallerArgumentExpression(nameof(actual))] string actualExpression = "")
    {
        TelemetryCollector.TrackAssertionCall("Assert.AreNotEqual");
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
        TelemetryCollector.TrackAssertionCall("Assert.AreNotEqual");

        CheckParameterNotNull(culture, "Assert.AreNotEqual", nameof(culture));
        if (!AreNotEqualFailing(notExpected, actual, ignoreCase, culture))
        {
            return;
        }

        ReportAssertAreNotEqualFailed(notExpected, actual, ignoreCase, culture, cultureExplicit: true, message, notExpectedExpression, actualExpression);
    }

#pragma warning restore RS0026 // Do not add multiple public overloads with optional parameters
#pragma warning restore RS0027 // API with optional parameter(s) should have the most parameters amongst its public overloads
}
