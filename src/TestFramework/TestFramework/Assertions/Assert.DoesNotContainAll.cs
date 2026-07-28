// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestTools.UnitTesting;

#pragma warning disable RS0026 // Do not add multiple public overloads with optional parameters
#pragma warning disable RS0027 // API with optional parameter(s) should have the most parameters amongst its public overloads
public sealed partial class Assert
{
    #region DoesNotContainAll

    /// <summary>
    /// Tests whether <paramref name="collection"/> does not contain every element of <paramref name="notExpected"/>
    /// (with multiplicity) and throws an exception if every element in <paramref name="notExpected"/> occurs at
    /// least as many times in <paramref name="collection"/>.
    /// </summary>
    /// <remarks>
    /// Element multiplicity is significant: <c>[1]</c> is considered not to contain all of <c>[1, 1]</c>
    /// (so this assertion would pass for those inputs).
    /// </remarks>
    /// <typeparam name="T">The type of the collection items.</typeparam>
    /// <param name="notExpected">
    /// The collection of items the test expects not to all be present in <paramref name="collection"/>.
    /// </param>
    /// <param name="collection">
    /// The collection expected not to contain every item of <paramref name="notExpected"/>.
    /// </param>
    /// <param name="message">
    /// The message to include in the exception when every element in <paramref name="notExpected"/>
    /// is also found (with sufficient multiplicity) in <paramref name="collection"/>. The message is shown in test results.
    /// </param>
    /// <param name="notExpectedExpression">
    /// The syntactic expression of notExpected as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    /// <param name="collectionExpression">
    /// The syntactic expression of collection as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// Thrown if every element of <paramref name="notExpected"/> occurs at least as many times in <paramref name="collection"/>.
    /// </exception>
    public static void DoesNotContainAll<T>([NotNull] IEnumerable<T>? notExpected, [NotNull] IEnumerable<T>? collection, string? message = "", [CallerArgumentExpression(nameof(notExpected))] string notExpectedExpression = "", [CallerArgumentExpression(nameof(collection))] string collectionExpression = "")
    {
        TelemetryCollector.TrackAssertionCall("Assert.DoesNotContainAll");
        CheckParameterNotNull(notExpected, "Assert.DoesNotContainAll", "notExpected");
        CheckParameterNotNull(collection, "Assert.DoesNotContainAll", "collection");
        DoesNotContainAllImpl(notExpected, collection, EqualityComparer<T>.Default, comparerName: null, message, notExpectedExpression, collectionExpression);
    }

    /// <summary>
    /// Tests whether <paramref name="collection"/> does not contain every element of <paramref name="notExpected"/>
    /// (with multiplicity) and throws an exception if every element in <paramref name="notExpected"/> occurs at
    /// least as many times in <paramref name="collection"/>.
    /// </summary>
    /// <remarks>
    /// Element multiplicity is significant: <c>[1]</c> is considered not to contain all of <c>[1, 1]</c>
    /// (so this assertion would pass for those inputs).
    /// </remarks>
    /// <typeparam name="T">The type of the collection items.</typeparam>
    /// <param name="notExpected">
    /// The collection of items the test expects not to all be present in <paramref name="collection"/>.
    /// </param>
    /// <param name="collection">
    /// The collection expected not to contain every item of <paramref name="notExpected"/>.
    /// </param>
    /// <param name="comparer">
    /// The equality comparer to use when comparing elements.
    /// </param>
    /// <param name="message">
    /// The message to include in the exception when every element in <paramref name="notExpected"/>
    /// is also found (with sufficient multiplicity) in <paramref name="collection"/>. The message is shown in test results.
    /// </param>
    /// <param name="notExpectedExpression">
    /// The syntactic expression of notExpected as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    /// <param name="collectionExpression">
    /// The syntactic expression of collection as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// Thrown if every element of <paramref name="notExpected"/> occurs at least as many times in <paramref name="collection"/>.
    /// </exception>
    public static void DoesNotContainAll<T>([NotNull] IEnumerable<T>? notExpected, [NotNull] IEnumerable<T>? collection, [NotNull] IEqualityComparer<T>? comparer, string? message = "", [CallerArgumentExpression(nameof(notExpected))] string notExpectedExpression = "", [CallerArgumentExpression(nameof(collection))] string collectionExpression = "")
    {
        TelemetryCollector.TrackAssertionCall("Assert.DoesNotContainAll");
        CheckParameterNotNull(notExpected, "Assert.DoesNotContainAll", "notExpected");
        CheckParameterNotNull(collection, "Assert.DoesNotContainAll", "collection");
        CheckParameterNotNull(comparer, "Assert.DoesNotContainAll", "comparer");
        DoesNotContainAllImpl(notExpected, collection, comparer, comparer.GetType().Name, message, notExpectedExpression, collectionExpression);
    }

