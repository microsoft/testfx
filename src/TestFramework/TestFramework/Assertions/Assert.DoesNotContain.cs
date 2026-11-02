// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestTools.UnitTesting;

#pragma warning disable RS0026 // Do not add multiple public overloads with optional parameters
#pragma warning disable RS0027 // API with optional parameter(s) should have the most parameters amongst its public overloads

public sealed partial class Assert
{
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
        TelemetryCollector.TrackAssertionCall("Assert.DoesNotContain");
        ContainsCore(notExpected, collection, "Assert.DoesNotContain", message, notExpectedExpression, collectionExpression, shouldContain: false);
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
        TelemetryCollector.TrackAssertionCall("Assert.DoesNotContain");
        ContainsCore(notExpected, collection, "Assert.DoesNotContain", message, notExpectedExpression, collectionExpression, shouldContain: false);
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
        TelemetryCollector.TrackAssertionCall("Assert.DoesNotContain");
        ContainsCore(notExpected, collection, comparer, "Assert.DoesNotContain", message, notExpectedExpression, collectionExpression, shouldContain: false);
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
        TelemetryCollector.TrackAssertionCall("Assert.DoesNotContain");
        ContainsCore(notExpected, collection, comparer, "Assert.DoesNotContain", message, notExpectedExpression, collectionExpression, shouldContain: false);
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
        TelemetryCollector.TrackAssertionCall("Assert.DoesNotContain");
        ContainsCore(predicate, collection, "Assert.DoesNotContain", message, predicateExpression, collectionExpression, shouldContain: false);
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
        TelemetryCollector.TrackAssertionCall("Assert.DoesNotContain");
        ContainsCore(predicate, collection, "Assert.DoesNotContain", message, predicateExpression, collectionExpression, shouldContain: false);
    }

#if NETCOREAPP3_1_OR_GREATER

    /// <summary>
    /// Tests whether the specified array does not contain the specified item.
    /// </summary>
    /// <typeparam name="T">The type of the array items.</typeparam>
    /// <param name="notExpected">The unexpected item.</param>
    /// <param name="collection">The array.</param>
    /// <param name="message">The message to display when the assertion fails.</param>
    /// <param name="notExpectedExpression">The syntactic expression of notExpected as given by the compiler via caller argument expression.</param>
    /// <param name="collectionExpression">The syntactic expression of collection as given by the compiler via caller argument expression.</param>
    public static void DoesNotContain<T>(T notExpected, T[] collection, string? message = "", [CallerArgumentExpression(nameof(notExpected))] string notExpectedExpression = "", [CallerArgumentExpression(nameof(collection))] string collectionExpression = "")
        => DoesNotContain(notExpected, (IEnumerable<T>)collection, message, notExpectedExpression, collectionExpression);

    /// <summary>
    /// Tests whether the specified array does not contain the specified item using the specified equality comparer.
    /// </summary>
    /// <typeparam name="T">The type of the array items.</typeparam>
    /// <param name="notExpected">The unexpected item.</param>
    /// <param name="collection">The array.</param>
    /// <param name="comparer">An equality comparer to compare values.</param>
    /// <param name="message">The message to display when the assertion fails.</param>
    /// <param name="notExpectedExpression">The syntactic expression of notExpected as given by the compiler via caller argument expression.</param>
    /// <param name="collectionExpression">The syntactic expression of collection as given by the compiler via caller argument expression.</param>
    public static void DoesNotContain<T>(T notExpected, T[] collection, IEqualityComparer<T> comparer, string? message = "", [CallerArgumentExpression(nameof(notExpected))] string notExpectedExpression = "", [CallerArgumentExpression(nameof(collection))] string collectionExpression = "")
        => DoesNotContain(notExpected, (IEnumerable<T>)collection, comparer, message, notExpectedExpression, collectionExpression);

    /// <summary>
    /// Tests whether the specified array contains no element that matches the given predicate.
    /// </summary>
    /// <typeparam name="T">The type of the array items.</typeparam>
    /// <param name="predicate">A function to test each element for a condition.</param>
    /// <param name="collection">The array.</param>
    /// <param name="message">The message to display when the assertion fails.</param>
    /// <param name="predicateExpression">The syntactic expression of predicate as given by the compiler via caller argument expression.</param>
    /// <param name="collectionExpression">The syntactic expression of collection as given by the compiler via caller argument expression.</param>
    public static void DoesNotContain<T>(Func<T, bool> predicate, T[] collection, string? message = "", [CallerArgumentExpression(nameof(predicate))] string predicateExpression = "", [CallerArgumentExpression(nameof(collection))] string collectionExpression = "")
        => DoesNotContain(predicate, (IEnumerable<T>)collection, message, predicateExpression, collectionExpression);

    /// <summary>
    /// Tests whether the specified collection does not contain the specified item.
    /// </summary>
    /// <typeparam name="T">The type of the collection items.</typeparam>
    /// <typeparam name="TCollection">The concrete type of the collection.</typeparam>
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
    /// <remarks>The collection may contain null elements even when <paramref name="notExpected"/> is not null.</remarks>
    public static void DoesNotContain<T, TCollection>(T notExpected, TCollection collection, string? message = "", [CallerArgumentExpression(nameof(notExpected))] string notExpectedExpression = "", [CallerArgumentExpression(nameof(collection))] string collectionExpression = "")
        where TCollection : IEnumerable<T?>
        => DoesNotContain(notExpected, (IEnumerable<T?>)collection, message, notExpectedExpression, collectionExpression);

    /// <summary>
    /// Tests whether the specified collection does not contain the specified item.
    /// </summary>
    /// <typeparam name="T">The type of the collection items.</typeparam>
    /// <typeparam name="TCollection">The concrete type of the collection.</typeparam>
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
    /// <remarks>The collection may contain null elements even when <paramref name="notExpected"/> is not null.</remarks>
    public static void DoesNotContain<T, TCollection>(T notExpected, TCollection collection, IEqualityComparer<T> comparer, string? message = "", [CallerArgumentExpression(nameof(notExpected))] string notExpectedExpression = "", [CallerArgumentExpression(nameof(collection))] string collectionExpression = "")
        where TCollection : IEnumerable<T?>
        // Equality comparers accept null operands. Preserve T for compatibility with non-nullable comparers.
        => DoesNotContain(notExpected, (IEnumerable<T>)collection, comparer, message, notExpectedExpression, collectionExpression);

    /// <summary>
    /// Tests whether the specified collection contains no element that matches the given predicate.
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
    public static void DoesNotContain<T, TCollection>(Func<T, bool> predicate, TCollection collection, string? message = "", [CallerArgumentExpression(nameof(predicate))] string predicateExpression = "", [CallerArgumentExpression(nameof(collection))] string collectionExpression = "")
        where TCollection : IEnumerable<T>
        => DoesNotContain(predicate, (IEnumerable<T>)collection, message, predicateExpression, collectionExpression);

#endif

    #endregion // DoesNotContain

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
}
