// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// A collection of helper classes to test various conditions associated
/// with collections within unit tests. If the condition being tested is not
/// met, an exception is thrown.
/// </summary>
public sealed partial class CollectionAssert
{
    #region Membership

    /// <summary>
    /// Tests whether the specified collection contains the specified element
    /// and throws an exception if the element is not in the collection.
    /// </summary>
    /// <param name="collection">
    /// The collection in which to search for the element.
    /// </param>
    /// <param name="element">
    /// The element that is expected to be in the collection.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// <paramref name="collection"/> is null, or <paramref name="collection"/> does not contain
    /// element <paramref name="element"/>.
    /// </exception>
    public static void Contains([NotNull] ICollection? collection, object? element)
        => Contains(collection, element, string.Empty);

    /// <summary>
    /// Tests whether the specified collection contains the specified element
    /// and throws an exception if the element is not in the collection.
    /// </summary>
    /// <param name="collection">
    /// The collection in which to search for the element.
    /// </param>
    /// <param name="element">
    /// The element that is expected to be in the collection.
    /// </param>
    /// <param name="message">
    /// The message to include in the exception when <paramref name="element"/>
    /// is not in <paramref name="collection"/>. The message is shown in
    /// test results.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// <paramref name="collection"/> is null, or <paramref name="collection"/> does not contain
    /// element <paramref name="element"/>.
    /// </exception>
    public static void Contains([NotNull] ICollection? collection, object? element, string? message)
    {
        TelemetryCollector.TrackAssertionCall("CollectionAssert.Contains");

        Assert.CheckParameterNotNull(collection, "CollectionAssert.Contains", "collection");

        foreach (object? current in collection)
        {
            if (object.Equals(current, element))
            {
                return;
            }
        }

        ReportContainsFailed(collection, element, Assert.BuildUserMessage(message));
    }

    [DoesNotReturn]
    private static void ReportContainsFailed(ICollection collection, object? element, string? userMessage)
    {
        string expectedText = AssertionValueRenderer.RenderValue(element);
        string collectionText = AssertionValueRenderer.RenderValue(collection);
        EvidenceBlock evidence = EvidenceBlock.Create()
            .AddLine("expected:", expectedText)
            .AddLine("collection:", collectionText);

        StructuredAssertionMessage structured = new(FrameworkMessages.ContainsItemFailedSummary);
        structured.WithUserMessage(userMessage);
        structured.WithEvidence(evidence);
        structured.WithExpectedAndActual(expectedText, collectionText);

        Assert.ReportAssertFailed(structured);
    }

    /// <summary>
    /// Tests whether the specified collection does not contain the specified
    /// element and throws an exception if the element is in the collection.
    /// </summary>
    /// <param name="collection">
    /// The collection in which to search for the element.
    /// </param>
    /// <param name="element">
    /// The element that is expected not to be in the collection.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// <paramref name="collection"/> is null, or <paramref name="collection"/> contains
    /// element <paramref name="element"/>.
    /// </exception>
    public static void DoesNotContain([NotNull] ICollection? collection, object? element)
        => DoesNotContain(collection, element, string.Empty);

    /// <summary>
    /// Tests whether the specified collection does not contain the specified
    /// element and throws an exception if the element is in the collection.
    /// </summary>
    /// <param name="collection">
    /// The collection in which to search for the element.
    /// </param>
    /// <param name="element">
    /// The element that is expected not to be in the collection.
    /// </param>
    /// <param name="message">
    /// The message to include in the exception when <paramref name="element"/>
    /// is in <paramref name="collection"/>. The message is shown in test
    /// results.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// <paramref name="collection"/> is null, or <paramref name="collection"/> contains
    /// element <paramref name="element"/>.
    /// </exception>
    public static void DoesNotContain([NotNull] ICollection? collection, object? element, string? message)
    {
        TelemetryCollector.TrackAssertionCall("CollectionAssert.DoesNotContain");

        Assert.CheckParameterNotNull(collection, "CollectionAssert.DoesNotContain", "collection");

        foreach (object? current in collection)
        {
            if (object.Equals(current, element))
            {
                ReportDoesNotContainFailed(collection, element, Assert.BuildUserMessage(message));
            }
        }
    }

    [DoesNotReturn]
    private static void ReportDoesNotContainFailed(ICollection collection, object? element, string? userMessage)
    {
        string notExpectedText = AssertionValueRenderer.RenderValue(element);
        string collectionText = AssertionValueRenderer.RenderValue(collection);
        EvidenceBlock evidence = EvidenceBlock.Create()
            .AddLine("unexpected:", notExpectedText)
            .AddLine("collection:", collectionText);

        StructuredAssertionMessage structured = new(FrameworkMessages.DoesNotContainItemFailedSummary);
        structured.WithUserMessage(userMessage);
        structured.WithEvidence(evidence);
        structured.WithExpectedAndActual(notExpectedText, collectionText);

        Assert.ReportAssertFailed(structured);
    }

