// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestTools.UnitTesting;

#pragma warning disable RS0026 // Do not add multiple public overloads with optional parameters
#pragma warning disable RS0027 // API with optional parameter(s) should have the most parameters amongst its public overloads
public sealed partial class Assert
{
    /// <summary>
    /// Tests whether <paramref name="subset"/> is a subset of <paramref name="superset"/>, with element multiplicity taken into account.
    /// </summary>
    /// <typeparam name="T">The type of the collection items.</typeparam>
    /// <param name="subset">The collection expected to be a subset of <paramref name="superset"/>.</param>
    /// <param name="superset">The collection expected to be a superset of <paramref name="subset"/>.</param>
    /// <param name="message">The message to include in the exception when the assertion fails.</param>
    /// <param name="subsetExpression">The syntactic expression of subset as given by the compiler.</param>
    /// <param name="supersetExpression">The syntactic expression of superset as given by the compiler.</param>
    public static void IsSubsetOf<T>([NotNull] IEnumerable<T>? subset, [NotNull] IEnumerable<T>? superset, string? message = "", [CallerArgumentExpression(nameof(subset))] string subsetExpression = "", [CallerArgumentExpression(nameof(superset))] string supersetExpression = "")
    {
        TelemetryCollector.TrackAssertionCall("Assert.IsSubsetOf");
        CheckParameterNotNull(subset, "Assert.IsSubsetOf", "subset");
        CheckParameterNotNull(superset, "Assert.IsSubsetOf", "superset");
        IsSubsetOfImpl(subset, superset, EqualityComparer<T>.Default, comparerName: null, message, subsetExpression, supersetExpression);
    }

    /// <summary>
    /// Tests whether <paramref name="subset"/> is a subset of <paramref name="superset"/>, using the supplied comparer and taking element multiplicity into account.
    /// </summary>
    /// <typeparam name="T">The type of the collection items.</typeparam>
    /// <param name="subset">The collection expected to be a subset of <paramref name="superset"/>.</param>
    /// <param name="superset">The collection expected to be a superset of <paramref name="subset"/>.</param>
    /// <param name="comparer">The equality comparer to use when comparing elements.</param>
    /// <param name="message">The message to include in the exception when the assertion fails.</param>
    /// <param name="subsetExpression">The syntactic expression of subset as given by the compiler.</param>
    /// <param name="supersetExpression">The syntactic expression of superset as given by the compiler.</param>
    public static void IsSubsetOf<T>([NotNull] IEnumerable<T>? subset, [NotNull] IEnumerable<T>? superset, [NotNull] IEqualityComparer<T>? comparer, string? message = "", [CallerArgumentExpression(nameof(subset))] string subsetExpression = "", [CallerArgumentExpression(nameof(superset))] string supersetExpression = "")
    {
        TelemetryCollector.TrackAssertionCall("Assert.IsSubsetOf");
        CheckParameterNotNull(subset, "Assert.IsSubsetOf", "subset");
        CheckParameterNotNull(superset, "Assert.IsSubsetOf", "superset");
        CheckParameterNotNull(comparer, "Assert.IsSubsetOf", "comparer");
        IsSubsetOfImpl(subset, superset, comparer, comparer.GetType().Name, message, subsetExpression, supersetExpression);
    }

    /// <summary>
    /// Tests whether <paramref name="subset"/> is a subset of <paramref name="superset"/>, with element multiplicity taken into account.
    /// </summary>
    /// <param name="subset">The collection expected to be a subset of <paramref name="superset"/>.</param>
    /// <param name="superset">The collection expected to be a superset of <paramref name="subset"/>.</param>
    /// <param name="message">The message to include in the exception when the assertion fails.</param>
    /// <param name="subsetExpression">The syntactic expression of subset as given by the compiler.</param>
    /// <param name="supersetExpression">The syntactic expression of superset as given by the compiler.</param>
    public static void IsSubsetOf([NotNull] IEnumerable? subset, [NotNull] IEnumerable? superset, string? message = "", [CallerArgumentExpression(nameof(subset))] string subsetExpression = "", [CallerArgumentExpression(nameof(superset))] string supersetExpression = "")
    {
        TelemetryCollector.TrackAssertionCall("Assert.IsSubsetOf");
        CheckParameterNotNull(subset, "Assert.IsSubsetOf", "subset");
        CheckParameterNotNull(superset, "Assert.IsSubsetOf", "superset");
        IsSubsetOfImpl(subset.Cast<object?>(), superset.Cast<object?>(), EqualityComparer<object?>.Default, comparerName: null, message, subsetExpression, supersetExpression);
    }

