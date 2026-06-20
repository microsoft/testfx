// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestTools.UnitTesting;

#pragma warning disable RS0026 // Do not add multiple public overloads with optional parameters
#pragma warning disable RS0027 // API with optional parameter(s) should have the most parameters amongst its public overloads

public sealed partial class Assert
{
    /// <summary>
    /// Tests whether the specified string begins with the specified prefix
    /// and throws an exception if the test string does not start with the
    /// prefix.
    /// </summary>
    /// <param name="expectedPrefix">
    /// The string expected to be a prefix of <paramref name="value"/>.
    /// </param>
    /// <param name="value">
    /// The string that is expected to begin with <paramref name="expectedPrefix"/>.
    /// </param>
    /// <param name="message">
    /// The message to include in the exception when <paramref name="value"/>
    /// does not begin with <paramref name="expectedPrefix"/>. The message is
    /// shown in test results.
    /// </param>
    /// <param name="expectedPrefixExpression">
    /// The syntactic expression of expectedPrefix as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    /// <param name="valueExpression">
    /// The syntactic expression of value as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// <paramref name="value"/> is null, or <paramref name="expectedPrefix"/> is null,
    /// or <paramref name="value"/> does not start with <paramref name="expectedPrefix"/>.
    /// </exception>
    public static void StartsWith([NotNull] string? expectedPrefix, [NotNull] string? value, string? message = "", [CallerArgumentExpression(nameof(expectedPrefix))] string expectedPrefixExpression = "", [CallerArgumentExpression(nameof(value))] string valueExpression = "")
        => StartsWith(expectedPrefix, value, StringComparison.Ordinal, message, expectedPrefixExpression, valueExpression);

    /// <summary>
    /// Tests whether the specified string begins with the specified prefix
    /// and throws an exception if the test string does not start with the
    /// prefix.
    /// </summary>
    /// <param name="expectedPrefix">
    /// The string expected to be a prefix of <paramref name="value"/>.
    /// </param>
    /// <param name="value">
    /// The string that is expected to begin with <paramref name="expectedPrefix"/>.
    /// </param>
    /// <param name="comparisonType">
    /// The comparison method to compare strings <paramref name="comparisonType"/>.
    /// </param>
    /// <param name="message">
    /// The message to include in the exception when <paramref name="value"/>
    /// does not begin with <paramref name="expectedPrefix"/>. The message is
    /// shown in test results.
    /// </param>
    /// <param name="expectedPrefixExpression">
    /// The syntactic expression of expectedPrefix as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    /// <param name="valueExpression">
    /// The syntactic expression of value as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// <paramref name="value"/> is null, or <paramref name="expectedPrefix"/> is null,
    /// or <paramref name="value"/> does not start with <paramref name="expectedPrefix"/>.
    /// </exception>
    public static void StartsWith([NotNull] string? expectedPrefix, [NotNull] string? value, StringComparison comparisonType, string? message = "", [CallerArgumentExpression(nameof(expectedPrefix))] string expectedPrefixExpression = "", [CallerArgumentExpression(nameof(value))] string valueExpression = "")
        => StartsOrEndsWithCore(
            expectedPrefix,
            value,
            comparisonType,
            "Assert.StartsWith",
            "expectedPrefix",
            message,
            expectedPrefixExpression,
            valueExpression,
            shouldMatch: true,
            static (candidate, affix, comparison) => candidate.StartsWith(affix, comparison),
            ReportAssertStartsWithFailed);

    [DoesNotReturn]
    private static void ReportAssertStartsWithFailed(string expectedPrefix, string value, StringComparison comparisonType, string? userMessage, string expectedPrefixExpression, string valueExpression)
    {
        string expectedText = AssertionValueRenderer.RenderValue(expectedPrefix);
        string actualText = AssertionValueRenderer.RenderValue(value);
        EvidenceBlock evidence = EvidenceBlock.Create()
            .AddLine("expected prefix:", expectedText)
            .AddLine("actual:", actualText)
            .AddLine("comparison:", comparisonType.ToString());

        StructuredAssertionMessage structured = new(FrameworkMessages.StartsWithFailedSummary);
        structured.WithUserMessage(userMessage);
        structured.WithEvidence(evidence);
        structured.WithExpectedAndActual(expectedText, actualText);
        structured.WithCallSiteExpression(FormatCallSiteExpression("Assert.StartsWith", expectedPrefixExpression, valueExpression, "<expectedPrefix>", "<value>"));

        ReportAssertFailed(structured);
    }

