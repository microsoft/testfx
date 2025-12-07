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
    [InterpolatedStringHandler]
    [EditorBrowsable(EditorBrowsableState.Never)]
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
    public readonly struct AssertIsExactInstanceOfTypeInterpolatedStringHandler
    {
        private readonly StringBuilder? _builder;
        private readonly object? _value;
        private readonly Type? _expectedType;

        public AssertIsExactInstanceOfTypeInterpolatedStringHandler(int literalLength, int formattedCount, object? value, Type? expectedType, out bool shouldAppend)
        {
            _value = value;
            _expectedType = expectedType;
            shouldAppend = IsExactInstanceOfTypeFailing(value, expectedType);
            if (shouldAppend)
            {
                _builder = new StringBuilder(literalLength + formattedCount);
            }
        }

        internal void ComputeAssertion(string valueExpression)
        {
            if (_builder is not null)
            {
                _builder.Insert(0, string.Format(CultureInfo.CurrentCulture, FrameworkMessages.CallerArgumentExpressionSingleParameterMessage, "value", valueExpression) + " ");
                ThrowAssertIsExactInstanceOfTypeFailed(_value, _expectedType, _builder.ToString());
            }
        }

        public void AppendLiteral(string value) => _builder!.Append(value);

        public void AppendFormatted<T>(T value) => AppendFormatted(value, format: null);

#if NETCOREAPP3_1_OR_GREATER
        public void AppendFormatted(ReadOnlySpan<char> value) => _builder!.Append(value);

#pragma warning disable RS0027 // API with optional parameter(s) should have the most parameters amongst its public overloads
        public void AppendFormatted(ReadOnlySpan<char> value, int alignment = 0, string? format = null) => AppendFormatted(value.ToString(), alignment, format);
#pragma warning restore RS0027 // API with optional parameter(s) should have the most parameters amongst its public overloads
#endif

        // NOTE: All the overloads involving format and/or alignment are not super efficient.
        // This code path is only for when an assert is failing, so that's not the common scenario
        // and should be okay if not very optimized.
        // A more efficient implementation that can be used for .NET 6 and later is to delegate the work to
        // the BCL's StringBuilder.AppendInterpolatedStringHandler
        public void AppendFormatted<T>(T value, string? format) => _builder!.AppendFormat(null, $"{{0:{format}}}", value);

        public void AppendFormatted<T>(T value, int alignment) => _builder!.AppendFormat(null, $"{{0,{alignment}}}", value);

        public void AppendFormatted<T>(T value, int alignment, string? format) => _builder!.AppendFormat(null, $"{{0,{alignment}:{format}}}", value);

        public void AppendFormatted(string? value) => _builder!.Append(value);

#pragma warning disable RS0026 // Do not add multiple public overloads with optional parameters
#pragma warning disable RS0027 // API with optional parameter(s) should have the most parameters amongst its public overloads
        public void AppendFormatted(string? value, int alignment = 0, string? format = null) => _builder!.AppendFormat(null, $"{{0,{alignment}:{format}}}", value);

        public void AppendFormatted(object? value, int alignment = 0, string? format = null) => _builder!.AppendFormat(null, $"{{0,{alignment}:{format}}}", value);
#pragma warning restore RS0026 // Do not add multiple public overloads with optional parameters
#pragma warning restore RS0027 // API with optional parameter(s) should have the most parameters amongst its public overloads
    }

    [InterpolatedStringHandler]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public readonly struct AssertGenericIsExactInstanceOfTypeInterpolatedStringHandler<TArg>
    {
        private readonly StringBuilder? _builder;
        private readonly object? _value;

        public AssertGenericIsExactInstanceOfTypeInterpolatedStringHandler(int literalLength, int formattedCount, object? value, out bool shouldAppend)
        {
            _value = value;
            shouldAppend = IsExactInstanceOfTypeFailing(value, typeof(TArg));
            if (shouldAppend)
            {
                _builder = new StringBuilder(literalLength + formattedCount);
            }
        }

        internal void ComputeAssertion(string valueExpression)
        {
            if (_builder is not null)
            {
                _builder.Insert(0, string.Format(CultureInfo.CurrentCulture, FrameworkMessages.CallerArgumentExpressionSingleParameterMessage, "value", valueExpression) + " ");
                ThrowAssertIsExactInstanceOfTypeFailed(_value, typeof(TArg), _builder.ToString());
            }
        }

        public void AppendLiteral(string value) => _builder!.Append(value);

        public void AppendFormatted<T>(T value) => AppendFormatted(value, format: null);

#if NETCOREAPP3_1_OR_GREATER
        public void AppendFormatted(ReadOnlySpan<char> value) => _builder!.Append(value);

#pragma warning disable RS0027 // API with optional parameter(s) should have the most parameters amongst its public overloads
        public void AppendFormatted(ReadOnlySpan<char> value, int alignment = 0, string? format = null) => AppendFormatted(value.ToString(), alignment, format);
#pragma warning restore RS0027 // API with optional parameter(s) should have the most parameters amongst its public overloads
#endif

        // NOTE: All the overloads involving format and/or alignment are not super efficient.
        // This code path is only for when an assert is failing, so that's not the common scenario
        // and should be okay if not very optimized.
        // A more efficient implementation that can be used for .NET 6 and later is to delegate the work to
        // the BCL's StringBuilder.AppendInterpolatedStringHandler
        public void AppendFormatted<T>(T value, string? format) => _builder!.AppendFormat(null, $"{{0:{format}}}", value);

        public void AppendFormatted<T>(T value, int alignment) => _builder!.AppendFormat(null, $"{{0,{alignment}}}", value);

        public void AppendFormatted<T>(T value, int alignment, string? format) => _builder!.AppendFormat(null, $"{{0,{alignment}:{format}}}", value);

        public void AppendFormatted(string? value) => _builder!.Append(value);

#pragma warning disable RS0026 // Do not add multiple public overloads with optional parameters
#pragma warning disable RS0027 // API with optional parameter(s) should have the most parameters amongst its public overloads
        public void AppendFormatted(string? value, int alignment = 0, string? format = null) => _builder!.AppendFormat(null, $"{{0,{alignment}:{format}}}", value);

        public void AppendFormatted(object? value, int alignment = 0, string? format = null) => _builder!.AppendFormat(null, $"{{0,{alignment}:{format}}}", value);
#pragma warning restore RS0026 // Do not add multiple public overloads with optional parameters
#pragma warning restore RS0027 // API with optional parameter(s) should have the most parameters amongst its public overloads
    }

    [InterpolatedStringHandler]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public readonly struct AssertIsNotExactInstanceOfTypeInterpolatedStringHandler
    {
        private readonly StringBuilder? _builder;
        private readonly object? _value;
        private readonly Type? _wrongType;

        public AssertIsNotExactInstanceOfTypeInterpolatedStringHandler(int literalLength, int formattedCount, object? value, Type? wrongType, out bool shouldAppend)
        {
            _value = value;
            _wrongType = wrongType;
            shouldAppend = IsNotExactInstanceOfTypeFailing(value, wrongType);
            if (shouldAppend)
            {
                _builder = new StringBuilder(literalLength + formattedCount);
            }
        }

        internal void ComputeAssertion(string valueExpression)
        {
            if (_builder is not null)
            {
                _builder.Insert(0, string.Format(CultureInfo.CurrentCulture, FrameworkMessages.CallerArgumentExpressionSingleParameterMessage, "value", valueExpression) + " ");
                ThrowAssertIsNotExactInstanceOfTypeFailed(_value, _wrongType, _builder.ToString());
            }
        }

        public void AppendLiteral(string value) => _builder!.Append(value);

        public void AppendFormatted<T>(T value) => AppendFormatted(value, format: null);

#if NETCOREAPP3_1_OR_GREATER
        public void AppendFormatted(ReadOnlySpan<char> value) => _builder!.Append(value);

#pragma warning disable RS0027 // API with optional parameter(s) should have the most parameters amongst its public overloads
        public void AppendFormatted(ReadOnlySpan<char> value, int alignment = 0, string? format = null) => AppendFormatted(value.ToString(), alignment, format);
#pragma warning restore RS0027 // API with optional parameter(s) should have the most parameters amongst its public overloads
#endif

        // NOTE: All the overloads involving format and/or alignment are not super efficient.
        // This code path is only for when an assert is failing, so that's not the common scenario
        // and should be okay if not very optimized.
        // A more efficient implementation that can be used for .NET 6 and later is to delegate the work to
        // the BCL's StringBuilder.AppendInterpolatedStringHandler
        public void AppendFormatted<T>(T value, string? format) => _builder!.AppendFormat(null, $"{{0:{format}}}", value);

        public void AppendFormatted<T>(T value, int alignment) => _builder!.AppendFormat(null, $"{{0,{alignment}}}", value);

        public void AppendFormatted<T>(T value, int alignment, string? format) => _builder!.AppendFormat(null, $"{{0,{alignment}:{format}}}", value);

        public void AppendFormatted(string? value) => _builder!.Append(value);

#pragma warning disable RS0026 // Do not add multiple public overloads with optional parameters
#pragma warning disable RS0027 // API with optional parameter(s) should have the most parameters amongst its public overloads
        public void AppendFormatted(string? value, int alignment = 0, string? format = null) => _builder!.AppendFormat(null, $"{{0,{alignment}:{format}}}", value);

        public void AppendFormatted(object? value, int alignment = 0, string? format = null) => _builder!.AppendFormat(null, $"{{0,{alignment}:{format}}}", value);
#pragma warning restore RS0026 // Do not add multiple public overloads with optional parameters
#pragma warning restore RS0027 // API with optional parameter(s) should have the most parameters amongst its public overloads
    }

    [InterpolatedStringHandler]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public readonly struct AssertGenericIsNotExactInstanceOfTypeInterpolatedStringHandler<TArg>
    {
        private readonly StringBuilder? _builder;
        private readonly object? _value;

        public AssertGenericIsNotExactInstanceOfTypeInterpolatedStringHandler(int literalLength, int formattedCount, object? value, out bool shouldAppend)
        {
            _value = value;
            shouldAppend = IsNotExactInstanceOfTypeFailing(value, typeof(TArg));
            if (shouldAppend)
            {
                _builder = new StringBuilder(literalLength + formattedCount);
            }
        }

        internal void ComputeAssertion(string valueExpression)
        {
            if (_builder is not null)
            {
                _builder.Insert(0, string.Format(CultureInfo.CurrentCulture, FrameworkMessages.CallerArgumentExpressionSingleParameterMessage, "value", valueExpression) + " ");
                ThrowAssertIsNotExactInstanceOfTypeFailed(_value, typeof(TArg), _builder.ToString());
            }
        }

        public void AppendLiteral(string value) => _builder!.Append(value);

        public void AppendFormatted<T>(T value) => AppendFormatted(value, format: null);

#if NETCOREAPP3_1_OR_GREATER
        public void AppendFormatted(ReadOnlySpan<char> value) => _builder!.Append(value);

#pragma warning disable RS0027 // API with optional parameter(s) should have the most parameters amongst its public overloads
        public void AppendFormatted(ReadOnlySpan<char> value, int alignment = 0, string? format = null) => AppendFormatted(value.ToString(), alignment, format);
#pragma warning restore RS0027 // API with optional parameter(s) should have the most parameters amongst its public overloads
#endif

        // NOTE: All the overloads involving format and/or alignment are not super efficient.
        // This code path is only for when an assert is failing, so that's not the common scenario
        // and should be okay if not very optimized.
        // A more efficient implementation that can be used for .NET 6 and later is to delegate the work to
        // the BCL's StringBuilder.AppendInterpolatedStringHandler
        public void AppendFormatted<T>(T value, string? format) => _builder!.AppendFormat(null, $"{{0:{format}}}", value);

        public void AppendFormatted<T>(T value, int alignment) => _builder!.AppendFormat(null, $"{{0,{alignment}}}", value);

        public void AppendFormatted<T>(T value, int alignment, string? format) => _builder!.AppendFormat(null, $"{{0,{alignment}:{format}}}", value);

        public void AppendFormatted(string? value) => _builder!.Append(value);

#pragma warning disable RS0026 // Do not add multiple public overloads with optional parameters
#pragma warning disable RS0027 // API with optional parameter(s) should have the most parameters amongst its public overloads
        public void AppendFormatted(string? value, int alignment = 0, string? format = null) => _builder!.AppendFormat(null, $"{{0,{alignment}:{format}}}", value);

        public void AppendFormatted(object? value, int alignment = 0, string? format = null) => _builder!.AppendFormat(null, $"{{0,{alignment}:{format}}}", value);
#pragma warning restore RS0026 // Do not add multiple public overloads with optional parameters
#pragma warning restore RS0027 // API with optional parameter(s) should have the most parameters amongst its public overloads
    }
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member

