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
    /// Reports an assertion failure and always throws, even within an <see cref="AssertScope"/>.
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
    {
        LaunchDebuggerIfNeeded();
        throw CreateAssertFailedException(assertionName, message);
    }

    /// <summary>
    /// Reports an assertion failure. Within an <see cref="AssertScope"/>, the failure is collected
    /// and execution continues. Outside a scope, the failure is thrown immediately.
    /// </summary>
    /// <param name="assertionName">
    /// name of the assertion throwing an exception.
    /// </param>
    /// <param name="message">
    /// The assertion failure message.
    /// </param>
#pragma warning disable CS8763 // A method marked [DoesNotReturn] should not return - Deliberately keeping [DoesNotReturn] annotation while using soft assertions. Within an AssertScope, the postcondition is not enforced (same as all other assertion postconditions in scoped mode).
    [DoesNotReturn]
    [StackTraceHidden]
    internal static void ReportAssertFailed(string assertionName, string? message)
    {
        LaunchDebuggerIfNeeded();
        AssertFailedException assertionFailedException = CreateAssertFailedException(assertionName, message);
        if (AssertScope.Current is { } scope)
        {
            // Throw and catch to capture the stack trace at the point of failure,
            // so the exception has a meaningful stack trace when reported from the scope.
            try
            {
                throw assertionFailedException;
            }
            catch (AssertFailedException ex)
            {
                assertionFailedException = ex;
            }

            scope.AddError(assertionFailedException);
            return;
        }

        throw assertionFailedException;
    }
#pragma warning restore CS8763 // A method marked [DoesNotReturn] should not return

    private static void LaunchDebuggerIfNeeded()
    {
        if (ShouldLaunchDebugger())
        {
            if (Debugger.IsAttached)
            {
                Debugger.Break();
            }
            else
            {
                Debugger.Launch();
            }
        }

        // Local functions
        static bool ShouldLaunchDebugger()
            => AssertionFailureSettings.LaunchDebuggerOnAssertionFailure switch
            {
                DebuggerLaunchMode.Enabled => true,
                DebuggerLaunchMode.EnabledExcludingCI => !CIEnvironmentDetector.Instance.IsCIEnvironment(),
                _ => false,
            };
    }

    private static AssertFailedException CreateAssertFailedException(string assertionName, string? message)
        => new(FormatAssertionFailed(assertionName, message));

    private static AssertFailedException CreateAssertFailedException(StructuredAssertionMessage structuredMessage)
    {
        AssertFailedException exception = new(structuredMessage.Format())
        {
            ExpectedText = structuredMessage.ExpectedText,
            ActualText = structuredMessage.ActualText,
        };

        if (structuredMessage.ExpectedText is not null)
        {
            exception.Data["assert.expected"] = structuredMessage.ExpectedText;
        }

        if (structuredMessage.ActualText is not null)
        {
            exception.Data["assert.actual"] = structuredMessage.ActualText;
        }

        return exception;
    }

    /// <summary>
    /// Reports an assertion failure using a structured message. Within an <see cref="AssertScope"/>,
    /// the failure is collected and execution continues. Outside a scope, the failure is thrown immediately.
    /// </summary>
    /// <param name="structuredMessage">
    /// The structured assertion failure message.
    /// </param>
#pragma warning disable CS8763 // A method marked [DoesNotReturn] should not return - Deliberately keeping [DoesNotReturn] annotation while using soft assertions. Within an AssertScope, the postcondition is not enforced (same as all other assertion postconditions in scoped mode).
    [DoesNotReturn]
    [StackTraceHidden]
    internal static void ReportAssertFailed(StructuredAssertionMessage structuredMessage)
    {
        LaunchDebuggerIfNeeded();
        AssertFailedException assertionFailedException = CreateAssertFailedException(structuredMessage);
        if (AssertScope.Current is { } scope)
        {
            try
            {
                throw assertionFailedException;
            }
            catch (AssertFailedException ex)
            {
                assertionFailedException = ex;
            }

            scope.AddError(assertionFailedException);
            return;
        }

        throw assertionFailedException;
    }
