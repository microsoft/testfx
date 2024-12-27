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
    public readonly struct AssertIsTrueInterpolatedStringHandler
    {
        private readonly StringBuilder? _builder;

        public AssertIsTrueInterpolatedStringHandler(int literalLength, int formattedCount, bool? condition, out bool shouldAppend)
        {
            shouldAppend = condition is false or null;
            if (shouldAppend)
            {
                _builder = new StringBuilder(literalLength + formattedCount);
            }
        }

        internal readonly string ToStringAndClear() => _builder!.ToString();

        public readonly void AppendLiteral(string value) => _builder!.Append(value);

        public readonly void AppendFormatted<T>(T value) => _builder!.Append(value);

#if NET
        public readonly void AppendFormatted(ReadOnlySpan<char> value) => _builder!.Append(value.ToString());
#endif
    }

    [InterpolatedStringHandler]
    [EditorBrowsable(EditorBrowsableState.Never)]
    public readonly struct AssertIsFalseInterpolatedStringHandler
    {
        private readonly StringBuilder? _builder;

        public AssertIsFalseInterpolatedStringHandler(int literalLength, int formattedCount, bool? condition, out bool shouldAppend)
        {
            shouldAppend = condition is true or null;
            if (shouldAppend)
            {
                _builder = new StringBuilder(literalLength + formattedCount);
            }
        }

        internal readonly string ToStringAndClear() => _builder!.ToString();

        public readonly void AppendLiteral(string value) => _builder!.Append(value);

        public readonly void AppendFormatted<T>(T value) => _builder!.Append(value);

#if NET
        public readonly void AppendFormatted(ReadOnlySpan<char> value) => _builder!.Append(value.ToString());
#endif
    }

    /// <summary>
    /// Tests whether the specified condition is true and throws an exception
    /// if the condition is false.
    /// </summary>
    /// <param name="condition">
    /// The condition the test expects to be true.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// Thrown if <paramref name="condition"/> is false.
    /// </exception>
    public static void IsTrue([DoesNotReturnIf(false)] bool condition)
        => IsTrue(condition, string.Empty, null);

    /// <summary>
    /// Tests whether the specified condition is true and throws an exception
    /// if the condition is false.
    /// </summary>
    /// <param name="condition">
    /// The condition the test expects to be true.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// Thrown if <paramref name="condition"/> is false.
    /// </exception>
    public static void IsTrue([DoesNotReturnIf(false)] bool? condition)
        => IsTrue(condition, string.Empty, null);

    /// <summary>
    /// Tests whether the specified condition is true and throws an exception
    /// if the condition is false.
    /// </summary>
    /// <param name="condition">
    /// The condition the test expects to be true.
    /// </param>
    /// <param name="message">
    /// The message to include in the exception when <paramref name="condition"/>
    /// is false. The message is shown in test results.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// Thrown if <paramref name="condition"/> is false.
    /// </exception>
    public static void IsTrue([DoesNotReturnIf(false)] bool condition, string? message)
        => IsTrue(condition, message, null);

    /// <inheritdoc cref="IsTrue(bool, string?)"/>
    public static void IsTrue([DoesNotReturnIf(false)] bool condition, [InterpolatedStringHandlerArgument(nameof(condition))] ref AssertIsTrueInterpolatedStringHandler message)
    {
        if (!condition)
        {
            ThrowAssertFailed("Assert.IsTrue", message.ToStringAndClear());
        }
    }

    /// <summary>
    /// Tests whether the specified condition is true and throws an exception
    /// if the condition is false.
    /// </summary>
    /// <param name="condition">
    /// The condition the test expects to be true.
    /// </param>
    /// <param name="message">
    /// The message to include in the exception when <paramref name="condition"/>
    /// is false. The message is shown in test results.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// Thrown if <paramref name="condition"/> is false.
    /// </exception>
    public static void IsTrue([DoesNotReturnIf(false)] bool? condition, string? message)
        => IsTrue(condition, message, null);

    /// <inheritdoc cref="IsTrue(bool?, string?)"/>
    public static void IsTrue([DoesNotReturnIf(false)] bool? condition, [InterpolatedStringHandlerArgument(nameof(condition))] ref AssertIsTrueInterpolatedStringHandler message)
    {
        if (condition is false or null)
        {
            ThrowAssertFailed("Assert.IsTrue", message.ToStringAndClear());
        }
    }

    /// <summary>
    /// Tests whether the specified condition is true and throws an exception
    /// if the condition is false.
    /// </summary>
    /// <param name="condition">
    /// The condition the test expects to be true.
    /// </param>
    /// <param name="message">
    /// The message to include in the exception when <paramref name="condition"/>
    /// is false. The message is shown in test results.
    /// </param>
    /// <param name="parameters">
    /// An array of parameters to use when formatting <paramref name="message"/>.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// Thrown if <paramref name="condition"/> is false.
    /// </exception>
    public static void IsTrue([DoesNotReturnIf(false)] bool condition, [StringSyntax(StringSyntaxAttribute.CompositeFormat)] string? message,
        params object?[]? parameters)
    {
        if (!condition)
        {
            ThrowAssertFailed("Assert.IsTrue", BuildUserMessage(message, parameters));
        }
    }

    /// <summary>
    /// Tests whether the specified condition is true and throws an exception
    /// if the condition is false.
    /// </summary>
    /// <param name="condition">
    /// The condition the test expects to be true.
    /// </param>
    /// <param name="message">
    /// The message to include in the exception when <paramref name="condition"/>
    /// is false. The message is shown in test results.
    /// </param>
    /// <param name="parameters">
    /// An array of parameters to use when formatting <paramref name="message"/>.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// Thrown if <paramref name="condition"/> is false.
    /// </exception>
    public static void IsTrue([DoesNotReturnIf(false)] bool? condition, [StringSyntax(StringSyntaxAttribute.CompositeFormat)] string? message,
        params object?[]? parameters)
    {
        if (condition is false or null)
        {
            ThrowAssertFailed("Assert.IsTrue", BuildUserMessage(message, parameters));
        }
    }

    /// <summary>
    /// Tests whether the specified condition is false and throws an exception
    /// if the condition is true.
    /// </summary>
    /// <param name="condition">
    /// The condition the test expects to be false.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// Thrown if <paramref name="condition"/> is true.
    /// </exception>
    public static void IsFalse([DoesNotReturnIf(true)] bool condition)
        => IsFalse(condition, string.Empty, null);

    /// <summary>
    /// Tests whether the specified condition is false and throws an exception
    /// if the condition is true.
    /// </summary>
    /// <param name="condition">
    /// The condition the test expects to be false.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// Thrown if <paramref name="condition"/> is true.
    /// </exception>
    public static void IsFalse([DoesNotReturnIf(true)] bool? condition)
        => IsFalse(condition, string.Empty, null);

    /// <summary>
    /// Tests whether the specified condition is false and throws an exception
    /// if the condition is true.
    /// </summary>
    /// <param name="condition">
    /// The condition the test expects to be false.
    /// </param>
    /// <param name="message">
    /// The message to include in the exception when <paramref name="condition"/>
    /// is true. The message is shown in test results.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// Thrown if <paramref name="condition"/> is true.
    /// </exception>
    public static void IsFalse([DoesNotReturnIf(true)] bool condition, string? message)
        => IsFalse(condition, message, null);

    /// <inheritdoc cref="IsFalse(bool, string?)" />
    public static void IsFalse([DoesNotReturnIf(true)] bool condition, [InterpolatedStringHandlerArgument(nameof(condition))] ref AssertIsFalseInterpolatedStringHandler message)
    {
        if (condition)
        {
            ThrowAssertFailed("Assert.IsFalse", message.ToStringAndClear());
        }
    }

    /// <summary>
    /// Tests whether the specified condition is false and throws an exception
    /// if the condition is true.
    /// </summary>
    /// <param name="condition">
    /// The condition the test expects to be false.
    /// </param>
    /// <param name="message">
    /// The message to include in the exception when <paramref name="condition"/>
    /// is true. The message is shown in test results.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// Thrown if <paramref name="condition"/> is true.
    /// </exception>
    public static void IsFalse([DoesNotReturnIf(true)] bool? condition, string? message)
        => IsFalse(condition, message, null);

    /// <inheritdoc cref="IsFalse(bool, string?)" />
    public static void IsFalse([DoesNotReturnIf(true)] bool? condition, [InterpolatedStringHandlerArgument(nameof(condition))] ref AssertIsFalseInterpolatedStringHandler message)
    {
        if (condition is true or null)
        {
            ThrowAssertFailed("Assert.IsFalse", message.ToStringAndClear());
        }
    }

    /// <summary>
    /// Tests whether the specified condition is false and throws an exception
    /// if the condition is true.
    /// </summary>
    /// <param name="condition">
    /// The condition the test expects to be false.
    /// </param>
    /// <param name="message">
    /// The message to include in the exception when <paramref name="condition"/>
    /// is true. The message is shown in test results.
    /// </param>
    /// <param name="parameters">
    /// An array of parameters to use when formatting <paramref name="message"/>.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// Thrown if <paramref name="condition"/> is true.
    /// </exception>
    public static void IsFalse([DoesNotReturnIf(true)] bool condition, [StringSyntax(StringSyntaxAttribute.CompositeFormat)] string? message,
        params object?[]? parameters)
    {
        if (condition)
        {
            ThrowAssertFailed("Assert.IsFalse", BuildUserMessage(message, parameters));
        }
    }

    /// <summary>
    /// Tests whether the specified condition is false and throws an exception
    /// if the condition is true.
    /// </summary>
    /// <param name="condition">
    /// The condition the test expects to be false.
    /// </param>
    /// <param name="message">
    /// The message to include in the exception when <paramref name="condition"/>
    /// is true. The message is shown in test results.
    /// </param>
    /// <param name="parameters">
    /// An array of parameters to use when formatting <paramref name="message"/>.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// Thrown if <paramref name="condition"/> is true.
    /// </exception>
    public static void IsFalse([DoesNotReturnIf(true)] bool? condition, [StringSyntax(StringSyntaxAttribute.CompositeFormat)] string? message,
        params object?[]? parameters)
    {
        if (condition is true or null)
        {
            ThrowAssertFailed("Assert.IsFalse", BuildUserMessage(message, parameters));
        }
    }
}
