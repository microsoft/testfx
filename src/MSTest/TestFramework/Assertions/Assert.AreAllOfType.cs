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
    #region AreAllOfType

    /// <summary>
    /// Tests whether all elements in the specified collection are instances of (or
    /// derived from) the expected type and throws an exception if the expected type is
    /// not in the inheritance hierarchy of one or more of the elements, or if any
    /// element is null.
    /// </summary>
    /// <param name="expectedType">
    /// The expected type of each element of <paramref name="collection"/>.
    /// </param>
    /// <param name="collection">
    /// The collection containing elements the test expects to be of the specified type.
    /// </param>
    /// <param name="message">
    /// The message to include in the exception when an element in <paramref name="collection"/>
    /// is null or not an instance of <paramref name="expectedType"/>. The message is shown in
    /// test results.
    /// </param>
    /// <param name="expectedTypeExpression">
    /// The syntactic expression of expectedType as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    /// <param name="collectionExpression">
    /// The syntactic expression of collection as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// Thrown if <paramref name="expectedType"/> or <paramref name="collection"/> is null,
    /// or some elements of <paramref name="collection"/> are null or do not inherit from /
    /// implement <paramref name="expectedType"/>.
    /// </exception>
    public static void AreAllOfType([NotNull] Type? expectedType, [NotNull] IEnumerable? collection, string? message = "", [CallerArgumentExpression(nameof(expectedType))] string expectedTypeExpression = "", [CallerArgumentExpression(nameof(collection))] string collectionExpression = "")
    {
        TelemetryCollector.TrackAssertionCall("Assert.AreAllOfType");
        CheckParameterNotNull(expectedType, "Assert.AreAllOfType", "expectedType");
        CheckParameterNotNull(collection, "Assert.AreAllOfType", "collection");
        AreAllOfTypeImpl(collection, expectedType, genericTypeArgumentName: null, message, collectionExpression, expectedTypeExpression);
    }

    /// <summary>
    /// Tests whether all elements in the specified collection are instances of (or
    /// derived from) the expected type and throws an exception if the expected type is
    /// not in the inheritance hierarchy of one or more of the elements, or if any
    /// element is null.
    /// </summary>
    /// <typeparam name="TExpected">The type each element of <paramref name="collection"/> is expected to be.</typeparam>
    /// <param name="collection">
    /// The collection containing elements the test expects to be of the specified type.
    /// </param>
    /// <param name="message">
    /// The message to include in the exception when an element in <paramref name="collection"/>
    /// is null or not an instance of <typeparamref name="TExpected"/>. The message is shown in
    /// test results.
    /// </param>
    /// <param name="collectionExpression">
    /// The syntactic expression of collection as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// Thrown if <paramref name="collection"/> is null or some elements of
    /// <paramref name="collection"/> are null or do not inherit from / implement
    /// <typeparamref name="TExpected"/>.
    /// </exception>
    public static void AreAllOfType<TExpected>([NotNull] IEnumerable? collection, string? message = "", [CallerArgumentExpression(nameof(collection))] string collectionExpression = "")
    {
        TelemetryCollector.TrackAssertionCall("Assert.AreAllOfType");
        CheckParameterNotNull(collection, "Assert.AreAllOfType", "collection");
        AreAllOfTypeImpl(collection, typeof(TExpected), genericTypeArgumentName: "TExpected", message, collectionExpression, expectedTypeExpression: null);
    }

    private static void AreAllOfTypeImpl(IEnumerable collection, Type expectedType, string? genericTypeArgumentName, string? message, string collectionExpression, string? expectedTypeExpression)
    {
        List<object?> snapshot = [.. collection.Cast<object?>()];
        List<TypeMismatch>? mismatches = null;
        for (int i = 0; i < snapshot.Count; i++)
        {
            object? element = snapshot[i];
            if (element is null)
            {
                mismatches ??= [];
                mismatches.Add(new TypeMismatch(i, actualType: null));
            }
            else if (!expectedType.IsInstanceOfType(element))
            {
                mismatches ??= [];
                mismatches.Add(new TypeMismatch(i, element.GetType()));
            }
        }

        if (mismatches is not null)
        {
            ReportAssertAreAllOfTypeFailed(snapshot, expectedType, mismatches, genericTypeArgumentName, message, collectionExpression, expectedTypeExpression);
        }
    }

    [DoesNotReturn]
    private static void ReportAssertAreAllOfTypeFailed(IEnumerable<object?> collection, Type expectedType, List<TypeMismatch> mismatches, string? genericTypeArgumentName, string? message, string collectionExpression, string? expectedTypeExpression)
    {
        string collectionText = AssertionValueRenderer.RenderValue(collection);
        string expectedTypeText = $"{expectedType} (or derived)";

        StringBuilder mismatchesBuilder = new();
        mismatchesBuilder.Append('[');
        for (int i = 0; i < mismatches.Count; i++)
        {
            if (i > 0)
            {
                mismatchesBuilder.Append(", ");
            }

            TypeMismatch mismatch = mismatches[i];
            mismatchesBuilder.Append("index ").Append(mismatch.Index).Append(": ");
            if (mismatch.ActualType is null)
            {
                mismatchesBuilder.Append("<null>");
            }
            else
            {
                mismatchesBuilder.Append(mismatch.ActualType);
            }
        }

        mismatchesBuilder.Append(']');

        EvidenceBlock evidence = EvidenceBlock.Create()
            .AddLine("expected type:", expectedTypeText)
            .AddLine("mismatches:", mismatchesBuilder.ToString())
            .AddLine("collection:", collectionText);

        string assertionMethodName = genericTypeArgumentName is null
            ? "Assert.AreAllOfType"
            : $"Assert.AreAllOfType<{genericTypeArgumentName}>";

        string? callSite = genericTypeArgumentName is null
            ? FormatCallSiteExpression(assertionMethodName, expectedTypeExpression!, collectionExpression, "<expectedType>", "<collection>")
            : FormatCallSiteExpression(assertionMethodName, collectionExpression, "<collection>");

        StructuredAssertionMessage structured = new(FrameworkMessages.AreAllOfTypeFailedSummary);
        structured.WithUserMessage(message);
        structured.WithEvidence(evidence);
        structured.WithExpectedAndActual(expectedTypeText, collectionText);
        structured.WithCallSiteExpression(callSite);

        ReportAssertFailed(structured);
    }

    [StackTraceHidden]
    private readonly struct TypeMismatch
    {
        public TypeMismatch(int index, Type? actualType)
        {
            Index = index;
            ActualType = actualType;
        }

        public int Index { get; }

        public Type? ActualType { get; }
    }

    #endregion // AreAllOfType

