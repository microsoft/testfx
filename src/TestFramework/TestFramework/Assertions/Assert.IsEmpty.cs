// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// A collection of helper classes to test various conditions within
/// unit tests. If the condition being tested is not met, an exception
/// is thrown.
/// </summary>
public sealed partial class Assert
{
#pragma warning disable RS0026 // Do not add multiple public overloads with optional parameters
#pragma warning disable RS0027 // API with optional parameter(s) should have the most parameters amongst its public overloads

    /// <summary>
    /// Tests that the collection is empty.
    /// </summary>
    /// <typeparam name="T">The type of the collection items.</typeparam>
    /// <param name="collection">The collection.</param>
    /// <param name="message">The message to display when the assertion fails.</param>
    /// <param name="collectionExpression">
    /// The syntactic expression of collection as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
#pragma warning disable IDE0060 // Remove unused parameter
    public static void IsEmpty<T>(IEnumerable<T> collection, [InterpolatedStringHandlerArgument(nameof(collection))] ref AssertCountInterpolatedStringHandler<T> message, [CallerArgumentExpression(nameof(collection))] string collectionExpression = "")
#pragma warning restore IDE0060 // Remove unused parameter
    {
        TelemetryCollector.TrackAssertionCall("Assert.IsEmpty");
        message.ComputeAssertion(nameof(IsEmpty), collectionExpression);
    }

    /// <summary>
    /// Tests that the collection is empty.
    /// </summary>
    /// <typeparam name="T">The type of the collection items.</typeparam>
    /// <param name="collection">The collection.</param>
    /// <param name="message">The message to display when the assertion fails.</param>
    /// <param name="collectionExpression">
    /// The syntactic expression of collection as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    public static void IsEmpty<T>(IEnumerable<T> collection, string? message = "", [CallerArgumentExpression(nameof(collection))] string collectionExpression = "")
        => HasCount(nameof(IsEmpty), 0, collection, message, collectionExpression);

    /// <summary>
    /// Tests that the collection is empty.
    /// </summary>
    /// <param name="collection">The collection.</param>
    /// <param name="message">The message to display when the assertion fails.</param>
    /// <param name="collectionExpression">
    /// The syntactic expression of collection as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    public static void IsEmpty(IEnumerable collection, string? message = "", [CallerArgumentExpression(nameof(collection))] string collectionExpression = "")
        => HasCount(nameof(IsEmpty), 0, collection, message, collectionExpression);

#if NETCOREAPP3_1_OR_GREATER

    /// <summary>
    /// Tests that the span is empty.
    /// </summary>
    /// <typeparam name="T">The type of the span items.</typeparam>
    /// <param name="collection">The span.</param>
    /// <param name="message">The message to display when the assertion fails.</param>
    /// <param name="collectionExpression">
    /// The syntactic expression of collection as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
#pragma warning disable IDE0060 // Remove unused parameter
    public static void IsEmpty<T>(ReadOnlySpan<T> collection, [InterpolatedStringHandlerArgument(nameof(collection))] ref AssertCountInterpolatedStringHandler<T> message, [CallerArgumentExpression(nameof(collection))] string collectionExpression = "")
#pragma warning restore IDE0060 // Remove unused parameter
    {
        TelemetryCollector.TrackAssertionCall("Assert.IsEmpty");
        message.ComputeAssertion(nameof(IsEmpty), collectionExpression);
    }

    /// <summary>
    /// Tests that the span is empty.
    /// </summary>
    /// <typeparam name="T">The type of the span items.</typeparam>
    /// <param name="collection">The span.</param>
    /// <param name="message">The message to display when the assertion fails.</param>
    /// <param name="collectionExpression">
    /// The syntactic expression of collection as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    public static void IsEmpty<T>(ReadOnlySpan<T> collection, string? message = "", [CallerArgumentExpression(nameof(collection))] string collectionExpression = "")
        => HasCount(nameof(IsEmpty), 0, collection, message, collectionExpression);