#pragma warning disable RS0026 // Do not add multiple public overloads with optional parameters
#pragma warning disable RS0027 // API with optional parameter(s) should have the most parameters amongst its public overloads

    /// <summary>
    /// Tests whether the specified object is exactly an instance of the expected
    /// type and throws an exception if the expected type does not match exactly.
    /// </summary>
    /// <param name="value">
    /// The object the test expects to be of the specified type.
    /// </param>
    /// <param name="expectedType">
    /// The expected exact type of <paramref name="value"/>.
    /// </param>
    /// <param name="message">
    /// The message to include in the exception when <paramref name="value"/>
    /// is not exactly an instance of <paramref name="expectedType"/>. The message is
    /// shown in test results.
    /// </param>
    /// <param name="valueExpression">
    /// The syntactic expression of value as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// Thrown if <paramref name="value"/> is null or
    /// <paramref name="expectedType"/> is not exactly the type
    /// of <paramref name="value"/>.
    /// </exception>
    public static void IsExactInstanceOfType([NotNull] object? value, [NotNull] Type? expectedType, string? message = "", [CallerArgumentExpression(nameof(value))] string valueExpression = "")
    {
        if (IsExactInstanceOfTypeFailing(value, expectedType))
        {
            ThrowAssertIsExactInstanceOfTypeFailed(value, expectedType, BuildUserMessageForValueExpression(message, valueExpression));
        }
    }

    /// <inheritdoc cref="IsExactInstanceOfType(object?, Type?, string, string)" />
