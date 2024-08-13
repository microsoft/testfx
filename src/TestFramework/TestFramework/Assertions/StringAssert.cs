// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// The string assert.
/// </summary>
public sealed class StringAssert
{
    private static readonly object[] Empty = [];

    #region Singleton constructor

    private StringAssert()
    {
    }

    /// <summary>
    /// Gets the singleton instance of the CollectionAssert functionality.
    /// </summary>
    /// <remarks>
    /// Users can use this to plug-in custom assertions through C# extension methods.
    /// For instance, the signature of a custom assertion provider could be "public static void ContainsWords(this StringAssert customAssert, string value, ICollection substrings)"
    /// Users could then use a syntax similar to the default assertions which in this case is "StringAssert.That.ContainsWords(value, substrings);"
    /// More documentation is at "https://github.com/Microsoft/testfx/docs/README.md".
    /// </remarks>
    public static StringAssert That { get; } = new StringAssert();

    #endregion

    #region Substrings

    /// <summary>
    /// Tests whether the specified string contains the specified substring
    /// and throws an exception if the substring does not occur within the
    /// test string.
    /// </summary>
    /// <param name="value">
    /// The string that is expected to contain <paramref name="substring"/>.
    /// </param>
    /// <param name="substring">
    /// The string expected to occur within <paramref name="value"/>.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// <paramref name="value"/> is null, or <paramref name="substring"/> is null,
    /// or <paramref name="value"/> does not contain <paramref name="substring"/>.
    /// </exception>
    public static void Contains([NotNull] string? value, [NotNull] string? substring)
        => Contains(value, substring, string.Empty, StringComparison.Ordinal);

    /// <summary>
    /// Tests whether the specified string contains the specified substring
    /// and throws an exception if the substring does not occur within the
    /// test string.
    /// </summary>
    /// <param name="value">
    /// The string that is expected to contain <paramref name="substring"/>.
    /// </param>
    /// <param name="substring">
    /// The string expected to occur within <paramref name="value"/>.
    /// </param>
    /// <param name="comparisonType">
    /// The comparison method to compare strings <paramref name="comparisonType"/>.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// <paramref name="value"/> is null, or <paramref name="substring"/> is null,
    /// or <paramref name="value"/> does not contain <paramref name="substring"/>.
    /// </exception>
    public static void Contains([NotNull] string? value, [NotNull] string? substring, StringComparison comparisonType)
        => Contains(value, substring, string.Empty, comparisonType);

    /// <summary>
    /// Tests whether the specified string contains the specified substring
    /// and throws an exception if the substring does not occur within the
    /// test string.
    /// </summary>
    /// <param name="value">
    /// The string that is expected to contain <paramref name="substring"/>.
    /// </param>
    /// <param name="substring">
    /// The string expected to occur within <paramref name="value"/>.
    /// </param>
    /// <param name="message">
    /// The message to include in the exception when <paramref name="substring"/>
    /// is not in <paramref name="value"/>. The message is shown in
    /// test results.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// <paramref name="value"/> is null, or <paramref name="substring"/> is null,
    /// or <paramref name="value"/> does not contain <paramref name="substring"/>.
    /// </exception>
    public static void Contains([NotNull] string? value, [NotNull] string? substring, string? message)
        => Contains(value, substring, message, StringComparison.Ordinal);

    /// <summary>
    /// Tests whether the specified string contains the specified substring
    /// and throws an exception if the substring does not occur within the
    /// test string.
    /// </summary>
    /// <param name="value">
    /// The string that is expected to contain <paramref name="substring"/>.
    /// </param>
    /// <param name="substring">
    /// The string expected to occur within <paramref name="value"/>.
    /// </param>
    /// <param name="message">
    /// The message to include in the exception when <paramref name="substring"/>
    /// is not in <paramref name="value"/>. The message is shown in
    /// test results.
    /// </param>
    /// <param name="comparisonType">
    /// The comparison method to compare strings <paramref name="comparisonType"/>.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// <paramref name="value"/> is null, or <paramref name="substring"/> is null,
    /// or <paramref name="value"/> does not contain <paramref name="substring"/>.
    /// </exception>
    public static void Contains([NotNull] string? value, [NotNull] string? substring, string? message,
        StringComparison comparisonType)
        => Contains(value, substring, message, comparisonType, Empty);

