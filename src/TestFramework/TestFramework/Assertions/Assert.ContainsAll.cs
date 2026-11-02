// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// A collection of helper classes to test various conditions within
/// unit tests. If the condition being tested is not met, an exception
/// is thrown.
/// </summary>
#pragma warning disable RS0026 // Do not add multiple public overloads with optional parameters
#pragma warning disable RS0027 // API with optional parameter(s) should have the most parameters amongst its public overloads
public sealed partial class Assert
{
    #region ContainsAll

    /// <summary>
    /// Tests whether <paramref name="collection"/> contains every element of <paramref name="expected"/>
    /// (with multiplicity) and throws an exception if any element in <paramref name="expected"/> occurs
    /// more times than in <paramref name="collection"/>.
    /// </summary>
    /// <remarks>
    /// Element multiplicity is significant: <c>[1]</c> does not contain all of <c>[1, 1]</c>.
    /// </remarks>
    /// <typeparam name="T">The type of the collection items.</typeparam>
    /// <param name="expected">
    /// The collection of items expected to all be present in <paramref name="collection"/>.
    /// </param>
    /// <param name="collection">
    /// The collection expected to contain every item of <paramref name="expected"/>.
    /// </param>
    /// <param name="message">
    /// The message to include in the exception when an element in <paramref name="expected"/>
    /// is not found (with sufficient multiplicity) in <paramref name="collection"/>. The message is shown in test results.
    /// </param>
    /// <param name="expectedExpression">
    /// The syntactic expression of expected as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    /// <param name="collectionExpression">
    /// The syntactic expression of collection as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// Thrown if <paramref name="expected"/> contains at least one element that occurs more times
    /// than in <paramref name="collection"/>.
    /// </exception>
    public static void ContainsAll<T>([NotNull] IEnumerable<T>? expected, [NotNull] IEnumerable<T>? collection, string? message = "", [CallerArgumentExpression(nameof(expected))] string expectedExpression = "", [CallerArgumentExpression(nameof(collection))] string collectionExpression = "")
    {
        TelemetryCollector.TrackAssertionCall("Assert.ContainsAll");
        CheckParameterNotNull(expected, "Assert.ContainsAll", "expected");
        CheckParameterNotNull(collection, "Assert.ContainsAll", "collection");
        ContainsAllImpl(expected, collection, EqualityComparer<T>.Default, comparerName: null, message, expectedExpression, collectionExpression);
    }

    /// <summary>
    /// Tests whether <paramref name="collection"/> contains every element of <paramref name="expected"/>
    /// (with multiplicity) and throws an exception if any element in <paramref name="expected"/> occurs
    /// more times than in <paramref name="collection"/>.
    /// </summary>
    /// <remarks>
    /// Element multiplicity is significant: <c>[1]</c> does not contain all of <c>[1, 1]</c>.
    /// </remarks>
    /// <typeparam name="T">The type of the collection items.</typeparam>
    /// <param name="expected">
    /// The collection of items expected to all be present in <paramref name="collection"/>.
    /// </param>
    /// <param name="collection">
    /// The collection expected to contain every item of <paramref name="expected"/>.
    /// </param>
    /// <param name="comparer">
    /// The equality comparer to use when comparing elements.
    /// </param>
    /// <param name="message">
    /// The message to include in the exception when an element in <paramref name="expected"/>
    /// is not found (with sufficient multiplicity) in <paramref name="collection"/>. The message is shown in test results.
    /// </param>
    /// <param name="expectedExpression">
    /// The syntactic expression of expected as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    /// <param name="collectionExpression">
    /// The syntactic expression of collection as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// Thrown if <paramref name="expected"/> contains at least one element that occurs more times
    /// than in <paramref name="collection"/>.
    /// </exception>
    public static void ContainsAll<T>([NotNull] IEnumerable<T>? expected, [NotNull] IEnumerable<T>? collection, [NotNull] IEqualityComparer<T>? comparer, string? message = "", [CallerArgumentExpression(nameof(expected))] string expectedExpression = "", [CallerArgumentExpression(nameof(collection))] string collectionExpression = "")
    {
        TelemetryCollector.TrackAssertionCall("Assert.ContainsAll");
        CheckParameterNotNull(expected, "Assert.ContainsAll", "expected");
        CheckParameterNotNull(collection, "Assert.ContainsAll", "collection");
        CheckParameterNotNull(comparer, "Assert.ContainsAll", "comparer");
        ContainsAllImpl(expected, collection, comparer, comparer.GetType().Name, message, expectedExpression, collectionExpression);
    }