#pragma warning restore CS8763 // A method marked [DoesNotReturn] should not return

    /// <summary>
    /// Reports an assertion failure using a structured message and always throws,
    /// even within an <see cref="AssertScope"/>.
    /// </summary>
    /// <param name="structuredMessage">
    /// The structured assertion failure message.
    /// </param>
    [DoesNotReturn]
    [StackTraceHidden]
    internal static void ThrowAssertFailed(StructuredAssertionMessage structuredMessage)
    {
        LaunchDebuggerIfNeeded();
        throw CreateAssertFailedException(structuredMessage);
    }

    /// <summary>
    /// Formats a call-site expression for display at the bottom of a structured assertion message.
    /// When the expression is empty, the call-site is omitted. When the expression contains newlines,
    /// it is replaced with the supplied placeholder (either a full <c>&lt;placeholder&gt;</c> or a raw parameter name).
    /// </summary>
    internal static string? FormatCallSiteExpression(string assertionMethodName, string expression, string placeholderOrParamName = "<value>")
    {
        if (string.IsNullOrWhiteSpace(expression))
        {
            return null;
        }

        string arg = IsMultiline(expression) ? NormalizeCallSitePlaceholder(placeholderOrParamName) : expression;
        return $"{assertionMethodName}({arg})";
    }

    /// <summary>
    /// Formats a call-site expression for display at the bottom of a structured assertion message,
    /// using two captured expressions. Multiline expressions are replaced with the supplied placeholders.
    /// When only one expression is empty/whitespace, its placeholder is used so the partial call site is still shown;
    /// only when both expressions are empty/whitespace is the entire call-site line suppressed.
    /// </summary>
    internal static string? FormatCallSiteExpression(string assertionMethodName, string expression1, string expression2, string placeholder1 = "<arg1>", string placeholder2 = "<arg2>")
    {
        bool empty1 = string.IsNullOrWhiteSpace(expression1);
        bool empty2 = string.IsNullOrWhiteSpace(expression2);
        if (empty1 && empty2)
        {
            return null;
        }

        string arg1 = empty1 || IsMultiline(expression1) ? NormalizeCallSitePlaceholder(placeholder1) : expression1;
        string arg2 = empty2 || IsMultiline(expression2) ? NormalizeCallSitePlaceholder(placeholder2) : expression2;

        return $"{assertionMethodName}({arg1}, {arg2})";
    }

    // string.Contains(char) is not available on netstandard2.0 / net462, so use IndexOf to check for newline characters.
    private static bool IsMultiline(string expression)
        => expression.IndexOf('\n') >= 0 || expression.IndexOf('\r') >= 0;

    private static string NormalizeCallSitePlaceholder(string placeholderOrParamName)
        => placeholderOrParamName.Length > 1 && placeholderOrParamName[0] == '<' && placeholderOrParamName[placeholderOrParamName.Length - 1] == '>'
            ? placeholderOrParamName
            : $"<{placeholderOrParamName}>";

    private static string FormatAssertionFailed(string assertionName, string? message)
    {
        string failedMessage = string.Format(CultureInfo.CurrentCulture, FrameworkMessages.AssertionFailed, assertionName);
        return string.IsNullOrWhiteSpace(message)
            ? failedMessage
            : message![0] is '\n' or '\r'
                ? string.Concat(failedMessage, message)
                : $"{failedMessage} {message}";
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

    private static string BuildUserMessageForCollectionExpression(string? format, string collectionExpression)
        => BuildUserMessageForSingleExpression(format, collectionExpression, "collection");

    private static string BuildUserMessageForSubstringExpressionAndValueExpression(string? format, string substringExpression, string valueExpression)
        => BuildUserMessageForTwoExpressions(format, substringExpression, "substring", valueExpression, "value");

    private static string BuildUserMessageForPatternExpressionAndValueExpression(string? format, string patternExpression, string valueExpression)
        => BuildUserMessageForTwoExpressions(format, patternExpression, "pattern", valueExpression, "value");

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
        if (param is null)
        {
            string finalMessage = string.Format(CultureInfo.CurrentCulture, FrameworkMessages.NullParameterToAssert, parameterName);
            throw CreateAssertFailedException(assertionName, finalMessage);
        }
    }

    internal static string ReplaceNulls(object? input)
        => input?.ToString() ?? string.Empty;

    /// <summary>
    /// Formats a call-site expression like <c>Assert.MethodName(expression)</c>.
    /// Returns <see langword="null"/> if the expression is empty or contains a line break.
    /// </summary>
    private static string? FormatCallSiteExpression(string methodName, string expression)
        => string.IsNullOrEmpty(expression) || expression.IndexOfAny(['\n', '\r']) >= 0
            ? null
            : $"{methodName}({expression})";

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
#if DEBUG && NET8_0_OR_GREATER
    [Obsolete(
        FrameworkConstants.DoNotUseAssertEquals,
        error: false,
        DiagnosticId = "MSTEST0100",
        UrlFormat = "https://aka.ms/mstest/diagnostics#{0}")]
#elif DEBUG
    [Obsolete(
        FrameworkConstants.DoNotUseAssertEquals,
        error: false)]
#elif NET8_0_OR_GREATER
    [Obsolete(
        FrameworkConstants.DoNotUseAssertEquals,
        error: true,
        DiagnosticId = "MSTEST0100",
        UrlFormat = "https://aka.ms/mstest/diagnostics#{0}")]
#else
    [Obsolete(
        FrameworkConstants.DoNotUseAssertEquals,
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
#if DEBUG && NET8_0_OR_GREATER
    [Obsolete(
        FrameworkConstants.DoNotUseAssertReferenceEquals,
        error: false,
        DiagnosticId = "MSTEST0101",
        UrlFormat = "https://aka.ms/mstest/diagnostics#{0}")]
#elif DEBUG
    [Obsolete(
        FrameworkConstants.DoNotUseAssertReferenceEquals,
        error: false)]
#elif NET8_0_OR_GREATER
    [Obsolete(
        FrameworkConstants.DoNotUseAssertReferenceEquals,
        error: true,
        DiagnosticId = "MSTEST0101",
        UrlFormat = "https://aka.ms/mstest/diagnostics#{0}")]
#else
    [Obsolete(
        FrameworkConstants.DoNotUseAssertReferenceEquals,
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
