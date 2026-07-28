// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestTools.UnitTesting;

#pragma warning disable RS0026 // Do not add multiple public overloads with optional parameters
#pragma warning disable RS0027 // API with optional parameter(s) should have the most parameters amongst its public overloads
public sealed partial class Assert
{
    #region AreSequenceEqual

    /// <summary>
    /// Tests whether two sequences contain equal elements in the same order and throws an exception if they do not.
    /// </summary>
    /// <typeparam name="T">The type of sequence elements.</typeparam>
    /// <param name="expected">The sequence expected to be equal to <paramref name="actual"/>.</param>
    /// <param name="actual">The sequence produced by the code under test.</param>
    /// <param name="message">The message to include in the exception when the sequences are not equal.</param>
    /// <param name="expectedExpression">The syntactic expression of expected as given by the compiler via caller argument expression.</param>
    /// <param name="actualExpression">The syntactic expression of actual as given by the compiler via caller argument expression.</param>
    public static void AreSequenceEqual<T>(IEnumerable<T>? expected, IEnumerable<T>? actual, string? message = "", [CallerArgumentExpression(nameof(expected))] string expectedExpression = "", [CallerArgumentExpression(nameof(actual))] string actualExpression = "")
    {
        TelemetryCollector.TrackAssertionCall("Assert.AreSequenceEqual");
        AreSequenceEqualImpl(expected, actual, EqualityComparer<T>.Default, comparerName: null, hasComparerArgument: false, SequenceOrder.InOrder, hasOrderArgument: false, message, expectedExpression, actualExpression);
    }

    /// <summary>
    /// Tests whether two sequences contain equal elements using the specified order semantics and throws an exception if they do not.
    /// </summary>
    /// <typeparam name="T">The type of sequence elements.</typeparam>
    /// <param name="expected">The sequence expected to be equal to <paramref name="actual"/>.</param>
    /// <param name="actual">The sequence produced by the code under test.</param>
    /// <param name="order">Specifies whether elements must appear in the same order or in any order.</param>
    /// <param name="message">The message to include in the exception when the sequences are not equal.</param>
    /// <param name="expectedExpression">The syntactic expression of expected as given by the compiler via caller argument expression.</param>
    /// <param name="actualExpression">The syntactic expression of actual as given by the compiler via caller argument expression.</param>
    public static void AreSequenceEqual<T>(IEnumerable<T>? expected, IEnumerable<T>? actual, SequenceOrder order, string? message = "", [CallerArgumentExpression(nameof(expected))] string expectedExpression = "", [CallerArgumentExpression(nameof(actual))] string actualExpression = "")
    {
        TelemetryCollector.TrackAssertionCall("Assert.AreSequenceEqual");
        AreSequenceEqualImpl(expected, actual, EqualityComparer<T>.Default, comparerName: null, hasComparerArgument: false, order, hasOrderArgument: true, message, expectedExpression, actualExpression);
    }

    /// <summary>
    /// Tests whether two sequences contain equal elements in the same order using the specified comparer and throws an exception if they do not.
    /// </summary>
    /// <typeparam name="T">The type of sequence elements.</typeparam>
    /// <param name="expected">The sequence expected to be equal to <paramref name="actual"/>.</param>
    /// <param name="actual">The sequence produced by the code under test.</param>
    /// <param name="comparer">The equality comparer to use when comparing elements, or <see langword="null"/> to use the default comparer.</param>
    /// <param name="message">The message to include in the exception when the sequences are not equal.</param>
    /// <param name="expectedExpression">The syntactic expression of expected as given by the compiler via caller argument expression.</param>
    /// <param name="actualExpression">The syntactic expression of actual as given by the compiler via caller argument expression.</param>
    public static void AreSequenceEqual<T>(IEnumerable<T>? expected, IEnumerable<T>? actual, IEqualityComparer<T>? comparer, string? message = "", [CallerArgumentExpression(nameof(expected))] string expectedExpression = "", [CallerArgumentExpression(nameof(actual))] string actualExpression = "")
    {
        TelemetryCollector.TrackAssertionCall("Assert.AreSequenceEqual");
        AreSequenceEqualImpl(expected, actual, comparer ?? EqualityComparer<T>.Default, comparer?.GetType().Name, hasComparerArgument: true, SequenceOrder.InOrder, hasOrderArgument: false, message, expectedExpression, actualExpression);
    }

