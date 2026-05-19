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

    private static void AreSequenceEqualImpl<T>(IEnumerable<T>? expected, IEnumerable<T>? actual, IEqualityComparer<T> comparer, string? comparerName, bool hasComparerArgument, SequenceOrder order, bool hasOrderArgument, string? message, string expectedExpression, string actualExpression)
    {
        ValidateSequenceOrder(order);

        if (object.ReferenceEquals(expected, actual))
        {
            return;
        }

        if (expected is null || actual is null)
        {
            List<T?>? expectedList = expected is null ? null : [.. expected];
            List<T?>? actualList = actual is null ? null : [.. actual];
            ReportAssertAreSequenceEqualNullMismatch(expectedList, actualList, comparerName, hasComparerArgument, order, hasOrderArgument, message, expectedExpression, actualExpression);
            return;
        }

        switch (order)
        {
            case SequenceOrder.InOrder:
                {
                    List<T?> expectedList = expected as List<T?> ?? [.. expected];
                    List<T?> actualList = actual as List<T?> ?? [.. actual];
                    if (expectedList.Count != actualList.Count)
                    {
                        ReportAssertAreSequenceEqualInOrderLengthMismatch(expectedList, actualList, comparerName, hasComparerArgument, hasOrderArgument, message, expectedExpression, actualExpression);
                        return;
                    }

                    int differenceCount = 0;
                    int firstDifferenceIndex = -1;
                    for (int i = 0; i < expectedList.Count; i++)
                    {
                        if (AreSequenceElementsEqual(expectedList[i], actualList[i], comparer))
                        {
                            continue;
                        }

                        differenceCount++;
                        firstDifferenceIndex = firstDifferenceIndex < 0 ? i : firstDifferenceIndex;
                    }

                    if (differenceCount > 0)
                    {
                        ReportAssertAreSequenceEqualInOrderElementMismatch(expectedList, actualList, differenceCount, firstDifferenceIndex, comparerName, hasComparerArgument, hasOrderArgument, message, expectedExpression, actualExpression);
                    }

                    return;
                }

            case SequenceOrder.InAnyOrder:
                {
                    List<T?> expectedList = expected as List<T?> ?? [.. expected];
                    List<T?> actualList = actual as List<T?> ?? [.. actual];
                    if (HasAnyOrderDifferences(expectedList, actualList, comparer, out List<T?>? missing, out List<T?>? unexpected))
                    {
                        ReportAssertAreSequenceEqualInAnyOrderFailed(expectedList, actualList, missing, unexpected, comparerName, hasComparerArgument, hasOrderArgument, message, expectedExpression, actualExpression);
                    }

                    return;
                }

            default:
                throw new ArgumentOutOfRangeException(nameof(order));
        }
    }

    private static void AreNotSequenceEqualImpl<T>(IEnumerable<T>? notExpected, IEnumerable<T>? actual, IEqualityComparer<T> comparer, string? comparerName, bool hasComparerArgument, SequenceOrder order, bool hasOrderArgument, string? message, string notExpectedExpression, string actualExpression)
    {
        ValidateSequenceOrder(order);

        if (object.ReferenceEquals(notExpected, actual))
        {
            ReportAssertAreNotSequenceEqualFailed(notExpected, actual, comparerName, order, hasComparerArgument, hasOrderArgument, message, notExpectedExpression, actualExpression);
            return;
        }

        if (notExpected is null || actual is null)
        {
            return;
        }

        switch (order)
        {
            case SequenceOrder.InOrder:
                {
                    List<T?> notExpectedList = notExpected as List<T?> ?? [.. notExpected];
                    List<T?> actualList = actual as List<T?> ?? [.. actual];
                    if (notExpectedList.Count != actualList.Count)
                    {
                        return;
                    }

                    for (int i = 0; i < notExpectedList.Count; i++)
                    {
                        if (!AreSequenceElementsEqual(notExpectedList[i], actualList[i], comparer))
                        {
                            return;
                        }
                    }

                    ReportAssertAreNotSequenceEqualFailed(notExpectedList, actualList, comparerName, order, hasComparerArgument, hasOrderArgument, message, notExpectedExpression, actualExpression);
                    return;
                }

            case SequenceOrder.InAnyOrder:
                {
                    List<T?> notExpectedList = notExpected as List<T?> ?? [.. notExpected];
                    List<T?> actualList = actual as List<T?> ?? [.. actual];
                    if (!HasAnyOrderDifferences(notExpectedList, actualList, comparer, out _, out _))
                    {
                        ReportAssertAreNotSequenceEqualFailed(notExpectedList, actualList, comparerName, order, hasComparerArgument, hasOrderArgument, message, notExpectedExpression, actualExpression);
                    }

                    return;
                }

            default:
                throw new ArgumentOutOfRangeException(nameof(order));
        }
    }

    private static bool AreSequenceElementsEqual<T>(T? expected, T? actual, IEqualityComparer<T> comparer)
        => expected is null || actual is null
            ? expected is null && actual is null
            : comparer.Equals(expected, actual);

    private static bool HasAnyOrderDifferences<T>(List<T?> expected, List<T?> actual, IEqualityComparer<T> comparer, out List<T?>? missing, out List<T?>? unexpected)
    {
#pragma warning disable CS8714 // The type cannot be used as type parameter in the generic type or method. Nullability of type argument doesn't match 'notnull' constraint.
#pragma warning disable IDE0028 // Collection initialization can be simplified - target-typed `new` cannot pass the comparer in the same syntactic form expected.
        Dictionary<T, int> expectedCounts = new(comparer);
#pragma warning restore IDE0028
#pragma warning restore CS8714
        int missingNullCount = 0;

        foreach (T? element in expected)
        {
            if (element is null)
            {
                missingNullCount++;
                continue;
            }

            expectedCounts.TryGetValue(element, out int count);
            expectedCounts[element] = count + 1;
        }

        unexpected = null;
        foreach (T? element in actual)
        {
            if (element is null)
            {
                if (missingNullCount > 0)
                {
                    missingNullCount--;
                }
                else
                {
                    unexpected ??= [];
                    unexpected.Add(default);
                }

                continue;
            }

            if (expectedCounts.TryGetValue(element, out int remaining) && remaining > 0)
            {
                if (remaining == 1)
                {
                    expectedCounts.Remove(element);
                }
                else
                {
                    expectedCounts[element] = remaining - 1;
                }
            }
            else
            {
                unexpected ??= [];
                unexpected.Add(element);
            }
        }

        missing = null;
        int remainingNullCount = missingNullCount;
        foreach (T? element in expected)
        {
            if (element is null)
            {
                if (remainingNullCount > 0)
                {
                    missing ??= [];
                    missing.Add(default);
                    remainingNullCount--;
                }

                continue;
            }

            if (expectedCounts.TryGetValue(element, out int remaining) && remaining > 0)
            {
                missing ??= [];
                missing.Add(element);
                if (remaining == 1)
                {
                    expectedCounts.Remove(element);
                }
                else
                {
                    expectedCounts[element] = remaining - 1;
                }
            }
        }

        return missing is not null || unexpected is not null;
    }

    private static void ValidateSequenceOrder(SequenceOrder order)
    {
        if (order is SequenceOrder.InOrder or SequenceOrder.InAnyOrder)
        {
            return;
        }

        throw new ArgumentOutOfRangeException(nameof(order));
    }

    [DoesNotReturn]
    private static void ReportAssertAreSequenceEqualNullMismatch<T>(IEnumerable<T>? expected, IEnumerable<T>? actual, string? comparerName, bool hasComparerArgument, SequenceOrder order, bool hasOrderArgument, string? message, string expectedExpression, string actualExpression)
    {
        string expectedText = AssertionValueRenderer.RenderValue(expected);
        string actualText = AssertionValueRenderer.RenderValue(actual);
        EvidenceBlock evidence = EvidenceBlock.Create()
            .AddLine("expected:", expectedText)
            .AddLine("actual:", actualText);

        if (comparerName is not null)
        {
            evidence.AddLine("comparer:", comparerName);
        }

        StructuredAssertionMessage structured = new(order == SequenceOrder.InAnyOrder ? FrameworkMessages.AreSequenceEqualInAnyOrderFailedSummary : FrameworkMessages.AreSequenceEqualInOrderFailedSummary);
        structured.WithAdditionalSummaryLine(FrameworkMessages.AreEquivalentMismatchNull);
        structured.WithUserMessage(message);
        structured.WithEvidence(evidence);
        structured.WithExpectedAndActual(expectedText, actualText);
        structured.WithCallSiteExpression(BuildSequenceEqualCallSite("Assert.AreSequenceEqual", expectedExpression, actualExpression, "<expected>", hasComparerArgument, hasOrderArgument));

        ReportAssertFailed(structured);
    }

    [DoesNotReturn]
    private static void ReportAssertAreSequenceEqualInOrderLengthMismatch<T>(IReadOnlyList<T?> expected, IReadOnlyList<T?> actual, string? comparerName, bool hasComparerArgument, bool hasOrderArgument, string? message, string expectedExpression, string actualExpression)
    {
        string expectedText = AssertionValueRenderer.RenderValue(expected);
        string actualText = AssertionValueRenderer.RenderValue(actual);
        EvidenceBlock evidence = EvidenceBlock.Create()
            .AddLine("expected:", expectedText)
            .AddLine("actual:", actualText);

        if (comparerName is not null)
        {
            evidence.AddLine("comparer:", comparerName);
        }

        StructuredAssertionMessage structured = new(FrameworkMessages.AreSequenceEqualInOrderFailedSummary);
        structured.WithAdditionalSummaryLine(string.Format(CultureInfo.CurrentCulture, FrameworkMessages.AreSequenceEqualLengthDiffMsg, expected.Count, actual.Count));
        structured.WithUserMessage(message);
        structured.WithEvidence(evidence);
        structured.WithExpectedAndActual(expectedText, actualText);
        structured.WithCallSiteExpression(BuildSequenceEqualCallSite("Assert.AreSequenceEqual", expectedExpression, actualExpression, "<expected>", hasComparerArgument, hasOrderArgument));

        ReportAssertFailed(structured);
    }

    [DoesNotReturn]
    private static void ReportAssertAreSequenceEqualInOrderElementMismatch<T>(IReadOnlyList<T?> expected, IReadOnlyList<T?> actual, int differenceCount, int firstDifferenceIndex, string? comparerName, bool hasComparerArgument, bool hasOrderArgument, string? message, string expectedExpression, string actualExpression)
    {
        string expectedText = AssertionValueRenderer.RenderValue(expected);
        string actualText = AssertionValueRenderer.RenderValue(actual);
        EvidenceBlock evidence = EvidenceBlock.Create()
            .AddLine("expected:", expectedText)
            .AddLine("actual:", actualText);

        if (comparerName is not null)
        {
            evidence.AddLine("comparer:", comparerName);
        }

        StructuredAssertionMessage structured = new(FrameworkMessages.AreSequenceEqualInOrderFailedSummary);
        structured.WithAdditionalSummaryLine(string.Format(CultureInfo.CurrentCulture, FrameworkMessages.AreSequenceEqualSameLengthDiffMsg, expected.Count, differenceCount, firstDifferenceIndex));
        structured.WithUserMessage(message);
        structured.WithEvidence(evidence);
        structured.WithExpectedAndActual(expectedText, actualText);
        structured.WithCallSiteExpression(BuildSequenceEqualCallSite("Assert.AreSequenceEqual", expectedExpression, actualExpression, "<expected>", hasComparerArgument, hasOrderArgument));

        ReportAssertFailed(structured);
    }

    [DoesNotReturn]
    private static void ReportAssertAreSequenceEqualInAnyOrderFailed<T>(IReadOnlyList<T?> expected, IReadOnlyList<T?> actual, List<T?>? missing, List<T?>? unexpected, string? comparerName, bool hasComparerArgument, bool hasOrderArgument, string? message, string expectedExpression, string actualExpression)
    {
        List<T?> missingItems = missing ?? [];
        List<T?> unexpectedItems = unexpected ?? [];
        string expectedText = AssertionValueRenderer.RenderValue(expected);
        string actualText = AssertionValueRenderer.RenderValue(actual);
        EvidenceBlock evidence = EvidenceBlock.Create()
            .AddLine("missing:", AssertionValueRenderer.RenderValue(missingItems))
            .AddLine("unexpected:", AssertionValueRenderer.RenderValue(unexpectedItems));

        if (comparerName is not null)
        {
            evidence.AddLine("comparer:", comparerName);
        }

        // The summary already says "in any order", so repeating the order as an evidence line would add noise.
        StructuredAssertionMessage structured = new(FrameworkMessages.AreSequenceEqualInAnyOrderFailedSummary);
        structured.WithAdditionalSummaryLine(string.Format(CultureInfo.CurrentCulture, FrameworkMessages.AreSequenceEqualMissingUnexpectedMsg, missingItems.Count, unexpectedItems.Count));
        structured.WithUserMessage(message);
        structured.WithEvidence(evidence);
        structured.WithExpectedAndActual(expectedText, actualText);
        structured.WithCallSiteExpression(BuildSequenceEqualCallSite("Assert.AreSequenceEqual", expectedExpression, actualExpression, "<expected>", hasComparerArgument, hasOrderArgument));

        ReportAssertFailed(structured);
    }

    [DoesNotReturn]
    private static void ReportAssertAreNotSequenceEqualFailed<T>(IEnumerable<T>? notExpected, IEnumerable<T>? actual, string? comparerName, SequenceOrder order, bool hasComparerArgument, bool hasOrderArgument, string? message, string notExpectedExpression, string actualExpression)
    {
        string notExpectedText = AssertionValueRenderer.RenderValue(notExpected);
        string actualText = AssertionValueRenderer.RenderValue(actual);
        EvidenceBlock evidence = EvidenceBlock.Create()
            .AddLine("notExpected:", notExpectedText)
            .AddLine("actual:", actualText);

        if (comparerName is not null)
        {
            evidence.AddLine("comparer:", comparerName);
        }

        StructuredAssertionMessage structured = new(order == SequenceOrder.InAnyOrder ? FrameworkMessages.AreNotSequenceEqualInAnyOrderFailedSummary : FrameworkMessages.AreNotSequenceEqualInOrderFailedSummary);
        structured.WithUserMessage(message);
        structured.WithEvidence(evidence);
        structured.WithExpectedAndActual(notExpectedText, actualText);
        structured.WithCallSiteExpression(BuildSequenceEqualCallSite("Assert.AreNotSequenceEqual", notExpectedExpression, actualExpression, "<notExpected>", hasComparerArgument, hasOrderArgument));

        ReportAssertFailed(structured);
    }

    private static string? BuildSequenceEqualCallSite(string assertionMethodName, string leftExpression, string rightExpression, string leftPlaceholder, bool hasComparerArgument, bool hasOrderArgument)
    {
        string? callSite = FormatCallSiteExpression(assertionMethodName, leftExpression, rightExpression, leftPlaceholder, "<actual>");
        if (callSite is null)
        {
            return null;
        }

        if (!hasComparerArgument && !hasOrderArgument)
        {
            return callSite;
        }

        Debug.Assert(callSite.Length > 0 && callSite[callSite.Length - 1] == ')', "FormatCallSiteExpression contract: rendered call-site must end with ')'.");

        string suffix = hasComparerArgument && hasOrderArgument
            ? ", <comparer>, <order>)"
            : hasComparerArgument
                ? ", <comparer>)"
                : ", <order>)";

        return string.Concat(callSite.Remove(callSite.Length - 1), suffix);
    }
}
