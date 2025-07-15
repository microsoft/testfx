// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestTools.UnitTesting;

#pragma warning disable RS0026 // Do not add multiple public overloads with optional parameters

public sealed partial class Assert
{
    #region EndsWith

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
    /// <exception cref="AssertFailedException">
    /// <paramref name="value"/> is null, or <paramref name="substring"/> is null,
    /// or <paramref name="value"/> does not end with <paramref name="substring"/>.
    /// </exception>
    public static void EndsWith([NotNull] string? substring, [NotNull] string? value, string message = "")
        => EndsWith(substring, value, StringComparison.Ordinal, message);

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
    /// <exception cref="AssertFailedException">
    /// <paramref name="value"/> is null, or <paramref name="substring"/> is null,
    /// or <paramref name="value"/> does not start with <paramref name="substring"/>.
    /// </exception>
    public static void EndsWith([NotNull] string? substring, [NotNull] string? value, StringComparison comparisonType, string message = "")
    {
        CheckParameterNotNull(value, "Assert.EndsWith", "value", string.Empty);
        CheckParameterNotNull(substring, "Assert.EndsWith", "substring", string.Empty);
        if (!value.EndsWith(substring, comparisonType))
        {
            string userMessage = BuildUserMessage(message);
            string finalMessage = string.Format(CultureInfo.CurrentCulture, FrameworkMessages.EndsWithFail, value, substring, userMessage);
            ThrowAssertFailed("Assert.EndsWith", finalMessage);
        }
    }

    #endregion // EndsWith

    #region DoesNotEndWith

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
    /// <exception cref="AssertFailedException">
    /// <paramref name="value"/> is null, or <paramref name="substring"/> is null,
    /// or <paramref name="value"/> ends with <paramref name="substring"/>.
    /// </exception>
    public static void DoesNotEndWith([NotNull] string? substring, [NotNull] string? value, string message = "")
        => DoesNotEndWith(substring, value, StringComparison.Ordinal, message);

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
    /// <exception cref="AssertFailedException">
    /// <paramref name="value"/> is null, or <paramref name="substring"/> is null,
    /// or <paramref name="value"/> ends with <paramref name="substring"/>.
    /// </exception>
    public static void DoesNotEndWith([NotNull] string? substring, [NotNull] string? value, StringComparison comparisonType, string message = "")
    {
        CheckParameterNotNull(value, "Assert.DoesNotEndWith", "value", string.Empty);
        CheckParameterNotNull(substring, "Assert.DoesNotEndWith", "substring", string.Empty);
        if (value.EndsWith(substring, comparisonType))
        {
            string userMessage = BuildUserMessage(message);
            string finalMessage = string.Format(CultureInfo.CurrentCulture, FrameworkMessages.DoesNotEndWithFail, value, substring, userMessage);
            ThrowAssertFailed("Assert.DoesNotEndWith", finalMessage);
        }
    }

    #endregion // DoesNotEndWith
}
