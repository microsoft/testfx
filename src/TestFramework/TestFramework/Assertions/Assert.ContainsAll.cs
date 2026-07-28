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

    #region ContainsAll span/memory

    /// <summary>
    /// Tests whether <paramref name="collection"/> contains every element of <paramref name="expected"/> (with multiplicity).
    /// </summary>
    /// <typeparam name="T">The type of the collection items.</typeparam>
    /// <param name="expected">The span of items expected to all be present in <paramref name="collection"/>.</param>
    /// <param name="collection">The span expected to contain every item of <paramref name="expected"/>.</param>
    /// <param name="message">The message to include in the exception when the assertion fails.</param>
    /// <param name="expectedExpression">
    /// The syntactic expression of expected as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    /// <param name="collectionExpression">
    /// The syntactic expression of collection as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    public static void ContainsAll<T>(ReadOnlySpan<T> expected, ReadOnlySpan<T> collection, string? message = "", [CallerArgumentExpression(nameof(expected))] string expectedExpression = "", [CallerArgumentExpression(nameof(collection))] string collectionExpression = "")
    {
        TelemetryCollector.TrackAssertionCall("Assert.ContainsAll");
        ContainsAllImpl<T>(expected.ToArray(), collection.ToArray(), EqualityComparer<T>.Default, comparerName: null, message, expectedExpression, collectionExpression);
    }

    /// <summary>
    /// Tests whether <paramref name="collection"/> contains every element of <paramref name="expected"/> (with multiplicity).
    /// </summary>
    /// <typeparam name="T">The type of the collection items.</typeparam>
    /// <param name="expected">The span of items expected to all be present in <paramref name="collection"/>.</param>
    /// <param name="collection">The span expected to contain every item of <paramref name="expected"/>.</param>
    /// <param name="comparer">The equality comparer to use when comparing elements.</param>
    /// <param name="message">The message to include in the exception when the assertion fails.</param>
    /// <param name="expectedExpression">
    /// The syntactic expression of expected as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    /// <param name="collectionExpression">
    /// The syntactic expression of collection as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    public static void ContainsAll<T>(ReadOnlySpan<T> expected, ReadOnlySpan<T> collection, [NotNull] IEqualityComparer<T>? comparer, string? message = "", [CallerArgumentExpression(nameof(expected))] string expectedExpression = "", [CallerArgumentExpression(nameof(collection))] string collectionExpression = "")
    {
        TelemetryCollector.TrackAssertionCall("Assert.ContainsAll");
        CheckParameterNotNull(comparer, "Assert.ContainsAll", "comparer");
        ContainsAllImpl<T>(expected.ToArray(), collection.ToArray(), comparer, comparer.GetType().Name, message, expectedExpression, collectionExpression);
    }

    /// <summary>
    /// Tests whether <paramref name="collection"/> contains every element of <paramref name="expected"/> (with multiplicity).
    /// </summary>
    /// <typeparam name="T">The type of the collection items.</typeparam>
    /// <param name="expected">The span of items expected to all be present in <paramref name="collection"/>.</param>
    /// <param name="collection">The span expected to contain every item of <paramref name="expected"/>.</param>
    /// <param name="message">The message to include in the exception when the assertion fails.</param>
    /// <param name="expectedExpression">
    /// The syntactic expression of expected as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    /// <param name="collectionExpression">
    /// The syntactic expression of collection as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    public static void ContainsAll<T>(Span<T> expected, Span<T> collection, string? message = "", [CallerArgumentExpression(nameof(expected))] string expectedExpression = "", [CallerArgumentExpression(nameof(collection))] string collectionExpression = "")
        => ContainsAll((ReadOnlySpan<T>)expected, collection, message, expectedExpression, collectionExpression);

    /// <summary>
    /// Tests whether <paramref name="collection"/> contains every element of <paramref name="expected"/> (with multiplicity).
    /// </summary>
    /// <typeparam name="T">The type of the collection items.</typeparam>
    /// <param name="expected">The span of items expected to all be present in <paramref name="collection"/>.</param>
    /// <param name="collection">The span expected to contain every item of <paramref name="expected"/>.</param>
    /// <param name="comparer">The equality comparer to use when comparing elements.</param>
    /// <param name="message">The message to include in the exception when the assertion fails.</param>
    /// <param name="expectedExpression">
    /// The syntactic expression of expected as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    /// <param name="collectionExpression">
    /// The syntactic expression of collection as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    public static void ContainsAll<T>(Span<T> expected, Span<T> collection, [NotNull] IEqualityComparer<T>? comparer, string? message = "", [CallerArgumentExpression(nameof(expected))] string expectedExpression = "", [CallerArgumentExpression(nameof(collection))] string collectionExpression = "")
        => ContainsAll((ReadOnlySpan<T>)expected, collection, comparer, message, expectedExpression, collectionExpression);

    /// <summary>
    /// Tests whether <paramref name="collection"/> contains every element of <paramref name="expected"/> (with multiplicity).
    /// </summary>
    /// <typeparam name="T">The type of the collection items.</typeparam>
    /// <param name="expected">The memory of items expected to all be present in <paramref name="collection"/>.</param>
    /// <param name="collection">The memory expected to contain every item of <paramref name="expected"/>.</param>
    /// <param name="message">The message to include in the exception when the assertion fails.</param>
    /// <param name="expectedExpression">
    /// The syntactic expression of expected as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    /// <param name="collectionExpression">
    /// The syntactic expression of collection as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    public static void ContainsAll<T>(ReadOnlyMemory<T> expected, ReadOnlyMemory<T> collection, string? message = "", [CallerArgumentExpression(nameof(expected))] string expectedExpression = "", [CallerArgumentExpression(nameof(collection))] string collectionExpression = "")
        => ContainsAll(expected.Span, collection.Span, message, expectedExpression, collectionExpression);

    /// <summary>
    /// Tests whether <paramref name="collection"/> contains every element of <paramref name="expected"/> (with multiplicity).
    /// </summary>
    /// <typeparam name="T">The type of the collection items.</typeparam>
    /// <param name="expected">The memory of items expected to all be present in <paramref name="collection"/>.</param>
    /// <param name="collection">The memory expected to contain every item of <paramref name="expected"/>.</param>
    /// <param name="comparer">The equality comparer to use when comparing elements.</param>
    /// <param name="message">The message to include in the exception when the assertion fails.</param>
    /// <param name="expectedExpression">
    /// The syntactic expression of expected as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    /// <param name="collectionExpression">
    /// The syntactic expression of collection as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    public static void ContainsAll<T>(ReadOnlyMemory<T> expected, ReadOnlyMemory<T> collection, [NotNull] IEqualityComparer<T>? comparer, string? message = "", [CallerArgumentExpression(nameof(expected))] string expectedExpression = "", [CallerArgumentExpression(nameof(collection))] string collectionExpression = "")
        => ContainsAll(expected.Span, collection.Span, comparer, message, expectedExpression, collectionExpression);

    /// <summary>
    /// Tests whether <paramref name="collection"/> contains every element of <paramref name="expected"/> (with multiplicity).
    /// </summary>
    /// <typeparam name="T">The type of the collection items.</typeparam>
    /// <param name="expected">The memory of items expected to all be present in <paramref name="collection"/>.</param>
    /// <param name="collection">The memory expected to contain every item of <paramref name="expected"/>.</param>
    /// <param name="message">The message to include in the exception when the assertion fails.</param>
    /// <param name="expectedExpression">
    /// The syntactic expression of expected as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    /// <param name="collectionExpression">
    /// The syntactic expression of collection as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    public static void ContainsAll<T>(Memory<T> expected, Memory<T> collection, string? message = "", [CallerArgumentExpression(nameof(expected))] string expectedExpression = "", [CallerArgumentExpression(nameof(collection))] string collectionExpression = "")
        => ContainsAll(expected.Span, collection.Span, message, expectedExpression, collectionExpression);

    /// <summary>
    /// Tests whether <paramref name="collection"/> contains every element of <paramref name="expected"/> (with multiplicity).
    /// </summary>
    /// <typeparam name="T">The type of the collection items.</typeparam>
    /// <param name="expected">The memory of items expected to all be present in <paramref name="collection"/>.</param>
    /// <param name="collection">The memory expected to contain every item of <paramref name="expected"/>.</param>
    /// <param name="comparer">The equality comparer to use when comparing elements.</param>
    /// <param name="message">The message to include in the exception when the assertion fails.</param>
    /// <param name="expectedExpression">
    /// The syntactic expression of expected as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    /// <param name="collectionExpression">
    /// The syntactic expression of collection as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    public static void ContainsAll<T>(Memory<T> expected, Memory<T> collection, [NotNull] IEqualityComparer<T>? comparer, string? message = "", [CallerArgumentExpression(nameof(expected))] string expectedExpression = "", [CallerArgumentExpression(nameof(collection))] string collectionExpression = "")
        => ContainsAll(expected.Span, collection.Span, comparer, message, expectedExpression, collectionExpression);

    #endregion // ContainsAll span/memory

