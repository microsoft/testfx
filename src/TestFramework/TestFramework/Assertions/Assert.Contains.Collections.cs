// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestTools.UnitTesting;

#pragma warning disable RS0026 // Do not add multiple public overloads with optional parameters
#pragma warning disable RS0027 // API with optional parameter(s) should have the most parameters amongst its public overloads
public sealed partial class Assert
{
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
        TelemetryCollector.TrackAssertionCall("Assert.Contains");
        ContainsCore(expected, collection, "Assert.Contains", message, expectedExpression, collectionExpression, shouldContain: true);
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
        TelemetryCollector.TrackAssertionCall("Assert.Contains");
        ContainsCore(expected, collection, "Assert.Contains", message, expectedExpression, collectionExpression, shouldContain: true);
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
        TelemetryCollector.TrackAssertionCall("Assert.Contains");
        ContainsCore(expected, collection, comparer, "Assert.Contains", message, expectedExpression, collectionExpression, shouldContain: true);
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
        TelemetryCollector.TrackAssertionCall("Assert.Contains");
        ContainsCore(expected, collection, comparer, "Assert.Contains", message, expectedExpression, collectionExpression, shouldContain: true);
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
        TelemetryCollector.TrackAssertionCall("Assert.Contains");
        ContainsCore(predicate, collection, "Assert.Contains", message, predicateExpression, collectionExpression, shouldContain: true);
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
        TelemetryCollector.TrackAssertionCall("Assert.Contains");
        ContainsCore(predicate, collection, "Assert.Contains", message, predicateExpression, collectionExpression, shouldContain: true);
    }

#if NETCOREAPP3_1_OR_GREATER

    /// <summary>
    /// Tests whether the specified array contains the given element.
    /// </summary>
    /// <typeparam name="T">The type of the array items.</typeparam>
    /// <param name="expected">The expected item.</param>
    /// <param name="collection">The array.</param>
    /// <param name="message">The message to display when the assertion fails.</param>
    /// <param name="expectedExpression">The syntactic expression of expected as given by the compiler via caller argument expression.</param>
    /// <param name="collectionExpression">The syntactic expression of collection as given by the compiler via caller argument expression.</param>
    public static void Contains<T>(T expected, T[] collection, string? message = "", [CallerArgumentExpression(nameof(expected))] string expectedExpression = "", [CallerArgumentExpression(nameof(collection))] string collectionExpression = "")
        => Contains(expected, (IEnumerable<T>)collection, message, expectedExpression, collectionExpression);

    /// <summary>
    /// Tests whether the specified array contains the given element using the specified equality comparer.
    /// </summary>
    /// <typeparam name="T">The type of the array items.</typeparam>
    /// <param name="expected">The expected item.</param>
    /// <param name="collection">The array.</param>
    /// <param name="comparer">An equality comparer to compare values.</param>
    /// <param name="message">The message to display when the assertion fails.</param>
    /// <param name="expectedExpression">The syntactic expression of expected as given by the compiler via caller argument expression.</param>
    /// <param name="collectionExpression">The syntactic expression of collection as given by the compiler via caller argument expression.</param>
    public static void Contains<T>(T expected, T[] collection, IEqualityComparer<T> comparer, string? message = "", [CallerArgumentExpression(nameof(expected))] string expectedExpression = "", [CallerArgumentExpression(nameof(collection))] string collectionExpression = "")
        => Contains(expected, (IEnumerable<T>)collection, comparer, message, expectedExpression, collectionExpression);

    /// <summary>
    /// Tests whether the specified array contains an element that matches the given predicate.
    /// </summary>
    /// <typeparam name="T">The type of the array items.</typeparam>
    /// <param name="predicate">A function to test each element for a condition.</param>
    /// <param name="collection">The array.</param>
    /// <param name="message">The message to display when the assertion fails.</param>
    /// <param name="predicateExpression">The syntactic expression of predicate as given by the compiler via caller argument expression.</param>
    /// <param name="collectionExpression">The syntactic expression of collection as given by the compiler via caller argument expression.</param>
    public static void Contains<T>(Func<T, bool> predicate, T[] collection, string? message = "", [CallerArgumentExpression(nameof(predicate))] string predicateExpression = "", [CallerArgumentExpression(nameof(collection))] string collectionExpression = "")
        => Contains(predicate, (IEnumerable<T>)collection, message, predicateExpression, collectionExpression);

    /// <summary>
    /// Tests whether the specified collection contains the given element.
    /// </summary>
    /// <typeparam name="T">The type of the collection items.</typeparam>
    /// <typeparam name="TCollection">The concrete type of the collection.</typeparam>
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
    /// <remarks>The collection may contain null elements even when <paramref name="expected"/> is not null.</remarks>
    public static void Contains<T, TCollection>(T expected, TCollection collection, string? message = "", [CallerArgumentExpression(nameof(expected))] string expectedExpression = "", [CallerArgumentExpression(nameof(collection))] string collectionExpression = "")
        where TCollection : IEnumerable<T?>
        => Contains(expected, (IEnumerable<T?>)collection, message, expectedExpression, collectionExpression);

    /// <summary>
    /// Tests whether the specified collection contains the given element.
    /// </summary>
    /// <typeparam name="T">The type of the collection items.</typeparam>
    /// <typeparam name="TCollection">The concrete type of the collection.</typeparam>
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
    /// <remarks>The collection may contain null elements even when <paramref name="expected"/> is not null.</remarks>
    public static void Contains<T, TCollection>(T expected, TCollection collection, IEqualityComparer<T> comparer, string? message = "", [CallerArgumentExpression(nameof(expected))] string expectedExpression = "", [CallerArgumentExpression(nameof(collection))] string collectionExpression = "")
        where TCollection : IEnumerable<T?>
        // Equality comparers accept null operands. Preserve T for compatibility with non-nullable comparers.
        => Contains(expected, (IEnumerable<T>)collection, comparer, message, expectedExpression, collectionExpression);

    /// <summary>
    /// Tests whether the specified collection contains an element that matches the given predicate.
    /// </summary>
    /// <typeparam name="T">The type of the collection items.</typeparam>
    /// <typeparam name="TCollection">The concrete type of the collection.</typeparam>
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
    public static void Contains<T, TCollection>(Func<T, bool> predicate, TCollection collection, string? message = "", [CallerArgumentExpression(nameof(predicate))] string predicateExpression = "", [CallerArgumentExpression(nameof(collection))] string collectionExpression = "")
        where TCollection : IEnumerable<T>
        => Contains(predicate, (IEnumerable<T>)collection, message, predicateExpression, collectionExpression);

#endif

    #endregion // Contains
}
