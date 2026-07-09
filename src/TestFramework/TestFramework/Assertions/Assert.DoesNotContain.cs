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
        => ContainsCore(notExpected, collection, "Assert.DoesNotContain", message, notExpectedExpression, collectionExpression, shouldContain: false);

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
        => ContainsCore(notExpected, collection, "Assert.DoesNotContain", message, notExpectedExpression, collectionExpression, shouldContain: false);

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
        => ContainsCore(notExpected, collection, comparer, "Assert.DoesNotContain", message, notExpectedExpression, collectionExpression, shouldContain: false);

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
        => ContainsCore(notExpected, collection, comparer, "Assert.DoesNotContain", message, notExpectedExpression, collectionExpression, shouldContain: false);

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
        => ContainsCore(predicate, collection, "Assert.DoesNotContain", message, predicateExpression, collectionExpression, shouldContain: false);

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
        => ContainsCore(predicate, collection, "Assert.DoesNotContain", message, predicateExpression, collectionExpression, shouldContain: false);

#if NETCOREAPP3_1_OR_GREATER

    /// <summary>
    /// Tests whether the specified span does not contain the specified item.
    /// </summary>
    /// <typeparam name="T">The type of the span items.</typeparam>
    /// <param name="notExpected">The unexpected item.</param>
    /// <param name="collection">The span.</param>
    /// <param name="message">The message to display when the assertion fails.</param>
    /// <param name="notExpectedExpression">
    /// The syntactic expression of notExpected as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    /// <param name="collectionExpression">
    /// The syntactic expression of collection as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    public static void DoesNotContain<T>(T notExpected, ReadOnlySpan<T> collection, string? message = "", [CallerArgumentExpression(nameof(notExpected))] string notExpectedExpression = "", [CallerArgumentExpression(nameof(collection))] string collectionExpression = "")
        => ContainsCore(notExpected, collection.ToArray(), "Assert.DoesNotContain", message, notExpectedExpression, collectionExpression, shouldContain: false);

    /// <summary>
    /// Tests whether the specified span does not contain the specified item.
    /// </summary>
    /// <typeparam name="T">The type of the span items.</typeparam>
    /// <param name="notExpected">The unexpected item.</param>
    /// <param name="collection">The span.</param>
    /// <param name="message">The message to display when the assertion fails.</param>
    /// <param name="notExpectedExpression">
    /// The syntactic expression of notExpected as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    /// <param name="collectionExpression">
    /// The syntactic expression of collection as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    public static void DoesNotContain<T>(T notExpected, Span<T> collection, string? message = "", [CallerArgumentExpression(nameof(notExpected))] string notExpectedExpression = "", [CallerArgumentExpression(nameof(collection))] string collectionExpression = "")
        => DoesNotContain(notExpected, (ReadOnlySpan<T>)collection, message, notExpectedExpression, collectionExpression);

    /// <summary>
    /// Tests whether the specified memory does not contain the specified item.
    /// </summary>
    /// <typeparam name="T">The type of the memory items.</typeparam>
    /// <param name="notExpected">The unexpected item.</param>
    /// <param name="collection">The memory.</param>
    /// <param name="message">The message to display when the assertion fails.</param>
    /// <param name="notExpectedExpression">
    /// The syntactic expression of notExpected as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    /// <param name="collectionExpression">
    /// The syntactic expression of collection as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    public static void DoesNotContain<T>(T notExpected, ReadOnlyMemory<T> collection, string? message = "", [CallerArgumentExpression(nameof(notExpected))] string notExpectedExpression = "", [CallerArgumentExpression(nameof(collection))] string collectionExpression = "")
        => DoesNotContain(notExpected, collection.Span, message, notExpectedExpression, collectionExpression);

    /// <summary>
    /// Tests whether the specified memory does not contain the specified item.
    /// </summary>
    /// <typeparam name="T">The type of the memory items.</typeparam>
    /// <param name="notExpected">The unexpected item.</param>
    /// <param name="collection">The memory.</param>
    /// <param name="message">The message to display when the assertion fails.</param>
    /// <param name="notExpectedExpression">
    /// The syntactic expression of notExpected as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    /// <param name="collectionExpression">
    /// The syntactic expression of collection as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    public static void DoesNotContain<T>(T notExpected, Memory<T> collection, string? message = "", [CallerArgumentExpression(nameof(notExpected))] string notExpectedExpression = "", [CallerArgumentExpression(nameof(collection))] string collectionExpression = "")
        => DoesNotContain(notExpected, collection.Span, message, notExpectedExpression, collectionExpression);

    /// <summary>
    /// Tests whether the specified span does not contain the specified item.
    /// </summary>
    /// <typeparam name="T">The type of the span items.</typeparam>
    /// <param name="notExpected">The unexpected item.</param>
    /// <param name="collection">The span.</param>
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
    public static void DoesNotContain<T>(T notExpected, ReadOnlySpan<T> collection, IEqualityComparer<T> comparer, string? message = "", [CallerArgumentExpression(nameof(notExpected))] string notExpectedExpression = "", [CallerArgumentExpression(nameof(collection))] string collectionExpression = "")
        => ContainsCore(notExpected, collection.ToArray(), comparer, "Assert.DoesNotContain", message, notExpectedExpression, collectionExpression, shouldContain: false);

    /// <summary>
    /// Tests whether the specified span does not contain the specified item.
    /// </summary>
    /// <typeparam name="T">The type of the span items.</typeparam>
    /// <param name="notExpected">The unexpected item.</param>
    /// <param name="collection">The span.</param>
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
    public static void DoesNotContain<T>(T notExpected, Span<T> collection, IEqualityComparer<T> comparer, string? message = "", [CallerArgumentExpression(nameof(notExpected))] string notExpectedExpression = "", [CallerArgumentExpression(nameof(collection))] string collectionExpression = "")
        => DoesNotContain(notExpected, (ReadOnlySpan<T>)collection, comparer, message, notExpectedExpression, collectionExpression);

    /// <summary>
    /// Tests whether the specified memory does not contain the specified item.
    /// </summary>
    /// <typeparam name="T">The type of the memory items.</typeparam>
    /// <param name="notExpected">The unexpected item.</param>
    /// <param name="collection">The memory.</param>
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
    public static void DoesNotContain<T>(T notExpected, ReadOnlyMemory<T> collection, IEqualityComparer<T> comparer, string? message = "", [CallerArgumentExpression(nameof(notExpected))] string notExpectedExpression = "", [CallerArgumentExpression(nameof(collection))] string collectionExpression = "")
        => DoesNotContain(notExpected, collection.Span, comparer, message, notExpectedExpression, collectionExpression);

    /// <summary>
    /// Tests whether the specified memory does not contain the specified item.
    /// </summary>
    /// <typeparam name="T">The type of the memory items.</typeparam>
    /// <param name="notExpected">The unexpected item.</param>
    /// <param name="collection">The memory.</param>
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
    public static void DoesNotContain<T>(T notExpected, Memory<T> collection, IEqualityComparer<T> comparer, string? message = "", [CallerArgumentExpression(nameof(notExpected))] string notExpectedExpression = "", [CallerArgumentExpression(nameof(collection))] string collectionExpression = "")
        => DoesNotContain(notExpected, collection.Span, comparer, message, notExpectedExpression, collectionExpression);

    /// <summary>
    /// Tests whether the specified span does not contain an element matching the given predicate.
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
    public static void DoesNotContain<T>(Func<T, bool> predicate, ReadOnlySpan<T> collection, string? message = "", [CallerArgumentExpression(nameof(predicate))] string predicateExpression = "", [CallerArgumentExpression(nameof(collection))] string collectionExpression = "")
        => ContainsCore(predicate, collection.ToArray(), "Assert.DoesNotContain", message, predicateExpression, collectionExpression, shouldContain: false);

    /// <summary>
    /// Tests whether the specified span does not contain an element matching the given predicate.
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
    public static void DoesNotContain<T>(Func<T, bool> predicate, Span<T> collection, string? message = "", [CallerArgumentExpression(nameof(predicate))] string predicateExpression = "", [CallerArgumentExpression(nameof(collection))] string collectionExpression = "")
        => DoesNotContain(predicate, (ReadOnlySpan<T>)collection, message, predicateExpression, collectionExpression);

    /// <summary>
    /// Tests whether the specified memory does not contain an element matching the given predicate.
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
    public static void DoesNotContain<T>(Func<T, bool> predicate, ReadOnlyMemory<T> collection, string? message = "", [CallerArgumentExpression(nameof(predicate))] string predicateExpression = "", [CallerArgumentExpression(nameof(collection))] string collectionExpression = "")
        => DoesNotContain(predicate, collection.Span, message, predicateExpression, collectionExpression);

    /// <summary>
    /// Tests whether the specified memory does not contain an element matching the given predicate.
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
    public static void DoesNotContain<T>(Func<T, bool> predicate, Memory<T> collection, string? message = "", [CallerArgumentExpression(nameof(predicate))] string predicateExpression = "", [CallerArgumentExpression(nameof(collection))] string collectionExpression = "")
        => DoesNotContain(predicate, collection.Span, message, predicateExpression, collectionExpression);

