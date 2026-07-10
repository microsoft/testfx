// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// A collection of helper classes to test various conditions within
/// unit tests. If the condition being tested is not met, an exception
/// is thrown.
/// </summary>
public sealed partial class Assert
{
#pragma warning disable RS0026 // Do not add multiple public overloads with optional parameters
#pragma warning disable RS0027 // API with optional parameter(s) should have the most parameters amongst its public overloads

    /// <summary>
    /// Tests whether the collection has the expected count/length.
    /// </summary>
    /// <typeparam name="T">The type of the collection items.</typeparam>
    /// <param name="expected">The expected count.</param>
    /// <param name="collection">The collection.</param>
    /// <param name="message">The message to display when the assertion fails.</param>
    /// <param name="collectionExpression">
    /// The syntactic expression of collection as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
#pragma warning disable IDE0060 // Remove unused parameter
    public static void HasCount<T>(int expected, IEnumerable<T> collection, [InterpolatedStringHandlerArgument(nameof(expected), nameof(collection))] ref AssertCountInterpolatedStringHandler<T> message, [CallerArgumentExpression(nameof(collection))] string collectionExpression = "")
#pragma warning restore IDE0060 // Remove unused parameter
    {
        TelemetryCollector.TrackAssertionCall("Assert.HasCount");
        message.ComputeAssertion(nameof(HasCount), collectionExpression);
    }

    /// <summary>
    /// Tests whether the collection has the expected count/length.
    /// </summary>
    /// <typeparam name="T">The type of the collection items.</typeparam>
    /// <param name="expected">The expected count.</param>
    /// <param name="collection">The collection.</param>
    /// <param name="message">The message to display when the assertion fails.</param>
    /// <param name="collectionExpression">
    /// The syntactic expression of collection as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    public static void HasCount<T>(int expected, IEnumerable<T> collection, string? message = "", [CallerArgumentExpression(nameof(collection))] string collectionExpression = "")
        => HasCount(nameof(HasCount), expected, collection, message, collectionExpression);

    /// <summary>
    /// Tests whether the collection has the expected count/length.
    /// </summary>
    /// <param name="expected">The expected count.</param>
    /// <param name="collection">The collection.</param>
    /// <param name="message">The message to display when the assertion fails.</param>
    /// <param name="collectionExpression">
    /// The syntactic expression of collection as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    public static void HasCount(int expected, IEnumerable collection, string? message = "", [CallerArgumentExpression(nameof(collection))] string collectionExpression = "")
        => HasCount(nameof(HasCount), expected, collection, message, collectionExpression);

#if NETCOREAPP3_1_OR_GREATER

    /// <summary>
    /// Tests whether the span has the expected count/length.
    /// </summary>
    /// <typeparam name="T">The type of the span items.</typeparam>
    /// <param name="expected">The expected count.</param>
    /// <param name="collection">The span.</param>
    /// <param name="message">The message to display when the assertion fails.</param>
    /// <param name="collectionExpression">
    /// The syntactic expression of collection as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
#pragma warning disable IDE0060 // Remove unused parameter
    public static void HasCount<T>(int expected, ReadOnlySpan<T> collection, [InterpolatedStringHandlerArgument(nameof(expected), nameof(collection))] ref AssertCountInterpolatedStringHandler<T> message, [CallerArgumentExpression(nameof(collection))] string collectionExpression = "")
#pragma warning restore IDE0060 // Remove unused parameter
    {
        TelemetryCollector.TrackAssertionCall("Assert.HasCount");
        message.ComputeAssertion(nameof(HasCount), collectionExpression);
    }

    /// <summary>
    /// Tests whether the span has the expected count/length.
    /// </summary>
    /// <typeparam name="T">The type of the span items.</typeparam>
    /// <param name="expected">The expected count.</param>
    /// <param name="collection">The span.</param>
    /// <param name="message">The message to display when the assertion fails.</param>
    /// <param name="collectionExpression">
    /// The syntactic expression of collection as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    public static void HasCount<T>(int expected, ReadOnlySpan<T> collection, string? message = "", [CallerArgumentExpression(nameof(collection))] string collectionExpression = "")
        => HasCount(nameof(HasCount), expected, collection, message, collectionExpression);