    /// <summary>
    /// Tests whether <paramref name="collection"/> does not contain every element of <paramref name="notExpected"/>
    /// (with multiplicity) and throws an exception if every element in <paramref name="notExpected"/> occurs at
    /// least as many times in <paramref name="collection"/>.
    /// </summary>
    /// <remarks>
    /// Element multiplicity is significant: <c>[1]</c> is considered not to contain all of <c>[1, 1]</c>
    /// (so this assertion would pass for those inputs).
    /// </remarks>
    /// <param name="notExpected">
    /// The collection of items the test expects not to all be present in <paramref name="collection"/>.
    /// </param>
    /// <param name="collection">
    /// The collection expected not to contain every item of <paramref name="notExpected"/>.
    /// </param>
    /// <param name="message">
    /// The message to include in the exception when every element in <paramref name="notExpected"/>
    /// is also found (with sufficient multiplicity) in <paramref name="collection"/>. The message is shown in test results.
    /// </param>
    /// <param name="notExpectedExpression">
    /// The syntactic expression of notExpected as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    /// <param name="collectionExpression">
    /// The syntactic expression of collection as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// Thrown if every element of <paramref name="notExpected"/> occurs at least as many times in <paramref name="collection"/>.
    /// </exception>
    public static void DoesNotContainAll([NotNull] IEnumerable? notExpected, [NotNull] IEnumerable? collection, string? message = "", [CallerArgumentExpression(nameof(notExpected))] string notExpectedExpression = "", [CallerArgumentExpression(nameof(collection))] string collectionExpression = "")
    {
        TelemetryCollector.TrackAssertionCall("Assert.DoesNotContainAll");
        CheckParameterNotNull(notExpected, "Assert.DoesNotContainAll", "notExpected");
        CheckParameterNotNull(collection, "Assert.DoesNotContainAll", "collection");

        DoesNotContainAllImpl(notExpected.Cast<object?>(), collection.Cast<object?>(), EqualityComparer<object?>.Default, comparerName: null, message, notExpectedExpression, collectionExpression);
    }

    /// <summary>
    /// Tests whether <paramref name="collection"/> does not contain every element of <paramref name="notExpected"/>
    /// (with multiplicity) and throws an exception if every element in <paramref name="notExpected"/> occurs at
    /// least as many times in <paramref name="collection"/>.
    /// </summary>
    /// <remarks>
    /// Element multiplicity is significant: <c>[1]</c> is considered not to contain all of <c>[1, 1]</c>
    /// (so this assertion would pass for those inputs).
    /// </remarks>
    /// <param name="notExpected">
    /// The collection of items the test expects not to all be present in <paramref name="collection"/>.
    /// </param>
    /// <param name="collection">
    /// The collection expected not to contain every item of <paramref name="notExpected"/>.
    /// </param>
    /// <param name="comparer">
    /// The equality comparer to use when comparing elements.
    /// </param>
    /// <param name="message">
    /// The message to include in the exception when every element in <paramref name="notExpected"/>
    /// is also found (with sufficient multiplicity) in <paramref name="collection"/>. The message is shown in test results.
    /// </param>
    /// <param name="notExpectedExpression">
    /// The syntactic expression of notExpected as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    /// <param name="collectionExpression">
    /// The syntactic expression of collection as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// Thrown if every element of <paramref name="notExpected"/> occurs at least as many times in <paramref name="collection"/>.
    /// </exception>
    public static void DoesNotContainAll([NotNull] IEnumerable? notExpected, [NotNull] IEnumerable? collection, [NotNull] IEqualityComparer? comparer, string? message = "", [CallerArgumentExpression(nameof(notExpected))] string notExpectedExpression = "", [CallerArgumentExpression(nameof(collection))] string collectionExpression = "")
    {
        TelemetryCollector.TrackAssertionCall("Assert.DoesNotContainAll");
        CheckParameterNotNull(notExpected, "Assert.DoesNotContainAll", "notExpected");
        CheckParameterNotNull(collection, "Assert.DoesNotContainAll", "collection");
        CheckParameterNotNull(comparer, "Assert.DoesNotContainAll", "comparer");

        DoesNotContainAllImpl(notExpected.Cast<object?>(), collection.Cast<object?>(), new NonGenericEqualityComparerAdapter(comparer), comparer.GetType().Name, message, notExpectedExpression, collectionExpression);
    }

