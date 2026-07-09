// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestTools.UnitTesting;

#pragma warning disable RS0026 // Do not add multiple public overloads with optional parameters
#pragma warning disable RS0027 // API with optional parameter(s) should have the most parameters amongst its public overloads
public sealed partial class Assert
{
    #region Contains

    /// <summary>
    /// Tests whether the specified collection contains the given element.
    /// </summary>
    /// <typeparam name="T">The type of the collection items.</typeparam>
    /// <param name="expected">The expected item.</param>
    /// <param name="collection">The collection.</param>
    /// <param name="message">The message to display when the assertion fails.</param>
    /// <param name="expectedExpression">
    /// The syntactic expression of expected as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    /// <param name="collectionExpression">
    /// The syntactic expression of collection as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    public static void Contains<T>(T expected, IEnumerable<T> collection, string? message = "", [CallerArgumentExpression(nameof(expected))] string expectedExpression = "", [CallerArgumentExpression(nameof(collection))] string collectionExpression = "")
        => ContainsCore(expected, collection, "Assert.Contains", message, expectedExpression, collectionExpression, shouldContain: true);

    /// <summary>
    /// Tests whether the specified collection contains the given element.
    /// </summary>
    /// <param name="expected">The expected item.</param>
    /// <param name="collection">The collection.</param>
    /// <param name="message">The message to display when the assertion fails.</param>
    /// <param name="expectedExpression">
    /// The syntactic expression of expected as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    /// <param name="collectionExpression">
    /// The syntactic expression of collection as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    public static void Contains(object? expected, IEnumerable collection, string? message = "", [CallerArgumentExpression(nameof(expected))] string expectedExpression = "", [CallerArgumentExpression(nameof(collection))] string collectionExpression = "")
        => ContainsCore(expected, collection, "Assert.Contains", message, expectedExpression, collectionExpression, shouldContain: true);

    /// <summary>
    /// Tests whether the specified collection contains the given element.
    /// </summary>
    /// <typeparam name="T">The type of the collection items.</typeparam>
    /// <param name="expected">The expected item.</param>
    /// <param name="collection">The collection.</param>
    /// <param name="comparer">An equality comparer to compare values.</param>
    /// <param name="message">The message to display when the assertion fails.</param>
    /// <param name="expectedExpression">
    /// The syntactic expression of expected as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    /// <param name="collectionExpression">
    /// The syntactic expression of collection as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    public static void Contains<T>(T expected, IEnumerable<T> collection, IEqualityComparer<T> comparer, string? message = "", [CallerArgumentExpression(nameof(expected))] string expectedExpression = "", [CallerArgumentExpression(nameof(collection))] string collectionExpression = "")
        => ContainsCore(expected, collection, comparer, "Assert.Contains", message, expectedExpression, collectionExpression, shouldContain: true);

    /// <summary>
    /// Tests whether the specified collection contains the given element.
    /// </summary>
    /// <param name="expected">The expected item.</param>
    /// <param name="collection">The collection.</param>
    /// <param name="comparer">An equality comparer to compare values.</param>
    /// <param name="message">The message to display when the assertion fails.</param>
    /// <param name="expectedExpression">
    /// The syntactic expression of expected as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    /// <param name="collectionExpression">
    /// The syntactic expression of collection as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    public static void Contains(object? expected, IEnumerable collection, IEqualityComparer comparer, string? message = "", [CallerArgumentExpression(nameof(expected))] string expectedExpression = "", [CallerArgumentExpression(nameof(collection))] string collectionExpression = "")
        => ContainsCore(expected, collection, comparer, "Assert.Contains", message, expectedExpression, collectionExpression, shouldContain: true);

    /// <summary>
    /// Tests whether the specified collection contains the given element.
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
    public static void Contains<T>(Func<T, bool> predicate, IEnumerable<T> collection, string? message = "", [CallerArgumentExpression(nameof(predicate))] string predicateExpression = "", [CallerArgumentExpression(nameof(collection))] string collectionExpression = "")
        => ContainsCore(predicate, collection, "Assert.Contains", message, predicateExpression, collectionExpression, shouldContain: true);

    /// <summary>
    /// Tests whether the specified collection contains the given element.
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
    public static void Contains(Func<object?, bool> predicate, IEnumerable collection, string? message = "", [CallerArgumentExpression(nameof(predicate))] string predicateExpression = "", [CallerArgumentExpression(nameof(collection))] string collectionExpression = "")
        => ContainsCore(predicate, collection, "Assert.Contains", message, predicateExpression, collectionExpression, shouldContain: true);

#if NETCOREAPP3_1_OR_GREATER

