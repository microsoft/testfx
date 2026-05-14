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
    #region AllItemsAreNotNull

    /// <summary>
    /// Tests whether all items in the specified collection are non-null and throws
    /// an exception if any element is null.
    /// </summary>
    /// <param name="collection">
    /// The collection in which to search for null elements.
    /// </param>
    /// <param name="message">
    /// The message to include in the exception when <paramref name="collection"/> contains
    /// a null element. The message is shown in test results.
    /// </param>
    /// <param name="collectionExpression">
    /// The syntactic expression of collection as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// Thrown if <paramref name="collection"/> is null or contains at least one null element.
    /// </exception>
    public static void AllItemsAreNotNull([NotNull] IEnumerable? collection, string? message = "", [CallerArgumentExpression(nameof(collection))] string collectionExpression = "")
    {
        CheckParameterNotNull(collection, "Assert.AllItemsAreNotNull", "collection");
        AllItemsAreNotNullImpl(collection.Cast<object?>(), message, collectionExpression);
    }

    /// <summary>
    /// Tests whether all items in the specified collection are non-null and throws
    /// an exception if any element is null.
    /// </summary>
    /// <typeparam name="T">The type of the collection items.</typeparam>
    /// <param name="collection">
    /// The collection in which to search for null elements.
    /// </param>
    /// <param name="message">
    /// The message to include in the exception when <paramref name="collection"/> contains
    /// a null element. The message is shown in test results.
    /// </param>
    /// <param name="collectionExpression">
    /// The syntactic expression of collection as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// Thrown if <paramref name="collection"/> is null or contains at least one null element.
    /// </exception>
    public static void AllItemsAreNotNull<T>([NotNull] IEnumerable<T>? collection, string? message = "", [CallerArgumentExpression(nameof(collection))] string collectionExpression = "")
    {
        CheckParameterNotNull(collection, "Assert.AllItemsAreNotNull", "collection");
        AllItemsAreNotNullImpl(collection, message, collectionExpression);
    }

    private static void AllItemsAreNotNullImpl<T>(IEnumerable<T> collection, string? message, string collectionExpression)
    {
        List<T> snapshot = collection is List<T> list ? list : [.. collection];
        List<int>? nullIndices = null;
        for (int i = 0; i < snapshot.Count; i++)
        {
            if (snapshot[i] is null)
            {
                nullIndices ??= [];
                nullIndices.Add(i);
            }
        }

        if (nullIndices is not null)
        {
            ReportAssertAllItemsAreNotNullFailed(snapshot, nullIndices, message, collectionExpression);
        }
    }

    [DoesNotReturn]
    private static void ReportAssertAllItemsAreNotNullFailed<T>(IEnumerable<T> collection, List<int> nullIndices, string? message, string collectionExpression)
    {
        string collectionText = AssertionValueRenderer.RenderValue(collection);
        string nullIndicesText = AssertionValueRenderer.RenderValue(nullIndices);

        EvidenceBlock evidence = EvidenceBlock.Create()
            .AddLine("null indices:", nullIndicesText)
            .AddLine("collection:", collectionText);

        StructuredAssertionMessage structured = new(FrameworkMessages.AllItemsAreNotNullFailedSummary);
        structured.WithUserMessage(message);
        structured.WithEvidence(evidence);
        structured.WithExpectedAndActual(expectedText: null, actualText: collectionText);
        structured.WithCallSiteExpression(FormatCallSiteExpression("Assert.AllItemsAreNotNull", collectionExpression, "<collection>"));

        ReportAssertFailed(structured);
    }

    #endregion // AllItemsAreNotNull
}
