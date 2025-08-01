// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.ComponentModel;
using System.Drawing;

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
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
    public readonly struct AssertAreEqualInterpolatedStringHandler<TArgument>
    {
        private readonly StringBuilder? _builder;
        private readonly object? _expected;
        private readonly object? _actual;

        public AssertAreEqualInterpolatedStringHandler(int literalLength, int formattedCount, TArgument? expected, TArgument? actual, out bool shouldAppend)
        {
            _expected = expected!;
            shouldAppend = AreEqualFailing(expected, actual);
            if (shouldAppend)
            {
                _builder = new StringBuilder(literalLength + formattedCount);
                _expected = expected;
                _actual = actual;
            }
        }

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

        public AssertAreEqualInterpolatedStringHandler(int literalLength, int formattedCount, IEquatable<TArgument>? expected, IEquatable<TArgument>? actual, out bool shouldAppend)
        {
            shouldAppend = AreEqualFailing(expected, actual);
            if (shouldAppend)
            {
                _builder = new StringBuilder(literalLength + formattedCount);
                _expected = expected;
                _actual = actual;
            }
        }

        internal void ComputeAssertion()
        {
            if (_builder is not null)
            {
                ThrowAssertAreEqualFailed(_expected, _actual, _builder.ToString());
            }
        }

        public void AppendLiteral(string? value) => _builder!.Append(value);

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
    public readonly struct AssertAreNotEqualInterpolatedStringHandler<TArgument>
    {
        private readonly StringBuilder? _builder;
        private readonly object? _notExpected;
        private readonly object? _actual;

        public AssertAreNotEqualInterpolatedStringHandler(int literalLength, int formattedCount, TArgument? notExpected, TArgument? actual, out bool shouldAppend)
            : this(literalLength, formattedCount, notExpected, actual, null, out shouldAppend)
        {
        }

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

        internal void ComputeAssertion()
        {
            if (_builder is not null)
            {
                ThrowAssertAreNotEqualFailed(_notExpected, _actual, _builder.ToString());
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
    public readonly struct AssertNonGenericAreEqualInterpolatedStringHandler
    {
        private readonly StringBuilder? _builder;
        private readonly Action<string>? _failAction;

        public AssertNonGenericAreEqualInterpolatedStringHandler(int literalLength, int formattedCount, float expected, float actual, float delta, out bool shouldAppend)
        {
            shouldAppend = AreEqualFailing(expected, actual, delta);
            if (shouldAppend)
            {
                _builder = new StringBuilder(literalLength + formattedCount);
                _failAction = userMessage => ThrowAssertAreEqualFailed(expected, actual, delta, userMessage);
            }
        }

        public AssertNonGenericAreEqualInterpolatedStringHandler(int literalLength, int formattedCount, decimal expected, decimal actual, decimal delta, out bool shouldAppend)
        {
            shouldAppend = AreEqualFailing(expected, actual, delta);
            if (shouldAppend)
            {
                _builder = new StringBuilder(literalLength + formattedCount);
                _failAction = userMessage => ThrowAssertAreEqualFailed(expected, actual, delta, userMessage);
            }
        }

        public AssertNonGenericAreEqualInterpolatedStringHandler(int literalLength, int formattedCount, long expected, long actual, long delta, out bool shouldAppend)
        {
            shouldAppend = AreEqualFailing(expected, actual, delta);
            if (shouldAppend)
            {
                _builder = new StringBuilder(literalLength + formattedCount);
                _failAction = userMessage => ThrowAssertAreEqualFailed(expected, actual, delta, userMessage);
            }
        }

        public AssertNonGenericAreEqualInterpolatedStringHandler(int literalLength, int formattedCount, double expected, double actual, double delta, out bool shouldAppend)
        {
            shouldAppend = AreEqualFailing(expected, actual, delta);
            if (shouldAppend)
            {
                _builder = new StringBuilder(literalLength + formattedCount);
                _failAction = userMessage => ThrowAssertAreEqualFailed(expected, actual, delta, userMessage);
            }
        }

        public AssertNonGenericAreEqualInterpolatedStringHandler(int literalLength, int formattedCount, string? expected, string? actual, bool ignoreCase, out bool shouldAppend)
            : this(literalLength, formattedCount, expected, actual, ignoreCase, CultureInfo.InvariantCulture, out shouldAppend)
        {
        }

        public AssertNonGenericAreEqualInterpolatedStringHandler(int literalLength, int formattedCount, string? expected, string? actual, bool ignoreCase, CultureInfo culture, out bool shouldAppend)
        {
            Guard.NotNull(culture);
            shouldAppend = AreEqualFailing(expected, actual, ignoreCase, culture);
            if (shouldAppend)
            {
                _builder = new StringBuilder(literalLength + formattedCount);
                _failAction = userMessage => ThrowAssertAreEqualFailed(expected, actual, ignoreCase, culture, userMessage);
            }
        }

        internal void ComputeAssertion()
            => _failAction?.Invoke(_builder!.ToString());

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
    public readonly struct AssertNonGenericAreNotEqualInterpolatedStringHandler
    {
        private readonly StringBuilder? _builder;
        private readonly Action<string>? _failAction;

        public AssertNonGenericAreNotEqualInterpolatedStringHandler(int literalLength, int formattedCount, float notExpected, float actual, float delta, out bool shouldAppend)
        {
            shouldAppend = AreNotEqualFailing(notExpected, actual, delta);
            if (shouldAppend)
            {
                _builder = new StringBuilder(literalLength + formattedCount);
                _failAction = userMessage => ThrowAssertAreNotEqualFailed(notExpected, actual, delta, userMessage);
            }
        }

        public AssertNonGenericAreNotEqualInterpolatedStringHandler(int literalLength, int formattedCount, decimal notExpected, decimal actual, decimal delta, out bool shouldAppend)
        {
            shouldAppend = AreNotEqualFailing(notExpected, actual, delta);
            if (shouldAppend)
            {
                _builder = new StringBuilder(literalLength + formattedCount);
                _failAction = userMessage => ThrowAssertAreNotEqualFailed(notExpected, actual, delta, userMessage);
            }
        }

        public AssertNonGenericAreNotEqualInterpolatedStringHandler(int literalLength, int formattedCount, long notExpected, long actual, long delta, out bool shouldAppend)
        {
            shouldAppend = AreNotEqualFailing(notExpected, actual, delta);
            if (shouldAppend)
            {
                _builder = new StringBuilder(literalLength + formattedCount);
                _failAction = userMessage => ThrowAssertAreNotEqualFailed(notExpected, actual, delta, userMessage);
            }
        }

        public AssertNonGenericAreNotEqualInterpolatedStringHandler(int literalLength, int formattedCount, double notExpected, double actual, double delta, out bool shouldAppend)
        {
            shouldAppend = AreNotEqualFailing(notExpected, actual, delta);
            if (shouldAppend)
            {
                _builder = new StringBuilder(literalLength + formattedCount);
                _failAction = userMessage => ThrowAssertAreNotEqualFailed(notExpected, actual, delta, userMessage);
            }
        }

        public AssertNonGenericAreNotEqualInterpolatedStringHandler(int literalLength, int formattedCount, string? notExpected, string? actual, bool ignoreCase, out bool shouldAppend)
            : this(literalLength, formattedCount, notExpected, actual, ignoreCase, CultureInfo.InvariantCulture, out shouldAppend)
        {
        }

        public AssertNonGenericAreNotEqualInterpolatedStringHandler(int literalLength, int formattedCount, string? notExpected, string? actual, bool ignoreCase, CultureInfo culture, out bool shouldAppend)
        {
            Guard.NotNull(culture);
            shouldAppend = AreNotEqualFailing(notExpected, actual, ignoreCase, culture);
            if (shouldAppend)
            {
                _builder = new StringBuilder(literalLength + formattedCount);
                _failAction = userMessage => ThrowAssertAreNotEqualFailed(notExpected, actual, userMessage);
            }
        }

        internal void ComputeAssertion()
            => _failAction?.Invoke(_builder!.ToString());

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

    /// <summary>
    /// Tests whether the specified values are equal and throws an exception
    /// if the two values are not equal.
    /// The equality is computed using the default <see cref="EqualityComparer{T}"/>.
    /// </summary>
    /// <typeparam name="T">
    /// The type of values to compare.
    /// </typeparam>
    /// <param name="expected">
    /// The first value to compare. This is the value the tests expects.
    /// </param>
    /// <param name="actual">
    /// The second value to compare. This is the value produced by the code under test.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// Thrown if <paramref name="expected"/> is not equal to <paramref name="actual"/>.
    /// </exception>
    public static void AreEqual<T>(T? expected, T? actual)
        => AreEqual(expected, actual, null, string.Empty, null);

    /// <summary>
    /// Tests whether the specified values are equal and throws an exception
    /// if the two values are not equal.
    /// The equality is computed using the default <see cref="EqualityComparer{T}"/>.
    /// </summary>
    /// <typeparam name="T">
    /// The type of values to compare.
    /// </typeparam>
    /// <param name="expected">
    /// The first value to compare. This is the value the tests expects.
    /// </param>
    /// <param name="actual">
    /// The second value to compare. This is the value produced by the code under test.
    /// </param>
    /// <param name="comparer">
    /// The <see cref="IEqualityComparer{T}"/> implementation to use when comparing keys,
    /// or null to use the default <see cref="EqualityComparer{T}"/>.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// Thrown if <paramref name="expected"/> is not equal to <paramref name="actual"/>.
    /// </exception>
    public static void AreEqual<T>(T? expected, T? actual, IEqualityComparer<T>? comparer)
        => AreEqual(expected, actual, comparer, string.Empty, null);

    /// <summary>
    /// Tests whether the specified values are equal and throws an exception
    /// if the two values are not equal.
    /// The equality is computed using the default <see cref="EqualityComparer{T}"/>.
    /// </summary>
    /// <typeparam name="T">
    /// The type of values to compare.
    /// </typeparam>
    /// <param name="expected">
    /// The first value to compare. This is the value the tests expects.
    /// </param>
    /// <param name="actual">
    /// The second value to compare. This is the value produced by the code under test.
    /// </param>
    /// <param name="message">
    /// The message to include in the exception when <paramref name="actual"/>
    /// is not equal to <paramref name="expected"/>. The message is shown in
    /// test results.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// Thrown if <paramref name="expected"/> is not equal to
    /// <paramref name="actual"/>.
    /// </exception>
    public static void AreEqual<T>(T? expected, T? actual, string? message)
        => AreEqual(expected, actual, null, message, null);

    /// <inheritdoc cref="AreEqual{T}(IEquatable{T}?, IEquatable{T}?, string?)" />
#pragma warning disable IDE0060 // Remove unused parameter - https://github.com/dotnet/roslyn/issues/76578
    public static void AreEqual<T>(T? expected, T? actual, [InterpolatedStringHandlerArgument(nameof(expected), nameof(actual))] ref AssertAreEqualInterpolatedStringHandler<T> message)
#pragma warning restore IDE0060 // Remove unused parameter
        => message.ComputeAssertion();

    /// <summary>
    /// Tests whether the specified values are equal and throws an exception
    /// if the two values are not equal.
    /// The equality is computed using the provided <paramref name="comparer"/> parameter.
    /// </summary>
    /// <typeparam name="T">
    /// The type of values to compare.
    /// </typeparam>
    /// <param name="expected">
    /// The first value to compare. This is the value the tests expects.
    /// </param>
    /// <param name="actual">
    /// The second value to compare. This is the value produced by the code under test.
    /// </param>
    /// <param name="comparer">
    /// The <see cref="IEqualityComparer{T}"/> implementation to use when comparing keys,
    /// or null to use the default <see cref="EqualityComparer{T}"/>.
    /// </param>
    /// <param name="message">
    /// The message to include in the exception when <paramref name="actual"/>
    /// is not equal to <paramref name="expected"/>. The message is shown in
    /// test results.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// Thrown if <paramref name="expected"/> is not equal to
    /// <paramref name="actual"/>.
    /// </exception>
    public static void AreEqual<T>(T? expected, T? actual, IEqualityComparer<T>? comparer, string? message)
        => AreEqual(expected, actual, comparer, message, null);

    /// <inheritdoc cref="AreEqual{T}(T, T, IEqualityComparer{T}?, string?)" />
#pragma warning disable IDE0060 // Remove unused parameter - https://github.com/dotnet/roslyn/issues/76578
    public static void AreEqual<T>(T? expected, T? actual, IEqualityComparer<T>? comparer, [InterpolatedStringHandlerArgument(nameof(expected), nameof(actual), nameof(comparer))] ref AssertAreEqualInterpolatedStringHandler<T> message)
#pragma warning restore IDE0060 // Remove unused parameter
        => message.ComputeAssertion();

    /// <summary>
    /// Tests whether the specified values are equal and throws an exception
    /// if the two values are not equal.
    /// The equality is computed using the default <see cref="EqualityComparer{T}"/>.
    /// </summary>
    /// <typeparam name="T">
    /// The type of values to compare.
    /// </typeparam>
    /// <param name="expected">
    /// The first value to compare. This is the value the tests expects.
    /// </param>
    /// <param name="actual">
    /// The second value to compare. This is the value produced by the code under test.
    /// </param>
    /// <param name="message">
    /// The message to include in the exception when <paramref name="actual"/>
    /// is not equal to <paramref name="expected"/>. The message is shown in
    /// test results.
    /// </param>
    /// <param name="parameters">
    /// An array of parameters to use when formatting <paramref name="message"/>.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// Thrown if <paramref name="expected"/> is not equal to
    /// <paramref name="actual"/>.
    /// </exception>
    public static void AreEqual<T>(T? expected, T? actual, [StringSyntax(StringSyntaxAttribute.CompositeFormat)] string? message, params object?[]? parameters)
        => AreEqual(expected, actual, null, message, parameters);

    /// <summary>
    /// Tests whether the specified values are equal and throws an exception
    /// if the two values are not equal.
    /// The equality is computed using the provided <paramref name="comparer"/> parameter.
    /// </summary>
    /// <typeparam name="T">
    /// The type of values to compare.
    /// </typeparam>
    /// <param name="expected">
    /// The first value to compare. This is the value the tests expects.
    /// </param>
    /// <param name="actual">
    /// The second value to compare. This is the value produced by the code under test.
    /// </param>
    /// <param name="comparer">
    /// The <see cref="IEqualityComparer{T}"/> implementation to use when comparing keys,
    /// or null to use the default <see cref="EqualityComparer{T}"/>.
    /// </param>
    /// <param name="message">
    /// The message to include in the exception when <paramref name="actual"/>
    /// is not equal to <paramref name="expected"/>. The message is shown in
    /// test results.
    /// </param>
    /// <param name="parameters">
    /// An array of parameters to use when formatting <paramref name="message"/>.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// Thrown if <paramref name="expected"/> is not equal to
    /// <paramref name="actual"/>.
    /// </exception>
    public static void AreEqual<T>(T? expected, T? actual, IEqualityComparer<T>? comparer,
        [StringSyntax(StringSyntaxAttribute.CompositeFormat)] string? message, params object?[]? parameters)
    {
        if (!AreEqualFailing(expected, actual, comparer))
        {
            return;
        }

        string userMessage = BuildUserMessage(message, parameters);
        ThrowAssertAreEqualFailed(expected, actual, userMessage);
    }

    /// <summary>
    /// Tests whether the specified values are equal and throws an exception
    /// if the two values are not equal.
    /// The equality is computed using the default <see cref="EqualityComparer{T}"/>.
    /// </summary>
    /// <typeparam name="T">
    /// The type of values to compare.
    /// </typeparam>
    /// <param name="expected">
    /// The first value to compare. This is the value the tests expects.
    /// </param>
    /// <param name="actual">
    /// The second value to compare. This is the value produced by the code under test.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// Thrown if <paramref name="expected"/> is not equal to <paramref name="actual"/>.
    /// </exception>
    public static void AreEqual<T>(IEquatable<T>? expected, IEquatable<T>? actual)
        => AreEqual(expected, actual, string.Empty, null);

    /// <summary>
    /// Tests whether the specified values are equal and throws an exception
    /// if the two values are not equal.
    /// The equality is computed using the default <see cref="EqualityComparer{T}"/>.
    /// </summary>
    /// <typeparam name="T">
    /// The type of values to compare.
    /// </typeparam>
    /// <param name="expected">
    /// The first value to compare. This is the value the tests expects.
    /// </param>
    /// <param name="actual">
    /// The second value to compare. This is the value produced by the code under test.
    /// </param>
    /// <param name="message">
    /// The message to include in the exception when <paramref name="actual"/>
    /// is not equal to <paramref name="expected"/>. The message is shown in
    /// test results.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// Thrown if <paramref name="expected"/> is not equal to
    /// <paramref name="actual"/>.
    /// </exception>
    public static void AreEqual<T>(IEquatable<T>? expected, IEquatable<T>? actual, string? message)
        => AreEqual(expected, actual, message, null);

    /// <inheritdoc cref="AreEqual{T}(IEquatable{T}?, IEquatable{T}?, string?)"/>
#pragma warning disable IDE0060 // Remove unused parameter - https://github.com/dotnet/roslyn/issues/76578
    public static void AreEqual<T>(IEquatable<T>? expected, IEquatable<T>? actual, [InterpolatedStringHandlerArgument(nameof(expected), nameof(actual))] ref AssertAreEqualInterpolatedStringHandler<T> message)
#pragma warning restore IDE0060 // Remove unused parameter
        => message.ComputeAssertion();

    /// <summary>
    /// Tests whether the specified values are equal and throws an exception
    /// if the two values are not equal.
    /// The equality is computed using the default <see cref="EqualityComparer{T}"/>.
    /// </summary>
    /// <typeparam name="T">
    /// The type of values to compare.
    /// </typeparam>
    /// <param name="expected">
    /// The first value to compare. This is the value the tests expects.
    /// </param>
    /// <param name="actual">
    /// The second value to compare. This is the value produced by the code under test.
    /// </param>
    /// <param name="message">
    /// The message to include in the exception when <paramref name="actual"/>
    /// is not equal to <paramref name="expected"/>. The message is shown in
    /// test results.
    /// </param>
    /// <param name="parameters">
    /// An array of parameters to use when formatting <paramref name="message"/>.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// Thrown if <paramref name="expected"/> is not equal to
    /// <paramref name="actual"/>.
    /// </exception>
    public static void AreEqual<T>(IEquatable<T>? expected, IEquatable<T>? actual, [StringSyntax(StringSyntaxAttribute.CompositeFormat)] string? message, params object?[]? parameters)
    {
        if (!AreEqualFailing(expected, actual))
        {
            return;
        }

        string userMessage = BuildUserMessage(message, parameters);
        ThrowAssertAreEqualFailed(expected, actual, userMessage);
    }

    private static bool AreEqualFailing<T>(T? expected, T? actual)
        => AreEqualFailing(expected, actual, null);

    private static bool AreEqualFailing<T>(T? expected, T? actual, IEqualityComparer<T>? comparer)
        => !(comparer ?? EqualityComparer<T>.Default).Equals(expected!, actual!);

    private static bool AreEqualFailing<T>(IEquatable<T>? expected, IEquatable<T>? actual)
        => (actual is not null || expected is not null) && actual?.Equals(expected) != true;

    private static bool AreEqualFailing(string? expected, string? actual, bool ignoreCase, CultureInfo culture)
        => CompareInternal(expected, actual, ignoreCase, culture) != 0;

    private static bool AreEqualFailing(float expected, float actual, float delta)
    {
        if (float.IsNaN(delta) || delta < 0)
        {
            // NaN and negative values don't make sense as a delta value.
            throw new ArgumentOutOfRangeException(nameof(delta));
        }

        if (expected.Equals(actual))
        {
            return false;
        }

        // If both floats are NaN, then they were considered equal in the previous check.
        // If only one of them is NaN, then they are not equal regardless of the value of delta.
        // Then, the subtraction comparison to delta isn't involving NaNs.
        return float.IsNaN(expected) || float.IsNaN(actual) ||
                Math.Abs(expected - actual) > delta;
    }

    private static bool AreEqualFailing(double expected, double actual, double delta)
    {
        if (double.IsNaN(delta) || delta < 0)
        {
            // NaN and negative values don't make sense as a delta value.
            throw new ArgumentOutOfRangeException(nameof(delta));
        }

        if (expected.Equals(actual))
        {
            return false;
        }

        // If both doubles are NaN, then they were considered equal in the previous check.
        // If only one of them is NaN, then they are not equal regardless of the value of delta.
        // Then, the subtraction comparison to delta isn't involving NaNs.
        return double.IsNaN(expected) || double.IsNaN(actual) ||
                Math.Abs(expected - actual) > delta;
    }

    private static bool AreEqualFailing(decimal expected, decimal actual, decimal delta)
        => Math.Abs(expected - actual) > delta;

    private static bool AreEqualFailing(long expected, long actual, long delta)
        => Math.Abs(expected - actual) > delta;

    private static string FormatStringComparisonMessage(string? expected, string? actual, string userMessage)
    {
        // Handle null cases
        if (expected is null && actual is null)
        {
            return string.Format(
                CultureInfo.CurrentCulture,
                FrameworkMessages.AreEqualFailMsg,
                userMessage,
                ReplaceNulls(expected),
                ReplaceNulls(actual));
        }

        if (expected is null || actual is null)
        {
            return string.Format(
                CultureInfo.CurrentCulture,
                FrameworkMessages.AreEqualFailMsg,
                userMessage,
                ReplaceNulls(expected),
                ReplaceNulls(actual));
        }

        // Find the first difference
        int diffIndex = FindFirstStringDifference(expected, actual);

        if (diffIndex == -1)
        {
            // Strings are equal - should not happen in practice but handle gracefully
            return string.Format(
                CultureInfo.CurrentCulture,
                FrameworkMessages.AreEqualFailMsg,
                userMessage,
                ReplaceNulls(expected),
                ReplaceNulls(actual));
        }

        // Format the enhanced string comparison message
        return FormatStringDifferenceMessage(expected, actual, diffIndex, userMessage);
    }

    private static int FindFirstStringDifference(string expected, string actual)
    {
        int minLength = Math.Min(expected.Length, actual.Length);

        for (int i = 0; i < minLength; i++)
        {
            if (expected[i] != actual[i])
            {
                return i;
            }
        }

        // If we reach here, one string is a prefix of the other
        return expected.Length != actual.Length ? minLength : -1;
    }

    private static string FormatStringDifferenceMessage(string expected, string actual, int diffIndex, string userMessage)
    {
        string lengthInfo = expected.Length == actual.Length
            ? string.Format(CultureInfo.CurrentCulture, FrameworkMessages.AreEqualStringDiffLengthBothMsg, expected.Length, diffIndex)
            : string.Format(CultureInfo.CurrentCulture, FrameworkMessages.AreEqualStringDiffLengthDifferentMsg, expected.Length, actual.Length);

        // Create contextual preview around the difference
        const int contextLength = 41; // Show up to 20 characters of context on each side
        Tuple<string, string, int> tuple = StringPreviewHelper.CreateStringPreviews(expected, actual, diffIndex, contextLength);
        string expectedPreview = tuple.Item1;
        string actualPreview = tuple.Item2;
        int caretPosition = tuple.Item3;

        // Get localized prefixes
        string expectedPrefix = FrameworkMessages.AreEqualStringDiffExpectedPrefix;
        string actualPrefix = FrameworkMessages.AreEqualStringDiffActualPrefix;

        // Calculate the maximum prefix length to align the caret properly
        int maxPrefixLength = Math.Max(expectedPrefix.Length, actualPrefix.Length);

        // Pad shorter prefix to match the longer one for proper alignment
        string paddedExpectedPrefix = expectedPrefix.PadRight(maxPrefixLength);
        string paddedActualPrefix = actualPrefix.PadRight(maxPrefixLength);

        // Build the formatted lines with proper alignment
        string expectedLine = paddedExpectedPrefix + $"\"{expectedPreview}\"";
        string actualLine = paddedActualPrefix + $"\"{actualPreview}\"";

        // The caret should align under the difference in the string content
        // For localized prefixes with different lengths, we need to account for the longer prefix
        // to ensure proper alignment. But the caret position is relative to the string content.
        int adjustedCaretPosition = maxPrefixLength + 1 + caretPosition; // +1 for the opening quote

        // Format user message properly - add leading space if not empty, otherwise no extra formatting
        string formattedUserMessage = string.IsNullOrEmpty(userMessage) ? string.Empty : $" {userMessage}";

        return string.Format(
            CultureInfo.CurrentCulture,
            FrameworkMessages.AreEqualStringDiffFailMsg,
            lengthInfo,
            formattedUserMessage,
            expectedLine,
            actualLine,
            new string('-', adjustedCaretPosition) + "^");
    }

    [DoesNotReturn]
    private static void ThrowAssertAreEqualFailed(object? expected, object? actual, string userMessage)
    {
        string finalMessage = actual != null && expected != null && !actual.GetType().Equals(expected.GetType())
            ? string.Format(
                CultureInfo.CurrentCulture,
                FrameworkMessages.AreEqualDifferentTypesFailMsg,
                userMessage,
                ReplaceNulls(expected),
                expected.GetType().FullName,
                ReplaceNulls(actual),
                actual.GetType().FullName)
            : expected is string expectedString && actual is string actualString
                ? FormatStringComparisonMessage(expectedString, actualString, userMessage)
                : string.Format(
                    CultureInfo.CurrentCulture,
                    FrameworkMessages.AreEqualFailMsg,
                    userMessage,
                    ReplaceNulls(expected),
                    ReplaceNulls(actual));
        ThrowAssertFailed("Assert.AreEqual", finalMessage);
    }

    [DoesNotReturn]
    private static void ThrowAssertAreEqualFailed<T>(T expected, T actual, T delta, string userMessage)
        where T : struct, IConvertible
    {
        string finalMessage = string.Format(
            CultureInfo.CurrentCulture,
            FrameworkMessages.AreEqualDeltaFailMsg,
            userMessage,
            expected.ToString(CultureInfo.CurrentCulture.NumberFormat),
            actual.ToString(CultureInfo.CurrentCulture.NumberFormat),
            delta.ToString(CultureInfo.CurrentCulture.NumberFormat));
        ThrowAssertFailed("Assert.AreEqual", finalMessage);
    }

    [DoesNotReturn]
    private static void ThrowAssertAreEqualFailed(string? expected, string? actual, bool ignoreCase, CultureInfo culture, string userMessage)
    {
        string finalMessage;

        // If the user requested to match case, and the difference between expected/actual is casing only, then we use a different message.
        if (!ignoreCase && CompareInternal(expected, actual, ignoreCase: true, culture) == 0)
        {
            finalMessage = string.Format(
                CultureInfo.CurrentCulture,
                FrameworkMessages.AreEqualCaseFailMsg,
                userMessage,
                ReplaceNulls(expected),
                ReplaceNulls(actual));
        }
        else
        {
            // Use enhanced string comparison for string-specific failures
            finalMessage = FormatStringComparisonMessage(expected, actual, userMessage);
        }

        ThrowAssertFailed("Assert.AreEqual", finalMessage);
    }

    /// <summary>
    /// Tests whether the specified values are unequal and throws an exception
    /// if the two values are equal.
    /// The equality is computed using the default <see cref="EqualityComparer{T}"/>.
    /// </summary>
    /// <typeparam name="T">
    /// The type of values to compare.
    /// </typeparam>
    /// <param name="notExpected">
    /// The first value to compare. This is the value the test expects not
    /// to match <paramref name="actual"/>.
    /// </param>
    /// <param name="actual">
    /// The second value to compare. This is the value produced by the code under test.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// Thrown if <paramref name="notExpected"/> is equal to <paramref name="actual"/>.
    /// </exception>
    public static void AreNotEqual<T>(T? notExpected, T? actual)
        => AreNotEqual(notExpected, actual, null, string.Empty, null);

    /// <summary>
    /// Tests whether the specified values are unequal and throws an exception
    /// if the two values are equal.
    /// The equality is computed using the provided <paramref name="comparer"/> parameter.
    /// </summary>
    /// <typeparam name="T">
    /// The type of values to compare.
    /// </typeparam>
    /// <param name="notExpected">
    /// The first value to compare. This is the value the test expects not
    /// to match <paramref name="actual"/>.
    /// </param>
    /// <param name="actual">
    /// The second value to compare. This is the value produced by the code under test.
    /// </param>
    /// <param name="comparer">
    /// The <see cref="IEqualityComparer{T}"/> implementation to use when comparing keys,
    /// or null to use the default <see cref="EqualityComparer{T}"/>.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// Thrown if <paramref name="notExpected"/> is equal to <paramref name="actual"/>.
    /// </exception>
    public static void AreNotEqual<T>(T? notExpected, T? actual, IEqualityComparer<T>? comparer)
        => AreNotEqual(notExpected, actual, comparer, string.Empty, null);

    /// <summary>
    /// Tests whether the specified values are unequal and throws an exception
    /// if the two values are equal.
    /// The equality is computed using the default <see cref="EqualityComparer{T}"/>.
    /// </summary>
    /// <typeparam name="T">
    /// The type of values to compare.
    /// </typeparam>
    /// <param name="notExpected">
    /// The first value to compare. This is the value the test expects not
    /// to match <paramref name="actual"/>.
    /// </param>
    /// <param name="actual">
    /// The second value to compare. This is the value produced by the code under test.
    /// </param>
    /// <param name="message">
    /// The message to include in the exception when <paramref name="actual"/>
    /// is equal to <paramref name="notExpected"/>. The message is shown in
    /// test results.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// Thrown if <paramref name="notExpected"/> is equal to <paramref name="actual"/>.
    /// </exception>
    public static void AreNotEqual<T>(T? notExpected, T? actual, string? message)
        => AreNotEqual(notExpected, actual, null, message, null);

    /// <inheritdoc cref="AreNotEqual{T}(T, T, string?)" />
#pragma warning disable IDE0060 // Remove unused parameter - https://github.com/dotnet/roslyn/issues/76578
    public static void AreNotEqual<T>(T? notExpected, T? actual, [InterpolatedStringHandlerArgument(nameof(notExpected), nameof(actual))] ref AssertAreNotEqualInterpolatedStringHandler<T> message)
#pragma warning restore IDE0060 // Remove unused parameter
        => message.ComputeAssertion();

    /// <summary>
    /// Tests whether the specified values are unequal and throws an exception
    /// if the two values are equal.
    /// The equality is computed using the provided <paramref name="comparer"/> parameter.
    /// </summary>
    /// <typeparam name="T">
    /// The type of values to compare.
    /// </typeparam>
    /// <param name="notExpected">
    /// The first value to compare. This is the value the test expects not
    /// to match <paramref name="actual"/>.
    /// </param>
    /// <param name="actual">
    /// The second value to compare. This is the value produced by the code under test.
    /// </param>
    /// <param name="comparer">
    /// The <see cref="IEqualityComparer{T}"/> implementation to use when comparing keys,
    /// or null to use the default <see cref="EqualityComparer{T}"/>.
    /// </param>
    /// <param name="message">
    /// The message to include in the exception when <paramref name="actual"/>
    /// is equal to <paramref name="notExpected"/>. The message is shown in
    /// test results.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// Thrown if <paramref name="notExpected"/> is equal to <paramref name="actual"/>.
    /// </exception>
    public static void AreNotEqual<T>(T? notExpected, T? actual, IEqualityComparer<T>? comparer, string? message)
        => AreNotEqual(notExpected, actual, comparer, message, null);

    /// <inheritdoc cref="AreNotEqual{T}(T, T, string?)" />
#pragma warning disable IDE0060 // Remove unused parameter - https://github.com/dotnet/roslyn/issues/76578
    public static void AreNotEqual<T>(T? notExpected, T? actual, IEqualityComparer<T>? comparer, [InterpolatedStringHandlerArgument(nameof(notExpected), nameof(actual), nameof(comparer))] ref AssertAreNotEqualInterpolatedStringHandler<T> message)
#pragma warning restore IDE0060 // Remove unused parameter
        => message.ComputeAssertion();

    /// <summary>
    /// Tests whether the specified values are unequal and throws an exception
    /// if the two values are equal.
    /// The equality is computed using the default <see cref="EqualityComparer{T}"/>.
    /// </summary>
    /// <typeparam name="T">
    /// The type of values to compare.
    /// </typeparam>
    /// <param name="notExpected">
    /// The first value to compare. This is the value the test expects not
    /// to match <paramref name="actual"/>.
    /// </param>
    /// <param name="actual">
    /// The second value to compare. This is the value produced by the code under test.
    /// </param>
    /// <param name="message">
    /// The message to include in the exception when <paramref name="actual"/>
    /// is equal to <paramref name="notExpected"/>. The message is shown in
    /// test results.
    /// </param>
    /// <param name="parameters">
    /// An array of parameters to use when formatting <paramref name="message"/>.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// Thrown if <paramref name="notExpected"/> is equal to <paramref name="actual"/>.
    /// </exception>
    public static void AreNotEqual<T>(T? notExpected, T? actual, [StringSyntax(StringSyntaxAttribute.CompositeFormat)] string? message, params object?[]? parameters)
        => AreNotEqual(notExpected, actual, null, message, parameters);

    /// <summary>
    /// Tests whether the specified values are unequal and throws an exception
    /// if the two values are equal.
    /// The equality is computed using the provided <paramref name="comparer"/> parameter.
    /// </summary>
    /// <typeparam name="T">
    /// The type of values to compare.
    /// </typeparam>
    /// <param name="notExpected">
    /// The first value to compare. This is the value the test expects not
    /// to match <paramref name="actual"/>.
    /// </param>
    /// <param name="actual">
    /// The second value to compare. This is the value produced by the code under test.
    /// </param>
    /// <param name="comparer">
    /// The <see cref="IEqualityComparer{T}"/> implementation to use when comparing keys,
    /// or null to use the default <see cref="EqualityComparer{T}"/>.
    /// </param>
    /// <param name="message">
    /// The message to include in the exception when <paramref name="actual"/>
    /// is equal to <paramref name="notExpected"/>. The message is shown in
    /// test results.
    /// </param>
    /// <param name="parameters">
    /// An array of parameters to use when formatting <paramref name="message"/>.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// Thrown if <paramref name="notExpected"/> is equal to <paramref name="actual"/>.
    /// </exception>
    public static void AreNotEqual<T>(T? notExpected, T? actual, IEqualityComparer<T>? comparer,
        [StringSyntax(StringSyntaxAttribute.CompositeFormat)] string? message, params object?[]? parameters)
    {
        if (!AreNotEqualFailing(notExpected, actual, comparer))
        {
            return;
        }

        string userMessage = BuildUserMessage(message, parameters);
        ThrowAssertAreNotEqualFailed(notExpected, actual, userMessage);
    }

    /// <summary>
    /// Tests whether the specified floats are equal and throws an exception
    /// if they are not equal.
    /// </summary>
    /// <param name="expected">
    /// The first float to compare. This is the float the tests expects.
    /// </param>
    /// <param name="actual">
    /// The second float to compare. This is the float produced by the code under test.
    /// </param>
    /// <param name="delta">
    /// The required accuracy. An exception will be thrown only if
    /// <paramref name="actual"/> is different than <paramref name="expected"/>
    /// by more than <paramref name="delta"/>.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// Thrown if <paramref name="expected"/> is not equal to
    /// <paramref name="actual"/>.
    /// </exception>
    public static void AreEqual(float expected, float actual, float delta)
        => AreEqual(expected, actual, delta, string.Empty, null);

    /// <summary>
    /// Tests whether the specified floats are equal and throws an exception
    /// if they are not equal.
    /// </summary>
    /// <param name="expected">
    /// The first float to compare. This is the float the tests expects.
    /// </param>
    /// <param name="actual">
    /// The second float to compare. This is the float produced by the code under test.
    /// </param>
    /// <param name="delta">
    /// The required accuracy. An exception will be thrown only if
    /// <paramref name="actual"/> is different than <paramref name="expected"/>
    /// by more than <paramref name="delta"/>.
    /// </param>
    /// <param name="message">
    /// The message to include in the exception when <paramref name="actual"/>
    /// is different than <paramref name="expected"/> by more than
    /// <paramref name="delta"/>. The message is shown in test results.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// Thrown if <paramref name="expected"/> is not equal to
    /// <paramref name="actual"/>.
    /// </exception>
    public static void AreEqual(float expected, float actual, float delta, string? message)
        => AreEqual(expected, actual, delta, message, null);

    /// <inheritdoc cref="AreEqual(float, float, float, string?)"/>
#pragma warning disable IDE0060 // Remove unused parameter - https://github.com/dotnet/roslyn/issues/76578
    public static void AreEqual(float expected, float actual, float delta, [InterpolatedStringHandlerArgument(nameof(expected), nameof(actual), nameof(delta))] ref AssertNonGenericAreEqualInterpolatedStringHandler message)
#pragma warning restore IDE0060 // Remove unused parameter
        => message.ComputeAssertion();

    /// <summary>
    /// Tests whether the specified floats are equal and throws an exception
    /// if they are not equal.
    /// </summary>
    /// <param name="expected">
    /// The first float to compare. This is the float the tests expects.
    /// </param>
    /// <param name="actual">
    /// The second float to compare. This is the float produced by the code under test.
    /// </param>
    /// <param name="delta">
    /// The required accuracy. An exception will be thrown only if
    /// <paramref name="actual"/> is different than <paramref name="expected"/>
    /// by more than <paramref name="delta"/>.
    /// </param>
    /// <param name="message">
    /// The message to include in the exception when <paramref name="actual"/>
    /// is different than <paramref name="expected"/> by more than
    /// <paramref name="delta"/>. The message is shown in test results.
    /// </param>
    /// <param name="parameters">
    /// An array of parameters to use when formatting <paramref name="message"/>.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// Thrown if <paramref name="expected"/> is not equal to
    /// <paramref name="actual"/>.
    /// </exception>
    public static void AreEqual(float expected, float actual, float delta, [StringSyntax(StringSyntaxAttribute.CompositeFormat)] string? message,
        params object?[]? parameters)
    {
        if (AreEqualFailing(expected, actual, delta))
        {
            string userMessage = BuildUserMessage(message, parameters);
            ThrowAssertAreEqualFailed(expected, actual, delta, userMessage);
        }
    }

    /// <summary>
    /// Tests whether the specified floats are unequal and throws an exception
    /// if they are equal.
    /// </summary>
    /// <param name="notExpected">
    /// The first float to compare. This is the float the test expects not to
    /// match <paramref name="actual"/>.
    /// </param>
    /// <param name="actual">
    /// The second float to compare. This is the float produced by the code under test.
    /// </param>
    /// <param name="delta">
    /// The required accuracy. An exception will be thrown only if
    /// <paramref name="actual"/> is different than <paramref name="notExpected"/>
    /// by at most <paramref name="delta"/>.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// Thrown if <paramref name="notExpected"/> is equal to <paramref name="actual"/>.
    /// </exception>
    public static void AreNotEqual(float notExpected, float actual, float delta)
        => AreNotEqual(notExpected, actual, delta, string.Empty, null);

    /// <summary>
    /// Tests whether the specified floats are unequal and throws an exception
    /// if they are equal.
    /// </summary>
    /// <param name="notExpected">
    /// The first float to compare. This is the float the test expects not to
    /// match <paramref name="actual"/>.
    /// </param>
    /// <param name="actual">
    /// The second float to compare. This is the float produced by the code under test.
    /// </param>
    /// <param name="delta">
    /// The required accuracy. An exception will be thrown only if
    /// <paramref name="actual"/> is different than <paramref name="notExpected"/>
    /// by at most <paramref name="delta"/>.
    /// </param>
    /// <param name="message">
    /// The message to include in the exception when <paramref name="actual"/>
    /// is equal to <paramref name="notExpected"/> or different by less than
    /// <paramref name="delta"/>. The message is shown in test results.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// Thrown if <paramref name="notExpected"/> is equal to <paramref name="actual"/>.
    /// </exception>
    public static void AreNotEqual(float notExpected, float actual, float delta, string? message)
        => AreNotEqual(notExpected, actual, delta, message, null);

    /// <inheritdoc cref="AreNotEqual(float, float, float, string?)" />
#pragma warning disable IDE0060 // Remove unused parameter - https://github.com/dotnet/roslyn/issues/76578
    public static void AreNotEqual(float notExpected, float actual, float delta, [InterpolatedStringHandlerArgument(nameof(notExpected), nameof(actual), nameof(delta))] ref AssertNonGenericAreNotEqualInterpolatedStringHandler message)
#pragma warning restore IDE0060 // Remove unused parameter
        => message.ComputeAssertion();

    /// <summary>
    /// Tests whether the specified floats are unequal and throws an exception
    /// if they are equal.
    /// </summary>
    /// <param name="notExpected">
    /// The first float to compare. This is the float the test expects not to
    /// match <paramref name="actual"/>.
    /// </param>
    /// <param name="actual">
    /// The second float to compare. This is the float produced by the code under test.
    /// </param>
    /// <param name="delta">
    /// The required accuracy. An exception will be thrown only if
    /// <paramref name="actual"/> is different than <paramref name="notExpected"/>
    /// by at most <paramref name="delta"/>.
    /// </param>
    /// <param name="message">
    /// The message to include in the exception when <paramref name="actual"/>
    /// is equal to <paramref name="notExpected"/> or different by less than
    /// <paramref name="delta"/>. The message is shown in test results.
    /// </param>
    /// <param name="parameters">
    /// An array of parameters to use when formatting <paramref name="message"/>.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// Thrown if <paramref name="notExpected"/> is equal to <paramref name="actual"/>.
    /// </exception>
    public static void AreNotEqual(float notExpected, float actual, float delta, [StringSyntax(StringSyntaxAttribute.CompositeFormat)] string? message,
        params object?[]? parameters)
    {
        if (AreNotEqualFailing(notExpected, actual, delta))
        {
            string userMessage = BuildUserMessage(message, parameters);
            ThrowAssertAreNotEqualFailed(notExpected, actual, delta, userMessage);
        }
    }

    private static bool AreNotEqualFailing(float notExpected, float actual, float delta)
    {
        if (float.IsNaN(delta) || delta < 0)
        {
            // NaN and negative values don't make sense as a delta value.
            throw new ArgumentOutOfRangeException(nameof(delta));
        }

        if (float.IsNaN(notExpected) && float.IsNaN(actual))
        {
            // If both notExpected and actual are NaN, then AreNotEqual should fail.
            return true;
        }

        // Note: if both notExpected and actual are NaN, that was handled separately above.
        // Now, if both are numerics, then the logic is good.
        // And, if only one of them is NaN, we know they are not equal, meaning AreNotEqual shouldn't fail.
        // And in this case we will correctly be returning false, because NaN <= anything is always false.
        return Math.Abs(notExpected - actual) <= delta;
    }

    /// <summary>
    /// Tests whether the specified decimals are equal and throws an exception
    /// if they are not equal.
    /// </summary>
    /// <param name="expected">
    /// The first decimal to compare. This is the decimal the tests expects.
    /// </param>
    /// <param name="actual">
    /// The second decimal to compare. This is the decimal produced by the code under test.
    /// </param>
    /// <param name="delta">
    /// The required accuracy. An exception will be thrown only if
    /// <paramref name="actual"/> is different than <paramref name="expected"/>
    /// by more than <paramref name="delta"/>.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// Thrown if <paramref name="expected"/> is not equal to
    /// <paramref name="actual"/>.
    /// </exception>
    public static void AreEqual(decimal expected, decimal actual, decimal delta)
        => AreEqual(expected, actual, delta, string.Empty, null);

    /// <summary>
    /// Tests whether the specified decimals are equal and throws an exception
    /// if they are not equal.
    /// </summary>
    /// <param name="expected">
    /// The first decimal to compare. This is the decimal the tests expects.
    /// </param>
    /// <param name="actual">
    /// The second decimal to compare. This is the decimal produced by the code under test.
    /// </param>
    /// <param name="delta">
    /// The required accuracy. An exception will be thrown only if
    /// <paramref name="actual"/> is different than <paramref name="expected"/>
    /// by more than <paramref name="delta"/>.
    /// </param>
    /// <param name="message">
    /// The message to include in the exception when <paramref name="actual"/>
    /// is different than <paramref name="expected"/> by more than
    /// <paramref name="delta"/>. The message is shown in test results.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// Thrown if <paramref name="expected"/> is not equal to
    /// <paramref name="actual"/>.
    /// </exception>
    public static void AreEqual(decimal expected, decimal actual, decimal delta, string? message)
        => AreEqual(expected, actual, delta, message, null);

    /// <inheritdoc cref="AreEqual(decimal, decimal, decimal, string?)" />
#pragma warning disable IDE0060 // Remove unused parameter - https://github.com/dotnet/roslyn/issues/76578
    public static void AreEqual(decimal expected, decimal actual, decimal delta, [InterpolatedStringHandlerArgument(nameof(expected), nameof(actual), nameof(delta))] ref AssertNonGenericAreEqualInterpolatedStringHandler message)
#pragma warning restore IDE0060 // Remove unused parameter
        => message.ComputeAssertion();

    /// <summary>
    /// Tests whether the specified decimals are equal and throws an exception
    /// if they are not equal.
    /// </summary>
    /// <param name="expected">
    /// The first decimal to compare. This is the decimal the tests expects.
    /// </param>
    /// <param name="actual">
    /// The second decimal to compare. This is the decimal produced by the code under test.
    /// </param>
    /// <param name="delta">
    /// The required accuracy. An exception will be thrown only if
    /// <paramref name="actual"/> is different than <paramref name="expected"/>
    /// by more than <paramref name="delta"/>.
    /// </param>
    /// <param name="message">
    /// The message to include in the exception when <paramref name="actual"/>
    /// is different than <paramref name="expected"/> by more than
    /// <paramref name="delta"/>. The message is shown in test results.
    /// </param>
    /// <param name="parameters">
    /// An array of parameters to use when formatting <paramref name="message"/>.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// Thrown if <paramref name="expected"/> is not equal to
    /// <paramref name="actual"/>.
    /// </exception>
    public static void AreEqual(decimal expected, decimal actual, decimal delta, [StringSyntax(StringSyntaxAttribute.CompositeFormat)] string? message,
        params object?[]? parameters)
    {
        if (AreEqualFailing(expected, actual, delta))
        {
            string userMessage = BuildUserMessage(message, parameters);
            ThrowAssertAreEqualFailed(expected, actual, delta, userMessage);
        }
    }

    /// <summary>
    /// Tests whether the specified decimals are unequal and throws an exception
    /// if they are equal.
    /// </summary>
    /// <param name="notExpected">
    /// The first decimal to compare. This is the decimal the test expects not to
    /// match <paramref name="actual"/>.
    /// </param>
    /// <param name="actual">
    /// The second decimal to compare. This is the decimal produced by the code under test.
    /// </param>
    /// <param name="delta">
    /// The required accuracy. An exception will be thrown only if
    /// <paramref name="actual"/> is different than <paramref name="notExpected"/>
    /// by at most <paramref name="delta"/>.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// Thrown if <paramref name="notExpected"/> is equal to <paramref name="actual"/>.
    /// </exception>
    public static void AreNotEqual(decimal notExpected, decimal actual, decimal delta)
        => AreNotEqual(notExpected, actual, delta, string.Empty, null);

    /// <summary>
    /// Tests whether the specified decimals are unequal and throws an exception
    /// if they are equal.
    /// </summary>
    /// <param name="notExpected">
    /// The first decimal to compare. This is the decimal the test expects not to
    /// match <paramref name="actual"/>.
    /// </param>
    /// <param name="actual">
    /// The second decimal to compare. This is the decimal produced by the code under test.
    /// </param>
    /// <param name="delta">
    /// The required accuracy. An exception will be thrown only if
    /// <paramref name="actual"/> is different than <paramref name="notExpected"/>
    /// by at most <paramref name="delta"/>.
    /// </param>
    /// <param name="message">
    /// The message to include in the exception when <paramref name="actual"/>
    /// is equal to <paramref name="notExpected"/> or different by less than
    /// <paramref name="delta"/>. The message is shown in test results.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// Thrown if <paramref name="notExpected"/> is equal to <paramref name="actual"/>.
    /// </exception>
    public static void AreNotEqual(decimal notExpected, decimal actual, decimal delta, string? message)
        => AreNotEqual(notExpected, actual, delta, message, null);

    /// <inheritdoc cref="AreNotEqual(decimal, decimal, decimal, string?)" />
#pragma warning disable IDE0060 // Remove unused parameter - https://github.com/dotnet/roslyn/issues/76578
    public static void AreNotEqual(decimal notExpected, decimal actual, decimal delta, [InterpolatedStringHandlerArgument(nameof(notExpected), nameof(actual), nameof(delta))] ref AssertNonGenericAreNotEqualInterpolatedStringHandler message)
#pragma warning restore IDE0060 // Remove unused parameter
        => message.ComputeAssertion();

    /// <summary>
    /// Tests whether the specified decimals are unequal and throws an exception
    /// if they are equal.
    /// </summary>
    /// <param name="notExpected">
    /// The first decimal to compare. This is the decimal the test expects not to
    /// match <paramref name="actual"/>.
    /// </param>
    /// <param name="actual">
    /// The second decimal to compare. This is the decimal produced by the code under test.
    /// </param>
    /// <param name="delta">
    /// The required accuracy. An exception will be thrown only if
    /// <paramref name="actual"/> is different than <paramref name="notExpected"/>
    /// by at most <paramref name="delta"/>.
    /// </param>
    /// <param name="message">
    /// The message to include in the exception when <paramref name="actual"/>
    /// is equal to <paramref name="notExpected"/> or different by less than
    /// <paramref name="delta"/>. The message is shown in test results.
    /// </param>
    /// <param name="parameters">
    /// An array of parameters to use when formatting <paramref name="message"/>.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// Thrown if <paramref name="notExpected"/> is equal to <paramref name="actual"/>.
    /// </exception>
    public static void AreNotEqual(decimal notExpected, decimal actual, decimal delta, [StringSyntax(StringSyntaxAttribute.CompositeFormat)] string? message,
        params object?[]? parameters)
    {
        if (AreNotEqualFailing(notExpected, actual, delta))
        {
            string userMessage = BuildUserMessage(message, parameters);
            ThrowAssertAreNotEqualFailed(notExpected, actual, delta, userMessage);
        }
    }

    private static bool AreNotEqualFailing(decimal notExpected, decimal actual, decimal delta)
        => Math.Abs(notExpected - actual) <= delta;

    /// <summary>
    /// Tests whether the specified longs are equal and throws an exception
    /// if they are not equal.
    /// </summary>
    /// <param name="expected">
    /// The first long to compare. This is the long the tests expects.
    /// </param>
    /// <param name="actual">
    /// The second long to compare. This is the long produced by the code under test.
    /// </param>
    /// <param name="delta">
    /// The required accuracy. An exception will be thrown only if
    /// <paramref name="actual"/> is different than <paramref name="expected"/>
    /// by more than <paramref name="delta"/>.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// Thrown if <paramref name="expected"/> is not equal to
    /// <paramref name="actual"/>.
    /// </exception>
    public static void AreEqual(long expected, long actual, long delta)
        => AreEqual(expected, actual, delta, string.Empty, null);

    /// <summary>
    /// Tests whether the specified longs are equal and throws an exception
    /// if they are not equal.
    /// </summary>
    /// <param name="expected">
    /// The first long to compare. This is the long the tests expects.
    /// </param>
    /// <param name="actual">
    /// The second long to compare. This is the long produced by the code under test.
    /// </param>
    /// <param name="delta">
    /// The required accuracy. An exception will be thrown only if
    /// <paramref name="actual"/> is different than <paramref name="expected"/>
    /// by more than <paramref name="delta"/>.
    /// </param>
    /// <param name="message">
    /// The message to include in the exception when <paramref name="actual"/>
    /// is different than <paramref name="expected"/> by more than
    /// <paramref name="delta"/>. The message is shown in test results.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// Thrown if <paramref name="expected"/> is not equal to
    /// <paramref name="actual"/>.
    /// </exception>
    public static void AreEqual(long expected, long actual, long delta, string? message)
        => AreEqual(expected, actual, delta, message, null);

    /// <inheritdoc cref="AreEqual(long, long, long, string?)" />
#pragma warning disable IDE0060 // Remove unused parameter - https://github.com/dotnet/roslyn/issues/76578
    public static void AreEqual(long expected, long actual, long delta, [InterpolatedStringHandlerArgument(nameof(expected), nameof(actual), nameof(delta))] ref AssertNonGenericAreEqualInterpolatedStringHandler message)
#pragma warning restore IDE0060 // Remove unused parameter
        => message.ComputeAssertion();

    /// <summary>
    /// Tests whether the specified longs are equal and throws an exception
    /// if they are not equal.
    /// </summary>
    /// <param name="expected">
    /// The first long to compare. This is the long the tests expects.
    /// </param>
    /// <param name="actual">
    /// The second long to compare. This is the long produced by the code under test.
    /// </param>
    /// <param name="delta">
    /// The required accuracy. An exception will be thrown only if
    /// <paramref name="actual"/> is different than <paramref name="expected"/>
    /// by more than <paramref name="delta"/>.
    /// </param>
    /// <param name="message">
    /// The message to include in the exception when <paramref name="actual"/>
    /// is different than <paramref name="expected"/> by more than
    /// <paramref name="delta"/>. The message is shown in test results.
    /// </param>
    /// <param name="parameters">
    /// An array of parameters to use when formatting <paramref name="message"/>.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// Thrown if <paramref name="expected"/> is not equal to
    /// <paramref name="actual"/>.
    /// </exception>
    public static void AreEqual(long expected, long actual, long delta, [StringSyntax(StringSyntaxAttribute.CompositeFormat)] string? message,
        params object?[]? parameters)
    {
        if (AreEqualFailing(expected, actual, delta))
        {
            string userMessage = BuildUserMessage(message, parameters);
            ThrowAssertAreEqualFailed(expected, actual, delta, userMessage);
        }
    }

    /// <summary>
    /// Tests whether the specified longs are unequal and throws an exception
    /// if they are equal.
    /// </summary>
    /// <param name="notExpected">
    /// The first long to compare. This is the long the test expects not to
    /// match <paramref name="actual"/>.
    /// </param>
    /// <param name="actual">
    /// The second long to compare. This is the long produced by the code under test.
    /// </param>
    /// <param name="delta">
    /// The required accuracy. An exception will be thrown only if
    /// <paramref name="actual"/> is different than <paramref name="notExpected"/>
    /// by at most <paramref name="delta"/>.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// Thrown if <paramref name="notExpected"/> is equal to <paramref name="actual"/>.
    /// </exception>
    public static void AreNotEqual(long notExpected, long actual, long delta)
        => AreNotEqual(notExpected, actual, delta, string.Empty, null);

    /// <summary>
    /// Tests whether the specified longs are unequal and throws an exception
    /// if they are equal.
    /// </summary>
    /// <param name="notExpected">
    /// The first long to compare. This is the long the test expects not to
    /// match <paramref name="actual"/>.
    /// </param>
    /// <param name="actual">
    /// The second long to compare. This is the long produced by the code under test.
    /// </param>
    /// <param name="delta">
    /// The required accuracy. An exception will be thrown only if
    /// <paramref name="actual"/> is different than <paramref name="notExpected"/>
    /// by at most <paramref name="delta"/>.
    /// </param>
    /// <param name="message">
    /// The message to include in the exception when <paramref name="actual"/>
    /// is equal to <paramref name="notExpected"/> or different by less than
    /// <paramref name="delta"/>. The message is shown in test results.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// Thrown if <paramref name="notExpected"/> is equal to <paramref name="actual"/>.
    /// </exception>
    public static void AreNotEqual(long notExpected, long actual, long delta, string? message)
        => AreNotEqual(notExpected, actual, delta, message, null);

    /// <inheritdoc cref="AreNotEqual(long, long, long, string?)" />
#pragma warning disable IDE0060 // Remove unused parameter - https://github.com/dotnet/roslyn/issues/76578
    public static void AreNotEqual(long notExpected, long actual, long delta, [InterpolatedStringHandlerArgument(nameof(notExpected), nameof(actual), nameof(delta))] ref AssertNonGenericAreNotEqualInterpolatedStringHandler message)
#pragma warning restore IDE0060 // Remove unused parameter
        => message.ComputeAssertion();

    /// <summary>
    /// Tests whether the specified longs are unequal and throws an exception
    /// if they are equal.
    /// </summary>
    /// <param name="notExpected">
    /// The first long to compare. This is the long the test expects not to
    /// match <paramref name="actual"/>.
    /// </param>
    /// <param name="actual">
    /// The second long to compare. This is the long produced by the code under test.
    /// </param>
    /// <param name="delta">
    /// The required accuracy. An exception will be thrown only if
    /// <paramref name="actual"/> is different than <paramref name="notExpected"/>
    /// by at most <paramref name="delta"/>.
    /// </param>
    /// <param name="message">
    /// The message to include in the exception when <paramref name="actual"/>
    /// is equal to <paramref name="notExpected"/> or different by less than
    /// <paramref name="delta"/>. The message is shown in test results.
    /// </param>
    /// <param name="parameters">
    /// An array of parameters to use when formatting <paramref name="message"/>.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// Thrown if <paramref name="notExpected"/> is equal to <paramref name="actual"/>.
    /// </exception>
    public static void AreNotEqual(long notExpected, long actual, long delta, [StringSyntax(StringSyntaxAttribute.CompositeFormat)] string? message,
        params object?[]? parameters)
    {
        if (AreNotEqualFailing(notExpected, actual, delta))
        {
            string userMessage = BuildUserMessage(message, parameters);
            ThrowAssertAreNotEqualFailed(notExpected, actual, delta, userMessage);
        }
    }

    private static bool AreNotEqualFailing(long notExpected, long actual, long delta)
        => Math.Abs(notExpected - actual) <= delta;

    /// <summary>
    /// Tests whether the specified doubles are equal and throws an exception
    /// if they are not equal.
    /// </summary>
    /// <param name="expected">
    /// The first double to compare. This is the double the tests expects.
    /// </param>
    /// <param name="actual">
    /// The second double to compare. This is the double produced by the code under test.
    /// </param>
    /// <param name="delta">
    /// The required accuracy. An exception will be thrown only if
    /// <paramref name="actual"/> is different than <paramref name="expected"/>
    /// by more than <paramref name="delta"/>.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// Thrown if <paramref name="expected"/> is not equal to
    /// <paramref name="actual"/>.
    /// </exception>
    public static void AreEqual(double expected, double actual, double delta)
        => AreEqual(expected, actual, delta, string.Empty, null);

    /// <summary>
    /// Tests whether the specified doubles are equal and throws an exception
    /// if they are not equal.
    /// </summary>
    /// <param name="expected">
    /// The first double to compare. This is the double the tests expects.
    /// </param>
    /// <param name="actual">
    /// The second double to compare. This is the double produced by the code under test.
    /// </param>
    /// <param name="delta">
    /// The required accuracy. An exception will be thrown only if
    /// <paramref name="actual"/> is different than <paramref name="expected"/>
    /// by more than <paramref name="delta"/>.
    /// </param>
    /// <param name="message">
    /// The message to include in the exception when <paramref name="actual"/>
    /// is different than <paramref name="expected"/> by more than
    /// <paramref name="delta"/>. The message is shown in test results.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// Thrown if <paramref name="expected"/> is not equal to <paramref name="actual"/>.
    /// </exception>
    public static void AreEqual(double expected, double actual, double delta, string? message)
        => AreEqual(expected, actual, delta, message, null);

    /// <inheritdoc cref="AreEqual(double, double, double, string?)" />
#pragma warning disable IDE0060 // Remove unused parameter - https://github.com/dotnet/roslyn/issues/76578
    public static void AreEqual(double expected, double actual, double delta, [InterpolatedStringHandlerArgument(nameof(expected), nameof(actual), nameof(delta))] ref AssertNonGenericAreEqualInterpolatedStringHandler message)
#pragma warning restore IDE0060 // Remove unused parameter
        => message.ComputeAssertion();

    /// <summary>
    /// Tests whether the specified doubles are equal and throws an exception
    /// if they are not equal.
    /// </summary>
    /// <param name="expected">
    /// The first double to compare. This is the double the tests expects.
    /// </param>
    /// <param name="actual">
    /// The second double to compare. This is the double produced by the code under test.
    /// </param>
    /// <param name="delta">
    /// The required accuracy. An exception will be thrown only if
    /// <paramref name="actual"/> is different than <paramref name="expected"/>
    /// by more than <paramref name="delta"/>.
    /// </param>
    /// <param name="message">
    /// The message to include in the exception when <paramref name="actual"/>
    /// is different than <paramref name="expected"/> by more than
    /// <paramref name="delta"/>. The message is shown in test results.
    /// </param>
    /// <param name="parameters">
    /// An array of parameters to use when formatting <paramref name="message"/>.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// Thrown if <paramref name="expected"/> is not equal to <paramref name="actual"/>.
    /// </exception>
    public static void AreEqual(double expected, double actual, double delta, [StringSyntax(StringSyntaxAttribute.CompositeFormat)] string? message,
        params object?[]? parameters)
    {
        if (AreEqualFailing(expected, actual, delta))
        {
            string userMessage = BuildUserMessage(message, parameters);
            ThrowAssertAreEqualFailed(expected, actual, delta, userMessage);
        }
    }

    /// <summary>
    /// Tests whether the specified doubles are unequal and throws an exception
    /// if they are equal.
    /// </summary>
    /// <param name="notExpected">
    /// The first double to compare. This is the double the test expects not to
    /// match <paramref name="actual"/>.
    /// </param>
    /// <param name="actual">
    /// The second double to compare. This is the double produced by the code under test.
    /// </param>
    /// <param name="delta">
    /// The required accuracy. An exception will be thrown only if
    /// <paramref name="actual"/> is different than <paramref name="notExpected"/>
    /// by at most <paramref name="delta"/>.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// Thrown if <paramref name="notExpected"/> is equal to <paramref name="actual"/>.
    /// </exception>
    public static void AreNotEqual(double notExpected, double actual, double delta)
        => AreNotEqual(notExpected, actual, delta, string.Empty, null);

    /// <summary>
    /// Tests whether the specified doubles are unequal and throws an exception
    /// if they are equal.
    /// </summary>
    /// <param name="notExpected">
    /// The first double to compare. This is the double the test expects not to
    /// match <paramref name="actual"/>.
    /// </param>
    /// <param name="actual">
    /// The second double to compare. This is the double produced by the code under test.
    /// </param>
    /// <param name="delta">
    /// The required accuracy. An exception will be thrown only if
    /// <paramref name="actual"/> is different than <paramref name="notExpected"/>
    /// by at most <paramref name="delta"/>.
    /// </param>
    /// <param name="message">
    /// The message to include in the exception when <paramref name="actual"/>
    /// is equal to <paramref name="notExpected"/> or different by less than
    /// <paramref name="delta"/>. The message is shown in test results.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// Thrown if <paramref name="notExpected"/> is equal to <paramref name="actual"/>.
    /// </exception>
    public static void AreNotEqual(double notExpected, double actual, double delta, string? message)
        => AreNotEqual(notExpected, actual, delta, message, null);

    /// <inheritdoc cref="AreNotEqual(double, double, double, string?)" />
#pragma warning disable IDE0060 // Remove unused parameter - https://github.com/dotnet/roslyn/issues/76578
    public static void AreNotEqual(double notExpected, double actual, double delta, [InterpolatedStringHandlerArgument(nameof(notExpected), nameof(actual), nameof(delta))] ref AssertNonGenericAreNotEqualInterpolatedStringHandler message)
#pragma warning restore IDE0060 // Remove unused parameter
        => message.ComputeAssertion();

    /// <summary>
    /// Tests whether the specified doubles are unequal and throws an exception
    /// if they are equal.
    /// </summary>
    /// <param name="notExpected">
    /// The first double to compare. This is the double the test expects not to
    /// match <paramref name="actual"/>.
    /// </param>
    /// <param name="actual">
    /// The second double to compare. This is the double produced by the code under test.
    /// </param>
    /// <param name="delta">
    /// The required accuracy. An exception will be thrown only if
    /// <paramref name="actual"/> is different than <paramref name="notExpected"/>
    /// by at most <paramref name="delta"/>.
    /// </param>
    /// <param name="message">
    /// The message to include in the exception when <paramref name="actual"/>
    /// is equal to <paramref name="notExpected"/> or different by less than
    /// <paramref name="delta"/>. The message is shown in test results.
    /// </param>
    /// <param name="parameters">
    /// An array of parameters to use when formatting <paramref name="message"/>.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// Thrown if <paramref name="notExpected"/> is equal to <paramref name="actual"/>.
    /// </exception>
    public static void AreNotEqual(double notExpected, double actual, double delta, [StringSyntax(StringSyntaxAttribute.CompositeFormat)] string? message,
        params object?[]? parameters)
    {
        if (AreNotEqualFailing(notExpected, actual, delta))
        {
            string userMessage = BuildUserMessage(message, parameters);
            ThrowAssertAreNotEqualFailed(notExpected, actual, delta, userMessage);
        }
    }

    private static bool AreNotEqualFailing(double notExpected, double actual, double delta)
    {
        if (double.IsNaN(delta) || delta < 0)
        {
            // NaN and negative values don't make sense as a delta value.
            throw new ArgumentOutOfRangeException(nameof(delta));
        }

        if (double.IsNaN(notExpected) && double.IsNaN(actual))
        {
            // If both notExpected and actual are NaN, then AreNotEqual should fail.
            return true;
        }

        // Note: if both notExpected and actual are NaN, that was handled separately above.
        // Now, if both are numerics, then the logic is good.
        // And, if only one of them is NaN, we know they are not equal, meaning AreNotEqual shouldn't fail.
        // And in this case we will correctly be returning false, because NaN <= anything is always false.
        return Math.Abs(notExpected - actual) <= delta;
    }

    [DoesNotReturn]
    private static void ThrowAssertAreNotEqualFailed<T>(T notExpected, T actual, T delta, string userMessage)
    where T : struct, IConvertible
    {
        string finalMessage = string.Format(
            CultureInfo.CurrentCulture,
            FrameworkMessages.AreNotEqualDeltaFailMsg,
            userMessage,
            notExpected.ToString(CultureInfo.CurrentCulture.NumberFormat),
            actual.ToString(CultureInfo.CurrentCulture.NumberFormat),
            delta.ToString(CultureInfo.CurrentCulture.NumberFormat));
        ThrowAssertFailed("Assert.AreNotEqual", finalMessage);
    }

    /// <summary>
    /// Tests whether the specified strings are equal and throws an exception
    /// if they are not equal. The invariant culture is used for the comparison.
    /// </summary>
    /// <param name="expected">
    /// The first string to compare. This is the string the tests expects.
    /// </param>
    /// <param name="actual">
    /// The second string to compare. This is the string produced by the code under test.
    /// </param>
    /// <param name="ignoreCase">
    /// A Boolean indicating a case-sensitive or insensitive comparison. (true
    /// indicates a case-insensitive comparison.)
    /// </param>
    /// <exception cref="AssertFailedException">
    /// Thrown if <paramref name="expected"/> is not equal to <paramref name="actual"/>.
    /// </exception>
    public static void AreEqual(string? expected, string? actual, bool ignoreCase)
        => AreEqual(expected, actual, ignoreCase, string.Empty, null);

    /// <summary>
    /// Tests whether the specified strings are equal and throws an exception
    /// if they are not equal. The invariant culture is used for the comparison.
    /// </summary>
    /// <param name="expected">
    /// The first string to compare. This is the string the tests expects.
    /// </param>
    /// <param name="actual">
    /// The second string to compare. This is the string produced by the code under test.
    /// </param>
    /// <param name="ignoreCase">
    /// A Boolean indicating a case-sensitive or insensitive comparison. (true
    /// indicates a case-insensitive comparison.)
    /// </param>
    /// <param name="message">
    /// The message to include in the exception when <paramref name="actual"/>
    /// is not equal to <paramref name="expected"/>. The message is shown in
    /// test results.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// Thrown if <paramref name="expected"/> is not equal to <paramref name="actual"/>.
    /// </exception>
    public static void AreEqual(string? expected, string? actual, bool ignoreCase, string? message)
        => AreEqual(expected, actual, ignoreCase, message, null);

    /// <inheritdoc cref="AreEqual(string?, string?, bool, string?)" />
#pragma warning disable IDE0060 // Remove unused parameter - https://github.com/dotnet/roslyn/issues/76578
    public static void AreEqual(string? expected, string? actual, bool ignoreCase, [InterpolatedStringHandlerArgument(nameof(expected), nameof(actual), nameof(ignoreCase))] ref AssertNonGenericAreEqualInterpolatedStringHandler message)
#pragma warning restore IDE0060 // Remove unused parameter
        => message.ComputeAssertion();

    /// <summary>
    /// Tests whether the specified strings are equal and throws an exception
    /// if they are not equal. The invariant culture is used for the comparison.
    /// </summary>
    /// <param name="expected">
    /// The first string to compare. This is the string the tests expects.
    /// </param>
    /// <param name="actual">
    /// The second string to compare. This is the string produced by the code under test.
    /// </param>
    /// <param name="ignoreCase">
    /// A Boolean indicating a case-sensitive or insensitive comparison. (true
    /// indicates a case-insensitive comparison.)
    /// </param>
    /// <param name="message">
    /// The message to include in the exception when <paramref name="actual"/>
    /// is not equal to <paramref name="expected"/>. The message is shown in
    /// test results.
    /// </param>
    /// <param name="parameters">
    /// An array of parameters to use when formatting <paramref name="message"/>.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// Thrown if <paramref name="expected"/> is not equal to <paramref name="actual"/>.
    /// </exception>
    public static void AreEqual(string? expected, string? actual, bool ignoreCase, [StringSyntax(StringSyntaxAttribute.CompositeFormat)] string? message,
        params object?[]? parameters)
        => AreEqual(expected, actual, ignoreCase, CultureInfo.InvariantCulture, message, parameters);

    /// <summary>
    /// Tests whether the specified strings are equal and throws an exception
    /// if they are not equal.
    /// </summary>
    /// <param name="expected">
    /// The first string to compare. This is the string the tests expects.
    /// </param>
    /// <param name="actual">
    /// The second string to compare. This is the string produced by the code under test.
    /// </param>
    /// <param name="ignoreCase">
    /// A Boolean indicating a case-sensitive or insensitive comparison. (true
    /// indicates a case-insensitive comparison.)
    /// </param>
    /// <param name="culture">
    /// A CultureInfo object that supplies culture-specific comparison information. If culture is null, the current culture is used.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// Thrown if <paramref name="expected"/> is not equal to <paramref name="actual"/>.
    /// </exception>
    public static void AreEqual(string? expected, string? actual, bool ignoreCase, CultureInfo culture)
        => AreEqual(expected, actual, ignoreCase, culture, string.Empty, null);

    /// <summary>
    /// Tests whether the specified strings are equal and throws an exception
    /// if they are not equal.
    /// </summary>
    /// <param name="expected">
    /// The first string to compare. This is the string the tests expects.
    /// </param>
    /// <param name="actual">
    /// The second string to compare. This is the string produced by the code under test.
    /// </param>
    /// <param name="ignoreCase">
    /// A Boolean indicating a case-sensitive or insensitive comparison. (true
    /// indicates a case-insensitive comparison.)
    /// </param>
    /// <param name="culture">
    /// A CultureInfo object that supplies culture-specific comparison information. If culture is null, the current culture is used.
    /// </param>
    /// <param name="message">
    /// The message to include in the exception when <paramref name="actual"/>
    /// is not equal to <paramref name="expected"/>. The message is shown in
    /// test results.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// Thrown if <paramref name="expected"/> is not equal to <paramref name="actual"/>.
    /// </exception>
    public static void AreEqual(string? expected, string? actual, bool ignoreCase, CultureInfo culture, string? message)
        => AreEqual(expected, actual, ignoreCase, culture, message, null);

    /// <inheritdoc cref="AreEqual(string?, string?, bool, CultureInfo, string?)" />
#pragma warning disable IDE0060 // Remove unused parameter - https://github.com/dotnet/roslyn/issues/76578
    public static void AreEqual(string? expected, string? actual, bool ignoreCase,
#pragma warning restore IDE0060 // Remove unused parameter
        CultureInfo culture, [InterpolatedStringHandlerArgument(nameof(expected), nameof(actual), nameof(ignoreCase), nameof(culture))] ref AssertNonGenericAreEqualInterpolatedStringHandler message)
    {
        CheckParameterNotNull(culture, "Assert.AreEqual", nameof(culture), string.Empty);
        message.ComputeAssertion();
    }

    /// <summary>
    /// Tests whether the specified strings are equal and throws an exception
    /// if they are not equal.
    /// </summary>
    /// <param name="expected">
    /// The first string to compare. This is the string the tests expects.
    /// </param>
    /// <param name="actual">
    /// The second string to compare. This is the string produced by the code under test.
    /// </param>
    /// <param name="ignoreCase">
    /// A Boolean indicating a case-sensitive or insensitive comparison. (true
    /// indicates a case-insensitive comparison.)
    /// </param>
    /// <param name="culture">
    /// A CultureInfo object that supplies culture-specific comparison information. If culture is null, the current culture is used.
    /// </param>
    /// <param name="message">
    /// The message to include in the exception when <paramref name="actual"/>
    /// is not equal to <paramref name="expected"/>. The message is shown in
    /// test results.
    /// </param>
    /// <param name="parameters">
    /// An array of parameters to use when formatting <paramref name="message"/>.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// Thrown if <paramref name="expected"/> is not equal to <paramref name="actual"/>.
    /// </exception>
    public static void AreEqual(string? expected, string? actual, bool ignoreCase, CultureInfo culture, [StringSyntax(StringSyntaxAttribute.CompositeFormat)] string? message, params object?[]? parameters)
    {
        CheckParameterNotNull(culture, "Assert.AreEqual", "culture", string.Empty);
        if (!AreEqualFailing(expected, actual, ignoreCase, culture))
        {
            return;
        }

        string userMessage = BuildUserMessage(message, parameters);
        ThrowAssertAreEqualFailed(expected, actual, ignoreCase, culture, userMessage);
    }

    /// <summary>
    /// Tests whether the specified strings are unequal and throws an exception
    /// if they are equal. The invariant culture is used for the comparison.
    /// </summary>
    /// <param name="notExpected">
    /// The first string to compare. This is the string the test expects not to
    /// match <paramref name="actual"/>.
    /// </param>
    /// <param name="actual">
    /// The second string to compare. This is the string produced by the code under test.
    /// </param>
    /// <param name="ignoreCase">
    /// A Boolean indicating a case-sensitive or insensitive comparison. (true
    /// indicates a case-insensitive comparison.)
    /// </param>
    /// <exception cref="AssertFailedException">
    /// Thrown if <paramref name="notExpected"/> is equal to <paramref name="actual"/>.
    /// </exception>
    public static void AreNotEqual(string? notExpected, string? actual, bool ignoreCase)
        => AreNotEqual(notExpected, actual, ignoreCase, string.Empty, null);

    /// <summary>
    /// Tests whether the specified strings are unequal and throws an exception
    /// if they are equal. The invariant culture is used for the comparison.
    /// </summary>
    /// <param name="notExpected">
    /// The first string to compare. This is the string the test expects not to
    /// match <paramref name="actual"/>.
    /// </param>
    /// <param name="actual">
    /// The second string to compare. This is the string produced by the code under test.
    /// </param>
    /// <param name="ignoreCase">
    /// A Boolean indicating a case-sensitive or insensitive comparison. (true
    /// indicates a case-insensitive comparison.)
    /// </param>
    /// <param name="message">
    /// The message to include in the exception when <paramref name="actual"/>
    /// is equal to <paramref name="notExpected"/>. The message is shown in
    /// test results.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// Thrown if <paramref name="notExpected"/> is equal to <paramref name="actual"/>.
    /// </exception>
    public static void AreNotEqual(string? notExpected, string? actual, bool ignoreCase, string? message)
        => AreNotEqual(notExpected, actual, ignoreCase, message, null);

    /// <inheritdoc cref="AreNotEqual(string?, string?, bool, string?)" />
#pragma warning disable IDE0060 // Remove unused parameter - https://github.com/dotnet/roslyn/issues/76578
    public static void AreNotEqual(string? notExpected, string? actual, bool ignoreCase, [InterpolatedStringHandlerArgument(nameof(notExpected), nameof(actual), nameof(ignoreCase))] ref AssertNonGenericAreNotEqualInterpolatedStringHandler message)
#pragma warning restore IDE0060 // Remove unused parameter
        => message.ComputeAssertion();

    /// <summary>
    /// Tests whether the specified strings are unequal and throws an exception
    /// if they are equal. The invariant culture is used for the comparison.
    /// </summary>
    /// <param name="notExpected">
    /// The first string to compare. This is the string the test expects not to
    /// match <paramref name="actual"/>.
    /// </param>
    /// <param name="actual">
    /// The second string to compare. This is the string produced by the code under test.
    /// </param>
    /// <param name="ignoreCase">
    /// A Boolean indicating a case-sensitive or insensitive comparison. (true
    /// indicates a case-insensitive comparison.)
    /// </param>
    /// <param name="message">
    /// The message to include in the exception when <paramref name="actual"/>
    /// is equal to <paramref name="notExpected"/>. The message is shown in
    /// test results.
    /// </param>
    /// <param name="parameters">
    /// An array of parameters to use when formatting <paramref name="message"/>.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// Thrown if <paramref name="notExpected"/> is equal to <paramref name="actual"/>.
    /// </exception>
    public static void AreNotEqual(string? notExpected, string? actual, bool ignoreCase,
        [StringSyntax(StringSyntaxAttribute.CompositeFormat)] string? message, params object?[]? parameters)
        => AreNotEqual(notExpected, actual, ignoreCase, CultureInfo.InvariantCulture, message, parameters);

    /// <summary>
    /// Tests whether the specified strings are unequal and throws an exception
    /// if they are equal.
    /// </summary>
    /// <param name="notExpected">
    /// The first string to compare. This is the string the test expects not to
    /// match <paramref name="actual"/>.
    /// </param>
    /// <param name="actual">
    /// The second string to compare. This is the string produced by the code under test.
    /// </param>
    /// <param name="ignoreCase">
    /// A Boolean indicating a case-sensitive or insensitive comparison. (true
    /// indicates a case-insensitive comparison.)
    /// </param>
    /// <param name="culture">
    /// A CultureInfo object that supplies culture-specific comparison information. If culture is null, the current culture is used.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// Thrown if <paramref name="notExpected"/> is equal to <paramref name="actual"/>.
    /// </exception>
    public static void AreNotEqual(string? notExpected, string? actual, bool ignoreCase, CultureInfo culture)
        => AreNotEqual(notExpected, actual, ignoreCase, culture, string.Empty, null);

    /// <summary>
    /// Tests whether the specified strings are unequal and throws an exception
    /// if they are equal.
    /// </summary>
    /// <param name="notExpected">
    /// The first string to compare. This is the string the test expects not to
    /// match <paramref name="actual"/>.
    /// </param>
    /// <param name="actual">
    /// The second string to compare. This is the string produced by the code under test.
    /// </param>
    /// <param name="ignoreCase">
    /// A Boolean indicating a case-sensitive or insensitive comparison. (true
    /// indicates a case-insensitive comparison.)
    /// </param>
    /// <param name="culture">
    /// A CultureInfo object that supplies culture-specific comparison information. If culture is null, the current culture is used.
    /// </param>
    /// <param name="message">
    /// The message to include in the exception when <paramref name="actual"/>
    /// is equal to <paramref name="notExpected"/>. The message is shown in
    /// test results.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// Thrown if <paramref name="notExpected"/> is equal to <paramref name="actual"/>.
    /// </exception>
    public static void AreNotEqual(string? notExpected, string? actual, bool ignoreCase, CultureInfo culture, string? message)
        => AreNotEqual(notExpected, actual, ignoreCase, culture, message, null);

    /// <inheritdoc cref="AreNotEqual(string?, string?, bool, CultureInfo, string?)" />
#pragma warning disable IDE0060 // Remove unused parameter - https://github.com/dotnet/roslyn/issues/76578
    public static void AreNotEqual(string? notExpected, string? actual, bool ignoreCase,
#pragma warning restore IDE0060 // Remove unused parameter
        CultureInfo culture, [InterpolatedStringHandlerArgument(nameof(notExpected), nameof(actual), nameof(ignoreCase), nameof(culture))] ref AssertNonGenericAreNotEqualInterpolatedStringHandler message)
    {
        CheckParameterNotNull(culture, "Assert.AreNotEqual", nameof(culture), string.Empty);
        message.ComputeAssertion();
    }

    /// <summary>
    /// Tests whether the specified strings are unequal and throws an exception
    /// if they are equal.
    /// </summary>
    /// <param name="notExpected">
    /// The first string to compare. This is the string the test expects not to
    /// match <paramref name="actual"/>.
    /// </param>
    /// <param name="actual">
    /// The second string to compare. This is the string produced by the code under test.
    /// </param>
    /// <param name="ignoreCase">
    /// A Boolean indicating a case-sensitive or insensitive comparison. (true
    /// indicates a case-insensitive comparison.)
    /// </param>
    /// <param name="culture">
    /// A CultureInfo object that supplies culture-specific comparison information. If culture is null, the current culture is used.
    /// </param>
    /// <param name="message">
    /// The message to include in the exception when <paramref name="actual"/>
    /// is equal to <paramref name="notExpected"/>. The message is shown in
    /// test results.
    /// </param>
    /// <param name="parameters">
    /// An array of parameters to use when formatting <paramref name="message"/>.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// Thrown if <paramref name="notExpected"/> is equal to <paramref name="actual"/>.
    /// </exception>
    public static void AreNotEqual(string? notExpected, string? actual, bool ignoreCase,
        CultureInfo culture, [StringSyntax(StringSyntaxAttribute.CompositeFormat)] string? message, params object?[]? parameters)
    {
        CheckParameterNotNull(culture, "Assert.AreNotEqual", "culture", string.Empty);
        if (!AreNotEqualFailing(notExpected, actual, ignoreCase, culture))
        {
            return;
        }

        string userMessage = BuildUserMessage(message, parameters);
        ThrowAssertAreNotEqualFailed(notExpected, actual, userMessage);
    }

    private static bool AreNotEqualFailing(string? notExpected, string? actual, bool ignoreCase, CultureInfo culture)
        => CompareInternal(notExpected, actual, ignoreCase, culture) == 0;

    private static bool AreNotEqualFailing<T>(T? notExpected, T? actual, IEqualityComparer<T>? comparer)
        => (comparer ?? EqualityComparer<T>.Default).Equals(notExpected!, actual!);

    [DoesNotReturn]
    private static void ThrowAssertAreNotEqualFailed(object? notExpected, object? actual, string userMessage)
    {
        string finalMessage = string.Format(
            CultureInfo.CurrentCulture,
            FrameworkMessages.AreNotEqualFailMsg,
            userMessage,
            ReplaceNulls(notExpected),
            ReplaceNulls(actual));
        ThrowAssertFailed("Assert.AreNotEqual", finalMessage);
    }
}

internal static class StringPreviewHelper
{
    public static Tuple<string, string, int> CreateStringPreviews(string expected, string actual, int diffIndex, int fullPreviewLength)
    {
        int ellipsisLength = 3; // Length of the ellipsis "..."

        if (fullPreviewLength % 2 == 0)
        {
            // Being odd makes it easier to calculate the context length, and center the marker, this is not user customizable.
            throw new ArgumentException($"{nameof(fullPreviewLength)} must be odd, but it was even.");
        }

        // This is arbitrary number that is 2 times the size of the ellipsis,
        // plus 3 chars to make it easier to check the tests are correct when part of string is masked.
        // Preview length is not user customizable, just makes it harder to break the tests, and avoids few ifs we would need to write otherwise.
        if (fullPreviewLength < 9)
        {
            throw new ArgumentException($"{nameof(fullPreviewLength)} cannot be shorter than 9.");
        }

        // In case we want to instead count runes or text elements we can change it just here.
        int expectedLength = expected.Length;
        int actualLength = actual.Length;

        if (diffIndex < 0 || diffIndex > Math.Min(expectedLength, actualLength)) // Not -1 here because the difference can be right after the end of the shorter string.
        {
            throw new ArgumentOutOfRangeException(nameof(diffIndex), "diffIndex must be within the bounds of both strings.");
        }

        int contextLength = (fullPreviewLength - 1) / 2;

        // Diff index must point into the string, the start of the strings will always be shortened the same amount,
        // because otherwise the diff would happen at the beginning of the string.
        // So we just care about how far we are from the end of the string, so we can show the maximum amount of info to the user
        // when diff is really close to the end.
        string shorterString = expectedLength < actualLength ? expected : actual;
        string longerString = expectedLength < actualLength ? actual : expected;
        bool expectedIsShorter = expectedLength < actualLength;

        int shorterStringLength = shorterString.Length;
        int longerStringLength = longerString.Length;

        // End marker will point to the end of the shorter string, but the end of the longer string will be replaced by ...
        // make sure we don't point at the dots. To do this we need to make sure the strings are cut at the beginning, rather than preferring the maximum context shown.
        bool markerPointsAtEllipsis = longerStringLength - shorterStringLength > ellipsisLength && shorterStringLength - diffIndex < ellipsisLength;
        int ellipsisSpaceOrZero = markerPointsAtEllipsis ? ellipsisLength + 2 : 0;

        // Find the end of the string that we will show, either then end of the shorter string, or the end of the preview window.
        // Then calculate the start of the preview from that. This makes sure that if diff is close end of the string we show as much as we can.
        int start = Math.Min(diffIndex + contextLength, shorterStringLength) - fullPreviewLength + ellipsisSpaceOrZero;

        // If the string is shorter than the preview, start cutting from 0, otherwise start cutting from the calculated start.
        int cutStart = Math.Max(0, start);
        // From here we need to handle longer and shorter string separately, because one of the can be shorter,
        // and we want to show the maximum we can that fits in thew preview window.
        int cutEndShort = Math.Min(cutStart + fullPreviewLength, shorterStringLength);
        int cutEndLong = Math.Min(cutStart + fullPreviewLength, longerStringLength);

        string shorterStringPreview = shorterString.Substring(cutStart, cutEndShort - cutStart);
        string longerStringPreview = longerString.Substring(cutStart, cutEndLong - cutStart);

        // We cut something from the start of the string, so we need to add ellipsis there.
        // We know if one string is cut then both must be cut, otherwise the diff would be at the beginning of the string.
        if (cutStart > 0)
        {
            shorterStringPreview = EllipsisStart(shorterStringPreview);
            longerStringPreview = EllipsisStart(longerStringPreview);
        }

        // We cut something from the end of the string, so we need to add ellipsis there.
        // We don't know if both strings are cut, so we need to check them separately.
        if (cutEndShort < shorterStringLength)
        {
            shorterStringPreview = EllipsisEnd(shorterStringPreview);
        }

        // We cut something from the end of the string, so we need to add ellipsis there.
        if (cutEndLong < longerStringLength)
        {
            longerStringPreview = EllipsisEnd(longerStringPreview);
        }

        string escapedShorterStringPreview = MakeControlCharactersVisible(shorterStringPreview);
        string escapedLongerStringPreview = MakeControlCharactersVisible(longerStringPreview);

        return new Tuple<string, string, int>(
            expectedIsShorter ? escapedShorterStringPreview : escapedLongerStringPreview,
            expectedIsShorter ? escapedLongerStringPreview : escapedShorterStringPreview,
            diffIndex - cutStart);
    }

    private static string EllipsisEnd(string text)
        => $"{text.Substring(0, text.Length - 3)}...";

    private static string EllipsisStart(string text)
        => $"...{text.Substring(3)}";

    private static string MakeControlCharactersVisible(string text)
    {
        var stringBuilder = new StringBuilder(text.Length);
        foreach (char ch in text)
        {
            if (char.IsControl(ch))
            {
                stringBuilder.Append((char)(0x2400 + ch));
            }
            else
            {
                stringBuilder.Append(ch);
            }
        }

        return stringBuilder.ToString();
    }
}
