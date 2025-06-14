// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestTools.UnitTesting;

public sealed partial class Assert
{
    #region StartsWith
    /// <summary>
    /// Tests whether the specified string begins with the specified substring
    /// and throws an exception if the test string does not start with the
    /// substring.
    /// </summary>
    /// <param name="value">
    /// The string that is expected to begin with <paramref name="substring"/>.
    /// </param>
    /// <param name="substring">
    /// The string expected to be a prefix of <paramref name="value"/>.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// <paramref name="value"/> is null, or <paramref name="substring"/> is null,
    /// or <paramref name="value"/> does not start with <paramref name="substring"/>.
    /// </exception>
    public static void StartsWith([NotNull] string? substring, [NotNull] string? value)
        => StartsWith(substring, value, StringComparison.Ordinal, string.Empty);

    /// <summary>
    /// Tests whether the specified string begins with the specified substring
    /// and throws an exception if the test string does not start with the
    /// substring.
    /// </summary>
    /// <param name="value">
    /// The string that is expected to begin with <paramref name="substring"/>.
    /// </param>
    /// <param name="substring">
    /// The string expected to be a prefix of <paramref name="value"/>.
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
    public static void StartsWith([NotNull] string? substring, [NotNull] string? value, string? message)
        => StartsWith(substring, value, StringComparison.Ordinal, message);

    /// <summary>
    /// Tests whether the specified string begins with the specified substring
    /// and throws an exception if the test string does not start with the
    /// substring.
    /// </summary>
    /// <param name="value">
    /// The string that is expected to begin with <paramref name="substring"/>.
    /// </param>
    /// <param name="substring">
    /// The string expected to be a prefix of <paramref name="value"/>.
    /// </param>
    /// <param name="message">
    /// The message to include in the exception when <paramref name="value"/>
    /// does not begin with <paramref name="substring"/>. The message is
    /// shown in test results.
    /// </param>
    /// <param name="parameters">
    /// An array of parameters to use when formatting <paramref name="message"/>.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// <paramref name="value"/> is null, or <paramref name="substring"/> is null,
    /// or <paramref name="value"/> does not start with <paramref name="substring"/>.
    /// </exception>
    public static void StartsWith([NotNull] string? substring, [NotNull] string? value, [StringSyntax(StringSyntaxAttribute.CompositeFormat)] string? message, params object?[]? parameters)
        => StartsWith(substring, value, StringComparison.Ordinal, message, parameters);

    /// <summary>
    /// Tests whether the specified string begins with the specified substring
    /// and throws an exception if the test string does not start with the
    /// substring.
    /// </summary>
    /// <param name="value">
    /// The string that is expected to begin with <paramref name="substring"/>.
    /// </param>
    /// <param name="substring">
    /// The string expected to be a prefix of <paramref name="value"/>.
    /// </param>
    /// <param name="comparisonType">
    /// The comparison method to compare strings <paramref name="comparisonType"/>.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// <paramref name="value"/> is null, or <paramref name="substring"/> is null,
    /// or <paramref name="value"/> does not start with <paramref name="substring"/>.
    /// </exception>
    public static void StartsWith([NotNull] string? substring, [NotNull] string? value, StringComparison comparisonType)
        => StartsWith(substring, value, comparisonType, string.Empty, Empty);

    /// <summary>
    /// Tests whether the specified string begins with the specified substring
    /// and throws an exception if the test string does not start with the
    /// substring.
    /// </summary>
    /// <param name="value">
    /// The string that is expected to begin with <paramref name="substring"/>.
    /// </param>
    /// <param name="substring">
    /// The string expected to be a prefix of <paramref name="value"/>.
    /// </param>
    /// <param name="message">
    /// The message to include in the exception when <paramref name="value"/>
    /// does not begin with <paramref name="substring"/>. The message is
    /// shown in test results.
    /// </param>
    /// <param name="comparisonType">
    /// The comparison method to compare strings <paramref name="comparisonType"/>.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// <paramref name="value"/> is null, or <paramref name="substring"/> is null,
    /// or <paramref name="value"/> does not start with <paramref name="substring"/>.
    /// </exception>
    public static void StartsWith([NotNull] string? substring, [NotNull] string? value, string? message, StringComparison comparisonType)
        => StartsWith(substring, value, comparisonType, message, Empty);

