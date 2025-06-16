﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestTools.UnitTesting;

public sealed partial class Assert
{
    #region MatchesRegex

    /// <summary>
    /// Tests whether the specified string MatchesRegex a regular expression and
    /// throws an exception if the string does not match the expression.
    /// </summary>
    /// <param name="pattern">
    /// The regular expression that <paramref name="value"/> is
    /// expected to match.
    /// </param>
    /// <param name="value">
    /// The string that is expected to match <paramref name="pattern"/>.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// <paramref name="value"/> is null, or <paramref name="pattern"/> is null,
    /// or <paramref name="value"/> does not match <paramref name="pattern"/>.
    /// </exception>
    public static void MatchesRegex([NotNull] Regex? pattern, [NotNull] string? value)
        => MatchesRegex(pattern, value, string.Empty, null);

    /// <summary>
    /// Tests whether the specified string MatchesRegex a regular expression and
    /// throws an exception if the string does not match the expression.
    /// </summary>
    /// <param name="pattern">
    /// The regular expression that <paramref name="value"/> is
    /// expected to match.
    /// </param>
    /// <param name="value">
    /// The string that is expected to match <paramref name="pattern"/>.
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
    public static void MatchesRegex([NotNull] Regex? pattern, [NotNull] string? value, string? message)
        => MatchesRegex(pattern, value, message, null);

    /// <summary>
    /// Tests whether the specified string MatchesRegex a regular expression and
    /// throws an exception if the string does not match the expression.
    /// </summary>
    /// <param name="pattern">
    /// The regular expression that <paramref name="value"/> is
    /// expected to match.
    /// </param>
    /// <param name="value">
    /// The string that is expected to match <paramref name="pattern"/>.
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
    public static void MatchesRegex([NotNull] Regex? pattern, [NotNull] string? value, [StringSyntax(StringSyntaxAttribute.CompositeFormat)] string? message, params object?[]? parameters)
    {
        CheckParameterNotNull(value, "Assert.MatchesRegex", "value", string.Empty);
        CheckParameterNotNull(pattern, "Assert.MatchesRegex", "pattern", string.Empty);

        if (!pattern.IsMatch(value))
        {
            string userMessage = BuildUserMessage(message, parameters);
            string finalMessage = string.Format(CultureInfo.CurrentCulture, FrameworkMessages.IsMatchFail, value, pattern, userMessage);
            ThrowAssertFailed("Assert.MatchesRegex", finalMessage);
        }
    }

    /// <summary>
    /// Tests whether the specified string MatchesRegex a regular expression and
    /// throws an exception if the string does not match the expression.
    /// </summary>
    /// <param name="pattern">
    /// The regular expression that <paramref name="value"/> is
    /// expected to match.
    /// </param>
    /// <param name="value">
    /// The string that is expected to match <paramref name="pattern"/>.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// <paramref name="value"/> is null, or <paramref name="pattern"/> is null,
    /// or <paramref name="value"/> does not match <paramref name="pattern"/>.
    /// </exception>
    public static void MatchesRegex([NotNull] string? pattern, [NotNull] string? value)
        => MatchesRegex(ToRegex(pattern), value, string.Empty, null);

    /// <summary>
    /// Tests whether the specified string MatchesRegex a regular expression and
    /// throws an exception if the string does not match the expression.
    /// </summary>
    /// <param name="pattern">
    /// The regular expression that <paramref name="value"/> is
    /// expected to match.
    /// </param>
    /// <param name="value">
    /// The string that is expected to match <paramref name="pattern"/>.
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
    public static void MatchesRegex([NotNull] string? pattern, [NotNull] string? value, string? message)
        => MatchesRegex(ToRegex(pattern), value, message, null);

    /// <summary>
    /// Tests whether the specified string MatchesRegex a regular expression and
    /// throws an exception if the string does not match the expression.
    /// </summary>
    /// <param name="pattern">
    /// The regular expression that <paramref name="value"/> is
    /// expected to match.
    /// </param>
    /// <param name="value">
    /// The string that is expected to match <paramref name="pattern"/>.
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
    public static void MatchesRegex([NotNull] string? pattern, [NotNull] string? value, [StringSyntax(StringSyntaxAttribute.CompositeFormat)] string? message, params object?[]? parameters)
        => MatchesRegex(ToRegex(pattern), value, message, parameters);

    #endregion // MatchesRegex

    #region DoesNotMatchRegex

    /// <summary>
    /// Tests whether the specified string does not match a regular expression
    /// and throws an exception if the string MatchesRegex the expression.
    /// </summary>
    /// <param name="pattern">
    /// The regular expression that <paramref name="value"/> is
    /// expected to not match.
    /// </param>
    /// <param name="value">
    /// The string that is expected not to match <paramref name="pattern"/>.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// <paramref name="value"/> is null, or <paramref name="pattern"/> is null,
    /// or <paramref name="value"/> MatchesRegex <paramref name="pattern"/>.
    /// </exception>
    public static void DoesNotMatchRegex([NotNull] Regex? pattern, [NotNull] string? value)
        => DoesNotMatchRegex(pattern, value, string.Empty);