#endif

    private static void ContainsAllImpl<T>(IEnumerable<T?> expected, IEnumerable<T?> collection, IEqualityComparer<T> comparer, string? comparerName, string? message, string expectedExpression, string collectionExpression)
    {
        // Snapshot once so we don't enumerate twice (counting + rendering on failure)
        // and so lazy/single-pass enumerables behave deterministically.
        List<T?> expectedList = expected is List<T?> el ? el : [.. expected];
        List<T?> collectionList = collection is List<T?> cl ? cl : [.. collection];

        if (TryFindMissingElements(expectedList, collectionList, comparer, out List<T?>? missing))
        {
            ReportAssertContainsAllFailed(expectedList, collectionList, missing, comparerName, message, expectedExpression, collectionExpression);
        }
    }

    /// <summary>
    /// Determines whether <paramref name="expected"/> contains any element not present
    /// (with sufficient multiplicity) in <paramref name="collection"/>.
    /// </summary>
    /// <returns>
    /// <see langword="true"/> if at least one element is missing — in which case <paramref name="missing"/>
    /// holds the excess elements (in their first-seen order in <paramref name="expected"/>) — and
    /// <see langword="false"/> when every element of <paramref name="expected"/> is matched in
    /// <paramref name="collection"/>.
    /// </returns>
    private static bool TryFindMissingElements<T>(IEnumerable<T?> expected, IEnumerable<T?> collection, IEqualityComparer<T> comparer, [NotNullWhen(true)] out List<T?>? missing)
    {
#pragma warning disable CS8714 // The type cannot be used as type parameter in the generic type or method. Nullability of type argument doesn't match 'notnull' constraint.
        Dictionary<T, int> collectionCounts = CountElements(collection, comparer, out int collectionNulls);
#pragma warning restore CS8714

        missing = null;

        // Walk the expected items in source order so excess elements appear in first-seen positional order
        // (with multiplicity preserved). For each element, decrement its remaining quota in the collection;
        // when the quota reaches zero, additional occurrences are reported as missing.
        foreach (T? element in expected)
        {
            if (element is null)
            {
                if (collectionNulls > 0)
                {
                    collectionNulls--;
                }
                else
                {
                    missing ??= [];
                    missing.Add(default);
                }

                continue;
            }

            if (collectionCounts.TryGetValue(element, out int remaining) && remaining > 0)
            {
                collectionCounts[element] = remaining - 1;
            }
            else
            {
                missing ??= [];
                missing.Add(element);
            }
        }

        return missing is not null;
    }