    /// <summary>
    /// Tests whether the specified string begins with the specified substring
    /// and throws an exception if the test string does not start with the
    /// substring.
    /// </summary>
    /// <param name="value">
    /// The string that is expected to begin with <paramref name="substring"/>.
    /// </param>
    /// <param name="substring">
    /// The string expected to be a prefix of <paramref name="value"/>.
    /// </param>
    /// <param name="message">
    /// The message to include in the exception when <paramref name="value"/>
    /// does not begin with <paramref name="substring"/>. The message is
    /// shown in test results.
    /// </param>
    /// <param name="comparisonType">
    /// The comparison method to compare strings <paramref name="comparisonType"/>.
    /// </param>
    /// <param name="parameters">
    /// An array of parameters to use when formatting <paramref name="message"/>.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// <paramref name="value"/> is null, or <paramref name="substring"/> is null,
    /// or <paramref name="value"/> does not start with <paramref name="substring"/>.
    /// </exception>
    public static void StartsWith([NotNull] string? substring, [NotNull] string? value, StringComparison comparisonType, [StringSyntax(StringSyntaxAttribute.CompositeFormat)] string? message, params object?[]? parameters)
    {
        Assert.CheckParameterNotNull(value, "StringAssert.StartsWith", "value", string.Empty);
        Assert.CheckParameterNotNull(substring, "StringAssert.StartsWith", "substring", string.Empty);
        if (!value.StartsWith(substring, comparisonType))
        {
            string userMessage = Assert.BuildUserMessage(message, parameters);
            string finalMessage = string.Format(CultureInfo.CurrentCulture, FrameworkMessages.StartsWithFail, value, substring, userMessage);
            Assert.ThrowAssertFailed("StringAssert.StartsWith", finalMessage);
        }
    }
    #endregion // StartsWith

    #region DoesNotStartWith
    /// <summary>
    /// Tests whether the specified string does not begin with the specified substring
    /// and throws an exception if the test string does start with the substring.
    /// </summary>
    /// <param name="value">
    /// The string that is expected to begin with <paramref name="substring"/>.
    /// </param>
    /// <param name="substring">
    /// The string expected to be a prefix of <paramref name="value"/>.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// <paramref name="value"/> is null, or <paramref name="substring"/> is null,
    /// or <paramref name="value"/> does not start with <paramref name="substring"/>.
    /// </exception>
    public static void DoesNotStartWith([NotNull] string? substring, [NotNull] string? value)
        => DoesNotStartWith(substring, value, StringComparison.Ordinal, string.Empty);

    /// <summary>
    /// Tests whether the specified string does not begin with the specified substring
    /// and throws an exception if the test string does start with the substring.
    /// </summary>
    /// <param name="value">
    /// The string that is expected to begin with <paramref name="substring"/>.
    /// </param>
    /// <param name="substring">
    /// The string expected to be a prefix of <paramref name="value"/>.
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
    public static void DoesNotStartWith([NotNull] string? substring, [NotNull] string? value, string? message)
        => DoesNotStartWith(substring, value, StringComparison.Ordinal, message);

    /// <summary>
    /// Tests whether the specified string does not begin with the specified substring
    /// and throws an exception if the test string does start with the substring.
    /// </summary>
    /// <param name="value">
    /// The string that is expected to begin with <paramref name="substring"/>.
    /// </param>
    /// <param name="substring">
    /// The string expected to be a prefix of <paramref name="value"/>.
    /// </param>
    /// <param name="message">
    /// The message to include in the exception when <paramref name="value"/>
    /// does not begin with <paramref name="substring"/>. The message is
    /// shown in test results.
    /// </param>
    /// <param name="parameters">
    /// An array of parameters to use when formatting <paramref name="message"/>.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// <paramref name="value"/> is null, or <paramref name="substring"/> is null,
    /// or <paramref name="value"/> does not start with <paramref name="substring"/>.
    /// </exception>
    public static void DoesNotStartWith([NotNull] string? substring, [NotNull] string? value, [StringSyntax(StringSyntaxAttribute.CompositeFormat)] string? message, params object?[]? parameters)
        => DoesNotStartWith(substring, value, StringComparison.Ordinal, message, parameters);

