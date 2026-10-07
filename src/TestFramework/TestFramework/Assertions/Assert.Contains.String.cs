// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestTools.UnitTesting;

#pragma warning disable RS0026 // Do not add multiple public overloads with optional parameters
#pragma warning disable RS0027 // API with optional parameter(s) should have the most parameters amongst its public overloads
public sealed partial class Assert
{
    #region Contains

    /// <summary>
    /// Tests whether the specified string contains the specified substring
    /// and throws an exception if the substring does not occur within the
    /// test string.
    /// </summary>
    /// <param name="substring">
    /// The string expected to occur within <paramref name="value"/>.
    /// </param>
    /// <param name="value">
    /// The string that is expected to contain <paramref name="substring"/>.
    /// </param>
    /// <param name="message">
    /// The message to include in the exception when <paramref name="substring"/>
    /// is not in <paramref name="value"/>. The message is shown in
    /// test results.
    /// </param>
    /// <param name="substringExpression">
    /// The syntactic expression of substring as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    /// <param name="valueExpression">
    /// The syntactic expression of value as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// <paramref name="value"/> is null, or <paramref name="substring"/> is null,
    /// or <paramref name="value"/> does not contain <paramref name="substring"/>.
    /// </exception>
    public static void Contains(string substring, string value, string? message = "", [CallerArgumentExpression(nameof(substring))] string substringExpression = "", [CallerArgumentExpression(nameof(value))] string valueExpression = "")
        => Contains(substring, value, StringComparison.Ordinal, message, substringExpression, valueExpression);

    /// <summary>
    /// Tests whether the specified string contains the specified substring
    /// and throws an exception if the substring does not occur within the
    /// test string.
    /// </summary>
    /// <param name="substring">
    /// The string expected to occur within <paramref name="value"/>.
    /// </param>
    /// <param name="value">
    /// The string that is expected to contain <paramref name="substring"/>.
    /// </param>
    /// <param name="comparisonType">
    /// The comparison method to compare strings <paramref name="comparisonType"/>.
    /// </param>
    /// <param name="message">
    /// The message to include in the exception when <paramref name="substring"/>
    /// is not in <paramref name="value"/>. The message is shown in
    /// test results.
    /// </param>
    /// <param name="substringExpression">
    /// The syntactic expression of substring as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    /// <param name="valueExpression">
    /// The syntactic expression of value as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// <paramref name="value"/> is null, or <paramref name="substring"/> is null,
    /// or <paramref name="value"/> does not contain <paramref name="substring"/>.
    /// </exception>
    public static void Contains(string substring, string value, StringComparison comparisonType, string? message = "", [CallerArgumentExpression(nameof(substring))] string substringExpression = "", [CallerArgumentExpression(nameof(value))] string valueExpression = "")
    {
        TelemetryCollector.TrackAssertionCall("Assert.Contains");
        ContainsCore(substring, value, comparisonType, "Assert.Contains", message, substringExpression, valueExpression, shouldContain: true);
    }

    #endregion // Contains
    [DoesNotReturn]
    private static void ReportAssertContainsItemFailed(object? expected, string? userMessage, string expectedExpression, string collectionExpression, object? comparer = null)
    {
        string expectedText = AssertionValueRenderer.RenderValue(expected);
        EvidenceBlock evidence = EvidenceBlock.Create().AddLine("expected:", expectedText);
        if (comparer is not null)
        {
            evidence.AddLine("comparer:", comparer.GetType().Name);
        }

        StructuredAssertionMessage structured = new(FrameworkMessages.ContainsItemFailedSummary);
        structured.WithUserMessage(userMessage);
        structured.WithEvidence(evidence);
        structured.WithExpectedAndActual(expectedText, null);
        structured.WithCallSiteExpression(FormatCallSiteExpression("Assert.Contains", expectedExpression, collectionExpression, "<expected>", "<collection>"));
        ReportAssertFailed(structured);
    }

    [DoesNotReturn]
    private static void ReportAssertContainsPredicateFailed(string? userMessage, string predicateExpression, string collectionExpression)
    {
        StructuredAssertionMessage structured = new(FrameworkMessages.ContainsPredicateFailedSummary);
        structured.WithUserMessage(userMessage);
        structured.WithCallSiteExpression(FormatCallSiteExpression("Assert.Contains", predicateExpression, collectionExpression, "<predicate>", "<collection>"));
        ReportAssertFailed(structured);
    }

    [DoesNotReturn]
    private static void ReportAssertContainsSubstringFailed(string substring, string value, StringComparison comparisonType, string? userMessage, string substringExpression, string valueExpression)
    {
        string expectedText = AssertionValueRenderer.RenderValue(substring);
        string actualText = AssertionValueRenderer.RenderValue(value);
        EvidenceBlock evidence = EvidenceBlock.Create()
            .AddLine("expected substring:", expectedText)
            .AddLine("actual:", actualText)
            .AddLine("comparison:", comparisonType.ToString());
        StructuredAssertionMessage structured = new(FrameworkMessages.ContainsSubstringFailedSummary);
        structured.WithUserMessage(userMessage);
        structured.WithEvidence(evidence);
        structured.WithExpectedAndActual(expectedText, actualText);
        structured.WithCallSiteExpression(FormatCallSiteExpression("Assert.Contains", substringExpression, valueExpression, "<substring>", "<value>"));
        ReportAssertFailed(structured);
    }
}
