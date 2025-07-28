// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// A collection of helper classes to test various conditions within
/// unit tests. If the condition being tested is not met, an exception
/// is thrown.
/// </summary>
public sealed partial class Assert
{
    private Assert()
    {
    }

    /// <summary>
    /// Gets the singleton instance of the Assert functionality.
    /// </summary>
    /// <remarks>
    /// Users can use this to plug-in custom assertions through C# extension methods.
    /// For instance, the signature of a custom assertion provider could be "public static void IsOfType&lt;T&gt;(this Assert assert, object obj)"
    /// Users could then use a syntax similar to the default assertions which in this case is "Assert.Instance.IsOfType&lt;Dog&gt;(animal);"
    /// More documentation is at "https://github.com/Microsoft/testfx/docs/README.md".
    /// </remarks>
    public static Assert Instance { get; } = new Assert();

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
    [StackTraceHidden]
    internal static void ThrowAssertFailed(string assertionName, string? message)
        => throw new AssertFailedException(
            string.Format(CultureInfo.CurrentCulture, FrameworkMessages.AssertionFailed, assertionName, ReplaceNulls(message)));

    /// <summary>
    /// Builds the formatted message using the given user format message and parameters.
    /// </summary>
    /// <param name="format">
    /// A composite format string.
    /// </param>
    /// <returns>
    /// The formatted string based on format and parameters.
    /// </returns>
    internal static string BuildUserMessage(string? format)
        => format is null
            ? FrameworkMessages.Common_NullInMessages.ToString()
            : ReplaceNullChars(format);

    private static string BuildUserMessageForSingleExpression(string? format, string callerArgExpression, string parameterName)
    {
        string userMessage = BuildUserMessage(format);
        if (string.IsNullOrEmpty(callerArgExpression))
        {
            return userMessage;
        }

        string callerArgMessagePart = string.Format(CultureInfo.InvariantCulture, FrameworkMessages.CallerArgumentExpressionSingleParameterMessage, parameterName, callerArgExpression);
        return string.IsNullOrEmpty(userMessage)
            ? callerArgMessagePart
            : $"{callerArgMessagePart} {userMessage}";
    }

    private static string BuildUserMessageForConditionExpression(string? format, string conditionExpression)
        => BuildUserMessageForSingleExpression(format, conditionExpression, "condition");

    private static string BuildUserMessageForValueExpression(string? format, string conditionExpression)
        => BuildUserMessageForSingleExpression(format, conditionExpression, "value");

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
    internal static void CheckParameterNotNull([NotNull] object? param, string assertionName, string parameterName, string? message)
    {
        if (param == null)
        {
            string userMessage = BuildUserMessage(message);
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

    private static int CompareInternal(string? expected, string? actual, bool ignoreCase, CultureInfo culture)
#pragma warning disable CA1309 // Use ordinal string comparison
        => string.Compare(expected, actual, ignoreCase, culture);
#pragma warning restore CA1309 // Use ordinal string comparison

    #region DoNotUse

    /// <summary>
    /// Static equals overloads are used for comparing instances of two types for reference
    /// equality. This method should <b>not</b> be used for comparison of two instances for
    /// equality. Please use Assert.AreEqual and associated overloads in your unit tests.
    /// </summary>
    /// <param name="objA"> Object A. </param>
    /// <param name="objB"> Object B. </param>
    /// <returns> Never returns. </returns>
    [SuppressMessage("Microsoft.Naming", "CA1720:IdentifiersShouldNotContainTypeNames", MessageId = "obj", Justification = "We want to compare 'object A' with 'object B', so it makes sense to have 'obj' in the parameter name")]
    [Obsolete(
        FrameworkConstants.DoNotUseAssertEquals,
#if DEBUG
        error: false)]
#else
        error: true)]
#endif
    [DoesNotReturn]
    public static new bool Equals(object? objA, object? objB)
    {
        Fail(FrameworkMessages.DoNotUseAssertEquals);
        return false;
    }

    /// <summary>
    /// Static ReferenceEquals overloads are used for comparing instances of two types for reference
    /// equality. This method should <b>not</b> be used for comparison of two instances for
    /// reference equality. Please use Assert.AreSame and associated overloads in your unit tests.
    /// </summary>
    /// <param name="objA"> Object A. </param>
    /// <param name="objB"> Object B. </param>
    /// <returns> Never returns. </returns>
    [SuppressMessage("Microsoft.Naming", "CA1720:IdentifiersShouldNotContainTypeNames", MessageId = "obj", Justification = "We want to compare 'object A' with 'object B', so it makes sense to have 'obj' in the parameter name")]
    [Obsolete(
        FrameworkConstants.DoNotUseAssertReferenceEquals,
#if DEBUG
        error: false)]
#else
        error: true)]
#endif
    [DoesNotReturn]
    public static new bool ReferenceEquals(object? objA, object? objB)
    {
        Fail(FrameworkMessages.DoNotUseAssertReferenceEquals);
        return false;
    }

    #endregion
}
