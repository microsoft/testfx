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
            shouldAppend = AreEqualFailing(expected, actual, null);
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

#pragma warning disable RS0026 // Do not add multiple public overloads with optional parameters
#pragma warning disable RS0027 // API with optional parameter(s) should have the most parameters amongst its public overloads

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
    public static void AreEqual<T>(T? expected, T? actual, string message = "")
        => AreEqual(expected, actual, EqualityComparer<T>.Default, message);

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
#pragma warning disable IDE0060 // Remove unused parameter - https://github.com/dotnet/roslyn/issues/76578
    public static void AreEqual<T>(T? expected, T? actual, [InterpolatedStringHandlerArgument(nameof(expected), nameof(actual))] ref AssertAreEqualInterpolatedStringHandler<T> message)
#pragma warning restore IDE0060 // Remove unused parameter
        => message.ComputeAssertion();

    /// <inheritdoc cref="AreEqual{T}(T, T, IEqualityComparer{T}?, string?)" />
#pragma warning disable IDE0060 // Remove unused parameter - https://github.com/dotnet/roslyn/issues/76578
    public static void AreEqual<T>(T? expected, T? actual, IEqualityComparer<T>? comparer, [InterpolatedStringHandlerArgument(nameof(expected), nameof(actual), nameof(comparer))] ref AssertAreEqualInterpolatedStringHandler<T> message)
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
    public static void AreEqual<T>(T? expected, T? actual, IEqualityComparer<T> comparer, string message = "")
    {
        if (!AreEqualFailing(expected, actual, comparer))
        {
            return;
        }

        string userMessage = BuildUserMessage(message);
        ThrowAssertAreEqualFailed(expected, actual, userMessage);
    }

    private static bool AreEqualFailing<T>(T? expected, T? actual, IEqualityComparer<T>? comparer)
        => !(comparer ?? EqualityComparer<T>.Default).Equals(expected!, actual!);

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
        // If the user requested to match case, and the difference between expected/actual is casing only, then we use a different message.
        string finalMessage = !ignoreCase && CompareInternal(expected, actual, ignoreCase: true, culture) == 0
            ? string.Format(
                CultureInfo.CurrentCulture,
                FrameworkMessages.AreEqualCaseFailMsg,
                userMessage,
                ReplaceNulls(expected),
                ReplaceNulls(actual))
            : string.Format(
                CultureInfo.CurrentCulture,
                FrameworkMessages.AreEqualFailMsg,
                userMessage,
                ReplaceNulls(expected),
                ReplaceNulls(actual));

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
    /// <param name="message">
    /// The message to include in the exception when <paramref name="actual"/>
    /// is equal to <paramref name="notExpected"/>. The message is shown in
    /// test results.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// Thrown if <paramref name="notExpected"/> is equal to <paramref name="actual"/>.
    /// </exception>
    public static void AreNotEqual<T>(T? notExpected, T? actual, string message = "")
        => AreNotEqual(notExpected, actual, EqualityComparer<T>.Default, message);

    /// <inheritdoc cref="AreNotEqual{T}(T, T, string?)" />
#pragma warning disable IDE0060 // Remove unused parameter - https://github.com/dotnet/roslyn/issues/76578
    public static void AreNotEqual<T>(T? notExpected, T? actual, [InterpolatedStringHandlerArgument(nameof(notExpected), nameof(actual))] ref AssertAreNotEqualInterpolatedStringHandler<T> message)
#pragma warning restore IDE0060 // Remove unused parameter
        => message.ComputeAssertion();

    /// <inheritdoc cref="AreNotEqual{T}(T, T, string?)" />
