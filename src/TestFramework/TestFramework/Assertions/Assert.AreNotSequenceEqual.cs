// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestTools.UnitTesting;

#pragma warning disable RS0026 // Do not add multiple public overloads with optional parameters
#pragma warning disable RS0027 // API with optional parameter(s) should have the most parameters amongst its public overloads
public sealed partial class Assert
{
    #region AreNotSequenceEqual

    /// <summary>
    /// Tests whether two sequences differ in the same order and throws an exception if they do not.
    /// </summary>
    /// <typeparam name="T">The type of sequence elements.</typeparam>
    /// <param name="notExpected">The sequence not expected to equal <paramref name="actual"/>.</param>
    /// <param name="actual">The sequence produced by the code under test.</param>
    /// <param name="message">The message to include in the exception when the sequences are equal.</param>
    /// <param name="notExpectedExpression">The syntactic expression of notExpected as given by the compiler via caller argument expression.</param>
    /// <param name="actualExpression">The syntactic expression of actual as given by the compiler via caller argument expression.</param>
    public static void AreNotSequenceEqual<T>(IEnumerable<T>? notExpected, IEnumerable<T>? actual, string? message = "", [CallerArgumentExpression(nameof(notExpected))] string notExpectedExpression = "", [CallerArgumentExpression(nameof(actual))] string actualExpression = "")
    {
        TelemetryCollector.TrackAssertionCall("Assert.AreNotSequenceEqual");
        AreNotSequenceEqualImpl(notExpected, actual, EqualityComparer<T>.Default, comparerName: null, hasComparerArgument: false, SequenceOrder.InOrder, hasOrderArgument: false, message, notExpectedExpression, actualExpression);
    }

    /// <summary>
    /// Tests whether two sequences differ using the specified order semantics and throws an exception if they do not.
    /// </summary>
    /// <typeparam name="T">The type of sequence elements.</typeparam>
    /// <param name="notExpected">The sequence not expected to equal <paramref name="actual"/>.</param>
    /// <param name="actual">The sequence produced by the code under test.</param>
    /// <param name="order">Specifies whether elements must appear in the same order or in any order.</param>
    /// <param name="message">The message to include in the exception when the sequences are equal.</param>
    /// <param name="notExpectedExpression">The syntactic expression of notExpected as given by the compiler via caller argument expression.</param>
    /// <param name="actualExpression">The syntactic expression of actual as given by the compiler via caller argument expression.</param>
    public static void AreNotSequenceEqual<T>(IEnumerable<T>? notExpected, IEnumerable<T>? actual, SequenceOrder order, string? message = "", [CallerArgumentExpression(nameof(notExpected))] string notExpectedExpression = "", [CallerArgumentExpression(nameof(actual))] string actualExpression = "")
    {
        TelemetryCollector.TrackAssertionCall("Assert.AreNotSequenceEqual");
        AreNotSequenceEqualImpl(notExpected, actual, EqualityComparer<T>.Default, comparerName: null, hasComparerArgument: false, order, hasOrderArgument: true, message, notExpectedExpression, actualExpression);
    }

    /// <summary>
    /// Tests whether two sequences differ in the same order using the specified comparer and throws an exception if they do not.
    /// </summary>
    /// <typeparam name="T">The type of sequence elements.</typeparam>
    /// <param name="notExpected">The sequence not expected to equal <paramref name="actual"/>.</param>
    /// <param name="actual">The sequence produced by the code under test.</param>
    /// <param name="comparer">The equality comparer to use when comparing elements, or <see langword="null"/> to use the default comparer.</param>
    /// <param name="message">The message to include in the exception when the sequences are equal.</param>
    /// <param name="notExpectedExpression">The syntactic expression of notExpected as given by the compiler via caller argument expression.</param>
    /// <param name="actualExpression">The syntactic expression of actual as given by the compiler via caller argument expression.</param>
    public static void AreNotSequenceEqual<T>(IEnumerable<T>? notExpected, IEnumerable<T>? actual, IEqualityComparer<T>? comparer, string? message = "", [CallerArgumentExpression(nameof(notExpected))] string notExpectedExpression = "", [CallerArgumentExpression(nameof(actual))] string actualExpression = "")
    {
        TelemetryCollector.TrackAssertionCall("Assert.AreNotSequenceEqual");
        AreNotSequenceEqualImpl(notExpected, actual, comparer ?? EqualityComparer<T>.Default, comparer?.GetType().Name, hasComparerArgument: true, SequenceOrder.InOrder, hasOrderArgument: false, message, notExpectedExpression, actualExpression);
    }

