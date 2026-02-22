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

        string formattedMessage = string.Format(CultureInfo.CurrentCulture, FrameworkMessages.AssertionFailed, assertionName, message);

        // When there's no user message (message starts with newline for parameter details),
        // the format produces a trailing space before the newline ("failed. \\r\\n").
        // Remove it to avoid trailing whitespace on the first line.
        if (message is not null && message.StartsWith(Environment.NewLine, StringComparison.Ordinal))
        {
            formattedMessage = $"{assertionName} failed.{message}";
        }

        throw new AssertFailedException(formattedMessage);
    }

    private static bool ShouldLaunchDebugger()
        => AssertionFailureSettings.LaunchDebuggerOnAssertionFailure switch
        {
            DebuggerLaunchMode.Enabled => true,
            DebuggerLaunchMode.EnabledExcludingCI => !CIEnvironmentDetector.Instance.IsCIEnvironment(),
            _ => false,
        };

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

    internal static string FormatValue<T>(T? value, int maxLength = 256)
    {
        if (value is null)
        {
            return "(null)";
        }

        if (value is string s)
        {
            return EscapeNewlines(Truncate($"\"{s}\"", maxLength));
        }

        // For collections, show a preview with element values
        if (value is IEnumerable enumerable)
        {
            return FormatCollectionPreview(enumerable);
        }

        Type type = typeof(T);
        if (type == typeof(object))
        {
            // If the static type is object, use the runtime type
            type = value.GetType();
        }

        if (type.IsPrimitive || value is decimal or DateTime or DateTimeOffset
            or TimeSpan or Guid or Enum)
        {
            return EscapeNewlines(Truncate(value.ToString() ?? string.Empty, maxLength));
        }

        MethodInfo? toStringMethod = type.GetMethod(nameof(ToString), BindingFlags.Public | BindingFlags.Instance, null, Type.EmptyTypes, null);
        if (toStringMethod is not null
            && toStringMethod.DeclaringType != typeof(object)
            && toStringMethod.DeclaringType != typeof(ValueType))
        {
            try
            {
                return EscapeNewlines(Truncate(value.ToString() ?? string.Empty, maxLength));
            }
            catch (Exception)
            {
                // Fall through to type name display if ToString throws
            }
        }

        // No useful ToString - just return the type name
        string typeName = type.FullName ?? type.Name;
        return $"<{typeName}>";
    }

    internal static string TruncateExpression(string expression, int maxLength = 100)
        => expression.Length <= maxLength
            ? expression
#if NETCOREAPP3_1_OR_GREATER
            : string.Concat(expression.AsSpan(0, maxLength), "...");
#else
            : expression.Substring(0, maxLength) + "...";
#endif

    private static string Truncate(string value, int maxLength)
        => value.Length <= maxLength
            ? value
#if NETCOREAPP3_1_OR_GREATER
            : string.Concat(value.AsSpan(0, maxLength), "... (", value.Length.ToString(CultureInfo.InvariantCulture), " chars)");
#else
            : value.Substring(0, maxLength) + $"... ({value.Length} chars)";