#pragma warning disable IDE0060 // Remove unused parameter - https://github.com/dotnet/roslyn/issues/76578
    public static void IsExactInstanceOfType([NotNull] object? value, [NotNull] Type? expectedType, [InterpolatedStringHandlerArgument(nameof(value), nameof(expectedType))] ref AssertIsExactInstanceOfTypeInterpolatedStringHandler message, [CallerArgumentExpression(nameof(value))] string valueExpression = "")
#pragma warning restore IDE0060 // Remove unused parameter
#pragma warning disable CS8777 // Parameter must have a non-null value when exiting. - Not sure how to express the semantics to the compiler, but the implementation guarantees that.
        => message.ComputeAssertion(valueExpression);
#pragma warning restore CS8777 // Parameter must have a non-null value when exiting.

    /// <summary>
    /// Tests whether the specified object is exactly an instance of the generic
    /// type and throws an exception if the generic type does not match exactly.
    /// </summary>
    /// <typeparam name="T">The expected exact type of <paramref name="value"/>.</typeparam>
    public static T IsExactInstanceOfType<T>([NotNull] object? value, string? message = "", [CallerArgumentExpression(nameof(value))] string valueExpression = "")
    {
        IsExactInstanceOfType(value, typeof(T), message, valueExpression);
        return (T)value!;
    }

    /// <inheritdoc cref="IsExactInstanceOfType{T}(object?, string, string)" />