    /// <summary>
    /// Tests whether the specified string does not match a regular expression
    /// and throws an exception if the string MatchesRegex the expression.
    /// </summary>
    /// <param name="pattern">
    /// The regular expression that <paramref name="value"/> is
    /// expected to not match.
    /// </param>
    /// <param name="value">
    /// The string that is expected not to match <paramref name="pattern"/>.
    /// </param>
    /// <param name="message">
    /// The message to include in the exception when <paramref name="value"/>
    /// MatchesRegex <paramref name="pattern"/>. The message is shown in test
    /// results.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// <paramref name="value"/> is null, or <paramref name="pattern"/> is null,
    /// or <paramref name="value"/> MatchesRegex <paramref name="pattern"/>.
    /// </exception>
    public static void DoesNotMatchRegex([NotNull] Regex? pattern, [NotNull] string? value, string? message)
        => DoesNotMatchRegex(pattern, value, message, null);

    /// <summary>
    /// Tests whether the specified string does not match a regular expression
    /// and throws an exception if the string MatchesRegex the expression.
    /// </summary>
    /// <param name="pattern">
    /// The regular expression that <paramref name="value"/> is
    /// expected to not match.
    /// </param>
    /// <param name="value">
    /// The string that is expected not to match <paramref name="pattern"/>.
    /// </param>
    /// <param name="message">
    /// The message to include in the exception when <paramref name="value"/>
    /// MatchesRegex <paramref name="pattern"/>. The message is shown in test
    /// results.
    /// </param>
    /// <param name="parameters">
    /// An array of parameters to use when formatting <paramref name="message"/>.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// <paramref name="value"/> is null, or <paramref name="pattern"/> is null,
    /// or <paramref name="value"/> MatchesRegex <paramref name="pattern"/>.
    /// </exception>
    public static void DoesNotMatchRegex([NotNull] Regex? pattern, [NotNull] string? value, [StringSyntax(StringSyntaxAttribute.CompositeFormat)] string? message, params object?[]? parameters)
    {
        CheckParameterNotNull(value, "Assert.DoesNotMatchRegex", "value", string.Empty);
        CheckParameterNotNull(pattern, "Assert.DoesNotMatchRegex", "pattern", string.Empty);

        if (pattern.IsMatch(value))
        {
            string userMessage = BuildUserMessage(message, parameters);
            string finalMessage = string.Format(CultureInfo.CurrentCulture, FrameworkMessages.IsNotMatchFail, value, pattern, userMessage);
            ThrowAssertFailed("Assert.DoesNotMatchRegex", finalMessage);
        }
    }

    /// <summary>
    /// Tests whether the specified string does not match a regular expression
    /// and throws an exception if the string MatchesRegex the expression.
    /// </summary>
    /// <param name="pattern">
    /// The regular expression that <paramref name="value"/> is
    /// expected to not match.
    /// </param>
    /// <param name="value">
    /// The string that is expected not to match <paramref name="pattern"/>.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// <paramref name="value"/> is null, or <paramref name="pattern"/> is null,
    /// or <paramref name="value"/> MatchesRegex <paramref name="pattern"/>.
    /// </exception>
    public static void DoesNotMatchRegex([NotNull] string? pattern, [NotNull] string? value)
        => DoesNotMatchRegex(ToRegex(pattern), value, string.Empty, null);

    /// <summary>
    /// Tests whether the specified string does not match a regular expression
    /// and throws an exception if the string MatchesRegex the expression.
    /// </summary>
    /// <param name="pattern">
    /// The regular expression that <paramref name="value"/> is
    /// expected to not match.
    /// </param>
    /// <param name="value">
    /// The string that is expected not to match <paramref name="pattern"/>.
    /// </param>
    /// <param name="message">
    /// The message to include in the exception when <paramref name="value"/>
    /// MatchesRegex <paramref name="pattern"/>. The message is shown in test
    /// results.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// <paramref name="value"/> is null, or <paramref name="pattern"/> is null,
    /// or <paramref name="value"/> MatchesRegex <paramref name="pattern"/>.
    /// </exception>
    public static void DoesNotMatchRegex([NotNull] string? pattern, [NotNull] string? value, string? message)
        => DoesNotMatchRegex(ToRegex(pattern), value, message, null);

    /// <summary>
    /// Tests whether the specified string does not match a regular expression
    /// and throws an exception if the string MatchesRegex the expression.
    /// </summary>
    /// <param name="pattern">
    /// The regular expression that <paramref name="value"/> is
    /// expected to not match.
    /// </param>
    /// <param name="value">
    /// The string that is expected not to match <paramref name="pattern"/>.
    /// </param>
    /// <param name="message">
    /// The message to include in the exception when <paramref name="value"/>
    /// MatchesRegex <paramref name="pattern"/>. The message is shown in test
    /// results.
    /// </param>
    /// <param name="parameters">
    /// An array of parameters to use when formatting <paramref name="message"/>.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// <paramref name="value"/> is null, or <paramref name="pattern"/> is null,
    /// or <paramref name="value"/> MatchesRegex <paramref name="pattern"/>.
    /// </exception>
    public static void DoesNotMatchRegex([NotNull] string? pattern, [NotNull] string? value, [StringSyntax(StringSyntaxAttribute.CompositeFormat)] string? message, params object?[]? parameters)
        => DoesNotMatchRegex(ToRegex(pattern), value, message, parameters);

    #endregion // DoesNotMatchRegex

    private static Regex? ToRegex([NotNull] string? pattern)
    {
        CheckParameterNotNull(pattern, "Assert.MatchesRegex", "pattern", string.Empty);
        return new Regex(pattern);
    }
}
