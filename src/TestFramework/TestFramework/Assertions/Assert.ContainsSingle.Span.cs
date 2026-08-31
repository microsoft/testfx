// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestTools.UnitTesting;

#if NETCOREAPP3_1_OR_GREATER

public sealed partial class Assert
{
#pragma warning disable RS0026 // Do not add multiple public overloads with optional parameters
#pragma warning disable RS0027 // API with optional parameter(s) should have the most parameters amongst its public overloads

    #region ContainsSingle span/memory

    /// <summary>
    /// Tests whether the specified span contains exactly one element.
    /// </summary>
    /// <typeparam name="T">The type of the span items.</typeparam>
    /// <param name="collection">The span.</param>
    /// <param name="message">The message to display when the assertion fails.</param>
    /// <param name="collectionExpression">
    /// The syntactic expression of collection as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    /// <returns>The item.</returns>
#pragma warning disable IDE0060 // Remove unused parameter
    public static T ContainsSingle<T>(ReadOnlySpan<T> collection, [InterpolatedStringHandlerArgument(nameof(collection))] ref AssertSingleInterpolatedStringHandler<T> message, [CallerArgumentExpression(nameof(collection))] string collectionExpression = "")
#pragma warning restore IDE0060 // Remove unused parameter
        => message.ComputeAssertion(collectionExpression);

    /// <summary>
    /// Tests whether the specified span contains exactly one element.
    /// </summary>
    /// <typeparam name="T">The type of the span items.</typeparam>
    /// <param name="collection">The span.</param>
    /// <param name="message">The message to display when the assertion fails.</param>
    /// <param name="collectionExpression">
    /// The syntactic expression of collection as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    /// <returns>The item.</returns>
    public static T ContainsSingle<T>(ReadOnlySpan<T> collection, string? message = "", [CallerArgumentExpression(nameof(collection))] string collectionExpression = "")
    {
        if (collection.Length == 1)
        {
            return collection[0];
        }

        ReportAssertContainsSingleFailed(collection.Length, message, collectionExpression);
        // Unreachable code but compiler cannot work it out
        return default!;
    }

    /// <summary>
    /// Tests whether the specified span contains exactly one element.
    /// </summary>
    /// <typeparam name="T">The type of the span items.</typeparam>
    /// <param name="collection">The span.</param>
    /// <param name="message">The message to display when the assertion fails.</param>
    /// <param name="collectionExpression">
    /// The syntactic expression of collection as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    /// <returns>The item.</returns>
#pragma warning disable IDE0060 // Remove unused parameter
    public static T ContainsSingle<T>(Span<T> collection, [InterpolatedStringHandlerArgument(nameof(collection))] ref AssertSingleInterpolatedStringHandler<T> message, [CallerArgumentExpression(nameof(collection))] string collectionExpression = "")
#pragma warning restore IDE0060 // Remove unused parameter
        => message.ComputeAssertion(collectionExpression);

    /// <summary>
    /// Tests whether the specified span contains exactly one element.
    /// </summary>
    /// <typeparam name="T">The type of the span items.</typeparam>
    /// <param name="collection">The span.</param>
    /// <param name="message">The message to display when the assertion fails.</param>
    /// <param name="collectionExpression">
    /// The syntactic expression of collection as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    /// <returns>The item.</returns>
    public static T ContainsSingle<T>(Span<T> collection, string? message = "", [CallerArgumentExpression(nameof(collection))] string collectionExpression = "")
        => ContainsSingle((ReadOnlySpan<T>)collection, message, collectionExpression);

    /// <summary>
    /// Tests whether the specified memory contains exactly one element.
    /// </summary>
    /// <typeparam name="T">The type of the memory items.</typeparam>
    /// <param name="collection">The memory.</param>
    /// <param name="message">The message to display when the assertion fails.</param>
    /// <param name="collectionExpression">
    /// The syntactic expression of collection as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    /// <returns>The item.</returns>
#pragma warning disable IDE0060 // Remove unused parameter
    public static T ContainsSingle<T>(ReadOnlyMemory<T> collection, [InterpolatedStringHandlerArgument(nameof(collection))] ref AssertSingleInterpolatedStringHandler<T> message, [CallerArgumentExpression(nameof(collection))] string collectionExpression = "")
#pragma warning restore IDE0060 // Remove unused parameter
        => message.ComputeAssertion(collectionExpression);