    /// <summary>
    /// Tests whether the specified span contains the given element.
    /// </summary>
    /// <typeparam name="T">The type of the span items.</typeparam>
    /// <param name="expected">The expected item.</param>
    /// <param name="collection">The span.</param>
    /// <param name="message">The message to display when the assertion fails.</param>
    /// <param name="expectedExpression">
    /// The syntactic expression of expected as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    /// <param name="collectionExpression">
    /// The syntactic expression of collection as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    public static void Contains<T>(T expected, ReadOnlySpan<T> collection, string? message = "", [CallerArgumentExpression(nameof(expected))] string expectedExpression = "", [CallerArgumentExpression(nameof(collection))] string collectionExpression = "")
        => ContainsCore(expected, collection.ToArray(), "Assert.Contains", message, expectedExpression, collectionExpression, shouldContain: true);

    /// <summary>
    /// Tests whether the specified span contains the given element.
    /// </summary>
    /// <typeparam name="T">The type of the span items.</typeparam>
    /// <param name="expected">The expected item.</param>
    /// <param name="collection">The span.</param>
    /// <param name="message">The message to display when the assertion fails.</param>
    /// <param name="expectedExpression">
    /// The syntactic expression of expected as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    /// <param name="collectionExpression">
    /// The syntactic expression of collection as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    public static void Contains<T>(T expected, Span<T> collection, string? message = "", [CallerArgumentExpression(nameof(expected))] string expectedExpression = "", [CallerArgumentExpression(nameof(collection))] string collectionExpression = "")
        => Contains(expected, (ReadOnlySpan<T>)collection, message, expectedExpression, collectionExpression);

    /// <summary>
    /// Tests whether the specified memory contains the given element.
    /// </summary>
    /// <typeparam name="T">The type of the memory items.</typeparam>
    /// <param name="expected">The expected item.</param>
    /// <param name="collection">The memory.</param>
    /// <param name="message">The message to display when the assertion fails.</param>
    /// <param name="expectedExpression">
    /// The syntactic expression of expected as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    /// <param name="collectionExpression">
    /// The syntactic expression of collection as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    public static void Contains<T>(T expected, ReadOnlyMemory<T> collection, string? message = "", [CallerArgumentExpression(nameof(expected))] string expectedExpression = "", [CallerArgumentExpression(nameof(collection))] string collectionExpression = "")
        => Contains(expected, collection.Span, message, expectedExpression, collectionExpression);

    /// <summary>
    /// Tests whether the specified memory contains the given element.
    /// </summary>
    /// <typeparam name="T">The type of the memory items.</typeparam>
    /// <param name="expected">The expected item.</param>
    /// <param name="collection">The memory.</param>
    /// <param name="message">The message to display when the assertion fails.</param>
    /// <param name="expectedExpression">
    /// The syntactic expression of expected as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    /// <param name="collectionExpression">
    /// The syntactic expression of collection as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    public static void Contains<T>(T expected, Memory<T> collection, string? message = "", [CallerArgumentExpression(nameof(expected))] string expectedExpression = "", [CallerArgumentExpression(nameof(collection))] string collectionExpression = "")
        => Contains(expected, collection.Span, message, expectedExpression, collectionExpression);

    /// <summary>
    /// Tests whether the specified span contains the given element.
    /// </summary>
    /// <typeparam name="T">The type of the span items.</typeparam>
    /// <param name="expected">The expected item.</param>
    /// <param name="collection">The span.</param>
    /// <param name="comparer">An equality comparer to compare values.</param>
    /// <param name="message">The message to display when the assertion fails.</param>
    /// <param name="expectedExpression">
    /// The syntactic expression of expected as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    /// <param name="collectionExpression">
    /// The syntactic expression of collection as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    public static void Contains<T>(T expected, ReadOnlySpan<T> collection, IEqualityComparer<T> comparer, string? message = "", [CallerArgumentExpression(nameof(expected))] string expectedExpression = "", [CallerArgumentExpression(nameof(collection))] string collectionExpression = "")
        => ContainsCore(expected, collection.ToArray(), comparer, "Assert.Contains", message, expectedExpression, collectionExpression, shouldContain: true);

