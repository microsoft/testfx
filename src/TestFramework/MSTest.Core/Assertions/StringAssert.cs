// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestTools.UnitTesting
{
    using System;
    using System.Globalization;
    using System.Text.RegularExpressions;

    /// <summary>
    /// The string assert.
    /// </summary>
    public sealed class StringAssert
    {
        private static StringAssert that;

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
        /// More documentation is at "https://github.com/Microsoft/testfx-docs".
        /// </remarks>
        public static StringAssert That
        {
            get
            {
                if (that == null)
                {
                    that = new StringAssert();
                }

                return that;
            }
        }

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
        /// Thrown if <paramref name="substring"/> is not found in
        /// <paramref name="value"/>.
        /// </exception>
        public static void Contains(string value, string substring)
        {
            Contains(value, substring, string.Empty, null);
        }

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
        /// Thrown if <paramref name="substring"/> is not found in
        /// <paramref name="value"/>.
        /// </exception>
        public static void Contains(string value, string substring, string message)
        {
            Contains(value, substring, message, null);
        }

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
        /// Thrown if <paramref name="substring"/> is not found in
        /// <paramref name="value"/>.
        /// </exception>
        public static void Contains(string value, string substring, string message, params object[] parameters)
        {
            Assert.CheckParameterNotNull(value, "StringAssert.Contains", "value", string.Empty);
            Assert.CheckParameterNotNull(substring, "StringAssert.Contains", "substring", string.Empty);
            if (value.IndexOf(substring, StringComparison.Ordinal) < 0)
            {
                string finalMessage = string.Format(CultureInfo.CurrentCulture, FrameworkMessages.ContainsFail, value, substring, message);
                Assert.HandleFail("StringAssert.Contains", finalMessage, parameters);
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
        /// Thrown if <paramref name="value"/> does not begin with
        /// <paramref name="substring"/>.
        /// </exception>
        public static void StartsWith(string value, string substring)
        {
            StartsWith(value, substring, string.Empty, null);
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
        /// <param name="message">
        /// The message to include in the exception when <paramref name="value"/>
        /// does not begin with <paramref name="substring"/>. The message is
        /// shown in test results.
        /// </param>
        /// <exception cref="AssertFailedException">
        /// Thrown if <paramref name="value"/> does not begin with
        /// <paramref name="substring"/>.
        /// </exception>
        public static void StartsWith(string value, string substring, string message)
        {
            StartsWith(value, substring, message, null);
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
        /// <param name="message">
        /// The message to include in the exception when <paramref name="value"/>
        /// does not begin with <paramref name="substring"/>. The message is
        /// shown in test results.
        /// </param>
        /// <param name="parameters">
        /// An array of parameters to use when formatting <paramref name="message"/>.
        /// </param>
        /// <exception cref="AssertFailedException">
        /// Thrown if <paramref name="value"/> does not begin with
        /// <paramref name="substring"/>.
        /// </exception>
        public static void StartsWith(string value, string substring, string message, params object[] parameters)
        {
            Assert.CheckParameterNotNull(value, "StringAssert.StartsWith", "value", string.Empty);
            Assert.CheckParameterNotNull(substring, "StringAssert.StartsWith", "substring", string.Empty);
            if (!value.StartsWith(substring, StringComparison.Ordinal))
            {
                string finalMessage = string.Format(CultureInfo.CurrentCulture, FrameworkMessages.StartsWithFail, value, substring, message);
                Assert.HandleFail("StringAssert.StartsWith", finalMessage, parameters);
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
        /// Thrown if <paramref name="value"/> does not end with
        /// <paramref name="substring"/>.
        /// </exception>
        public static void EndsWith(string value, string substring)
        {
            EndsWith(value, substring, string.Empty, null);
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
        /// <param name="message">
        /// The message to include in the exception when <paramref name="value"/>
        /// does not end with <paramref name="substring"/>. The message is
        /// shown in test results.
        /// </param>
        /// <exception cref="AssertFailedException">
        /// Thrown if <paramref name="value"/> does not end with
        /// <paramref name="substring"/>.
        /// </exception>
        public static void EndsWith(string value, string substring, string message)
        {
            EndsWith(value, substring, message, null);
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
        /// <param name="message">
        /// The message to include in the exception when <paramref name="value"/>
        /// does not end with <paramref name="substring"/>. The message is
        /// shown in test results.
        /// </param>
        /// <param name="parameters">
        /// An array of parameters to use when formatting <paramref name="message"/>.
        /// </param>
        /// <exception cref="AssertFailedException">
        /// Thrown if <paramref name="value"/> does not end with
        /// <paramref name="substring"/>.
        /// </exception>
        public static void EndsWith(string value, string substring, string message, params object[] parameters)
        {
            Assert.CheckParameterNotNull(value, "StringAssert.EndsWith", "value", string.Empty);
            Assert.CheckParameterNotNull(substring, "StringAssert.EndsWith", "substring", string.Empty);
            if (!value.EndsWith(substring, StringComparison.Ordinal))
            {
                string finalMessage = string.Format(CultureInfo.CurrentCulture, FrameworkMessages.EndsWithFail, value, substring, message);
                Assert.HandleFail("StringAssert.EndsWith", finalMessage, parameters);
            }
        }

        #endregion Substrings

        #region Regular Expresssions

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
        /// Thrown if <paramref name="value"/> does not match
        /// <paramref name="pattern"/>.
        /// </exception>
        public static void Matches(string value, Regex pattern)
        {
            Matches(value, pattern, string.Empty, null);
        }

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
        /// Thrown if <paramref name="value"/> does not match
        /// <paramref name="pattern"/>.
        /// </exception>
        public static void Matches(string value, Regex pattern, string message)
        {
            Matches(value, pattern, message, null);
        }

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
        /// Thrown if <paramref name="value"/> does not match
        /// <paramref name="pattern"/>.
        /// </exception>
        public static void Matches(string value, Regex pattern, string message, params object[] parameters)
        {
            Assert.CheckParameterNotNull(value, "StringAssert.Matches", "value", string.Empty);
            Assert.CheckParameterNotNull(pattern, "StringAssert.Matches", "pattern", string.Empty);

            if (!pattern.IsMatch(value))
            {
                string finalMessage = string.Format(CultureInfo.CurrentCulture, FrameworkMessages.IsMatchFail, value, pattern, message);
                Assert.HandleFail("StringAssert.Matches", finalMessage, parameters);
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
        /// Thrown if <paramref name="value"/> matches <paramref name="pattern"/>.
        /// </exception>
        public static void DoesNotMatch(string value, Regex pattern)
        {
            DoesNotMatch(value, pattern, string.Empty, null);
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
        /// <param name="message">
        /// The message to include in the exception when <paramref name="value"/>
        /// matches <paramref name="pattern"/>. The message is shown in test
        /// results.
        /// </param>
        /// <exception cref="AssertFailedException">
        /// Thrown if <paramref name="value"/> matches <paramref name="pattern"/>.
        /// </exception>
        public static void DoesNotMatch(string value, Regex pattern, string message)
        {
            DoesNotMatch(value, pattern, message, null);
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
        /// <param name="message">
        /// The message to include in the exception when <paramref name="value"/>
        /// matches <paramref name="pattern"/>. The message is shown in test
        /// results.
        /// </param>
        /// <param name="parameters">
        /// An array of parameters to use when formatting <paramref name="message"/>.
        /// </param>
        /// <exception cref="AssertFailedException">
        /// Thrown if <paramref name="value"/> matches <paramref name="pattern"/>.
        /// </exception>
        public static void DoesNotMatch(string value, Regex pattern, string message, params object[] parameters)
        {
            Assert.CheckParameterNotNull(value, "StringAssert.DoesNotMatch", "value", string.Empty);
            Assert.CheckParameterNotNull(pattern, "StringAssert.DoesNotMatch", "pattern", string.Empty);

            if (pattern.IsMatch(value))
            {
                string finalMessage = string.Format(CultureInfo.CurrentCulture, FrameworkMessages.IsNotMatchFail, value, pattern, message);
                Assert.HandleFail("StringAssert.DoesNotMatch", finalMessage, parameters);
            }
        }

        #endregion Regular Expresssions
    }
}