    /// <summary>
    /// Tests whether <paramref name="collection"/> contains every element of <paramref name="expected"/>
    /// (with multiplicity) and throws an exception if any element in <paramref name="expected"/> occurs
    /// more times than in <paramref name="collection"/>.
    /// </summary>
    /// <remarks>
    /// Element multiplicity is significant: <c>[1]</c> does not contain all of <c>[1, 1]</c>.
    /// </remarks>
    /// <param name="expected">
    /// The collection of items expected to all be present in <paramref name="collection"/>.
    /// </param>
    /// <param name="collection">
    /// The collection expected to contain every item of <paramref name="expected"/>.
    /// </param>
    /// <param name="message">
    /// The message to include in the exception when an element in <paramref name="expected"/>
    /// is not found (with sufficient multiplicity) in <paramref name="collection"/>. The message is shown in test results.
    /// </param>
    /// <param name="expectedExpression">
    /// The syntactic expression of expected as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    /// <param name="collectionExpression">
    /// The syntactic expression of collection as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// Thrown if <paramref name="expected"/> contains at least one element that occurs more times
    /// than in <paramref name="collection"/>.
    /// </exception>
    public static void ContainsAll([NotNull] IEnumerable? expected, [NotNull] IEnumerable? collection, string? message = "", [CallerArgumentExpression(nameof(expected))] string expectedExpression = "", [CallerArgumentExpression(nameof(collection))] string collectionExpression = "")
    {
        TelemetryCollector.TrackAssertionCall("Assert.ContainsAll");
        CheckParameterNotNull(expected, "Assert.ContainsAll", "expected");
        CheckParameterNotNull(collection, "Assert.ContainsAll", "collection");

        ContainsAllImpl(expected.Cast<object?>(), collection.Cast<object?>(), EqualityComparer<object?>.Default, comparerName: null, message, expectedExpression, collectionExpression);
    }

    /// <summary>
    /// Tests whether <paramref name="collection"/> contains every element of <paramref name="expected"/>
    /// (with multiplicity) and throws an exception if any element in <paramref name="expected"/> occurs
    /// more times than in <paramref name="collection"/>.
    /// </summary>
    /// <remarks>
    /// Element multiplicity is significant: <c>[1]</c> does not contain all of <c>[1, 1]</c>.
    /// </remarks>
    /// <param name="expected">
    /// The collection of items expected to all be present in <paramref name="collection"/>.
    /// </param>
    /// <param name="collection">
    /// The collection expected to contain every item of <paramref name="expected"/>.
    /// </param>
    /// <param name="comparer">
    /// The equality comparer to use when comparing elements.
    /// </param>
    /// <param name="message">
    /// The message to include in the exception when an element in <paramref name="expected"/>
    /// is not found (with sufficient multiplicity) in <paramref name="collection"/>. The message is shown in test results.
    /// </param>
    /// <param name="expectedExpression">
    /// The syntactic expression of expected as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    /// <param name="collectionExpression">
    /// The syntactic expression of collection as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// Thrown if <paramref name="expected"/> contains at least one element that occurs more times
    /// than in <paramref name="collection"/>.
    /// </exception>
    public static void ContainsAll([NotNull] IEnumerable? expected, [NotNull] IEnumerable? collection, [NotNull] IEqualityComparer? comparer, string? message = "", [CallerArgumentExpression(nameof(expected))] string expectedExpression = "", [CallerArgumentExpression(nameof(collection))] string collectionExpression = "")
    {
        TelemetryCollector.TrackAssertionCall("Assert.ContainsAll");
        CheckParameterNotNull(expected, "Assert.ContainsAll", "expected");
        CheckParameterNotNull(collection, "Assert.ContainsAll", "collection");
        CheckParameterNotNull(comparer, "Assert.ContainsAll", "comparer");

        ContainsAllImpl(expected.Cast<object?>(), collection.Cast<object?>(), new NonGenericEqualityComparerAdapter(comparer), comparer.GetType().Name, message, expectedExpression, collectionExpression);
    }