    #endregion // DoesNotContainAll

#if NETCOREAPP3_1_OR_GREATER

    #region DoesNotContainAll span/memory

    /// <summary>
    /// Tests whether <paramref name="collection"/> does not contain every element of <paramref name="notExpected"/> (with multiplicity).
    /// </summary>
    /// <typeparam name="T">The type of the collection items.</typeparam>
    /// <param name="notExpected">The span of items the test expects not to all be present in <paramref name="collection"/>.</param>
    /// <param name="collection">The span expected not to contain every item of <paramref name="notExpected"/>.</param>
    /// <param name="message">The message to include in the exception when the assertion fails.</param>
    /// <param name="notExpectedExpression">
    /// The syntactic expression of notExpected as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    /// <param name="collectionExpression">
    /// The syntactic expression of collection as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    public static void DoesNotContainAll<T>(ReadOnlySpan<T> notExpected, ReadOnlySpan<T> collection, string? message = "", [CallerArgumentExpression(nameof(notExpected))] string notExpectedExpression = "", [CallerArgumentExpression(nameof(collection))] string collectionExpression = "")
    {
        TelemetryCollector.TrackAssertionCall("Assert.DoesNotContainAll");
        DoesNotContainAllImpl<T>(notExpected.ToArray(), collection.ToArray(), EqualityComparer<T>.Default, comparerName: null, message, notExpectedExpression, collectionExpression);
    }

    /// <summary>
    /// Tests whether <paramref name="collection"/> does not contain every element of <paramref name="notExpected"/> (with multiplicity).
    /// </summary>
    /// <typeparam name="T">The type of the collection items.</typeparam>
    /// <param name="notExpected">The span of items the test expects not to all be present in <paramref name="collection"/>.</param>
    /// <param name="collection">The span expected not to contain every item of <paramref name="notExpected"/>.</param>
    /// <param name="comparer">The equality comparer to use when comparing elements.</param>
    /// <param name="message">The message to include in the exception when the assertion fails.</param>
    /// <param name="notExpectedExpression">
    /// The syntactic expression of notExpected as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    /// <param name="collectionExpression">
    /// The syntactic expression of collection as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    public static void DoesNotContainAll<T>(ReadOnlySpan<T> notExpected, ReadOnlySpan<T> collection, [NotNull] IEqualityComparer<T>? comparer, string? message = "", [CallerArgumentExpression(nameof(notExpected))] string notExpectedExpression = "", [CallerArgumentExpression(nameof(collection))] string collectionExpression = "")
    {
        TelemetryCollector.TrackAssertionCall("Assert.DoesNotContainAll");
        CheckParameterNotNull(comparer, "Assert.DoesNotContainAll", "comparer");
        DoesNotContainAllImpl<T>(notExpected.ToArray(), collection.ToArray(), comparer, comparer.GetType().Name, message, notExpectedExpression, collectionExpression);
    }

