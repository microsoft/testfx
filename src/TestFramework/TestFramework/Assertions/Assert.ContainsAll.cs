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
        CheckParameterNotNull(expected, "Assert.ContainsAll", "expected");
        CheckParameterNotNull(collection, "Assert.ContainsAll", "collection");
        CheckParameterNotNull(comparer, "Assert.ContainsAll", "comparer");

        ContainsAllImpl(expected.Cast<object?>(), collection.Cast<object?>(), new NonGenericEqualityComparerAdapter(comparer), comparer.GetType().Name, message, expectedExpression, collectionExpression);
    }

    #endregion // ContainsAll

    #region DoesNotContainAll

    /// <summary>
    /// Tests whether <paramref name="collection"/> does not contain every element of <paramref name="expected"/>
    /// (with multiplicity) and throws an exception if every element in <paramref name="expected"/> occurs at
    /// least as many times in <paramref name="collection"/>.
    /// </summary>
    /// <remarks>
    /// Element multiplicity is significant: <c>[1]</c> is considered not to contain all of <c>[1, 1]</c>
    /// (so this assertion would pass for those inputs).
    /// </remarks>
    /// <typeparam name="T">The type of the collection items.</typeparam>
    /// <param name="expected">
    /// The collection of items expected not to all be present in <paramref name="collection"/>.
    /// </param>
    /// <param name="collection">
    /// The collection expected not to contain every item of <paramref name="expected"/>.
    /// </param>
    /// <param name="message">
    /// The message to include in the exception when every element in <paramref name="expected"/>
    /// is also found (with sufficient multiplicity) in <paramref name="collection"/>. The message is shown in test results.
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
    /// Thrown if every element of <paramref name="expected"/> occurs at least as many times in <paramref name="collection"/>.
    /// </exception>
    public static void DoesNotContainAll<T>([NotNull] IEnumerable<T>? expected, [NotNull] IEnumerable<T>? collection, string? message = "", [CallerArgumentExpression(nameof(expected))] string expectedExpression = "", [CallerArgumentExpression(nameof(collection))] string collectionExpression = "")
    {
        CheckParameterNotNull(expected, "Assert.DoesNotContainAll", "expected");
        CheckParameterNotNull(collection, "Assert.DoesNotContainAll", "collection");
        DoesNotContainAllImpl(expected, collection, EqualityComparer<T>.Default, comparerName: null, message, expectedExpression, collectionExpression);
    }

    /// <summary>
    /// Tests whether <paramref name="collection"/> does not contain every element of <paramref name="expected"/>
    /// (with multiplicity) and throws an exception if every element in <paramref name="expected"/> occurs at
    /// least as many times in <paramref name="collection"/>.
    /// </summary>
    /// <remarks>
    /// Element multiplicity is significant: <c>[1]</c> is considered not to contain all of <c>[1, 1]</c>
    /// (so this assertion would pass for those inputs).
    /// </remarks>
    /// <typeparam name="T">The type of the collection items.</typeparam>
    /// <param name="expected">
    /// The collection of items expected not to all be present in <paramref name="collection"/>.
    /// </param>
    /// <param name="collection">
    /// The collection expected not to contain every item of <paramref name="expected"/>.
    /// </param>
    /// <param name="comparer">
    /// The equality comparer to use when comparing elements.
    /// </param>
    /// <param name="message">
    /// The message to include in the exception when every element in <paramref name="expected"/>
    /// is also found (with sufficient multiplicity) in <paramref name="collection"/>. The message is shown in test results.
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
    /// Thrown if every element of <paramref name="expected"/> occurs at least as many times in <paramref name="collection"/>.
    /// </exception>
    public static void DoesNotContainAll<T>([NotNull] IEnumerable<T>? expected, [NotNull] IEnumerable<T>? collection, [NotNull] IEqualityComparer<T>? comparer, string? message = "", [CallerArgumentExpression(nameof(expected))] string expectedExpression = "", [CallerArgumentExpression(nameof(collection))] string collectionExpression = "")
    {
        CheckParameterNotNull(expected, "Assert.DoesNotContainAll", "expected");
        CheckParameterNotNull(collection, "Assert.DoesNotContainAll", "collection");
        CheckParameterNotNull(comparer, "Assert.DoesNotContainAll", "comparer");
        DoesNotContainAllImpl(expected, collection, comparer, comparer.GetType().Name, message, expectedExpression, collectionExpression);
    }

    /// <summary>
    /// Tests whether <paramref name="collection"/> does not contain every element of <paramref name="expected"/>
    /// (with multiplicity) and throws an exception if every element in <paramref name="expected"/> occurs at
    /// least as many times in <paramref name="collection"/>.
    /// </summary>
    /// <remarks>
    /// Element multiplicity is significant: <c>[1]</c> is considered not to contain all of <c>[1, 1]</c>
    /// (so this assertion would pass for those inputs).
    /// </remarks>
    /// <param name="expected">
    /// The collection of items expected not to all be present in <paramref name="collection"/>.
    /// </param>
    /// <param name="collection">
    /// The collection expected not to contain every item of <paramref name="expected"/>.
    /// </param>
    /// <param name="message">
    /// The message to include in the exception when every element in <paramref name="expected"/>
    /// is also found (with sufficient multiplicity) in <paramref name="collection"/>. The message is shown in test results.
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
    /// Thrown if every element of <paramref name="expected"/> occurs at least as many times in <paramref name="collection"/>.
    /// </exception>
    public static void DoesNotContainAll([NotNull] IEnumerable? expected, [NotNull] IEnumerable? collection, string? message = "", [CallerArgumentExpression(nameof(expected))] string expectedExpression = "", [CallerArgumentExpression(nameof(collection))] string collectionExpression = "")
    {
        CheckParameterNotNull(expected, "Assert.DoesNotContainAll", "expected");
        CheckParameterNotNull(collection, "Assert.DoesNotContainAll", "collection");

        DoesNotContainAllImpl(expected.Cast<object?>(), collection.Cast<object?>(), EqualityComparer<object?>.Default, comparerName: null, message, expectedExpression, collectionExpression);
    }

    /// <summary>
    /// Tests whether <paramref name="collection"/> does not contain every element of <paramref name="expected"/>
    /// (with multiplicity) and throws an exception if every element in <paramref name="expected"/> occurs at
    /// least as many times in <paramref name="collection"/>.
    /// </summary>
    /// <remarks>
    /// Element multiplicity is significant: <c>[1]</c> is considered not to contain all of <c>[1, 1]</c>
    /// (so this assertion would pass for those inputs).
    /// </remarks>
    /// <param name="expected">
    /// The collection of items expected not to all be present in <paramref name="collection"/>.
    /// </param>
    /// <param name="collection">
    /// The collection expected not to contain every item of <paramref name="expected"/>.
    /// </param>
    /// <param name="comparer">
    /// The equality comparer to use when comparing elements.
    /// </param>
    /// <param name="message">
    /// The message to include in the exception when every element in <paramref name="expected"/>
    /// is also found (with sufficient multiplicity) in <paramref name="collection"/>. The message is shown in test results.
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
    /// Thrown if every element of <paramref name="expected"/> occurs at least as many times in <paramref name="collection"/>.
    /// </exception>
    public static void DoesNotContainAll([NotNull] IEnumerable? expected, [NotNull] IEnumerable? collection, [NotNull] IEqualityComparer? comparer, string? message = "", [CallerArgumentExpression(nameof(expected))] string expectedExpression = "", [CallerArgumentExpression(nameof(collection))] string collectionExpression = "")
    {
        CheckParameterNotNull(expected, "Assert.DoesNotContainAll", "expected");
        CheckParameterNotNull(collection, "Assert.DoesNotContainAll", "collection");
        CheckParameterNotNull(comparer, "Assert.DoesNotContainAll", "comparer");

        DoesNotContainAllImpl(expected.Cast<object?>(), collection.Cast<object?>(), new NonGenericEqualityComparerAdapter(comparer), comparer.GetType().Name, message, expectedExpression, collectionExpression);
    }

    #endregion // DoesNotContainAll

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

    private static void DoesNotContainAllImpl<T>(IEnumerable<T?> expected, IEnumerable<T?> collection, IEqualityComparer<T> comparer, string? comparerName, string? message, string expectedExpression, string collectionExpression)
    {
        List<T?> expectedList = expected is List<T?> el ? el : [.. expected];
        List<T?> collectionList = collection is List<T?> cl ? cl : [.. collection];

        if (!HasAnyMissingElement(expectedList, collectionList, comparer))
        {
            ReportAssertDoesNotContainAllFailed(expectedList, collectionList, comparerName, message, expectedExpression, collectionExpression);
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

    /// <summary>
    /// Equivalent to <see cref="TryFindMissingElements{T}(IEnumerable{T}, IEnumerable{T}, IEqualityComparer{T}, out List{T})"/>
    /// for the <c>DoesNotContainAll</c> path, but skips the <see cref="List{T}"/> of excess elements
    /// and exits the <paramref name="expected"/> walk on the first uncovered element. The
    /// <c>O(|collection|)</c> count-dictionary build is still performed up-front.
    /// </summary>
    private static bool HasAnyMissingElement<T>(IEnumerable<T?> expected, IEnumerable<T?> collection, IEqualityComparer<T> comparer)
    {
#pragma warning disable CS8714 // The type cannot be used as type parameter in the generic type or method. Nullability of type argument doesn't match 'notnull' constraint.
        Dictionary<T, int> collectionCounts = CountElements(collection, comparer, out int collectionNulls);
#pragma warning restore CS8714

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
                    return true;
                }

                continue;
            }

            if (collectionCounts.TryGetValue(element, out int remaining) && remaining > 0)
            {
                collectionCounts[element] = remaining - 1;
            }
            else
            {
                return true;
            }
        }

        return false;
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

    [DoesNotReturn]
    private static void ReportAssertDoesNotContainAllFailed<T>(IEnumerable<T?> expected, IEnumerable<T?> collection, string? comparerName, string? message, string expectedExpression, string collectionExpression)
    {
        string expectedText = AssertionValueRenderer.RenderValue(expected);
        string collectionText = AssertionValueRenderer.RenderValue(collection);

        EvidenceBlock evidence = EvidenceBlock.Create()
            .AddLine("expected:", expectedText)
            .AddLine("collection:", collectionText);

        if (comparerName is not null)
        {
            evidence.AddLine("comparer:", comparerName);
        }

        StructuredAssertionMessage structured = new(FrameworkMessages.DoesNotContainAllFailedSummary);
        structured.WithUserMessage(message);
        structured.WithEvidence(evidence);
        structured.WithExpectedAndActual(expectedText: null, actualText: collectionText);
        structured.WithCallSiteExpression(BuildCallSiteWithComparer("Assert.DoesNotContainAll", expectedExpression, collectionExpression, comparerName is not null));

        ReportAssertFailed(structured);
    }

    private static string? BuildCallSiteWithComparer(string assertionMethodName, string expectedExpression, string collectionExpression, bool hasComparer)
        => hasComparer
            // No [CallerArgumentExpression] is captured for the comparer parameter, so the third
            // expression slot is always unavailable. Pass the "<comparer>" placeholder directly as
            // the third expression to ensure the call site is rendered (e.g. as
            // "Assert.ContainsAll(<expected>, <collection>, <comparer>)") even when callers do not
            // support [CallerArgumentExpression] and the other expressions are also empty.
            ? FormatCallSiteExpression(assertionMethodName, expectedExpression, collectionExpression, expression3: "<comparer>", "<expected>", "<collection>", "<comparer>")
            : FormatCallSiteExpression(assertionMethodName, expectedExpression, collectionExpression, "<expected>", "<collection>");

    private sealed class NonGenericEqualityComparerAdapter : IEqualityComparer<object?>
    {
        private readonly IEqualityComparer _comparer;

        public NonGenericEqualityComparerAdapter(IEqualityComparer comparer)
            => _comparer = comparer;

        // The 'new' modifier suppresses CS0108: this instance method intentionally hides the
        // static 'object.Equals(object?, object?)' (only sharing its name/signature) to satisfy
        // the IEqualityComparer<object?>.Equals contract. There is nothing to override.
        public new bool Equals(object? x, object? y) => _comparer.Equals(x, y);

        public int GetHashCode(object? obj) => obj is null ? 0 : _comparer.GetHashCode(obj);
    }
}
