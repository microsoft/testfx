// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestTools.UnitTesting;

#pragma warning disable RS0026 // Do not add multiple public overloads with optional parameters
#pragma warning disable RS0027 // API with optional parameter(s) should have the most parameters amongst its public overloads
public sealed partial class Assert
{
    #region AreUnorderedEquivalent

    /// <summary>
    /// Tests whether two collections contain the same elements with the same multiplicity, regardless of order,
    /// and throws an exception if they do not.
    /// </summary>
    /// <remarks>
    /// This assertion performs an unordered multiset comparison of the top-level collection elements.
    /// It differs from <see cref="AreEquivalent{T}(T, T, string, string, string)"/>, which performs a deep,
    /// order-sensitive structural comparison.
    /// </remarks>
    /// <typeparam name="T">The type of the collection items.</typeparam>
    /// <param name="expected">The collection expected to be unordered-equivalent to <paramref name="actual"/>.</param>
    /// <param name="actual">The collection produced by the code under test.</param>
    /// <param name="message">
    /// The message to include in the exception when the collections are not unordered-equivalent.
    /// The message is shown in test results.
    /// </param>
    /// <param name="expectedExpression">
    /// The syntactic expression of expected as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    /// <param name="actualExpression">
    /// The syntactic expression of actual as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// Thrown if <paramref name="expected"/> and <paramref name="actual"/> are not unordered-equivalent.
    /// </exception>
    public static void AreUnorderedEquivalent<T>([NotNull] IEnumerable<T>? expected, [NotNull] IEnumerable<T>? actual, string? message = "", [CallerArgumentExpression(nameof(expected))] string expectedExpression = "", [CallerArgumentExpression(nameof(actual))] string actualExpression = "")
    {
        TelemetryCollector.TrackAssertionCall("Assert.AreUnorderedEquivalent");
        CheckParameterNotNull(expected, "Assert.AreUnorderedEquivalent", nameof(expected));
        CheckParameterNotNull(actual, "Assert.AreUnorderedEquivalent", nameof(actual));
        AreUnorderedEquivalentImpl(expected, actual, EqualityComparer<T>.Default, comparerName: null, message, expectedExpression, actualExpression);
    }

    /// <summary>
    /// Tests whether two collections contain the same elements with the same multiplicity, regardless of order,
    /// and throws an exception if they do not.
    /// </summary>
    /// <remarks>
    /// This assertion performs an unordered multiset comparison of the top-level collection elements.
    /// It differs from <see cref="AreEquivalent{T}(T, T, string, string, string)"/>, which performs a deep,
    /// order-sensitive structural comparison.
    /// </remarks>
    /// <typeparam name="T">The type of the collection items.</typeparam>
    /// <param name="expected">The collection expected to be unordered-equivalent to <paramref name="actual"/>.</param>
    /// <param name="actual">The collection produced by the code under test.</param>
    /// <param name="comparer">The equality comparer to use when comparing elements.</param>
    /// <param name="message">
    /// The message to include in the exception when the collections are not unordered-equivalent.
    /// The message is shown in test results.
    /// </param>
    /// <param name="expectedExpression">
    /// The syntactic expression of expected as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    /// <param name="actualExpression">
    /// The syntactic expression of actual as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// Thrown if <paramref name="expected"/> and <paramref name="actual"/> are not unordered-equivalent.
    /// </exception>
    public static void AreUnorderedEquivalent<T>([NotNull] IEnumerable<T>? expected, [NotNull] IEnumerable<T>? actual, [NotNull] IEqualityComparer<T>? comparer, string? message = "", [CallerArgumentExpression(nameof(expected))] string expectedExpression = "", [CallerArgumentExpression(nameof(actual))] string actualExpression = "")
    {
        TelemetryCollector.TrackAssertionCall("Assert.AreUnorderedEquivalent");
        CheckParameterNotNull(expected, "Assert.AreUnorderedEquivalent", nameof(expected));
        CheckParameterNotNull(actual, "Assert.AreUnorderedEquivalent", nameof(actual));
        CheckParameterNotNull(comparer, "Assert.AreUnorderedEquivalent", nameof(comparer));
        AreUnorderedEquivalentImpl(expected, actual, comparer, comparer.GetType().Name, message, expectedExpression, actualExpression);
    }