    /// <summary>
    /// Tests whether the specified memory contains exactly one element.
    /// </summary>
    /// <typeparam name="T">The type of the memory items.</typeparam>
    /// <param name="collection">The memory.</param>
    /// <param name="message">The message to display when the assertion fails.</param>
    /// <param name="collectionExpression">
    /// The syntactic expression of collection as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    /// <returns>The item.</returns>
    public static T ContainsSingle<T>(ReadOnlyMemory<T> collection, string? message = "", [CallerArgumentExpression(nameof(collection))] string collectionExpression = "")
        => ContainsSingle(collection.Span, message, collectionExpression);

    /// <summary>
    /// Tests whether the specified memory contains exactly one element.
    /// </summary>
    /// <typeparam name="T">The type of the memory items.</typeparam>
    /// <param name="collection">The memory.</param>
    /// <param name="message">The message to display when the assertion fails.</param>
    /// <param name="collectionExpression">
    /// The syntactic expression of collection as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    /// <returns>The item.</returns>
#pragma warning disable IDE0060 // Remove unused parameter
    public static T ContainsSingle<T>(Memory<T> collection, [InterpolatedStringHandlerArgument(nameof(collection))] ref AssertSingleInterpolatedStringHandler<T> message, [CallerArgumentExpression(nameof(collection))] string collectionExpression = "")
#pragma warning restore IDE0060 // Remove unused parameter
        => message.ComputeAssertion(collectionExpression);

    /// <summary>
    /// Tests whether the specified memory contains exactly one element.
    /// </summary>
    /// <typeparam name="T">The type of the memory items.</typeparam>
    /// <param name="collection">The memory.</param>
    /// <param name="message">The message to display when the assertion fails.</param>
    /// <param name="collectionExpression">
    /// The syntactic expression of collection as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    /// <returns>The item.</returns>
    public static T ContainsSingle<T>(Memory<T> collection, string? message = "", [CallerArgumentExpression(nameof(collection))] string collectionExpression = "")
        => ContainsSingle(collection.Span, message, collectionExpression);

    /// <summary>
    /// Tests whether the specified span contains exactly one element that matches the given predicate.
    /// </summary>
    /// <typeparam name="T">The type of the span items.</typeparam>
    /// <param name="predicate">A function to test each element for a condition.</param>
    /// <param name="collection">The span.</param>
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
    public static T ContainsSingle<T>(Func<T, bool> predicate, ReadOnlySpan<T> collection, string? message = "", [CallerArgumentExpression(nameof(predicate))] string predicateExpression = "", [CallerArgumentExpression(nameof(collection))] string collectionExpression = "")
    {
        TelemetryCollector.TrackAssertionCall("Assert.ContainsSingle");
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

        ReportAssertSingleMatchFailed(matchCount, message, predicateExpression, collectionExpression);
        // Unreachable code but compiler cannot work it out
        return default!;
    }

    /// <summary>
    /// Tests whether the specified span contains exactly one element that matches the given predicate.
    /// </summary>
    /// <typeparam name="T">The type of the span items.</typeparam>
    /// <param name="predicate">A function to test each element for a condition.</param>
    /// <param name="collection">The span.</param>
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
    public static T ContainsSingle<T>(Func<T, bool> predicate, Span<T> collection, string? message = "", [CallerArgumentExpression(nameof(predicate))] string predicateExpression = "", [CallerArgumentExpression(nameof(collection))] string collectionExpression = "")
        => ContainsSingle(predicate, (ReadOnlySpan<T>)collection, message, predicateExpression, collectionExpression);

    /// <summary>
    /// Tests whether the specified memory contains exactly one element that matches the given predicate.
    /// </summary>
    /// <typeparam name="T">The type of the memory items.</typeparam>
    /// <param name="predicate">A function to test each element for a condition.</param>
    /// <param name="collection">The memory.</param>
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
    public static T ContainsSingle<T>(Func<T, bool> predicate, ReadOnlyMemory<T> collection, string? message = "", [CallerArgumentExpression(nameof(predicate))] string predicateExpression = "", [CallerArgumentExpression(nameof(collection))] string collectionExpression = "")
        => ContainsSingle(predicate, collection.Span, message, predicateExpression, collectionExpression);

    /// <summary>
    /// Tests whether the specified memory contains exactly one element that matches the given predicate.
    /// </summary>
    /// <typeparam name="T">The type of the memory items.</typeparam>
    /// <param name="predicate">A function to test each element for a condition.</param>
    /// <param name="collection">The memory.</param>
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
    public static T ContainsSingle<T>(Func<T, bool> predicate, Memory<T> collection, string? message = "", [CallerArgumentExpression(nameof(predicate))] string predicateExpression = "", [CallerArgumentExpression(nameof(collection))] string collectionExpression = "")
        => ContainsSingle(predicate, collection.Span, message, predicateExpression, collectionExpression);

    #endregion // ContainsSingle span/memory
}

#endif
