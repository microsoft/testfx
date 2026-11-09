// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestTools.UnitTesting;

#pragma warning disable RS0026 // Do not add multiple public overloads with optional parameters
#pragma warning disable RS0027 // API with optional parameter(s) should have the most parameters amongst its public overloads

public sealed partial class Assert
{
    /// <summary>
    /// Tests whether every element in the collection matches the predicate.
    /// </summary>
    /// <typeparam name="T">The type of the collection items.</typeparam>
    /// <param name="predicate">A function to test each element for a condition.</param>
    /// <param name="collection">The collection.</param>
    /// <param name="message">The message to display when the assertion fails.</param>
    /// <param name="predicateExpression">The syntactic expression of predicate as given by the compiler via caller argument expression.</param>
    /// <param name="collectionExpression">The syntactic expression of collection as given by the compiler via caller argument expression.</param>
    /// <remarks>
    /// An empty collection passes. The predicate is evaluated once per visited element, stopping at the first
    /// non-matching element. Predicate and enumeration exceptions are not converted into assertion failures.
    /// </remarks>
    /// <exception cref="AssertFailedException">The collection or predicate is null, or an element does not match the predicate.</exception>
    public static void ContainsOnly<T>(Func<T, bool> predicate, IEnumerable<T> collection, string? message = "", [CallerArgumentExpression(nameof(predicate))] string predicateExpression = "", [CallerArgumentExpression(nameof(collection))] string collectionExpression = "")
        => ContainsOnlyCore(predicate, collection, message, predicateExpression, collectionExpression);

    /// <summary>
    /// Tests whether every element in the collection matches the predicate.
    /// </summary>
    /// <param name="predicate">A function to test each element for a condition.</param>
    /// <param name="collection">The collection.</param>
    /// <param name="message">The message to display when the assertion fails.</param>
    /// <param name="predicateExpression">The syntactic expression of predicate as given by the compiler via caller argument expression.</param>
    /// <param name="collectionExpression">The syntactic expression of collection as given by the compiler via caller argument expression.</param>
    /// <remarks>
    /// An empty collection passes. The predicate is evaluated once per visited element, stopping at the first
    /// non-matching element. Predicate and enumeration exceptions are not converted into assertion failures.
    /// </remarks>
    /// <exception cref="AssertFailedException">The collection or predicate is null, or an element does not match the predicate.</exception>
    public static void ContainsOnly(Func<object?, bool> predicate, IEnumerable collection, string? message = "", [CallerArgumentExpression(nameof(predicate))] string predicateExpression = "", [CallerArgumentExpression(nameof(collection))] string collectionExpression = "")
    {
        TelemetryCollector.TrackAssertionCall("Assert.ContainsOnly");
        CheckParameterNotNull(collection, "Assert.ContainsOnly", nameof(collection));
        CheckParameterNotNull(predicate, "Assert.ContainsOnly", nameof(predicate));
        if (collection is null || predicate is null)
        {
            return;
        }

        if (TryFindPredicateMismatch(collection.Cast<object?>(), predicate, expectedPredicateResult: true, out long index, out string? itemText))
        {
            ReportPredicateMismatchFailed(expectedPredicateResult: true, index, itemText, message, predicateExpression, collectionExpression);
        }
    }

#if NETCOREAPP3_1_OR_GREATER
    /// <summary>
    /// Tests whether every element in the array matches the predicate.
    /// </summary>
    /// <typeparam name="T">The type of the array items.</typeparam>
    /// <param name="predicate">A function to test each element for a condition.</param>
    /// <param name="collection">The array.</param>
    /// <param name="message">The message to display when the assertion fails.</param>
    /// <param name="predicateExpression">The syntactic expression of predicate as given by the compiler via caller argument expression.</param>
    /// <param name="collectionExpression">The syntactic expression of collection as given by the compiler via caller argument expression.</param>
    /// <remarks>An empty array passes. Evaluation stops at the first non-matching element.</remarks>
    /// <exception cref="AssertFailedException">The array or predicate is null, or an element does not match the predicate.</exception>
    public static void ContainsOnly<T>(Func<T, bool> predicate, T[] collection, string? message = "", [CallerArgumentExpression(nameof(predicate))] string predicateExpression = "", [CallerArgumentExpression(nameof(collection))] string collectionExpression = "")
        => ContainsOnlyCore(predicate, (IEnumerable<T>)collection, message, predicateExpression, collectionExpression);

    /// <summary>
    /// Tests whether every element in the collection matches the predicate.
    /// </summary>
    /// <typeparam name="T">The type of the collection items.</typeparam>
    /// <typeparam name="TCollection">The concrete type of the collection.</typeparam>
    /// <param name="predicate">A function to test each element for a condition.</param>
    /// <param name="collection">The collection.</param>
    /// <param name="message">The message to display when the assertion fails.</param>
    /// <param name="predicateExpression">The syntactic expression of predicate as given by the compiler via caller argument expression.</param>
    /// <param name="collectionExpression">The syntactic expression of collection as given by the compiler via caller argument expression.</param>
    /// <remarks>An empty collection passes. Evaluation stops at the first non-matching element.</remarks>
    /// <exception cref="AssertFailedException">The collection or predicate is null, or an element does not match the predicate.</exception>
    public static void ContainsOnly<T, TCollection>(Func<T, bool> predicate, TCollection collection, string? message = "", [CallerArgumentExpression(nameof(predicate))] string predicateExpression = "", [CallerArgumentExpression(nameof(collection))] string collectionExpression = "")
        where TCollection : IEnumerable<T>
        => ContainsOnlyCore(predicate, collection, message, predicateExpression, collectionExpression);