#pragma warning disable IDE0060 // Remove unused parameter - https://github.com/dotnet/roslyn/issues/76578
    public static T IsExactInstanceOfType<T>([NotNull] object? value, [InterpolatedStringHandlerArgument(nameof(value))] ref AssertGenericIsExactInstanceOfTypeInterpolatedStringHandler<T> message, [CallerArgumentExpression(nameof(value))] string valueExpression = "")
#pragma warning restore IDE0060 // Remove unused parameter
#pragma warning disable CS8777 // Parameter must have a non-null value when exiting. - Not sure how to express the semantics to the compiler, but the implementation guarantees that.
    {
        message.ComputeAssertion(valueExpression);
        return (T)value!;
    }
#pragma warning restore CS8777 // Parameter must have a non-null value when exiting.

    private static bool IsExactInstanceOfTypeFailing([NotNullWhen(false)] object? value, [NotNullWhen(false)] Type? expectedType)
        => expectedType is null || value is null || value.GetType() != expectedType;

    [DoesNotReturn]
    private static void ThrowAssertIsExactInstanceOfTypeFailed(object? value, Type? expectedType, string userMessage)
    {
        string finalMessage = userMessage;
        if (expectedType is not null && value is not null)
        {
            finalMessage = string.Format(
                CultureInfo.CurrentCulture,
                FrameworkMessages.IsExactInstanceOfFailMsg,
                userMessage,
                expectedType.ToString(),
                value.GetType().ToString());
        }

        ThrowAssertFailed("Assert.IsExactInstanceOfType", finalMessage);
    }

    /// <summary>
    /// Tests whether the specified object is not exactly an instance of the wrong
    /// type and throws an exception if the specified type matches exactly.
    /// </summary>
    /// <param name="value">
    /// The object the test expects not to be exactly of the specified type.
    /// </param>
    /// <param name="wrongType">
    /// The exact type that <paramref name="value"/> should not be.
    /// </param>
    /// <param name="message">
    /// The message to include in the exception when <paramref name="value"/>
    /// is exactly an instance of <paramref name="wrongType"/>. The message is shown
    /// in test results.
    /// </param>
    /// <param name="valueExpression">
    /// The syntactic expression of value as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// Thrown if <paramref name="value"/> is not null and
    /// <paramref name="wrongType"/> is exactly the type
    /// of <paramref name="value"/>.
    /// </exception>
    public static void IsNotExactInstanceOfType(object? value, [NotNull] Type? wrongType, string? message = "", [CallerArgumentExpression(nameof(value))] string valueExpression = "")
    {
        if (IsNotExactInstanceOfTypeFailing(value, wrongType))
        {
            ThrowAssertIsNotExactInstanceOfTypeFailed(value, wrongType, BuildUserMessageForValueExpression(message, valueExpression));
        }
    }

    /// <inheritdoc cref="IsNotExactInstanceOfType(object?, Type?, string, string)" />
