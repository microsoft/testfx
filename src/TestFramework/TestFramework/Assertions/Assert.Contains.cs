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

        internal TItem ComputeAssertion(string collectionExpression)
        {
            if (_builder is not null)
            {
                ThrowAssertContainsSingleFailed(_actualCount, _builder.ToString(), collectionExpression);
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

#pragma warning disable RS0026 // Do not add multiple public overloads with optional parameters

    /// <summary>
    /// Tests whether the specified collection contains exactly one element.
    /// </summary>
    /// <typeparam name="T">The type of the collection items.</typeparam>
    /// <param name="collection">The collection.</param>
    /// <param name="message">The message to display when the assertion fails.</param>
    /// <param name="collectionExpression">
    /// The syntactic expression of collection as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    /// <returns>The item.</returns>
#pragma warning disable IDE0060 // Remove unused parameter
    public static T ContainsSingle<T>(IEnumerable<T> collection, [InterpolatedStringHandlerArgument(nameof(collection))] ref AssertSingleInterpolatedStringHandler<T> message, [CallerArgumentExpression(nameof(collection))] string collectionExpression = "")
#pragma warning restore IDE0060 // Remove unused parameter
        => message.ComputeAssertion(collectionExpression);

#pragma warning disable RS0027 // API with optional parameter(s) should have the most parameters amongst its public overloads

    #region ContainsSingle

    /// <summary>
    /// Tests whether the specified collection contains exactly one element.
    /// </summary>
    /// <typeparam name="T">The type of the collection items.</typeparam>
    /// <param name="collection">The collection.</param>
    /// <param name="message">The message to display when the assertion fails.</param>
    /// <param name="collectionExpression">
    /// The syntactic expression of collection as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    /// <returns>The item.</returns>
    public static T ContainsSingle<T>(IEnumerable<T> collection, string? message = "", [CallerArgumentExpression(nameof(collection))] string collectionExpression = "")
        => ContainsSingle(static _ => true, collection, message, predicateExpression: string.Empty, collectionExpression);

    /// <summary>
    /// Tests whether the specified collection contains exactly one element.
    /// </summary>
    /// <param name="collection">The collection.</param>
    /// <param name="message">The message to display when the assertion fails.</param>
    /// <param name="collectionExpression">
    /// The syntactic expression of collection as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    /// <returns>The item.</returns>
    public static object? ContainsSingle(IEnumerable collection, string? message = "", [CallerArgumentExpression(nameof(collection))] string collectionExpression = "")
        => ContainsSingle(static _ => true, collection, message, predicateExpression: string.Empty, collectionExpression);

    /// <summary>
    /// Tests whether the specified collection contains exactly one element that matches the given predicate.
    /// </summary>
    /// <typeparam name="T">The type of the collection items.</typeparam>
    /// <param name="predicate">A function to test each element for a condition.</param>
    /// <param name="collection">The collection.</param>
    /// <param name="message">The message to display when the assertion fails.</param>
    /// <param name="predicateExpression">
    /// The syntactic expression of predicate as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    /// <param name="collectionExpression">
    /// The syntactic expression of collection as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    /// <returns>The item that matches the predicate.</returns>
    public static T ContainsSingle<T>(Func<T, bool> predicate, IEnumerable<T> collection, string? message = "", [CallerArgumentExpression(nameof(predicate))] string predicateExpression = "", [CallerArgumentExpression(nameof(collection))] string collectionExpression = "")
    {
        var matchingElements = collection.Where(predicate).ToList();
        int actualCount = matchingElements.Count;

        if (actualCount == 1)
        {
            return matchingElements[0];
        }

        if (string.IsNullOrEmpty(predicateExpression))
        {
            ThrowAssertContainsSingleFailed(actualCount, message, collectionExpression);
        }
        else
        {
            ThrowAssertSingleMatchFailed(actualCount, message, predicateExpression, collectionExpression);
        }

        // Unreachable code but compiler cannot work it out
        return default;
    }

    /// <summary>
    /// Tests whether the specified collection contains exactly one element that matches the given predicate.
    /// </summary>
    /// <param name="predicate">A function to test each element for a condition.</param>
    /// <param name="collection">The collection.</param>
    /// <param name="message">The message to display when the assertion fails.</param>
    /// <param name="predicateExpression">
    /// The syntactic expression of predicate as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    /// <param name="collectionExpression">
    /// The syntactic expression of collection as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    /// <returns>The item that matches the predicate.</returns>
    public static object? ContainsSingle(Func<object?, bool> predicate, IEnumerable collection, string? message = "", [CallerArgumentExpression(nameof(predicate))] string predicateExpression = "", [CallerArgumentExpression(nameof(collection))] string collectionExpression = "")
    {
        object? firstMatch = null;
        int matchCount = 0;

        foreach (object? item in collection)
        {
            if (!predicate(item))
            {
                continue;
            }

            if (matchCount == 0)
            {
                firstMatch = item;
            }

            matchCount++;

            // Early exit optimization - no need to continue if we already have more than one match
            if (matchCount > 1)
            {
                break;
            }
        }

        if (matchCount == 1)
        {
            return firstMatch;
        }

        if (string.IsNullOrEmpty(predicateExpression))
        {
            ThrowAssertContainsSingleFailed(matchCount, message, collectionExpression);
        }
        else
        {
            ThrowAssertSingleMatchFailed(matchCount, message, predicateExpression, collectionExpression);
        }

        return default;
    }

    #endregion // ContainsSingle

    #region Contains

    /// <summary>
    /// Tests whether the specified collection contains the given element.
    /// </summary>
    /// <typeparam name="T">The type of the collection items.</typeparam>
    /// <param name="expected">The expected item.</param>
    /// <param name="collection">The collection.</param>
    /// <param name="message">The message to display when the assertion fails.</param>
    /// <param name="expectedExpression">
    /// The syntactic expression of expected as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    /// <param name="collectionExpression">
    /// The syntactic expression of collection as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    public static void Contains<T>(T expected, IEnumerable<T> collection, string? message = "", [CallerArgumentExpression(nameof(expected))] string expectedExpression = "", [CallerArgumentExpression(nameof(collection))] string collectionExpression = "")
    {
        if (!collection.Contains(expected))
        {
            ThrowAssertContainsItemFailed(message, expectedExpression, collectionExpression, collection);
        }
    }

    /// <summary>
    /// Tests whether the specified collection contains the given element.
    /// </summary>
    /// <param name="expected">The expected item.</param>
    /// <param name="collection">The collection.</param>
    /// <param name="message">The message to display when the assertion fails.</param>
    /// <param name="expectedExpression">
    /// The syntactic expression of expected as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    /// <param name="collectionExpression">
    /// The syntactic expression of collection as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    public static void Contains(object? expected, IEnumerable collection, string? message = "", [CallerArgumentExpression(nameof(expected))] string expectedExpression = "", [CallerArgumentExpression(nameof(collection))] string collectionExpression = "")
    {
        CheckParameterNotNull(collection, "Assert.Contains", "collection");

        foreach (object? item in collection)
        {
            if (object.Equals(item, expected))
            {
                return;
            }
        }

        ThrowAssertContainsItemFailed(message, expectedExpression, collectionExpression, collection);
    }

    /// <summary>
    /// Tests whether the specified collection contains the given element.
    /// </summary>
    /// <typeparam name="T">The type of the collection items.</typeparam>
    /// <param name="expected">The expected item.</param>
    /// <param name="collection">The collection.</param>
    /// <param name="comparer">An equality comparer to compare values.</param>
    /// <param name="message">The message to display when the assertion fails.</param>
    /// <param name="expectedExpression">
    /// The syntactic expression of expected as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    /// <param name="collectionExpression">
    /// The syntactic expression of collection as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    public static void Contains<T>(T expected, IEnumerable<T> collection, IEqualityComparer<T> comparer, string? message = "", [CallerArgumentExpression(nameof(expected))] string expectedExpression = "", [CallerArgumentExpression(nameof(collection))] string collectionExpression = "")
    {
        if (!collection.Contains(expected, comparer))
        {
            ThrowAssertContainsItemFailed(message, expectedExpression, collectionExpression, collection);
        }
    }

    /// <summary>
    /// Tests whether the specified collection contains the given element.
    /// </summary>
    /// <param name="expected">The expected item.</param>
    /// <param name="collection">The collection.</param>
    /// <param name="comparer">An equality comparer to compare values.</param>
    /// <param name="message">The message to display when the assertion fails.</param>
    /// <param name="expectedExpression">
    /// The syntactic expression of expected as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    /// <param name="collectionExpression">
    /// The syntactic expression of collection as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    public static void Contains(object? expected, IEnumerable collection, IEqualityComparer comparer, string? message = "", [CallerArgumentExpression(nameof(expected))] string expectedExpression = "", [CallerArgumentExpression(nameof(collection))] string collectionExpression = "")
    {
        CheckParameterNotNull(collection, "Assert.Contains", "collection");
        CheckParameterNotNull(comparer, "Assert.Contains", "comparer");

        foreach (object? item in collection)
        {
            if (comparer.Equals(item, expected))
            {
                return;
            }
        }

        ThrowAssertContainsItemFailed(message, expectedExpression, collectionExpression, collection);
    }

    /// <summary>
    /// Tests whether the specified collection contains the given element.
    /// </summary>
    /// <typeparam name="T">The type of the collection items.</typeparam>
    /// <param name="predicate">A function to test each element for a condition.</param>
    /// <param name="collection">The collection.</param>
    /// <param name="message">The message to display when the assertion fails.</param>
    /// <param name="predicateExpression">
    /// The syntactic expression of predicate as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    /// <param name="collectionExpression">
    /// The syntactic expression of collection as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    public static void Contains<T>(Func<T, bool> predicate, IEnumerable<T> collection, string? message = "", [CallerArgumentExpression(nameof(predicate))] string predicateExpression = "", [CallerArgumentExpression(nameof(collection))] string collectionExpression = "")
    {
        if (!collection.Any(predicate))
        {
            ThrowAssertContainsPredicateFailed(message, predicateExpression, collectionExpression, collection);
        }
    }

    /// <summary>
    /// Tests whether the specified collection contains the given element.
    /// </summary>
    /// <param name="predicate">A function to test each element for a condition.</param>
    /// <param name="collection">The collection.</param>
    /// <param name="message">The message to display when the assertion fails.</param>
    /// <param name="predicateExpression">
    /// The syntactic expression of predicate as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    /// <param name="collectionExpression">
    /// The syntactic expression of collection as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    public static void Contains(Func<object?, bool> predicate, IEnumerable collection, string? message = "", [CallerArgumentExpression(nameof(predicate))] string predicateExpression = "", [CallerArgumentExpression(nameof(collection))] string collectionExpression = "")
    {
        CheckParameterNotNull(collection, "Assert.Contains", "collection");
        CheckParameterNotNull(predicate, "Assert.Contains", "predicate");

        foreach (object? item in collection)
        {
            if (predicate(item))
            {
                return;
            }
        }

        ThrowAssertContainsPredicateFailed(message, predicateExpression, collectionExpression, collection);
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
    /// <param name="message">
    /// The message to include in the exception when <paramref name="substring"/>
    /// is not in <paramref name="value"/>. The message is shown in
    /// test results.
    /// </param>
    /// <param name="substringExpression">
    /// The syntactic expression of substring as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    /// <param name="valueExpression">
    /// The syntactic expression of value as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// <paramref name="value"/> is null, or <paramref name="substring"/> is null,
    /// or <paramref name="value"/> does not contain <paramref name="substring"/>.
    /// </exception>
    public static void Contains(string substring, string value, string? message = "", [CallerArgumentExpression(nameof(substring))] string substringExpression = "", [CallerArgumentExpression(nameof(value))] string valueExpression = "")
        => Contains(substring, value, StringComparison.Ordinal, message, substringExpression, valueExpression);

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
    /// <param name="substringExpression">
    /// The syntactic expression of substring as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    /// <param name="valueExpression">
    /// The syntactic expression of value as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// <paramref name="value"/> is null, or <paramref name="substring"/> is null,
    /// or <paramref name="value"/> does not contain <paramref name="substring"/>.
    /// </exception>
    public static void Contains(string substring, string value, StringComparison comparisonType, string? message = "", [CallerArgumentExpression(nameof(substring))] string substringExpression = "", [CallerArgumentExpression(nameof(value))] string valueExpression = "")
    {
        CheckParameterNotNull(value, "Assert.Contains", "value");
        CheckParameterNotNull(substring, "Assert.Contains", "substring");

        if (!value.Contains(substring, comparisonType))
        {
            ThrowAssertStringContainsFailed(value, substring, message, substringExpression, valueExpression);
        }
    }

    #endregion // Contains

    #region DoesNotContain

    /// <summary>
    /// Tests whether the specified collection does not contain the specified item.
    /// </summary>
    /// <typeparam name="T">The type of the collection items.</typeparam>
    /// <param name="notExpected">The unexpected item.</param>
    /// <param name="collection">The collection.</param>
    /// <param name="message">The message to display when the assertion fails.</param>
    /// <param name="notExpectedExpression">
    /// The syntactic expression of notExpected as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    /// <param name="collectionExpression">
    /// The syntactic expression of collection as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    public static void DoesNotContain<T>(T notExpected, IEnumerable<T> collection, string? message = "", [CallerArgumentExpression(nameof(notExpected))] string notExpectedExpression = "", [CallerArgumentExpression(nameof(collection))] string collectionExpression = "")
    {
        if (collection.Contains(notExpected))
        {
            ThrowAssertDoesNotContainItemFailed(message, notExpectedExpression, collectionExpression, collection);
        }
    }

    /// <summary>
    /// Tests whether the specified collection does not contain the specified item.
    /// </summary>
    /// <param name="notExpected">The unexpected item.</param>
    /// <param name="collection">The collection.</param>
    /// <param name="message">The message to display when the assertion fails.</param>
    /// <param name="notExpectedExpression">
    /// The syntactic expression of notExpected as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    /// <param name="collectionExpression">
    /// The syntactic expression of collection as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    public static void DoesNotContain(object? notExpected, IEnumerable collection, string? message = "", [CallerArgumentExpression(nameof(notExpected))] string notExpectedExpression = "", [CallerArgumentExpression(nameof(collection))] string collectionExpression = "")
    {
        CheckParameterNotNull(collection, "Assert.DoesNotContain", "collection");

        foreach (object? item in collection)
        {
            if (object.Equals(notExpected, item))
            {
                ThrowAssertDoesNotContainItemFailed(message, notExpectedExpression, collectionExpression, collection);
            }
        }
    }

    /// <summary>
    /// Tests whether the specified collection does not contain the specified item.
    /// </summary>
    /// <typeparam name="T">The type of the collection items.</typeparam>
    /// <param name="notExpected">The unexpected item.</param>
    /// <param name="collection">The collection.</param>
    /// <param name="comparer">An equality comparer to compare values.</param>
    /// <param name="message">The message to display when the assertion fails.</param>
    /// <param name="notExpectedExpression">
    /// The syntactic expression of notExpected as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    /// <param name="collectionExpression">
    /// The syntactic expression of collection as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    public static void DoesNotContain<T>(T notExpected, IEnumerable<T> collection, IEqualityComparer<T> comparer, string? message = "", [CallerArgumentExpression(nameof(notExpected))] string notExpectedExpression = "", [CallerArgumentExpression(nameof(collection))] string collectionExpression = "")
    {
        if (collection.Contains(notExpected, comparer))
        {
            ThrowAssertDoesNotContainItemFailed(message, notExpectedExpression, collectionExpression, collection);
        }
    }

    /// <summary>
    /// Tests whether the specified collection does not contain the specified item.
    /// </summary>
    /// <param name="notExpected">The unexpected item.</param>
    /// <param name="collection">The collection.</param>
    /// <param name="comparer">An equality comparer to compare values.</param>
    /// <param name="message">The message to display when the assertion fails.</param>
    /// <param name="notExpectedExpression">
    /// The syntactic expression of notExpected as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    /// <param name="collectionExpression">
    /// The syntactic expression of collection as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    public static void DoesNotContain(object? notExpected, IEnumerable collection, IEqualityComparer comparer, string? message = "", [CallerArgumentExpression(nameof(notExpected))] string notExpectedExpression = "", [CallerArgumentExpression(nameof(collection))] string collectionExpression = "")
    {
        CheckParameterNotNull(collection, "Assert.DoesNotContain", "collection");
        CheckParameterNotNull(comparer, "Assert.DoesNotContain", "comparer");

        foreach (object? item in collection)
        {
            if (comparer.Equals(item, notExpected))
            {
                ThrowAssertDoesNotContainItemFailed(message, notExpectedExpression, collectionExpression, collection);
            }
        }
    }

    /// <summary>
    /// Tests whether the specified collection does not contain the specified item.
    /// </summary>
    /// <typeparam name="T">The type of the collection items.</typeparam>
    /// <param name="predicate">A function to test each element for a condition.</param>
    /// <param name="collection">The collection.</param>
    /// <param name="message">The message to display when the assertion fails.</param>
    /// <param name="predicateExpression">
    /// The syntactic expression of predicate as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    /// <param name="collectionExpression">
    /// The syntactic expression of collection as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    public static void DoesNotContain<T>(Func<T, bool> predicate, IEnumerable<T> collection, string? message = "", [CallerArgumentExpression(nameof(predicate))] string predicateExpression = "", [CallerArgumentExpression(nameof(collection))] string collectionExpression = "")
    {
        if (collection.Any(predicate))
        {
            ThrowAssertDoesNotContainPredicateFailed(message, predicateExpression, collectionExpression, collection);
        }
    }

    /// <summary>
    /// Tests whether the specified collection does not contain the specified item.
    /// </summary>
    /// <param name="predicate">A function to test each element for a condition.</param>
    /// <param name="collection">The collection.</param>
    /// <param name="message">The message to display when the assertion fails.</param>
    /// <param name="predicateExpression">
    /// The syntactic expression of predicate as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    /// <param name="collectionExpression">
    /// The syntactic expression of collection as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    public static void DoesNotContain(Func<object?, bool> predicate, IEnumerable collection, string? message = "", [CallerArgumentExpression(nameof(predicate))] string predicateExpression = "", [CallerArgumentExpression(nameof(collection))] string collectionExpression = "")
    {
        CheckParameterNotNull(collection, "Assert.DoesNotContain", "collection");
        CheckParameterNotNull(predicate, "Assert.DoesNotContain", "predicate");

        foreach (object? item in collection)
        {
            if (predicate(item))
            {
                ThrowAssertDoesNotContainPredicateFailed(message, predicateExpression, collectionExpression, collection);
            }
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
    /// <param name="message">
    /// The message to include in the exception when <paramref name="substring"/>
    /// is in <paramref name="value"/>. The message is shown in
    /// test results.
    /// </param>
    /// <param name="substringExpression">
    /// The syntactic expression of substring as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    /// <param name="valueExpression">
    /// The syntactic expression of value as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// <paramref name="value"/> is null, or <paramref name="substring"/> is null,
    /// or <paramref name="value"/> contains <paramref name="substring"/>.
    /// </exception>
    public static void DoesNotContain(string substring, string value, string? message = "", [CallerArgumentExpression(nameof(substring))] string substringExpression = "", [CallerArgumentExpression(nameof(value))] string valueExpression = "")
        => DoesNotContain(substring, value, StringComparison.Ordinal, message, substringExpression, valueExpression);

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
    /// <param name="substringExpression">
    /// The syntactic expression of substring as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    /// <param name="valueExpression">
    /// The syntactic expression of value as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// <paramref name="value"/> is null, or <paramref name="substring"/> is null,
    /// or <paramref name="value"/> contains <paramref name="substring"/>.
    /// </exception>
    public static void DoesNotContain(string substring, string value, StringComparison comparisonType, string? message = "", [CallerArgumentExpression(nameof(substring))] string substringExpression = "", [CallerArgumentExpression(nameof(value))] string valueExpression = "")
    {
        CheckParameterNotNull(value, "Assert.DoesNotContain", "value");
        CheckParameterNotNull(substring, "Assert.DoesNotContain", "substring");

        if (value.Contains(substring, comparisonType))
        {
            ThrowAssertStringDoesNotContainFailed(value, substring, message, substringExpression, valueExpression);
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
    /// <param name="minValueExpression">
    /// The syntactic expression of minValue as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    /// <param name="maxValueExpression">
    /// The syntactic expression of maxValue as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    /// <param name="valueExpression">
    /// The syntactic expression of value as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    public static void IsInRange<T>(T minValue, T maxValue, T value, string? message = "", [CallerArgumentExpression(nameof(minValue))] string minValueExpression = "", [CallerArgumentExpression(nameof(maxValue))] string maxValueExpression = "", [CallerArgumentExpression(nameof(value))] string valueExpression = "")
        where T : struct, IComparable<T>
    {
        if (maxValue.CompareTo(minValue) < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxValue), FrameworkMessages.IsInRangeMaxValueMustBeGreaterThanOrEqualMinValue);
        }

        if (value.CompareTo(minValue) < 0 || value.CompareTo(maxValue) > 0)
        {
            ThrowAssertIsInRangeFailed(value, minValue, maxValue, message, minValueExpression, maxValueExpression, valueExpression);
        }
    }

    #endregion // IsInRange

    [DoesNotReturn]
    private static void ThrowAssertSingleMatchFailed(int actualCount, string? userMessage, string predicateExpression, string collectionExpression)
    {
        string message = string.IsNullOrEmpty(userMessage) ? string.Empty : userMessage!;
        message += Environment.NewLine + string.Format(CultureInfo.CurrentCulture, FrameworkMessages.ContainsSingleMatchFailNew, actualCount);
        message += FormatExpressionParameter("predicate", predicateExpression)
                 + FormatExpressionParameter("collection", collectionExpression);
        ThrowAssertFailed("Assert.ContainsSingle", message);
    }

    [DoesNotReturn]
    private static void ThrowAssertContainsSingleFailed(int actualCount, string? userMessage, string collectionExpression)
    {
        string message = string.IsNullOrEmpty(userMessage) ? string.Empty : userMessage!;
        message += Environment.NewLine + string.Format(CultureInfo.CurrentCulture, FrameworkMessages.ContainsSingleFailNew, actualCount);
        message += FormatExpressionParameter("collection", collectionExpression);
        ThrowAssertFailed("Assert.ContainsSingle", message);
    }

    [DoesNotReturn]
    private static void ThrowAssertContainsItemFailed(string? userMessage, string expectedExpression, string collectionExpression, IEnumerable? collectionValue = null)
    {
        string message = string.IsNullOrEmpty(userMessage) ? string.Empty : userMessage!;
        message += Environment.NewLine + FrameworkMessages.ContainsItemFailNew;
        message += FormatExpressionParameter("expected", expectedExpression);
        if (collectionValue is not null)
        {
            message += FormatCollectionParameter("collection", collectionExpression, collectionValue);
        }
        else
        {
            message += FormatExpressionParameter("collection", collectionExpression);
        }

        ThrowAssertFailed("Assert.Contains", message);
    }

    [DoesNotReturn]
    private static void ThrowAssertContainsPredicateFailed(string? userMessage, string predicateExpression, string collectionExpression, IEnumerable? collectionValue = null)
    {
        string message = string.IsNullOrEmpty(userMessage) ? string.Empty : userMessage!;
        message += Environment.NewLine + FrameworkMessages.ContainsPredicateFailNew;
        message += FormatExpressionParameter("predicate", predicateExpression);
        if (collectionValue is not null)
        {
            message += FormatCollectionParameter("collection", collectionExpression, collectionValue);
        }
        else
        {
            message += FormatExpressionParameter("collection", collectionExpression);
        }

        ThrowAssertFailed("Assert.Contains", message);
    }

    [DoesNotReturn]
    private static void ThrowAssertDoesNotContainItemFailed(string? userMessage, string notExpectedExpression, string collectionExpression, IEnumerable? collectionValue = null)
    {
        string message = string.IsNullOrEmpty(userMessage) ? string.Empty : userMessage!;
        message += Environment.NewLine + FrameworkMessages.DoesNotContainItemFailNew;
        message += FormatExpressionParameter("notExpected", notExpectedExpression);
        if (collectionValue is not null)
        {
            message += FormatCollectionParameter("collection", collectionExpression, collectionValue);
        }
        else
        {
            message += FormatExpressionParameter("collection", collectionExpression);
        }

        ThrowAssertFailed("Assert.DoesNotContain", message);
    }

    [DoesNotReturn]
    private static void ThrowAssertDoesNotContainPredicateFailed(string? userMessage, string predicateExpression, string collectionExpression, IEnumerable? collectionValue = null)
    {
        string message = string.IsNullOrEmpty(userMessage) ? string.Empty : userMessage!;
        message += Environment.NewLine + FrameworkMessages.DoesNotContainPredicateFailNew;
        message += FormatExpressionParameter("predicate", predicateExpression);
        if (collectionValue is not null)
        {
            message += FormatCollectionParameter("collection", collectionExpression, collectionValue);
        }
        else
        {
            message += FormatExpressionParameter("collection", collectionExpression);
        }

        ThrowAssertFailed("Assert.DoesNotContain", message);
    }

    [DoesNotReturn]
    private static void ThrowAssertStringContainsFailed(string value, string substring, string? userMessage, string substringExpression, string valueExpression)
    {
        string message = string.IsNullOrEmpty(userMessage) ? string.Empty : userMessage!;
        message += Environment.NewLine + FrameworkMessages.ContainsStringFailNew;
        message += Environment.NewLine + FormatParameter(nameof(substring), substringExpression, substring)
                 + Environment.NewLine + FormatParameter(nameof(value), valueExpression, value);
        ThrowAssertFailed("Assert.Contains", message);
    }

    [DoesNotReturn]
    private static void ThrowAssertStringDoesNotContainFailed(string value, string substring, string? userMessage, string substringExpression, string valueExpression)
    {
        string message = string.IsNullOrEmpty(userMessage) ? string.Empty : userMessage!;
        message += Environment.NewLine + FrameworkMessages.DoesNotContainStringFailNew;
        message += Environment.NewLine + FormatParameter(nameof(substring), substringExpression, substring)
                 + Environment.NewLine + FormatParameter(nameof(value), valueExpression, value);
        ThrowAssertFailed("Assert.DoesNotContain", message);
    }

    [DoesNotReturn]
    private static void ThrowAssertIsInRangeFailed<T>(T value, T minValue, T maxValue, string? userMessage, string minValueExpression, string maxValueExpression, string valueExpression)
    {
        string message = string.IsNullOrEmpty(userMessage) ? string.Empty : userMessage!;
        message += Environment.NewLine + FrameworkMessages.IsInRangeFailNew;
        message += Environment.NewLine + FormatParameter(nameof(minValue), minValueExpression, minValue)
                 + Environment.NewLine + FormatParameter(nameof(maxValue), maxValueExpression, maxValue)
                 + Environment.NewLine + FormatParameter(nameof(value), valueExpression, value);
        ThrowAssertFailed("Assert.IsInRange", message);
    }
}