#endif

    private static string EscapeNewlines(string value)
        => value.Contains('\n') || value.Contains('\r')
            ? value.Replace("\r\n", "\\r\\n").Replace("\n", "\\n").Replace("\r", "\\r")
            : value;

    private static bool IsExpressionRedundant(string expression, string formattedValue)
    {
        if (string.IsNullOrEmpty(expression))
        {
            return true;
        }

        // Exact match: expression "5" == formattedValue "5"
        if (expression == formattedValue)
        {
            return true;
        }

        // Null literal: expression "null" vs formattedValue "(null)"
        if (expression is "null" && formattedValue is "(null)")
        {
            return true;
        }

        // Boolean/true/false: expression "true" vs formattedValue "True"
        if (string.Equals(expression, formattedValue, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // Numeric literal in different notation (e.g., "100E-2" vs "1")
        if (double.TryParse(expression, NumberStyles.Any, CultureInfo.InvariantCulture, out double exprNum)
            && double.TryParse(formattedValue, NumberStyles.Any, CultureInfo.InvariantCulture, out double fmtNum)
            && exprNum == fmtNum)
        {
            return true;
        }

        // C# string literal expression: @"\d+" or "\d+" vs formattedValue \d+
        // Strip the string literal syntax and compare the inner content
        string? innerContent = TryExtractStringLiteralContent(expression);
        return innerContent is not null && innerContent == formattedValue;
    }

    /// <summary>
    /// Tries to extract the string content from a C# string literal expression.
    /// Returns the inner string value for @"..." and "..." literals, or null if not a string literal.
    /// </summary>
    private static string? TryExtractStringLiteralContent(string expression)
    {
        // Verbatim string: @"content"
        if (expression.Length >= 3 && expression[0] == '@' && expression[1] == '"' && expression[expression.Length - 1] == '"')
        {
            return expression.Substring(2, expression.Length - 3).Replace("\"\"", "\"");
        }

        // Regular string: "content"
        if (expression.Length >= 2 && expression[0] == '"' && expression[expression.Length - 1] == '"')
        {
            return expression.Substring(1, expression.Length - 2);
        }

        // Not a string literal
        return null;
    }

    /// <summary>
    /// Checks if the expression is a typed numeric literal (e.g., 1.0f, 1.1d, 0.001m, 2L)
    /// or a well-known numeric constant (float.NaN, double.NaN) that is a more informative
    /// representation than the plain ToString() value.
    /// </summary>
    private static bool IsExpressionMoreSpecificNumericLiteral(string expression, string formattedValue)
    {
        if (string.IsNullOrEmpty(expression) || expression.Length < 2)
        {
            return false;
        }

        // Well-known numeric constants: float.NaN, double.NaN, float.PositiveInfinity, etc.
        if (expression.StartsWith("float.", StringComparison.Ordinal) || expression.StartsWith("double.", StringComparison.Ordinal))
        {
            return true;
        }

        // Check if expression ends with a numeric type suffix
        char lastChar = expression[expression.Length - 1];
        if (lastChar is not ('f' or 'F' or 'd' or 'D' or 'm' or 'M' or 'L' or 'l' or 'u' or 'U'))
        {
            return false;
        }

        // The formatted value should be the numeric part without the suffix
        // e.g., expression "1.0d" -> formattedValue "1" or "1.0"
        string numericPart = expression.Substring(0, expression.Length - 1);

        // Handle UL/ul suffix (two chars)
        if (numericPart.Length > 0 && numericPart[numericPart.Length - 1] is 'u' or 'U' or 'l' or 'L')
        {
            numericPart = numericPart.Substring(0, numericPart.Length - 1);
        }

        // Check if removing the suffix gives the formatted value, or if they represent the same number
        return numericPart == formattedValue
            || (double.TryParse(numericPart, NumberStyles.Any, CultureInfo.InvariantCulture, out double exprNum)
                && double.TryParse(formattedValue, NumberStyles.Any, CultureInfo.InvariantCulture, out double valNum)
                && exprNum == valNum);
    }

    internal static string FormatParameter<T>(string paramName, string expression, T? value)
    {
        string formattedValue = FormatValue(value);

        // If expression is a typed numeric literal more specific than the value, use it as the display
        if (IsExpressionMoreSpecificNumericLiteral(expression, formattedValue))
        {
            return $"  {paramName}: {expression}";
        }

        // Skip expression when it matches the parameter name (e.g., "minValue (minValue): 5" → "minValue: 5")
        if (expression == paramName || IsExpressionRedundant(expression, formattedValue))
        {
            return $"  {paramName}: {formattedValue}";
        }

        // Default case: show both expression and value
        return $"  {paramName} ({TruncateExpression(expression)}): {formattedValue}";
    }

    /// <summary>
    /// Formats a parameter line showing only the expression (no value).
    /// Used for parameters like collections, predicates, and actions where the
    /// runtime value's ToString() is not useful.
    /// Returns empty string if the expression is empty or matches the parameter name (nothing useful to show).
    /// </summary>
    internal static string FormatExpressionParameter(string paramName, string expression)
        => string.IsNullOrEmpty(expression) || expression == paramName
            ? string.Empty
            : Environment.NewLine + $"  {paramName}: {TruncateExpression(expression)}";

    /// <summary>
    /// Formats a collection parameter line showing a preview of the collection elements.
    /// </summary>
    internal static string FormatCollectionParameter(string paramName, string expression, IEnumerable collection)
    {
        string preview = FormatCollectionPreview(collection);
        return string.IsNullOrEmpty(expression) || expression == paramName
            ? $"{Environment.NewLine}  {paramName}: {preview}"
            : $"{Environment.NewLine}  {paramName} ({TruncateExpression(expression)}): {preview}";
    }

    private static string FormatCollectionPreview(IEnumerable collection, int maxLength = 256)
    {
        var elements = new List<string>();
        int totalCount = 0;
        int currentLength = 0;
        bool truncated = false;

        foreach (object? item in collection)
        {
            totalCount++;
            if (truncated)
            {
                continue;
            }

            string formatted = item is IEnumerable innerCollection and not string
                ? FormatCollectionPreview(innerCollection, maxLength: 50)
                : FormatValue(item, maxLength: 50);

            // Account for ", " separator between elements
            int addedLength = elements.Count > 0
                ? formatted.Length + 2
                : formatted.Length;

            if (currentLength + addedLength > maxLength && elements.Count > 0)
            {
                truncated = true;
            }
            else
            {
                elements.Add(formatted);
                currentLength += addedLength;
            }
        }

        string elementList = string.Join(", ", elements);
        if (truncated)
        {
            elementList += ", ...";
        }

        return $"[{elementList}] ({totalCount} {(totalCount == 1 ? "element" : "elements")})";
    }

    internal static string FormatParameterWithValue(string paramName, string expression, string formattedValue)
        => (expression == paramName || IsExpressionRedundant(expression, formattedValue))
            ? $"  {paramName}: {formattedValue}"
            : $"  {paramName} ({TruncateExpression(expression)}): {formattedValue}";

    /// <summary>
    /// Formats a parameter line, checking expression redundancy against a base value
    /// while displaying a different (enriched) display value.
    /// </summary>
    internal static string FormatParameterWithExpressionCheck(string paramName, string expression, string baseValue, string displayValue)
        => (expression == paramName || IsExpressionRedundant(expression, baseValue))
            ? $"  {paramName}: {displayValue}"
            : $"  {paramName} ({TruncateExpression(expression)}): {displayValue}";

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