    #endregion // ContainsAll

#if NETCOREAPP3_1_OR_GREATER

    #region ContainsAll array/collection forwarding

    /// <summary>
    /// Tests whether <paramref name="collection"/> contains every element of the specified <paramref name="expected"/> array.
    /// </summary>
    /// <remarks>Element multiplicity is significant: <c>[1]</c> does not contain all of <c>[1, 1]</c>.</remarks>
    /// <typeparam name="T">The type of the array items.</typeparam>
    /// <param name="expected">The array of items expected to all be present in <paramref name="collection"/>.</param>
    /// <param name="collection">The collection expected to contain every item of <paramref name="expected"/>.</param>
    /// <param name="message">The message to include in the exception when the assertion fails.</param>
    /// <param name="expectedExpression">The syntactic expression of expected as given by the compiler via caller argument expression.</param>
    /// <param name="collectionExpression">The syntactic expression of collection as given by the compiler via caller argument expression.</param>
    /// <exception cref="AssertFailedException">Thrown if an expected element does not occur enough times in <paramref name="collection"/>.</exception>
#pragma warning disable IDE0004 // Casts are required to forward to the legacy IEnumerable overload.
    [OverloadResolutionPriority(1)]
    public static void ContainsAll<T>([NotNull] T[]? expected, [NotNull] IEnumerable<T>? collection, string? message = "", [CallerArgumentExpression(nameof(expected))] string expectedExpression = "", [CallerArgumentExpression(nameof(collection))] string collectionExpression = "")
        => ContainsAll((IEnumerable<T>?)expected, collection, message, expectedExpression, collectionExpression);
#pragma warning restore IDE0004 // Casts are required to forward to the legacy IEnumerable overload.

    /// <summary>
    /// Tests whether the specified array contains every element of <paramref name="expected"/>.
    /// </summary>
    /// <remarks>Element multiplicity is significant: <c>[1]</c> does not contain all of <c>[1, 1]</c>.</remarks>
    /// <typeparam name="T">The type of the array items.</typeparam>
    /// <param name="expected">The array of items expected to all be present in <paramref name="collection"/>.</param>
    /// <param name="collection">The array expected to contain every item of <paramref name="expected"/>.</param>
    /// <param name="message">The message to include in the exception when the assertion fails.</param>
    /// <param name="expectedExpression">The syntactic expression of expected as given by the compiler via caller argument expression.</param>
    /// <param name="collectionExpression">The syntactic expression of collection as given by the compiler via caller argument expression.</param>
    /// <exception cref="AssertFailedException">Thrown if an expected element does not occur enough times in <paramref name="collection"/>.</exception>
#pragma warning disable IDE0004 // Casts are required to forward to the legacy IEnumerable overload.
    [OverloadResolutionPriority(1)]
    public static void ContainsAll<T>([NotNull] T[]? expected, [NotNull] T[]? collection, string? message = "", [CallerArgumentExpression(nameof(expected))] string expectedExpression = "", [CallerArgumentExpression(nameof(collection))] string collectionExpression = "")
        => ContainsAll((IEnumerable<T>?)expected, (IEnumerable<T>?)collection, message, expectedExpression, collectionExpression);
#pragma warning restore IDE0004 // Casts are required to forward to the legacy IEnumerable overload.