    /// <summary>
    /// Tests whether two collections contain the same elements with the same multiplicity, regardless of order,
    /// and throws an exception if they do not.
    /// </summary>
    /// <remarks>
    /// This assertion performs an unordered multiset comparison of the top-level collection elements.
    /// It differs from <see cref="AreEquivalent{T}(T, T, string, string, string)"/>, which performs a deep,
    /// order-sensitive structural comparison.
    /// </remarks>
    /// <param name="expected">The collection expected to be unordered-equivalent to <paramref name="actual"/>.</param>
    /// <param name="actual">The collection produced by the code under test.</param>
    /// <param name="message">
    /// The message to include in the exception when the collections are not unordered-equivalent.
    /// The message is shown in test results.
    /// </param>
    /// <param name="expectedExpression">
    /// The syntactic expression of expected as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    /// <param name="actualExpression">
    /// The syntactic expression of actual as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// Thrown if <paramref name="expected"/> and <paramref name="actual"/> are not unordered-equivalent.
    /// </exception>
    public static void AreUnorderedEquivalent([NotNull] IEnumerable? expected, [NotNull] IEnumerable? actual, string? message = "", [CallerArgumentExpression(nameof(expected))] string expectedExpression = "", [CallerArgumentExpression(nameof(actual))] string actualExpression = "")
    {
        TelemetryCollector.TrackAssertionCall("Assert.AreUnorderedEquivalent");
        CheckParameterNotNull(expected, "Assert.AreUnorderedEquivalent", nameof(expected));
        CheckParameterNotNull(actual, "Assert.AreUnorderedEquivalent", nameof(actual));
        AreUnorderedEquivalentImpl(expected.Cast<object?>(), actual.Cast<object?>(), EqualityComparer<object?>.Default, comparerName: null, message, expectedExpression, actualExpression);
    }

    /// <summary>
    /// Tests whether two collections contain the same elements with the same multiplicity, regardless of order,
    /// and throws an exception if they do not.
    /// </summary>
    /// <remarks>
    /// This assertion performs an unordered multiset comparison of the top-level collection elements.
    /// It differs from <see cref="AreEquivalent{T}(T, T, string, string, string)"/>, which performs a deep,
    /// order-sensitive structural comparison.
    /// </remarks>
    /// <param name="expected">The collection expected to be unordered-equivalent to <paramref name="actual"/>.</param>
    /// <param name="actual">The collection produced by the code under test.</param>
    /// <param name="comparer">The equality comparer to use when comparing elements.</param>
    /// <param name="message">
    /// The message to include in the exception when the collections are not unordered-equivalent.
    /// The message is shown in test results.
    /// </param>
    /// <param name="expectedExpression">
    /// The syntactic expression of expected as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    /// <param name="actualExpression">
    /// The syntactic expression of actual as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// Thrown if <paramref name="expected"/> and <paramref name="actual"/> are not unordered-equivalent.
    /// </exception>
    public static void AreUnorderedEquivalent([NotNull] IEnumerable? expected, [NotNull] IEnumerable? actual, [NotNull] IEqualityComparer? comparer, string? message = "", [CallerArgumentExpression(nameof(expected))] string expectedExpression = "", [CallerArgumentExpression(nameof(actual))] string actualExpression = "")
    {
        TelemetryCollector.TrackAssertionCall("Assert.AreUnorderedEquivalent");
        CheckParameterNotNull(expected, "Assert.AreUnorderedEquivalent", nameof(expected));
        CheckParameterNotNull(actual, "Assert.AreUnorderedEquivalent", nameof(actual));
        CheckParameterNotNull(comparer, "Assert.AreUnorderedEquivalent", nameof(comparer));
        AreUnorderedEquivalentImpl(expected.Cast<object?>(), actual.Cast<object?>(), new NonGenericEqualityComparerAdapter(comparer), comparer.GetType().Name, message, expectedExpression, actualExpression);
    }

    #endregion // AreUnorderedEquivalent

    #region AreNotUnorderedEquivalent