#pragma warning disable CS8714 // The type cannot be used as type parameter in the generic type or method. Nullability of type argument doesn't match 'notnull' constraint.
    private static Dictionary<T, int> CountElements<T>(IEnumerable<T?> collection, IEqualityComparer<T> comparer, out int nullCount)
    {
#pragma warning disable IDE0028 // Collection initialization can be simplified - target-typed new with constructor argument is preferred over collection expression here
        Dictionary<T, int> counts = new(comparer);
#pragma warning restore IDE0028
        nullCount = 0;
        foreach (T? element in collection)
        {
            if (element is null)
            {
                nullCount++;
                continue;
            }

            counts.TryGetValue(element, out int count);
            counts[element] = count + 1;
        }

        return counts;
    }
#pragma warning restore CS8714

    [DoesNotReturn]
    private static void ReportAssertContainsAllFailed<T>(IEnumerable<T?> expected, IEnumerable<T?> collection, List<T?> missing, string? comparerName, string? message, string expectedExpression, string collectionExpression)
    {
        string expectedText = AssertionValueRenderer.RenderValue(expected);
        string collectionText = AssertionValueRenderer.RenderValue(collection);
        string missingText = AssertionValueRenderer.RenderValue(missing);

        EvidenceBlock evidence = EvidenceBlock.Create()
            .AddLine("missing:", missingText)
            .AddLine("expected:", expectedText)
            .AddLine("collection:", collectionText);

        if (comparerName is not null)
        {
            evidence.AddLine("comparer:", comparerName);
        }

        StructuredAssertionMessage structured = new(FrameworkMessages.ContainsAllFailedSummary);
        structured.WithUserMessage(message);
        structured.WithEvidence(evidence);
        structured.WithExpectedAndActual(expectedText, collectionText);
        structured.WithCallSiteExpression(BuildCallSiteWithComparer("Assert.ContainsAll", expectedExpression, collectionExpression, comparerName is not null));

        ReportAssertFailed(structured);
    }

    private static string? BuildCallSiteWithComparer(string assertionMethodName, string firstArgExpression, string secondArgExpression, bool hasComparer, string firstArgPlaceholder = "<expected>")
        => hasComparer
            // No [CallerArgumentExpression] is captured for the comparer parameter, so the third
            // expression slot is always unavailable. Pass the "<comparer>" placeholder directly as
            // the third expression to ensure the call site is rendered (e.g. as
            // "Assert.ContainsAll(<expected>, <collection>, <comparer>)" or
            // "Assert.DoesNotContainAll(<notExpected>, <collection>, <comparer>)") even when callers
            // do not support [CallerArgumentExpression] and the other expressions are also empty.
            ? FormatCallSiteExpression(assertionMethodName, firstArgExpression, secondArgExpression, expression3: "<comparer>", firstArgPlaceholder, "<collection>", "<comparer>")
            : FormatCallSiteExpression(assertionMethodName, firstArgExpression, secondArgExpression, firstArgPlaceholder, "<collection>");
}