    /// <summary>
    /// Tests whether two sequences contain equal elements using the specified comparer and order semantics and throws an exception if they do not.
    /// </summary>
    /// <typeparam name="T">The type of sequence elements.</typeparam>
    /// <param name="expected">The sequence expected to be equal to <paramref name="actual"/>.</param>
    /// <param name="actual">The sequence produced by the code under test.</param>
    /// <param name="comparer">The equality comparer to use when comparing elements, or <see langword="null"/> to use the default comparer.</param>
    /// <param name="order">Specifies whether elements must appear in the same order or in any order.</param>
    /// <param name="message">The message to include in the exception when the sequences are not equal.</param>
    /// <param name="expectedExpression">The syntactic expression of expected as given by the compiler via caller argument expression.</param>
    /// <param name="actualExpression">The syntactic expression of actual as given by the compiler via caller argument expression.</param>
    public static void AreSequenceEqual<T>(IEnumerable<T>? expected, IEnumerable<T>? actual, IEqualityComparer<T>? comparer, SequenceOrder order, string? message = "", [CallerArgumentExpression(nameof(expected))] string expectedExpression = "", [CallerArgumentExpression(nameof(actual))] string actualExpression = "")
    {
        TelemetryCollector.TrackAssertionCall("Assert.AreSequenceEqual");
        AreSequenceEqualImpl(expected, actual, comparer ?? EqualityComparer<T>.Default, comparer?.GetType().Name, hasComparerArgument: true, order, hasOrderArgument: true, message, expectedExpression, actualExpression);
    }

    /// <summary>
    /// Tests whether two sequences contain equal elements in the same order and throws an exception if they do not.
    /// </summary>
    /// <param name="expected">The sequence expected to be equal to <paramref name="actual"/>.</param>
    /// <param name="actual">The sequence produced by the code under test.</param>
    /// <param name="message">The message to include in the exception when the sequences are not equal.</param>
    /// <param name="expectedExpression">The syntactic expression of expected as given by the compiler via caller argument expression.</param>
    /// <param name="actualExpression">The syntactic expression of actual as given by the compiler via caller argument expression.</param>
    public static void AreSequenceEqual(IEnumerable? expected, IEnumerable? actual, string? message = "", [CallerArgumentExpression(nameof(expected))] string expectedExpression = "", [CallerArgumentExpression(nameof(actual))] string actualExpression = "")
    {
        TelemetryCollector.TrackAssertionCall("Assert.AreSequenceEqual");
        AreSequenceEqualImpl(expected?.Cast<object?>(), actual?.Cast<object?>(), EqualityComparer<object?>.Default, comparerName: null, hasComparerArgument: false, SequenceOrder.InOrder, hasOrderArgument: false, message, expectedExpression, actualExpression);
    }

    /// <summary>
    /// Tests whether two sequences contain equal elements using the specified order semantics and throws an exception if they do not.
    /// </summary>
    /// <param name="expected">The sequence expected to be equal to <paramref name="actual"/>.</param>
    /// <param name="actual">The sequence produced by the code under test.</param>
    /// <param name="order">Specifies whether elements must appear in the same order or in any order.</param>
    /// <param name="message">The message to include in the exception when the sequences are not equal.</param>
    /// <param name="expectedExpression">The syntactic expression of expected as given by the compiler via caller argument expression.</param>
    /// <param name="actualExpression">The syntactic expression of actual as given by the compiler via caller argument expression.</param>
    public static void AreSequenceEqual(IEnumerable? expected, IEnumerable? actual, SequenceOrder order, string? message = "", [CallerArgumentExpression(nameof(expected))] string expectedExpression = "", [CallerArgumentExpression(nameof(actual))] string actualExpression = "")
    {
        TelemetryCollector.TrackAssertionCall("Assert.AreSequenceEqual");
        AreSequenceEqualImpl(expected?.Cast<object?>(), actual?.Cast<object?>(), EqualityComparer<object?>.Default, comparerName: null, hasComparerArgument: false, order, hasOrderArgument: true, message, expectedExpression, actualExpression);
    }