    /// <summary>
    /// Tests whether every element in the span matches the predicate.
    /// </summary>
    /// <typeparam name="T">The type of the span items.</typeparam>
    /// <param name="predicate">A function to test each element for a condition.</param>
    /// <param name="collection">The span.</param>
    /// <param name="message">The message to display when the assertion fails.</param>
    /// <param name="predicateExpression">The syntactic expression of predicate as given by the compiler via caller argument expression.</param>
    /// <param name="collectionExpression">The syntactic expression of collection as given by the compiler via caller argument expression.</param>
    /// <remarks>An empty span passes. Evaluation stops at the first non-matching element.</remarks>
    /// <exception cref="AssertFailedException">The predicate is null, or an element does not match the predicate.</exception>
    public static void ContainsOnly<T>(Func<T, bool> predicate, Span<T> collection, string? message = "", [CallerArgumentExpression(nameof(predicate))] string predicateExpression = "", [CallerArgumentExpression(nameof(collection))] string collectionExpression = "")
        => ContainsOnlyCore(predicate, collection, message, predicateExpression, collectionExpression);

    /// <summary>
    /// Tests whether every element in the span matches the predicate.
    /// </summary>
    /// <typeparam name="T">The type of the span items.</typeparam>
    /// <param name="predicate">A function to test each element for a condition.</param>
    /// <param name="collection">The span.</param>
    /// <param name="message">The message to display when the assertion fails.</param>
    /// <param name="predicateExpression">The syntactic expression of predicate as given by the compiler via caller argument expression.</param>
    /// <param name="collectionExpression">The syntactic expression of collection as given by the compiler via caller argument expression.</param>
    /// <remarks>An empty span passes. Evaluation stops at the first non-matching element.</remarks>
    /// <exception cref="AssertFailedException">The predicate is null, or an element does not match the predicate.</exception>
    public static void ContainsOnly<T>(Func<T, bool> predicate, ReadOnlySpan<T> collection, string? message = "", [CallerArgumentExpression(nameof(predicate))] string predicateExpression = "", [CallerArgumentExpression(nameof(collection))] string collectionExpression = "")
        => ContainsOnlyCore(predicate, collection, message, predicateExpression, collectionExpression);

    /// <summary>
    /// Tests whether every element in the memory matches the predicate.
    /// </summary>
    /// <typeparam name="T">The type of the memory items.</typeparam>
    /// <param name="predicate">A function to test each element for a condition.</param>
    /// <param name="collection">The memory.</param>
    /// <param name="message">The message to display when the assertion fails.</param>
    /// <param name="predicateExpression">The syntactic expression of predicate as given by the compiler via caller argument expression.</param>
    /// <param name="collectionExpression">The syntactic expression of collection as given by the compiler via caller argument expression.</param>
    /// <remarks>An empty memory passes. Evaluation stops at the first non-matching element.</remarks>
    /// <exception cref="AssertFailedException">The predicate is null, or an element does not match the predicate.</exception>
    public static void ContainsOnly<T>(Func<T, bool> predicate, Memory<T> collection, string? message = "", [CallerArgumentExpression(nameof(predicate))] string predicateExpression = "", [CallerArgumentExpression(nameof(collection))] string collectionExpression = "")
        => ContainsOnlyCore(predicate, collection.Span, message, predicateExpression, collectionExpression);

    /// <summary>
    /// Tests whether every element in the memory matches the predicate.
    /// </summary>
    /// <typeparam name="T">The type of the memory items.</typeparam>
    /// <param name="predicate">A function to test each element for a condition.</param>
    /// <param name="collection">The memory.</param>
    /// <param name="message">The message to display when the assertion fails.</param>
    /// <param name="predicateExpression">The syntactic expression of predicate as given by the compiler via caller argument expression.</param>
    /// <param name="collectionExpression">The syntactic expression of collection as given by the compiler via caller argument expression.</param>
    /// <remarks>An empty memory passes. Evaluation stops at the first non-matching element.</remarks>
    /// <exception cref="AssertFailedException">The predicate is null, or an element does not match the predicate.</exception>
    public static void ContainsOnly<T>(Func<T, bool> predicate, ReadOnlyMemory<T> collection, string? message = "", [CallerArgumentExpression(nameof(predicate))] string predicateExpression = "", [CallerArgumentExpression(nameof(collection))] string collectionExpression = "")
        => ContainsOnlyCore(predicate, collection.Span, message, predicateExpression, collectionExpression);

    private static void ContainsOnlyCore<T>(Func<T, bool> predicate, ReadOnlySpan<T> collection, string? message, string predicateExpression, string collectionExpression)
    {
        TelemetryCollector.TrackAssertionCall("Assert.ContainsOnly");
        CheckParameterNotNull(predicate, "Assert.ContainsOnly", nameof(predicate));
        if (predicate is null)
        {
            return;
        }

        if (TryFindPredicateMismatch(collection, predicate, expectedPredicateResult: true, out long index, out string? itemText))
        {
            ReportPredicateMismatchFailed(expectedPredicateResult: true, index, itemText, message, predicateExpression, collectionExpression);
        }
    }
#endif

    private static void ContainsOnlyCore<T>(Func<T, bool> predicate, IEnumerable<T> collection, string? message, string predicateExpression, string collectionExpression)
    {
        TelemetryCollector.TrackAssertionCall("Assert.ContainsOnly");
        CheckParameterNotNull(collection, "Assert.ContainsOnly", nameof(collection));
        CheckParameterNotNull(predicate, "Assert.ContainsOnly", nameof(predicate));
        if (collection is null || predicate is null)
        {
            return;
        }

        if (TryFindPredicateMismatch(collection, predicate, expectedPredicateResult: true, out long index, out string? itemText))
        {
            ReportPredicateMismatchFailed(expectedPredicateResult: true, index, itemText, message, predicateExpression, collectionExpression);
        }
    }
}