    /// <summary>
    /// Tests whether the span has the expected count/length.
    /// </summary>
    /// <typeparam name="T">The type of the span items.</typeparam>
    /// <param name="expected">The expected count.</param>
    /// <param name="collection">The span.</param>
    /// <param name="message">The message to display when the assertion fails.</param>
    /// <param name="collectionExpression">
    /// The syntactic expression of collection as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
#pragma warning disable IDE0060 // Remove unused parameter
    public static void HasCount<T>(int expected, Span<T> collection, [InterpolatedStringHandlerArgument(nameof(expected), nameof(collection))] ref AssertCountInterpolatedStringHandler<T> message, [CallerArgumentExpression(nameof(collection))] string collectionExpression = "")
#pragma warning restore IDE0060 // Remove unused parameter
    {
        TelemetryCollector.TrackAssertionCall("Assert.HasCount");
        message.ComputeAssertion(nameof(HasCount), collectionExpression);
    }

    /// <summary>
    /// Tests whether the span has the expected count/length.
    /// </summary>
    /// <typeparam name="T">The type of the span items.</typeparam>
    /// <param name="expected">The expected count.</param>
    /// <param name="collection">The span.</param>
    /// <param name="message">The message to display when the assertion fails.</param>
    /// <param name="collectionExpression">
    /// The syntactic expression of collection as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    public static void HasCount<T>(int expected, Span<T> collection, string? message = "", [CallerArgumentExpression(nameof(collection))] string collectionExpression = "")
        => HasCount(nameof(HasCount), expected, collection, message, collectionExpression);

    /// <summary>
    /// Tests whether the memory has the expected count/length.
    /// </summary>
    /// <typeparam name="T">The type of the memory items.</typeparam>
    /// <param name="expected">The expected count.</param>
    /// <param name="collection">The memory.</param>
    /// <param name="message">The message to display when the assertion fails.</param>
    /// <param name="collectionExpression">
    /// The syntactic expression of collection as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
#pragma warning disable IDE0060 // Remove unused parameter
    public static void HasCount<T>(int expected, ReadOnlyMemory<T> collection, [InterpolatedStringHandlerArgument(nameof(expected), nameof(collection))] ref AssertCountInterpolatedStringHandler<T> message, [CallerArgumentExpression(nameof(collection))] string collectionExpression = "")
#pragma warning restore IDE0060 // Remove unused parameter
    {
        TelemetryCollector.TrackAssertionCall("Assert.HasCount");
        message.ComputeAssertion(nameof(HasCount), collectionExpression);
    }

    /// <summary>
    /// Tests whether the memory has the expected count/length.
    /// </summary>
    /// <typeparam name="T">The type of the memory items.</typeparam>
    /// <param name="expected">The expected count.</param>
    /// <param name="collection">The memory.</param>
    /// <param name="message">The message to display when the assertion fails.</param>
    /// <param name="collectionExpression">
    /// The syntactic expression of collection as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    public static void HasCount<T>(int expected, ReadOnlyMemory<T> collection, string? message = "", [CallerArgumentExpression(nameof(collection))] string collectionExpression = "")
        => HasCount(nameof(HasCount), expected, collection.Span, message, collectionExpression);

    /// <summary>
    /// Tests whether the memory has the expected count/length.
    /// </summary>
    /// <typeparam name="T">The type of the memory items.</typeparam>
    /// <param name="expected">The expected count.</param>
    /// <param name="collection">The memory.</param>
    /// <param name="message">The message to display when the assertion fails.</param>
    /// <param name="collectionExpression">
    /// The syntactic expression of collection as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
#pragma warning disable IDE0060 // Remove unused parameter
    public static void HasCount<T>(int expected, Memory<T> collection, [InterpolatedStringHandlerArgument(nameof(expected), nameof(collection))] ref AssertCountInterpolatedStringHandler<T> message, [CallerArgumentExpression(nameof(collection))] string collectionExpression = "")
#pragma warning restore IDE0060 // Remove unused parameter
    {
        TelemetryCollector.TrackAssertionCall("Assert.HasCount");
        message.ComputeAssertion(nameof(HasCount), collectionExpression);
    }