    /// <summary>
    /// Tests whether two sequences differ using the specified comparer and order semantics and throws an exception if they do not.
    /// </summary>
    /// <typeparam name="T">The type of sequence elements.</typeparam>
    /// <param name="notExpected">The sequence not expected to equal <paramref name="actual"/>.</param>
    /// <param name="actual">The sequence produced by the code under test.</param>
    /// <param name="comparer">The equality comparer to use when comparing elements, or <see langword="null"/> to use the default comparer.</param>
    /// <param name="order">Specifies whether elements must appear in the same order or in any order.</param>
    /// <param name="message">The message to include in the exception when the sequences are equal.</param>
    /// <param name="notExpectedExpression">The syntactic expression of notExpected as given by the compiler via caller argument expression.</param>
    /// <param name="actualExpression">The syntactic expression of actual as given by the compiler via caller argument expression.</param>
    public static void AreNotSequenceEqual<T>(IEnumerable<T>? notExpected, IEnumerable<T>? actual, IEqualityComparer<T>? comparer, SequenceOrder order, string? message = "", [CallerArgumentExpression(nameof(notExpected))] string notExpectedExpression = "", [CallerArgumentExpression(nameof(actual))] string actualExpression = "")
    {
        TelemetryCollector.TrackAssertionCall("Assert.AreNotSequenceEqual");
        AreNotSequenceEqualImpl(notExpected, actual, comparer ?? EqualityComparer<T>.Default, comparer?.GetType().Name, hasComparerArgument: true, order, hasOrderArgument: true, message, notExpectedExpression, actualExpression);
    }

    /// <summary>
    /// Tests whether two sequences differ in the same order and throws an exception if they do not.
    /// </summary>
    /// <param name="notExpected">The sequence not expected to equal <paramref name="actual"/>.</param>
    /// <param name="actual">The sequence produced by the code under test.</param>
    /// <param name="message">The message to include in the exception when the sequences are equal.</param>
    /// <param name="notExpectedExpression">The syntactic expression of notExpected as given by the compiler via caller argument expression.</param>
    /// <param name="actualExpression">The syntactic expression of actual as given by the compiler via caller argument expression.</param>
    public static void AreNotSequenceEqual(IEnumerable? notExpected, IEnumerable? actual, string? message = "", [CallerArgumentExpression(nameof(notExpected))] string notExpectedExpression = "", [CallerArgumentExpression(nameof(actual))] string actualExpression = "")
    {
        TelemetryCollector.TrackAssertionCall("Assert.AreNotSequenceEqual");
        AreNotSequenceEqualImpl(notExpected?.Cast<object?>(), actual?.Cast<object?>(), EqualityComparer<object?>.Default, comparerName: null, hasComparerArgument: false, SequenceOrder.InOrder, hasOrderArgument: false, message, notExpectedExpression, actualExpression);
    }

    /// <summary>
    /// Tests whether two sequences differ using the specified order semantics and throws an exception if they do not.
    /// </summary>
    /// <param name="notExpected">The sequence not expected to equal <paramref name="actual"/>.</param>
    /// <param name="actual">The sequence produced by the code under test.</param>
    /// <param name="order">Specifies whether elements must appear in the same order or in any order.</param>
    /// <param name="message">The message to include in the exception when the sequences are equal.</param>
    /// <param name="notExpectedExpression">The syntactic expression of notExpected as given by the compiler via caller argument expression.</param>
    /// <param name="actualExpression">The syntactic expression of actual as given by the compiler via caller argument expression.</param>
    public static void AreNotSequenceEqual(IEnumerable? notExpected, IEnumerable? actual, SequenceOrder order, string? message = "", [CallerArgumentExpression(nameof(notExpected))] string notExpectedExpression = "", [CallerArgumentExpression(nameof(actual))] string actualExpression = "")
    {
        TelemetryCollector.TrackAssertionCall("Assert.AreNotSequenceEqual");
        AreNotSequenceEqualImpl(notExpected?.Cast<object?>(), actual?.Cast<object?>(), EqualityComparer<object?>.Default, comparerName: null, hasComparerArgument: false, order, hasOrderArgument: true, message, notExpectedExpression, actualExpression);
    }

