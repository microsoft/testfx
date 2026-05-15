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
    #region AreAllDistinct

    /// <summary>
    /// Tests whether all items in the specified collection are distinct (no two
    /// elements are equal) and throws an exception if any two elements in the
    /// collection are equal.
    /// </summary>
    /// <typeparam name="T">The type of the collection items.</typeparam>
    /// <param name="collection">
    /// The collection in which to search for duplicate elements.
    /// </param>
    /// <param name="message">
    /// The message to include in the exception when <paramref name="collection"/> contains
    /// at least one duplicate element. The message is shown in test results.
    /// </param>
    /// <param name="collectionExpression">
    /// The syntactic expression of collection as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// Thrown if <paramref name="collection"/> is null or contains at least one duplicate element.
    /// </exception>
    public static void AreAllDistinct<T>([NotNull] IEnumerable<T>? collection, string? message = "", [CallerArgumentExpression(nameof(collection))] string collectionExpression = "")
    {
        CheckParameterNotNull(collection, "Assert.AreAllDistinct", "collection");
        AreAllDistinctImpl(collection, EqualityComparer<T>.Default, hasUserComparer: false, message, collectionExpression);
    }

    /// <summary>
    /// Tests whether all items in the specified collection are distinct (no two
    /// elements are equal) using the supplied <paramref name="comparer"/> and throws
    /// an exception if any two elements in the collection are equal.
    /// </summary>
    /// <typeparam name="T">The type of the collection items.</typeparam>
    /// <param name="collection">
    /// The collection in which to search for duplicate elements.
    /// </param>
    /// <param name="comparer">
    /// The equality comparer to use when comparing elements.
    /// </param>
    /// <param name="message">
    /// The message to include in the exception when <paramref name="collection"/> contains
    /// at least one duplicate element. The message is shown in test results.
    /// </param>
    /// <param name="collectionExpression">
    /// The syntactic expression of collection as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// Thrown if <paramref name="collection"/> is null or contains at least one duplicate element.
    /// </exception>
    public static void AreAllDistinct<T>([NotNull] IEnumerable<T>? collection, [NotNull] IEqualityComparer<T>? comparer, string? message = "", [CallerArgumentExpression(nameof(collection))] string collectionExpression = "")
    {
        CheckParameterNotNull(collection, "Assert.AreAllDistinct", "collection");
        CheckParameterNotNull(comparer, "Assert.AreAllDistinct", "comparer");
        AreAllDistinctImpl(collection, comparer, hasUserComparer: true, message, collectionExpression);
    }

    /// <summary>
    /// Tests whether all items in the specified collection are distinct (no two
    /// elements are equal) and throws an exception if any two elements in the
    /// collection are equal.
    /// </summary>
    /// <param name="collection">
    /// The collection in which to search for duplicate elements.
    /// </param>
    /// <param name="message">
    /// The message to include in the exception when <paramref name="collection"/> contains
    /// at least one duplicate element. The message is shown in test results.
    /// </param>
    /// <param name="collectionExpression">
    /// The syntactic expression of collection as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// Thrown if <paramref name="collection"/> is null or contains at least one duplicate element.
    /// </exception>
    public static void AreAllDistinct([NotNull] IEnumerable? collection, string? message = "", [CallerArgumentExpression(nameof(collection))] string collectionExpression = "")
    {
        CheckParameterNotNull(collection, "Assert.AreAllDistinct", "collection");
        AreAllDistinctImpl(collection.Cast<object?>(), EqualityComparer<object?>.Default, hasUserComparer: false, message, collectionExpression);
    }

    /// <summary>
    /// Tests whether all items in the specified collection are distinct (no two
    /// elements are equal) using the supplied <paramref name="comparer"/> and throws
    /// an exception if any two elements in the collection are equal.
    /// </summary>
    /// <param name="collection">
    /// The collection in which to search for duplicate elements.
    /// </param>
    /// <param name="comparer">
    /// The equality comparer to use when comparing elements.
    /// </param>
    /// <param name="message">
    /// The message to include in the exception when <paramref name="collection"/> contains
    /// at least one duplicate element. The message is shown in test results.
    /// </param>
    /// <param name="collectionExpression">
    /// The syntactic expression of collection as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// Thrown if <paramref name="collection"/> is null or contains at least one duplicate element.
    /// </exception>
    public static void AreAllDistinct([NotNull] IEnumerable? collection, [NotNull] IEqualityComparer? comparer, string? message = "", [CallerArgumentExpression(nameof(collection))] string collectionExpression = "")
    {
        CheckParameterNotNull(collection, "Assert.AreAllDistinct", "collection");
        CheckParameterNotNull(comparer, "Assert.AreAllDistinct", "comparer");
        AreAllDistinctImpl(collection.Cast<object?>(), new NonGenericEqualityComparerAdapter(comparer), hasUserComparer: true, message, collectionExpression);
    }

