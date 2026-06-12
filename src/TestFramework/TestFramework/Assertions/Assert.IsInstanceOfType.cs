// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.ComponentModel;

namespace Microsoft.VisualStudio.TestTools.UnitTesting;

#pragma warning disable CA2263 // Prefer generic overload when type is known - false positives

/// <summary>
/// A collection of helper classes to test various conditions within
/// unit tests. If the condition being tested is not met, an exception
/// is thrown.
/// </summary>
public sealed partial class Assert
{
    /// <summary>
    /// Provides an interpolated string handler used by <c>Assert.IsInstanceOfType</c> overloads
    /// that only allocates and formats the message when the assertion is failing.
    /// </summary>
    /// <remarks>
    /// This type is intended to be used by the compiler; users should not reference it directly.
    /// </remarks>
    [StackTraceHidden]
    [InterpolatedStringHandler]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public readonly struct AssertIsInstanceOfTypeInterpolatedStringHandler
    {
        private readonly StringBuilder? _builder;
        private readonly object? _value;
        private readonly Type? _expectedType;

        /// <summary>
        /// Initializes a new instance of the <see cref="AssertIsInstanceOfTypeInterpolatedStringHandler"/> struct.
        /// </summary>
        /// <param name="literalLength">The number of constant characters in the interpolated string.</param>
        /// <param name="formattedCount">The number of interpolation expressions in the interpolated string.</param>
        /// <param name="value">The value being asserted; the message is only computed when the assertion fails.</param>
        /// <param name="expectedType">The expected type for the value being asserted.</param>
        /// <param name="shouldAppend">When this method returns, indicates whether the interpolated string should be evaluated.</param>
        public AssertIsInstanceOfTypeInterpolatedStringHandler(int literalLength, int formattedCount, object? value, Type? expectedType, out bool shouldAppend)
        {
            _value = value;
            _expectedType = expectedType;
            shouldAppend = IsInstanceOfTypeFailing(value, expectedType);
            if (shouldAppend)
            {
                _builder = new StringBuilder(literalLength + formattedCount);
            }
        }

        internal void ComputeAssertion(string valueExpression)
        {
            if (_builder is not null)
            {
                ReportAssertIsInstanceOfTypeFailed(_value, _expectedType, _builder.ToString(), valueExpression);
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
    /// Provides an interpolated string handler used by <c>Assert.IsInstanceOfType</c> overloads
    /// that only allocates and formats the message when the assertion is failing.
    /// </summary>
    /// <typeparam name="TArg">The type the value is expected to be related to.</typeparam>
    /// <remarks>
    /// This type is intended to be used by the compiler; users should not reference it directly.
    /// </remarks>
    [StackTraceHidden]
    [InterpolatedStringHandler]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public readonly struct AssertGenericIsInstanceOfTypeInterpolatedStringHandler<TArg>
    {
        private readonly StringBuilder? _builder;
        private readonly object? _value;

        /// <summary>
        /// Initializes a new instance of the <see cref="AssertGenericIsInstanceOfTypeInterpolatedStringHandler{TArg}"/> struct.
        /// </summary>
        /// <param name="literalLength">The number of constant characters in the interpolated string.</param>
        /// <param name="formattedCount">The number of interpolation expressions in the interpolated string.</param>
        /// <param name="value">The value being asserted; the message is only computed when the assertion fails.</param>
        /// <param name="shouldAppend">When this method returns, indicates whether the interpolated string should be evaluated.</param>
        public AssertGenericIsInstanceOfTypeInterpolatedStringHandler(int literalLength, int formattedCount, object? value, out bool shouldAppend)
        {
            _value = value;
            shouldAppend = IsInstanceOfTypeFailing(value, typeof(TArg));
            if (shouldAppend)
            {
                _builder = new StringBuilder(literalLength + formattedCount);
            }
        }

        internal void ComputeAssertion(string valueExpression)
        {
            if (_builder is not null)
            {
                ReportAssertIsInstanceOfTypeFailed(_value, typeof(TArg), _builder.ToString(), valueExpression);
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
    /// Provides an interpolated string handler used by <c>Assert.IsNotInstanceOfType</c> overloads
    /// that only allocates and formats the message when the assertion is failing.
    /// </summary>
    /// <remarks>
    /// This type is intended to be used by the compiler; users should not reference it directly.
    /// </remarks>
    [StackTraceHidden]
    [InterpolatedStringHandler]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public readonly struct AssertIsNotInstanceOfTypeInterpolatedStringHandler
    {
        private readonly StringBuilder? _builder;
        private readonly object? _value;
        private readonly Type? _wrongType;

        /// <summary>
        /// Initializes a new instance of the <see cref="AssertIsNotInstanceOfTypeInterpolatedStringHandler"/> struct.
        /// </summary>
        /// <param name="literalLength">The number of constant characters in the interpolated string.</param>
        /// <param name="formattedCount">The number of interpolation expressions in the interpolated string.</param>
        /// <param name="value">The value being asserted; the message is only computed when the assertion fails.</param>
        /// <param name="wrongType">The type the value is not expected to be an instance of.</param>
        /// <param name="shouldAppend">When this method returns, indicates whether the interpolated string should be evaluated.</param>
        public AssertIsNotInstanceOfTypeInterpolatedStringHandler(int literalLength, int formattedCount, object? value, Type? wrongType, out bool shouldAppend)
        {
            _value = value;
            _wrongType = wrongType;
            shouldAppend = IsNotInstanceOfTypeFailing(value, wrongType);
            if (shouldAppend)
            {
                _builder = new StringBuilder(literalLength + formattedCount);
            }
        }

        internal void ComputeAssertion(string valueExpression)
        {
            if (_builder is not null)
            {
                ReportAssertIsNotInstanceOfTypeFailed(_value, _wrongType, _builder.ToString(), valueExpression);
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
    /// Provides an interpolated string handler used by <c>Assert.IsNotInstanceOfType</c> overloads
    /// that only allocates and formats the message when the assertion is failing.
    /// </summary>
    /// <typeparam name="TArg">The type the value is expected to be related to.</typeparam>
    /// <remarks>
    /// This type is intended to be used by the compiler; users should not reference it directly.
    /// </remarks>
    [StackTraceHidden]
    [InterpolatedStringHandler]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public readonly struct AssertGenericIsNotInstanceOfTypeInterpolatedStringHandler<TArg>
    {
        private readonly StringBuilder? _builder;
        private readonly object? _value;

        /// <summary>
        /// Initializes a new instance of the <see cref="AssertGenericIsNotInstanceOfTypeInterpolatedStringHandler{TArg}"/> struct.
        /// </summary>
        /// <param name="literalLength">The number of constant characters in the interpolated string.</param>
        /// <param name="formattedCount">The number of interpolation expressions in the interpolated string.</param>
        /// <param name="value">The value being asserted; the message is only computed when the assertion fails.</param>
        /// <param name="shouldAppend">When this method returns, indicates whether the interpolated string should be evaluated.</param>
        public AssertGenericIsNotInstanceOfTypeInterpolatedStringHandler(int literalLength, int formattedCount, object? value, out bool shouldAppend)
        {
            _value = value;
            shouldAppend = IsNotInstanceOfTypeFailing(value, typeof(TArg));
            if (shouldAppend)
            {
                _builder = new StringBuilder(literalLength + formattedCount);
            }
        }

        internal void ComputeAssertion(string valueExpression)
        {
            if (_builder is not null)
            {
                ReportAssertIsNotInstanceOfTypeFailed(_value, typeof(TArg), _builder.ToString(), valueExpression);
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

#pragma warning disable RS0026 // Do not add multiple public overloads with optional parameters
#pragma warning disable RS0027 // API with optional parameter(s) should have the most parameters amongst its public overloads

    /// <summary>
    /// Tests whether the specified object is an instance of the expected
    /// type and throws an exception if the expected type is not in the
    /// inheritance hierarchy of the object.
    /// </summary>
    /// <param name="value">
    /// The object the test expects to be of the specified type.
    /// </param>
    /// <param name="expectedType">
    /// The expected type of <paramref name="value"/>.
    /// </param>
    /// <param name="message">
    /// The message to include in the exception when <paramref name="value"/>
    /// is not an instance of <paramref name="expectedType"/>. The message is
    /// shown in test results.
    /// </param>
    /// <param name="valueExpression">
    /// The syntactic expression of value as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// Thrown if <paramref name="value"/> is null or
    /// <paramref name="expectedType"/> is not in the inheritance hierarchy
    /// of <paramref name="value"/>.
    /// </exception>
    public static void IsInstanceOfType([NotNull] object? value, [NotNull] Type? expectedType, string? message = "", [CallerArgumentExpression(nameof(value))] string valueExpression = "")
    {
        TelemetryCollector.TrackAssertionCall("Assert.IsInstanceOfType");

        if (IsInstanceOfTypeFailing(value, expectedType))
        {
            ReportAssertIsInstanceOfTypeFailed(value, expectedType, message, valueExpression);
        }
    }

    /// <inheritdoc cref="IsInstanceOfType(object?, Type?, string, string)" />
#pragma warning disable IDE0060 // Remove unused parameter - https://github.com/dotnet/roslyn/issues/76578
    public static void IsInstanceOfType([NotNull] object? value, [NotNull] Type? expectedType, [InterpolatedStringHandlerArgument(nameof(value), nameof(expectedType))] ref AssertIsInstanceOfTypeInterpolatedStringHandler message, [CallerArgumentExpression(nameof(value))] string valueExpression = "")
#pragma warning restore IDE0060 // Remove unused parameter
#pragma warning disable CS8777 // Parameter must have a non-null value when exiting. - Deliberately keeping [NotNull] annotation while using soft assertions. Within an AssertScope, the postcondition is not enforced (same as all other assertion postconditions in scoped mode).
    {
        TelemetryCollector.TrackAssertionCall("Assert.IsInstanceOfType");
        message.ComputeAssertion(valueExpression);
    }
#pragma warning restore CS8777 // Parameter must have a non-null value when exiting.

    /// <summary>
    /// Tests whether the specified object is an instance of the generic
    /// type and throws an exception if the generic type is not in the
    /// inheritance hierarchy of the object.
    /// </summary>
    /// <typeparam name="T">The expected type of <paramref name="value"/>.</typeparam>
    public static T IsInstanceOfType<T>([NotNull] object? value, string? message = "", [CallerArgumentExpression(nameof(value))] string valueExpression = "")
    {
        IsInstanceOfType(value, typeof(T), message, valueExpression);
        return (T)value!;
    }

    /// <inheritdoc cref="IsInstanceOfType{T}(object?, string, string)" />
#pragma warning disable IDE0060 // Remove unused parameter - https://github.com/dotnet/roslyn/issues/76578
    public static T IsInstanceOfType<T>([NotNull] object? value, [InterpolatedStringHandlerArgument(nameof(value))] ref AssertGenericIsInstanceOfTypeInterpolatedStringHandler<T> message, [CallerArgumentExpression(nameof(value))] string valueExpression = "")
#pragma warning restore IDE0060 // Remove unused parameter
#pragma warning disable CS8777 // Parameter must have a non-null value when exiting. - Deliberately keeping [NotNull] annotation while using soft assertions. Within an AssertScope, the postcondition is not enforced (same as all other assertion postconditions in scoped mode).
    {
        TelemetryCollector.TrackAssertionCall("Assert.IsInstanceOfType");
        message.ComputeAssertion(valueExpression);
        return (T)value!;
    }
#pragma warning restore CS8777 // Parameter must have a non-null value when exiting.

    private static bool IsInstanceOfTypeFailing([NotNullWhen(false)] object? value, [NotNullWhen(false)] Type? expectedType)
        => expectedType == null || value == null || !expectedType.IsInstanceOfType(value);

    [DoesNotReturn]
    private static void ReportAssertIsInstanceOfTypeFailed(object? value, Type? expectedType, string? userMessage, string valueExpression)
    {
        StructuredAssertionMessage msg = expectedType is null
            ? new("Cannot check type because the expected type argument is null.")
            : new($"Expected value to be of type {expectedType.Name} (or derived).");
        msg.WithUserMessage(userMessage);

        if (expectedType is not null)
        {
            string actualTypeText = value?.GetType().ToString() ?? "null";
            EvidenceBlock evidence = EvidenceBlock.Create()
                .AddLine("expected type:", $"{expectedType} (or derived)")
                .AddLine(value is null ? "actual:" : "actual type:", actualTypeText);
            msg.WithEvidence(evidence)
               .WithExpectedAndActual($"{expectedType} (or derived)", actualTypeText);
        }

        msg.WithCallSiteExpression(FormatCallSiteExpression("Assert.IsInstanceOfType", valueExpression, "<value>"));
        ReportAssertFailed(msg);
    }

    /// <summary>
    /// Tests whether the specified object is not an instance of the wrong
    /// type and throws an exception if the specified type is in the
    /// inheritance hierarchy of the object.
    /// </summary>
    /// <param name="value">
    /// The object the test expects not to be of the specified type.
    /// </param>
    /// <param name="wrongType">
    /// The type that <paramref name="value"/> should not be.
    /// </param>
    /// <param name="message">
    /// The message to include in the exception when <paramref name="value"/>
    /// is an instance of <paramref name="wrongType"/>. The message is shown
    /// in test results.
    /// </param>
    /// <param name="valueExpression">
    /// The syntactic expression of value as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// Thrown if <paramref name="value"/> is not null and
    /// <paramref name="wrongType"/> is in the inheritance hierarchy
    /// of <paramref name="value"/>.
    /// </exception>
    public static void IsNotInstanceOfType(object? value, [NotNull] Type? wrongType, string? message = "", [CallerArgumentExpression(nameof(value))] string valueExpression = "")
    {
        TelemetryCollector.TrackAssertionCall("Assert.IsNotInstanceOfType");

        if (IsNotInstanceOfTypeFailing(value, wrongType))
        {
            ReportAssertIsNotInstanceOfTypeFailed(value, wrongType, message, valueExpression);
        }
    }

    /// <inheritdoc cref="IsNotInstanceOfType(object?, Type?, string, string)" />
#pragma warning disable IDE0060 // Remove unused parameter - https://github.com/dotnet/roslyn/issues/76578
    public static void IsNotInstanceOfType(object? value, [NotNull] Type? wrongType, [InterpolatedStringHandlerArgument(nameof(value), nameof(wrongType))] ref AssertIsNotInstanceOfTypeInterpolatedStringHandler message, [CallerArgumentExpression(nameof(value))] string valueExpression = "")
#pragma warning restore IDE0060 // Remove unused parameter
#pragma warning disable CS8777 // Parameter must have a non-null value when exiting. - Not sure how to express the semantics to the compiler, but the implementation guarantees that.
    {
        TelemetryCollector.TrackAssertionCall("Assert.IsNotInstanceOfType");
        message.ComputeAssertion(valueExpression);
    }
#pragma warning restore CS8777 // Parameter must have a non-null value when exiting.

    /// <summary>
    /// Tests whether the specified object is not an instance of the wrong generic
    /// type and throws an exception if the specified type is in the
    /// inheritance hierarchy of the object.
    /// </summary>
    /// <typeparam name="T">The type that <paramref name="value"/> should not be.</typeparam>
    public static void IsNotInstanceOfType<T>(object? value, string? message = "", [CallerArgumentExpression(nameof(value))] string valueExpression = "")
        => IsNotInstanceOfType(value, typeof(T), message, valueExpression);

    /// <inheritdoc cref="IsNotInstanceOfType{T}(object?, string, string)" />
#pragma warning disable IDE0060 // Remove unused parameter - https://github.com/dotnet/roslyn/issues/76578
    public static void IsNotInstanceOfType<T>(object? value, [InterpolatedStringHandlerArgument(nameof(value))] AssertGenericIsNotInstanceOfTypeInterpolatedStringHandler<T> message, [CallerArgumentExpression(nameof(value))] string valueExpression = "")
#pragma warning restore IDE0060 // Remove unused parameter
    {
        TelemetryCollector.TrackAssertionCall("Assert.IsNotInstanceOfType");
        message.ComputeAssertion(valueExpression);
    }

    private static bool IsNotInstanceOfTypeFailing(object? value, [NotNullWhen(false)] Type? wrongType)
        => wrongType is null ||
            // Null is not an instance of any type.
            (value is not null && wrongType.IsInstanceOfType(value));

    [DoesNotReturn]
    private static void ReportAssertIsNotInstanceOfTypeFailed(object? value, Type? wrongType, string? userMessage, string valueExpression)
    {
        StructuredAssertionMessage msg = wrongType is null
            ? new("Cannot check type because the not-expected type argument is null.")
            : new($"Expected value to not be of type {wrongType.Name} (or derived).");
        msg.WithUserMessage(userMessage);

        if (wrongType is not null)
        {
            string actualTypeText = value?.GetType().ToString() ?? "null";
            EvidenceBlock evidence = EvidenceBlock.Create()
                .AddLine("not expected type:", $"{wrongType} (or derived)")
                .AddLine("actual type:", actualTypeText);
            msg.WithEvidence(evidence)
               .WithExpectedAndActual($"{wrongType} (or derived)", actualTypeText);
        }

        msg.WithCallSiteExpression(FormatCallSiteExpression("Assert.IsNotInstanceOfType", valueExpression, "<value>"));
        ReportAssertFailed(msg);
    }
}