    /// <summary>
    /// Tests whether two sequences differ in the same order using the specified comparer and throws an exception if they do not.
    /// </summary>
    /// <param name="notExpected">The sequence not expected to equal <paramref name="actual"/>.</param>
    /// <param name="actual">The sequence produced by the code under test.</param>
    /// <param name="comparer">The equality comparer to use when comparing elements, or <see langword="null"/> to use the default comparer.</param>
    /// <param name="message">The message to include in the exception when the sequences are equal.</param>
    /// <param name="notExpectedExpression">The syntactic expression of notExpected as given by the compiler via caller argument expression.</param>
    /// <param name="actualExpression">The syntactic expression of actual as given by the compiler via caller argument expression.</param>
    public static void AreNotSequenceEqual(IEnumerable? notExpected, IEnumerable? actual, IEqualityComparer? comparer, string? message = "", [CallerArgumentExpression(nameof(notExpected))] string notExpectedExpression = "", [CallerArgumentExpression(nameof(actual))] string actualExpression = "")
    {
        TelemetryCollector.TrackAssertionCall("Assert.AreNotSequenceEqual");
        AreNotSequenceEqualImpl(notExpected?.Cast<object?>(), actual?.Cast<object?>(), comparer is null ? EqualityComparer<object?>.Default : new NonGenericEqualityComparerAdapter(comparer), comparer?.GetType().Name, hasComparerArgument: true, SequenceOrder.InOrder, hasOrderArgument: false, message, notExpectedExpression, actualExpression);
    }

    /// <summary>
    /// Tests whether two sequences differ using the specified comparer and order semantics and throws an exception if they do not.
    /// </summary>
    /// <param name="notExpected">The sequence not expected to equal <paramref name="actual"/>.</param>
    /// <param name="actual">The sequence produced by the code under test.</param>
    /// <param name="comparer">The equality comparer to use when comparing elements, or <see langword="null"/> to use the default comparer.</param>
    /// <param name="order">Specifies whether elements must appear in the same order or in any order.</param>
    /// <param name="message">The message to include in the exception when the sequences are equal.</param>
    /// <param name="notExpectedExpression">The syntactic expression of notExpected as given by the compiler via caller argument expression.</param>
    /// <param name="actualExpression">The syntactic expression of actual as given by the compiler via caller argument expression.</param>
    public static void AreNotSequenceEqual(IEnumerable? notExpected, IEnumerable? actual, IEqualityComparer? comparer, SequenceOrder order, string? message = "", [CallerArgumentExpression(nameof(notExpected))] string notExpectedExpression = "", [CallerArgumentExpression(nameof(actual))] string actualExpression = "")
    {
        TelemetryCollector.TrackAssertionCall("Assert.AreNotSequenceEqual");
        AreNotSequenceEqualImpl(notExpected?.Cast<object?>(), actual?.Cast<object?>(), comparer is null ? EqualityComparer<object?>.Default : new NonGenericEqualityComparerAdapter(comparer), comparer?.GetType().Name, hasComparerArgument: true, order, hasOrderArgument: true, message, notExpectedExpression, actualExpression);
    }

    #endregion // AreNotSequenceEqual

#if NETCOREAPP3_1_OR_GREATER

    #region AreNotSequenceEqual span/memory