    /// <summary>
    /// Tests whether the memory has the expected count/length.
    /// </summary>
    /// <typeparam name="T">The type of the memory items.</typeparam>
    /// <param name="expected">The expected count.</param>
    /// <param name="collection">The memory.</param>
    /// <param name="message">The message to display when the assertion fails.</param>
    /// <param name="collectionExpression">
    /// The syntactic expression of collection as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    public static void HasCount<T>(int expected, Memory<T> collection, string? message = "", [CallerArgumentExpression(nameof(collection))] string collectionExpression = "")
        => HasCount(nameof(HasCount), expected, collection.Span, message, collectionExpression);

#endif

#pragma warning restore RS0026 // Do not add multiple public overloads with optional parameters
#pragma warning restore RS0027 // API with optional parameter(s) should have the most parameters amongst its public overloads

    private static void HasCount<T>(string assertionName, int expected, IEnumerable<T> collection, string? message, string collectionExpression)
    {
        // assertionName is one of a small fixed set ("HasCount", "IsEmpty"); use a cached prefixed
        // string instead of allocating "Assert." + assertionName on every call.
        TelemetryCollector.TrackAssertionCall(GetTrackedAssertionName(assertionName));

        int actualCount = collection.Count();
        if (actualCount == expected)
        {
            return;
        }

        ReportAssertCountFailed(assertionName, expected, actualCount, message, collectionExpression);
    }

    private static void HasCount(string assertionName, int expected, IEnumerable collection, string? message, string collectionExpression)
        => HasCount(assertionName, expected, collection.Cast<object>(), message, collectionExpression);

#if NETCOREAPP3_1_OR_GREATER
    private static void HasCount<T>(string assertionName, int expected, ReadOnlySpan<T> collection, string? message, string collectionExpression)
    {
        // assertionName is one of a small fixed set ("HasCount", "IsEmpty"); use a cached prefixed
        // string instead of allocating "Assert." + assertionName on every call.
        TelemetryCollector.TrackAssertionCall(GetTrackedAssertionName(assertionName));

        int actualCount = collection.Length;
        if (actualCount == expected)
        {
            return;
        }

        ReportAssertCountFailed(assertionName, expected, actualCount, message, collectionExpression);
    }
#endif

    private static string GetTrackedAssertionName(string assertionName)
        => assertionName switch
        {
            "HasCount" => "Assert.HasCount",
            "IsEmpty" => "Assert.IsEmpty",
            _ => string.Concat("Assert.", assertionName),
        };

    [DoesNotReturn]
    private static void ReportAssertCountFailed(string assertionName, int expectedCount, int actualCount, string? userMessage, string collectionExpression)
    {
        bool isEmptyAssertion = string.Equals(assertionName, nameof(IsEmpty), StringComparison.Ordinal);
        string summary = isEmptyAssertion
            ? FrameworkMessages.IsEmptyFailedSummary
            : FrameworkMessages.HasCountFailedSummary;

        string expectedEvidenceText = expectedCount.ToString(CultureInfo.CurrentCulture);
        string expectedCallSiteText = expectedCount.ToString(CultureInfo.InvariantCulture);
        string actualText = actualCount.ToString(CultureInfo.CurrentCulture);
        EvidenceBlock evidence = EvidenceBlock.Create()
            .AddLine("expected count:", expectedEvidenceText)
            .AddLine("actual count:", actualText);

        StructuredAssertionMessage structured = new(summary);
        structured.WithUserMessage(userMessage);
        structured.WithEvidence(evidence);
        structured.WithExpectedAndActual(expectedEvidenceText, actualText);
        structured.WithCallSiteExpression(isEmptyAssertion
            ? FormatCallSiteExpression($"Assert.{assertionName}", collectionExpression, "<collection>")
            : FormatCallSiteExpression($"Assert.{assertionName}", expectedCallSiteText, collectionExpression, "<expected>", "<collection>"));

        ReportAssertFailed(structured);
    }
}