#endif

    /// <summary>
    /// Tests whether the specified string does not contain the specified substring
    /// and throws an exception if the substring occurs within the
    /// test string.
    /// </summary>
    /// <param name="substring">
    /// The string expected to not occur within <paramref name="value"/>.
    /// </param>
    /// <param name="value">
    /// The string that is expected to not contain <paramref name="substring"/>.
    /// </param>
    /// <param name="message">
    /// The message to include in the exception when <paramref name="substring"/>
    /// is in <paramref name="value"/>. The message is shown in
    /// test results.
    /// </param>
    /// <param name="substringExpression">
    /// The syntactic expression of substring as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    /// <param name="valueExpression">
    /// The syntactic expression of value as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// <paramref name="value"/> is null, or <paramref name="substring"/> is null,
    /// or <paramref name="value"/> contains <paramref name="substring"/>.
    /// </exception>
    public static void DoesNotContain(string substring, string value, string? message = "", [CallerArgumentExpression(nameof(substring))] string substringExpression = "", [CallerArgumentExpression(nameof(value))] string valueExpression = "")
        => DoesNotContain(substring, value, StringComparison.Ordinal, message, substringExpression, valueExpression);

    /// <summary>
    /// Tests whether the specified string does not contain the specified substring
    /// and throws an exception if the substring occurs within the
    /// test string.
    /// </summary>
    /// <param name="substring">
    /// The string expected to not occur within <paramref name="value"/>.
    /// </param>
    /// <param name="value">
    /// The string that is expected to not contain <paramref name="substring"/>.
    /// </param>
    /// <param name="comparisonType">
    /// The comparison method to compare strings <paramref name="comparisonType"/>.
    /// </param>
    /// <param name="message">
    /// The message to include in the exception when <paramref name="substring"/>
    /// is in <paramref name="value"/>. The message is shown in
    /// test results.
    /// </param>
    /// <param name="substringExpression">
    /// The syntactic expression of substring as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    /// <param name="valueExpression">
    /// The syntactic expression of value as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// <paramref name="value"/> is null, or <paramref name="substring"/> is null,
    /// or <paramref name="value"/> contains <paramref name="substring"/>.
    /// </exception>
    public static void DoesNotContain(string substring, string value, StringComparison comparisonType, string? message = "", [CallerArgumentExpression(nameof(substring))] string substringExpression = "", [CallerArgumentExpression(nameof(value))] string valueExpression = "")
        => ContainsCore(substring, value, comparisonType, "Assert.DoesNotContain", message, substringExpression, valueExpression, shouldContain: false);

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

    [DoesNotReturn]
    private static void ReportAssertDoesNotContainSubstringFailed(string substring, string value, StringComparison comparisonType, string? userMessage, string substringExpression, string valueExpression)
    {
        string notExpectedText = AssertionValueRenderer.RenderValue(substring);
        string actualText = AssertionValueRenderer.RenderValue(value);
        EvidenceBlock evidence = EvidenceBlock.Create()
            .AddLine("unexpected substring:", notExpectedText)
            .AddLine("actual:", actualText)
            .AddLine("comparison:", comparisonType.ToString());
        StructuredAssertionMessage structured = new(FrameworkMessages.DoesNotContainSubstringFailedSummary);
        structured.WithUserMessage(userMessage);
        structured.WithEvidence(evidence);
        structured.WithExpectedAndActual(notExpectedText, actualText);
        structured.WithCallSiteExpression(FormatCallSiteExpression("Assert.DoesNotContain", substringExpression, valueExpression, "<substring>", "<value>"));
        ReportAssertFailed(structured);
    }
}