    /// <summary>
    /// Tests whether the specified string contains the specified substring
    /// and throws an exception if the substring does not occur within the
    /// test string.
    /// </summary>
    /// <param name="value">
    /// The string that is expected to contain <paramref name="substring"/>.
    /// </param>
    /// <param name="substring">
    /// The string expected to occur within <paramref name="value"/>.
    /// </param>
    /// <param name="message">
    /// The message to include in the exception when <paramref name="substring"/>
    /// is not in <paramref name="value"/>. The message is shown in
    /// test results.
    /// </param>
    /// <param name="parameters">
    /// An array of parameters to use when formatting <paramref name="message"/>.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// <paramref name="value"/> is null, or <paramref name="substring"/> is null,
    /// or <paramref name="value"/> does not contain <paramref name="substring"/>.
    /// </exception>
    public static void Contains([NotNull] string? value, [NotNull] string? substring, [StringSyntax(StringSyntaxAttribute.CompositeFormat)] string? message,
        params object?[]? parameters)
        => Contains(value, substring, message, StringComparison.Ordinal, parameters);

    /// <summary>
    /// Tests whether the specified string contains the specified substring
    /// and throws an exception if the substring does not occur within the
    /// test string.
    /// </summary>
    /// <param name="value">
    /// The string that is expected to contain <paramref name="substring"/>.
    /// </param>
    /// <param name="substring">
    /// The string expected to occur within <paramref name="value"/>.
    /// </param>
    /// <param name="message">
    /// The message to include in the exception when <paramref name="substring"/>
    /// is not in <paramref name="value"/>. The message is shown in
    /// test results.
    /// </param>
    /// <param name="comparisonType">
    /// The comparison method to compare strings <paramref name="comparisonType"/>.
    /// </param>
    /// <param name="parameters">
    /// An array of parameters to use when formatting <paramref name="message"/>.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// <paramref name="value"/> is null, or <paramref name="substring"/> is null,
    /// or <paramref name="value"/> does not contain <paramref name="substring"/>.
    /// </exception>
    public static void Contains([NotNull] string? value, [NotNull] string? substring, [StringSyntax(StringSyntaxAttribute.CompositeFormat)] string? message,
        StringComparison comparisonType, params object?[]? parameters)
    {
        Assert.CheckParameterNotNull(value, "StringAssert.Contains", "value", string.Empty);
        Assert.CheckParameterNotNull(substring, "StringAssert.Contains", "substring", string.Empty);
        if (value.IndexOf(substring, comparisonType) < 0)
        {
            string userMessage = Assert.BuildUserMessage(message, parameters);
            string finalMessage = string.Format(CultureInfo.CurrentCulture, FrameworkMessages.ContainsFail, value, substring, userMessage);
            Assert.ThrowAssertFailed("StringAssert.Contains", finalMessage);
        }
    }

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
    public static void StartsWith([NotNull] string? value, [NotNull] string? substring)
        => StartsWith(value, substring, string.Empty, StringComparison.Ordinal);

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
    public static void StartsWith([NotNull] string? value, [NotNull] string? substring,
        StringComparison comparisonType)
        => StartsWith(value, substring, string.Empty, comparisonType, Empty);

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
    public static void StartsWith([NotNull] string? value, [NotNull] string? substring, string? message)
        => StartsWith(value, substring, message, StringComparison.Ordinal);

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
    public static void StartsWith([NotNull] string? value, [NotNull] string? substring, [StringSyntax(StringSyntaxAttribute.CompositeFormat)] string? message,
        params object?[]? parameters)
        => StartsWith(value, substring, message, StringComparison.Ordinal, parameters);

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
    public static void StartsWith([NotNull] string? value, [NotNull] string? substring, string? message,
        StringComparison comparisonType)
        => StartsWith(value, substring, message, comparisonType, Empty);

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
    public static void StartsWith([NotNull] string? value, [NotNull] string? substring, [StringSyntax(StringSyntaxAttribute.CompositeFormat)] string? message,
        StringComparison comparisonType, params object?[]? parameters)
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