    /// <summary>
    /// Tests whether two collections do not contain the same elements with the same multiplicity, regardless of order,
    /// and throws an exception if they do.
    /// </summary>
    /// <remarks>
    /// This assertion performs an unordered multiset comparison of the top-level collection elements.
    /// It differs from <see cref="AreNotEquivalent{T}(T, T, string, string, string)"/>, which checks deep,
    /// order-sensitive structural comparison.
    /// </remarks>
    /// <typeparam name="T">The type of the collection items.</typeparam>
    /// <param name="expected">The collection expected to differ from <paramref name="actual"/>.</param>
    /// <param name="actual">The collection produced by the code under test.</param>
    /// <param name="message">
    /// The message to include in the exception when the collections are unexpectedly unordered-equivalent.
    /// The message is shown in test results.
    /// </param>
    /// <param name="expectedExpression">
    /// The syntactic expression of expected as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    /// <param name="actualExpression">
    /// The syntactic expression of actual as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// Thrown if <paramref name="expected"/> and <paramref name="actual"/> are unordered-equivalent.
    /// </exception>
    public static void AreNotUnorderedEquivalent<T>([NotNull] IEnumerable<T>? expected, [NotNull] IEnumerable<T>? actual, string? message = "", [CallerArgumentExpression(nameof(expected))] string expectedExpression = "", [CallerArgumentExpression(nameof(actual))] string actualExpression = "")
    {
        TelemetryCollector.TrackAssertionCall("Assert.AreNotUnorderedEquivalent");
        CheckParameterNotNull(expected, "Assert.AreNotUnorderedEquivalent", nameof(expected));
        CheckParameterNotNull(actual, "Assert.AreNotUnorderedEquivalent", nameof(actual));
        AreNotUnorderedEquivalentImpl(expected, actual, EqualityComparer<T>.Default, comparerName: null, message, expectedExpression, actualExpression);
    }

    /// <summary>
    /// Tests whether two collections do not contain the same elements with the same multiplicity, regardless of order,
    /// and throws an exception if they do.
    /// </summary>
    /// <remarks>
    /// This assertion performs an unordered multiset comparison of the top-level collection elements.
    /// It differs from <see cref="AreNotEquivalent{T}(T, T, string, string, string)"/>, which checks deep,
    /// order-sensitive structural comparison.
    /// </remarks>
    /// <typeparam name="T">The type of the collection items.</typeparam>
    /// <param name="expected">The collection expected to differ from <paramref name="actual"/>.</param>
    /// <param name="actual">The collection produced by the code under test.</param>
    /// <param name="comparer">The equality comparer to use when comparing elements.</param>
    /// <param name="message">
    /// The message to include in the exception when the collections are unexpectedly unordered-equivalent.
    /// The message is shown in test results.
    /// </param>
    /// <param name="expectedExpression">
    /// The syntactic expression of expected as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    /// <param name="actualExpression">
    /// The syntactic expression of actual as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// Thrown if <paramref name="expected"/> and <paramref name="actual"/> are unordered-equivalent.
    /// </exception>
    public static void AreNotUnorderedEquivalent<T>([NotNull] IEnumerable<T>? expected, [NotNull] IEnumerable<T>? actual, [NotNull] IEqualityComparer<T>? comparer, string? message = "", [CallerArgumentExpression(nameof(expected))] string expectedExpression = "", [CallerArgumentExpression(nameof(actual))] string actualExpression = "")
    {
        TelemetryCollector.TrackAssertionCall("Assert.AreNotUnorderedEquivalent");
        CheckParameterNotNull(expected, "Assert.AreNotUnorderedEquivalent", nameof(expected));
        CheckParameterNotNull(actual, "Assert.AreNotUnorderedEquivalent", nameof(actual));
        CheckParameterNotNull(comparer, "Assert.AreNotUnorderedEquivalent", nameof(comparer));
        AreNotUnorderedEquivalentImpl(expected, actual, comparer, comparer.GetType().Name, message, expectedExpression, actualExpression);
    }

