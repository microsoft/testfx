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
                ReportAssertContainsSingleFailed(_actualCount, _builder.ToString(), collectionExpression);
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
        T firstMatch = default!;
        int matchCount = 0;

        foreach (T item in collection)
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
        }

        if (matchCount == 1)
        {
            return firstMatch;
        }

        if (string.IsNullOrEmpty(predicateExpression))
        {
            ReportAssertContainsSingleFailed(matchCount, message, collectionExpression);
        }
        else
        {
            ReportAssertSingleMatchFailed(matchCount, message, predicateExpression, collectionExpression);
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
            ReportAssertContainsSingleFailed(matchCount, message, collectionExpression);
        }
        else
        {
            ReportAssertSingleMatchFailed(matchCount, message, predicateExpression, collectionExpression);
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
            ReportAssertContainsItemFailed(expected, message, expectedExpression, collectionExpression);
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

        ReportAssertContainsItemFailed(expected, message, expectedExpression, collectionExpression);
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
            ReportAssertContainsItemFailed(expected, message, expectedExpression, collectionExpression, comparer);
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

        ReportAssertContainsItemFailed(expected, message, expectedExpression, collectionExpression, comparer);
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
            ReportAssertContainsPredicateFailed(message, predicateExpression, collectionExpression);
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

        ReportAssertContainsPredicateFailed(message, predicateExpression, collectionExpression);
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

#if NETCOREAPP
        if (!value.Contains(substring, comparisonType))
#else
        if (value.IndexOf(substring, comparisonType) < 0)
#endif
        {
            ReportAssertContainsSubstringFailed(substring, value, comparisonType, message, substringExpression, valueExpression);
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
            ReportAssertDoesNotContainItemFailed(notExpected, message, notExpectedExpression, collectionExpression);
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
                ReportAssertDoesNotContainItemFailed(notExpected, message, notExpectedExpression, collectionExpression);
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
            ReportAssertDoesNotContainItemFailed(notExpected, message, notExpectedExpression, collectionExpression, comparer);
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
                ReportAssertDoesNotContainItemFailed(notExpected, message, notExpectedExpression, collectionExpression, comparer);
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
            ReportAssertDoesNotContainPredicateFailed(message, predicateExpression, collectionExpression);
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
                ReportAssertDoesNotContainPredicateFailed(message, predicateExpression, collectionExpression);
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

#if NETCOREAPP
        if (value.Contains(substring, comparisonType))
#else
        if (value.IndexOf(substring, comparisonType) >= 0)
#endif
        {
            ReportAssertDoesNotContainSubstringFailed(substring, value, comparisonType, message, substringExpression, valueExpression);
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
            ReportAssertIsInRangeFailed(minValue, maxValue, value, message, minValueExpression, maxValueExpression, valueExpression);
        }
    }

    #endregion // IsInRange

    [DoesNotReturn]
    private static void ReportAssertContainsSingleFailed(int actualCount, string? userMessage, string collectionExpression)
    {
        EvidenceBlock evidence = EvidenceBlock.Create()
            .AddLine("expected count:", "1")
            .AddLine("actual count:", actualCount.ToString(CultureInfo.CurrentCulture));

        StructuredAssertionMessage structured = new(FrameworkMessages.ContainsSingleFailedSummary);
        structured.WithUserMessage(userMessage);
        structured.WithEvidence(evidence);
        structured.WithExpectedAndActual("1", actualCount.ToString(CultureInfo.CurrentCulture));
        structured.WithCallSiteExpression(FormatCallSiteExpression("Assert.ContainsSingle", collectionExpression, "<collection>"));

        ReportAssertFailed(structured);
    }

    [DoesNotReturn]
    private static void ReportAssertSingleMatchFailed(int actualCount, string? userMessage, string predicateExpression, string collectionExpression)
    {
        EvidenceBlock evidence = EvidenceBlock.Create()
            .AddLine("expected matches:", "1")
            .AddLine("actual matches:", actualCount.ToString(CultureInfo.CurrentCulture));

        StructuredAssertionMessage structured = new(FrameworkMessages.ContainsSingleMatchFailedSummary);
        structured.WithUserMessage(userMessage);
        structured.WithEvidence(evidence);
        structured.WithExpectedAndActual("1", actualCount.ToString(CultureInfo.CurrentCulture));
        structured.WithCallSiteExpression(FormatCallSiteExpression("Assert.ContainsSingle", predicateExpression, collectionExpression, "<predicate>", "<collection>"));

        ReportAssertFailed(structured);
    }

    [DoesNotReturn]
    private static void ReportAssertContainsItemFailed(object? expected, string? userMessage, string expectedExpression, string collectionExpression, object? comparer = null)
    {
        string expectedText = AssertionValueRenderer.RenderValue(expected);
        EvidenceBlock evidence = EvidenceBlock.Create().AddLine("expected:", expectedText);
        if (comparer is not null)
        {
            evidence.AddLine("comparer:", comparer.GetType().Name);
        }

        StructuredAssertionMessage structured = new(FrameworkMessages.ContainsItemFailedSummary);
        structured.WithUserMessage(userMessage);
        structured.WithEvidence(evidence);
        structured.WithExpectedAndActual(expectedText, null);
        structured.WithCallSiteExpression(FormatCallSiteExpression("Assert.Contains", expectedExpression, collectionExpression, "<expected>", "<collection>"));

        ReportAssertFailed(structured);
    }

    [DoesNotReturn]
    private static void ReportAssertContainsPredicateFailed(string? userMessage, string predicateExpression, string collectionExpression)
    {
        StructuredAssertionMessage structured = new(FrameworkMessages.ContainsPredicateFailedSummary);
        structured.WithUserMessage(userMessage);
        structured.WithCallSiteExpression(FormatCallSiteExpression("Assert.Contains", predicateExpression, collectionExpression, "<predicate>", "<collection>"));

        ReportAssertFailed(structured);
    }

    [DoesNotReturn]
    private static void ReportAssertContainsSubstringFailed(string substring, string value, StringComparison comparisonType, string? userMessage, string substringExpression, string valueExpression)
    {
        string expectedText = AssertionValueRenderer.RenderValue(substring);
        string actualText = AssertionValueRenderer.RenderValue(value);
        EvidenceBlock evidence = EvidenceBlock.Create()
            .AddLine("expected substring:", expectedText)
            .AddLine("actual:", actualText)
            .AddLine("comparison:", comparisonType.ToString());

        StructuredAssertionMessage structured = new(FrameworkMessages.ContainsSubstringFailedSummary);
        structured.WithUserMessage(userMessage);
        structured.WithEvidence(evidence);
        structured.WithExpectedAndActual(expectedText, actualText);
        structured.WithCallSiteExpression(FormatCallSiteExpression("Assert.Contains", substringExpression, valueExpression, "<substring>", "<value>"));

        ReportAssertFailed(structured);
    }

    [DoesNotReturn]
    private static void ReportAssertDoesNotContainItemFailed(object? notExpected, string? userMessage, string notExpectedExpression, string collectionExpression, object? comparer = null)
    {
        string notExpectedText = AssertionValueRenderer.RenderValue(notExpected);
        EvidenceBlock evidence = EvidenceBlock.Create().AddLine("unexpected:", notExpectedText);
        if (comparer is not null)
        {
            evidence.AddLine("comparer:", comparer.GetType().Name);
        }

        StructuredAssertionMessage structured = new(FrameworkMessages.DoesNotContainItemFailedSummary);
        structured.WithUserMessage(userMessage);
        structured.WithEvidence(evidence);
        structured.WithExpectedAndActual(notExpectedText, null);
        structured.WithCallSiteExpression(FormatCallSiteExpression("Assert.DoesNotContain", notExpectedExpression, collectionExpression, "<notExpected>", "<collection>"));

        ReportAssertFailed(structured);
    }

    [DoesNotReturn]
    private static void ReportAssertDoesNotContainPredicateFailed(string? userMessage, string predicateExpression, string collectionExpression)
    {
        StructuredAssertionMessage structured = new(FrameworkMessages.DoesNotContainPredicateFailedSummary);
        structured.WithUserMessage(userMessage);
        structured.WithCallSiteExpression(FormatCallSiteExpression("Assert.DoesNotContain", predicateExpression, collectionExpression, "<predicate>", "<collection>"));

        ReportAssertFailed(structured);
    }

    [DoesNotReturn]
    private static void ReportAssertDoesNotContainSubstringFailed(string substring, string value, StringComparison comparisonType, string? userMessage, string substringExpression, string valueExpression)
    {
        string notExpectedText = AssertionValueRenderer.RenderValue(substring);
        string actualText = AssertionValueRenderer.RenderValue(value);
        EvidenceBlock evidence = EvidenceBlock.Create()
            .AddLine("unexpected substring:", notExpectedText)
            .AddLine("actual:", actualText)
            .AddLine("comparison:", comparisonType.ToString());

        StructuredAssertionMessage structured = new(FrameworkMessages.DoesNotContainSubstringFailedSummary);
        structured.WithUserMessage(userMessage);
        structured.WithEvidence(evidence);
        structured.WithExpectedAndActual(notExpectedText, actualText);
        structured.WithCallSiteExpression(FormatCallSiteExpression("Assert.DoesNotContain", substringExpression, valueExpression, "<substring>", "<value>"));

        ReportAssertFailed(structured);
    }

    [DoesNotReturn]
    private static void ReportAssertIsInRangeFailed<T>(T minValue, T maxValue, T value, string? userMessage, string minValueExpression, string maxValueExpression, string valueExpression)
    {
        string minText = AssertionValueRenderer.RenderValue(minValue);
        string maxText = AssertionValueRenderer.RenderValue(maxValue);
        string actualText = AssertionValueRenderer.RenderValue(value);
        EvidenceBlock evidence = EvidenceBlock.Create()
            .AddLine("expected:", $"[{minText}, {maxText}]")
            .AddLine("actual:", actualText);

        StructuredAssertionMessage structured = new(FrameworkMessages.IsInRangeFailedSummary);
        structured.WithUserMessage(userMessage);
        structured.WithEvidence(evidence);
        structured.WithExpectedAndActual($"[{minText}, {maxText}]", actualText);
        // The middle "value" arg comes after minValue, maxValue — keep all three in the call-site for clarity.
        string? callSite = FormatIsInRangeCallSite(minValueExpression, maxValueExpression, valueExpression);
        structured.WithCallSiteExpression(callSite);

        ReportAssertFailed(structured);
    }

    private static string? FormatIsInRangeCallSite(string minValueExpression, string maxValueExpression, string valueExpression)
    {
        bool emptyMin = string.IsNullOrWhiteSpace(minValueExpression);
        bool emptyMax = string.IsNullOrWhiteSpace(maxValueExpression);
        bool emptyValue = string.IsNullOrWhiteSpace(valueExpression);
        if (emptyMin && emptyMax && emptyValue)
        {
            return null;
        }

        string minArg = emptyMin || ContainsLineBreak(minValueExpression) ? "<minValue>" : minValueExpression;
        string maxArg = emptyMax || ContainsLineBreak(maxValueExpression) ? "<maxValue>" : maxValueExpression;
        string valueArg = emptyValue || ContainsLineBreak(valueExpression) ? "<value>" : valueExpression;
        return $"Assert.IsInRange({minArg}, {maxArg}, {valueArg})";
    }

    private static bool ContainsLineBreak(string s)
        => s.IndexOf('\n') >= 0 || s.IndexOf('\r') >= 0;
}
