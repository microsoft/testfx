// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.ComponentModel;

namespace Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// A collection of helper classes to test various conditions within
/// unit tests. If the condition being tested is not met, an exception
/// is thrown.
/// </summary>
public sealed partial class Assert
{
    /// <summary>
    /// Provides an interpolated string handler used by <c>Assert.AreEqual</c> overloads
    /// that only allocates and formats the message when the assertion is failing.
    /// </summary>
    /// <typeparam name="TArgument">The type of value being asserted.</typeparam>
    /// <remarks>
    /// This type is intended to be used by the compiler; users should not reference it directly.
    /// </remarks>
    [StackTraceHidden]
    [InterpolatedStringHandler]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public readonly struct AssertAreEqualInterpolatedStringHandler<TArgument>
    {
        private readonly StringBuilder? _builder;
        private readonly object? _expected;
        private readonly object? _actual;

        /// <summary>
        /// Initializes a new instance of the <see cref="AssertAreEqualInterpolatedStringHandler{TArgument}"/> struct.
        /// </summary>
        /// <param name="literalLength">The number of constant characters in the interpolated string.</param>
        /// <param name="formattedCount">The number of interpolation expressions in the interpolated string.</param>
        /// <param name="expected">The expected value being asserted.</param>
        /// <param name="actual">The actual value being asserted.</param>
        /// <param name="shouldAppend">When this method returns, indicates whether the interpolated string should be evaluated.</param>
        public AssertAreEqualInterpolatedStringHandler(int literalLength, int formattedCount, TArgument? expected, TArgument? actual, out bool shouldAppend)
        {
            _expected = expected!;
            shouldAppend = AreEqualFailing(expected, actual, null);
            if (shouldAppend)
            {
                _builder = new StringBuilder(literalLength + formattedCount);
                _expected = expected;
                _actual = actual;
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="AssertAreEqualInterpolatedStringHandler{TArgument}"/> struct.
        /// </summary>
        /// <param name="literalLength">The number of constant characters in the interpolated string.</param>
        /// <param name="formattedCount">The number of interpolation expressions in the interpolated string.</param>
        /// <param name="expected">The expected value being asserted.</param>
        /// <param name="actual">The actual value being asserted.</param>
        /// <param name="comparer">The equality comparer used to compare values.</param>
        /// <param name="shouldAppend">When this method returns, indicates whether the interpolated string should be evaluated.</param>
        public AssertAreEqualInterpolatedStringHandler(int literalLength, int formattedCount, TArgument? expected, TArgument? actual, IEqualityComparer<TArgument>? comparer, out bool shouldAppend)
        {
            shouldAppend = AreEqualFailing(expected, actual, comparer);
            if (shouldAppend)
            {
                _builder = new StringBuilder(literalLength + formattedCount);
                _expected = expected;
                _actual = actual;
            }
        }

        internal void ComputeAssertion(string expectedExpression, string actualExpression)
        {
            if (_builder is not null)
            {
                ReportAssertAreEqualFailed(_expected, _actual, _builder.ToString(), expectedExpression, actualExpression);
            }
        }

        /// <summary>Appends a literal string to the interpolated message.</summary>
        /// <param name="value">The literal string to append.</param>
        public void AppendLiteral(string? value) => _builder!.Append(value);

        /// <summary>Appends a formatted value to the interpolated message.</summary>
        /// <typeparam name="T">The type of the value being appended.</typeparam>
        /// <param name="value">The value to append.</param>
        public void AppendFormatted<T>(T value) => AppendFormatted(value, format: null);

#if NETCOREAPP3_1_OR_GREATER

        /// <summary>Appends a formatted value to the interpolated message.</summary>
        /// <param name="value">The value to append.</param>
        public void AppendFormatted(ReadOnlySpan<char> value) => _builder!.Append(value);

#pragma warning disable RS0027 // API with optional parameter(s) should have the most parameters amongst its public overloads

        /// <summary>Appends a formatted value to the interpolated message.</summary>
        /// <param name="value">The character span to append.</param>
        /// <param name="alignment">The minimum width of the formatted value.</param>
        /// <param name="format">The format string to use.</param>
        public void AppendFormatted(ReadOnlySpan<char> value, int alignment = 0, string? format = null) => AppendFormatted(value.ToString(), alignment, format);
#pragma warning restore RS0027 // API with optional parameter(s) should have the most parameters amongst its public overloads
#endif

        // NOTE: All the overloads involving format and/or alignment are not super efficient.
        // This code path is only for when an assert is failing, so that's not the common scenario
        // and should be okay if not very optimized.
        // A more efficient implementation that can be used for .NET 6 and later is to delegate the work to
        // the BCL's StringBuilder.AppendInterpolatedStringHandler

        /// <summary>Appends a formatted value to the interpolated message.</summary>
        /// <typeparam name="T">The type of the value being appended.</typeparam>
        /// <param name="value">The value to append.</param>
        /// <param name="format">The format string to use.</param>
        public void AppendFormatted<T>(T value, string? format) => _builder!.AppendFormat(null, $"{{0:{format}}}", value);

        /// <summary>Appends a formatted value to the interpolated message.</summary>
        /// <typeparam name="T">The type of the value being appended.</typeparam>
        /// <param name="value">The value to append.</param>
        /// <param name="alignment">The minimum width of the formatted value.</param>
        public void AppendFormatted<T>(T value, int alignment) => _builder!.AppendFormat(null, $"{{0,{alignment}}}", value);

        /// <summary>Appends a formatted value to the interpolated message.</summary>
        /// <typeparam name="T">The type of the value being appended.</typeparam>
        /// <param name="value">The value to append.</param>
        /// <param name="alignment">The minimum width of the formatted value.</param>
        /// <param name="format">The format string to use.</param>
        public void AppendFormatted<T>(T value, int alignment, string? format) => _builder!.AppendFormat(null, $"{{0,{alignment}:{format}}}", value);

        /// <summary>Appends a formatted value to the interpolated message.</summary>
        /// <param name="value">The value to append.</param>
        public void AppendFormatted(string? value) => _builder!.Append(value);

#pragma warning disable RS0026 // Do not add multiple public overloads with optional parameters
#pragma warning disable RS0027 // API with optional parameter(s) should have the most parameters amongst its public overloads

        /// <summary>Appends a formatted value to the interpolated message.</summary>
        /// <param name="value">The value to append.</param>
        /// <param name="alignment">The minimum width of the formatted value.</param>
        /// <param name="format">The format string to use.</param>
        public void AppendFormatted(string? value, int alignment = 0, string? format = null) => _builder!.AppendFormat(null, $"{{0,{alignment}:{format}}}", value);

        /// <summary>Appends a formatted value to the interpolated message.</summary>
        /// <param name="value">The value to append.</param>
        /// <param name="alignment">The minimum width of the formatted value.</param>
        /// <param name="format">The format string to use.</param>
        public void AppendFormatted(object? value, int alignment = 0, string? format = null) => _builder!.AppendFormat(null, $"{{0,{alignment}:{format}}}", value);
#pragma warning restore RS0026 // Do not add multiple public overloads with optional parameters
#pragma warning restore RS0027 // API with optional parameter(s) should have the most parameters amongst its public overloads
    }

    /// <summary>
    /// Provides an interpolated string handler used by <c>Assert.AreNotEqual</c> overloads
    /// that only allocates and formats the message when the assertion is failing.
    /// </summary>
    /// <typeparam name="TArgument">The type of value being asserted.</typeparam>
    /// <remarks>
    /// This type is intended to be used by the compiler; users should not reference it directly.
    /// </remarks>
    [StackTraceHidden]
    [InterpolatedStringHandler]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public readonly struct AssertAreNotEqualInterpolatedStringHandler<TArgument>
    {
        private readonly StringBuilder? _builder;
        private readonly object? _notExpected;
        private readonly object? _actual;

        /// <summary>
        /// Initializes a new instance of the <see cref="AssertAreNotEqualInterpolatedStringHandler{TArgument}"/> struct.
        /// </summary>
        /// <param name="literalLength">The number of constant characters in the interpolated string.</param>
        /// <param name="formattedCount">The number of interpolation expressions in the interpolated string.</param>
        /// <param name="notExpected">The value that is not expected.</param>
        /// <param name="actual">The actual value being asserted.</param>
        /// <param name="shouldAppend">When this method returns, indicates whether the interpolated string should be evaluated.</param>
        public AssertAreNotEqualInterpolatedStringHandler(int literalLength, int formattedCount, TArgument? notExpected, TArgument? actual, out bool shouldAppend)
            : this(literalLength, formattedCount, notExpected, actual, null, out shouldAppend)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="AssertAreNotEqualInterpolatedStringHandler{TArgument}"/> struct.
        /// </summary>
        /// <param name="literalLength">The number of constant characters in the interpolated string.</param>
        /// <param name="formattedCount">The number of interpolation expressions in the interpolated string.</param>
        /// <param name="notExpected">The value that is not expected.</param>
        /// <param name="actual">The actual value being asserted.</param>
        /// <param name="comparer">The equality comparer used to compare values.</param>
        /// <param name="shouldAppend">When this method returns, indicates whether the interpolated string should be evaluated.</param>
        public AssertAreNotEqualInterpolatedStringHandler(int literalLength, int formattedCount, TArgument? notExpected, TArgument? actual, IEqualityComparer<TArgument>? comparer, out bool shouldAppend)
        {
            shouldAppend = AreNotEqualFailing(notExpected, actual, comparer);
            if (shouldAppend)
            {
                _builder = new StringBuilder(literalLength + formattedCount);
                _notExpected = notExpected;
                _actual = actual;
            }
        }

        internal void ComputeAssertion(string notExpectedExpression, string actualExpression)
        {
            if (_builder is not null)
            {
                ReportAssertAreNotEqualFailed(_notExpected, _actual, _builder.ToString(), notExpectedExpression, actualExpression);
            }
        }

        /// <summary>Appends a literal string to the interpolated message.</summary>
        /// <param name="value">The literal string to append.</param>
        public void AppendLiteral(string value) => _builder!.Append(value);

        /// <summary>Appends a formatted value to the interpolated message.</summary>
        /// <typeparam name="T">The type of the value being appended.</typeparam>
        /// <param name="value">The value to append.</param>
        public void AppendFormatted<T>(T value) => AppendFormatted(value, format: null);

#if NETCOREAPP3_1_OR_GREATER

        /// <summary>Appends a formatted value to the interpolated message.</summary>
        /// <param name="value">The value to append.</param>
        public void AppendFormatted(ReadOnlySpan<char> value) => _builder!.Append(value);

#pragma warning disable RS0027 // API with optional parameter(s) should have the most parameters amongst its public overloads

        /// <summary>Appends a formatted value to the interpolated message.</summary>
        /// <param name="value">The character span to append.</param>
        /// <param name="alignment">The minimum width of the formatted value.</param>
        /// <param name="format">The format string to use.</param>
        public void AppendFormatted(ReadOnlySpan<char> value, int alignment = 0, string? format = null) => AppendFormatted(value.ToString(), alignment, format);
#pragma warning restore RS0027 // API with optional parameter(s) should have the most parameters amongst its public overloads
#endif

        // NOTE: All the overloads involving format and/or alignment are not super efficient.
        // This code path is only for when an assert is failing, so that's not the common scenario
        // and should be okay if not very optimized.
        // A more efficient implementation that can be used for .NET 6 and later is to delegate the work to
        // the BCL's StringBuilder.AppendInterpolatedStringHandler

        /// <summary>Appends a formatted value to the interpolated message.</summary>
        /// <typeparam name="T">The type of the value being appended.</typeparam>
        /// <param name="value">The value to append.</param>
        /// <param name="format">The format string to use.</param>
        public void AppendFormatted<T>(T value, string? format) => _builder!.AppendFormat(null, $"{{0:{format}}}", value);

        /// <summary>Appends a formatted value to the interpolated message.</summary>
        /// <typeparam name="T">The type of the value being appended.</typeparam>
        /// <param name="value">The value to append.</param>
        /// <param name="alignment">The minimum width of the formatted value.</param>
        public void AppendFormatted<T>(T value, int alignment) => _builder!.AppendFormat(null, $"{{0,{alignment}}}", value);

        /// <summary>Appends a formatted value to the interpolated message.</summary>
        /// <typeparam name="T">The type of the value being appended.</typeparam>
        /// <param name="value">The value to append.</param>
        /// <param name="alignment">The minimum width of the formatted value.</param>
        /// <param name="format">The format string to use.</param>
        public void AppendFormatted<T>(T value, int alignment, string? format) => _builder!.AppendFormat(null, $"{{0,{alignment}:{format}}}", value);

        /// <summary>Appends a formatted value to the interpolated message.</summary>
        /// <param name="value">The value to append.</param>
        public void AppendFormatted(string? value) => _builder!.Append(value);

#pragma warning disable RS0026 // Do not add multiple public overloads with optional parameters
#pragma warning disable RS0027 // API with optional parameter(s) should have the most parameters amongst its public overloads

        /// <summary>Appends a formatted value to the interpolated message.</summary>
        /// <param name="value">The value to append.</param>
        /// <param name="alignment">The minimum width of the formatted value.</param>
        /// <param name="format">The format string to use.</param>
        public void AppendFormatted(string? value, int alignment = 0, string? format = null) => _builder!.AppendFormat(null, $"{{0,{alignment}:{format}}}", value);

        /// <summary>Appends a formatted value to the interpolated message.</summary>
        /// <param name="value">The value to append.</param>
        /// <param name="alignment">The minimum width of the formatted value.</param>
        /// <param name="format">The format string to use.</param>
        public void AppendFormatted(object? value, int alignment = 0, string? format = null) => _builder!.AppendFormat(null, $"{{0,{alignment}:{format}}}", value);
#pragma warning restore RS0026 // Do not add multiple public overloads with optional parameters
#pragma warning restore RS0027 // API with optional parameter(s) should have the most parameters amongst its public overloads
    }

    /// <summary>
    /// Provides an interpolated string handler used by <c>Assert.AreEqual</c> overloads
    /// that only allocates and formats the message when the assertion is failing.
    /// </summary>
    /// <remarks>
    /// This type is intended to be used by the compiler; users should not reference it directly.
    /// </remarks>
    [StackTraceHidden]
    [InterpolatedStringHandler]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public readonly struct AssertNonGenericAreEqualInterpolatedStringHandler
    {
        private readonly StringBuilder? _builder;
        private readonly Action<string, string, string>? _failAction;

        /// <summary>
        /// Initializes a new instance of the <see cref="AssertNonGenericAreEqualInterpolatedStringHandler"/> struct.
        /// </summary>
        /// <param name="literalLength">The number of constant characters in the interpolated string.</param>
        /// <param name="formattedCount">The number of interpolation expressions in the interpolated string.</param>
        /// <param name="expected">The expected value being asserted.</param>
        /// <param name="actual">The actual value being asserted.</param>
        /// <param name="delta">The maximum allowed difference between the expected and actual values.</param>
        /// <param name="shouldAppend">When this method returns, indicates whether the interpolated string should be evaluated.</param>
        public AssertNonGenericAreEqualInterpolatedStringHandler(int literalLength, int formattedCount, float expected, float actual, float delta, out bool shouldAppend)
        {
            shouldAppend = AreEqualFailing(expected, actual, delta);
            if (shouldAppend)
            {
                _builder = new StringBuilder(literalLength + formattedCount);
                _failAction = (userMessage, expectedExpression, actualExpression) =>
                    ReportAssertAreEqualFailed(expected, actual, delta, userMessage, expectedExpression, actualExpression);
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="AssertNonGenericAreEqualInterpolatedStringHandler"/> struct.
        /// </summary>
        /// <param name="literalLength">The number of constant characters in the interpolated string.</param>
        /// <param name="formattedCount">The number of interpolation expressions in the interpolated string.</param>
        /// <param name="expected">The expected value being asserted.</param>
        /// <param name="actual">The actual value being asserted.</param>
        /// <param name="delta">The maximum allowed difference between the expected and actual values.</param>
        /// <param name="shouldAppend">When this method returns, indicates whether the interpolated string should be evaluated.</param>
        public AssertNonGenericAreEqualInterpolatedStringHandler(int literalLength, int formattedCount, decimal expected, decimal actual, decimal delta, out bool shouldAppend)
        {
            shouldAppend = AreEqualFailing(expected, actual, delta);
            if (shouldAppend)
            {
                _builder = new StringBuilder(literalLength + formattedCount);
                _failAction = (userMessage, expectedExpression, actualExpression) =>
                    ReportAssertAreEqualFailed(expected, actual, delta, userMessage, expectedExpression, actualExpression);
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="AssertNonGenericAreEqualInterpolatedStringHandler"/> struct.
        /// </summary>
        /// <param name="literalLength">The number of constant characters in the interpolated string.</param>
        /// <param name="formattedCount">The number of interpolation expressions in the interpolated string.</param>
        /// <param name="expected">The expected value being asserted.</param>
        /// <param name="actual">The actual value being asserted.</param>
        /// <param name="delta">The maximum allowed difference between the expected and actual values.</param>
        /// <param name="shouldAppend">When this method returns, indicates whether the interpolated string should be evaluated.</param>
        public AssertNonGenericAreEqualInterpolatedStringHandler(int literalLength, int formattedCount, long expected, long actual, long delta, out bool shouldAppend)
        {
            shouldAppend = AreEqualFailing(expected, actual, delta);
            if (shouldAppend)
            {
                _builder = new StringBuilder(literalLength + formattedCount);
                _failAction = (userMessage, expectedExpression, actualExpression) =>
                    ReportAssertAreEqualFailed(expected, actual, delta, userMessage, expectedExpression, actualExpression);
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="AssertNonGenericAreEqualInterpolatedStringHandler"/> struct.
        /// </summary>
        /// <param name="literalLength">The number of constant characters in the interpolated string.</param>
        /// <param name="formattedCount">The number of interpolation expressions in the interpolated string.</param>
        /// <param name="expected">The expected value being asserted.</param>
        /// <param name="actual">The actual value being asserted.</param>
        /// <param name="delta">The maximum allowed difference between the expected and actual values.</param>
        /// <param name="shouldAppend">When this method returns, indicates whether the interpolated string should be evaluated.</param>
        public AssertNonGenericAreEqualInterpolatedStringHandler(int literalLength, int formattedCount, double expected, double actual, double delta, out bool shouldAppend)
        {
            shouldAppend = AreEqualFailing(expected, actual, delta);
            if (shouldAppend)
            {
                _builder = new StringBuilder(literalLength + formattedCount);
                _failAction = (userMessage, expectedExpression, actualExpression) =>
                    ReportAssertAreEqualFailed(expected, actual, delta, userMessage, expectedExpression, actualExpression);
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="AssertNonGenericAreEqualInterpolatedStringHandler"/> struct.
        /// </summary>
        /// <param name="literalLength">The number of constant characters in the interpolated string.</param>
        /// <param name="formattedCount">The number of interpolation expressions in the interpolated string.</param>
        /// <param name="expected">The expected value being asserted.</param>
        /// <param name="actual">The actual value being asserted.</param>
        /// <param name="ignoreCase">A value indicating whether the comparison ignores case.</param>
        /// <param name="shouldAppend">When this method returns, indicates whether the interpolated string should be evaluated.</param>
        public AssertNonGenericAreEqualInterpolatedStringHandler(int literalLength, int formattedCount, string? expected, string? actual, bool ignoreCase, out bool shouldAppend)
        {
            shouldAppend = AreEqualFailing(expected, actual, ignoreCase, CultureInfo.InvariantCulture);
            if (shouldAppend)
            {
                _builder = new StringBuilder(literalLength + formattedCount);
                _failAction = (userMessage, expectedExpression, actualExpression) =>
                    ReportAssertAreEqualFailed(expected, actual, ignoreCase, CultureInfo.InvariantCulture, cultureExplicit: false, userMessage, expectedExpression, actualExpression);
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="AssertNonGenericAreEqualInterpolatedStringHandler"/> struct.
        /// </summary>
        /// <param name="literalLength">The number of constant characters in the interpolated string.</param>
        /// <param name="formattedCount">The number of interpolation expressions in the interpolated string.</param>
        /// <param name="expected">The expected value being asserted.</param>
        /// <param name="actual">The actual value being asserted.</param>
        /// <param name="ignoreCase">A value indicating whether the comparison ignores case.</param>
        /// <param name="culture">The culture used for the string comparison.</param>
        /// <param name="shouldAppend">When this method returns, indicates whether the interpolated string should be evaluated.</param>
        public AssertNonGenericAreEqualInterpolatedStringHandler(int literalLength, int formattedCount, string? expected, string? actual, bool ignoreCase, CultureInfo culture, out bool shouldAppend)
        {
            _ = culture ?? throw new ArgumentNullException(nameof(culture));
            shouldAppend = AreEqualFailing(expected, actual, ignoreCase, culture);
            if (shouldAppend)
            {
                _builder = new StringBuilder(literalLength + formattedCount);
                _failAction = (userMessage, expectedExpression, actualExpression) =>
                    ReportAssertAreEqualFailed(expected, actual, ignoreCase, culture, cultureExplicit: true, userMessage, expectedExpression, actualExpression);
            }
        }

        internal void ComputeAssertion(string expectedExpression, string actualExpression)
            => _failAction?.Invoke(_builder!.ToString(), expectedExpression, actualExpression);

        /// <summary>Appends a literal string to the interpolated message.</summary>
        /// <param name="value">The literal string to append.</param>
        public void AppendLiteral(string value) => _builder!.Append(value);

        /// <summary>Appends a formatted value to the interpolated message.</summary>
        /// <typeparam name="T">The type of the value being appended.</typeparam>
        /// <param name="value">The value to append.</param>
        public void AppendFormatted<T>(T value) => AppendFormatted(value, format: null);

#if NETCOREAPP3_1_OR_GREATER

        /// <summary>Appends a formatted value to the interpolated message.</summary>
        /// <param name="value">The value to append.</param>
        public void AppendFormatted(ReadOnlySpan<char> value) => _builder!.Append(value);

#pragma warning disable RS0027 // API with optional parameter(s) should have the most parameters amongst its public overloads

        /// <summary>Appends a formatted value to the interpolated message.</summary>
        /// <param name="value">The character span to append.</param>
        /// <param name="alignment">The minimum width of the formatted value.</param>
        /// <param name="format">The format string to use.</param>
        public void AppendFormatted(ReadOnlySpan<char> value, int alignment = 0, string? format = null) => AppendFormatted(value.ToString(), alignment, format);
#pragma warning restore RS0027 // API with optional parameter(s) should have the most parameters amongst its public overloads
#endif

        // NOTE: All the overloads involving format and/or alignment are not super efficient.
        // This code path is only for when an assert is failing, so that's not the common scenario
        // and should be okay if not very optimized.
        // A more efficient implementation that can be used for .NET 6 and later is to delegate the work to
        // the BCL's StringBuilder.AppendInterpolatedStringHandler

        /// <summary>Appends a formatted value to the interpolated message.</summary>
        /// <typeparam name="T">The type of the value being appended.</typeparam>
        /// <param name="value">The value to append.</param>
        /// <param name="format">The format string to use.</param>
        public void AppendFormatted<T>(T value, string? format) => _builder!.AppendFormat(null, $"{{0:{format}}}", value);

        /// <summary>Appends a formatted value to the interpolated message.</summary>
        /// <typeparam name="T">The type of the value being appended.</typeparam>
        /// <param name="value">The value to append.</param>
        /// <param name="alignment">The minimum width of the formatted value.</param>
        public void AppendFormatted<T>(T value, int alignment) => _builder!.AppendFormat(null, $"{{0,{alignment}}}", value);

        /// <summary>Appends a formatted value to the interpolated message.</summary>
        /// <typeparam name="T">The type of the value being appended.</typeparam>
        /// <param name="value">The value to append.</param>
        /// <param name="alignment">The minimum width of the formatted value.</param>
        /// <param name="format">The format string to use.</param>
        public void AppendFormatted<T>(T value, int alignment, string? format) => _builder!.AppendFormat(null, $"{{0,{alignment}:{format}}}", value);

        /// <summary>Appends a formatted value to the interpolated message.</summary>
        /// <param name="value">The value to append.</param>
        public void AppendFormatted(string? value) => _builder!.Append(value);

#pragma warning disable RS0026 // Do not add multiple public overloads with optional parameters
#pragma warning disable RS0027 // API with optional parameter(s) should have the most parameters amongst its public overloads

        /// <summary>Appends a formatted value to the interpolated message.</summary>
        /// <param name="value">The value to append.</param>
        /// <param name="alignment">The minimum width of the formatted value.</param>
        /// <param name="format">The format string to use.</param>
        public void AppendFormatted(string? value, int alignment = 0, string? format = null) => _builder!.AppendFormat(null, $"{{0,{alignment}:{format}}}", value);

        /// <summary>Appends a formatted value to the interpolated message.</summary>
        /// <param name="value">The value to append.</param>
        /// <param name="alignment">The minimum width of the formatted value.</param>
        /// <param name="format">The format string to use.</param>
        public void AppendFormatted(object? value, int alignment = 0, string? format = null) => _builder!.AppendFormat(null, $"{{0,{alignment}:{format}}}", value);
#pragma warning restore RS0026 // Do not add multiple public overloads with optional parameters
#pragma warning restore RS0027 // API with optional parameter(s) should have the most parameters amongst its public overloads
    }

    /// <summary>
    /// Provides an interpolated string handler used by <c>Assert.AreNotEqual</c> overloads
    /// that only allocates and formats the message when the assertion is failing.
    /// </summary>
    /// <remarks>
    /// This type is intended to be used by the compiler; users should not reference it directly.
    /// </remarks>
    [StackTraceHidden]
    [InterpolatedStringHandler]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public readonly struct AssertNonGenericAreNotEqualInterpolatedStringHandler
    {
        private readonly StringBuilder? _builder;
        private readonly Action<string, string, string>? _failAction;

        /// <summary>
        /// Initializes a new instance of the <see cref="AssertNonGenericAreNotEqualInterpolatedStringHandler"/> struct.
        /// </summary>
        /// <param name="literalLength">The number of constant characters in the interpolated string.</param>
        /// <param name="formattedCount">The number of interpolation expressions in the interpolated string.</param>
        /// <param name="notExpected">The value that is not expected.</param>
        /// <param name="actual">The actual value being asserted.</param>
        /// <param name="delta">The maximum allowed difference between the expected and actual values.</param>
        /// <param name="shouldAppend">When this method returns, indicates whether the interpolated string should be evaluated.</param>
        public AssertNonGenericAreNotEqualInterpolatedStringHandler(int literalLength, int formattedCount, float notExpected, float actual, float delta, out bool shouldAppend)
        {
            shouldAppend = AreNotEqualFailing(notExpected, actual, delta);
            if (shouldAppend)
            {
                _builder = new StringBuilder(literalLength + formattedCount);
                _failAction = (userMessage, notExpectedExpr, actualExpr) =>
                    ReportAssertAreNotEqualFailed(notExpected, actual, delta, userMessage, notExpectedExpr, actualExpr);
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="AssertNonGenericAreNotEqualInterpolatedStringHandler"/> struct.
        /// </summary>
        /// <param name="literalLength">The number of constant characters in the interpolated string.</param>
        /// <param name="formattedCount">The number of interpolation expressions in the interpolated string.</param>
        /// <param name="notExpected">The value that is not expected.</param>
        /// <param name="actual">The actual value being asserted.</param>
        /// <param name="delta">The maximum allowed difference between the expected and actual values.</param>
        /// <param name="shouldAppend">When this method returns, indicates whether the interpolated string should be evaluated.</param>
        public AssertNonGenericAreNotEqualInterpolatedStringHandler(int literalLength, int formattedCount, decimal notExpected, decimal actual, decimal delta, out bool shouldAppend)
        {
            shouldAppend = AreNotEqualFailing(notExpected, actual, delta);
            if (shouldAppend)
            {
                _builder = new StringBuilder(literalLength + formattedCount);
                _failAction = (userMessage, notExpectedExpr, actualExpr) =>
                    ReportAssertAreNotEqualFailed(notExpected, actual, delta, userMessage, notExpectedExpr, actualExpr);
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="AssertNonGenericAreNotEqualInterpolatedStringHandler"/> struct.
        /// </summary>
        /// <param name="literalLength">The number of constant characters in the interpolated string.</param>
        /// <param name="formattedCount">The number of interpolation expressions in the interpolated string.</param>
        /// <param name="notExpected">The value that is not expected.</param>
        /// <param name="actual">The actual value being asserted.</param>
        /// <param name="delta">The maximum allowed difference between the expected and actual values.</param>
        /// <param name="shouldAppend">When this method returns, indicates whether the interpolated string should be evaluated.</param>
        public AssertNonGenericAreNotEqualInterpolatedStringHandler(int literalLength, int formattedCount, long notExpected, long actual, long delta, out bool shouldAppend)
        {
            shouldAppend = AreNotEqualFailing(notExpected, actual, delta);
            if (shouldAppend)
            {
                _builder = new StringBuilder(literalLength + formattedCount);
                _failAction = (userMessage, notExpectedExpr, actualExpr) =>
                    ReportAssertAreNotEqualFailed(notExpected, actual, delta, userMessage, notExpectedExpr, actualExpr);
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="AssertNonGenericAreNotEqualInterpolatedStringHandler"/> struct.
        /// </summary>
        /// <param name="literalLength">The number of constant characters in the interpolated string.</param>
        /// <param name="formattedCount">The number of interpolation expressions in the interpolated string.</param>
        /// <param name="notExpected">The value that is not expected.</param>
        /// <param name="actual">The actual value being asserted.</param>
        /// <param name="delta">The maximum allowed difference between the expected and actual values.</param>
        /// <param name="shouldAppend">When this method returns, indicates whether the interpolated string should be evaluated.</param>
        public AssertNonGenericAreNotEqualInterpolatedStringHandler(int literalLength, int formattedCount, double notExpected, double actual, double delta, out bool shouldAppend)
        {
            shouldAppend = AreNotEqualFailing(notExpected, actual, delta);
            if (shouldAppend)
            {
                _builder = new StringBuilder(literalLength + formattedCount);
                _failAction = (userMessage, notExpectedExpr, actualExpr) =>
                    ReportAssertAreNotEqualFailed(notExpected, actual, delta, userMessage, notExpectedExpr, actualExpr);
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="AssertNonGenericAreNotEqualInterpolatedStringHandler"/> struct.
        /// </summary>
        /// <param name="literalLength">The number of constant characters in the interpolated string.</param>
        /// <param name="formattedCount">The number of interpolation expressions in the interpolated string.</param>
        /// <param name="notExpected">The value that is not expected.</param>
        /// <param name="actual">The actual value being asserted.</param>
        /// <param name="ignoreCase">A value indicating whether the comparison ignores case.</param>
        /// <param name="shouldAppend">When this method returns, indicates whether the interpolated string should be evaluated.</param>
        public AssertNonGenericAreNotEqualInterpolatedStringHandler(int literalLength, int formattedCount, string? notExpected, string? actual, bool ignoreCase, out bool shouldAppend)
        {
            shouldAppend = AreNotEqualFailing(notExpected, actual, ignoreCase, CultureInfo.InvariantCulture);
            if (shouldAppend)
            {
                _builder = new StringBuilder(literalLength + formattedCount);
                _failAction = (userMessage, notExpectedExpr, actualExpr) =>
                    ReportAssertAreNotEqualFailed(notExpected, actual, ignoreCase, CultureInfo.InvariantCulture, cultureExplicit: false, userMessage, notExpectedExpr, actualExpr);
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="AssertNonGenericAreNotEqualInterpolatedStringHandler"/> struct.
        /// </summary>
        /// <param name="literalLength">The number of constant characters in the interpolated string.</param>
        /// <param name="formattedCount">The number of interpolation expressions in the interpolated string.</param>
        /// <param name="notExpected">The value that is not expected.</param>
        /// <param name="actual">The actual value being asserted.</param>
        /// <param name="ignoreCase">A value indicating whether the comparison ignores case.</param>
        /// <param name="culture">The culture used for the string comparison.</param>
        /// <param name="shouldAppend">When this method returns, indicates whether the interpolated string should be evaluated.</param>
        public AssertNonGenericAreNotEqualInterpolatedStringHandler(int literalLength, int formattedCount, string? notExpected, string? actual, bool ignoreCase, CultureInfo culture, out bool shouldAppend)
        {
            _ = culture ?? throw new ArgumentNullException(nameof(culture));
            shouldAppend = AreNotEqualFailing(notExpected, actual, ignoreCase, culture);
            if (shouldAppend)
            {
                _builder = new StringBuilder(literalLength + formattedCount);
                _failAction = (userMessage, notExpectedExpr, actualExpr) =>
                    ReportAssertAreNotEqualFailed(notExpected, actual, ignoreCase, culture, cultureExplicit: true, userMessage, notExpectedExpr, actualExpr);
            }
        }

        internal void ComputeAssertion(string notExpectedExpression, string actualExpression)
            => _failAction?.Invoke(_builder!.ToString(), notExpectedExpression, actualExpression);

        /// <summary>Appends a literal string to the interpolated message.</summary>
        /// <param name="value">The literal string to append.</param>
        public void AppendLiteral(string value) => _builder!.Append(value);

        /// <summary>Appends a formatted value to the interpolated message.</summary>
        /// <typeparam name="T">The type of the value being appended.</typeparam>
        /// <param name="value">The value to append.</param>
        public void AppendFormatted<T>(T value) => AppendFormatted(value, format: null);

#if NETCOREAPP3_1_OR_GREATER

        /// <summary>Appends a formatted value to the interpolated message.</summary>
        /// <param name="value">The value to append.</param>
        public void AppendFormatted(ReadOnlySpan<char> value) => _builder!.Append(value);

#pragma warning disable RS0027 // API with optional parameter(s) should have the most parameters amongst its public overloads

        /// <summary>Appends a formatted value to the interpolated message.</summary>
        /// <param name="value">The character span to append.</param>
        /// <param name="alignment">The minimum width of the formatted value.</param>
        /// <param name="format">The format string to use.</param>
        public void AppendFormatted(ReadOnlySpan<char> value, int alignment = 0, string? format = null) => AppendFormatted(value.ToString(), alignment, format);
#pragma warning restore RS0027 // API with optional parameter(s) should have the most parameters amongst its public overloads
#endif

        // NOTE: All the overloads involving format and/or alignment are not super efficient.
        // This code path is only for when an assert is failing, so that's not the common scenario
        // and should be okay if not very optimized.
        // A more efficient implementation that can be used for .NET 6 and later is to delegate the work to
        // the BCL's StringBuilder.AppendInterpolatedStringHandler

        /// <summary>Appends a formatted value to the interpolated message.</summary>
        /// <typeparam name="T">The type of the value being appended.</typeparam>
        /// <param name="value">The value to append.</param>
        /// <param name="format">The format string to use.</param>
        public void AppendFormatted<T>(T value, string? format) => _builder!.AppendFormat(null, $"{{0:{format}}}", value);

        /// <summary>Appends a formatted value to the interpolated message.</summary>
        /// <typeparam name="T">The type of the value being appended.</typeparam>
        /// <param name="value">The value to append.</param>
        /// <param name="alignment">The minimum width of the formatted value.</param>
        public void AppendFormatted<T>(T value, int alignment) => _builder!.AppendFormat(null, $"{{0,{alignment}}}", value);

        /// <summary>Appends a formatted value to the interpolated message.</summary>
        /// <typeparam name="T">The type of the value being appended.</typeparam>
        /// <param name="value">The value to append.</param>
        /// <param name="alignment">The minimum width of the formatted value.</param>
        /// <param name="format">The format string to use.</param>
        public void AppendFormatted<T>(T value, int alignment, string? format) => _builder!.AppendFormat(null, $"{{0,{alignment}:{format}}}", value);

        /// <summary>Appends a formatted value to the interpolated message.</summary>
        /// <param name="value">The value to append.</param>
        public void AppendFormatted(string? value) => _builder!.Append(value);

#pragma warning disable RS0026 // Do not add multiple public overloads with optional parameters
#pragma warning disable RS0027 // API with optional parameter(s) should have the most parameters amongst its public overloads

        /// <summary>Appends a formatted value to the interpolated message.</summary>
        /// <param name="value">The value to append.</param>
        /// <param name="alignment">The minimum width of the formatted value.</param>
        /// <param name="format">The format string to use.</param>
        public void AppendFormatted(string? value, int alignment = 0, string? format = null) => _builder!.AppendFormat(null, $"{{0,{alignment}:{format}}}", value);

        /// <summary>Appends a formatted value to the interpolated message.</summary>
        /// <param name="value">The value to append.</param>
        /// <param name="alignment">The minimum width of the formatted value.</param>
        /// <param name="format">The format string to use.</param>
        public void AppendFormatted(object? value, int alignment = 0, string? format = null) => _builder!.AppendFormat(null, $"{{0,{alignment}:{format}}}", value);
#pragma warning restore RS0026 // Do not add multiple public overloads with optional parameters
#pragma warning restore RS0027 // API with optional parameter(s) should have the most parameters amongst its public overloads
    }
}