    /// <summary>
    /// Tests whether two sequences contain equal elements in the same order using the specified comparer and throws an exception if they do not.
    /// </summary>
    /// <param name="expected">The sequence expected to be equal to <paramref name="actual"/>.</param>
    /// <param name="actual">The sequence produced by the code under test.</param>
    /// <param name="comparer">The equality comparer to use when comparing elements, or <see langword="null"/> to use the default comparer.</param>
    /// <param name="message">The message to include in the exception when the sequences are not equal.</param>
    /// <param name="expectedExpression">The syntactic expression of expected as given by the compiler via caller argument expression.</param>
    /// <param name="actualExpression">The syntactic expression of actual as given by the compiler via caller argument expression.</param>
    public static void AreSequenceEqual(IEnumerable? expected, IEnumerable? actual, IEqualityComparer? comparer, string? message = "", [CallerArgumentExpression(nameof(expected))] string expectedExpression = "", [CallerArgumentExpression(nameof(actual))] string actualExpression = "")
    {
        TelemetryCollector.TrackAssertionCall("Assert.AreSequenceEqual");
        AreSequenceEqualImpl(expected?.Cast<object?>(), actual?.Cast<object?>(), comparer is null ? EqualityComparer<object?>.Default : new NonGenericEqualityComparerAdapter(comparer), comparer?.GetType().Name, hasComparerArgument: true, SequenceOrder.InOrder, hasOrderArgument: false, message, expectedExpression, actualExpression);
    }

    /// <summary>
    /// Tests whether two sequences contain equal elements using the specified comparer and order semantics and throws an exception if they do not.
    /// </summary>
    /// <param name="expected">The sequence expected to be equal to <paramref name="actual"/>.</param>
    /// <param name="actual">The sequence produced by the code under test.</param>
    /// <param name="comparer">The equality comparer to use when comparing elements, or <see langword="null"/> to use the default comparer.</param>
    /// <param name="order">Specifies whether elements must appear in the same order or in any order.</param>
    /// <param name="message">The message to include in the exception when the sequences are not equal.</param>
    /// <param name="expectedExpression">The syntactic expression of expected as given by the compiler via caller argument expression.</param>
    /// <param name="actualExpression">The syntactic expression of actual as given by the compiler via caller argument expression.</param>
    public static void AreSequenceEqual(IEnumerable? expected, IEnumerable? actual, IEqualityComparer? comparer, SequenceOrder order, string? message = "", [CallerArgumentExpression(nameof(expected))] string expectedExpression = "", [CallerArgumentExpression(nameof(actual))] string actualExpression = "")
    {
        TelemetryCollector.TrackAssertionCall("Assert.AreSequenceEqual");
        AreSequenceEqualImpl(expected?.Cast<object?>(), actual?.Cast<object?>(), comparer is null ? EqualityComparer<object?>.Default : new NonGenericEqualityComparerAdapter(comparer), comparer?.GetType().Name, hasComparerArgument: true, order, hasOrderArgument: true, message, expectedExpression, actualExpression);
    }

    #endregion // AreSequenceEqual

#if NETCOREAPP3_1_OR_GREATER

    #region AreSequenceEqual span/memory