    /// <summary>
    /// Tests whether the specified span contains the given element.
    /// </summary>
    /// <typeparam name="T">The type of the span items.</typeparam>
    /// <param name="expected">The expected item.</param>
    /// <param name="collection">The span.</param>
    /// <param name="comparer">An equality comparer to compare values.</param>
    /// <param name="message">The message to display when the assertion fails.</param>
    /// <param name="expectedExpression">
    /// The syntactic expression of expected as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    /// <param name="collectionExpression">
    /// The syntactic expression of collection as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    public static void Contains<T>(T expected, Span<T> collection, IEqualityComparer<T> comparer, string? message = "", [CallerArgumentExpression(nameof(expected))] string expectedExpression = "", [CallerArgumentExpression(nameof(collection))] string collectionExpression = "")
        => Contains(expected, (ReadOnlySpan<T>)collection, comparer, message, expectedExpression, collectionExpression);

    /// <summary>
    /// Tests whether the specified memory contains the given element.
    /// </summary>
    /// <typeparam name="T">The type of the memory items.</typeparam>
    /// <param name="expected">The expected item.</param>
    /// <param name="collection">The memory.</param>
    /// <param name="comparer">An equality comparer to compare values.</param>
    /// <param name="message">The message to display when the assertion fails.</param>
    /// <param name="expectedExpression">
    /// The syntactic expression of expected as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    /// <param name="collectionExpression">
    /// The syntactic expression of collection as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    public static void Contains<T>(T expected, ReadOnlyMemory<T> collection, IEqualityComparer<T> comparer, string? message = "", [CallerArgumentExpression(nameof(expected))] string expectedExpression = "", [CallerArgumentExpression(nameof(collection))] string collectionExpression = "")
        => Contains(expected, collection.Span, comparer, message, expectedExpression, collectionExpression);

    /// <summary>
    /// Tests whether the specified memory contains the given element.
    /// </summary>
    /// <typeparam name="T">The type of the memory items.</typeparam>
    /// <param name="expected">The expected item.</param>
    /// <param name="collection">The memory.</param>
    /// <param name="comparer">An equality comparer to compare values.</param>
    /// <param name="message">The message to display when the assertion fails.</param>
    /// <param name="expectedExpression">
    /// The syntactic expression of expected as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    /// <param name="collectionExpression">
    /// The syntactic expression of collection as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    public static void Contains<T>(T expected, Memory<T> collection, IEqualityComparer<T> comparer, string? message = "", [CallerArgumentExpression(nameof(expected))] string expectedExpression = "", [CallerArgumentExpression(nameof(collection))] string collectionExpression = "")
        => Contains(expected, collection.Span, comparer, message, expectedExpression, collectionExpression);

    /// <summary>
    /// Tests whether the specified span contains an element matching the given predicate.
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
    public static void Contains<T>(Func<T, bool> predicate, ReadOnlySpan<T> collection, string? message = "", [CallerArgumentExpression(nameof(predicate))] string predicateExpression = "", [CallerArgumentExpression(nameof(collection))] string collectionExpression = "")
        => ContainsCore(predicate, collection.ToArray(), "Assert.Contains", message, predicateExpression, collectionExpression, shouldContain: true);

    /// <summary>
    /// Tests whether the specified span contains an element matching the given predicate.
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
    public static void Contains<T>(Func<T, bool> predicate, Span<T> collection, string? message = "", [CallerArgumentExpression(nameof(predicate))] string predicateExpression = "", [CallerArgumentExpression(nameof(collection))] string collectionExpression = "")
        => Contains(predicate, (ReadOnlySpan<T>)collection, message, predicateExpression, collectionExpression);

    /// <summary>
    /// Tests whether the specified memory contains an element matching the given predicate.
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
    public static void Contains<T>(Func<T, bool> predicate, ReadOnlyMemory<T> collection, string? message = "", [CallerArgumentExpression(nameof(predicate))] string predicateExpression = "", [CallerArgumentExpression(nameof(collection))] string collectionExpression = "")
        => Contains(predicate, collection.Span, message, predicateExpression, collectionExpression);

    /// <summary>
    /// Tests whether the specified memory contains an element matching the given predicate.
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
    public static void Contains<T>(Func<T, bool> predicate, Memory<T> collection, string? message = "", [CallerArgumentExpression(nameof(predicate))] string predicateExpression = "", [CallerArgumentExpression(nameof(collection))] string collectionExpression = "")
        => Contains(predicate, collection.Span, message, predicateExpression, collectionExpression);

#endif