    /// <summary>
    /// Tests whether two collections do not contain the same elements with the same multiplicity, regardless of order,
    /// and throws an exception if they do.
    /// </summary>
    /// <remarks>
    /// This assertion performs an unordered multiset comparison of the top-level collection elements.
    /// It differs from <see cref="AreNotEquivalent{T}(T, T, string, string, string)"/>, which checks deep,
    /// order-sensitive structural comparison.
    /// </remarks>
    /// <param name="expected">The collection expected to differ from <paramref name="actual"/>.</param>
    /// <param name="actual">The collection produced by the code under test.</param>
    /// <param name="message">
    /// The message to include in the exception when the collections are unexpectedly unordered-equivalent.
    /// The message is shown in test results.
    /// </param>
    /// <param name="expectedExpression">
    /// The syntactic expression of expected as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    /// <param name="actualExpression">
    /// The syntactic expression of actual as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// Thrown if <paramref name="expected"/> and <paramref name="actual"/> are unordered-equivalent.
    /// </exception>
    public static void AreNotUnorderedEquivalent([NotNull] IEnumerable? expected, [NotNull] IEnumerable? actual, string? message = "", [CallerArgumentExpression(nameof(expected))] string expectedExpression = "", [CallerArgumentExpression(nameof(actual))] string actualExpression = "")
    {
        TelemetryCollector.TrackAssertionCall("Assert.AreNotUnorderedEquivalent");
        CheckParameterNotNull(expected, "Assert.AreNotUnorderedEquivalent", nameof(expected));
        CheckParameterNotNull(actual, "Assert.AreNotUnorderedEquivalent", nameof(actual));
        AreNotUnorderedEquivalentImpl(expected.Cast<object?>(), actual.Cast<object?>(), EqualityComparer<object?>.Default, comparerName: null, message, expectedExpression, actualExpression);
    }

    /// <summary>
    /// Tests whether two collections do not contain the same elements with the same multiplicity, regardless of order,
    /// and throws an exception if they do.
    /// </summary>
    /// <remarks>
    /// This assertion performs an unordered multiset comparison of the top-level collection elements.
    /// It differs from <see cref="AreNotEquivalent{T}(T, T, string, string, string)"/>, which checks deep,
    /// order-sensitive structural comparison.
    /// </remarks>
    /// <param name="expected">The collection expected to differ from <paramref name="actual"/>.</param>
    /// <param name="actual">The collection produced by the code under test.</param>
    /// <param name="comparer">The equality comparer to use when comparing elements.</param>
    /// <param name="message">
    /// The message to include in the exception when the collections are unexpectedly unordered-equivalent.
    /// The message is shown in test results.
    /// </param>
    /// <param name="expectedExpression">
    /// The syntactic expression of expected as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    /// <param name="actualExpression">
    /// The syntactic expression of actual as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// Thrown if <paramref name="expected"/> and <paramref name="actual"/> are unordered-equivalent.
    /// </exception>
    public static void AreNotUnorderedEquivalent([NotNull] IEnumerable? expected, [NotNull] IEnumerable? actual, [NotNull] IEqualityComparer? comparer, string? message = "", [CallerArgumentExpression(nameof(expected))] string expectedExpression = "", [CallerArgumentExpression(nameof(actual))] string actualExpression = "")
    {
        TelemetryCollector.TrackAssertionCall("Assert.AreNotUnorderedEquivalent");
        CheckParameterNotNull(expected, "Assert.AreNotUnorderedEquivalent", nameof(expected));
        CheckParameterNotNull(actual, "Assert.AreNotUnorderedEquivalent", nameof(actual));
        CheckParameterNotNull(comparer, "Assert.AreNotUnorderedEquivalent", nameof(comparer));
        AreNotUnorderedEquivalentImpl(expected.Cast<object?>(), actual.Cast<object?>(), new NonGenericEqualityComparerAdapter(comparer), comparer.GetType().Name, message, expectedExpression, actualExpression);
    }

    #endregion // AreNotUnorderedEquivalent

    private static void AreUnorderedEquivalentImpl<T>(IEnumerable<T?> expected, IEnumerable<T?> actual, IEqualityComparer<T> comparer, string? comparerName, string? message, string expectedExpression, string actualExpression)
    {
        List<T?> expectedList = expected is List<T?> expectedItems ? expectedItems : [.. expected];
        List<T?> actualList = actual is List<T?> actualItems ? actualItems : [.. actual];

        if (CollectionAssert.TryFindMissingAndUnexpectedElements(expectedList, actualList, comparer, out List<T?>? missing, out List<T?>? unexpected))
        {
            ReportAssertAreUnorderedEquivalentFailed(expectedList, actualList, missing, unexpected, comparerName, message, expectedExpression, actualExpression);
        }
    }