    /// <summary>
    /// Tests whether two spans contain equal elements in the same order and throws an exception if they do not.
    /// </summary>
    /// <typeparam name="T">The type of sequence elements.</typeparam>
    /// <param name="expected">The span expected to be equal to <paramref name="actual"/>.</param>
    /// <param name="actual">The span produced by the code under test.</param>
    /// <param name="message">The message to include in the exception when the sequences are not equal.</param>
    /// <param name="expectedExpression">The syntactic expression of expected as given by the compiler via caller argument expression.</param>
    /// <param name="actualExpression">The syntactic expression of actual as given by the compiler via caller argument expression.</param>
    public static void AreSequenceEqual<T>(ReadOnlySpan<T> expected, ReadOnlySpan<T> actual, string? message = "", [CallerArgumentExpression(nameof(expected))] string expectedExpression = "", [CallerArgumentExpression(nameof(actual))] string actualExpression = "")
    {
        TelemetryCollector.TrackAssertionCall("Assert.AreSequenceEqual");
        AreSequenceEqualImpl(expected.ToArray(), actual.ToArray(), EqualityComparer<T>.Default, comparerName: null, hasComparerArgument: false, SequenceOrder.InOrder, hasOrderArgument: false, message, expectedExpression, actualExpression);
    }

    /// <summary>
    /// Tests whether two spans contain equal elements using the specified order semantics and throws an exception if they do not.
    /// </summary>
    /// <typeparam name="T">The type of sequence elements.</typeparam>
    /// <param name="expected">The span expected to be equal to <paramref name="actual"/>.</param>
    /// <param name="actual">The span produced by the code under test.</param>
    /// <param name="order">Specifies whether elements must appear in the same order or in any order.</param>
    /// <param name="message">The message to include in the exception when the sequences are not equal.</param>
    /// <param name="expectedExpression">The syntactic expression of expected as given by the compiler via caller argument expression.</param>
    /// <param name="actualExpression">The syntactic expression of actual as given by the compiler via caller argument expression.</param>
    public static void AreSequenceEqual<T>(ReadOnlySpan<T> expected, ReadOnlySpan<T> actual, SequenceOrder order, string? message = "", [CallerArgumentExpression(nameof(expected))] string expectedExpression = "", [CallerArgumentExpression(nameof(actual))] string actualExpression = "")
    {
        TelemetryCollector.TrackAssertionCall("Assert.AreSequenceEqual");
        AreSequenceEqualImpl(expected.ToArray(), actual.ToArray(), EqualityComparer<T>.Default, comparerName: null, hasComparerArgument: false, order, hasOrderArgument: true, message, expectedExpression, actualExpression);
    }

    /// <summary>
    /// Tests whether two spans contain equal elements in the same order using the specified comparer and throws an exception if they do not.
    /// </summary>
    /// <typeparam name="T">The type of sequence elements.</typeparam>
    /// <param name="expected">The span expected to be equal to <paramref name="actual"/>.</param>
    /// <param name="actual">The span produced by the code under test.</param>
    /// <param name="comparer">The equality comparer to use when comparing elements, or <see langword="null"/> to use the default comparer.</param>
    /// <param name="message">The message to include in the exception when the sequences are not equal.</param>
    /// <param name="expectedExpression">The syntactic expression of expected as given by the compiler via caller argument expression.</param>
    /// <param name="actualExpression">The syntactic expression of actual as given by the compiler via caller argument expression.</param>
    public static void AreSequenceEqual<T>(ReadOnlySpan<T> expected, ReadOnlySpan<T> actual, IEqualityComparer<T>? comparer, string? message = "", [CallerArgumentExpression(nameof(expected))] string expectedExpression = "", [CallerArgumentExpression(nameof(actual))] string actualExpression = "")
    {
        TelemetryCollector.TrackAssertionCall("Assert.AreSequenceEqual");
        AreSequenceEqualImpl(expected.ToArray(), actual.ToArray(), comparer ?? EqualityComparer<T>.Default, comparer?.GetType().Name, hasComparerArgument: true, SequenceOrder.InOrder, hasOrderArgument: false, message, expectedExpression, actualExpression);
    }