    /// <summary>
    /// Tests whether two spans differ in the same order and throws an exception if they do not.
    /// </summary>
    /// <typeparam name="T">The type of sequence elements.</typeparam>
    /// <param name="notExpected">The span not expected to equal <paramref name="actual"/>.</param>
    /// <param name="actual">The span produced by the code under test.</param>
    /// <param name="message">The message to include in the exception when the sequences are equal.</param>
    /// <param name="notExpectedExpression">The syntactic expression of notExpected as given by the compiler via caller argument expression.</param>
    /// <param name="actualExpression">The syntactic expression of actual as given by the compiler via caller argument expression.</param>
    public static void AreNotSequenceEqual<T>(ReadOnlySpan<T> notExpected, ReadOnlySpan<T> actual, string? message = "", [CallerArgumentExpression(nameof(notExpected))] string notExpectedExpression = "", [CallerArgumentExpression(nameof(actual))] string actualExpression = "")
    {
        TelemetryCollector.TrackAssertionCall("Assert.AreNotSequenceEqual");
        AreNotSequenceEqualImpl(notExpected.ToArray(), actual.ToArray(), EqualityComparer<T>.Default, comparerName: null, hasComparerArgument: false, SequenceOrder.InOrder, hasOrderArgument: false, message, notExpectedExpression, actualExpression);
    }

    /// <summary>
    /// Tests whether two spans differ using the specified order semantics and throws an exception if they do not.
    /// </summary>
    /// <typeparam name="T">The type of sequence elements.</typeparam>
    /// <param name="notExpected">The span not expected to equal <paramref name="actual"/>.</param>
    /// <param name="actual">The span produced by the code under test.</param>
    /// <param name="order">Specifies whether elements must appear in the same order or in any order.</param>
    /// <param name="message">The message to include in the exception when the sequences are equal.</param>
    /// <param name="notExpectedExpression">The syntactic expression of notExpected as given by the compiler via caller argument expression.</param>
    /// <param name="actualExpression">The syntactic expression of actual as given by the compiler via caller argument expression.</param>
    public static void AreNotSequenceEqual<T>(ReadOnlySpan<T> notExpected, ReadOnlySpan<T> actual, SequenceOrder order, string? message = "", [CallerArgumentExpression(nameof(notExpected))] string notExpectedExpression = "", [CallerArgumentExpression(nameof(actual))] string actualExpression = "")
    {
        TelemetryCollector.TrackAssertionCall("Assert.AreNotSequenceEqual");
        AreNotSequenceEqualImpl(notExpected.ToArray(), actual.ToArray(), EqualityComparer<T>.Default, comparerName: null, hasComparerArgument: false, order, hasOrderArgument: true, message, notExpectedExpression, actualExpression);
    }

    /// <summary>
    /// Tests whether two spans differ in the same order using the specified comparer and throws an exception if they do not.
    /// </summary>
    /// <typeparam name="T">The type of sequence elements.</typeparam>
    /// <param name="notExpected">The span not expected to equal <paramref name="actual"/>.</param>
    /// <param name="actual">The span produced by the code under test.</param>
    /// <param name="comparer">The equality comparer to use when comparing elements, or <see langword="null"/> to use the default comparer.</param>
    /// <param name="message">The message to include in the exception when the sequences are equal.</param>
    /// <param name="notExpectedExpression">The syntactic expression of notExpected as given by the compiler via caller argument expression.</param>
    /// <param name="actualExpression">The syntactic expression of actual as given by the compiler via caller argument expression.</param>
    public static void AreNotSequenceEqual<T>(ReadOnlySpan<T> notExpected, ReadOnlySpan<T> actual, IEqualityComparer<T>? comparer, string? message = "", [CallerArgumentExpression(nameof(notExpected))] string notExpectedExpression = "", [CallerArgumentExpression(nameof(actual))] string actualExpression = "")
    {
        TelemetryCollector.TrackAssertionCall("Assert.AreNotSequenceEqual");
        AreNotSequenceEqualImpl(notExpected.ToArray(), actual.ToArray(), comparer ?? EqualityComparer<T>.Default, comparer?.GetType().Name, hasComparerArgument: true, SequenceOrder.InOrder, hasOrderArgument: false, message, notExpectedExpression, actualExpression);
    }