    private static void AreNotUnorderedEquivalentImpl<T>(IEnumerable<T?> expected, IEnumerable<T?> actual, IEqualityComparer<T> comparer, string? comparerName, string? message, string expectedExpression, string actualExpression)
    {
        List<T?> expectedList = expected is List<T?> expectedItems ? expectedItems : [.. expected];
        List<T?> actualList = actual is List<T?> actualItems ? actualItems : [.. actual];

        if (!CollectionAssert.TryFindMissingAndUnexpectedElements(expectedList, actualList, comparer, out _, out _))
        {
            ReportAssertAreNotUnorderedEquivalentFailed(expectedList, actualList, comparerName, message, expectedExpression, actualExpression);
        }
    }

    [DoesNotReturn]
    private static void ReportAssertAreUnorderedEquivalentFailed<T>(IEnumerable<T?> expected, IEnumerable<T?> actual, List<T?>? missing, List<T?>? unexpected, string? comparerName, string? message, string expectedExpression, string actualExpression)
    {
        List<T?> missingItems = missing ?? [];
        List<T?> unexpectedItems = unexpected ?? [];
        string expectedText = AssertionValueRenderer.RenderValue(expected);
        string actualText = AssertionValueRenderer.RenderValue(actual);
        string missingText = AssertionValueRenderer.RenderValue(missingItems);
        string unexpectedText = AssertionValueRenderer.RenderValue(unexpectedItems);
        string differenceSummary = string.Format(CultureInfo.CurrentCulture, FrameworkMessages.AreUnorderedEquivalentDifferenceSummary, missingItems.Count, unexpectedItems.Count);

        EvidenceBlock evidence = EvidenceBlock.Create()
            .AddLine("missing:", missingText)
            .AddLine("unexpected:", unexpectedText);

        if (comparerName is not null)
        {
            evidence.AddLine("comparer:", comparerName);
        }

        StructuredAssertionMessage structured = new(FrameworkMessages.AreUnorderedEquivalentFailedSummary);
        structured.WithAdditionalSummaryLine(differenceSummary);
        structured.WithUserMessage(message);
        structured.WithEvidence(evidence);
        structured.WithExpectedAndActual(expectedText, actualText);
        structured.WithCallSiteExpression(BuildUnorderedEquivalentCallSiteWithComparer("Assert.AreUnorderedEquivalent", expectedExpression, actualExpression, comparerName is not null));

        ReportAssertFailed(structured);
    }

    [DoesNotReturn]
    private static void ReportAssertAreNotUnorderedEquivalentFailed<T>(IEnumerable<T?> expected, IEnumerable<T?> actual, string? comparerName, string? message, string expectedExpression, string actualExpression)
    {
        string expectedText = AssertionValueRenderer.RenderValue(expected);
        string actualText = AssertionValueRenderer.RenderValue(actual);

        StructuredAssertionMessage structured = new(FrameworkMessages.AreNotUnorderedEquivalentFailedSummary);
        structured.WithUserMessage(message);
        structured.WithExpectedAndActual($"not unordered equivalent to {expectedText}", actualText);
        structured.WithCallSiteExpression(BuildUnorderedEquivalentCallSiteWithComparer("Assert.AreNotUnorderedEquivalent", expectedExpression, actualExpression, comparerName is not null));

        ReportAssertFailed(structured);
    }

    private static string? BuildUnorderedEquivalentCallSiteWithComparer(string assertionMethodName, string expectedExpression, string actualExpression, bool hasComparer)
        => hasComparer
            ? FormatCallSiteExpression(assertionMethodName, expectedExpression, actualExpression, expression3: "<comparer>", "<expected>", "<actual>", "<comparer>")
            : FormatCallSiteExpression(assertionMethodName, expectedExpression, actualExpression, "<expected>", "<actual>");
}
