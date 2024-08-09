// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// A collection of helper classes to test various conditions within
/// unit tests. If the condition being tested is not met, an exception
/// is thrown.
/// </summary>
public static partial class Assert
{
    /// <summary>
    /// Gets the singleton instance of the Assert functionality.
    /// </summary>
    /// <remarks>
    /// Users can use this to plug-in custom assertions through C# extension methods.
    /// For instance, the signature of a custom assertion provider could be "public static void IsOfType&lt;T&gt;(this Assert assert, object obj)"
    /// Users could then use a syntax similar to the default assertions which in this case is "Assert.That.IsOfType&lt;Dog&gt;(animal);"
    /// More documentation is at "https://github.com/Microsoft/testfx/docs/README.md".
    /// </remarks>
    public static Assert That { get; } = new Assert();

    /// <summary>
    /// Replaces null characters ('\0') with "\\0".
    /// </summary>
    /// <param name="input">
    /// The string to search.
    /// </param>
    /// <returns>
    /// The converted string with null characters replaced by "\\0".
    /// </returns>
    /// <remarks>
    /// This is only public and still present to preserve compatibility with the V1 framework.
    /// </remarks>
    [return: NotNullIfNotNull(nameof(input))]
    public static string? ReplaceNullChars(string? input)
        => StringEx.IsNullOrEmpty(input)
            ? input
            : input.Replace("\0", "\\0");

    /// <summary>
    /// Helper function that creates and throws an AssertionFailedException.
    /// </summary>
    /// <param name="assertionName">
    /// name of the assertion throwing an exception.
    /// </param>
    /// <param name="message">
    /// The assertion failure message.
    /// </param>
    [DoesNotReturn]
    internal static void ThrowAssertFailed(string assertionName, string? message)
        => throw new AssertFailedException(
            string.Format(CultureInfo.CurrentCulture, FrameworkMessages.AssertionFailed, assertionName, ReplaceNulls(message)));

    /// <summary>
    /// Builds the formatted message using the given user format message and parameters.
    /// </summary>
    /// <param name="format">
    /// A composite format string.
    /// </param>
    /// <param name="parameters">
    /// An object array that contains zero or more objects to format.
    /// </param>
    /// <returns>
    /// The formatted string based on format and parameters.
    /// </returns>
    internal static string BuildUserMessage(string? format, params object?[]? parameters)
        => format is null
            ? ReplaceNulls(format)
            : format.Length == 0
                ? string.Empty
                : parameters == null || parameters.Length == 0
                    ? ReplaceNulls(format)
                    : string.Format(CultureInfo.CurrentCulture, ReplaceNulls(format), parameters);

    /// <summary>
    /// Checks the parameter for valid conditions.
    /// </summary>
    /// <param name="param">
    /// The parameter.
    /// </param>
    /// <param name="assertionName">
    /// The assertion Name.
    /// </param>
    /// <param name="parameterName">
    /// parameter name.
    /// </param>
    /// <param name="message">
    /// message for the invalid parameter exception.
    /// </param>
    /// <param name="parameters">
    /// The parameters.
    /// </param>
    internal static void CheckParameterNotNull([NotNull] object? param, string assertionName, string parameterName,
        [StringSyntax(StringSyntaxAttribute.CompositeFormat)] string? message, params object?[]? parameters)
    {
        if (param == null)
        {
            string userMessage = BuildUserMessage(message, parameters);
            string finalMessage = string.Format(CultureInfo.CurrentCulture, FrameworkMessages.NullParameterToAssert, parameterName, userMessage);
            ThrowAssertFailed(assertionName, finalMessage);
        }
    }

    /// <summary>
    /// Safely converts an object to a string, handling null values and null characters.
    /// Null values are converted to "(null)". Null characters are converted to "\\0".
    /// </summary>
    /// <param name="input">
    /// The object to convert to a string.
    /// </param>
    /// <returns>
    /// The converted string.
    /// </returns>
    [SuppressMessage("ReSharper", "RedundantToStringCall", Justification = "We are ensuring ToString() isn't overloaded in a way to misbehave")]
    [return: NotNull]
    internal static string ReplaceNulls(object? input)
    {
        // Use the localized "(null)" string for null values.
        if (input == null)
        {
            return FrameworkMessages.Common_NullInMessages.ToString();
        }

        // Convert it to a string.
        string? inputString = input.ToString();

        // Make sure the class didn't override ToString and return null.
        return inputString == null ? FrameworkMessages.Common_ObjectString.ToString() : ReplaceNullChars(inputString);
    }

    private static int CompareInternal(string? expected, string? actual, bool ignoreCase, CultureInfo? culture)
#pragma warning disable CA1309 // Use ordinal string comparison
        => string.Compare(expected, actual, ignoreCase, culture);
#pragma warning restore CA1309 // Use ordinal string comparison

    #region EqualsAssertion

    /// <summary>
    /// Static equals overloads are used for comparing instances of two types for reference
    /// equality. This method should <b>not</b> be used for comparison of two instances for
    /// equality. This object will <b>always</b> throw with Assert.Fail. Please use
    /// Assert.AreEqual and associated overloads in your unit tests.
    /// </summary>
    /// <param name="objA"> Object A. </param>
    /// <param name="objB"> Object B. </param>
    /// <returns> False, always. </returns>
    [SuppressMessage("Microsoft.Naming", "CA1720:IdentifiersShouldNotContainTypeNames", MessageId = "obj", Justification = "We want to compare 'object A' with 'object B', so it makes sense to have 'obj' in the parameter name")]
#pragma warning disable IDE0060 // Remove unused parameter
    public static new bool Equals(object? objA, object? objB)
#pragma warning restore IDE0060 // Remove unused parameter
    {
        Fail(FrameworkMessages.DoNotUseAssertEquals);
        return false;
    }
    #endregion
}