    /// <summary>
    /// Tests whether two spans differ using the specified comparer and order semantics and throws an exception if they do not.
    /// </summary>
    /// <typeparam name="T">The type of sequence elements.</typeparam>
    /// <param name="notExpected">The span not expected to equal <paramref name="actual"/>.</param>
    /// <param name="actual">The span produced by the code under test.</param>
    /// <param name="comparer">The equality comparer to use when comparing elements, or <see langword="null"/> to use the default comparer.</param>
    /// <param name="order">Specifies whether elements must appear in the same order or in any order.</param>
    /// <param name="message">The message to include in the exception when the sequences are equal.</param>
    /// <param name="notExpectedExpression">The syntactic expression of notExpected as given by the compiler via caller argument expression.</param>
    /// <param name="actualExpression">The syntactic expression of actual as given by the compiler via caller argument expression.</param>
    public static void AreNotSequenceEqual<T>(ReadOnlySpan<T> notExpected, ReadOnlySpan<T> actual, IEqualityComparer<T>? comparer, SequenceOrder order, string? message = "", [CallerArgumentExpression(nameof(notExpected))] string notExpectedExpression = "", [CallerArgumentExpression(nameof(actual))] string actualExpression = "")
    {
        TelemetryCollector.TrackAssertionCall("Assert.AreNotSequenceEqual");
        AreNotSequenceEqualImpl(notExpected.ToArray(), actual.ToArray(), comparer ?? EqualityComparer<T>.Default, comparer?.GetType().Name, hasComparerArgument: true, order, hasOrderArgument: true, message, notExpectedExpression, actualExpression);
    }

    /// <summary>
    /// Tests whether two spans differ in the same order and throws an exception if they do not.
    /// </summary>
    /// <typeparam name="T">The type of sequence elements.</typeparam>
    /// <param name="notExpected">The span not expected to equal <paramref name="actual"/>.</param>
    /// <param name="actual">The span produced by the code under test.</param>
    /// <param name="message">The message to include in the exception when the sequences are equal.</param>
    /// <param name="notExpectedExpression">The syntactic expression of notExpected as given by the compiler via caller argument expression.</param>
    /// <param name="actualExpression">The syntactic expression of actual as given by the compiler via caller argument expression.</param>
    public static void AreNotSequenceEqual<T>(Span<T> notExpected, Span<T> actual, string? message = "", [CallerArgumentExpression(nameof(notExpected))] string notExpectedExpression = "", [CallerArgumentExpression(nameof(actual))] string actualExpression = "")
        => AreNotSequenceEqual((ReadOnlySpan<T>)notExpected, actual, message, notExpectedExpression, actualExpression);

    /// <summary>
    /// Tests whether two spans differ using the specified order semantics and throws an exception if they do not.
    /// </summary>
    /// <typeparam name="T">The type of sequence elements.</typeparam>
    /// <param name="notExpected">The span not expected to equal <paramref name="actual"/>.</param>
    /// <param name="actual">The span produced by the code under test.</param>
    /// <param name="order">Specifies whether elements must appear in the same order or in any order.</param>
    /// <param name="message">The message to include in the exception when the sequences are equal.</param>
    /// <param name="notExpectedExpression">The syntactic expression of notExpected as given by the compiler via caller argument expression.</param>
    /// <param name="actualExpression">The syntactic expression of actual as given by the compiler via caller argument expression.</param>
    public static void AreNotSequenceEqual<T>(Span<T> notExpected, Span<T> actual, SequenceOrder order, string? message = "", [CallerArgumentExpression(nameof(notExpected))] string notExpectedExpression = "", [CallerArgumentExpression(nameof(actual))] string actualExpression = "")
        => AreNotSequenceEqual((ReadOnlySpan<T>)notExpected, actual, order, message, notExpectedExpression, actualExpression);

