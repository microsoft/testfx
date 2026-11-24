// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestTools.UnitTesting;

#if NETCOREAPP3_1_OR_GREATER

#pragma warning disable RS0026 // Do not add multiple public overloads with optional parameters
#pragma warning disable RS0027 // API with optional parameter(s) should have the most parameters amongst its public overloads
public sealed partial class Assert
{
    #region ContainsAll span/memory

    /// <summary>
    /// Tests whether <paramref name="collection"/> contains every element of <paramref name="expected"/> (with multiplicity).
    /// </summary>
    /// <typeparam name="T">The type of the collection items.</typeparam>
    /// <param name="expected">The span of items expected to all be present in <paramref name="collection"/>.</param>
    /// <param name="collection">The span expected to contain every item of <paramref name="expected"/>.</param>
    /// <param name="message">The message to include in the exception when the assertion fails.</param>
    /// <param name="expectedExpression">
    /// The syntactic expression of expected as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    /// <param name="collectionExpression">
    /// The syntactic expression of collection as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    public static void ContainsAll<T>(ReadOnlySpan<T> expected, ReadOnlySpan<T> collection, string? message = "", [CallerArgumentExpression(nameof(expected))] string expectedExpression = "", [CallerArgumentExpression(nameof(collection))] string collectionExpression = "")
    {
        TelemetryCollector.TrackAssertionCall("Assert.ContainsAll");
        ContainsAllImpl<T>(expected.ToArray(), collection.ToArray(), EqualityComparer<T>.Default, comparerName: null, message, expectedExpression, collectionExpression);
    }

    /// <summary>
    /// Tests whether <paramref name="collection"/> contains every element of <paramref name="expected"/> (with multiplicity).
    /// </summary>
    /// <typeparam name="T">The type of the collection items.</typeparam>
    /// <param name="expected">The span of items expected to all be present in <paramref name="collection"/>.</param>
    /// <param name="collection">The span expected to contain every item of <paramref name="expected"/>.</param>
    /// <param name="comparer">The equality comparer to use when comparing elements.</param>
    /// <param name="message">The message to include in the exception when the assertion fails.</param>
    /// <param name="expectedExpression">
    /// The syntactic expression of expected as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    /// <param name="collectionExpression">
    /// The syntactic expression of collection as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    public static void ContainsAll<T>(ReadOnlySpan<T> expected, ReadOnlySpan<T> collection, [NotNull] IEqualityComparer<T>? comparer, string? message = "", [CallerArgumentExpression(nameof(expected))] string expectedExpression = "", [CallerArgumentExpression(nameof(collection))] string collectionExpression = "")
    {
        TelemetryCollector.TrackAssertionCall("Assert.ContainsAll");
        CheckParameterNotNull(comparer, "Assert.ContainsAll", "comparer");
        ContainsAllImpl<T>(expected.ToArray(), collection.ToArray(), comparer, comparer.GetType().Name, message, expectedExpression, collectionExpression);
    }

    /// <summary>
    /// Tests whether <paramref name="collection"/> contains every element of <paramref name="expected"/> (with multiplicity).
    /// </summary>
    /// <typeparam name="T">The type of the collection items.</typeparam>
    /// <param name="expected">The span of items expected to all be present in <paramref name="collection"/>.</param>
    /// <param name="collection">The span expected to contain every item of <paramref name="expected"/>.</param>
    /// <param name="message">The message to include in the exception when the assertion fails.</param>
    /// <param name="expectedExpression">
    /// The syntactic expression of expected as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    /// <param name="collectionExpression">
    /// The syntactic expression of collection as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    public static void ContainsAll<T>(Span<T> expected, Span<T> collection, string? message = "", [CallerArgumentExpression(nameof(expected))] string expectedExpression = "", [CallerArgumentExpression(nameof(collection))] string collectionExpression = "")
        => ContainsAll((ReadOnlySpan<T>)expected, collection, message, expectedExpression, collectionExpression);

    /// <summary>
    /// Tests whether <paramref name="collection"/> contains every element of <paramref name="expected"/> (with multiplicity).
    /// </summary>
    /// <typeparam name="T">The type of the collection items.</typeparam>
    /// <param name="expected">The span of items expected to all be present in <paramref name="collection"/>.</param>
    /// <param name="collection">The span expected to contain every item of <paramref name="expected"/>.</param>
    /// <param name="comparer">The equality comparer to use when comparing elements.</param>
    /// <param name="message">The message to include in the exception when the assertion fails.</param>
    /// <param name="expectedExpression">
    /// The syntactic expression of expected as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    /// <param name="collectionExpression">
    /// The syntactic expression of collection as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    public static void ContainsAll<T>(Span<T> expected, Span<T> collection, [NotNull] IEqualityComparer<T>? comparer, string? message = "", [CallerArgumentExpression(nameof(expected))] string expectedExpression = "", [CallerArgumentExpression(nameof(collection))] string collectionExpression = "")
        => ContainsAll((ReadOnlySpan<T>)expected, collection, comparer, message, expectedExpression, collectionExpression);