#if NETCOREAPP3_1_OR_GREATER

    #region AreAllOfType span/memory

    /// <summary>
    /// Tests whether all elements in the specified span are instances of (or derived from) the expected type.
    /// </summary>
    /// <typeparam name="TItem">The element type of the span.</typeparam>
    /// <param name="expectedType">The expected type of each element of <paramref name="collection"/>.</param>
    /// <param name="collection">The span containing elements the test expects to be of the specified type.</param>
    /// <param name="message">The message to include in the exception when the assertion fails.</param>
    /// <param name="expectedTypeExpression">
    /// The syntactic expression of expectedType as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    /// <param name="collectionExpression">
    /// The syntactic expression of collection as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    public static void AreAllOfType<TItem>([NotNull] Type? expectedType, ReadOnlySpan<TItem> collection, string? message = "", [CallerArgumentExpression(nameof(expectedType))] string expectedTypeExpression = "", [CallerArgumentExpression(nameof(collection))] string collectionExpression = "")
    {
        TelemetryCollector.TrackAssertionCall("Assert.AreAllOfType");
        CheckParameterNotNull(expectedType, "Assert.AreAllOfType", "expectedType");
        AreAllOfTypeImpl(collection.ToArray(), expectedType, genericTypeArgumentName: null, message, collectionExpression, expectedTypeExpression);
    }

    /// <summary>
    /// Tests whether all elements in the specified span are instances of (or derived from) the expected type.
    /// </summary>
    /// <typeparam name="TItem">The element type of the span.</typeparam>
    /// <param name="expectedType">The expected type of each element of <paramref name="collection"/>.</param>
    /// <param name="collection">The span containing elements the test expects to be of the specified type.</param>
    /// <param name="message">The message to include in the exception when the assertion fails.</param>
    /// <param name="expectedTypeExpression">
    /// The syntactic expression of expectedType as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    /// <param name="collectionExpression">
    /// The syntactic expression of collection as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    public static void AreAllOfType<TItem>([NotNull] Type? expectedType, Span<TItem> collection, string? message = "", [CallerArgumentExpression(nameof(expectedType))] string expectedTypeExpression = "", [CallerArgumentExpression(nameof(collection))] string collectionExpression = "")
        => AreAllOfType(expectedType, (ReadOnlySpan<TItem>)collection, message, expectedTypeExpression, collectionExpression);

    /// <summary>
    /// Tests whether all elements in the specified memory are instances of (or derived from) the expected type.
    /// </summary>
    /// <typeparam name="TItem">The element type of the memory.</typeparam>
    /// <param name="expectedType">The expected type of each element of <paramref name="collection"/>.</param>
    /// <param name="collection">The memory containing elements the test expects to be of the specified type.</param>
    /// <param name="message">The message to include in the exception when the assertion fails.</param>
    /// <param name="expectedTypeExpression">
    /// The syntactic expression of expectedType as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    /// <param name="collectionExpression">
    /// The syntactic expression of collection as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    public static void AreAllOfType<TItem>([NotNull] Type? expectedType, ReadOnlyMemory<TItem> collection, string? message = "", [CallerArgumentExpression(nameof(expectedType))] string expectedTypeExpression = "", [CallerArgumentExpression(nameof(collection))] string collectionExpression = "")
        => AreAllOfType(expectedType, collection.Span, message, expectedTypeExpression, collectionExpression);

    /// <summary>
    /// Tests whether all elements in the specified memory are instances of (or derived from) the expected type.
    /// </summary>
    /// <typeparam name="TItem">The element type of the memory.</typeparam>
    /// <param name="expectedType">The expected type of each element of <paramref name="collection"/>.</param>
    /// <param name="collection">The memory containing elements the test expects to be of the specified type.</param>
    /// <param name="message">The message to include in the exception when the assertion fails.</param>
    /// <param name="expectedTypeExpression">
    /// The syntactic expression of expectedType as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    /// <param name="collectionExpression">
    /// The syntactic expression of collection as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    public static void AreAllOfType<TItem>([NotNull] Type? expectedType, Memory<TItem> collection, string? message = "", [CallerArgumentExpression(nameof(expectedType))] string expectedTypeExpression = "", [CallerArgumentExpression(nameof(collection))] string collectionExpression = "")
        => AreAllOfType(expectedType, collection.Span, message, expectedTypeExpression, collectionExpression);

    /// <summary>
    /// Tests whether all elements in the specified span are instances of (or derived from) <typeparamref name="TExpected"/>.
    /// </summary>
    /// <typeparam name="TExpected">The type each element of <paramref name="collection"/> is expected to be.</typeparam>
    /// <typeparam name="TItem">The element type of the span.</typeparam>
    /// <param name="collection">The span containing elements the test expects to be of the specified type.</param>
    /// <param name="message">The message to include in the exception when the assertion fails.</param>
    /// <param name="collectionExpression">
    /// The syntactic expression of collection as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    public static void AreAllOfType<TExpected, TItem>(ReadOnlySpan<TItem> collection, string? message = "", [CallerArgumentExpression(nameof(collection))] string collectionExpression = "")
    {
        TelemetryCollector.TrackAssertionCall("Assert.AreAllOfType");
        AreAllOfTypeImpl(collection.ToArray(), typeof(TExpected), genericTypeArgumentName: "TExpected", message, collectionExpression, expectedTypeExpression: null);
    }

    /// <summary>
    /// Tests whether all elements in the specified span are instances of (or derived from) <typeparamref name="TExpected"/>.
    /// </summary>
    /// <typeparam name="TExpected">The type each element of <paramref name="collection"/> is expected to be.</typeparam>
    /// <typeparam name="TItem">The element type of the span.</typeparam>
    /// <param name="collection">The span containing elements the test expects to be of the specified type.</param>
    /// <param name="message">The message to include in the exception when the assertion fails.</param>
    /// <param name="collectionExpression">
    /// The syntactic expression of collection as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    public static void AreAllOfType<TExpected, TItem>(Span<TItem> collection, string? message = "", [CallerArgumentExpression(nameof(collection))] string collectionExpression = "")
        => AreAllOfType<TExpected, TItem>((ReadOnlySpan<TItem>)collection, message, collectionExpression);

    /// <summary>
    /// Tests whether all elements in the specified memory are instances of (or derived from) <typeparamref name="TExpected"/>.
    /// </summary>
    /// <typeparam name="TExpected">The type each element of <paramref name="collection"/> is expected to be.</typeparam>
    /// <typeparam name="TItem">The element type of the memory.</typeparam>
    /// <param name="collection">The memory containing elements the test expects to be of the specified type.</param>
    /// <param name="message">The message to include in the exception when the assertion fails.</param>
    /// <param name="collectionExpression">
    /// The syntactic expression of collection as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    public static void AreAllOfType<TExpected, TItem>(ReadOnlyMemory<TItem> collection, string? message = "", [CallerArgumentExpression(nameof(collection))] string collectionExpression = "")
        => AreAllOfType<TExpected, TItem>(collection.Span, message, collectionExpression);

    /// <summary>
    /// Tests whether all elements in the specified memory are instances of (or derived from) <typeparamref name="TExpected"/>.
    /// </summary>
    /// <typeparam name="TExpected">The type each element of <paramref name="collection"/> is expected to be.</typeparam>
    /// <typeparam name="TItem">The element type of the memory.</typeparam>
    /// <param name="collection">The memory containing elements the test expects to be of the specified type.</param>
    /// <param name="message">The message to include in the exception when the assertion fails.</param>
    /// <param name="collectionExpression">
    /// The syntactic expression of collection as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    public static void AreAllOfType<TExpected, TItem>(Memory<TItem> collection, string? message = "", [CallerArgumentExpression(nameof(collection))] string collectionExpression = "")
        => AreAllOfType<TExpected, TItem>(collection.Span, message, collectionExpression);

    #endregion // AreAllOfType span/memory

#endif

}