#pragma warning disable CS8714 // The type cannot be used as type parameter in the generic type or method. Nullability of type argument doesn't match 'notnull' constraint.
    private static void AreAllDistinctImpl<T>(IEnumerable<T> collection, IEqualityComparer<T> comparer, bool hasUserComparer, string? message, string collectionExpression)
    {
        List<T> snapshot = collection is List<T> list ? list : [.. collection];

#pragma warning disable IDE0028 // Collection initialization can be simplified - target-typed new with constructor argument is preferred over collection expression here
        HashSet<T> seen = new(comparer);
#pragma warning restore IDE0028

        bool seenNull = false;
        List<T>? duplicates = null;
        HashSet<T>? duplicatesSeen = null;
        bool nullDuplicateRecorded = false;

        foreach (T item in snapshot)
        {
            if (item is null)
            {
                if (!seenNull)
                {
                    seenNull = true;
                    continue;
                }

                if (!nullDuplicateRecorded)
                {
                    duplicates ??= [];
                    duplicates.Add(default!);
                    nullDuplicateRecorded = true;
                }

                continue;
            }

            if (!seen.Add(item))
            {
#pragma warning disable IDE0028
                duplicatesSeen ??= new HashSet<T>(comparer);
#pragma warning restore IDE0028
                if (duplicatesSeen.Add(item))
                {
                    duplicates ??= [];
                    duplicates.Add(item);
                }
            }
        }

        if (duplicates is not null)
        {
            ReportAssertAreAllDistinctFailed(snapshot, duplicates, hasUserComparer, message, collectionExpression);
        }
    }
#pragma warning restore CS8714

    [DoesNotReturn]
    private static void ReportAssertAreAllDistinctFailed<T>(IEnumerable<T> collection, List<T> duplicates, bool hasUserComparer, string? message, string collectionExpression)
    {
        string collectionText = AssertionValueRenderer.RenderValue(collection);
        string duplicatesText = AssertionValueRenderer.RenderValue(duplicates);

        EvidenceBlock evidence = EvidenceBlock.Create()
            .AddLine("duplicates:", duplicatesText)
            .AddLine("collection:", collectionText);

        StructuredAssertionMessage structured = new(FrameworkMessages.AreAllDistinctFailedSummary);
        structured.WithUserMessage(message);
        structured.WithEvidence(evidence);
        structured.WithExpectedAndActual(expectedText: null, actualText: collectionText);
        structured.WithCallSiteExpression(BuildCallSiteWithComparerForCollection("Assert.AreAllDistinct", collectionExpression, hasUserComparer));

        ReportAssertFailed(structured);
    }

    private static string? BuildCallSiteWithComparerForCollection(string assertionMethodName, string collectionExpression, bool hasComparer)
    {
        string? callSite = FormatCallSiteExpression(assertionMethodName, collectionExpression, "<collection>");
        if (callSite is null || !hasComparer)
        {
            return callSite;
        }

        // FormatCallSiteExpression has no overload accepting a third argument expression; insert
        // the <comparer> placeholder so the rendered call-site reflects the overload that was actually invoked.
        // Note: range/index syntax (callSite[..^1]) is not used because System.Range/System.Index are unavailable
        // on net462 / netstandard2.0, the lowest TFMs targeted by this project.
        Debug.Assert(callSite.Length > 0 && callSite[callSite.Length - 1] == ')', "FormatCallSiteExpression contract: rendered call-site must end with ')'.");
        return string.Concat(callSite.Substring(0, callSite.Length - 1), ", <comparer>)");
    }

    #endregion // AreAllDistinct

    // TODO: Deduplicate with the same adapter in Assert.CollectionEquivalence.cs (introduced by PR #8234)
    // once both PRs have landed.
    private sealed class NonGenericEqualityComparerAdapter : IEqualityComparer<object?>
    {
        private readonly IEqualityComparer _comparer;

        public NonGenericEqualityComparerAdapter(IEqualityComparer comparer)
            => _comparer = comparer;

        public new bool Equals(object? x, object? y) => _comparer.Equals(x, y);

        public int GetHashCode(object? obj) => obj is null ? 0 : _comparer.GetHashCode(obj);
    }
}