    /// <summary>
    /// Tests whether two spans contain equal elements using the specified comparer and order semantics and throws an exception if they do not.
    /// </summary>
    /// <typeparam name="T">The type of sequence elements.</typeparam>
    /// <param name="expected">The span expected to be equal to <paramref name="actual"/>.</param>
    /// <param name="actual">The span produced by the code under test.</param>
    /// <param name="comparer">The equality comparer to use when comparing elements, or <see langword="null"/> to use the default comparer.</param>
    /// <param name="order">Specifies whether elements must appear in the same order or in any order.</param>
    /// <param name="message">The message to include in the exception when the sequences are not equal.</param>
    /// <param name="expectedExpression">The syntactic expression of expected as given by the compiler via caller argument expression.</param>
    /// <param name="actualExpression">The syntactic expression of actual as given by the compiler via caller argument expression.</param>
    public static void AreSequenceEqual<T>(ReadOnlySpan<T> expected, ReadOnlySpan<T> actual, IEqualityComparer<T>? comparer, SequenceOrder order, string? message = "", [CallerArgumentExpression(nameof(expected))] string expectedExpression = "", [CallerArgumentExpression(nameof(actual))] string actualExpression = "")
    {
        TelemetryCollector.TrackAssertionCall("Assert.AreSequenceEqual");
        AreSequenceEqualImpl(expected.ToArray(), actual.ToArray(), comparer ?? EqualityComparer<T>.Default, comparer?.GetType().Name, hasComparerArgument: true, order, hasOrderArgument: true, message, expectedExpression, actualExpression);
    }

    /// <summary>
    /// Tests whether two spans contain equal elements in the same order and throws an exception if they do not.
    /// </summary>
    /// <typeparam name="T">The type of sequence elements.</typeparam>
    /// <param name="expected">The span expected to be equal to <paramref name="actual"/>.</param>
    /// <param name="actual">The span produced by the code under test.</param>
    /// <param name="message">The message to include in the exception when the sequences are not equal.</param>
    /// <param name="expectedExpression">The syntactic expression of expected as given by the compiler via caller argument expression.</param>
    /// <param name="actualExpression">The syntactic expression of actual as given by the compiler via caller argument expression.</param>
    public static void AreSequenceEqual<T>(Span<T> expected, Span<T> actual, string? message = "", [CallerArgumentExpression(nameof(expected))] string expectedExpression = "", [CallerArgumentExpression(nameof(actual))] string actualExpression = "")
        => AreSequenceEqual((ReadOnlySpan<T>)expected, actual, message, expectedExpression, actualExpression);

    /// <summary>
    /// Tests whether two spans contain equal elements using the specified order semantics and throws an exception if they do not.
    /// </summary>
    /// <typeparam name="T">The type of sequence elements.</typeparam>
    /// <param name="expected">The span expected to be equal to <paramref name="actual"/>.</param>
    /// <param name="actual">The span produced by the code under test.</param>
    /// <param name="order">Specifies whether elements must appear in the same order or in any order.</param>
    /// <param name="message">The message to include in the exception when the sequences are not equal.</param>
    /// <param name="expectedExpression">The syntactic expression of expected as given by the compiler via caller argument expression.</param>
    /// <param name="actualExpression">The syntactic expression of actual as given by the compiler via caller argument expression.</param>
    public static void AreSequenceEqual<T>(Span<T> expected, Span<T> actual, SequenceOrder order, string? message = "", [CallerArgumentExpression(nameof(expected))] string expectedExpression = "", [CallerArgumentExpression(nameof(actual))] string actualExpression = "")
        => AreSequenceEqual((ReadOnlySpan<T>)expected, actual, order, message, expectedExpression, actualExpression);