    /// <summary>
    /// Tests whether <paramref name="subset"/> is a subset of <paramref name="superset"/>, using the supplied comparer and taking element multiplicity into account.
    /// </summary>
    /// <param name="subset">The collection expected to be a subset of <paramref name="superset"/>.</param>
    /// <param name="superset">The collection expected to be a superset of <paramref name="subset"/>.</param>
    /// <param name="comparer">The equality comparer to use when comparing elements.</param>
    /// <param name="message">The message to include in the exception when the assertion fails.</param>
    /// <param name="subsetExpression">The syntactic expression of subset as given by the compiler.</param>
    /// <param name="supersetExpression">The syntactic expression of superset as given by the compiler.</param>
    public static void IsSubsetOf([NotNull] IEnumerable? subset, [NotNull] IEnumerable? superset, [NotNull] IEqualityComparer? comparer, string? message = "", [CallerArgumentExpression(nameof(subset))] string subsetExpression = "", [CallerArgumentExpression(nameof(superset))] string supersetExpression = "")
    {
        TelemetryCollector.TrackAssertionCall("Assert.IsSubsetOf");
        CheckParameterNotNull(subset, "Assert.IsSubsetOf", "subset");
        CheckParameterNotNull(superset, "Assert.IsSubsetOf", "superset");
        CheckParameterNotNull(comparer, "Assert.IsSubsetOf", "comparer");
        IsSubsetOfImpl(subset.Cast<object?>(), superset.Cast<object?>(), new NonGenericEqualityComparerAdapter(comparer), comparer.GetType().Name, message, subsetExpression, supersetExpression);
    }

    /// <summary>
    /// Tests whether <paramref name="subset"/> is not a subset of <paramref name="superset"/>, with element multiplicity taken into account.
    /// </summary>
    /// <typeparam name="T">The type of the collection items.</typeparam>
    /// <param name="subset">The collection expected not to be a subset of <paramref name="superset"/>.</param>
    /// <param name="superset">The collection expected not to be a superset of <paramref name="subset"/>.</param>
    /// <param name="message">The message to include in the exception when the assertion fails.</param>
    /// <param name="subsetExpression">The syntactic expression of subset as given by the compiler.</param>
    /// <param name="supersetExpression">The syntactic expression of superset as given by the compiler.</param>
    public static void IsNotSubsetOf<T>([NotNull] IEnumerable<T>? subset, [NotNull] IEnumerable<T>? superset, string? message = "", [CallerArgumentExpression(nameof(subset))] string subsetExpression = "", [CallerArgumentExpression(nameof(superset))] string supersetExpression = "")
    {
        TelemetryCollector.TrackAssertionCall("Assert.IsNotSubsetOf");
        CheckParameterNotNull(subset, "Assert.IsNotSubsetOf", "subset");
        CheckParameterNotNull(superset, "Assert.IsNotSubsetOf", "superset");
        IsNotSubsetOfImpl(subset, superset, EqualityComparer<T>.Default, comparerName: null, message, subsetExpression, supersetExpression);
    }

    /// <summary>
    /// Tests whether <paramref name="subset"/> is not a subset of <paramref name="superset"/>, using the supplied comparer and taking element multiplicity into account.
    /// </summary>
    /// <typeparam name="T">The type of the collection items.</typeparam>
    /// <param name="subset">The collection expected not to be a subset of <paramref name="superset"/>.</param>
    /// <param name="superset">The collection expected not to be a superset of <paramref name="subset"/>.</param>
    /// <param name="comparer">The equality comparer to use when comparing elements.</param>
    /// <param name="message">The message to include in the exception when the assertion fails.</param>
    /// <param name="subsetExpression">The syntactic expression of subset as given by the compiler.</param>
    /// <param name="supersetExpression">The syntactic expression of superset as given by the compiler.</param>
    public static void IsNotSubsetOf<T>([NotNull] IEnumerable<T>? subset, [NotNull] IEnumerable<T>? superset, [NotNull] IEqualityComparer<T>? comparer, string? message = "", [CallerArgumentExpression(nameof(subset))] string subsetExpression = "", [CallerArgumentExpression(nameof(superset))] string supersetExpression = "")
    {
        TelemetryCollector.TrackAssertionCall("Assert.IsNotSubsetOf");
        CheckParameterNotNull(subset, "Assert.IsNotSubsetOf", "subset");
        CheckParameterNotNull(superset, "Assert.IsNotSubsetOf", "superset");
        CheckParameterNotNull(comparer, "Assert.IsNotSubsetOf", "comparer");
        IsNotSubsetOfImpl(subset, superset, comparer, comparer.GetType().Name, message, subsetExpression, supersetExpression);
    }

