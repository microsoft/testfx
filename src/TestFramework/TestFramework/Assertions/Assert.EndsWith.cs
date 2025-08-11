// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestTools.UnitTesting;

#pragma warning disable RS0026 // Do not add multiple public overloads with optional parameters
#pragma warning disable RS0027 // API with optional parameter(s) should have the most parameters amongst its public overloads

public sealed partial class Assert
{
    /// <summary>
    /// Tests whether the specified string ends with the specified substring
    /// and throws an exception if the test string does not end with the
    /// substring.
    /// </summary>
    /// <param name="substring">
    /// The string expected to be a suffix of <paramref name="value"/>.
    /// </param>
    /// <param name="value">
    /// The string that is expected to end with <paramref name="substring"/>.
    /// </param>
    /// <param name="message">
    /// The message to include in the exception when <paramref name="value"/>
    /// does not end with <paramref name="substring"/>. The message is
    /// shown in test results.
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
    /// or <paramref name="value"/> does not start with <paramref name="substring"/>.
    /// </exception>
    public static void EndsWith([NotNull] string? substring, [NotNull] string? value, string message = "", [CallerArgumentExpression(nameof(substring))] string substringExpression = "", [CallerArgumentExpression(nameof(value))] string valueExpression = "")
        => EndsWith(substring, value, StringComparison.Ordinal, message, substringExpression, valueExpression);

    /// <summary>
    /// Tests whether the specified string ends with the specified substring
    /// and throws an exception if the test string does not end with the
    /// substring.
    /// </summary>
    /// <param name="substring">
    /// The string expected to be a suffix of <paramref name="value"/>.
    /// </param>
    /// <param name="value">
    /// The string that is expected to end with <paramref name="substring"/>.
    /// </param>
    /// <param name="comparisonType">
    /// The comparison method to compare strings <paramref name="comparisonType"/>.
    /// </param>
    /// <param name="message">
    /// The message to include in the exception when <paramref name="value"/>
    /// does not end with <paramref name="substring"/>. The message is
    /// shown in test results.
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
    /// or <paramref name="value"/> does not start with <paramref name="substring"/>.
    /// </exception>
    public static void EndsWith([NotNull] string? substring, [NotNull] string? value, StringComparison comparisonType, string message = "", [CallerArgumentExpression(nameof(substring))] string substringExpression = "", [CallerArgumentExpression(nameof(value))] string valueExpression = "")
    {
        CheckParameterNotNull(value, "Assert.EndsWith", "value", string.Empty);
        CheckParameterNotNull(substring, "Assert.EndsWith", "substring", string.Empty);
        if (!value.EndsWith(substring, comparisonType))
        {
            string userMessage = BuildUserMessageForSubstringExpressionAndValueExpression(message, substringExpression, valueExpression);
            string finalMessage = string.Format(CultureInfo.CurrentCulture, FrameworkMessages.EndsWithFail, value, substring, userMessage);
            ThrowAssertFailed("Assert.EndsWith", finalMessage);
        }
    }

    /// <summary>
    /// Tests whether the specified string does not end with the specified substring
    /// and throws an exception if the test string does not end with the
    /// substring.
    /// </summary>
    /// <param name="substring">
    /// The string expected not to be a suffix of <paramref name="value"/>.
    /// </param>
    /// <param name="value">
    /// The string that is expected not to end with <paramref name="substring"/>.
    /// </param>
    /// <param name="message">
    /// The message to include in the exception when <paramref name="value"/>
    /// ends with <paramref name="substring"/>. The message is
    /// shown in test results.
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
    /// or <paramref name="value"/> ends with <paramref name="substring"/>.
    /// </exception>
    public static void DoesNotEndWith([NotNull] string? substring, [NotNull] string? value, string message = "", [CallerArgumentExpression(nameof(substring))] string substringExpression = "", [CallerArgumentExpression(nameof(value))] string valueExpression = "")
        => DoesNotEndWith(substring, value, StringComparison.Ordinal, message, substringExpression, valueExpression);

    /// <summary>
    /// Tests whether the specified string does not end with the specified substring
    /// and throws an exception if the test string does not end with the
    /// substring.
    /// </summary>
    /// <param name="substring">
    /// The string expected not to be a suffix of <paramref name="value"/>.
    /// </param>
    /// <param name="value">
    /// The string that is expected not to end with <paramref name="substring"/>.
    /// </param>
    /// <param name="comparisonType">
    /// The comparison method to compare strings <paramref name="comparisonType"/>.
    /// </param>
    /// <param name="message">
    /// The message to include in the exception when <paramref name="value"/>
    /// ends with <paramref name="substring"/>. The message is
    /// shown in test results.
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
    /// or <paramref name="value"/> ends with <paramref name="substring"/>.
    /// </exception>
    public static void DoesNotEndWith([NotNull] string? substring, [NotNull] string? value, StringComparison comparisonType, string message = "", [CallerArgumentExpression(nameof(substring))] string substringExpression = "", [CallerArgumentExpression(nameof(value))] string valueExpression = "")
    {
        CheckParameterNotNull(value, "Assert.DoesNotEndWith", "value", string.Empty);
        CheckParameterNotNull(substring, "Assert.DoesNotEndWith", "substring", string.Empty);
        if (value.EndsWith(substring, comparisonType))
        {
            string userMessage = BuildUserMessageForSubstringExpressionAndValueExpression(message, substringExpression, valueExpression);
            string finalMessage = string.Format(CultureInfo.CurrentCulture, FrameworkMessages.DoesNotEndWithFail, value, substring, userMessage);
            ThrowAssertFailed("Assert.DoesNotEndWith", finalMessage);
        }
    }
}