    /// <summary>
    /// Tests whether two spans contain equal elements in the same order using the specified comparer and throws an exception if they do not.
    /// </summary>
    /// <typeparam name="T">The type of sequence elements.</typeparam>
    /// <param name="expected">The span expected to be equal to <paramref name="actual"/>.</param>
    /// <param name="actual">The span produced by the code under test.</param>
    /// <param name="comparer">The equality comparer to use when comparing elements, or <see langword="null"/> to use the default comparer.</param>
    /// <param name="message">The message to include in the exception when the sequences are not equal.</param>
    /// <param name="expectedExpression">The syntactic expression of expected as given by the compiler via caller argument expression.</param>
    /// <param name="actualExpression">The syntactic expression of actual as given by the compiler via caller argument expression.</param>
    public static void AreSequenceEqual<T>(Span<T> expected, Span<T> actual, IEqualityComparer<T>? comparer, string? message = "", [CallerArgumentExpression(nameof(expected))] string expectedExpression = "", [CallerArgumentExpression(nameof(actual))] string actualExpression = "")
        => AreSequenceEqual((ReadOnlySpan<T>)expected, actual, comparer, message, expectedExpression, actualExpression);

    /// <summary>
    /// Tests whether two spans contain equal elements using the specified comparer and order semantics and throws an exception if they do not.
    /// </summary>
    /// <typeparam name="T">The type of sequence elements.</typeparam>
    /// <param name="expected">The span expected to be equal to <paramref name="actual"/>.</param>
    /// <param name="actual">The span produced by the code under test.</param>
    /// <param name="comparer">The equality comparer to use when comparing elements, or <see langword="null"/> to use the default comparer.</param>
    /// <param name="order">Specifies whether elements must appear in the same order or in any order.</param>
    /// <param name="message">The message to include in the exception when the sequences are not equal.</param>
    /// <param name="expectedExpression">The syntactic expression of expected as given by the compiler via caller argument expression.</param>
    /// <param name="actualExpression">The syntactic expression of actual as given by the compiler via caller argument expression.</param>
    public static void AreSequenceEqual<T>(Span<T> expected, Span<T> actual, IEqualityComparer<T>? comparer, SequenceOrder order, string? message = "", [CallerArgumentExpression(nameof(expected))] string expectedExpression = "", [CallerArgumentExpression(nameof(actual))] string actualExpression = "")
        => AreSequenceEqual((ReadOnlySpan<T>)expected, actual, comparer, order, message, expectedExpression, actualExpression);

    /// <summary>
    /// Tests whether two memory regions contain equal elements in the same order and throws an exception if they do not.
    /// </summary>
    /// <typeparam name="T">The type of sequence elements.</typeparam>
    /// <param name="expected">The memory expected to be equal to <paramref name="actual"/>.</param>
    /// <param name="actual">The memory produced by the code under test.</param>
    /// <param name="message">The message to include in the exception when the sequences are not equal.</param>
    /// <param name="expectedExpression">The syntactic expression of expected as given by the compiler via caller argument expression.</param>
    /// <param name="actualExpression">The syntactic expression of actual as given by the compiler via caller argument expression.</param>
    public static void AreSequenceEqual<T>(ReadOnlyMemory<T> expected, ReadOnlyMemory<T> actual, string? message = "", [CallerArgumentExpression(nameof(expected))] string expectedExpression = "", [CallerArgumentExpression(nameof(actual))] string actualExpression = "")
        => AreSequenceEqual(expected.Span, actual.Span, message, expectedExpression, actualExpression);