    /// <summary>
    /// Tests whether <paramref name="subset"/> is not a subset of <paramref name="superset"/>, with element multiplicity taken into account.
    /// </summary>
    /// <param name="subset">The collection expected not to be a subset of <paramref name="superset"/>.</param>
    /// <param name="superset">The collection expected not to be a superset of <paramref name="subset"/>.</param>
    /// <param name="message">The message to include in the exception when the assertion fails.</param>
    /// <param name="subsetExpression">The syntactic expression of subset as given by the compiler.</param>
    /// <param name="supersetExpression">The syntactic expression of superset as given by the compiler.</param>
    public static void IsNotSubsetOf([NotNull] IEnumerable? subset, [NotNull] IEnumerable? superset, string? message = "", [CallerArgumentExpression(nameof(subset))] string subsetExpression = "", [CallerArgumentExpression(nameof(superset))] string supersetExpression = "")
    {
        TelemetryCollector.TrackAssertionCall("Assert.IsNotSubsetOf");
        CheckParameterNotNull(subset, "Assert.IsNotSubsetOf", "subset");
        CheckParameterNotNull(superset, "Assert.IsNotSubsetOf", "superset");
        IsNotSubsetOfImpl(subset.Cast<object?>(), superset.Cast<object?>(), EqualityComparer<object?>.Default, comparerName: null, message, subsetExpression, supersetExpression);
    }

    /// <summary>
    /// Tests whether <paramref name="subset"/> is not a subset of <paramref name="superset"/>, using the supplied comparer and taking element multiplicity into account.
    /// </summary>
    /// <param name="subset">The collection expected not to be a subset of <paramref name="superset"/>.</param>
    /// <param name="superset">The collection expected not to be a superset of <paramref name="subset"/>.</param>
    /// <param name="comparer">The equality comparer to use when comparing elements.</param>
    /// <param name="message">The message to include in the exception when the assertion fails.</param>
    /// <param name="subsetExpression">The syntactic expression of subset as given by the compiler.</param>
    /// <param name="supersetExpression">The syntactic expression of superset as given by the compiler.</param>
    public static void IsNotSubsetOf([NotNull] IEnumerable? subset, [NotNull] IEnumerable? superset, [NotNull] IEqualityComparer? comparer, string? message = "", [CallerArgumentExpression(nameof(subset))] string subsetExpression = "", [CallerArgumentExpression(nameof(superset))] string supersetExpression = "")
    {
        TelemetryCollector.TrackAssertionCall("Assert.IsNotSubsetOf");
        CheckParameterNotNull(subset, "Assert.IsNotSubsetOf", "subset");
        CheckParameterNotNull(superset, "Assert.IsNotSubsetOf", "superset");
        CheckParameterNotNull(comparer, "Assert.IsNotSubsetOf", "comparer");
        IsNotSubsetOfImpl(subset.Cast<object?>(), superset.Cast<object?>(), new NonGenericEqualityComparerAdapter(comparer), comparer.GetType().Name, message, subsetExpression, supersetExpression);
    }

#if NETCOREAPP3_1_OR_GREATER

    /// <summary>
    /// Tests whether <paramref name="subset"/> is a subset of <paramref name="superset"/>.
    /// </summary>
    public static void IsSubsetOf<T>(ReadOnlySpan<T> subset, ReadOnlySpan<T> superset, string? message = "", [CallerArgumentExpression(nameof(subset))] string subsetExpression = "", [CallerArgumentExpression(nameof(superset))] string supersetExpression = "")
    {
        TelemetryCollector.TrackAssertionCall("Assert.IsSubsetOf");
        IsSubsetOfImpl<T>(subset.ToArray(), superset.ToArray(), EqualityComparer<T>.Default, comparerName: null, message, subsetExpression, supersetExpression);
    }

