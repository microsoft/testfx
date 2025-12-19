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
    /// Users could then use a syntax similar to the default assertions which in this case is "Assert.That.IsOfType&lt;Dog&gt;(animal);"
    /// More documentation is at "https://github.com/Microsoft/testfx/docs/README.md".
    /// </remarks>
    public static Assert That { get; } = new();

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
            string.Format(CultureInfo.CurrentCulture, FrameworkMessages.AssertionFailed, assertionName, message));

    /// <summary>
    /// Helper function that creates and throws an AssertionFailedException with expected and actual values.
    /// </summary>
    /// <typeparam name="T">
    /// The type of the expected and actual values.
    /// </typeparam>
    /// <param name="assertionName">
    /// name of the assertion throwing an exception.
    /// </param>
    /// <param name="message">
    /// The assertion failure message.
    /// </param>
    /// <param name="expected">
    /// Expected value to store in exception data.
    /// </param>
    /// <param name="actual">
    /// Actual value to store in exception data.
    /// </param>
    [DoesNotReturn]
    [StackTraceHidden]
    internal static void ThrowAssertFailed<T>(string assertionName, string? message, T? expected = default, T? actual = default)
    {
        AssertFailedException exception = new(
            string.Format(CultureInfo.CurrentCulture, FrameworkMessages.AssertionFailed, assertionName, message));

        // Store expected and actual values in exception Data for types with known good ToString implementations
        if (HasKnownGoodToString(expected))
        {
            exception.Data["assert.expected"] = expected;
        }

        if (HasKnownGoodToString(actual))
        {
            exception.Data["assert.actual"] = actual;
        }

        throw exception;
    }

    private static bool HasKnownGoodToString<T>([NotNullWhen(true)] T? value)
    {
        if (value is null)
        {
            return false;
        }

        Type type = typeof(T);

        // Unwrap nullable value types
        type = Nullable.GetUnderlyingType(type) ?? type;

        // Primitive types and string
        if (type.IsPrimitive || type == typeof(string))
        {
            return true;
        }

        // Common types with good ToString implementations
        return type == typeof(decimal)
            || type == typeof(DateTime)
            || type == typeof(DateTimeOffset)
            || type == typeof(TimeSpan)
            || type == typeof(Guid)
            || type == typeof(Uri)
            || type.IsEnum
            || typeof(Exception).IsAssignableFrom(type);
    }

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
        => format ?? string.Empty;

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

    private static string BuildUserMessageForTwoExpressions(string? format, string callerArgExpression1, string parameterName1, string callerArgExpression2, string parameterName2)
    {
        string userMessage = BuildUserMessage(format);
        if (string.IsNullOrEmpty(callerArgExpression1) || string.IsNullOrEmpty(callerArgExpression2))
        {
            return userMessage;
        }

        string callerArgMessagePart = string.Format(CultureInfo.InvariantCulture, FrameworkMessages.CallerArgumentExpressionTwoParametersMessage, parameterName1, callerArgExpression1, parameterName2, callerArgExpression2);
        return string.IsNullOrEmpty(userMessage)
            ? callerArgMessagePart
            : $"{callerArgMessagePart} {userMessage}";
    }

    private static string BuildUserMessageForThreeExpressions(string? format, string callerArgExpression1, string parameterName1, string callerArgExpression2, string parameterName2, string callerArgExpression3, string parameterName3)
    {
        string userMessage = BuildUserMessage(format);
        if (string.IsNullOrEmpty(callerArgExpression1) || string.IsNullOrEmpty(callerArgExpression2) || string.IsNullOrEmpty(callerArgExpression3))
        {
            return userMessage;
        }

        string callerArgMessagePart = string.Format(CultureInfo.InvariantCulture, FrameworkMessages.CallerArgumentExpressionThreeParametersMessage, parameterName1, callerArgExpression1, parameterName2, callerArgExpression2, parameterName3, callerArgExpression3);
        return string.IsNullOrEmpty(userMessage)
            ? callerArgMessagePart
            : $"{callerArgMessagePart} {userMessage}";
    }

    private static string BuildUserMessageForConditionExpression(string? format, string conditionExpression)
        => BuildUserMessageForSingleExpression(format, conditionExpression, "condition");

    private static string BuildUserMessageForValueExpression(string? format, string valueExpression)
        => BuildUserMessageForSingleExpression(format, valueExpression, "value");

    private static string BuildUserMessageForActionExpression(string? format, string actionExpression)
        => BuildUserMessageForSingleExpression(format, actionExpression, "action");

    private static string BuildUserMessageForCollectionExpression(string? format, string collectionExpression)
        => BuildUserMessageForSingleExpression(format, collectionExpression, "collection");

    private static string BuildUserMessageForSubstringExpressionAndValueExpression(string? format, string substringExpression, string valueExpression)
        => BuildUserMessageForTwoExpressions(format, substringExpression, "substring", valueExpression, "value");

    private static string BuildUserMessageForExpectedSuffixExpressionAndValueExpression(string? format, string expectedSuffixExpression, string valueExpression)
        => BuildUserMessageForTwoExpressions(format, expectedSuffixExpression, "expectedSuffix", valueExpression, "value");

    private static string BuildUserMessageForNotExpectedSuffixExpressionAndValueExpression(string? format, string notExpectedSuffixExpression, string valueExpression)
        => BuildUserMessageForTwoExpressions(format, notExpectedSuffixExpression, "notExpectedSuffix", valueExpression, "value");

    private static string BuildUserMessageForExpectedPrefixExpressionAndValueExpression(string? format, string expectedPrefixExpression, string valueExpression)
        => BuildUserMessageForTwoExpressions(format, expectedPrefixExpression, "expectedPrefix", valueExpression, "value");

    private static string BuildUserMessageForNotExpectedPrefixExpressionAndValueExpression(string? format, string notExpectedPrefixExpression, string valueExpression)
        => BuildUserMessageForTwoExpressions(format, notExpectedPrefixExpression, "notExpectedPrefix", valueExpression, "value");

    private static string BuildUserMessageForPatternExpressionAndValueExpression(string? format, string patternExpression, string valueExpression)
        => BuildUserMessageForTwoExpressions(format, patternExpression, "pattern", valueExpression, "value");

    private static string BuildUserMessageForLowerBoundExpressionAndValueExpression(string? format, string lowerBoundExpression, string valueExpression)
        => BuildUserMessageForTwoExpressions(format, lowerBoundExpression, "lowerBound", valueExpression, "value");

    private static string BuildUserMessageForUpperBoundExpressionAndValueExpression(string? format, string upperBoundExpression, string valueExpression)
        => BuildUserMessageForTwoExpressions(format, upperBoundExpression, "upperBound", valueExpression, "value");

    private static string BuildUserMessageForExpectedExpressionAndCollectionExpression(string? format, string expectedExpression, string collectionExpression)
        => BuildUserMessageForTwoExpressions(format, expectedExpression, "expected", collectionExpression, "collection");

    private static string BuildUserMessageForNotExpectedExpressionAndCollectionExpression(string? format, string notExpectedExpression, string collectionExpression)
        => BuildUserMessageForTwoExpressions(format, notExpectedExpression, "notExpected", collectionExpression, "collection");

    private static string BuildUserMessageForPredicateExpressionAndCollectionExpression(string? format, string predicateExpression, string collectionExpression)
        => BuildUserMessageForTwoExpressions(format, predicateExpression, "predicate", collectionExpression, "collection");

    private static string BuildUserMessageForExpectedExpressionAndActualExpression(string? format, string expectedExpression, string actualExpression)
        => BuildUserMessageForTwoExpressions(format, expectedExpression, "expected", actualExpression, "actual");

    private static string BuildUserMessageForNotExpectedExpressionAndActualExpression(string? format, string notExpectedExpression, string actualExpression)
        => BuildUserMessageForTwoExpressions(format, notExpectedExpression, "notExpected", actualExpression, "actual");

    private static string BuildUserMessageForMinValueExpressionAndMaxValueExpressionAndValueExpression(string? format, string minValueExpression, string maxValueExpression, string valueExpression)
        => BuildUserMessageForThreeExpressions(format, minValueExpression, "minValue", maxValueExpression, "maxValue", valueExpression, "value");

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
    internal static void CheckParameterNotNull([NotNull] object? param, string assertionName, string parameterName)
    {
        if (param == null)
        {
            string finalMessage = string.Format(CultureInfo.CurrentCulture, FrameworkMessages.NullParameterToAssert, parameterName);
            ThrowAssertFailed(assertionName, finalMessage);
        }
    }

    internal static string ReplaceNulls(object? input)
        => input?.ToString() ?? string.Empty;

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