    /// <summary>
    /// Tests whether the specified string does not begin with the specified unexpected prefix
    /// and throws an exception if the test string does start with the prefix.
    /// </summary>
    /// <param name="notExpectedPrefix">
    /// The string not expected to be a prefix of <paramref name="value"/>.
    /// </param>
    /// <param name="value">
    /// The string that is not expected to begin with <paramref name="notExpectedPrefix"/>.
    /// </param>
    /// <param name="message">
    /// The message to include in the exception when <paramref name="value"/>
    /// begins with <paramref name="notExpectedPrefix"/>. The message is
    /// shown in test results.
    /// </param>
    /// <param name="notExpectedPrefixExpression">
    /// The syntactic expression of notExpectedPrefix as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    /// <param name="valueExpression">
    /// The syntactic expression of value as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// <paramref name="value"/> is null, or <paramref name="notExpectedPrefix"/> is null,
    /// or <paramref name="value"/> does not start with <paramref name="notExpectedPrefix"/>.
    /// </exception>
    public static void DoesNotStartWith([NotNull] string? notExpectedPrefix, [NotNull] string? value, string? message = "", [CallerArgumentExpression(nameof(notExpectedPrefix))] string notExpectedPrefixExpression = "", [CallerArgumentExpression(nameof(value))] string valueExpression = "")
        => DoesNotStartWith(notExpectedPrefix, value, StringComparison.Ordinal, message, notExpectedPrefixExpression, valueExpression);

    /// <summary>
    /// Tests whether the specified string does not begin with the specified unexpected prefix
    /// and throws an exception if the test string does start with the prefix.
    /// </summary>
    /// <param name="notExpectedPrefix">
    /// The string not expected to be a prefix of <paramref name="value"/>.
    /// </param>
    /// <param name="value">
    /// The string that is not expected to begin with <paramref name="notExpectedPrefix"/>.
    /// </param>
    /// <param name="comparisonType">
    /// The comparison method to compare strings <paramref name="comparisonType"/>.
    /// </param>
    /// <param name="message">
    /// The message to include in the exception when <paramref name="value"/>
    /// begins with <paramref name="notExpectedPrefix"/>. The message is
    /// shown in test results.
    /// </param>
    /// <param name="notExpectedPrefixExpression">
    /// The syntactic expression of notExpectedPrefix as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    /// <param name="valueExpression">
    /// The syntactic expression of value as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// <paramref name="value"/> is null, or <paramref name="notExpectedPrefix"/> is null,
    /// or <paramref name="value"/> does not start with <paramref name="notExpectedPrefix"/>.
    /// </exception>
    public static void DoesNotStartWith([NotNull] string? notExpectedPrefix, [NotNull] string? value, StringComparison comparisonType, string? message = "", [CallerArgumentExpression(nameof(notExpectedPrefix))] string notExpectedPrefixExpression = "", [CallerArgumentExpression(nameof(value))] string valueExpression = "")
        => StartsOrEndsWithCore(
            notExpectedPrefix,
            value,
            comparisonType,
            "Assert.DoesNotStartWith",
            "notExpectedPrefix",
            message,
            notExpectedPrefixExpression,
            valueExpression,
            shouldMatch: false,
            static (candidate, affix, comparison) => candidate.StartsWith(affix, comparison),
            ReportAssertDoesNotStartWithFailed);

    [DoesNotReturn]
    private static void ReportAssertDoesNotStartWithFailed(string notExpectedPrefix, string value, StringComparison comparisonType, string? userMessage, string notExpectedPrefixExpression, string valueExpression)
    {
        string notExpectedText = AssertionValueRenderer.RenderValue(notExpectedPrefix);
        string actualText = AssertionValueRenderer.RenderValue(value);
        EvidenceBlock evidence = EvidenceBlock.Create()
            .AddLine("unexpected prefix:", notExpectedText)
            .AddLine("actual:", actualText)
            .AddLine("comparison:", comparisonType.ToString());

        StructuredAssertionMessage structured = new(FrameworkMessages.DoesNotStartWithFailedSummary);
        structured.WithUserMessage(userMessage);
        structured.WithEvidence(evidence);
        structured.WithExpectedAndActual(notExpectedText, actualText);
        structured.WithCallSiteExpression(FormatCallSiteExpression("Assert.DoesNotStartWith", notExpectedPrefixExpression, valueExpression, "<notExpectedPrefix>", "<value>"));

        ReportAssertFailed(structured);
    }
}
