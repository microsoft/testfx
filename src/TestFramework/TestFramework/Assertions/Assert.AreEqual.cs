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
    public readonly struct AssertAreEqualInterpolatedStringHandler<TArgument>
    {
        private readonly StringBuilder? _builder;

        public AssertAreEqualInterpolatedStringHandler(int literalLength, int formattedCount, TArgument? expected, TArgument? actual, out bool shouldAppend)
        {
            shouldAppend = AreEqualFailing(expected, actual);
            if (shouldAppend)
            {
                _builder = new StringBuilder(literalLength + formattedCount);
            }
        }

        public AssertAreEqualInterpolatedStringHandler(int literalLength, int formattedCount, TArgument? expected, TArgument? actual, IEqualityComparer<TArgument>? comparer, out bool shouldAppend)
        {
            shouldAppend = AreEqualFailing(expected, actual, comparer);
            if (shouldAppend)
            {
                _builder = new StringBuilder(literalLength + formattedCount);
            }
        }

        public AssertAreEqualInterpolatedStringHandler(int literalLength, int formattedCount, IEquatable<TArgument>? expected, IEquatable<TArgument>? actual, out bool shouldAppend)
        {
            shouldAppend = AreEqualFailing(expected, actual);
            if (shouldAppend)
            {
                _builder = new StringBuilder(literalLength + formattedCount);
            }
        }

        internal bool ShouldAppend => _builder is not null;

        internal readonly string ToStringAndClear() => _builder!.ToString();

        public readonly void AppendLiteral(string value) => _builder!.Append(value);

        public readonly void AppendFormatted<T>(T value) => _builder!.Append(value);

#if NET
        public readonly void AppendFormatted(ReadOnlySpan<char> value) => _builder!.Append(value.ToString());
#endif
    }

    [InterpolatedStringHandler]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public readonly struct AssertAreNotEqualInterpolatedStringHandler<TArgument>
    {
        private readonly StringBuilder? _builder;

        public AssertAreNotEqualInterpolatedStringHandler(int literalLength, int formattedCount, TArgument? notExpected, TArgument? actual, out bool shouldAppend)
        {
            shouldAppend = AreNotEqualFailing(notExpected, actual);
            if (shouldAppend)
            {
                _builder = new StringBuilder(literalLength + formattedCount);
            }
        }

        public AssertAreNotEqualInterpolatedStringHandler(int literalLength, int formattedCount, TArgument? notExpected, TArgument? actual, IEqualityComparer<TArgument>? comparer, out bool shouldAppend)
        {
            shouldAppend = AreNotEqualFailing(notExpected, actual, comparer);
            if (shouldAppend)
            {
                _builder = new StringBuilder(literalLength + formattedCount);
            }
        }

        internal bool ShouldAppend => _builder is not null;

        internal readonly string ToStringAndClear() => _builder!.ToString();

        public readonly void AppendLiteral(string value) => _builder!.Append(value);

        public readonly void AppendFormatted<T>(T value) => _builder!.Append(value);

#if NET
        public readonly void AppendFormatted(ReadOnlySpan<char> value) => _builder!.Append(value.ToString());
#endif
    }

    [InterpolatedStringHandler]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public readonly struct AssertNonGenericAreEqualInterpolatedStringHandler
    {
        private readonly StringBuilder? _builder;

        public AssertNonGenericAreEqualInterpolatedStringHandler(int literalLength, int formattedCount, float expected, float actual, float delta, out bool shouldAppend)
        {
            shouldAppend = AreEqualFailing(expected, actual, delta);
            if (shouldAppend)
            {
                _builder = new StringBuilder(literalLength + formattedCount);
            }
        }

        public AssertNonGenericAreEqualInterpolatedStringHandler(int literalLength, int formattedCount, decimal expected, decimal actual, decimal delta, out bool shouldAppend)
        {
            shouldAppend = AreEqualFailing(expected, actual, delta);
            if (shouldAppend)
            {
                _builder = new StringBuilder(literalLength + formattedCount);
            }
        }

        public AssertNonGenericAreEqualInterpolatedStringHandler(int literalLength, int formattedCount, long expected, long actual, long delta, out bool shouldAppend)
        {
            shouldAppend = AreEqualFailing(expected, actual, delta);
            if (shouldAppend)
            {
                _builder = new StringBuilder(literalLength + formattedCount);
            }
        }

        public AssertNonGenericAreEqualInterpolatedStringHandler(int literalLength, int formattedCount, double expected, double actual, double delta, out bool shouldAppend)
        {
            shouldAppend = AreEqualFailing(expected, actual, delta);
            if (shouldAppend)
            {
                _builder = new StringBuilder(literalLength + formattedCount);
            }
        }

        public AssertNonGenericAreEqualInterpolatedStringHandler(int literalLength, int formattedCount, string? expected, string? actual, bool ignoreCase, out bool shouldAppend)
        {
            shouldAppend = AreEqualFailing(expected, actual, ignoreCase);
            if (shouldAppend)
            {
                _builder = new StringBuilder(literalLength + formattedCount);
            }
        }

        public AssertNonGenericAreEqualInterpolatedStringHandler(int literalLength, int formattedCount, string? expected, string? actual, bool ignoreCase, [NotNull] CultureInfo? culture, out bool shouldAppend)
        {
            Guard.NotNull(culture);
            shouldAppend = AreEqualFailing(expected, actual, ignoreCase, culture);
            if (shouldAppend)
            {
                _builder = new StringBuilder(literalLength + formattedCount);
            }
        }

        internal bool ShouldAppend => _builder is not null;

        internal readonly string ToStringAndClear() => _builder!.ToString();

        public readonly void AppendLiteral(string value) => _builder!.Append(value);

        public readonly void AppendFormatted<T>(T value) => _builder!.Append(value);

#if NET
        public readonly void AppendFormatted(ReadOnlySpan<char> value) => _builder!.Append(value.ToString());
#endif
    }

    [InterpolatedStringHandler]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public readonly struct AssertNonGenericAreNotEqualInterpolatedStringHandler
    {
        private readonly StringBuilder? _builder;

        public AssertNonGenericAreNotEqualInterpolatedStringHandler(int literalLength, int formattedCount, float expected, float actual, float delta, out bool shouldAppend)
        {
            shouldAppend = AreNotEqualFailing(expected, actual, delta);
            if (shouldAppend)
            {
                _builder = new StringBuilder(literalLength + formattedCount);
            }
        }

        public AssertNonGenericAreNotEqualInterpolatedStringHandler(int literalLength, int formattedCount, decimal expected, decimal actual, decimal delta, out bool shouldAppend)
        {
            shouldAppend = AreNotEqualFailing(expected, actual, delta);
            if (shouldAppend)
            {
                _builder = new StringBuilder(literalLength + formattedCount);
            }
        }

        public AssertNonGenericAreNotEqualInterpolatedStringHandler(int literalLength, int formattedCount, long expected, long actual, long delta, out bool shouldAppend)
        {
            shouldAppend = AreNotEqualFailing(expected, actual, delta);
            if (shouldAppend)
            {
                _builder = new StringBuilder(literalLength + formattedCount);
            }
        }

        public AssertNonGenericAreNotEqualInterpolatedStringHandler(int literalLength, int formattedCount, double expected, double actual, double delta, out bool shouldAppend)
        {
            shouldAppend = AreNotEqualFailing(expected, actual, delta);
            if (shouldAppend)
            {
                _builder = new StringBuilder(literalLength + formattedCount);
            }
        }

        public AssertNonGenericAreNotEqualInterpolatedStringHandler(int literalLength, int formattedCount, string? expected, string? actual, bool ignoreCase, out bool shouldAppend)
        {
            shouldAppend = AreNotEqualFailing(expected, actual, ignoreCase);
            if (shouldAppend)
            {
                _builder = new StringBuilder(literalLength + formattedCount);
            }
        }

        public AssertNonGenericAreNotEqualInterpolatedStringHandler(int literalLength, int formattedCount, string? expected, string? actual, bool ignoreCase, [NotNull] CultureInfo? culture, out bool shouldAppend)
        {
            Guard.NotNull(culture);
            shouldAppend = AreNotEqualFailing(expected, actual, ignoreCase, culture);
            if (shouldAppend)
            {
                _builder = new StringBuilder(literalLength + formattedCount);
            }
        }

        internal bool ShouldAppend => _builder is not null;

        internal readonly string ToStringAndClear() => _builder!.ToString();

        public readonly void AppendLiteral(string value) => _builder!.Append(value);

        public readonly void AppendFormatted<T>(T value) => _builder!.Append(value);

#if NET
        public readonly void AppendFormatted(ReadOnlySpan<char> value) => _builder!.Append(value.ToString());
#endif
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
    public static void AreEqual<T>(T? expected, T? actual, [InterpolatedStringHandlerArgument(nameof(expected), nameof(actual))] ref AssertAreEqualInterpolatedStringHandler<T> message)
    {
        if (message.ShouldAppend)
        {
            ThrowAssertFailed("Assert.AreEqual", GetAreEqualFinalMessage(expected, actual, message.ToStringAndClear()));
        }
    }

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
#pragma warning disable IDE0060 // Remove unused parameter - false positive. The comparer parameter is used via the interpolated string handler.
    public static void AreEqual<T>(T? expected, T? actual, IEqualityComparer<T>? comparer, [InterpolatedStringHandlerArgument(nameof(expected), nameof(actual), nameof(comparer))] ref AssertAreEqualInterpolatedStringHandler<T> message)
#pragma warning restore IDE0060 // Remove unused parameter
    {
        if (message.ShouldAppend)
        {
            ThrowAssertFailed("Assert.AreEqual", GetAreEqualFinalMessage(expected, actual, message.ToStringAndClear()));
        }
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
        string finalMessage = GetAreEqualFinalMessage(expected, actual, userMessage);

        ThrowAssertFailed("Assert.AreEqual", finalMessage);
    }

    private static string GetAreEqualFinalMessage(object? expected, object? actual, string userMessage)
        => actual != null && expected != null && !actual.GetType().Equals(expected.GetType())
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
    public static void AreEqual<T>(IEquatable<T>? expected, IEquatable<T>? actual, [InterpolatedStringHandlerArgument(nameof(expected), nameof(actual))] ref AssertAreEqualInterpolatedStringHandler<T> message)
    {
        if (message.ShouldAppend)
        {
            ThrowAssertFailed("Assert.AreEqual", GetAreEqualFinalMessage(expected, actual, message.ToStringAndClear()));
        }
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
        string finalMessage = GetAreEqualFinalMessage(expected, actual, userMessage);

        ThrowAssertFailed("Assert.AreEqual", finalMessage);
    }

    private static bool AreEqualFailing<T>(T? expected, T? actual)
        => AreEqualFailing(expected, actual, null);

    private static bool AreEqualFailing<T>(T? expected, T? actual, IEqualityComparer<T>? comparer)
        => !(comparer ?? EqualityComparer<T>.Default).Equals(expected!, actual!);

    private static bool AreEqualFailing<T>(IEquatable<T>? expected, IEquatable<T>? actual)
        => (actual is not null || expected is not null) && actual?.Equals(expected) != true;

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
    public static void AreNotEqual<T>(T? notExpected, T? actual, [InterpolatedStringHandlerArgument(nameof(notExpected), nameof(actual))] ref AssertAreNotEqualInterpolatedStringHandler<T> message)
    {
        if (message.ShouldAppend)
        {
            ThrowAssertFailed("Assert.AreNotEqual", GetAreNotEqualFinalMessage(notExpected, actual, message.ToStringAndClear()));
        }
    }

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
#pragma warning disable IDE0060 // Remove unused parameter - false positive. The comparer parameter is used via the interpolated string handler.
    public static void AreNotEqual<T>(T? notExpected, T? actual, IEqualityComparer<T>? comparer, [InterpolatedStringHandlerArgument(nameof(notExpected), nameof(actual), nameof(comparer))] ref AssertAreNotEqualInterpolatedStringHandler<T> message)
#pragma warning restore IDE0060 // Remove unused parameter
    {
        if (message.ShouldAppend)
        {
            ThrowAssertFailed("Assert.AreNotEqual", GetAreNotEqualFinalMessage(notExpected, actual, message.ToStringAndClear()));
        }
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
        IEqualityComparer<T> localComparer = comparer ?? EqualityComparer<T>.Default;
        if (!localComparer.Equals(notExpected!, actual!))
        {
            return;
        }

        string userMessage = BuildUserMessage(message, parameters);
        string finalMessage = GetAreNotEqualFinalMessage(notExpected, actual, userMessage);
        ThrowAssertFailed("Assert.AreNotEqual", finalMessage);
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
    public static void AreEqual(float expected, float actual, float delta, [InterpolatedStringHandlerArgument(nameof(expected), nameof(actual), nameof(delta))] ref AssertNonGenericAreEqualInterpolatedStringHandler message)
    {
        if (message.ShouldAppend)
        {
            // TODO:
        }
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
        if (float.IsNaN(expected) || float.IsNaN(actual) || float.IsNaN(delta) ||
            Math.Abs(expected - actual) > delta)
        {
            string userMessage = BuildUserMessage(message, parameters);
            string finalMessage = string.Format(
                CultureInfo.CurrentCulture,
                FrameworkMessages.AreEqualDeltaFailMsg,
                userMessage,
                expected.ToString(CultureInfo.CurrentCulture.NumberFormat),
                actual.ToString(CultureInfo.CurrentCulture.NumberFormat),
                delta.ToString(CultureInfo.CurrentCulture.NumberFormat));
            ThrowAssertFailed("Assert.AreEqual", finalMessage);
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

    public static void AreNotEqual(float notExpected, float actual, float delta, [InterpolatedStringHandlerArgument(nameof(notExpected), nameof(actual), nameof(delta))] ref AssertNonGenericAreNotEqualInterpolatedStringHandler message)
    {
        if (message.ShouldAppend)
        {
            // TODO:
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
        if (Math.Abs(notExpected - actual) <= delta)
        {
            string userMessage = BuildUserMessage(message, parameters);
            string finalMessage = string.Format(
                CultureInfo.CurrentCulture,
                FrameworkMessages.AreNotEqualDeltaFailMsg,
                userMessage,
                notExpected.ToString(CultureInfo.CurrentCulture.NumberFormat),
                actual.ToString(CultureInfo.CurrentCulture.NumberFormat),
                delta.ToString(CultureInfo.CurrentCulture.NumberFormat));
            ThrowAssertFailed("Assert.AreNotEqual", finalMessage);
        }
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
    public static void AreEqual(decimal expected, decimal actual, decimal delta, [InterpolatedStringHandlerArgument(nameof(expected), nameof(actual), nameof(delta))] ref AssertNonGenericAreEqualInterpolatedStringHandler message)
    {
        if (message.ShouldAppend)
        {
            // TODO:
        }
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
        if (Math.Abs(expected - actual) > delta)
        {
            string userMessage = BuildUserMessage(message, parameters);
            string finalMessage = string.Format(
                CultureInfo.CurrentCulture,
                FrameworkMessages.AreEqualDeltaFailMsg,
                userMessage,
                expected.ToString(CultureInfo.CurrentCulture.NumberFormat),
                actual.ToString(CultureInfo.CurrentCulture.NumberFormat),
                delta.ToString(CultureInfo.CurrentCulture.NumberFormat));
            ThrowAssertFailed("Assert.AreEqual", finalMessage);
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
    public static void AreNotEqual(decimal notExpected, decimal actual, decimal delta, [InterpolatedStringHandlerArgument(nameof(notExpected), nameof(actual), nameof(delta))] ref AssertNonGenericAreNotEqualInterpolatedStringHandler message)
    {
        if (message.ShouldAppend)
        {
            // TODO:
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
        if (Math.Abs(notExpected - actual) <= delta)
        {
            string userMessage = BuildUserMessage(message, parameters);
            string finalMessage = string.Format(
                CultureInfo.CurrentCulture,
                FrameworkMessages.AreNotEqualDeltaFailMsg,
                userMessage,
                notExpected.ToString(CultureInfo.CurrentCulture.NumberFormat),
                actual.ToString(CultureInfo.CurrentCulture.NumberFormat),
                delta.ToString(CultureInfo.CurrentCulture.NumberFormat));
            ThrowAssertFailed("Assert.AreNotEqual", finalMessage);
        }
    }

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
    public static void AreEqual(long expected, long actual, long delta, [InterpolatedStringHandlerArgument(nameof(expected), nameof(actual), nameof(delta))] ref AssertNonGenericAreEqualInterpolatedStringHandler message)
    {
        if (message.ShouldAppend)
        {
            // TODO:
        }
    }

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
        if (Math.Abs(expected - actual) > delta)
        {
            string userMessage = BuildUserMessage(message, parameters);
            string finalMessage = string.Format(
                CultureInfo.CurrentCulture,
                FrameworkMessages.AreEqualDeltaFailMsg,
                userMessage,
                expected.ToString(CultureInfo.CurrentCulture.NumberFormat),
                actual.ToString(CultureInfo.CurrentCulture.NumberFormat),
                delta.ToString(CultureInfo.CurrentCulture.NumberFormat));
            ThrowAssertFailed("Assert.AreEqual", finalMessage);
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
    public static void AreNotEqual(long notExpected, long actual, long delta, [InterpolatedStringHandlerArgument(nameof(notExpected), nameof(actual), nameof(delta))] ref AssertNonGenericAreNotEqualInterpolatedStringHandler message)
    {
        if (message.ShouldAppend)
        {
            // TODO:
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
        if (Math.Abs(notExpected - actual) <= delta)
        {
            string userMessage = BuildUserMessage(message, parameters);
            string finalMessage = string.Format(
                CultureInfo.CurrentCulture,
                FrameworkMessages.AreNotEqualDeltaFailMsg,
                userMessage,
                notExpected.ToString(CultureInfo.CurrentCulture.NumberFormat),
                actual.ToString(CultureInfo.CurrentCulture.NumberFormat),
                delta.ToString(CultureInfo.CurrentCulture.NumberFormat));
            ThrowAssertFailed("Assert.AreNotEqual", finalMessage);
        }
    }

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
    public static void AreEqual(double expected, double actual, double delta, [InterpolatedStringHandlerArgument(nameof(expected), nameof(actual), nameof(delta))] ref AssertNonGenericAreEqualInterpolatedStringHandler message)
    {
        if (message.ShouldAppend)
        {
            // TODO:
        }
    }

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
        if (double.IsNaN(expected) || double.IsNaN(actual) || double.IsNaN(delta) ||
            Math.Abs(expected - actual) > delta)
        {
            string userMessage = BuildUserMessage(message, parameters);
            string finalMessage = string.Format(
                CultureInfo.CurrentCulture,
                FrameworkMessages.AreEqualDeltaFailMsg,
                userMessage,
                expected.ToString(CultureInfo.CurrentCulture.NumberFormat),
                actual.ToString(CultureInfo.CurrentCulture.NumberFormat),
                delta.ToString(CultureInfo.CurrentCulture.NumberFormat));
            ThrowAssertFailed("Assert.AreEqual", finalMessage);
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
    public static void AreNotEqual(double notExpected, double actual, double delta, [InterpolatedStringHandlerArgument(nameof(notExpected), nameof(actual), nameof(delta))] ref AssertNonGenericAreNotEqualInterpolatedStringHandler message)
    {
        if (message.ShouldAppend)
        {
            // TODO:
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
        if (Math.Abs(notExpected - actual) <= delta)
        {
            string userMessage = BuildUserMessage(message, parameters);
            string finalMessage = string.Format(
                CultureInfo.CurrentCulture,
                FrameworkMessages.AreNotEqualDeltaFailMsg,
                userMessage,
                notExpected.ToString(CultureInfo.CurrentCulture.NumberFormat),
                actual.ToString(CultureInfo.CurrentCulture.NumberFormat),
                delta.ToString(CultureInfo.CurrentCulture.NumberFormat));
            ThrowAssertFailed("Assert.AreNotEqual", finalMessage);
        }
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
    public static void AreEqual(string? expected, string? actual, bool ignoreCase, [InterpolatedStringHandlerArgument(nameof(expected), nameof(actual), nameof(ignoreCase))] ref AssertNonGenericAreEqualInterpolatedStringHandler message)
    {
        if (message.ShouldAppend)
        {
            // TODO:
        }
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
    public static void AreEqual(string? expected, string? actual, bool ignoreCase,
        [NotNull] CultureInfo? culture)
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
    public static void AreEqual(string? expected, string? actual, bool ignoreCase,
        [NotNull] CultureInfo? culture, string? message)
        => AreEqual(expected, actual, ignoreCase, culture, message, null);

    /// <inheritdoc cref="AreEqual(string?, string?, bool, CultureInfo, string?)" />
    public static void AreEqual(string? expected, string? actual, bool ignoreCase,
        [NotNull] CultureInfo? culture, [InterpolatedStringHandlerArgument(nameof(expected), nameof(actual), nameof(ignoreCase), nameof(culture))] ref AssertNonGenericAreEqualInterpolatedStringHandler message)
    {
        CheckParameterNotNull(culture, "Assert.AreEqual", nameof(culture), string.Empty);
        if (message.ShouldAppend)
        {
            // TODO:
        }
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
    public static void AreEqual(string? expected, string? actual, bool ignoreCase,
        [NotNull] CultureInfo? culture, [StringSyntax(StringSyntaxAttribute.CompositeFormat)] string? message, params object?[]? parameters)
    {
        CheckParameterNotNull(culture, "Assert.AreEqual", "culture", string.Empty);
        if (CompareInternal(expected, actual, ignoreCase, culture) == 0)
        {
            return;
        }

        string userMessage = BuildUserMessage(message, parameters);
        string finalMessage = !ignoreCase && CompareInternal(expected, actual, ignoreCase, culture) == 0
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

        // Comparison failed. Check if it was a case-only failure.
        ThrowAssertFailed("Assert.AreEqual", finalMessage);
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
    public static void AreNotEqual(string? notExpected, string? actual, bool ignoreCase, [InterpolatedStringHandlerArgument(nameof(notExpected), nameof(actual), nameof(ignoreCase))] ref AssertNonGenericAreNotEqualInterpolatedStringHandler message)
    {
        if (message.ShouldAppend)
        {
            // TODO:
        }
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
    public static void AreNotEqual(string? notExpected, string? actual, bool ignoreCase, CultureInfo? culture)
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
    public static void AreNotEqual(string? notExpected, string? actual, bool ignoreCase,
        CultureInfo? culture, string? message)
        => AreNotEqual(notExpected, actual, ignoreCase, culture, message, null);

    /// <inheritdoc cref="AreNotEqual(string?, string?, bool, CultureInfo?, string?)" />
    public static void AreNotEqual(string? notExpected, string? actual, bool ignoreCase,
        CultureInfo? culture, [InterpolatedStringHandlerArgument(nameof(notExpected), nameof(actual), nameof(ignoreCase), nameof(culture))] ref AssertNonGenericAreNotEqualInterpolatedStringHandler message)
    {
        CheckParameterNotNull(culture, "Assert.AreEqual", nameof(culture), string.Empty);
        if (message.ShouldAppend)
        {
            // TODO:
        }
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
        CultureInfo? culture, [StringSyntax(StringSyntaxAttribute.CompositeFormat)] string? message, params object?[]? parameters)
    {
        CheckParameterNotNull(culture, "Assert.AreNotEqual", "culture", string.Empty);
        if (CompareInternal(notExpected, actual, ignoreCase, culture) != 0)
        {
            return;
        }

        string userMessage = BuildUserMessage(message, parameters);
        string finalMessage = GetAreNotEqualFinalMessage(notExpected, actual, userMessage);
        ThrowAssertFailed("Assert.AreNotEqual", finalMessage);
    }

    private static string GetAreNotEqualFinalMessage(object? notExpected, object? actual, string userMessage)
        => string.Format(
            CultureInfo.CurrentCulture,
            FrameworkMessages.AreNotEqualFailMsg,
            userMessage,
            ReplaceNulls(notExpected),
            ReplaceNulls(actual));
}
