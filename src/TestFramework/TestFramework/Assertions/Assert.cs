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

        string formattedMessage = string.IsNullOrEmpty(message)
            ? assertionName
            : assertionName + Environment.NewLine + message;

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
        if (param is null)
        {
            string finalMessage = string.Format(CultureInfo.CurrentCulture, FrameworkMessages.NullParameterToAssert, parameterName);
            ThrowAssertFailed(assertionName, finalMessage);
        }
    }

    internal static string ReplaceNulls(object? input)
        => input?.ToString() ?? "null";

    internal static string FormatValue<T>(T? value, int maxLength = 256)
    {
        if (value is null)
        {
            return "null";
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

        // Always use the runtime type for non-null values so that interface/base-class
        // typed parameters still resolve the actual overridden ToString().
        Type type = value.GetType();

        if (type.IsPrimitive || value is decimal or DateTime or DateTimeOffset
            or TimeSpan or Guid or Enum)
        {
            string formatted = EscapeNewlines(Truncate(
                Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty, maxLength));
            string suffix = GetNumericTypeSuffix(value);
            return suffix.Length > 0 ? formatted + suffix : formatted;
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
        return FormatType(type);
    }

    /// <summary>
    /// Returns the C# literal suffix for numeric types where the type
    /// is not the default for its category (int for integers, double for
    /// floating-point). Returns empty string for int, double, and
    /// non-numeric types.
    /// </summary>
    private static string GetNumericTypeSuffix<T>(T value) => value switch
    {
        long => "L",
        ulong => "UL",
        uint => "U",
        float => "f",
        decimal => "m",
        _ => string.Empty,
    };

    internal static string FormatType(Type type)
    {
        string typeName = type.FullName ?? type.Name;
        return $"<{typeName}>";
    }

    internal static string FormatValueWithType(object value)
    {
        string formattedValue = FormatValue(value);
        string formattedType = FormatType(value.GetType());

        // When FormatValue already returned the type name (e.g. <System.Object>),
        // don't repeat it.
        return formattedValue == formattedType
            ? formattedValue
            : $"{formattedValue} ({formattedType})";
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
            : string.Concat(value.AsSpan(0, maxLength), "... ", (value.Length - maxLength).ToString(CultureInfo.InvariantCulture), " more");
#else
            : value.Substring(0, maxLength) + $"... {value.Length - maxLength} more";
#endif

    private static string EscapeNewlines(string value)
        => value.Contains('\n') || value.Contains('\r')
            ? value.Replace("\r\n", "\\r\\n").Replace("\n", "\\n").Replace("\r", "\\r")
            : value;

    internal static string FormatParameter<T>(string paramName, string expression, T? value)
    {
        string formattedValue = FormatValue(value);

        return $"  {paramName}: {formattedValue}";
    }

    /// <summary>
    /// Prepends the user-provided message before the assertion explanation.
    /// Returns the message unchanged if userMessage is null or empty.
    /// </summary>
    internal static string AppendUserMessage(string message, string? userMessage)
        => string.IsNullOrEmpty(userMessage)
            ? message
            : userMessage + Environment.NewLine + message;

    /// <summary>
    /// Formats a parameter line showing only the expression (no value).
    /// Used for parameters like predicates and actions where the
    /// runtime value's ToString() is not useful.
    /// Returns empty string if the expression is empty or matches the parameter name.
    /// </summary>
    internal static string FormatExpressionParameter(string paramName, string expression)
        => string.IsNullOrEmpty(expression) || expression == paramName
            ? string.Empty
            : Environment.NewLine + $"  {paramName}: {TruncateExpression(expression)}";

    /// <summary>
    /// Formats a collection parameter line showing a preview of the collection elements.
    /// Callers must ensure the collection is safe to enumerate (e.g. materialized via
    /// snapshot at the assertion boundary). Non-ICollection enumerables are shown as their
    /// type name as a safety net.
    /// </summary>
    internal static string FormatCollectionParameter(string expression, IEnumerable collection)
    {
        // Safety net: callers should materialize non-ICollection enumerables before
        // reaching here, but if they don't, fall back to the type name rather than
        // risk re-enumerating a non-deterministic or exhausted enumerator.
        if (collection is not ICollection)
        {
            return $"{Environment.NewLine}  collection: {FormatType(collection.GetType())}";
        }

        string preview = FormatCollectionPreview(collection);

        return $"{Environment.NewLine}  collection: {preview}";
    }

    /// <summary>
    /// Formats a preview string for a collection, showing element values up to <paramref name="maxLength"/> characters.
    /// <para>
    /// Performance: We avoid enumerating the entire collection when the display is truncated.
    /// For ICollection, we read .Count directly (O(1)) to get the total without full enumeration.
    /// For non-ICollection enumerables (e.g. LINQ queries, infinite sequences), we stop
    /// enumeration as soon as the display budget is exhausted and report "N+ elements" since
    /// the true count is unknown. This prevents hangs on lazy/infinite sequences and avoids
    /// O(n) enumeration cost when only a prefix is displayed.
    /// </para>
    /// </summary>
    private static string FormatCollectionPreview(IEnumerable collection, int maxLength = 256)
    {
        // Perf: get count from ICollection (O(1)) to avoid full enumeration just for the count.
        int? knownCount = collection is ICollection c ? c.Count : null;

        var elements = new List<string>();
        int enumeratedCount = 0;
        int currentLength = 0;
        bool truncated = false;
        bool failedEnumeration = false;

        // Perf: wrap in try-catch so that faulting enumerators (e.g. collection modified during
        // iteration, or user-defined iterators that throw) don't bubble up from assertion formatting.
        try
        {
            foreach (object? item in collection)
            {
                enumeratedCount++;

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

                    // Perf: stop enumeration immediately once the display budget is exceeded.
                    // Without this break, we'd continue iterating potentially millions of
                    // elements (or hang on infinite sequences) just to compute totalCount.
                    break;
                }

                elements.Add(formatted);
                currentLength += addedLength;
            }
        }
        catch (Exception)
        {
            // If enumeration fails, report what we've collected so far.
            // Only mark as truncated if we actually collected some elements,
            // and rely on the failedEnumeration flag to handle the suffix.
            failedEnumeration = true;
        }

        int totalCount = knownCount ?? enumeratedCount;
        int displayedCount = elements.Count;

        string elementList = string.Join(", ", elements);
        if (truncated || (failedEnumeration && displayedCount > 0))
        {
            int remaining = totalCount - displayedCount;
            if (failedEnumeration)
            {
                elementList += ", ...";
            }
            else if (remaining > 0)
            {
                string remainingText = knownCount is null
                    ? $"{remaining}+"
                    : $"{remaining}";
                elementList += $", ... {remainingText} more";
            }
        }

        return $"[{elementList}]";
    }

    /// <summary>
    /// A simple name-value pair used by <see cref="FormatCallSite"/> and <see cref="FormatAlignedParameters"/>
    /// to avoid relying on System.ValueTuple which is not available on net462.
    /// </summary>
    internal readonly struct StringPair
    {
        public StringPair(string name, string value)
        {
            Name = name;
            Value = value;
        }

        public string Name { get; }

        public string Value { get; }
    }

    /// <summary>
    /// Builds the "Assert.Method(expr1, expr2)" call site string for the first line.
    /// Only the primary/semantic parameters are included (not message, culture, delta, etc.).
    /// </summary>
    internal static string FormatCallSite(string methodName, params StringPair[] args)
    {
        var sb = new StringBuilder(methodName);
        sb.Append('(');
        bool hasVisibleArgs = false;
        for (int i = 0; i < args.Length; i++)
        {
            string expression = args[i].Value;
            string paramName = args[i].Name;

            // Sentinel "..." indicates additional parameters were omitted.
            if (paramName == "...")
            {
                if (hasVisibleArgs)
                {
                    sb.Append(", ");
                }

                sb.Append("...");
                continue;
            }

            if (hasVisibleArgs)
            {
                sb.Append(", ");
            }

            sb.Append(string.IsNullOrEmpty(expression) || expression == paramName
                ? paramName
                : TruncateExpression(expression, 50));
            hasVisibleArgs = true;
        }

        sb.Append(')');

        return sb.ToString();
    }

    /// <summary>
    /// Formats multiple parameter lines with aligned values.
    /// All labels are padded so that values start at the same column.
    /// </summary>
    internal static string FormatAlignedParameters(params StringPair[] parameters)
    {
        int maxLabelLength = 0;
        for (int i = 0; i < parameters.Length; i++)
        {
            if (parameters[i].Name.Length > maxLabelLength)
            {
                maxLabelLength = parameters[i].Name.Length;
            }
        }

        var sb = new StringBuilder();
        for (int i = 0; i < parameters.Length; i++)
        {
            sb.Append(Environment.NewLine);
            sb.Append("  ");
            sb.Append(parameters[i].Name);
            sb.Append(':');
            sb.Append(new string(' ', maxLabelLength - parameters[i].Name.Length + 1));
            sb.Append(parameters[i].Value);
        }

        return sb.ToString();
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
