﻿// Copyright (c) Microsoft Corporation. All rights reserved.
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
    [InterpolatedStringHandler]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public readonly struct AssertIsInstanceOfTypeInterpolatedStringHandler
    {
        private readonly StringBuilder? _builder;
        private readonly object? _value;
        private readonly Type? _expectedType;

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

        internal void ComputeAssertion()
        {
            if (_builder is not null)
            {
                ThrowAssertIsInstanceOfTypeFailed(_value, _expectedType, _builder.ToString());
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
    public readonly struct AssertGenericIsInstanceOfTypeInterpolatedStringHandler<TArg>
    {
        private readonly StringBuilder? _builder;
        private readonly object? _value;

        public AssertGenericIsInstanceOfTypeInterpolatedStringHandler(int literalLength, int formattedCount, object? value, out bool shouldAppend)
        {
            _value = value;
            shouldAppend = IsInstanceOfTypeFailing(value, typeof(TArg));
            if (shouldAppend)
            {
                _builder = new StringBuilder(literalLength + formattedCount);
            }
        }

        internal void ComputeAssertion()
        {
            if (_builder is not null)
            {
                ThrowAssertIsInstanceOfTypeFailed(_value, typeof(TArg), _builder.ToString());
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
    public readonly struct AssertIsNotInstanceOfTypeInterpolatedStringHandler
    {
        private readonly StringBuilder? _builder;
        private readonly object? _value;
        private readonly Type? _wrongType;

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

        internal void ComputeAssertion()
        {
            if (_builder is not null)
            {
                ThrowAssertIsNotInstanceOfTypeFailed(_value, _wrongType, _builder.ToString());
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
    public readonly struct AssertGenericIsNotInstanceOfTypeInterpolatedStringHandler<TArg>
    {
        private readonly StringBuilder? _builder;
        private readonly object? _value;

        public AssertGenericIsNotInstanceOfTypeInterpolatedStringHandler(int literalLength, int formattedCount, object? value, out bool shouldAppend)
        {
            _value = value;
            shouldAppend = IsNotInstanceOfTypeFailing(value, typeof(TArg));
            if (shouldAppend)
            {
                _builder = new StringBuilder(literalLength + formattedCount);
            }
        }

        internal void ComputeAssertion()
        {
            if (_builder is not null)
            {
                ThrowAssertIsNotInstanceOfTypeFailed(_value, typeof(TArg), _builder.ToString());
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
    /// <exception cref="AssertFailedException">
    /// Thrown if <paramref name="value"/> is null or
    /// <paramref name="expectedType"/> is not in the inheritance hierarchy
    /// of <paramref name="value"/>.
    /// </exception>
    public static void IsInstanceOfType([NotNull] object? value, [NotNull] Type? expectedType)
        => IsInstanceOfType(value, expectedType, string.Empty, null);

    /// <summary>
    /// Tests whether the specified object is an instance of the generic
    /// type and throws an exception if the generic type is not in the
    /// inheritance hierarchy of the object.
    /// </summary>
    /// <typeparam name="T">The expected type of <paramref name="value"/>.</typeparam>
    public static void IsInstanceOfType<T>([NotNull] object? value)
        => IsInstanceOfType(value, typeof(T), string.Empty, null);

    /// <summary>
    /// Tests whether the specified object is an instance of the generic
    /// type and throws an exception if the generic type is not in the
    /// inheritance hierarchy of the object.
    /// </summary>
    /// <typeparam name="T">The expected type of <paramref name="value"/>.</typeparam>
    public static void IsInstanceOfType<T>([NotNull] object? value, out T instance)
        => IsInstanceOfType(value, out instance, string.Empty, null);

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
    /// <exception cref="AssertFailedException">
    /// Thrown if <paramref name="value"/> is null or
    /// <paramref name="expectedType"/> is not in the inheritance hierarchy
    /// of <paramref name="value"/>.
    /// </exception>
    public static void IsInstanceOfType([NotNull] object? value, [NotNull] Type? expectedType, string? message)
        => IsInstanceOfType(value, expectedType, message, null);

    /// <inheritdoc cref="IsInstanceOfType(object?, Type?, string?)" />
#pragma warning disable IDE0060 // Remove unused parameter - https://github.com/dotnet/roslyn/issues/76578
    public static void IsInstanceOfType([NotNull] object? value, [NotNull] Type? expectedType, [InterpolatedStringHandlerArgument(nameof(value), nameof(expectedType))] ref AssertIsInstanceOfTypeInterpolatedStringHandler message)
#pragma warning restore IDE0060 // Remove unused parameter
#pragma warning disable CS8777 // Parameter must have a non-null value when exiting. - Not sure how to express the semantics to the compiler, but the implementation guarantees that.
        => message.ComputeAssertion();
#pragma warning restore CS8777 // Parameter must have a non-null value when exiting.

    /// <summary>
    /// Tests whether the specified object is an instance of the generic
    /// type and throws an exception if the generic type is not in the
    /// inheritance hierarchy of the object.
    /// </summary>
    /// <typeparam name="T">The expected type of <paramref name="value"/>.</typeparam>
    public static void IsInstanceOfType<T>([NotNull] object? value, string? message)
        => IsInstanceOfType(value, typeof(T), message, null);

    /// <inheritdoc cref="IsInstanceOfType{T}(object?, string?)" />
#pragma warning disable IDE0060 // Remove unused parameter - https://github.com/dotnet/roslyn/issues/76578
    public static void IsInstanceOfType<T>([NotNull] object? value, [InterpolatedStringHandlerArgument(nameof(value))] ref AssertGenericIsInstanceOfTypeInterpolatedStringHandler<T> message)
#pragma warning restore IDE0060 // Remove unused parameter
#pragma warning disable CS8777 // Parameter must have a non-null value when exiting. - Not sure how to express the semantics to the compiler, but the implementation guarantees that.
        => message.ComputeAssertion();
#pragma warning restore CS8777 // Parameter must have a non-null value when exiting.

    /// <summary>
    /// Tests whether the specified object is an instance of the generic
    /// type and throws an exception if the generic type is not in the
    /// inheritance hierarchy of the object.
    /// </summary>
    /// <typeparam name="T">The expected type of <paramref name="value"/>.</typeparam>
    public static void IsInstanceOfType<T>([NotNull] object? value, out T instance, string? message)
        => IsInstanceOfType(value, out instance, message, null);

    /// <inheritdoc cref="IsInstanceOfType{T}(object?, out T, string?)" />
    public static void IsInstanceOfType<T>([NotNull] object? value, out T instance, [InterpolatedStringHandlerArgument(nameof(value))] ref AssertGenericIsInstanceOfTypeInterpolatedStringHandler<T> message)
#pragma warning disable CS8777 // Parameter must have a non-null value when exiting. - Not sure how to express the semantics to the compiler, but the implementation guarantees that.
    {
        message.ComputeAssertion();
        instance = (T)value!;
    }
#pragma warning restore CS8777 // Parameter must have a non-null value when exiting.

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
    /// <param name="parameters">
    /// An array of parameters to use when formatting <paramref name="message"/>.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// Thrown if <paramref name="value"/> is null or
    /// <paramref name="expectedType"/> is not in the inheritance hierarchy
    /// of <paramref name="value"/>.
    /// </exception>
    public static void IsInstanceOfType([NotNull] object? value, [NotNull] Type? expectedType, [StringSyntax(StringSyntaxAttribute.CompositeFormat)] string? message,
        params object?[]? parameters)
    {
        if (IsInstanceOfTypeFailing(value, expectedType))
        {
            ThrowAssertIsInstanceOfTypeFailed(value, expectedType, BuildUserMessage(message, parameters));
        }
    }

    private static bool IsInstanceOfTypeFailing([NotNullWhen(false)] object? value, [NotNullWhen(false)] Type? expectedType)
        => expectedType == null || value == null || !expectedType.IsInstanceOfType(value);

    [DoesNotReturn]
    private static void ThrowAssertIsInstanceOfTypeFailed(object? value, Type? expectedType, string userMessage)
    {
        string finalMessage = userMessage;
        if (expectedType is not null && value is not null)
        {
            finalMessage = string.Format(
                CultureInfo.CurrentCulture,
                FrameworkMessages.IsInstanceOfFailMsg,
                userMessage,
                expectedType.ToString(),
                value.GetType().ToString());
        }

        ThrowAssertFailed("Assert.IsInstanceOfType", finalMessage);
    }

    /// <summary>
    /// Tests whether the specified object is an instance of the generic
    /// type and throws an exception if the generic type is not in the
    /// inheritance hierarchy of the object.
    /// </summary>
    /// <typeparam name="T">The expected type of <paramref name="value"/>.</typeparam>
    public static void IsInstanceOfType<T>([NotNull] object? value, [StringSyntax(StringSyntaxAttribute.CompositeFormat)] string? message, params object?[]? parameters)
        => IsInstanceOfType(value, typeof(T), message, parameters);

    /// <summary>
    /// Tests whether the specified object is an instance of the generic
    /// type and throws an exception if the generic type is not in the
    /// inheritance hierarchy of the object.
    /// </summary>
    /// <typeparam name="T">The expected type of <paramref name="value"/>.</typeparam>
    public static void IsInstanceOfType<T>([NotNull] object? value, out T instance, [StringSyntax(StringSyntaxAttribute.CompositeFormat)] string? message, params object?[]? parameters)
    {
#pragma warning disable CA2263 // Prefer generic overload when type is known
        IsInstanceOfType(value, typeof(T), message, parameters);
#pragma warning restore CA2263 // Prefer generic overload when type is known
        instance = (T)value;
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
    /// <exception cref="AssertFailedException">
    /// Thrown if <paramref name="value"/> is not null and
    /// <paramref name="wrongType"/> is in the inheritance hierarchy
    /// of <paramref name="value"/>.
    /// </exception>
    public static void IsNotInstanceOfType(object? value, [NotNull] Type? wrongType)
        => IsNotInstanceOfType(value, wrongType, string.Empty, null);

    /// <summary>
    /// Tests whether the specified object is not an instance of the wrong generic
    /// type and throws an exception if the specified type is in the
    /// inheritance hierarchy of the object.
    /// </summary>
    /// <typeparam name="T">The type that <paramref name="value"/> should not be.</typeparam>
    public static void IsNotInstanceOfType<T>(object? value)
        => IsNotInstanceOfType(value, typeof(T), string.Empty, null);

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
    /// <exception cref="AssertFailedException">
    /// Thrown if <paramref name="value"/> is not null and
    /// <paramref name="wrongType"/> is in the inheritance hierarchy
    /// of <paramref name="value"/>.
    /// </exception>
    public static void IsNotInstanceOfType(object? value, [NotNull] Type? wrongType, string? message)
        => IsNotInstanceOfType(value, wrongType, message, null);

    /// <inheritdoc cref="IsNotInstanceOfType(object?, Type?, string?)" />
#pragma warning disable IDE0060 // Remove unused parameter - https://github.com/dotnet/roslyn/issues/76578
    public static void IsNotInstanceOfType(object? value, [NotNull] Type? wrongType, [InterpolatedStringHandlerArgument(nameof(value), nameof(wrongType))] ref AssertIsNotInstanceOfTypeInterpolatedStringHandler message)
#pragma warning restore IDE0060 // Remove unused parameter
#pragma warning disable CS8777 // Parameter must have a non-null value when exiting. - Not sure how to express the semantics to the compiler, but the implementation guarantees that.
        => message.ComputeAssertion();
#pragma warning restore CS8777 // Parameter must have a non-null value when exiting.

    /// <summary>
    /// Tests whether the specified object is not an instance of the wrong generic
    /// type and throws an exception if the specified type is in the
    /// inheritance hierarchy of the object.
    /// </summary>
    /// <typeparam name="T">The type that <paramref name="value"/> should not be.</typeparam>
    public static void IsNotInstanceOfType<T>(object? value, string? message)
        => IsNotInstanceOfType(value, typeof(T), message, null);

    /// <inheritdoc cref="IsNotInstanceOfType{T}(object?, string?)" />
#pragma warning disable IDE0060 // Remove unused parameter - https://github.com/dotnet/roslyn/issues/76578
    public static void IsNotInstanceOfType<T>(object? value, [InterpolatedStringHandlerArgument(nameof(value))] AssertGenericIsNotInstanceOfTypeInterpolatedStringHandler<T> message)
#pragma warning restore IDE0060 // Remove unused parameter
        => message.ComputeAssertion();

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
    /// <param name="parameters">
    /// An array of parameters to use when formatting <paramref name="message"/>.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// Thrown if <paramref name="value"/> is not null and
    /// <paramref name="wrongType"/> is in the inheritance hierarchy
    /// of <paramref name="value"/>.
    /// </exception>
    public static void IsNotInstanceOfType(object? value, [NotNull] Type? wrongType, [StringSyntax(StringSyntaxAttribute.CompositeFormat)] string? message,
        params object?[]? parameters)
    {
        if (IsNotInstanceOfTypeFailing(value, wrongType))
        {
            ThrowAssertIsNotInstanceOfTypeFailed(value, wrongType, BuildUserMessage(message, parameters));
        }
    }

    private static bool IsNotInstanceOfTypeFailing(object? value, [NotNullWhen(false)] Type? wrongType)
        => wrongType is null ||
            // Null is not an instance of any type.
            (value is not null && wrongType.IsInstanceOfType(value));

    [DoesNotReturn]
    private static void ThrowAssertIsNotInstanceOfTypeFailed(object? value, Type? wrongType, string userMessage)
    {
        string finalMessage = userMessage;
        if (wrongType is not null)
        {
            finalMessage = string.Format(
                CultureInfo.CurrentCulture,
                FrameworkMessages.IsNotInstanceOfFailMsg,
                userMessage,
                wrongType.ToString(),
                value!.GetType().ToString());
        }

        ThrowAssertFailed("Assert.IsNotInstanceOfType", finalMessage);
    }

    /// <summary>
    /// Tests whether the specified object is not an instance of the wrong generic
    /// type and throws an exception if the specified type is in the
    /// inheritance hierarchy of the object.
    /// </summary>
    /// <typeparam name="T">The type that <paramref name="value"/> should not be.</typeparam>
    public static void IsNotInstanceOfType<T>(object? value, [StringSyntax(StringSyntaxAttribute.CompositeFormat)] string? message, params object?[]? parameters)
        => IsNotInstanceOfType(value, typeof(T), message, parameters);
}