    /// <summary>
    /// Tests that the span is empty.
    /// </summary>
    /// <typeparam name="T">The type of the span items.</typeparam>
    /// <param name="collection">The span.</param>
    /// <param name="message">The message to display when the assertion fails.</param>
    /// <param name="collectionExpression">
    /// The syntactic expression of collection as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
#pragma warning disable IDE0060 // Remove unused parameter
    public static void IsEmpty<T>(Span<T> collection, [InterpolatedStringHandlerArgument(nameof(collection))] ref AssertCountInterpolatedStringHandler<T> message, [CallerArgumentExpression(nameof(collection))] string collectionExpression = "")
#pragma warning restore IDE0060 // Remove unused parameter
    {
        TelemetryCollector.TrackAssertionCall("Assert.IsEmpty");
        message.ComputeAssertion(nameof(IsEmpty), collectionExpression);
    }

    /// <summary>
    /// Tests that the span is empty.
    /// </summary>
    /// <typeparam name="T">The type of the span items.</typeparam>
    /// <param name="collection">The span.</param>
    /// <param name="message">The message to display when the assertion fails.</param>
    /// <param name="collectionExpression">
    /// The syntactic expression of collection as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    public static void IsEmpty<T>(Span<T> collection, string? message = "", [CallerArgumentExpression(nameof(collection))] string collectionExpression = "")
        => HasCount(nameof(IsEmpty), 0, collection, message, collectionExpression);

    /// <summary>
    /// Tests that the memory is empty.
    /// </summary>
    /// <typeparam name="T">The type of the memory items.</typeparam>
    /// <param name="collection">The memory.</param>
    /// <param name="message">The message to display when the assertion fails.</param>
    /// <param name="collectionExpression">
    /// The syntactic expression of collection as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
#pragma warning disable IDE0060 // Remove unused parameter
    public static void IsEmpty<T>(ReadOnlyMemory<T> collection, [InterpolatedStringHandlerArgument(nameof(collection))] ref AssertCountInterpolatedStringHandler<T> message, [CallerArgumentExpression(nameof(collection))] string collectionExpression = "")
#pragma warning restore IDE0060 // Remove unused parameter
    {
        TelemetryCollector.TrackAssertionCall("Assert.IsEmpty");
        message.ComputeAssertion(nameof(IsEmpty), collectionExpression);
    }

    /// <summary>
    /// Tests that the memory is empty.
    /// </summary>
    /// <typeparam name="T">The type of the memory items.</typeparam>
    /// <param name="collection">The memory.</param>
    /// <param name="message">The message to display when the assertion fails.</param>
    /// <param name="collectionExpression">
    /// The syntactic expression of collection as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    public static void IsEmpty<T>(ReadOnlyMemory<T> collection, string? message = "", [CallerArgumentExpression(nameof(collection))] string collectionExpression = "")
        => HasCount(nameof(IsEmpty), 0, collection.Span, message, collectionExpression);

    /// <summary>
    /// Tests that the memory is empty.
    /// </summary>
    /// <typeparam name="T">The type of the memory items.</typeparam>
    /// <param name="collection">The memory.</param>
    /// <param name="message">The message to display when the assertion fails.</param>
    /// <param name="collectionExpression">
    /// The syntactic expression of collection as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
#pragma warning disable IDE0060 // Remove unused parameter
    public static void IsEmpty<T>(Memory<T> collection, [InterpolatedStringHandlerArgument(nameof(collection))] ref AssertCountInterpolatedStringHandler<T> message, [CallerArgumentExpression(nameof(collection))] string collectionExpression = "")
#pragma warning restore IDE0060 // Remove unused parameter
    {
        TelemetryCollector.TrackAssertionCall("Assert.IsEmpty");
        message.ComputeAssertion(nameof(IsEmpty), collectionExpression);
    }

    /// <summary>
    /// Tests that the memory is empty.
    /// </summary>
    /// <typeparam name="T">The type of the memory items.</typeparam>
    /// <param name="collection">The memory.</param>
    /// <param name="message">The message to display when the assertion fails.</param>
    /// <param name="collectionExpression">
    /// The syntactic expression of collection as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    public static void IsEmpty<T>(Memory<T> collection, string? message = "", [CallerArgumentExpression(nameof(collection))] string collectionExpression = "")
        => HasCount(nameof(IsEmpty), 0, collection.Span, message, collectionExpression);

#endif

#pragma warning restore RS0026 // Do not add multiple public overloads with optional parameters
#pragma warning restore RS0027 // API with optional parameter(s) should have the most parameters amongst its public overloads
}
