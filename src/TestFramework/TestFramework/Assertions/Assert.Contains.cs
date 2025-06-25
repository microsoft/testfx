// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.ComponentModel;

namespace Microsoft.VisualStudio.TestTools.UnitTesting;

#pragma warning disable RS0027 // API with optional parameter(s) should have the most parameters amongst its public overloads

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

        internal TItem ComputeAssertion()
        {
            if (_builder is not null)
            {
                ThrowAssertContainsSingleFailed(_actualCount, _builder.ToString());
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

        public void AppendFormatted(ReadOnlySpan<char> value, int alignment = 0, string? format = null)
            => AppendFormatted(value.ToString(), alignment, format);
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
        public void AppendFormatted(string? value, int alignment = 0, string? format = null)
            => _builder!.AppendFormat(null, $"{{0,{alignment}:{format}}}", value);

        public void AppendFormatted(object? value, int alignment = 0, string? format = null)
            => _builder!.AppendFormat(null, $"{{0,{alignment}:{format}}}", value);
#pragma warning restore RS0026 // Do not add multiple public overloads with optional parameters
    }
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member

    /// <summary>
    /// Tests whether the specified collection contains exactly one element.
    /// </summary>
    /// <typeparam name="T">The type of the collection items.</typeparam>
    /// <param name="collection">The collection.</param>
    /// <returns>The item.</returns>
    public static T ContainsSingle<T>(IEnumerable<T> collection)
        => ContainsSingle(collection, string.Empty, null);

    /// <summary>
    /// Tests whether the specified collection contains exactly one element.
    /// </summary>
    /// <typeparam name="T">The type of the collection items.</typeparam>
    /// <param name="collection">The collection.</param>
    /// <param name="message">The message to display when the assertion fails.</param>
    /// <returns>The item.</returns>
    public static T ContainsSingle<T>(IEnumerable<T> collection, string? message)
        => ContainsSingle(collection, message, null);

    /// <summary>
    /// Tests whether the specified collection contains exactly one element.
    /// </summary>
    /// <typeparam name="T">The type of the collection items.</typeparam>
    /// <param name="collection">The collection.</param>
    /// <param name="message">The message to display when the assertion fails.</param>
    /// <returns>The item.</returns>
#pragma warning disable IDE0060 // Remove unused parameter
    public static T ContainsSingle<T>(IEnumerable<T> collection, [InterpolatedStringHandlerArgument(nameof(collection))] ref AssertSingleInterpolatedStringHandler<T> message)
#pragma warning restore IDE0060 // Remove unused parameter
        => message.ComputeAssertion();

    /// <summary>
    /// Tests whether the specified collection contains exactly one element.
    /// </summary>
    /// <typeparam name="T">The type of the collection items.</typeparam>
    /// <param name="collection">The collection.</param>
    /// <param name="message">The message format to display when the assertion fails.</param>
    /// <param name="parameters">The parameters to format the message.</param>
    /// <returns>The item.</returns>
    public static T ContainsSingle<T>(IEnumerable<T> collection, [StringSyntax(StringSyntaxAttribute.CompositeFormat)] string? message, params object?[]? parameters)
    {
        int actualCount = collection.Count();
        if (actualCount == 1)
        {
            return collection.First();
        }

        string userMessage = BuildUserMessage(message, parameters);
        ThrowAssertContainsSingleFailed(actualCount, userMessage);

        // Unreachable code but compiler cannot work it out
        return default;
    }

    /// <summary>
    /// Tests whether the specified collection contains exactly one element that matches the given predicate.
    /// </summary>
    /// <typeparam name="T">The type of the collection items.</typeparam>
    /// <param name="predicate">A function to test each element for a condition.</param>
    /// <param name="collection">The collection.</param>
    /// <param name="message">The message format to display when the assertion fails.</param>
    /// <returns>The item that matches the predicate.</returns>
    public static T ContainsSingle<T>(Func<T, bool> predicate, IEnumerable<T> collection, string message = "")
    {
        var matchingElements = collection.Where(predicate).ToList();
        int actualCount = matchingElements.Count;

        if (actualCount == 1)
        {
            return matchingElements[0];
        }

        string userMessage = BuildUserMessage(message);
        ThrowAssertSingleMatchFailed(actualCount, userMessage);

        // Unreachable code but compiler cannot work it out
        return default;
    }

    #region Contains

    /// <summary>
    /// Tests whether the specified collection contains the given element.
    /// </summary>
    /// <typeparam name="T">The type of the collection items.</typeparam>
    /// <param name="expected">The expected item.</param>
    /// <param name="collection">The collection.</param>
    public static void Contains<T>(T expected, IEnumerable<T> collection)
        => Contains(expected, collection, string.Empty, null);

    /// <summary>
    /// Tests whether the specified collection contains the given element.
    /// </summary>
    /// <typeparam name="T">The type of the collection items.</typeparam>
    /// <param name="expected">The expected item.</param>
    /// <param name="collection">The collection.</param>
    /// <param name="message">The message to display when the assertion fails.</param>
    public static void Contains<T>(T expected, IEnumerable<T> collection, string? message)
        => Contains(expected, collection, message, null);

    /// <summary>
    /// Tests whether the specified collection contains the given element.
    /// </summary>
    /// <typeparam name="T">The type of the collection items.</typeparam>
    /// <param name="expected">The expected item.</param>
    /// <param name="collection">The collection.</param>
    /// <param name="message">The message format to display when the assertion fails.</param>
    /// <param name="parameters">The parameters to format the message.</param>
    public static void Contains<T>(T expected, IEnumerable<T> collection, string? message, params object?[]? parameters)
    {
        if (!collection.Contains(expected))
        {
            string userMessage = BuildUserMessage(message, parameters);
            ThrowAssertContainsItemFailed(userMessage);
        }
    }

    /// <summary>
    /// Tests whether the specified collection contains the given element.
    /// </summary>
    /// <typeparam name="T">The type of the collection items.</typeparam>
    /// <param name="expected">The expected item.</param>
    /// <param name="collection">The collection.</param>
    /// <param name="comparer">An equality comparer to compare values.</param>
    public static void Contains<T>(T expected, IEnumerable<T> collection, IEqualityComparer<T> comparer)
        => Contains(expected, collection, comparer, string.Empty, null);

    /// <summary>
    /// Tests whether the specified collection contains the given element.
    /// </summary>
    /// <typeparam name="T">The type of the collection items.</typeparam>
    /// <param name="expected">The expected item.</param>
    /// <param name="collection">The collection.</param>
    /// <param name="comparer">An equality comparer to compare values.</param>
    /// <param name="message">The message to display when the assertion fails.</param>
    public static void Contains<T>(T expected, IEnumerable<T> collection, IEqualityComparer<T> comparer, string? message)
        => Contains(expected, collection, comparer, message, null);

    /// <summary>
    /// Tests whether the specified collection contains the given element.
    /// </summary>
    /// <typeparam name="T">The type of the collection items.</typeparam>
    /// <param name="expected">The expected item.</param>
    /// <param name="collection">The collection.</param>
    /// <param name="comparer">An equality comparer to compare values.</param>
    /// <param name="message">The message to display when the assertion fails.</param>
    /// <param name="parameters">The parameters to format the message.</param>
    public static void Contains<T>(T expected, IEnumerable<T> collection, IEqualityComparer<T> comparer, string? message, params object?[]? parameters)
    {
        if (!collection.Contains(expected, comparer))
        {
            string userMessage = BuildUserMessage(message, parameters);
            ThrowAssertContainsItemFailed(userMessage);
        }
    }

    /// <summary>
    /// Tests whether the specified collection contains the given element.
    /// </summary>
    /// <typeparam name="T">The type of the collection items.</typeparam>
    /// <param name="predicate">A function to test each element for a condition.</param>
    /// <param name="collection">The collection.</param>
    public static void Contains<T>(Func<T, bool> predicate, IEnumerable<T> collection)
        => Contains(predicate, collection, string.Empty, null);

    /// <summary>
    /// Tests whether the specified collection contains the given element.
    /// </summary>
    /// <typeparam name="T">The type of the collection items.</typeparam>
    /// <param name="predicate">A function to test each element for a condition.</param>
    /// <param name="collection">The collection.</param>
    /// <param name="message">The message to display when the assertion fails.</param>
    public static void Contains<T>(Func<T, bool> predicate, IEnumerable<T> collection, string? message)
        => Contains(predicate, collection, message, null);

    /// <summary>
    /// Tests whether the specified collection contains the given element.
    /// </summary>
    /// <typeparam name="T">The type of the collection items.</typeparam>
    /// <param name="predicate">A function to test each element for a condition.</param>
    /// <param name="collection">The collection.</param>
    /// <param name="message">The message format to display when the assertion fails.</param>
    /// <param name="parameters">The parameters to format the message.</param>
    public static void Contains<T>(Func<T, bool> predicate, IEnumerable<T> collection, string? message, params object?[]? parameters)
    {
        if (!collection.Any(predicate))
        {
            string userMessage = BuildUserMessage(message, parameters);
            ThrowAssertContainsPredicateFailed(userMessage);
        }
    }

    /// <summary>
    /// Tests whether the specified string contains the specified substring
    /// and throws an exception if the substring does not occur within the
    /// test string.
    /// </summary>
    /// <param name="substring">
    /// The string expected to occur within <paramref name="value"/>.
    /// </param>
    /// <param name="value">
    /// The string that is expected to contain <paramref name="substring"/>.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// <paramref name="value"/> is null, or <paramref name="substring"/> is null,
    /// or <paramref name="value"/> does not contain <paramref name="substring"/>.
    /// </exception>
    public static void Contains(string substring, string value)
        => Contains(substring, value, StringComparison.Ordinal, string.Empty, null);

    /// <summary>
    /// Tests whether the specified string contains the specified substring
    /// and throws an exception if the substring does not occur within the
    /// test string.
    /// </summary>
    /// <param name="substring">
    /// The string expected to occur within <paramref name="value"/>.
    /// </param>
    /// <param name="value">
    /// The string that is expected to contain <paramref name="substring"/>.
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
    public static void Contains(string substring, string value, string? message)
        => Contains(substring, value, StringComparison.Ordinal, message, null);

    /// <summary>
    /// Tests whether the specified string contains the specified substring
    /// and throws an exception if the substring does not occur within the
    /// test string.
    /// </summary>
    /// <param name="substring">
    /// The string expected to occur within <paramref name="value"/>.
    /// </param>
    /// <param name="value">
    /// The string that is expected to contain <paramref name="substring"/>.
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
    public static void Contains(string substring, string value, [StringSyntax(StringSyntaxAttribute.CompositeFormat)] string? message,
        params object?[]? parameters)
        => Contains(substring, value, StringComparison.Ordinal, message, parameters);

    /// <summary>
    /// Tests whether the specified string contains the specified substring
    /// and throws an exception if the substring does not occur within the
    /// test string.
    /// </summary>
    /// <param name="substring">
    /// The string expected to occur within <paramref name="value"/>.
    /// </param>
    /// <param name="value">
    /// The string that is expected to contain <paramref name="substring"/>.
    /// </param>
    /// <param name="comparisonType">
    /// The comparison method to compare strings <paramref name="comparisonType"/>.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// <paramref name="value"/> is null, or <paramref name="substring"/> is null,
    /// or <paramref name="value"/> does not contain <paramref name="substring"/>.
    /// </exception>
    public static void Contains(string substring, string value, StringComparison comparisonType)
        => Contains(substring, value, comparisonType, string.Empty, null);

    /// <summary>
    /// Tests whether the specified string contains the specified substring
    /// and throws an exception if the substring does not occur within the
    /// test string.
    /// </summary>
    /// <param name="substring">
    /// The string expected to occur within <paramref name="value"/>.
    /// </param>
    /// <param name="value">
    /// The string that is expected to contain <paramref name="substring"/>.
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
    public static void Contains(string substring, string value, StringComparison comparisonType, string? message)
        => Contains(substring, value, comparisonType, message, null);

    /// <summary>
    /// Tests whether the specified string contains the specified substring
    /// and throws an exception if the substring does not occur within the
    /// test string.
    /// </summary>
    /// <param name="substring">
    /// The string expected to occur within <paramref name="value"/>.
    /// </param>
    /// <param name="value">
    /// The string that is expected to contain <paramref name="substring"/>.
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
    public static void Contains(string substring, string value, StringComparison comparisonType,
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
            ThrowAssertFailed("Assert.Contains", finalMessage);
        }
    }

    #endregion // Contains

    #region DoesNotContain

    /// <summary>
    /// Tests whether the specified collection does not contain the specified item.
    /// </summary>
    /// <typeparam name="T">The type of the collection items.</typeparam>
    /// <param name="expected">The expected item.</param>
    /// <param name="collection">The collection.</param>
    public static void DoesNotContain<T>(T expected, IEnumerable<T> collection)
        => DoesNotContain(expected, collection, string.Empty, null);

    /// <summary>
    /// Tests whether the specified collection does not contain the specified item.
    /// </summary>
    /// <typeparam name="T">The type of the collection items.</typeparam>
    /// <param name="expected">The expected item.</param>
    /// <param name="collection">The collection.</param>
    /// <param name="message">The message to display when the assertion fails.</param>
    public static void DoesNotContain<T>(T expected, IEnumerable<T> collection, string? message)
        => DoesNotContain(expected, collection, message, null);

    /// <summary>
    /// Tests whether the specified collection does not contain the specified item.
    /// </summary>
    /// <typeparam name="T">The type of the collection items.</typeparam>
    /// <param name="expected">The expected item.</param>
    /// <param name="collection">The collection.</param>
    /// <param name="message">The message to display when the assertion fails.</param>
    /// <param name="parameters">The parameters to format the message.</param>
    public static void DoesNotContain<T>(T expected, IEnumerable<T> collection, string? message, params object?[]? parameters)
    {
        if (collection.Contains(expected))
        {
            string userMessage = BuildUserMessage(message, parameters);
            ThrowAssertDoesNotContainItemFailed(userMessage);
        }
    }

    /// <summary>
    /// Tests whether the specified collection does not contain the specified item.
    /// </summary>
    /// <typeparam name="T">The type of the collection items.</typeparam>
    /// <param name="expected">The expected item.</param>
    /// <param name="collection">The collection.</param>
    /// <param name="comparer">An equality comparer to compare values.</param>
    public static void DoesNotContain<T>(T expected, IEnumerable<T> collection, IEqualityComparer<T> comparer)
        => DoesNotContain(expected, collection, comparer, string.Empty, null);

    /// <summary>
    /// Tests whether the specified collection does not contain the specified item.
    /// </summary>
    /// <typeparam name="T">The type of the collection items.</typeparam>
    /// <param name="expected">The expected item.</param>
    /// <param name="collection">The collection.</param>
    /// <param name="comparer">An equality comparer to compare values.</param>
    /// <param name="message">The message to display when the assertion fails.</param>
    public static void DoesNotContain<T>(T expected, IEnumerable<T> collection, IEqualityComparer<T> comparer, string? message)
        => DoesNotContain(expected, collection, comparer, message, null);

    /// <summary>
    /// Tests whether the specified collection does not contain the specified item.
    /// </summary>
    /// <typeparam name="T">The type of the collection items.</typeparam>
    /// <param name="expected">The expected item.</param>
    /// <param name="collection">The collection.</param>
    /// <param name="comparer">An equality comparer to compare values.</param>
    /// <param name="message">The message to display when the assertion fails.</param>
    /// <param name="parameters">The parameters to format the message.</param>
    public static void DoesNotContain<T>(T expected, IEnumerable<T> collection, IEqualityComparer<T> comparer, string? message, params object?[]? parameters)
    {
        if (collection.Contains(expected, comparer))
        {
            string userMessage = BuildUserMessage(message, parameters);
            ThrowAssertDoesNotContainItemFailed(userMessage);
        }
    }

    /// <summary>
    /// Tests whether the specified collection does not contain the specified item.
    /// </summary>
    /// <typeparam name="T">The type of the collection items.</typeparam>
    /// <param name="predicate">A function to test each element for a condition.</param>
    /// <param name="collection">The collection.</param>
    public static void DoesNotContain<T>(Func<T, bool> predicate, IEnumerable<T> collection)
        => DoesNotContain(predicate, collection, string.Empty, null);

    /// <summary>
    /// Tests whether the specified collection does not contain the specified item.
    /// </summary>
    /// <typeparam name="T">The type of the collection items.</typeparam>
    /// <param name="predicate">A function to test each element for a condition.</param>
    /// <param name="collection">The collection.</param>
    /// <param name="message">The message to display when the assertion fails.</param>
    public static void DoesNotContain<T>(Func<T, bool> predicate, IEnumerable<T> collection, string? message)
        => DoesNotContain(predicate, collection, message, null);

    /// <summary>
    /// Tests whether the specified collection does not contain the specified item.
    /// </summary>
    /// <typeparam name="T">The type of the collection items.</typeparam>
    /// <param name="predicate">A function to test each element for a condition.</param>
    /// <param name="collection">The collection.</param>
    /// <param name="message">The message to display when the assertion fails.</param>
    /// <param name="parameters">The parameters to format the message.</param>
    public static void DoesNotContain<T>(Func<T, bool> predicate, IEnumerable<T> collection, string? message, params object?[]? parameters)
    {
        if (collection.Any(predicate))
        {
            string userMessage = BuildUserMessage(message, parameters);
            ThrowAssertDoesNotContainPredicateFailed(userMessage);
        }
    }

    /// <summary>
    /// Tests whether the specified string does not contain the specified substring
    /// and throws an exception if the substring occurs within the
    /// test string.
    /// </summary>
    /// <param name="substring">
    /// The string expected to not occur within <paramref name="value"/>.
    /// </param>
    /// <param name="value">
    /// The string that is expected to not contain <paramref name="substring"/>.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// <paramref name="value"/> is null, or <paramref name="substring"/> is null,
    /// or <paramref name="value"/> contains <paramref name="substring"/>.
    /// </exception>
    public static void DoesNotContain(string substring, string value)
        => DoesNotContain(substring, value, StringComparison.Ordinal, string.Empty);

    /// <summary>
    /// Tests whether the specified string does not contain the specified substring
    /// and throws an exception if the substring occurs within the
    /// test string.
    /// </summary>
    /// <param name="substring">
    /// The string expected to not occur within <paramref name="value"/>.
    /// </param>
    /// <param name="value">
    /// The string that is expected to not contain <paramref name="substring"/>.
    /// </param>
    /// <param name="message">
    /// The message to include in the exception when <paramref name="substring"/>
    /// is in <paramref name="value"/>. The message is shown in
    /// test results.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// <paramref name="value"/> is null, or <paramref name="substring"/> is null,
    /// or <paramref name="value"/> contains <paramref name="substring"/>.
    /// </exception>
    public static void DoesNotContain(string substring, string value, string? message)
        => DoesNotContain(substring, value, StringComparison.Ordinal, message);

    /// <summary>
    /// Tests whether the specified string does not contain the specified substring
    /// and throws an exception if the substring occurs within the
    /// test string.
    /// </summary>
    /// <param name="substring">
    /// The string expected to not occur within <paramref name="value"/>.
    /// </param>
    /// <param name="value">
    /// The string that is expected to not contain <paramref name="substring"/>.
    /// </param>
    /// <param name="message">
    /// The message to include in the exception when <paramref name="substring"/>
    /// is in <paramref name="value"/>. The message is shown in
    /// test results.
    /// </param>
    /// <param name="parameters">
    /// An array of parameters to use when formatting <paramref name="message"/>.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// <paramref name="value"/> is null, or <paramref name="substring"/> is null,
    /// or <paramref name="value"/> contains <paramref name="substring"/>.
    /// </exception>
    public static void DoesNotContain(string substring, string value, [StringSyntax(StringSyntaxAttribute.CompositeFormat)] string? message,
        params object?[]? parameters)
        => DoesNotContain(substring, value, StringComparison.Ordinal, message, parameters);

    /// <summary>
    /// Tests whether the specified string does not contain the specified substring
    /// and throws an exception if the substring occurs within the
    /// test string.
    /// </summary>
    /// <param name="substring">
    /// The string expected to not occur within <paramref name="value"/>.
    /// </param>
    /// <param name="value">
    /// The string that is expected to not contain <paramref name="substring"/>.
    /// </param>
    /// <param name="comparisonType">
    /// The comparison method to compare strings <paramref name="comparisonType"/>.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// <paramref name="value"/> is null, or <paramref name="substring"/> is null,
    /// or <paramref name="value"/> contains <paramref name="substring"/>.
    /// </exception>
    public static void DoesNotContain(string substring, string value, StringComparison comparisonType)
        => DoesNotContain(substring, value, comparisonType, string.Empty);

    /// <summary>
    /// Tests whether the specified string does not contain the specified substring
    /// and throws an exception if the substring occurs within the
    /// test string.
    /// </summary>
    /// <param name="substring">
    /// The string expected to not occur within <paramref name="value"/>.
    /// </param>
    /// <param name="value">
    /// The string that is expected to not contain <paramref name="substring"/>.
    /// </param>
    /// <param name="comparisonType">
    /// The comparison method to compare strings <paramref name="comparisonType"/>.
    /// </param>
    /// <param name="message">
    /// The message to include in the exception when <paramref name="substring"/>
    /// is in <paramref name="value"/>. The message is shown in
    /// test results.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// <paramref name="value"/> is null, or <paramref name="substring"/> is null,
    /// or <paramref name="value"/> contains <paramref name="substring"/>.
    /// </exception>
    public static void DoesNotContain(string substring, string value, StringComparison comparisonType, string? message)
        => DoesNotContain(substring, value, comparisonType, message, null);

    /// <summary>
    /// Tests whether the specified string does not contain the specified substring
    /// and throws an exception if the substring occurs within the
    /// test string.
    /// </summary>
    /// <param name="substring">
    /// The string expected to not occur within <paramref name="value"/>.
    /// </param>
    /// <param name="value">
    /// The string that is expected to not contain <paramref name="substring"/>.
    /// </param>
    /// <param name="comparisonType">
    /// The comparison method to compare strings <paramref name="comparisonType"/>.
    /// </param>
    /// <param name="message">
    /// The message to include in the exception when <paramref name="substring"/>
    /// is in <paramref name="value"/>. The message is shown in
    /// test results.
    /// </param>
    /// <param name="parameters">
    /// An array of parameters to use when formatting <paramref name="message"/>.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// <paramref name="value"/> is null, or <paramref name="substring"/> is null,
    /// or <paramref name="value"/> contains <paramref name="substring"/>.
    /// </exception>
    public static void DoesNotContain(string substring, string value, StringComparison comparisonType,
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
            ThrowAssertFailed("Assert.DoesNotContain", finalMessage);
        }
    }

    #endregion // DoesNotContain

    #region IsInRange

    /// <summary>
    /// Tests whether the specified value is within the expected range (inclusive).
    /// The range includes both the minimum and maximum values.
    /// </summary>
    /// <typeparam name="T">The type of the values to compare.</typeparam>
    /// <param name="minValue">The minimum value of the expected range (inclusive).</param>
    /// <param name="maxValue">The maximum value of the expected range (inclusive).</param>
    /// <param name="value">The value to test.</param>
    /// <param name="message">The message format to display when the assertion fails.</param>
    public static void IsInRange<T>(T minValue, T maxValue, T value, string message = "")
        where T : struct, IComparable<T>
    {
        if (maxValue.CompareTo(minValue) <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxValue), "The maximum value must be greater than the minimum value.");
        }

        if (value.CompareTo(minValue) < 0 || value.CompareTo(maxValue) > 0)
        {
            string userMessage = BuildUserMessage(message);
            string finalMessage = string.Format(CultureInfo.CurrentCulture, FrameworkMessages.IsInRangeFail, value, minValue, maxValue, userMessage);
            ThrowAssertFailed("IsInRange", finalMessage);
        }
    }

    #endregion // IsInRange

    [DoesNotReturn]
    private static void ThrowAssertSingleMatchFailed(int actualCount, string userMessage)
    {
        string finalMessage = string.Format(
            CultureInfo.CurrentCulture,
            FrameworkMessages.ContainsSingleMatchFailMsg,
            userMessage,
            actualCount);
        ThrowAssertFailed("Assert.ContainsSingle", finalMessage);
    }

    [DoesNotReturn]
    private static void ThrowAssertContainsSingleFailed(int actualCount, string userMessage)
    {
        string finalMessage = string.Format(
            CultureInfo.CurrentCulture,
            FrameworkMessages.ContainsSingleFailMsg,
            userMessage,
            actualCount);
        ThrowAssertFailed("Assert.ContainsSingle", finalMessage);
    }

    [DoesNotReturn]
    private static void ThrowAssertContainsItemFailed(string userMessage)
    {
        string finalMessage = string.Format(
            CultureInfo.CurrentCulture,
            FrameworkMessages.ContainsItemFailMsg,
            userMessage);
        ThrowAssertFailed("Assert.Contains", finalMessage);
    }

    [DoesNotReturn]
    private static void ThrowAssertContainsPredicateFailed(string userMessage)
    {
        string finalMessage = string.Format(
            CultureInfo.CurrentCulture,
            FrameworkMessages.ContainsPredicateFailMsg,
            userMessage);
        ThrowAssertFailed("Assert.Contains", finalMessage);
    }

    [DoesNotReturn]
    private static void ThrowAssertDoesNotContainItemFailed(string userMessage)
    {
        string finalMessage = string.Format(
            CultureInfo.CurrentCulture,
            FrameworkMessages.DoesNotContainItemFailMsg,
            userMessage);
        ThrowAssertFailed("Assert.DoesNotContain", finalMessage);
    }

    [DoesNotReturn]
    private static void ThrowAssertDoesNotContainPredicateFailed(string userMessage)
    {
        string finalMessage = string.Format(
            CultureInfo.CurrentCulture,
            FrameworkMessages.DoesNotContainPredicateFailMsg,
            userMessage);
        ThrowAssertFailed("Assert.DoesNotContain", finalMessage);
    }
}