    /// <summary>
    /// Tests whether two memory regions contain equal elements using the specified order semantics and throws an exception if they do not.
    /// </summary>
    /// <typeparam name="T">The type of sequence elements.</typeparam>
    /// <param name="expected">The memory expected to be equal to <paramref name="actual"/>.</param>
    /// <param name="actual">The memory produced by the code under test.</param>
    /// <param name="order">Specifies whether elements must appear in the same order or in any order.</param>
    /// <param name="message">The message to include in the exception when the sequences are not equal.</param>
    /// <param name="expectedExpression">The syntactic expression of expected as given by the compiler via caller argument expression.</param>
    /// <param name="actualExpression">The syntactic expression of actual as given by the compiler via caller argument expression.</param>
    public static void AreSequenceEqual<T>(ReadOnlyMemory<T> expected, ReadOnlyMemory<T> actual, SequenceOrder order, string? message = "", [CallerArgumentExpression(nameof(expected))] string expectedExpression = "", [CallerArgumentExpression(nameof(actual))] string actualExpression = "")
        => AreSequenceEqual(expected.Span, actual.Span, order, message, expectedExpression, actualExpression);

    /// <summary>
    /// Tests whether two memory regions contain equal elements in the same order using the specified comparer and throws an exception if they do not.
    /// </summary>
    /// <typeparam name="T">The type of sequence elements.</typeparam>
    /// <param name="expected">The memory expected to be equal to <paramref name="actual"/>.</param>
    /// <param name="actual">The memory produced by the code under test.</param>
    /// <param name="comparer">The equality comparer to use when comparing elements, or <see langword="null"/> to use the default comparer.</param>
    /// <param name="message">The message to include in the exception when the sequences are not equal.</param>
    /// <param name="expectedExpression">The syntactic expression of expected as given by the compiler via caller argument expression.</param>
    /// <param name="actualExpression">The syntactic expression of actual as given by the compiler via caller argument expression.</param>
    public static void AreSequenceEqual<T>(ReadOnlyMemory<T> expected, ReadOnlyMemory<T> actual, IEqualityComparer<T>? comparer, string? message = "", [CallerArgumentExpression(nameof(expected))] string expectedExpression = "", [CallerArgumentExpression(nameof(actual))] string actualExpression = "")
        => AreSequenceEqual(expected.Span, actual.Span, comparer, message, expectedExpression, actualExpression);

    /// <summary>
    /// Tests whether two memory regions contain equal elements using the specified comparer and order semantics and throws an exception if they do not.
    /// </summary>
    /// <typeparam name="T">The type of sequence elements.</typeparam>
    /// <param name="expected">The memory expected to be equal to <paramref name="actual"/>.</param>
    /// <param name="actual">The memory produced by the code under test.</param>
    /// <param name="comparer">The equality comparer to use when comparing elements, or <see langword="null"/> to use the default comparer.</param>
    /// <param name="order">Specifies whether elements must appear in the same order or in any order.</param>
    /// <param name="message">The message to include in the exception when the sequences are not equal.</param>
    /// <param name="expectedExpression">The syntactic expression of expected as given by the compiler via caller argument expression.</param>
    /// <param name="actualExpression">The syntactic expression of actual as given by the compiler via caller argument expression.</param>
    public static void AreSequenceEqual<T>(ReadOnlyMemory<T> expected, ReadOnlyMemory<T> actual, IEqualityComparer<T>? comparer, SequenceOrder order, string? message = "", [CallerArgumentExpression(nameof(expected))] string expectedExpression = "", [CallerArgumentExpression(nameof(actual))] string actualExpression = "")
        => AreSequenceEqual(expected.Span, actual.Span, comparer, order, message, expectedExpression, actualExpression);

    /// <summary>
    /// Tests whether two memory regions contain equal elements in the same order and throws an exception if they do not.
    /// </summary>
    /// <typeparam name="T">The type of sequence elements.</typeparam>
    /// <param name="expected">The memory expected to be equal to <paramref name="actual"/>.</param>
    /// <param name="actual">The memory produced by the code under test.</param>
    /// <param name="message">The message to include in the exception when the sequences are not equal.</param>
    /// <param name="expectedExpression">The syntactic expression of expected as given by the compiler via caller argument expression.</param>
    /// <param name="actualExpression">The syntactic expression of actual as given by the compiler via caller argument expression.</param>
    public static void AreSequenceEqual<T>(Memory<T> expected, Memory<T> actual, string? message = "", [CallerArgumentExpression(nameof(expected))] string expectedExpression = "", [CallerArgumentExpression(nameof(actual))] string actualExpression = "")
        => AreSequenceEqual(expected.Span, actual.Span, message, expectedExpression, actualExpression);