    /// <summary>
    /// Tests whether <paramref name="collection"/> does not contain every element of <paramref name="notExpected"/> (with multiplicity).
    /// </summary>
    /// <typeparam name="T">The type of the collection items.</typeparam>
    /// <param name="notExpected">The span of items the test expects not to all be present in <paramref name="collection"/>.</param>
    /// <param name="collection">The span expected not to contain every item of <paramref name="notExpected"/>.</param>
    /// <param name="message">The message to include in the exception when the assertion fails.</param>
    /// <param name="notExpectedExpression">
    /// The syntactic expression of notExpected as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    /// <param name="collectionExpression">
    /// The syntactic expression of collection as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    public static void DoesNotContainAll<T>(Span<T> notExpected, Span<T> collection, string? message = "", [CallerArgumentExpression(nameof(notExpected))] string notExpectedExpression = "", [CallerArgumentExpression(nameof(collection))] string collectionExpression = "")
        => DoesNotContainAll((ReadOnlySpan<T>)notExpected, collection, message, notExpectedExpression, collectionExpression);

    /// <summary>
    /// Tests whether <paramref name="collection"/> does not contain every element of <paramref name="notExpected"/> (with multiplicity).
    /// </summary>
    /// <typeparam name="T">The type of the collection items.</typeparam>
    /// <param name="notExpected">The span of items the test expects not to all be present in <paramref name="collection"/>.</param>
    /// <param name="collection">The span expected not to contain every item of <paramref name="notExpected"/>.</param>
    /// <param name="comparer">The equality comparer to use when comparing elements.</param>
    /// <param name="message">The message to include in the exception when the assertion fails.</param>
    /// <param name="notExpectedExpression">
    /// The syntactic expression of notExpected as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    /// <param name="collectionExpression">
    /// The syntactic expression of collection as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    public static void DoesNotContainAll<T>(Span<T> notExpected, Span<T> collection, [NotNull] IEqualityComparer<T>? comparer, string? message = "", [CallerArgumentExpression(nameof(notExpected))] string notExpectedExpression = "", [CallerArgumentExpression(nameof(collection))] string collectionExpression = "")
        => DoesNotContainAll((ReadOnlySpan<T>)notExpected, collection, comparer, message, notExpectedExpression, collectionExpression);

    /// <summary>
    /// Tests whether <paramref name="collection"/> does not contain every element of <paramref name="notExpected"/> (with multiplicity).
    /// </summary>
    /// <typeparam name="T">The type of the collection items.</typeparam>
    /// <param name="notExpected">The memory of items the test expects not to all be present in <paramref name="collection"/>.</param>
    /// <param name="collection">The memory expected not to contain every item of <paramref name="notExpected"/>.</param>
    /// <param name="message">The message to include in the exception when the assertion fails.</param>
    /// <param name="notExpectedExpression">
    /// The syntactic expression of notExpected as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    /// <param name="collectionExpression">
    /// The syntactic expression of collection as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    public static void DoesNotContainAll<T>(ReadOnlyMemory<T> notExpected, ReadOnlyMemory<T> collection, string? message = "", [CallerArgumentExpression(nameof(notExpected))] string notExpectedExpression = "", [CallerArgumentExpression(nameof(collection))] string collectionExpression = "")
        => DoesNotContainAll(notExpected.Span, collection.Span, message, notExpectedExpression, collectionExpression);

    /// <summary>
    /// Tests whether <paramref name="collection"/> does not contain every element of <paramref name="notExpected"/> (with multiplicity).
    /// </summary>
    /// <typeparam name="T">The type of the collection items.</typeparam>
    /// <param name="notExpected">The memory of items the test expects not to all be present in <paramref name="collection"/>.</param>
    /// <param name="collection">The memory expected not to contain every item of <paramref name="notExpected"/>.</param>
    /// <param name="comparer">The equality comparer to use when comparing elements.</param>
    /// <param name="message">The message to include in the exception when the assertion fails.</param>
    /// <param name="notExpectedExpression">
    /// The syntactic expression of notExpected as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    /// <param name="collectionExpression">
    /// The syntactic expression of collection as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    public static void DoesNotContainAll<T>(ReadOnlyMemory<T> notExpected, ReadOnlyMemory<T> collection, [NotNull] IEqualityComparer<T>? comparer, string? message = "", [CallerArgumentExpression(nameof(notExpected))] string notExpectedExpression = "", [CallerArgumentExpression(nameof(collection))] string collectionExpression = "")
        => DoesNotContainAll(notExpected.Span, collection.Span, comparer, message, notExpectedExpression, collectionExpression);