    /// <summary>
    /// Tests whether all items in the specified collection are non-null and throws
    /// an exception if any element is null.
    /// </summary>
    /// <param name="collection">
    /// The collection in which to search for null elements.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// <paramref name="collection"/> is null, or <paramref name="collection"/> contains a null element.
    /// </exception>
    public static void AllItemsAreNotNull([NotNull] ICollection? collection)
        => AllItemsAreNotNull(collection, string.Empty);

    /// <summary>
    /// Tests whether all items in the specified collection are non-null and throws
    /// an exception if any element is null.
    /// </summary>
    /// <param name="collection">
    /// The collection in which to search for null elements.
    /// </param>
    /// <param name="message">
    /// The message to include in the exception when <paramref name="collection"/>
    /// contains a null element. The message is shown in test results.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// <paramref name="collection"/> is null, or <paramref name="collection"/> contains a null element.
    /// </exception>
    public static void AllItemsAreNotNull([NotNull] ICollection? collection, string? message)
    {
        TelemetryCollector.TrackAssertionCall("CollectionAssert.AllItemsAreNotNull");

        Assert.CheckParameterNotNull(collection, "CollectionAssert.AllItemsAreNotNull", "collection");
        foreach (object? current in collection)
        {
            if (current == null)
            {
                ReportAllItemsAreNotNullFailed(collection, Assert.BuildUserMessage(message));
            }
        }
    }

    [DoesNotReturn]
    private static void ReportAllItemsAreNotNullFailed(ICollection collection, string? userMessage)
    {
        string collectionText = AssertionValueRenderer.RenderValue(collection);
        EvidenceBlock evidence = EvidenceBlock.Create()
            .AddLine("collection:", collectionText);

        StructuredAssertionMessage structured = new(FrameworkMessages.AreAllNotNullFailedSummary);
        structured.WithUserMessage(userMessage);
        structured.WithEvidence(evidence);
        structured.WithExpectedAndActual(expectedText: null, actualText: collectionText);

        Assert.ReportAssertFailed(structured);
    }

    /// <summary>
    /// Tests whether all items in the specified collection are unique or not and
    /// throws if any two elements in the collection are equal.
    /// </summary>
    /// <param name="collection">
    /// The collection in which to search for duplicate elements.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// <paramref name="collection"/> is null, or <paramref name="collection"/> contains at least one duplicate
    /// element.
    /// </exception>
    public static void AllItemsAreUnique([NotNull] ICollection? collection)
        => AllItemsAreUnique(collection, string.Empty);

    /// <summary>
    /// Tests whether all items in the specified collection are unique or not and
    /// throws if any two elements in the collection are equal.
    /// </summary>
    /// <param name="collection">
    /// The collection in which to search for duplicate elements.
    /// </param>
    /// <param name="message">
    /// The message to include in the exception when <paramref name="collection"/>
    /// contains at least one duplicate element. The message is shown in
    /// test results.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// <paramref name="collection"/> is null, or <paramref name="collection"/> contains at least one duplicate
    /// element.
    /// </exception>
    public static void AllItemsAreUnique([NotNull] ICollection? collection, string? message)
    {
        TelemetryCollector.TrackAssertionCall("CollectionAssert.AllItemsAreUnique");

        Assert.CheckParameterNotNull(collection, "CollectionAssert.AllItemsAreUnique", "collection");

        bool foundNull = false;
        HashSet<object> table = [];
        foreach (object? current in collection)
        {
            if (current == null)
            {
                if (!foundNull)
                {
                    foundNull = true;
                }
                else
                {
                    // Found a second occurrence of null.
                    ReportAllItemsAreUniqueFailed(collection, duplicate: null, Assert.BuildUserMessage(message));
                }
            }
            else
            {
                if (!table.Add(current))
                {
                    ReportAllItemsAreUniqueFailed(collection, current, Assert.BuildUserMessage(message));
                }
            }
        }
    }

    [DoesNotReturn]
    private static void ReportAllItemsAreUniqueFailed(ICollection collection, object? duplicate, string? userMessage)
    {
        string duplicateText = AssertionValueRenderer.RenderValue(duplicate);
        string collectionText = AssertionValueRenderer.RenderValue(collection);
        EvidenceBlock evidence = EvidenceBlock.Create()
            .AddLine("duplicate:", duplicateText)
            .AddLine("collection:", collectionText);

        StructuredAssertionMessage structured = new(FrameworkMessages.AreAllDistinctFailedSummary);
        structured.WithUserMessage(userMessage);
        structured.WithEvidence(evidence);
        structured.WithExpectedAndActual(expectedText: null, actualText: collectionText);

        Assert.ReportAssertFailed(structured);
    }

    #endregion
}