    /// <summary>
    /// Tests whether the specified string contains the specified substring
    /// and throws an exception if the substring does not occur within the
    /// test string.
    /// </summary>
    /// <param name="substring">
    /// The string expected to occur within <paramref name="value"/>.
    /// </param>
    /// <param name="value">
    /// The string that is expected to contain <paramref name="substring"/>.
    /// </param>
    /// <param name="message">
    /// The message to include in the exception when <paramref name="substring"/>
    /// is not in <paramref name="value"/>. The message is shown in
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
    /// or <paramref name="value"/> does not contain <paramref name="substring"/>.
    /// </exception>
    public static void Contains(string substring, string value, string? message = "", [CallerArgumentExpression(nameof(substring))] string substringExpression = "", [CallerArgumentExpression(nameof(value))] string valueExpression = "")
        => Contains(substring, value, StringComparison.Ordinal, message, substringExpression, valueExpression);

    /// <summary>
    /// Tests whether the specified string contains the specified substring
    /// and throws an exception if the substring does not occur within the
    /// test string.
    /// </summary>
    /// <param name="substring">
    /// The string expected to occur within <paramref name="value"/>.
    /// </param>
    /// <param name="value">
    /// The string that is expected to contain <paramref name="substring"/>.
    /// </param>
    /// <param name="comparisonType">
    /// The comparison method to compare strings <paramref name="comparisonType"/>.
    /// </param>
    /// <param name="message">
    /// The message to include in the exception when <paramref name="substring"/>
    /// is not in <paramref name="value"/>. The message is shown in
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
    /// or <paramref name="value"/> does not contain <paramref name="substring"/>.
    /// </exception>
    public static void Contains(string substring, string value, StringComparison comparisonType, string? message = "", [CallerArgumentExpression(nameof(substring))] string substringExpression = "", [CallerArgumentExpression(nameof(value))] string valueExpression = "")
        => ContainsCore(substring, value, comparisonType, "Assert.Contains", message, substringExpression, valueExpression, shouldContain: true);

    #endregion // Contains
    [DoesNotReturn]
    private static void ReportAssertContainsItemFailed(object? expected, string? userMessage, string expectedExpression, string collectionExpression, object? comparer = null)
    {
        string expectedText = AssertionValueRenderer.RenderValue(expected);
        EvidenceBlock evidence = EvidenceBlock.Create().AddLine("expected:", expectedText);
        if (comparer is not null)
        {
            evidence.AddLine("comparer:", comparer.GetType().Name);
        }

        StructuredAssertionMessage structured = new(FrameworkMessages.ContainsItemFailedSummary);
        structured.WithUserMessage(userMessage);
        structured.WithEvidence(evidence);
        structured.WithExpectedAndActual(expectedText, null);
        structured.WithCallSiteExpression(FormatCallSiteExpression("Assert.Contains", expectedExpression, collectionExpression, "<expected>", "<collection>"));
        ReportAssertFailed(structured);
    }

    [DoesNotReturn]
    private static void ReportAssertContainsPredicateFailed(string? userMessage, string predicateExpression, string collectionExpression)
    {
        StructuredAssertionMessage structured = new(FrameworkMessages.ContainsPredicateFailedSummary);
        structured.WithUserMessage(userMessage);
        structured.WithCallSiteExpression(FormatCallSiteExpression("Assert.Contains", predicateExpression, collectionExpression, "<predicate>", "<collection>"));
        ReportAssertFailed(structured);
    }

    [DoesNotReturn]
    private static void ReportAssertContainsSubstringFailed(string substring, string value, StringComparison comparisonType, string? userMessage, string substringExpression, string valueExpression)
    {
        string expectedText = AssertionValueRenderer.RenderValue(substring);
        string actualText = AssertionValueRenderer.RenderValue(value);
        EvidenceBlock evidence = EvidenceBlock.Create()
            .AddLine("expected substring:", expectedText)
            .AddLine("actual:", actualText)
            .AddLine("comparison:", comparisonType.ToString());
        StructuredAssertionMessage structured = new(FrameworkMessages.ContainsSubstringFailedSummary);
        structured.WithUserMessage(userMessage);
        structured.WithEvidence(evidence);
        structured.WithExpectedAndActual(expectedText, actualText);
        structured.WithCallSiteExpression(FormatCallSiteExpression("Assert.Contains", substringExpression, valueExpression, "<substring>", "<value>"));
        ReportAssertFailed(structured);
    }
}
