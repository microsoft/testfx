// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestTools.UnitTesting;

public sealed partial class Assert
{
    #region IsInRange

    /// <summary>
    /// Tests whether the specified value is within the expected range (inclusive).
    /// The range includes both the minimum and maximum values.
    /// </summary>
    /// <typeparam name="T">The type of the values to compare.</typeparam>
    /// <param name="minValue">The minimum value of the expected range (inclusive).</param>
    /// <param name="maxValue">The maximum value of the expected range (inclusive).</param>
    /// <param name="value">The value to test.</param>
    /// <param name="message">The message format to display when the assertion fails.</param>
    /// <param name="minValueExpression">
    /// The syntactic expression of minValue as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    /// <param name="maxValueExpression">
    /// The syntactic expression of maxValue as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    /// <param name="valueExpression">
    /// The syntactic expression of value as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    public static void IsInRange<T>(T minValue, T maxValue, T value, string? message = "", [CallerArgumentExpression(nameof(minValue))] string minValueExpression = "", [CallerArgumentExpression(nameof(maxValue))] string maxValueExpression = "", [CallerArgumentExpression(nameof(value))] string valueExpression = "")
        where T : struct, IComparable<T>
    {
        TelemetryCollector.TrackAssertionCall("Assert.IsInRange");
        if (maxValue.CompareTo(minValue) < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxValue), FrameworkMessages.IsInRangeMaxValueMustBeGreaterThanOrEqualMinValue);
        }

        if (value.CompareTo(minValue) < 0 || value.CompareTo(maxValue) > 0)
        {
            ReportAssertIsInRangeFailed(minValue, maxValue, value, message, minValueExpression, maxValueExpression, valueExpression);
        }
    }

    #endregion // IsInRange
    [DoesNotReturn]
    private static void ReportAssertIsInRangeFailed<T>(T minValue, T maxValue, T value, string? userMessage, string minValueExpression, string maxValueExpression, string valueExpression)
    {
        string minText = AssertionValueRenderer.RenderValue(minValue);
        string maxText = AssertionValueRenderer.RenderValue(maxValue);
        string actualText = AssertionValueRenderer.RenderValue(value);
        EvidenceBlock evidence = EvidenceBlock.Create()
            .AddLine("expected:", $"[{minText}, {maxText}]")
            .AddLine("actual:", actualText);
        StructuredAssertionMessage structured = new(FrameworkMessages.IsInRangeFailedSummary);
        structured.WithUserMessage(userMessage);
        structured.WithEvidence(evidence);
        structured.WithExpectedAndActual($"[{minText}, {maxText}]", actualText);
        // The middle "value" arg comes after minValue, maxValue — keep all three in the call-site for clarity.
        string? callSite = FormatIsInRangeCallSite(minValueExpression, maxValueExpression, valueExpression);
        structured.WithCallSiteExpression(callSite);
        ReportAssertFailed(structured);
    }

    private static string? FormatIsInRangeCallSite(string minValueExpression, string maxValueExpression, string valueExpression)
    {
        bool emptyMin = string.IsNullOrWhiteSpace(minValueExpression);
        bool emptyMax = string.IsNullOrWhiteSpace(maxValueExpression);
        bool emptyValue = string.IsNullOrWhiteSpace(valueExpression);
        if (emptyMin && emptyMax && emptyValue)
        {
            return null;
        }

        string minArg = emptyMin || IsMultiline(minValueExpression) ? "<minValue>" : minValueExpression;
        string maxArg = emptyMax || IsMultiline(maxValueExpression) ? "<maxValue>" : maxValueExpression;
        string valueArg = emptyValue || IsMultiline(valueExpression) ? "<value>" : valueExpression;
        return $"Assert.IsInRange({minArg}, {maxArg}, {valueArg})";
    }
}
