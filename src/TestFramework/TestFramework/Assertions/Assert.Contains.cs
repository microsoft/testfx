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
    public readonly struct AssertSingleInterpolatedStringHandler<TItem>
    {
        private readonly StringBuilder? _builder;
        private readonly int _actualCount;
        private readonly TItem? _item;

        public AssertSingleInterpolatedStringHandler(int literalLength, int formattedCount, IEnumerable<TItem> collection, out bool shouldAppend)
        {
            _actualCount = collection.Count();
            shouldAppend = _actualCount != 1;
            if (shouldAppend)
            {
                _builder = new StringBuilder(literalLength + formattedCount);
            }
            else
            {
                _item = collection.First();
            }
        }

        internal TItem ComputeAssertion(string assertionName)
        {
            if (_builder is not null)
            {
                ThrowAssertCountFailed(assertionName, 1, _actualCount, _builder.ToString());
            }

            return _item!;
        }

        public void AppendLiteral(string value)
            => _builder!.Append(value);

        public void AppendFormatted<T>(T value)
            => AppendFormatted(value, format: null);

#if NETCOREAPP3_1_OR_GREATER
        public void AppendFormatted(ReadOnlySpan<char> value)
            => _builder!.Append(value);

#pragma warning disable RS0027 // API with optional parameter(s) should have the most parameters amongst its public overloads
        public void AppendFormatted(ReadOnlySpan<char> value, int alignment = 0, string? format = null)
            => AppendFormatted(value.ToString(), alignment, format);
#pragma warning restore RS0027 // API with optional parameter(s) should have the most parameters amongst its public overloads
#endif

        // NOTE: All the overloads involving format and/or alignment are not super efficient.
        // This code path is only for when an assert is failing, so that's not the common scenario
        // and should be okay if not very optimized.
        // A more efficient implementation that can be used for .NET 6 and later is to delegate the work to
        // the BCL's StringBuilder.AppendInterpolatedStringHandler
        public void AppendFormatted<T>(T value, string? format)
            => _builder!.AppendFormat(null, $"{{0:{format}}}", value);

        public void AppendFormatted<T>(T value, int alignment)
            => _builder!.AppendFormat(null, $"{{0,{alignment}}}", value);

        public void AppendFormatted<T>(T value, int alignment, string? format)
            => _builder!.AppendFormat(null, $"{{0,{alignment}:{format}}}", value);

        public void AppendFormatted(string? value)
            => _builder!.Append(value);

#pragma warning disable RS0026 // Do not add multiple public overloads with optional parameters
#pragma warning disable RS0027 // API with optional parameter(s) should have the most parameters amongst its public overloads
        public void AppendFormatted(string? value, int alignment = 0, string? format = null)
            => _builder!.AppendFormat(null, $"{{0,{alignment}:{format}}}", value);

        public void AppendFormatted(object? value, int alignment = 0, string? format = null)
            => _builder!.AppendFormat(null, $"{{0,{alignment}:{format}}}", value);
#pragma warning restore RS0026 // Do not add multiple public overloads with optional parameters
#pragma warning restore RS0027 // API with optional parameter(s) should have the most parameters amongst its public overloads
    }

    public static T ContainsSingle<T>(IEnumerable<T> collection)
        => ContainsSingle(collection, string.Empty, null);

    public static T ContainsSingle<T>(IEnumerable<T> collection, string? message)
        => ContainsSingle(collection, message, null);

#pragma warning disable IDE0060 // Remove unused parameter
    public static T ContainsSingle<T>(IEnumerable<T> collection, [InterpolatedStringHandlerArgument(nameof(collection))] ref AssertSingleInterpolatedStringHandler<T> message)
