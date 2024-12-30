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
    public readonly struct AssertIsNullInterpolatedStringHandler
    {
        private readonly StringBuilder? _builder;

        public AssertIsNullInterpolatedStringHandler(int literalLength, int formattedCount, object? value, out bool shouldAppend)
        {
            shouldAppend = IsNullFailing(value);
            if (shouldAppend)
            {
                _builder = new StringBuilder(literalLength + formattedCount);
            }
        }

        internal void FailIfNeeded()
        {
            if (_builder is not null)
            {
                FailIsNull(_builder.ToString());
            }
        }

        public readonly void AppendLiteral(string value) => _builder!.Append(value);

        public readonly void AppendFormatted<T>(T value) => _builder!.Append(value);

#if NET
        public readonly void AppendFormatted(ReadOnlySpan<char> value) => _builder!.Append(value.ToString());
#endif
    }

    [InterpolatedStringHandler]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public readonly struct AssertIsNotNullInterpolatedStringHandler
    {
        private readonly StringBuilder? _builder;

        public AssertIsNotNullInterpolatedStringHandler(int literalLength, int formattedCount, object? value, out bool shouldAppend)
        {
            shouldAppend = IsNotNullFailing(value);
            if (shouldAppend)
            {
                _builder = new StringBuilder(literalLength + formattedCount);
            }
        }

        internal void FailIfNeeded()
        {
            if (_builder is not null)
            {
                FailIsNotNull(_builder.ToString());
            }
        }

        public readonly void AppendLiteral(string value) => _builder!.Append(value);

        public readonly void AppendFormatted<T>(T value) => _builder!.Append(value);

#if NET
        public readonly void AppendFormatted(ReadOnlySpan<char> value) => _builder!.Append(value.ToString());
#endif
    }

    /// <summary>
    /// Tests whether the specified object is null and throws an exception
    /// if it is not.
    /// </summary>
    /// <param name="value">
    /// The object the test expects to be null.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// Thrown if <paramref name="value"/> is not null.
    /// </exception>
    public static void IsNull(object? value)
        => IsNull(value, string.Empty, null);

    /// <summary>
    /// Tests whether the specified object is null and throws an exception
    /// if it is not.
    /// </summary>
    /// <param name="value">
    /// The object the test expects to be null.
    /// </param>
    /// <param name="message">
    /// The message to include in the exception when <paramref name="value"/>
    /// is not null. The message is shown in test results.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// Thrown if <paramref name="value"/> is not null.
    /// </exception>
    public static void IsNull(object? value, string? message)
        => IsNull(value, message, null);

    /// <inheritdoc cref="IsNull(object?, string?)" />
#pragma warning disable IDE0060 // Remove unused parameter - false positive. The value parameter is used via the interpolated string handler.
    public static void IsNull(object? value, [InterpolatedStringHandlerArgument(nameof(value))] ref AssertIsNullInterpolatedStringHandler message)
#pragma warning restore IDE0060 // Remove unused parameter
        => message.FailIfNeeded();

    /// <summary>
    /// Tests whether the specified object is null and throws an exception
    /// if it is not.
    /// </summary>
    /// <param name="value">
    /// The object the test expects to be null.
    /// </param>
    /// <param name="message">
    /// The message to include in the exception when <paramref name="value"/>
    /// is not null. The message is shown in test results.
    /// </param>
    /// <param name="parameters">
    /// An array of parameters to use when formatting <paramref name="message"/>.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// Thrown if <paramref name="value"/> is not null.
    /// </exception>
    public static void IsNull(object? value, [StringSyntax(StringSyntaxAttribute.CompositeFormat)] string? message, params object?[]? parameters)
    {
        if (IsNullFailing(value))
        {
            FailIsNull(BuildUserMessage(message, parameters));
        }
    }

    private static bool IsNullFailing(object? value) => value is not null;

    private static void FailIsNull(string message)
        => ThrowAssertFailed("Assert.IsNull", message);

    /// <summary>
    /// Tests whether the specified object is non-null and throws an exception
    /// if it is null.
    /// </summary>
    /// <param name="value">
    /// The object the test expects not to be null.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// Thrown if <paramref name="value"/> is null.
    /// </exception>
    public static void IsNotNull([NotNull] object? value)
        => IsNotNull(value, string.Empty, null);

    /// <summary>
    /// Tests whether the specified object is non-null and throws an exception
    /// if it is null.
    /// </summary>
    /// <param name="value">
    /// The object the test expects not to be null.
    /// </param>
    /// <param name="message">
    /// The message to include in the exception when <paramref name="value"/>
    /// is null. The message is shown in test results.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// Thrown if <paramref name="value"/> is null.
    /// </exception>
    public static void IsNotNull([NotNull] object? value, string? message)
        => IsNotNull(value, message, null);

    /// <inheritdoc cref="IsNull(object?, string?)" />
#pragma warning disable IDE0060 // Remove unused parameter - false positive. The value parameter is used via the interpolated string handler.
    public static void IsNotNull(object? value, [InterpolatedStringHandlerArgument(nameof(value))] ref AssertIsNotNullInterpolatedStringHandler message)
#pragma warning restore IDE0060 // Remove unused parameter
        => message.FailIfNeeded();

    /// <summary>
    /// Tests whether the specified object is non-null and throws an exception
    /// if it is null.
    /// </summary>
    /// <param name="value">
    /// The object the test expects not to be null.
    /// </param>
    /// <param name="message">
    /// The message to include in the exception when <paramref name="value"/>
    /// is null. The message is shown in test results.
    /// </param>
    /// <param name="parameters">
    /// An array of parameters to use when formatting <paramref name="message"/>.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// Thrown if <paramref name="value"/> is null.
    /// </exception>
    public static void IsNotNull([NotNull] object? value, [StringSyntax(StringSyntaxAttribute.CompositeFormat)] string? message, params object?[]? parameters)
    {
        if (IsNotNullFailing(value))
        {
            FailIsNotNull(BuildUserMessage(message, parameters));
        }
    }

    private static bool IsNotNullFailing([NotNullWhen(false)] object? value) => value is null;

    [DoesNotReturn]
    private static void FailIsNotNull(string message)
        => ThrowAssertFailed("Assert.IsNotNull", message);
}
