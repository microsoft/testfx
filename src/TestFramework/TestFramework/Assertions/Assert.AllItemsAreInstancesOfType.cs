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
    #region AllItemsAreInstancesOfType

    /// <summary>
    /// Tests whether all elements in the specified collection are instances of the
    /// expected type and throws an exception if the expected type is not in the
    /// inheritance hierarchy of one or more of the elements.
    /// </summary>
    /// <param name="collection">
    /// The collection containing elements the test expects to be of the specified type.
    /// </param>
    /// <param name="expectedType">
    /// The expected type of each element of <paramref name="collection"/>.
    /// </param>
    /// <param name="message">
    /// The message to include in the exception when an element in <paramref name="collection"/>
    /// is not an instance of <paramref name="expectedType"/>. The message is shown in
    /// test results.
    /// </param>
    /// <param name="collectionExpression">
    /// The syntactic expression of collection as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    /// <param name="expectedTypeExpression">
    /// The syntactic expression of expectedType as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// Thrown if <paramref name="collection"/> or <paramref name="expectedType"/> is null,
    /// or some elements of <paramref name="collection"/> do not inherit from / implement
    /// <paramref name="expectedType"/>.
    /// </exception>
    public static void AllItemsAreInstancesOfType([NotNull] IEnumerable? collection, [NotNull] Type? expectedType, string? message = "", [CallerArgumentExpression(nameof(collection))] string collectionExpression = "", [CallerArgumentExpression(nameof(expectedType))] string expectedTypeExpression = "")
    {
        CheckParameterNotNull(collection, "Assert.AllItemsAreInstancesOfType", "collection");
        CheckParameterNotNull(expectedType, "Assert.AllItemsAreInstancesOfType", "expectedType");
        AllItemsAreInstancesOfTypeImpl(collection, expectedType, genericTypeArgumentName: null, message, collectionExpression, expectedTypeExpression);
    }

    /// <summary>
    /// Tests whether all elements in the specified collection are instances of the
    /// expected type and throws an exception if the expected type is not in the
    /// inheritance hierarchy of one or more of the elements.
    /// </summary>
    /// <typeparam name="TExpected">The type each element of <paramref name="collection"/> is expected to be.</typeparam>
    /// <param name="collection">
    /// The collection containing elements the test expects to be of the specified type.
    /// </param>
    /// <param name="message">
    /// The message to include in the exception when an element in <paramref name="collection"/>
    /// is not an instance of <typeparamref name="TExpected"/>. The message is shown in
    /// test results.
    /// </param>
    /// <param name="collectionExpression">
    /// The syntactic expression of collection as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// Thrown if <paramref name="collection"/> is null or some elements of
    /// <paramref name="collection"/> do not inherit from / implement <typeparamref name="TExpected"/>.
    /// </exception>
    public static void AllItemsAreInstancesOfType<TExpected>([NotNull] IEnumerable? collection, string? message = "", [CallerArgumentExpression(nameof(collection))] string collectionExpression = "")
    {
        CheckParameterNotNull(collection, "Assert.AllItemsAreInstancesOfType", "collection");
        AllItemsAreInstancesOfTypeImpl(collection, typeof(TExpected), genericTypeArgumentName: "TExpected", message, collectionExpression, expectedTypeExpression: null);
    }

    private static void AllItemsAreInstancesOfTypeImpl(IEnumerable collection, Type expectedType, string? genericTypeArgumentName, string? message, string collectionExpression, string? expectedTypeExpression)
    {
        List<object?> snapshot = [.. collection.Cast<object?>()];
        List<int>? mismatchIndices = null;
        List<Type>? mismatchTypes = null;
        for (int i = 0; i < snapshot.Count; i++)
        {
            object? element = snapshot[i];
            if (element is not null && !expectedType.IsInstanceOfType(element))
            {
                mismatchIndices ??= [];
                mismatchTypes ??= [];
                mismatchIndices.Add(i);
                mismatchTypes.Add(element.GetType());
            }
        }

        if (mismatchIndices is not null)
        {
            ReportAssertAllItemsAreInstancesOfTypeFailed(snapshot, expectedType, mismatchIndices, mismatchTypes!, genericTypeArgumentName, message, collectionExpression, expectedTypeExpression);
        }
    }

    [DoesNotReturn]
    private static void ReportAssertAllItemsAreInstancesOfTypeFailed(IEnumerable<object?> collection, Type expectedType, List<int> mismatchIndices, List<Type> mismatchTypes, string? genericTypeArgumentName, string? message, string collectionExpression, string? expectedTypeExpression)
    {
        string collectionText = AssertionValueRenderer.RenderValue(collection);
        string expectedTypeText = $"{expectedType} (or derived)";

        StringBuilder mismatchesBuilder = new();
        mismatchesBuilder.Append('[');
        for (int i = 0; i < mismatchIndices.Count; i++)
        {
            if (i > 0)
            {
                mismatchesBuilder.Append(", ");
            }

            mismatchesBuilder.Append("index ").Append(mismatchIndices[i]).Append(": ").Append(mismatchTypes[i]);
        }

        mismatchesBuilder.Append(']');

        EvidenceBlock evidence = EvidenceBlock.Create()
            .AddLine("expected type:", expectedTypeText)
            .AddLine("mismatches:", mismatchesBuilder.ToString())
            .AddLine("collection:", collectionText);

        string assertionMethodName = genericTypeArgumentName is null
            ? "Assert.AllItemsAreInstancesOfType"
            : $"Assert.AllItemsAreInstancesOfType<{genericTypeArgumentName}>";

        string? callSite = genericTypeArgumentName is null
            ? FormatCallSiteExpression(assertionMethodName, collectionExpression, expectedTypeExpression!, "<collection>", "<expectedType>")
            : FormatCallSiteExpression(assertionMethodName, collectionExpression, "<collection>");

        StructuredAssertionMessage structured = new(FrameworkMessages.AllItemsAreInstancesOfTypeFailedSummary);
        structured.WithUserMessage(message);
        structured.WithEvidence(evidence);
        structured.WithExpectedAndActual(expectedText: null, actualText: collectionText);
        structured.WithCallSiteExpression(callSite);

        ReportAssertFailed(structured);
    }

    #endregion // AllItemsAreInstancesOfType
}