    /// <summary>
    /// Tests whether two spans differ in the same order using the specified comparer and throws an exception if they do not.
    /// </summary>
    /// <typeparam name="T">The type of sequence elements.</typeparam>
    /// <param name="notExpected">The span not expected to equal <paramref name="actual"/>.</param>
    /// <param name="actual">The span produced by the code under test.</param>
    /// <param name="comparer">The equality comparer to use when comparing elements, or <see langword="null"/> to use the default comparer.</param>
    /// <param name="message">The message to include in the exception when the sequences are equal.</param>
    /// <param name="notExpectedExpression">The syntactic expression of notExpected as given by the compiler via caller argument expression.</param>
    /// <param name="actualExpression">The syntactic expression of actual as given by the compiler via caller argument expression.</param>
    public static void AreNotSequenceEqual<T>(Span<T> notExpected, Span<T> actual, IEqualityComparer<T>? comparer, string? message = "", [CallerArgumentExpression(nameof(notExpected))] string notExpectedExpression = "", [CallerArgumentExpression(nameof(actual))] string actualExpression = "")
        => AreNotSequenceEqual((ReadOnlySpan<T>)notExpected, actual, comparer, message, notExpectedExpression, actualExpression);

    /// <summary>
    /// Tests whether two spans differ using the specified comparer and order semantics and throws an exception if they do not.
    /// </summary>
    /// <typeparam name="T">The type of sequence elements.</typeparam>
    /// <param name="notExpected">The span not expected to equal <paramref name="actual"/>.</param>
    /// <param name="actual">The span produced by the code under test.</param>
    /// <param name="comparer">The equality comparer to use when comparing elements, or <see langword="null"/> to use the default comparer.</param>
    /// <param name="order">Specifies whether elements must appear in the same order or in any order.</param>
    /// <param name="message">The message to include in the exception when the sequences are equal.</param>
    /// <param name="notExpectedExpression">The syntactic expression of notExpected as given by the compiler via caller argument expression.</param>
    /// <param name="actualExpression">The syntactic expression of actual as given by the compiler via caller argument expression.</param>
    public static void AreNotSequenceEqual<T>(Span<T> notExpected, Span<T> actual, IEqualityComparer<T>? comparer, SequenceOrder order, string? message = "", [CallerArgumentExpression(nameof(notExpected))] string notExpectedExpression = "", [CallerArgumentExpression(nameof(actual))] string actualExpression = "")
        => AreNotSequenceEqual((ReadOnlySpan<T>)notExpected, actual, comparer, order, message, notExpectedExpression, actualExpression);

    /// <summary>
    /// Tests whether two memory regions differ in the same order and throws an exception if they do not.
    /// </summary>
    /// <typeparam name="T">The type of sequence elements.</typeparam>
    /// <param name="notExpected">The memory not expected to equal <paramref name="actual"/>.</param>
    /// <param name="actual">The memory produced by the code under test.</param>
    /// <param name="message">The message to include in the exception when the sequences are equal.</param>
    /// <param name="notExpectedExpression">The syntactic expression of notExpected as given by the compiler via caller argument expression.</param>
    /// <param name="actualExpression">The syntactic expression of actual as given by the compiler via caller argument expression.</param>
    public static void AreNotSequenceEqual<T>(ReadOnlyMemory<T> notExpected, ReadOnlyMemory<T> actual, string? message = "", [CallerArgumentExpression(nameof(notExpected))] string notExpectedExpression = "", [CallerArgumentExpression(nameof(actual))] string actualExpression = "")
        => AreNotSequenceEqual(notExpected.Span, actual.Span, message, notExpectedExpression, actualExpression);