    /// <summary>
    /// Tests whether <paramref name="collection"/> does not contain every element of <paramref name="notExpected"/> (with multiplicity).
    /// </summary>
    /// <typeparam name="T">The type of the collection items.</typeparam>
    /// <param name="notExpected">The memory of items the test expects not to all be present in <paramref name="collection"/>.</param>
    /// <param name="collection">The memory expected not to contain every item of <paramref name="notExpected"/>.</param>
    /// <param name="message">The message to include in the exception when the assertion fails.</param>
    /// <param name="notExpectedExpression">
    /// The syntactic expression of notExpected as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    /// <param name="collectionExpression">
    /// The syntactic expression of collection as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    public static void DoesNotContainAll<T>(Memory<T> notExpected, Memory<T> collection, string? message = "", [CallerArgumentExpression(nameof(notExpected))] string notExpectedExpression = "", [CallerArgumentExpression(nameof(collection))] string collectionExpression = "")
        => DoesNotContainAll(notExpected.Span, collection.Span, message, notExpectedExpression, collectionExpression);

    /// <summary>
    /// Tests whether <paramref name="collection"/> does not contain every element of <paramref name="notExpected"/> (with multiplicity).
    /// </summary>
    /// <typeparam name="T">The type of the collection items.</typeparam>
    /// <param name="notExpected">The memory of items the test expects not to all be present in <paramref name="collection"/>.</param>
    /// <param name="collection">The memory expected not to contain every item of <paramref name="notExpected"/>.</param>
    /// <param name="comparer">The equality comparer to use when comparing elements.</param>
    /// <param name="message">The message to include in the exception when the assertion fails.</param>
    /// <param name="notExpectedExpression">
    /// The syntactic expression of notExpected as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    /// <param name="collectionExpression">
    /// The syntactic expression of collection as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    public static void DoesNotContainAll<T>(Memory<T> notExpected, Memory<T> collection, [NotNull] IEqualityComparer<T>? comparer, string? message = "", [CallerArgumentExpression(nameof(notExpected))] string notExpectedExpression = "", [CallerArgumentExpression(nameof(collection))] string collectionExpression = "")
        => DoesNotContainAll(notExpected.Span, collection.Span, comparer, message, notExpectedExpression, collectionExpression);

    #endregion // DoesNotContainAll span/memory

#endif

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

    private static void DoesNotContainAllImpl<T>(IEnumerable<T?> notExpected, IEnumerable<T?> collection, IEqualityComparer<T> comparer, string? comparerName, string? message, string notExpectedExpression, string collectionExpression)
    {
        List<T?> notExpectedList = notExpected is List<T?> nel ? nel : [.. notExpected];
        List<T?> collectionList = collection is List<T?> cl ? cl : [.. collection];

        if (!HasAnyMissingElement(notExpectedList, collectionList, comparer))
        {
            ReportAssertDoesNotContainAllFailed(notExpectedList, collectionList, comparerName, message, notExpectedExpression, collectionExpression);
        }
    }

    [DoesNotReturn]
    private static void ReportAssertDoesNotContainAllFailed<T>(IEnumerable<T?> notExpected, IEnumerable<T?> collection, string? comparerName, string? message, string notExpectedExpression, string collectionExpression)
    {
        string notExpectedText = AssertionValueRenderer.RenderValue(notExpected);
        string collectionText = AssertionValueRenderer.RenderValue(collection);

        EvidenceBlock evidence = EvidenceBlock.Create()
            .AddLine("notExpected:", notExpectedText)
            .AddLine("collection:", collectionText);

        if (comparerName is not null)
        {
            evidence.AddLine("comparer:", comparerName);
        }

        StructuredAssertionMessage structured = new(FrameworkMessages.DoesNotContainAllFailedSummary);
        structured.WithUserMessage(message);
        structured.WithEvidence(evidence);
        structured.WithExpectedAndActual(expectedText: null, actualText: collectionText);
        structured.WithCallSiteExpression(BuildCallSiteWithComparer("Assert.DoesNotContainAll", notExpectedExpression, collectionExpression, comparerName is not null, firstArgPlaceholder: "<notExpected>"));

        ReportAssertFailed(structured);
    }
}