    /// <summary>
    /// Tests whether the specified string ends with the specified substring
    /// and throws an exception if the test string does not end with the
    /// substring.
    /// </summary>
    /// <param name="value">
    /// The string that is expected to end with <paramref name="substring"/>.
    /// </param>
    /// <param name="substring">
    /// The string expected to be a suffix of <paramref name="value"/>.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// <paramref name="value"/> is null, or <paramref name="substring"/> is null,
    /// or <paramref name="value"/> does not end with <paramref name="substring"/>.
    /// </exception>
    public static void EndsWith([NotNull] string? value, [NotNull] string? substring)
        => EndsWith(value, substring, string.Empty, StringComparison.Ordinal);

    /// <summary>
    /// Tests whether the specified string ends with the specified substring
    /// and throws an exception if the test string does not end with the
    /// substring.
    /// </summary>
    /// <param name="value">
    /// The string that is expected to end with <paramref name="substring"/>.
    /// </param>
    /// <param name="substring">
    /// The string expected to be a suffix of <paramref name="value"/>.
    /// </param>
    /// <param name="comparisonType">
    /// The comparison method to compare strings <paramref name="comparisonType"/>.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// <paramref name="value"/> is null, or <paramref name="substring"/> is null,
    /// or <paramref name="value"/> does not end with <paramref name="substring"/>.
    /// </exception>
    public static void EndsWith([NotNull] string? value, [NotNull] string? substring,
        StringComparison comparisonType)
        => EndsWith(value, substring, string.Empty, comparisonType, Empty);

    /// <summary>
    /// Tests whether the specified string ends with the specified substring
    /// and throws an exception if the test string does not end with the
    /// substring.
    /// </summary>
    /// <param name="value">
    /// The string that is expected to end with <paramref name="substring"/>.
    /// </param>
    /// <param name="substring">
    /// The string expected to be a suffix of <paramref name="value"/>.
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
    public static void EndsWith([NotNull] string? value, [NotNull] string? substring, string? message)
        => EndsWith(value, substring, message, StringComparison.Ordinal);

    /// <summary>
    /// Tests whether the specified string ends with the specified substring
    /// and throws an exception if the test string does not end with the
    /// substring.
    /// </summary>
    /// <param name="value">
    /// The string that is expected to end with <paramref name="substring"/>.
    /// </param>
    /// <param name="substring">
    /// The string expected to be a suffix of <paramref name="value"/>.
    /// </param>
    /// <param name="message">
    /// The message to include in the exception when <paramref name="value"/>
    /// does not end with <paramref name="substring"/>. The message is
    /// shown in test results.
    /// </param>
    /// <param name="parameters">
    /// An array of parameters to use when formatting <paramref name="message"/>.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// <paramref name="value"/> is null, or <paramref name="substring"/> is null,
    /// or <paramref name="value"/> does not end with <paramref name="substring"/>.
    /// </exception>
    public static void EndsWith([NotNull] string? value, [NotNull] string? substring, [StringSyntax(StringSyntaxAttribute.CompositeFormat)] string? message,
        params object?[]? parameters)
        => EndsWith(value, substring, message, StringComparison.Ordinal, parameters);

    /// <summary>
    /// Tests whether the specified string ends with the specified substring
    /// and throws an exception if the test string does not end with the
    /// substring.
    /// </summary>
    /// <param name="value">
    /// The string that is expected to end with <paramref name="substring"/>.
    /// </param>
    /// <param name="substring">
    /// The string expected to be a suffix of <paramref name="value"/>.
    /// </param>
    /// <param name="message">
    /// The message to include in the exception when <paramref name="value"/>
    /// does not end with <paramref name="substring"/>. The message is
    /// shown in test results.
    /// </param>
    /// <param name="comparisonType">
    /// The comparison method to compare strings <paramref name="comparisonType"/>.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// <paramref name="value"/> is null, or <paramref name="substring"/> is null,
    /// or <paramref name="value"/> does not end with <paramref name="substring"/>.
    /// </exception>
    public static void EndsWith([NotNull] string? value, [NotNull] string? substring, string? message,
        StringComparison comparisonType)
        => EndsWith(value, substring, message, comparisonType, Empty);