    /// <summary>
    /// Tests whether <paramref name="subset"/> is a subset of <paramref name="superset"/> using the supplied comparer.
    /// </summary>
    public static void IsSubsetOf<T>(ReadOnlySpan<T> subset, ReadOnlySpan<T> superset, [NotNull] IEqualityComparer<T>? comparer, string? message = "", [CallerArgumentExpression(nameof(subset))] string subsetExpression = "", [CallerArgumentExpression(nameof(superset))] string supersetExpression = "")
    {
        TelemetryCollector.TrackAssertionCall("Assert.IsSubsetOf");
        CheckParameterNotNull(comparer, "Assert.IsSubsetOf", "comparer");
        IsSubsetOfImpl<T>(subset.ToArray(), superset.ToArray(), comparer, comparer.GetType().Name, message, subsetExpression, supersetExpression);
    }

    /// <inheritdoc cref="IsSubsetOf{T}(ReadOnlySpan{T}, ReadOnlySpan{T}, string?, string, string)"/>
    public static void IsSubsetOf<T>(Span<T> subset, Span<T> superset, string? message = "", [CallerArgumentExpression(nameof(subset))] string subsetExpression = "", [CallerArgumentExpression(nameof(superset))] string supersetExpression = "")
        => IsSubsetOf((ReadOnlySpan<T>)subset, superset, message, subsetExpression, supersetExpression);

    /// <inheritdoc cref="IsSubsetOf{T}(ReadOnlySpan{T}, ReadOnlySpan{T}, IEqualityComparer{T}?, string?, string, string)"/>
    public static void IsSubsetOf<T>(Span<T> subset, Span<T> superset, [NotNull] IEqualityComparer<T>? comparer, string? message = "", [CallerArgumentExpression(nameof(subset))] string subsetExpression = "", [CallerArgumentExpression(nameof(superset))] string supersetExpression = "")
        => IsSubsetOf((ReadOnlySpan<T>)subset, superset, comparer, message, subsetExpression, supersetExpression);

    /// <inheritdoc cref="IsSubsetOf{T}(ReadOnlySpan{T}, ReadOnlySpan{T}, string?, string, string)"/>
    public static void IsSubsetOf<T>(ReadOnlyMemory<T> subset, ReadOnlyMemory<T> superset, string? message = "", [CallerArgumentExpression(nameof(subset))] string subsetExpression = "", [CallerArgumentExpression(nameof(superset))] string supersetExpression = "")
        => IsSubsetOf(subset.Span, superset.Span, message, subsetExpression, supersetExpression);

    /// <inheritdoc cref="IsSubsetOf{T}(ReadOnlySpan{T}, ReadOnlySpan{T}, IEqualityComparer{T}?, string?, string, string)"/>
    public static void IsSubsetOf<T>(ReadOnlyMemory<T> subset, ReadOnlyMemory<T> superset, [NotNull] IEqualityComparer<T>? comparer, string? message = "", [CallerArgumentExpression(nameof(subset))] string subsetExpression = "", [CallerArgumentExpression(nameof(superset))] string supersetExpression = "")
        => IsSubsetOf(subset.Span, superset.Span, comparer, message, subsetExpression, supersetExpression);

    /// <inheritdoc cref="IsSubsetOf{T}(ReadOnlySpan{T}, ReadOnlySpan{T}, string?, string, string)"/>
    public static void IsSubsetOf<T>(Memory<T> subset, Memory<T> superset, string? message = "", [CallerArgumentExpression(nameof(subset))] string subsetExpression = "", [CallerArgumentExpression(nameof(superset))] string supersetExpression = "")
        => IsSubsetOf(subset.Span, superset.Span, message, subsetExpression, supersetExpression);

    /// <inheritdoc cref="IsSubsetOf{T}(ReadOnlySpan{T}, ReadOnlySpan{T}, IEqualityComparer{T}?, string?, string, string)"/>
    public static void IsSubsetOf<T>(Memory<T> subset, Memory<T> superset, [NotNull] IEqualityComparer<T>? comparer, string? message = "", [CallerArgumentExpression(nameof(subset))] string subsetExpression = "", [CallerArgumentExpression(nameof(superset))] string supersetExpression = "")
        => IsSubsetOf(subset.Span, superset.Span, comparer, message, subsetExpression, supersetExpression);