#pragma warning restore IDE0060 // Remove unused parameter
        => message.ComputeAssertion("ContainsSingle");

    public static T ContainsSingle<T>(IEnumerable<T> collection, [StringSyntax(StringSyntaxAttribute.CompositeFormat)] string? message, params object?[]? parameters)
    {
        int actualCount = collection.Count();
        if (actualCount == 1)
        {
            return collection.First();
        }

        string userMessage = BuildUserMessage(message, parameters);
        ThrowAssertCountFailed("ContainsSingle", 1, actualCount, userMessage);

        // Unreachable code but compiler cannot work it out
        return default;
    }

    #region Contains

    public static void Contains<T>(IEnumerable<T> collection, T expected)
        => Contains(collection, expected, string.Empty, null);

    public static void Contains<T>(IEnumerable<T> collection, T expected, string? message)
        => Contains(collection, expected, message, null);

    public static void Contains<T>(IEnumerable<T> collection, T expected, string? message, params object?[]? parameters)
    {
        if (!collection.Contains(expected))
        {
            string userMessage = BuildUserMessage(message, parameters);
            ThrowAssertFailed("Contains", userMessage);
        }
    }

    public static void Contains<T>(IEnumerable<T> collection, T expected, IEqualityComparer<T> comparer)
        => Contains(collection, expected, comparer, string.Empty, null);

    public static void Contains<T>(IEnumerable<T> collection, T expected, IEqualityComparer<T> comparer, string? message)
        => Contains(collection, expected, comparer, message, null);

    public static void Contains<T>(IEnumerable<T> collection, T expected, IEqualityComparer<T> comparer, string? message, params object?[]? parameters)
    {
        if (!collection.Contains(expected, comparer))
        {
            string userMessage = BuildUserMessage(message, parameters);
            ThrowAssertFailed("Contains", userMessage);
        }
    }

    public static void Contains<T>(IEnumerable<T> collection, Func<T, bool> predicate)
        => Contains(collection, predicate, string.Empty, null);

    public static void Contains<T>(IEnumerable<T> collection, Func<T, bool> predicate, string? message)
        => Contains(collection, predicate, message, null);

    public static void Contains<T>(IEnumerable<T> collection, Func<T, bool> predicate, string? message, params object?[]? parameters)
    {
        if (!collection.Any(predicate))
        {
            string userMessage = BuildUserMessage(message, parameters);
            ThrowAssertFailed("Contains", userMessage);
        }
    }

    /// <summary>
    /// Tests whether the specified string contains the specified substring
    /// and throws an exception if the substring does not occur within the
    /// test string.
    /// </summary>
    /// <param name="value">
    /// The string that is expected to contain <paramref name="substring"/>.
    /// </param>
    /// <param name="substring">
    /// The string expected to occur within <paramref name="value"/>.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// <paramref name="value"/> is null, or <paramref name="substring"/> is null,
    /// or <paramref name="value"/> does not contain <paramref name="substring"/>.
    /// </exception>
    public static void Contains(string value, string substring)
        => Contains(value, substring, StringComparison.Ordinal, string.Empty, null);

    /// <summary>
    /// Tests whether the specified string contains the specified substring
    /// and throws an exception if the substring does not occur within the
    /// test string.
    /// </summary>
    /// <param name="value">
    /// The string that is expected to contain <paramref name="substring"/>.
    /// </param>
    /// <param name="substring">
    /// The string expected to occur within <paramref name="value"/>.
    /// </param>
    /// <param name="message">
    /// The message to include in the exception when <paramref name="substring"/>
    /// is not in <paramref name="value"/>. The message is shown in
    /// test results.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// <paramref name="value"/> is null, or <paramref name="substring"/> is null,
    /// or <paramref name="value"/> does not contain <paramref name="substring"/>.
    /// </exception>
    public static void Contains(string value, string substring, string? message)
        => Contains(value, substring, StringComparison.Ordinal, message, null);

    /// <summary>
    /// Tests whether the specified string contains the specified substring
    /// and throws an exception if the substring does not occur within the
    /// test string.
    /// </summary>
    /// <param name="value">
    /// The string that is expected to contain <paramref name="substring"/>.
    /// </param>
    /// <param name="substring">
    /// The string expected to occur within <paramref name="value"/>.
    /// </param>
    /// <param name="message">
    /// The message to include in the exception when <paramref name="substring"/>
    /// is not in <paramref name="value"/>. The message is shown in
    /// test results.
    /// </param>
    /// <param name="parameters">
    /// An array of parameters to use when formatting <paramref name="message"/>.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// <paramref name="value"/> is null, or <paramref name="substring"/> is null,
    /// or <paramref name="value"/> does not contain <paramref name="substring"/>.
    /// </exception>
    public static void Contains(string value, string substring, [StringSyntax(StringSyntaxAttribute.CompositeFormat)] string? message,
        params object?[]? parameters)
        => Contains(value, substring, StringComparison.Ordinal, message, parameters);

    /// <summary>
    /// Tests whether the specified string contains the specified substring
    /// and throws an exception if the substring does not occur within the
    /// test string.
    /// </summary>
    /// <param name="value">
    /// The string that is expected to contain <paramref name="substring"/>.
    /// </param>
    /// <param name="substring">
    /// The string expected to occur within <paramref name="value"/>.
    /// </param>
    /// <param name="comparisonType">
    /// The comparison method to compare strings <paramref name="comparisonType"/>.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// <paramref name="value"/> is null, or <paramref name="substring"/> is null,
    /// or <paramref name="value"/> does not contain <paramref name="substring"/>.
    /// </exception>
    public static void Contains(string value, string substring, StringComparison comparisonType)
        => Contains(value, substring, comparisonType, string.Empty, null);

    /// <summary>
    /// Tests whether the specified string contains the specified substring
    /// and throws an exception if the substring does not occur within the
    /// test string.
    /// </summary>
    /// <param name="value">
    /// The string that is expected to contain <paramref name="substring"/>.
    /// </param>
    /// <param name="substring">
    /// The string expected to occur within <paramref name="value"/>.
    /// </param>
    /// <param name="comparisonType">
    /// The comparison method to compare strings <paramref name="comparisonType"/>.
    /// </param>
    /// <param name="message">
    /// The message to include in the exception when <paramref name="substring"/>
    /// is not in <paramref name="value"/>. The message is shown in
    /// test results.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// <paramref name="value"/> is null, or <paramref name="substring"/> is null,
    /// or <paramref name="value"/> does not contain <paramref name="substring"/>.
    /// </exception>
    public static void Contains(string value, string substring, StringComparison comparisonType, string? message)
        => Contains(value, substring, comparisonType, message, null);

    /// <summary>
    /// Tests whether the specified string contains the specified substring
    /// and throws an exception if the substring does not occur within the
    /// test string.
    /// </summary>
    /// <param name="value">
    /// The string that is expected to contain <paramref name="substring"/>.
    /// </param>
    /// <param name="substring">
    /// The string expected to occur within <paramref name="value"/>.
    /// </param>
    /// <param name="comparisonType">
    /// The comparison method to compare strings <paramref name="comparisonType"/>.
    /// </param>
    /// <param name="message">
    /// The message to include in the exception when <paramref name="substring"/>
    /// is not in <paramref name="value"/>. The message is shown in
    /// test results.
    /// </param>
    /// <param name="parameters">
    /// An array of parameters to use when formatting <paramref name="message"/>.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// <paramref name="value"/> is null, or <paramref name="substring"/> is null,
    /// or <paramref name="value"/> does not contain <paramref name="substring"/>.
    /// </exception>
    public static void Contains(string value, string substring, StringComparison comparisonType,
        [StringSyntax(StringSyntaxAttribute.CompositeFormat)] string? message, params object?[]? parameters)
    {
#if NETFRAMEWORK || NETSTANDARD
        if (value.IndexOf(substring, comparisonType) < 0)
#else
        if (!value.Contains(substring, comparisonType))
#endif
        {
            string userMessage = BuildUserMessage(message, parameters);
            string finalMessage = string.Format(CultureInfo.CurrentCulture, FrameworkMessages.ContainsFail, value, substring, userMessage);
            ThrowAssertFailed("StringAssert.Contains", finalMessage);
        }
    }

    #endregion // Contains

    #region DoesNotContain

    public static void DoesNotContain<T>(IEnumerable<T> collection, T expected)
    => DoesNotContain(collection, expected, string.Empty, null);

    public static void DoesNotContain<T>(IEnumerable<T> collection, T expected, string? message)
        => DoesNotContain(collection, expected, message, null);

    public static void DoesNotContain<T>(IEnumerable<T> collection, T expected, string? message, params object?[]? parameters)
    {
        if (collection.Contains(expected))
        {
            string userMessage = BuildUserMessage(message, parameters);
            ThrowAssertFailed("DoesNotContain", userMessage);
        }
    }

    public static void DoesNotContain<T>(IEnumerable<T> collection, T expected, IEqualityComparer<T> comparer)
        => DoesNotContain(collection, expected, comparer, string.Empty, null);

    public static void DoesNotContain<T>(IEnumerable<T> collection, T expected, IEqualityComparer<T> comparer, string? message)
        => DoesNotContain(collection, expected, comparer, message, null);

    public static void DoesNotContain<T>(IEnumerable<T> collection, T expected, IEqualityComparer<T> comparer, string? message, params object?[]? parameters)
    {
        if (collection.Contains(expected, comparer))
        {
            string userMessage = BuildUserMessage(message, parameters);
            ThrowAssertFailed("DoesNotContain", userMessage);
        }
    }

    public static void DoesNotContain<T>(IEnumerable<T> collection, Func<T, bool> predicate)
        => DoesNotContain(collection, predicate, string.Empty, null);

    public static void DoesNotContain<T>(IEnumerable<T> collection, Func<T, bool> predicate, string? message)
        => DoesNotContain(collection, predicate, message, null);

    public static void DoesNotContain<T>(IEnumerable<T> collection, Func<T, bool> predicate, string? message, params object?[]? parameters)
    {
        if (collection.Any(predicate))
        {
            string userMessage = BuildUserMessage(message, parameters);
            ThrowAssertFailed("DoesNotContain", userMessage);
        }
    }

    /// <summary>
    /// Tests whether the specified string contains the specified substring
    /// and throws an exception if the substring does not occur within the
    /// test string.
    /// </summary>
    /// <param name="value">
    /// The string that is expected to contain <paramref name="substring"/>.
    /// </param>
    /// <param name="substring">
    /// The string expected to occur within <paramref name="value"/>.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// <paramref name="value"/> is null, or <paramref name="substring"/> is null,
    /// or <paramref name="value"/> does not contain <paramref name="substring"/>.
    /// </exception>
    public static void DoesNotContain(string value, string substring)
        => DoesNotContain(value, substring, string.Empty, StringComparison.Ordinal);

    /// <summary>
    /// Tests whether the specified string contains the specified substring
    /// and throws an exception if the substring does not occur within the
    /// test string.
    /// </summary>
    /// <param name="value">
    /// The string that is expected to contain <paramref name="substring"/>.
    /// </param>
    /// <param name="substring">
    /// The string expected to occur within <paramref name="value"/>.
    /// </param>
    /// <param name="message">
    /// The message to include in the exception when <paramref name="substring"/>
    /// is not in <paramref name="value"/>. The message is shown in
    /// test results.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// <paramref name="value"/> is null, or <paramref name="substring"/> is null,
    /// or <paramref name="value"/> does not contain <paramref name="substring"/>.
    /// </exception>
    public static void DoesNotContain(string value, string substring, string? message)
        => DoesNotContain(value, substring, message, StringComparison.Ordinal);

    /// <summary>
    /// Tests whether the specified string contains the specified substring
    /// and throws an exception if the substring does not occur within the
    /// test string.
    /// </summary>
    /// <param name="value">
    /// The string that is expected to contain <paramref name="substring"/>.
    /// </param>
    /// <param name="substring">
    /// The string expected to occur within <paramref name="value"/>.
    /// </param>
    /// <param name="message">
    /// The message to include in the exception when <paramref name="substring"/>
    /// is not in <paramref name="value"/>. The message is shown in
    /// test results.
    /// </param>
    /// <param name="parameters">
    /// An array of parameters to use when formatting <paramref name="message"/>.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// <paramref name="value"/> is null, or <paramref name="substring"/> is null,
    /// or <paramref name="value"/> does not contain <paramref name="substring"/>.
    /// </exception>
    public static void DoesNotContain(string value, string substring, [StringSyntax(StringSyntaxAttribute.CompositeFormat)] string? message,
        params object?[]? parameters)
        => DoesNotContain(value, substring, message, StringComparison.Ordinal, parameters);

    /// <summary>
    /// Tests whether the specified string contains the specified substring
    /// and throws an exception if the substring does not occur within the
    /// test string.
    /// </summary>
    /// <param name="value">
    /// The string that is expected to contain <paramref name="substring"/>.
    /// </param>
    /// <param name="substring">
    /// The string expected to occur within <paramref name="value"/>.
    /// </param>
    /// <param name="comparisonType">
    /// The comparison method to compare strings <paramref name="comparisonType"/>.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// <paramref name="value"/> is null, or <paramref name="substring"/> is null,
    /// or <paramref name="value"/> does not contain <paramref name="substring"/>.
    /// </exception>
    public static void DoesNotContain(string value, string substring, StringComparison comparisonType)
        => DoesNotContain(value, substring, string.Empty, comparisonType);

    /// <summary>
    /// Tests whether the specified string contains the specified substring
    /// and throws an exception if the substring does not occur within the
    /// test string.
    /// </summary>
    /// <param name="value">
    /// The string that is expected to contain <paramref name="substring"/>.
    /// </param>
    /// <param name="substring">
    /// The string expected to occur within <paramref name="value"/>.
    /// </param>
    /// <param name="message">
    /// The message to include in the exception when <paramref name="substring"/>
    /// is not in <paramref name="value"/>. The message is shown in
    /// test results.
    /// </param>
    /// <param name="comparisonType">
    /// The comparison method to compare strings <paramref name="comparisonType"/>.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// <paramref name="value"/> is null, or <paramref name="substring"/> is null,
    /// or <paramref name="value"/> does not contain <paramref name="substring"/>.
    /// </exception>
    public static void DoesNotContain(string value, string substring, string? message, StringComparison comparisonType)
        => DoesNotContain(value, substring, message, comparisonType, string.Empty);

    /// <summary>
    /// Tests whether the specified string contains the specified substring
    /// and throws an exception if the substring does not occur within the
    /// test string.
    /// </summary>
    /// <param name="value">
    /// The string that is expected to contain <paramref name="substring"/>.
    /// </param>
    /// <param name="substring">
    /// The string expected to occur within <paramref name="value"/>.
    /// </param>
    /// <param name="comparisonType">
    /// The comparison method to compare strings <paramref name="comparisonType"/>.
    /// </param>
    /// <param name="message">
    /// The message to include in the exception when <paramref name="substring"/>
    /// is not in <paramref name="value"/>. The message is shown in
    /// test results.
    /// </param>
    /// <param name="parameters">
    /// An array of parameters to use when formatting <paramref name="message"/>.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// <paramref name="value"/> is null, or <paramref name="substring"/> is null,
    /// or <paramref name="value"/> does not contain <paramref name="substring"/>.
    /// </exception>
    public static void DoesNotContain(string value, string substring, StringComparison comparisonType,
        [StringSyntax(StringSyntaxAttribute.CompositeFormat)] string? message, params object?[]? parameters)
    {
#if NETFRAMEWORK || NETSTANDARD
        if (value.IndexOf(substring, comparisonType) >= 0)
#else
        if (value.Contains(substring, comparisonType))
#endif
        {
            string userMessage = BuildUserMessage(message, parameters);
            string finalMessage = string.Format(CultureInfo.CurrentCulture, FrameworkMessages.DoesNotContainFail, value, substring, userMessage);
            ThrowAssertFailed("StringAssert.DoesNotContain", finalMessage);
        }
    }

    #endregion // DoesNotContain
}