#pragma warning disable IDE0060 // Remove unused parameter - https://github.com/dotnet/roslyn/issues/76578
    public static void IsNotExactInstanceOfType(object? value, [NotNull] Type? wrongType, [InterpolatedStringHandlerArgument(nameof(value), nameof(wrongType))] ref AssertIsNotExactInstanceOfTypeInterpolatedStringHandler message, [CallerArgumentExpression(nameof(value))] string valueExpression = "")
#pragma warning restore IDE0060 // Remove unused parameter
#pragma warning disable CS8777 // Parameter must have a non-null value when exiting. - Not sure how to express the semantics to the compiler, but the implementation guarantees that.
        => message.ComputeAssertion(valueExpression);
#pragma warning restore CS8777 // Parameter must have a non-null value when exiting.

    /// <summary>
    /// Tests whether the specified object is not exactly an instance of the wrong generic
    /// type and throws an exception if the specified type matches exactly.
    /// </summary>
    /// <typeparam name="T">The exact type that <paramref name="value"/> should not be.</typeparam>
    public static void IsNotExactInstanceOfType<T>(object? value, string? message = "", [CallerArgumentExpression(nameof(value))] string valueExpression = "")
        => IsNotExactInstanceOfType(value, typeof(T), message, valueExpression);

    /// <inheritdoc cref="IsNotExactInstanceOfType{T}(object?, string, string)" />
#pragma warning disable IDE0060 // Remove unused parameter - https://github.com/dotnet/roslyn/issues/76578
    public static void IsNotExactInstanceOfType<T>(object? value, [InterpolatedStringHandlerArgument(nameof(value))] ref AssertGenericIsNotExactInstanceOfTypeInterpolatedStringHandler<T> message, [CallerArgumentExpression(nameof(value))] string valueExpression = "")
#pragma warning restore IDE0060 // Remove unused parameter
        => message.ComputeAssertion(valueExpression);

    private static bool IsNotExactInstanceOfTypeFailing(object? value, [NotNullWhen(false)] Type? wrongType)
        => wrongType is null ||
            // Null is not an instance of any type.
            (value is not null && value.GetType() == wrongType);

    [DoesNotReturn]
    private static void ThrowAssertIsNotExactInstanceOfTypeFailed(object? value, Type? wrongType, string userMessage)
    {
        string finalMessage = userMessage;
        if (wrongType is not null)
        {
            finalMessage = string.Format(
                CultureInfo.CurrentCulture,
                FrameworkMessages.IsNotExactInstanceOfFailMsg,
                userMessage,
                wrongType.ToString(),
                value!.GetType().ToString());
        }

        ThrowAssertFailed("Assert.IsNotExactInstanceOfType", finalMessage);
    }
}