    /// <summary>
    /// Tests whether two memory regions contain equal elements using the specified order semantics and throws an exception if they do not.
    /// </summary>
    /// <typeparam name="T">The type of sequence elements.</typeparam>
    /// <param name="expected">The memory expected to be equal to <paramref name="actual"/>.</param>
    /// <param name="actual">The memory produced by the code under test.</param>
    /// <param name="order">Specifies whether elements must appear in the same order or in any order.</param>
    /// <param name="message">The message to include in the exception when the sequences are not equal.</param>
    /// <param name="expectedExpression">The syntactic expression of expected as given by the compiler via caller argument expression.</param>
    /// <param name="actualExpression">The syntactic expression of actual as given by the compiler via caller argument expression.</param>
    public static void AreSequenceEqual<T>(Memory<T> expected, Memory<T> actual, SequenceOrder order, string? message = "", [CallerArgumentExpression(nameof(expected))] string expectedExpression = "", [CallerArgumentExpression(nameof(actual))] string actualExpression = "")
        => AreSequenceEqual(expected.Span, actual.Span, order, message, expectedExpression, actualExpression);

    /// <summary>
    /// Tests whether two memory regions contain equal elements in the same order using the specified comparer and throws an exception if they do not.
    /// </summary>
    /// <typeparam name="T">The type of sequence elements.</typeparam>
    /// <param name="expected">The memory expected to be equal to <paramref name="actual"/>.</param>
    /// <param name="actual">The memory produced by the code under test.</param>
    /// <param name="comparer">The equality comparer to use when comparing elements, or <see langword="null"/> to use the default comparer.</param>
    /// <param name="message">The message to include in the exception when the sequences are not equal.</param>
    /// <param name="expectedExpression">The syntactic expression of expected as given by the compiler via caller argument expression.</param>
    /// <param name="actualExpression">The syntactic expression of actual as given by the compiler via caller argument expression.</param>
    public static void AreSequenceEqual<T>(Memory<T> expected, Memory<T> actual, IEqualityComparer<T>? comparer, string? message = "", [CallerArgumentExpression(nameof(expected))] string expectedExpression = "", [CallerArgumentExpression(nameof(actual))] string actualExpression = "")
        => AreSequenceEqual(expected.Span, actual.Span, comparer, message, expectedExpression, actualExpression);

    /// <summary>
    /// Tests whether two memory regions contain equal elements using the specified comparer and order semantics and throws an exception if they do not.
    /// </summary>
    /// <typeparam name="T">The type of sequence elements.</typeparam>
    /// <param name="expected">The memory expected to be equal to <paramref name="actual"/>.</param>
    /// <param name="actual">The memory produced by the code under test.</param>
    /// <param name="comparer">The equality comparer to use when comparing elements, or <see langword="null"/> to use the default comparer.</param>
    /// <param name="order">Specifies whether elements must appear in the same order or in any order.</param>
    /// <param name="message">The message to include in the exception when the sequences are not equal.</param>
    /// <param name="expectedExpression">The syntactic expression of expected as given by the compiler via caller argument expression.</param>
    /// <param name="actualExpression">The syntactic expression of actual as given by the compiler via caller argument expression.</param>
    public static void AreSequenceEqual<T>(Memory<T> expected, Memory<T> actual, IEqualityComparer<T>? comparer, SequenceOrder order, string? message = "", [CallerArgumentExpression(nameof(expected))] string expectedExpression = "", [CallerArgumentExpression(nameof(actual))] string actualExpression = "")
        => AreSequenceEqual(expected.Span, actual.Span, comparer, order, message, expectedExpression, actualExpression);

    #endregion // AreSequenceEqual span/memory

#endif
}