    /// <summary>
    /// Tests whether <paramref name="subset"/> is not a subset of <paramref name="superset"/>.
    /// </summary>
    public static void IsNotSubsetOf<T>(ReadOnlySpan<T> subset, ReadOnlySpan<T> superset, string? message = "", [CallerArgumentExpression(nameof(subset))] string subsetExpression = "", [CallerArgumentExpression(nameof(superset))] string supersetExpression = "")
    {
        TelemetryCollector.TrackAssertionCall("Assert.IsNotSubsetOf");
        IsNotSubsetOfImpl<T>(subset.ToArray(), superset.ToArray(), EqualityComparer<T>.Default, comparerName: null, message, subsetExpression, supersetExpression);
    }

    /// <summary>
    /// Tests whether <paramref name="subset"/> is not a subset of <paramref name="superset"/> using the supplied comparer.
    /// </summary>
    public static void IsNotSubsetOf<T>(ReadOnlySpan<T> subset, ReadOnlySpan<T> superset, [NotNull] IEqualityComparer<T>? comparer, string? message = "", [CallerArgumentExpression(nameof(subset))] string subsetExpression = "", [CallerArgumentExpression(nameof(superset))] string supersetExpression = "")
    {
        TelemetryCollector.TrackAssertionCall("Assert.IsNotSubsetOf");
        CheckParameterNotNull(comparer, "Assert.IsNotSubsetOf", "comparer");
        IsNotSubsetOfImpl<T>(subset.ToArray(), superset.ToArray(), comparer, comparer.GetType().Name, message, subsetExpression, supersetExpression);
    }

    /// <inheritdoc cref="IsNotSubsetOf{T}(ReadOnlySpan{T}, ReadOnlySpan{T}, string?, string, string)"/>
    public static void IsNotSubsetOf<T>(Span<T> subset, Span<T> superset, string? message = "", [CallerArgumentExpression(nameof(subset))] string subsetExpression = "", [CallerArgumentExpression(nameof(superset))] string supersetExpression = "")
        => IsNotSubsetOf((ReadOnlySpan<T>)subset, superset, message, subsetExpression, supersetExpression);

    /// <inheritdoc cref="IsNotSubsetOf{T}(ReadOnlySpan{T}, ReadOnlySpan{T}, IEqualityComparer{T}?, string?, string, string)"/>
    public static void IsNotSubsetOf<T>(Span<T> subset, Span<T> superset, [NotNull] IEqualityComparer<T>? comparer, string? message = "", [CallerArgumentExpression(nameof(subset))] string subsetExpression = "", [CallerArgumentExpression(nameof(superset))] string supersetExpression = "")
        => IsNotSubsetOf((ReadOnlySpan<T>)subset, superset, comparer, message, subsetExpression, supersetExpression);

    /// <inheritdoc cref="IsNotSubsetOf{T}(ReadOnlySpan{T}, ReadOnlySpan{T}, string?, string, string)"/>
    public static void IsNotSubsetOf<T>(ReadOnlyMemory<T> subset, ReadOnlyMemory<T> superset, string? message = "", [CallerArgumentExpression(nameof(subset))] string subsetExpression = "", [CallerArgumentExpression(nameof(superset))] string supersetExpression = "")
        => IsNotSubsetOf(subset.Span, superset.Span, message, subsetExpression, supersetExpression);

    /// <inheritdoc cref="IsNotSubsetOf{T}(ReadOnlySpan{T}, ReadOnlySpan{T}, IEqualityComparer{T}?, string?, string, string)"/>
    public static void IsNotSubsetOf<T>(ReadOnlyMemory<T> subset, ReadOnlyMemory<T> superset, [NotNull] IEqualityComparer<T>? comparer, string? message = "", [CallerArgumentExpression(nameof(subset))] string subsetExpression = "", [CallerArgumentExpression(nameof(superset))] string supersetExpression = "")
        => IsNotSubsetOf(subset.Span, superset.Span, comparer, message, subsetExpression, supersetExpression);

    /// <inheritdoc cref="IsNotSubsetOf{T}(ReadOnlySpan{T}, ReadOnlySpan{T}, string?, string, string)"/>
    public static void IsNotSubsetOf<T>(Memory<T> subset, Memory<T> superset, string? message = "", [CallerArgumentExpression(nameof(subset))] string subsetExpression = "", [CallerArgumentExpression(nameof(superset))] string supersetExpression = "")
        => IsNotSubsetOf(subset.Span, superset.Span, message, subsetExpression, supersetExpression);