    /// <summary>
    /// Tests whether the specified string does not begin with the specified substring
    /// and throws an exception if the test string does start with the substring.
    /// </summary>
    /// <param name="value">
    /// The string that is expected to begin with <paramref name="substring"/>.
    /// </param>
    /// <param name="substring">
    /// The string expected to be a prefix of <paramref name="value"/>.
    /// </param>
    /// <param name="comparisonType">
    /// The comparison method to compare strings <paramref name="comparisonType"/>.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// <paramref name="value"/> is null, or <paramref name="substring"/> is null,
    /// or <paramref name="value"/> does not start with <paramref name="substring"/>.
    /// </exception>
    public static void DoesNotStartWith([NotNull] string? substring, [NotNull] string? value, StringComparison comparisonType)
        => DoesNotStartWith(substring, value, comparisonType, string.Empty, Empty);

    /// <summary>
    /// Tests whether the specified string does not begin with the specified substring
    /// and throws an exception if the test string does start with the substring.
    /// </summary>
    /// <param name="value">
    /// The string that is expected to begin with <paramref name="substring"/>.
    /// </param>
    /// <param name="substring">
    /// The string expected to be a prefix of <paramref name="value"/>.
    /// </param>
    /// <param name="message">
    /// The message to include in the exception when <paramref name="value"/>
    /// does not begin with <paramref name="substring"/>. The message is
    /// shown in test results.
    /// </param>
    /// <param name="comparisonType">
    /// The comparison method to compare strings <paramref name="comparisonType"/>.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// <paramref name="value"/> is null, or <paramref name="substring"/> is null,
    /// or <paramref name="value"/> does not start with <paramref name="substring"/>.
    /// </exception>
    public static void DoesNotStartWith([NotNull] string? substring, [NotNull] string? value, string? message, StringComparison comparisonType)
        => DoesNotStartWith(substring, value, comparisonType, message, Empty);

    /// <summary>
    /// Tests whether the specified string does not begin with the specified substring
    /// and throws an exception if the test string does start with the substring.
    /// </summary>
    /// <param name="value">
    /// The string that is expected to begin with <paramref name="substring"/>.
    /// </param>
    /// <param name="substring">
    /// The string expected to be a prefix of <paramref name="value"/>.
    /// </param>
    /// <param name="message">
    /// The message to include in the exception when <paramref name="value"/>
    /// does not begin with <paramref name="substring"/>. The message is
    /// shown in test results.
    /// </param>
    /// <param name="comparisonType">
    /// The comparison method to compare strings <paramref name="comparisonType"/>.
    /// </param>
    /// <param name="parameters">
    /// An array of parameters to use when formatting <paramref name="message"/>.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// <paramref name="value"/> is null, or <paramref name="substring"/> is null,
    /// or <paramref name="value"/> does not start with <paramref name="substring"/>.
    /// </exception>
    public static void DoesNotStartWith([NotNull] string? substring, [NotNull] string? value, StringComparison comparisonType,
        [StringSyntax(StringSyntaxAttribute.CompositeFormat)] string? message, params object?[]? parameters)
    {
        Assert.CheckParameterNotNull(value, "StringAssert.DoesNotStartWith", "value", string.Empty);
        Assert.CheckParameterNotNull(substring, "StringAssert.DoesNotStartWith", "substring", string.Empty);
        if (value.StartsWith(substring, comparisonType))
        {
            string userMessage = Assert.BuildUserMessage(message, parameters);
            string finalMessage = string.Format(CultureInfo.CurrentCulture, FrameworkMessages.DoesNotStartWithFail, value, substring, userMessage);
            Assert.ThrowAssertFailed("StringAssert.DoesNotStartWith", finalMessage);
        }
    }
    #endregion // DoesNotStartWith
}