    /// <summary>
    /// Tests whether the specified string ends with the specified substring
    /// and throws an exception if the test string does not end with the
    /// substring.
    /// </summary>
    /// <param name="value">
    /// The string that is expected to end with <paramref name="substring"/>.
    /// </param>
    /// <param name="substring">
    /// The string expected to be a suffix of <paramref name="value"/>.
    /// </param>
    /// <param name="message">
    /// The message to include in the exception when <paramref name="value"/>
    /// does not end with <paramref name="substring"/>. The message is
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
    /// or <paramref name="value"/> does not end with <paramref name="substring"/>.
    /// </exception>
    public static void EndsWith([NotNull] string? value, [NotNull] string? substring, [StringSyntax(StringSyntaxAttribute.CompositeFormat)] string? message,
        StringComparison comparisonType, params object?[]? parameters)
    {
        Assert.CheckParameterNotNull(value, "StringAssert.EndsWith", "value", string.Empty);
        Assert.CheckParameterNotNull(substring, "StringAssert.EndsWith", "substring", string.Empty);
        if (!value.EndsWith(substring, comparisonType))
        {
            string userMessage = Assert.BuildUserMessage(message, parameters);
            string finalMessage = string.Format(CultureInfo.CurrentCulture, FrameworkMessages.EndsWithFail, value, substring, userMessage);
            Assert.ThrowAssertFailed("StringAssert.EndsWith", finalMessage);
        }
    }

    #endregion Substrings

    #region Regular Expressions

    /// <summary>
    /// Tests whether the specified string matches a regular expression and
    /// throws an exception if the string does not match the expression.
    /// </summary>
    /// <param name="value">
    /// The string that is expected to match <paramref name="pattern"/>.
    /// </param>
    /// <param name="pattern">
    /// The regular expression that <paramref name="value"/> is
    /// expected to match.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// <paramref name="value"/> is null, or <paramref name="pattern"/> is null,
    /// or <paramref name="value"/> does not match <paramref name="pattern"/>.
    /// </exception>
    public static void Matches([NotNull] string? value, [NotNull] Regex? pattern)
        => Matches(value, pattern, string.Empty);

    /// <summary>
    /// Tests whether the specified string matches a regular expression and
    /// throws an exception if the string does not match the expression.
    /// </summary>
    /// <param name="value">
    /// The string that is expected to match <paramref name="pattern"/>.
    /// </param>
    /// <param name="pattern">
    /// The regular expression that <paramref name="value"/> is
    /// expected to match.
    /// </param>
    /// <param name="message">
    /// The message to include in the exception when <paramref name="value"/>
    /// does not match <paramref name="pattern"/>. The message is shown in
    /// test results.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// <paramref name="value"/> is null, or <paramref name="pattern"/> is null,
    /// or <paramref name="value"/> does not match <paramref name="pattern"/>.
    /// </exception>
    public static void Matches([NotNull] string? value, [NotNull] Regex? pattern, string? message)
        => Matches(value, pattern, message, null);