    /// <inheritdoc cref="IsNotSubsetOf{T}(ReadOnlySpan{T}, ReadOnlySpan{T}, IEqualityComparer{T}?, string?, string, string)"/>
    public static void IsNotSubsetOf<T>(Memory<T> subset, Memory<T> superset, [NotNull] IEqualityComparer<T>? comparer, string? message = "", [CallerArgumentExpression(nameof(subset))] string subsetExpression = "", [CallerArgumentExpression(nameof(superset))] string supersetExpression = "")
        => IsNotSubsetOf(subset.Span, superset.Span, comparer, message, subsetExpression, supersetExpression);

#endif

    private static void IsSubsetOfImpl<T>(IEnumerable<T?> subset, IEnumerable<T?> superset, IEqualityComparer<T> comparer, string? comparerName, string? message, string subsetExpression, string supersetExpression)
    {
        List<T?> subsetList = subset is List<T?> subsetItems ? subsetItems : [.. subset];
        List<T?> supersetList = superset is List<T?> supersetItems ? supersetItems : [.. superset];

        if (TryFindMissingElements(subsetList, supersetList, comparer, out List<T?>? missing))
        {
            ReportAssertIsSubsetOfFailed(subsetList, supersetList, missing, comparerName, message, subsetExpression, supersetExpression);
        }
    }

    private static void IsNotSubsetOfImpl<T>(IEnumerable<T?> subset, IEnumerable<T?> superset, IEqualityComparer<T> comparer, string? comparerName, string? message, string subsetExpression, string supersetExpression)
    {
        List<T?> subsetList = subset is List<T?> subsetItems ? subsetItems : [.. subset];
        List<T?> supersetList = superset is List<T?> supersetItems ? supersetItems : [.. superset];

        if (!HasAnyMissingElement(subsetList, supersetList, comparer))
        {
            ReportAssertIsNotSubsetOfFailed(subsetList, supersetList, comparerName, message, subsetExpression, supersetExpression);
        }
    }

    [DoesNotReturn]
    private static void ReportAssertIsSubsetOfFailed<T>(IEnumerable<T?> subset, IEnumerable<T?> superset, List<T?> missing, string? comparerName, string? message, string subsetExpression, string supersetExpression)
    {
        string subsetText = AssertionValueRenderer.RenderValue(subset);
        string supersetText = AssertionValueRenderer.RenderValue(superset);

        EvidenceBlock evidence = EvidenceBlock.Create()
            .AddLine("missing:", AssertionValueRenderer.RenderValue(missing))
            .AddLine("subset:", subsetText)
            .AddLine("superset:", supersetText);

        if (comparerName is not null)
        {
            evidence.AddLine("comparer:", comparerName);
        }

        StructuredAssertionMessage structured = new(FrameworkMessages.IsSubsetOfFailedSummary);
        structured.WithUserMessage(message);
        structured.WithEvidence(evidence);
        structured.WithExpectedAndActual(subsetText, supersetText);
        structured.WithCallSiteExpression(BuildCallSiteWithComparer("Assert.IsSubsetOf", subsetExpression, supersetExpression, comparerName is not null, "<subset>", "<superset>"));

        ReportAssertFailed(structured);
    }

    [DoesNotReturn]
    private static void ReportAssertIsNotSubsetOfFailed<T>(IEnumerable<T?> subset, IEnumerable<T?> superset, string? comparerName, string? message, string subsetExpression, string supersetExpression)
    {
        string subsetText = AssertionValueRenderer.RenderValue(subset);
        string supersetText = AssertionValueRenderer.RenderValue(superset);

        EvidenceBlock evidence = EvidenceBlock.Create()
            .AddLine("subset:", subsetText)
            .AddLine("superset:", supersetText);

        if (comparerName is not null)
        {
            evidence.AddLine("comparer:", comparerName);
        }

        StructuredAssertionMessage structured = new(FrameworkMessages.IsNotSubsetOfFailedSummary);
        structured.WithUserMessage(message);
        structured.WithEvidence(evidence);
        structured.WithExpectedAndActual(expectedText: null, actualText: supersetText);
        structured.WithCallSiteExpression(BuildCallSiteWithComparer("Assert.IsNotSubsetOf", subsetExpression, supersetExpression, comparerName is not null, "<subset>", "<superset>"));

        ReportAssertFailed(structured);
    }
}