    /// <summary>
    /// Tests whether two memory regions differ using the specified order semantics and throws an exception if they do not.
    /// </summary>
    /// <typeparam name="T">The type of sequence elements.</typeparam>
    /// <param name="notExpected">The memory not expected to equal <paramref name="actual"/>.</param>
    /// <param name="actual">The memory produced by the code under test.</param>
    /// <param name="order">Specifies whether elements must appear in the same order or in any order.</param>
    /// <param name="message">The message to include in the exception when the sequences are equal.</param>
    /// <param name="notExpectedExpression">The syntactic expression of notExpected as given by the compiler via caller argument expression.</param>
    /// <param name="actualExpression">The syntactic expression of actual as given by the compiler via caller argument expression.</param>
    public static void AreNotSequenceEqual<T>(ReadOnlyMemory<T> notExpected, ReadOnlyMemory<T> actual, SequenceOrder order, string? message = "", [CallerArgumentExpression(nameof(notExpected))] string notExpectedExpression = "", [CallerArgumentExpression(nameof(actual))] string actualExpression = "")
        => AreNotSequenceEqual(notExpected.Span, actual.Span, order, message, notExpectedExpression, actualExpression);

    /// <summary>
    /// Tests whether two memory regions differ in the same order using the specified comparer and throws an exception if they do not.
    /// </summary>
    /// <typeparam name="T">The type of sequence elements.</typeparam>
    /// <param name="notExpected">The memory not expected to equal <paramref name="actual"/>.</param>
    /// <param name="actual">The memory produced by the code under test.</param>
    /// <param name="comparer">The equality comparer to use when comparing elements, or <see langword="null"/> to use the default comparer.</param>
    /// <param name="message">The message to include in the exception when the sequences are equal.</param>
    /// <param name="notExpectedExpression">The syntactic expression of notExpected as given by the compiler via caller argument expression.</param>
    /// <param name="actualExpression">The syntactic expression of actual as given by the compiler via caller argument expression.</param>
    public static void AreNotSequenceEqual<T>(ReadOnlyMemory<T> notExpected, ReadOnlyMemory<T> actual, IEqualityComparer<T>? comparer, string? message = "", [CallerArgumentExpression(nameof(notExpected))] string notExpectedExpression = "", [CallerArgumentExpression(nameof(actual))] string actualExpression = "")
        => AreNotSequenceEqual(notExpected.Span, actual.Span, comparer, message, notExpectedExpression, actualExpression);

    /// <summary>
    /// Tests whether two memory regions differ using the specified comparer and order semantics and throws an exception if they do not.
    /// </summary>
    /// <typeparam name="T">The type of sequence elements.</typeparam>
    /// <param name="notExpected">The memory not expected to equal <paramref name="actual"/>.</param>
    /// <param name="actual">The memory produced by the code under test.</param>
    /// <param name="comparer">The equality comparer to use when comparing elements, or <see langword="null"/> to use the default comparer.</param>
    /// <param name="order">Specifies whether elements must appear in the same order or in any order.</param>
    /// <param name="message">The message to include in the exception when the sequences are equal.</param>
    /// <param name="notExpectedExpression">The syntactic expression of notExpected as given by the compiler via caller argument expression.</param>
    /// <param name="actualExpression">The syntactic expression of actual as given by the compiler via caller argument expression.</param>
    public static void AreNotSequenceEqual<T>(ReadOnlyMemory<T> notExpected, ReadOnlyMemory<T> actual, IEqualityComparer<T>? comparer, SequenceOrder order, string? message = "", [CallerArgumentExpression(nameof(notExpected))] string notExpectedExpression = "", [CallerArgumentExpression(nameof(actual))] string actualExpression = "")
        => AreNotSequenceEqual(notExpected.Span, actual.Span, comparer, order, message, notExpectedExpression, actualExpression);

    /// <summary>
    /// Tests whether two memory regions differ in the same order and throws an exception if they do not.
    /// </summary>
    /// <typeparam name="T">The type of sequence elements.</typeparam>
    /// <param name="notExpected">The memory not expected to equal <paramref name="actual"/>.</param>
    /// <param name="actual">The memory produced by the code under test.</param>
    /// <param name="message">The message to include in the exception when the sequences are equal.</param>
    /// <param name="notExpectedExpression">The syntactic expression of notExpected as given by the compiler via caller argument expression.</param>
    /// <param name="actualExpression">The syntactic expression of actual as given by the compiler via caller argument expression.</param>
    public static void AreNotSequenceEqual<T>(Memory<T> notExpected, Memory<T> actual, string? message = "", [CallerArgumentExpression(nameof(notExpected))] string notExpectedExpression = "", [CallerArgumentExpression(nameof(actual))] string actualExpression = "")
        => AreNotSequenceEqual(notExpected.Span, actual.Span, message, notExpectedExpression, actualExpression);