    /// <summary>
    /// Tests whether the specified array contains every element of <paramref name="expected"/> using the specified equality comparer.
    /// </summary>
    /// <remarks>Element multiplicity is significant: <c>[1]</c> does not contain all of <c>[1, 1]</c>.</remarks>
    /// <typeparam name="T">The type of the array items.</typeparam>
    /// <param name="expected">The array of items expected to all be present in <paramref name="collection"/>.</param>
    /// <param name="collection">The array expected to contain every item of <paramref name="expected"/>.</param>
    /// <param name="comparer">The equality comparer to use when comparing elements.</param>
    /// <param name="message">The message to include in the exception when the assertion fails.</param>
    /// <param name="expectedExpression">The syntactic expression of expected as given by the compiler via caller argument expression.</param>
    /// <param name="collectionExpression">The syntactic expression of collection as given by the compiler via caller argument expression.</param>
    /// <exception cref="AssertFailedException">Thrown if an expected element does not occur enough times in <paramref name="collection"/>.</exception>
#pragma warning disable IDE0004 // Casts are required to forward to the legacy IEnumerable overload.
    public static void ContainsAll<T>([NotNull] T[]? expected, [NotNull] T[]? collection, [NotNull] IEqualityComparer<T>? comparer, string? message = "", [CallerArgumentExpression(nameof(expected))] string expectedExpression = "", [CallerArgumentExpression(nameof(collection))] string collectionExpression = "")
        => ContainsAll((IEnumerable<T>?)expected, (IEnumerable<T>?)collection, comparer, message, expectedExpression, collectionExpression);
#pragma warning restore IDE0004 // Casts are required to forward to the legacy IEnumerable overload.

    /// <summary>
    /// Tests whether the specified collection contains every element of <paramref name="expected"/> using the specified equality comparer.
    /// </summary>
    /// <remarks>
    /// Element multiplicity is significant: <c>[1]</c> does not contain all of <c>[1, 1]</c>.
    /// </remarks>
    /// <typeparam name="T">The type of the collection items.</typeparam>
    /// <typeparam name="TExpectedCollection">The concrete type of the expected collection.</typeparam>
    /// <typeparam name="TCollection">The concrete type of the collection.</typeparam>
    /// <param name="expected">The collection of items expected to all be present in <paramref name="collection"/>.</param>
    /// <param name="collection">The collection expected to contain every item of <paramref name="expected"/>.</param>
    /// <param name="comparer">The equality comparer to use when comparing elements.</param>
    /// <param name="message">The message to include in the exception when the assertion fails.</param>
    /// <param name="expectedExpression">The syntactic expression of expected as given by the compiler via caller argument expression.</param>
    /// <param name="collectionExpression">The syntactic expression of collection as given by the compiler via caller argument expression.</param>
    /// <exception cref="AssertFailedException">
    /// Thrown if <paramref name="expected"/> contains an element that does not occur enough times in <paramref name="collection"/>.
    /// </exception>
#pragma warning disable IDE0004 // Casts are required to forward to the legacy IEnumerable overload.
    public static void ContainsAll<T, TExpectedCollection, TCollection>([NotNull] TExpectedCollection? expected, [NotNull] TCollection? collection, [NotNull] IEqualityComparer<T>? comparer, string? message = "", [CallerArgumentExpression(nameof(expected))] string expectedExpression = "", [CallerArgumentExpression(nameof(collection))] string collectionExpression = "")
        where TExpectedCollection : IEnumerable<T>
        where TCollection : IEnumerable<T>
        => ContainsAll((IEnumerable<T>?)expected, (IEnumerable<T>?)collection, comparer, message, expectedExpression, collectionExpression);
#pragma warning restore IDE0004 // Casts are required to forward to the legacy IEnumerable overload.

    #endregion // ContainsAll array/collection forwarding

#endif
}
