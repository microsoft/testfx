// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestTools.UnitTesting;

#pragma warning disable RS0026 // Do not add multiple public overloads with optional parameters

public sealed partial class Assert
{
    #region StartsWith

    /// <summary>
    /// Tests whether the specified string begins with the specified substring
    /// and throws an exception if the test string does not start with the
    /// substring.
    /// </summary>
    /// <param name="substring">
    /// The string expected to be a prefix of <paramref name="value"/>.
    /// </param>
    /// <param name="value">
    /// The string that is expected to begin with <paramref name="substring"/>.
    /// </param>
    /// <param name="message">
    /// The message to include in the exception when <paramref name="value"/>
    /// does not begin with <paramref name="substring"/>. The message is
    /// shown in test results.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// <paramref name="value"/> is null, or <paramref name="substring"/> is null,
    /// or <paramref name="value"/> does not start with <paramref name="substring"/>.
    /// </exception>
    public static void StartsWith([NotNull] string? substring, [NotNull] string? value, string message = "")
        => StartsWith(substring, value, StringComparison.Ordinal, message);

    /// <summary>
    /// Tests whether the specified string begins with the specified substring
    /// and throws an exception if the test string does not start with the
    /// substring.
    /// </summary>
    /// <param name="substring">
    /// The string expected to be a prefix of <paramref name="value"/>.
    /// </param>
    /// <param name="value">
    /// The string that is expected to begin with <paramref name="substring"/>.
    /// </param>
    /// <param name="comparisonType">
    /// The comparison method to compare strings <paramref name="comparisonType"/>.
    /// </param>
    /// <param name="message">
    /// The message to include in the exception when <paramref name="value"/>
    /// does not begin with <paramref name="substring"/>. The message is
    /// shown in test results.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// <paramref name="value"/> is null, or <paramref name="substring"/> is null,
    /// or <paramref name="value"/> does not start with <paramref name="substring"/>.
    /// </exception>
    public static void StartsWith([NotNull] string? substring, [NotNull] string? value, StringComparison comparisonType, string message = "")
    {
        CheckParameterNotNull(value, "Assert.StartsWith", "value", string.Empty);
        CheckParameterNotNull(substring, "Assert.StartsWith", "substring", string.Empty);
        if (!value.StartsWith(substring, comparisonType))
        {
            string userMessage = BuildUserMessage(message);
            string finalMessage = string.Format(CultureInfo.CurrentCulture, FrameworkMessages.StartsWithFail, value, substring, userMessage);
            ThrowAssertFailed("Assert.StartsWith", finalMessage);
        }
    }

    #endregion // StartsWith

    #region DoesNotStartWith

    /// <summary>
    /// Tests whether the specified string does not begin with the specified substring
    /// and throws an exception if the test string does start with the substring.
    /// </summary>
    /// <param name="substring">
    /// The string expected to be a prefix of <paramref name="value"/>.
    /// </param>
    /// <param name="value">
    /// The string that is expected to begin with <paramref name="substring"/>.
    /// </param>
    /// <param name="message">
    /// The message to include in the exception when <paramref name="value"/>
    /// does not begin with <paramref name="substring"/>. The message is
    /// shown in test results.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// <paramref name="value"/> is null, or <paramref name="substring"/> is null,
    /// or <paramref name="value"/> does not start with <paramref name="substring"/>.
    /// </exception>
    public static void DoesNotStartWith([NotNull] string? substring, [NotNull] string? value, string message = "")
        => DoesNotStartWith(substring, value, StringComparison.Ordinal, message);

    /// <summary>
    /// Tests whether the specified string does not begin with the specified substring
    /// and throws an exception if the test string does start with the substring.
    /// </summary>
    /// <param name="substring">
    /// The string expected to be a prefix of <paramref name="value"/>.
    /// </param>
    /// <param name="value">
    /// The string that is expected to begin with <paramref name="substring"/>.
    /// </param>
    /// <param name="comparisonType">
    /// The comparison method to compare strings <paramref name="comparisonType"/>.
    /// </param>
    /// <param name="message">
    /// The message to include in the exception when <paramref name="value"/>
    /// does not begin with <paramref name="substring"/>. The message is
    /// shown in test results.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// <paramref name="value"/> is null, or <paramref name="substring"/> is null,
    /// or <paramref name="value"/> does not start with <paramref name="substring"/>.
    /// </exception>
    public static void DoesNotStartWith([NotNull] string? substring, [NotNull] string? value, StringComparison comparisonType, string message = "")
    {
        CheckParameterNotNull(value, "Assert.DoesNotStartWith", "value", string.Empty);
        CheckParameterNotNull(substring, "Assert.DoesNotStartWith", "substring", string.Empty);
        if (value.StartsWith(substring, comparisonType))
        {
            string userMessage = BuildUserMessage(message);
            string finalMessage = string.Format(CultureInfo.CurrentCulture, FrameworkMessages.DoesNotStartWithFail, value, substring, userMessage);
            ThrowAssertFailed("Assert.DoesNotStartWith", finalMessage);
        }
    }

    #endregion // DoesNotStartWith
}