    /// <summary>
    /// Tests whether two memory regions differ using the specified order semantics and throws an exception if they do not.
    /// </summary>
    /// <typeparam name="T">The type of sequence elements.</typeparam>
    /// <param name="notExpected">The memory not expected to equal <paramref name="actual"/>.</param>
    /// <param name="actual">The memory produced by the code under test.</param>
    /// <param name="order">Specifies whether elements must appear in the same order or in any order.</param>
    /// <param name="message">The message to include in the exception when the sequences are equal.</param>
    /// <param name="notExpectedExpression">The syntactic expression of notExpected as given by the compiler via caller argument expression.</param>
    /// <param name="actualExpression">The syntactic expression of actual as given by the compiler via caller argument expression.</param>
    public static void AreNotSequenceEqual<T>(Memory<T> notExpected, Memory<T> actual, SequenceOrder order, string? message = "", [CallerArgumentExpression(nameof(notExpected))] string notExpectedExpression = "", [CallerArgumentExpression(nameof(actual))] string actualExpression = "")
        => AreNotSequenceEqual(notExpected.Span, actual.Span, order, message, notExpectedExpression, actualExpression);

    /// <summary>
    /// Tests whether two memory regions differ in the same order using the specified comparer and throws an exception if they do not.
    /// </summary>
    /// <typeparam name="T">The type of sequence elements.</typeparam>
    /// <param name="notExpected">The memory not expected to equal <paramref name="actual"/>.</param>
    /// <param name="actual">The memory produced by the code under test.</param>
    /// <param name="comparer">The equality comparer to use when comparing elements, or <see langword="null"/> to use the default comparer.</param>
    /// <param name="message">The message to include in the exception when the sequences are equal.</param>
    /// <param name="notExpectedExpression">The syntactic expression of notExpected as given by the compiler via caller argument expression.</param>
    /// <param name="actualExpression">The syntactic expression of actual as given by the compiler via caller argument expression.</param>
    public static void AreNotSequenceEqual<T>(Memory<T> notExpected, Memory<T> actual, IEqualityComparer<T>? comparer, string? message = "", [CallerArgumentExpression(nameof(notExpected))] string notExpectedExpression = "", [CallerArgumentExpression(nameof(actual))] string actualExpression = "")
        => AreNotSequenceEqual(notExpected.Span, actual.Span, comparer, message, notExpectedExpression, actualExpression);

    /// <summary>
    /// Tests whether two memory regions differ using the specified comparer and order semantics and throws an exception if they do not.
    /// </summary>
    /// <typeparam name="T">The type of sequence elements.</typeparam>
    /// <param name="notExpected">The memory not expected to equal <paramref name="actual"/>.</param>
    /// <param name="actual">The memory produced by the code under test.</param>
    /// <param name="comparer">The equality comparer to use when comparing elements, or <see langword="null"/> to use the default comparer.</param>
    /// <param name="order">Specifies whether elements must appear in the same order or in any order.</param>
    /// <param name="message">The message to include in the exception when the sequences are equal.</param>
    /// <param name="notExpectedExpression">The syntactic expression of notExpected as given by the compiler via caller argument expression.</param>
    /// <param name="actualExpression">The syntactic expression of actual as given by the compiler via caller argument expression.</param>
    public static void AreNotSequenceEqual<T>(Memory<T> notExpected, Memory<T> actual, IEqualityComparer<T>? comparer, SequenceOrder order, string? message = "", [CallerArgumentExpression(nameof(notExpected))] string notExpectedExpression = "", [CallerArgumentExpression(nameof(actual))] string actualExpression = "")
        => AreNotSequenceEqual(notExpected.Span, actual.Span, comparer, order, message, notExpectedExpression, actualExpression);

    #endregion // AreNotSequenceEqual span/memory

#endif
}
