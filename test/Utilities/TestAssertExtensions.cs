// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.Testing.TestInfrastructure;

/// <summary>
/// Test-only assertions for filesystem paths, strings, and dictionary entries.
/// </summary>
/// <remarks>
/// Like other assertions, nonnull postconditions apply to successful assertions, not failures collected by an assertion scope.
/// Messages are ordinary string arguments; do not replace lazy interpolated assertion messages with side effects.
/// </remarks>
internal static class TestAssertExtensions
{
    extension(Assert)
    {
        /// <summary>
        /// Asserts that <see cref="File.Exists(string)"/> returns true for the path.
        /// </summary>
        public static void FileExists(string? path, string? message = null, [CallerArgumentExpression(nameof(path))] string pathExpression = "")
            => Check(File.Exists(path), nameof(FileExists), path, pathExpression, message);

        /// <summary>
        /// Asserts that <see cref="File.Exists(string)"/> returns false for the path.
        /// </summary>
        /// <remarks>
        /// This does not prove physical absence or successful access: Exists also returns false for invalid or unavailable paths.
        /// </remarks>
        public static void FileDoesNotExist(string? path, string? message = null, [CallerArgumentExpression(nameof(path))] string pathExpression = "")
            => Check(!File.Exists(path), nameof(FileDoesNotExist), path, pathExpression, message);

        /// <summary>
        /// Asserts that <see cref="Directory.Exists(string)"/> returns true for the path.
        /// </summary>
        public static void DirectoryExists(string? path, string? message = null, [CallerArgumentExpression(nameof(path))] string pathExpression = "")
            => Check(Directory.Exists(path), nameof(DirectoryExists), path, pathExpression, message);

        /// <summary>
        /// Asserts that <see cref="Directory.Exists(string)"/> returns false for the path.
        /// </summary>
        /// <remarks>
        /// This does not prove physical absence or successful access: Exists also returns false for invalid or unavailable paths.
        /// </remarks>
        public static void DirectoryDoesNotExist(string? path, string? message = null, [CallerArgumentExpression(nameof(path))] string pathExpression = "")
            => Check(!Directory.Exists(path), nameof(DirectoryDoesNotExist), path, pathExpression, message);

        /// <summary>
        /// Asserts that the dictionary contains the key and its value equals the expected value.
        /// </summary>
        /// <remarks>
        /// Uses the dictionary's own key lookup and <see cref="EqualityComparer{T}.Default"/> for value equality.
        /// Accepts <see cref="IDictionary{TKey, TValue}"/> without modifying it; readonly-interface-only dictionaries are not supported.
        /// </remarks>
        public static void ContainsEntry<TKey, TValue>(
            TKey key,
            TValue expectedValue,
            IDictionary<TKey, TValue>? dictionary,
            string? message = null,
            [CallerArgumentExpression(nameof(key))] string keyExpression = "",
            [CallerArgumentExpression(nameof(expectedValue))] string expectedValueExpression = "",
            [CallerArgumentExpression(nameof(dictionary))] string dictionaryExpression = "")
        {
            if (dictionary is null)
            {
                ReportFailure(dictionary is not null, "Dictionary is <null>.");
                return;
            }

            bool found = dictionary.TryGetValue(key, out TValue? actualValue);
            if (!found)
            {
                int count = dictionary.Count;
                ReportFailure(found, count is 0 ? "Dictionary is empty." : $"Key was not found (dictionary count: {count}).");
                return;
            }

            bool equal = EqualityComparer<TValue>.Default.Equals(expectedValue, actualValue);
            if (equal)
            {
                return;
            }

            string mismatch = actualValue is null
                ? "Key is present, but its value is <null>."
                : expectedValue is null
                    ? "Key is present, but a <null> value was expected."
                    : "Key is present, but its value differs.";
            ReportFailure(equal, mismatch, $" Actual: {FormatValue(actualValue)}.");

            void ReportFailure(bool condition, string reason, string actual = "")
            {
                string details = $"Assert.ContainsEntry({keyExpression}, {expectedValueExpression}, {dictionaryExpression}) failed. Key: {FormatValue(key)}. {reason} Expected: {FormatValue(expectedValue)}.{actual}";
                if (message is not null)
                {
                    details += $" {message}";
                }

                Assert.IsTrue(condition, details, conditionExpression: string.Empty);
            }
        }

        /// <summary>
        /// Asserts that the value is null or empty, without treating whitespace as empty.
        /// </summary>
        public static void IsNullOrEmpty(string? value, string? message = null, [CallerArgumentExpression(nameof(value))] string valueExpression = "")
            => Check(string.IsNullOrEmpty(value), nameof(IsNullOrEmpty), value, valueExpression, message);

        /// <summary>
        /// Asserts that the value is neither null nor empty; whitespace is permitted.
        /// </summary>
        public static void IsNotNullOrEmpty([NotNull] string? value, string? message = null, [CallerArgumentExpression(nameof(value))] string valueExpression = "")
            => Check(value is not null && !string.IsNullOrEmpty(value), nameof(IsNotNullOrEmpty), value, valueExpression, message);

        /// <summary>
        /// Asserts that the value is nonnull and contains at least one character that is not .NET whitespace.
        /// </summary>
        public static void IsNotNullOrWhiteSpace([NotNull] string? value, string? message = null, [CallerArgumentExpression(nameof(value))] string valueExpression = "")
            => Check(value is not null && !string.IsNullOrWhiteSpace(value), nameof(IsNotNullOrWhiteSpace), value, valueExpression, message);
    }

    private static void Check([DoesNotReturnIf(false)] bool condition, string assertion, string? value, string expression, string? message)
    {
        if (condition)
        {
            return;
        }

        string details = $"Assert.{assertion}({expression}) failed. Actual: {FormatValue(value)}.";
        if (message is not null)
        {
            details += $" {message}";
        }

        // Boolean assertions collect failures in the caller's scope; Assert.Fail always throws.
        Assert.IsTrue(condition, details, conditionExpression: string.Empty);
    }

    private static string FormatValue(object? value)
    {
        if (value is null)
        {
            return "<null>";
        }

        if (value is not string text)
        {
            return $"{FormatValue(Convert.ToString(value, CultureInfo.InvariantCulture))} ({value.GetType().FullName})";
        }

        StringBuilder builder = new("\"");
        foreach (char character in text)
        {
            switch (character)
            {
                case '\\':
                    builder.Append(@"\\");
                    break;
                case '"':
                    builder.Append("\\\"");
                    break;
                case '\r':
                    builder.Append(@"\r");
                    break;
                case '\n':
                    builder.Append(@"\n");
                    break;
                case '\t':
                    builder.Append(@"\t");
                    break;
                default:
                    if (char.IsControl(character) || (char.IsWhiteSpace(character) && character != ' ') || char.IsSurrogate(character))
                    {
                        builder.Append(@"\u");
                        builder.Append(((int)character).ToString("x4", CultureInfo.InvariantCulture));
                    }
                    else
                    {
                        builder.Append(character);
                    }

                    break;
            }
        }

        return builder.Append('"').ToString();
    }
}