#pragma warning disable IDE0060 // Remove unused parameter - https://github.com/dotnet/roslyn/issues/76578
    public static void AreNotEqual<T>(T? notExpected, T? actual, IEqualityComparer<T> comparer, [InterpolatedStringHandlerArgument(nameof(notExpected), nameof(actual), nameof(comparer))] ref AssertAreNotEqualInterpolatedStringHandler<T> message)
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
    public static void AreNotEqual<T>(T? notExpected, T? actual, IEqualityComparer<T> comparer, string message = "")
    {
        if (!AreNotEqualFailing(notExpected, actual, comparer))
        {
            return;
        }

        string userMessage = BuildUserMessage(message);
        ThrowAssertAreNotEqualFailed(notExpected, actual, userMessage);
    }

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
    /// <exception cref="AssertFailedException">
    /// Thrown if <paramref name="expected"/> is not equal to
    /// <paramref name="actual"/>.
    /// </exception>
    public static void AreEqual(float expected, float actual, float delta, string message = "")
    {
        if (AreEqualFailing(expected, actual, delta))
        {
            string userMessage = BuildUserMessage(message);
            ThrowAssertAreEqualFailed(expected, actual, delta, userMessage);
        }
    }

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
    /// <exception cref="AssertFailedException">
    /// Thrown if <paramref name="notExpected"/> is equal to <paramref name="actual"/>.
    /// </exception>
    public static void AreNotEqual(float notExpected, float actual, float delta, string message = "")
    {
        if (AreNotEqualFailing(notExpected, actual, delta))
        {
            string userMessage = BuildUserMessage(message);
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
    /// <exception cref="AssertFailedException">
    /// Thrown if <paramref name="expected"/> is not equal to
    /// <paramref name="actual"/>.
    /// </exception>
    public static void AreEqual(decimal expected, decimal actual, decimal delta, string message = "")
    {
        if (AreEqualFailing(expected, actual, delta))
        {
            string userMessage = BuildUserMessage(message);
            ThrowAssertAreEqualFailed(expected, actual, delta, userMessage);
        }
    }

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
    /// <exception cref="AssertFailedException">
    /// Thrown if <paramref name="notExpected"/> is equal to <paramref name="actual"/>.
    /// </exception>
    public static void AreNotEqual(decimal notExpected, decimal actual, decimal delta, string message = "")
    {
        if (AreNotEqualFailing(notExpected, actual, delta))
        {
            string userMessage = BuildUserMessage(message);
            ThrowAssertAreNotEqualFailed(notExpected, actual, delta, userMessage);
        }
    }

    private static bool AreNotEqualFailing(decimal notExpected, decimal actual, decimal delta)
        => Math.Abs(notExpected - actual) <= delta;

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
    /// <exception cref="AssertFailedException">
    /// Thrown if <paramref name="expected"/> is not equal to
    /// <paramref name="actual"/>.
    /// </exception>
    public static void AreEqual(long expected, long actual, long delta, string message = "")
    {
        if (AreEqualFailing(expected, actual, delta))
        {
            string userMessage = BuildUserMessage(message);
            ThrowAssertAreEqualFailed(expected, actual, delta, userMessage);
        }
    }

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
    /// <exception cref="AssertFailedException">
    /// Thrown if <paramref name="notExpected"/> is equal to <paramref name="actual"/>.
    /// </exception>
    public static void AreNotEqual(long notExpected, long actual, long delta, string message = "")
    {
        if (AreNotEqualFailing(notExpected, actual, delta))
        {
            string userMessage = BuildUserMessage(message);
            ThrowAssertAreNotEqualFailed(notExpected, actual, delta, userMessage);
        }
    }

    private static bool AreNotEqualFailing(long notExpected, long actual, long delta)
        => Math.Abs(notExpected - actual) <= delta;

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
    /// <exception cref="AssertFailedException">
    /// Thrown if <paramref name="expected"/> is not equal to <paramref name="actual"/>.
    /// </exception>
    public static void AreEqual(double expected, double actual, double delta, string message = "")
    {
        if (AreEqualFailing(expected, actual, delta))
        {
            string userMessage = BuildUserMessage(message);
            ThrowAssertAreEqualFailed(expected, actual, delta, userMessage);
        }
    }

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
    /// <exception cref="AssertFailedException">
    /// Thrown if <paramref name="notExpected"/> is equal to <paramref name="actual"/>.
    /// </exception>
    public static void AreNotEqual(double notExpected, double actual, double delta, string message = "")
    {
        if (AreNotEqualFailing(notExpected, actual, delta))
        {
            string userMessage = BuildUserMessage(message);
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
    /// <param name="message">
    /// The message to include in the exception when <paramref name="actual"/>
    /// is not equal to <paramref name="expected"/>. The message is shown in
    /// test results.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// Thrown if <paramref name="expected"/> is not equal to <paramref name="actual"/>.
    /// </exception>
    public static void AreEqual(string? expected, string? actual, bool ignoreCase, string message = "")
        => AreEqual(expected, actual, ignoreCase, CultureInfo.InvariantCulture, message);

    /// <inheritdoc cref="AreEqual(string?, string?, bool, string?)" />
#pragma warning disable IDE0060 // Remove unused parameter - https://github.com/dotnet/roslyn/issues/76578
    public static void AreEqual(string? expected, string? actual, bool ignoreCase, [InterpolatedStringHandlerArgument(nameof(expected), nameof(actual), nameof(ignoreCase))] ref AssertNonGenericAreEqualInterpolatedStringHandler message)
#pragma warning restore IDE0060 // Remove unused parameter
        => message.ComputeAssertion();

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
    /// <exception cref="AssertFailedException">
    /// Thrown if <paramref name="expected"/> is not equal to <paramref name="actual"/>.
    /// </exception>
    public static void AreEqual(string? expected, string? actual, bool ignoreCase, CultureInfo culture, string message = "")
    {
        CheckParameterNotNull(culture, "Assert.AreEqual", "culture", string.Empty);
        if (!AreEqualFailing(expected, actual, ignoreCase, culture))
        {
            return;
        }

        string userMessage = BuildUserMessage(message);
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
    /// <param name="message">
    /// The message to include in the exception when <paramref name="actual"/>
    /// is equal to <paramref name="notExpected"/>. The message is shown in
    /// test results.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// Thrown if <paramref name="notExpected"/> is equal to <paramref name="actual"/>.
    /// </exception>
    public static void AreNotEqual(string? notExpected, string? actual, bool ignoreCase, string message = "")
        => AreNotEqual(notExpected, actual, ignoreCase, CultureInfo.InvariantCulture, message);

    /// <inheritdoc cref="AreNotEqual(string?, string?, bool, string?)" />
#pragma warning disable IDE0060 // Remove unused parameter - https://github.com/dotnet/roslyn/issues/76578
    public static void AreNotEqual(string? notExpected, string? actual, bool ignoreCase, [InterpolatedStringHandlerArgument(nameof(notExpected), nameof(actual), nameof(ignoreCase))] ref AssertNonGenericAreNotEqualInterpolatedStringHandler message)
#pragma warning restore IDE0060 // Remove unused parameter
        => message.ComputeAssertion();

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
    /// <exception cref="AssertFailedException">
    /// Thrown if <paramref name="notExpected"/> is equal to <paramref name="actual"/>.
    /// </exception>
    public static void AreNotEqual(string? notExpected, string? actual, bool ignoreCase, CultureInfo culture, string message = "")
    {
        CheckParameterNotNull(culture, "Assert.AreNotEqual", "culture", string.Empty);
        if (!AreNotEqualFailing(notExpected, actual, ignoreCase, culture))
        {
            return;
        }

        string userMessage = BuildUserMessage(message);
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
