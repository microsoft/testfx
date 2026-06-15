// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.ComponentModel;

namespace Microsoft.VisualStudio.TestTools.UnitTesting;

#pragma warning disable RS0026 // Do not add multiple public overloads with optional parameters

/// <summary>
/// A collection of helper classes to test various conditions within
/// unit tests. If the condition being tested is not met, an exception
/// is thrown.
/// </summary>
public sealed partial class Assert
{
    /// <summary>
    /// Provides an interpolated string handler used by <c>Assert.Throws</c> overloads
    /// that only allocates and formats the message when the assertion is failing.
    /// </summary>
    /// <typeparam name="TException">The type of exception expected to be thrown.</typeparam>
    /// <remarks>
    /// This type is intended to be used by the compiler; users should not reference it directly.
    /// </remarks>
    [StackTraceHidden]
    [InterpolatedStringHandler]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public readonly struct AssertNonStrictThrowsInterpolatedStringHandler<TException>
        where TException : Exception
    {
        private readonly StringBuilder? _builder;
        private readonly ThrowsExceptionState _state;

        /// <summary>
        /// Initializes a new instance of the <see cref="AssertNonStrictThrowsInterpolatedStringHandler{TException}"/> struct.
        /// </summary>
        /// <param name="literalLength">The number of constant characters in the interpolated string.</param>
        /// <param name="formattedCount">The number of interpolation expressions in the interpolated string.</param>
        /// <param name="action">The delegate being asserted; the message is only computed when the assertion fails.</param>
        /// <param name="shouldAppend">When this method returns, indicates whether the interpolated string should be evaluated.</param>
        public AssertNonStrictThrowsInterpolatedStringHandler(int literalLength, int formattedCount, Action action, out bool shouldAppend)
        {
            _state = IsThrowsFailing<TException>(action, isStrictType: false);
            shouldAppend = _state.FailureKind != ThrowsFailureKind.NotFailing;
            if (shouldAppend)
            {
                _builder = new StringBuilder(literalLength + formattedCount);
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="AssertNonStrictThrowsInterpolatedStringHandler{TException}"/> struct.
        /// </summary>
        /// <param name="literalLength">The number of constant characters in the interpolated string.</param>
        /// <param name="formattedCount">The number of interpolation expressions in the interpolated string.</param>
        /// <param name="action">The delegate being asserted; the message is only computed when the assertion fails.</param>
        /// <param name="shouldAppend">When this method returns, indicates whether the interpolated string should be evaluated.</param>
        public AssertNonStrictThrowsInterpolatedStringHandler(int literalLength, int formattedCount, Func<object?> action, out bool shouldAppend)
            : this(literalLength, formattedCount, (Action)(() => _ = action()), out shouldAppend)
        {
        }

        internal TException ComputeAssertion(string actionExpression)
        {
            if (_state.FailureKind != ThrowsFailureKind.NotFailing)
            {
                ReportThrowsFailed<TException>(isStrictType: false, _state, _builder!.ToString(), actionExpression, nameof(Throws));
            }
            else
            {
                return (TException)_state.ExceptionThrown!;
            }

            // Reached when ReportThrowsFailed records the failure into the active AssertScope and returns instead of throwing.
            return null!;
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
#pragma warning restore RS0027 // API with optional parameter(s) should have the most parameters amongst its public overloads
    }

    /// <summary>
    /// Provides an interpolated string handler used by <c>Assert.ThrowsExactly</c> overloads
    /// that only allocates and formats the message when the assertion is failing.
    /// </summary>
    /// <typeparam name="TException">The type of exception expected to be thrown.</typeparam>
    /// <remarks>
    /// This type is intended to be used by the compiler; users should not reference it directly.
    /// </remarks>
    [StackTraceHidden]
    [InterpolatedStringHandler]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public readonly struct AssertThrowsExactlyInterpolatedStringHandler<TException>
        where TException : Exception
    {
        private readonly StringBuilder? _builder;
        private readonly ThrowsExceptionState _state;

        /// <summary>
        /// Initializes a new instance of the <see cref="AssertThrowsExactlyInterpolatedStringHandler{TException}"/> struct.
        /// </summary>
        /// <param name="literalLength">The number of constant characters in the interpolated string.</param>
        /// <param name="formattedCount">The number of interpolation expressions in the interpolated string.</param>
        /// <param name="action">The delegate being asserted; the message is only computed when the assertion fails.</param>
        /// <param name="shouldAppend">When this method returns, indicates whether the interpolated string should be evaluated.</param>
        public AssertThrowsExactlyInterpolatedStringHandler(int literalLength, int formattedCount, Action action, out bool shouldAppend)
        {
            _state = IsThrowsFailing<TException>(action, isStrictType: true);
            shouldAppend = _state.FailureKind != ThrowsFailureKind.NotFailing;
            if (shouldAppend)
            {
                _builder = new StringBuilder(literalLength + formattedCount);
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="AssertThrowsExactlyInterpolatedStringHandler{TException}"/> struct.
        /// </summary>
        /// <param name="literalLength">The number of constant characters in the interpolated string.</param>
        /// <param name="formattedCount">The number of interpolation expressions in the interpolated string.</param>
        /// <param name="action">The delegate being asserted; the message is only computed when the assertion fails.</param>
        /// <param name="shouldAppend">When this method returns, indicates whether the interpolated string should be evaluated.</param>
        public AssertThrowsExactlyInterpolatedStringHandler(int literalLength, int formattedCount, Func<object?> action, out bool shouldAppend)
            : this(literalLength, formattedCount, (Action)(() => _ = action()), out shouldAppend)
        {
        }

        internal TException ComputeAssertion(string actionExpression)
        {
            if (_state.FailureKind != ThrowsFailureKind.NotFailing)
            {
                ReportThrowsFailed<TException>(isStrictType: true, _state, _builder!.ToString(), actionExpression, nameof(ThrowsExactly));
            }
            else
            {
                return (TException)_state.ExceptionThrown!;
            }

            // Reached when ReportThrowsFailed records the failure into the active AssertScope and returns instead of throwing.
            return null!;
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
#pragma warning restore RS0027 // API with optional parameter(s) should have the most parameters amongst its public overloads
    }
}