    /// <summary>
    /// Tests whether the specified string matches a regular expression and
    /// throws an exception if the string does not match the expression.
    /// </summary>
    /// <param name="value">
    /// The string that is expected to match <paramref name="pattern"/>.
    /// </param>
    /// <param name="pattern">
    /// The regular expression that <paramref name="value"/> is
    /// expected to match.
    /// </param>
    /// <param name="message">
    /// The message to include in the exception when <paramref name="value"/>
    /// does not match <paramref name="pattern"/>. The message is shown in
    /// test results.
    /// </param>
    /// <param name="parameters">
    /// An array of parameters to use when formatting <paramref name="message"/>.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// <paramref name="value"/> is null, or <paramref name="pattern"/> is null,
    /// or <paramref name="value"/> does not match <paramref name="pattern"/>.
    /// </exception>
    public static void Matches([NotNull] string? value, [NotNull] Regex? pattern, [StringSyntax(StringSyntaxAttribute.CompositeFormat)] string? message, params object?[]? parameters)
    {
        Assert.CheckParameterNotNull(value, "StringAssert.Matches", "value", string.Empty);
        Assert.CheckParameterNotNull(pattern, "StringAssert.Matches", "pattern", string.Empty);

        if (!pattern.IsMatch(value))
        {
            string userMessage = Assert.BuildUserMessage(message, parameters);
            string finalMessage = string.Format(CultureInfo.CurrentCulture, FrameworkMessages.IsMatchFail, value, pattern, userMessage);
            Assert.ThrowAssertFailed("StringAssert.Matches", finalMessage);
        }
    }

    /// <summary>
    /// Tests whether the specified string does not match a regular expression
    /// and throws an exception if the string matches the expression.
    /// </summary>
    /// <param name="value">
    /// The string that is expected not to match <paramref name="pattern"/>.
    /// </param>
    /// <param name="pattern">
    /// The regular expression that <paramref name="value"/> is
    /// expected to not match.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// <paramref name="value"/> is null, or <paramref name="pattern"/> is null,
    /// or <paramref name="value"/> matches <paramref name="pattern"/>.
    /// </exception>
    public static void DoesNotMatch([NotNull] string? value, [NotNull] Regex? pattern)
        => DoesNotMatch(value, pattern, string.Empty);

    /// <summary>
    /// Tests whether the specified string does not match a regular expression
    /// and throws an exception if the string matches the expression.
    /// </summary>
    /// <param name="value">
    /// The string that is expected not to match <paramref name="pattern"/>.
    /// </param>
    /// <param name="pattern">
    /// The regular expression that <paramref name="value"/> is
    /// expected to not match.
    /// </param>
    /// <param name="message">
    /// The message to include in the exception when <paramref name="value"/>
    /// matches <paramref name="pattern"/>. The message is shown in test
    /// results.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// <paramref name="value"/> is null, or <paramref name="pattern"/> is null,
    /// or <paramref name="value"/> matches <paramref name="pattern"/>.
    /// </exception>
    public static void DoesNotMatch([NotNull] string? value, [NotNull] Regex? pattern, string? message)
        => DoesNotMatch(value, pattern, message, null);

    /// <summary>
    /// Tests whether the specified string does not match a regular expression
    /// and throws an exception if the string matches the expression.
    /// </summary>
    /// <param name="value">
    /// The string that is expected not to match <paramref name="pattern"/>.
    /// </param>
    /// <param name="pattern">
    /// The regular expression that <paramref name="value"/> is
    /// expected to not match.
    /// </param>
    /// <param name="message">
    /// The message to include in the exception when <paramref name="value"/>
    /// matches <paramref name="pattern"/>. The message is shown in test
    /// results.
    /// </param>
    /// <param name="parameters">
    /// An array of parameters to use when formatting <paramref name="message"/>.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// <paramref name="value"/> is null, or <paramref name="pattern"/> is null,
    /// or <paramref name="value"/> matches <paramref name="pattern"/>.
    /// </exception>
    public static void DoesNotMatch([NotNull] string? value, [NotNull] Regex? pattern, [StringSyntax(StringSyntaxAttribute.CompositeFormat)] string? message, params object?[]? parameters)
    {
        Assert.CheckParameterNotNull(value, "StringAssert.DoesNotMatch", "value", string.Empty);
        Assert.CheckParameterNotNull(pattern, "StringAssert.DoesNotMatch", "pattern", string.Empty);

        if (pattern.IsMatch(value))
        {
            string userMessage = Assert.BuildUserMessage(message, parameters);
            string finalMessage = string.Format(CultureInfo.CurrentCulture, FrameworkMessages.IsNotMatchFail, value, pattern, userMessage);
            Assert.ThrowAssertFailed("StringAssert.DoesNotMatch", finalMessage);
        }
    }

    #endregion Regular Expressions
}