    /// <summary>
    /// Tests whether <paramref name="collection"/> contains every element of <paramref name="expected"/> (with multiplicity).
    /// </summary>
    /// <typeparam name="T">The type of the collection items.</typeparam>
    /// <param name="expected">The memory of items expected to all be present in <paramref name="collection"/>.</param>
    /// <param name="collection">The memory expected to contain every item of <paramref name="expected"/>.</param>
    /// <param name="message">The message to include in the exception when the assertion fails.</param>
    /// <param name="expectedExpression">
    /// The syntactic expression of expected as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    /// <param name="collectionExpression">
    /// The syntactic expression of collection as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    public static void ContainsAll<T>(ReadOnlyMemory<T> expected, ReadOnlyMemory<T> collection, string? message = "", [CallerArgumentExpression(nameof(expected))] string expectedExpression = "", [CallerArgumentExpression(nameof(collection))] string collectionExpression = "")
        => ContainsAll(expected.Span, collection.Span, message, expectedExpression, collectionExpression);

    /// <summary>
    /// Tests whether <paramref name="collection"/> contains every element of <paramref name="expected"/> (with multiplicity).
    /// </summary>
    /// <typeparam name="T">The type of the collection items.</typeparam>
    /// <param name="expected">The memory of items expected to all be present in <paramref name="collection"/>.</param>
    /// <param name="collection">The memory expected to contain every item of <paramref name="expected"/>.</param>
    /// <param name="comparer">The equality comparer to use when comparing elements.</param>
    /// <param name="message">The message to include in the exception when the assertion fails.</param>
    /// <param name="expectedExpression">
    /// The syntactic expression of expected as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    /// <param name="collectionExpression">
    /// The syntactic expression of collection as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    public static void ContainsAll<T>(ReadOnlyMemory<T> expected, ReadOnlyMemory<T> collection, [NotNull] IEqualityComparer<T>? comparer, string? message = "", [CallerArgumentExpression(nameof(expected))] string expectedExpression = "", [CallerArgumentExpression(nameof(collection))] string collectionExpression = "")
        => ContainsAll(expected.Span, collection.Span, comparer, message, expectedExpression, collectionExpression);

    /// <summary>
    /// Tests whether <paramref name="collection"/> contains every element of <paramref name="expected"/> (with multiplicity).
    /// </summary>
    /// <typeparam name="T">The type of the collection items.</typeparam>
    /// <param name="expected">The memory of items expected to all be present in <paramref name="collection"/>.</param>
    /// <param name="collection">The memory expected to contain every item of <paramref name="expected"/>.</param>
    /// <param name="message">The message to include in the exception when the assertion fails.</param>
    /// <param name="expectedExpression">
    /// The syntactic expression of expected as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    /// <param name="collectionExpression">
    /// The syntactic expression of collection as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    public static void ContainsAll<T>(Memory<T> expected, Memory<T> collection, string? message = "", [CallerArgumentExpression(nameof(expected))] string expectedExpression = "", [CallerArgumentExpression(nameof(collection))] string collectionExpression = "")
        => ContainsAll(expected.Span, collection.Span, message, expectedExpression, collectionExpression);

    /// <summary>
    /// Tests whether <paramref name="collection"/> contains every element of <paramref name="expected"/> (with multiplicity).
    /// </summary>
    /// <typeparam name="T">The type of the collection items.</typeparam>
    /// <param name="expected">The memory of items expected to all be present in <paramref name="collection"/>.</param>
    /// <param name="collection">The memory expected to contain every item of <paramref name="expected"/>.</param>
    /// <param name="comparer">The equality comparer to use when comparing elements.</param>
    /// <param name="message">The message to include in the exception when the assertion fails.</param>
    /// <param name="expectedExpression">
    /// The syntactic expression of expected as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    /// <param name="collectionExpression">
    /// The syntactic expression of collection as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    public static void ContainsAll<T>(Memory<T> expected, Memory<T> collection, [NotNull] IEqualityComparer<T>? comparer, string? message = "", [CallerArgumentExpression(nameof(expected))] string expectedExpression = "", [CallerArgumentExpression(nameof(collection))] string collectionExpression = "")
        => ContainsAll(expected.Span, collection.Span, comparer, message, expectedExpression, collectionExpression);

    #endregion // ContainsAll span/memory
}

#endif
