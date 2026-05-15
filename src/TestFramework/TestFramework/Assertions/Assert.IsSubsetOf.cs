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
    #region IsSubsetOf

    /// <summary>
    /// Tests whether one collection is a subset of another collection and throws an
    /// exception if any element in the subset is not also in the superset.
    /// </summary>
    /// <typeparam name="T">The type of the collection items.</typeparam>
    /// <param name="subset">
    /// The collection expected to be a subset of <paramref name="superset"/>.
    /// </param>
    /// <param name="superset">
    /// The collection expected to be a superset of <paramref name="subset"/>.
    /// </param>
    /// <param name="message">
    /// The message to include in the exception when an element in <paramref name="subset"/>
    /// is not found in <paramref name="superset"/>. The message is shown in test results.
    /// </param>
    /// <param name="subsetExpression">
    /// The syntactic expression of subset as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    /// <param name="supersetExpression">
    /// The syntactic expression of superset as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// Thrown if <paramref name="subset"/> contains at least one element not contained in <paramref name="superset"/>.
    /// </exception>
    public static void IsSubsetOf<T>([NotNull] IEnumerable<T>? subset, [NotNull] IEnumerable<T>? superset, string? message = "", [CallerArgumentExpression(nameof(subset))] string subsetExpression = "", [CallerArgumentExpression(nameof(superset))] string supersetExpression = "")
    {
        CheckParameterNotNull(subset, "Assert.IsSubsetOf", "subset");
        CheckParameterNotNull(superset, "Assert.IsSubsetOf", "superset");
        IsSubsetOfImpl(subset, superset, EqualityComparer<T>.Default, comparerName: null, message, subsetExpression, supersetExpression);
    }

    /// <summary>
    /// Tests whether one collection is a subset of another collection and throws an
    /// exception if any element in the subset is not also in the superset.
    /// </summary>
    /// <typeparam name="T">The type of the collection items.</typeparam>
    /// <param name="subset">
    /// The collection expected to be a subset of <paramref name="superset"/>.
    /// </param>
    /// <param name="superset">
    /// The collection expected to be a superset of <paramref name="subset"/>.
    /// </param>
    /// <param name="comparer">
    /// The equality comparer to use when comparing elements.
    /// </param>
    /// <param name="message">
    /// The message to include in the exception when an element in <paramref name="subset"/>
    /// is not found in <paramref name="superset"/>. The message is shown in test results.
    /// </param>
    /// <param name="subsetExpression">
    /// The syntactic expression of subset as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    /// <param name="supersetExpression">
    /// The syntactic expression of superset as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// Thrown if <paramref name="subset"/> contains at least one element not contained in <paramref name="superset"/>.
    /// </exception>
    public static void IsSubsetOf<T>([NotNull] IEnumerable<T>? subset, [NotNull] IEnumerable<T>? superset, [NotNull] IEqualityComparer<T>? comparer, string? message = "", [CallerArgumentExpression(nameof(subset))] string subsetExpression = "", [CallerArgumentExpression(nameof(superset))] string supersetExpression = "")
    {
        CheckParameterNotNull(subset, "Assert.IsSubsetOf", "subset");
        CheckParameterNotNull(superset, "Assert.IsSubsetOf", "superset");
        CheckParameterNotNull(comparer, "Assert.IsSubsetOf", "comparer");
        IsSubsetOfImpl(subset, superset, comparer, comparer.GetType().Name, message, subsetExpression, supersetExpression);
    }

    /// <summary>
    /// Tests whether one collection is a subset of another collection and throws an
    /// exception if any element in the subset is not also in the superset.
    /// </summary>
    /// <param name="subset">
    /// The collection expected to be a subset of <paramref name="superset"/>.
    /// </param>
    /// <param name="superset">
    /// The collection expected to be a superset of <paramref name="subset"/>.
    /// </param>
    /// <param name="message">
    /// The message to include in the exception when an element in <paramref name="subset"/>
    /// is not found in <paramref name="superset"/>. The message is shown in test results.
    /// </param>
    /// <param name="subsetExpression">
    /// The syntactic expression of subset as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    /// <param name="supersetExpression">
    /// The syntactic expression of superset as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// Thrown if <paramref name="subset"/> contains at least one element not contained in <paramref name="superset"/>.
    /// </exception>
    public static void IsSubsetOf([NotNull] IEnumerable? subset, [NotNull] IEnumerable? superset, string? message = "", [CallerArgumentExpression(nameof(subset))] string subsetExpression = "", [CallerArgumentExpression(nameof(superset))] string supersetExpression = "")
    {
        CheckParameterNotNull(subset, "Assert.IsSubsetOf", "subset");
        CheckParameterNotNull(superset, "Assert.IsSubsetOf", "superset");

        IsSubsetOfImpl(subset.Cast<object?>(), superset.Cast<object?>(), EqualityComparer<object?>.Default, comparerName: null, message, subsetExpression, supersetExpression);
    }

    /// <summary>
    /// Tests whether one collection is a subset of another collection and throws an
    /// exception if any element in the subset is not also in the superset.
    /// </summary>
    /// <param name="subset">
    /// The collection expected to be a subset of <paramref name="superset"/>.
    /// </param>
    /// <param name="superset">
    /// The collection expected to be a superset of <paramref name="subset"/>.
    /// </param>
    /// <param name="comparer">
    /// The equality comparer to use when comparing elements.
    /// </param>
    /// <param name="message">
    /// The message to include in the exception when an element in <paramref name="subset"/>
    /// is not found in <paramref name="superset"/>. The message is shown in test results.
    /// </param>
    /// <param name="subsetExpression">
    /// The syntactic expression of subset as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    /// <param name="supersetExpression">
    /// The syntactic expression of superset as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// Thrown if <paramref name="subset"/> contains at least one element not contained in <paramref name="superset"/>.
    /// </exception>
    public static void IsSubsetOf([NotNull] IEnumerable? subset, [NotNull] IEnumerable? superset, [NotNull] IEqualityComparer? comparer, string? message = "", [CallerArgumentExpression(nameof(subset))] string subsetExpression = "", [CallerArgumentExpression(nameof(superset))] string supersetExpression = "")
    {
        CheckParameterNotNull(subset, "Assert.IsSubsetOf", "subset");
        CheckParameterNotNull(superset, "Assert.IsSubsetOf", "superset");
        CheckParameterNotNull(comparer, "Assert.IsSubsetOf", "comparer");

        IsSubsetOfImpl(subset.Cast<object?>(), superset.Cast<object?>(), new NonGenericEqualityComparerAdapter(comparer), comparer.GetType().Name, message, subsetExpression, supersetExpression);
    }

    #endregion // IsSubsetOf

    #region IsNotSubsetOf

    /// <summary>
    /// Tests whether one collection is not a subset of another collection and throws
    /// an exception if all elements in the subset are also in the superset.
    /// </summary>
    /// <typeparam name="T">The type of the collection items.</typeparam>
    /// <param name="subset">
    /// The collection expected not to be a subset of <paramref name="superset"/>.
    /// </param>
    /// <param name="superset">
    /// The collection expected not to be a superset of <paramref name="subset"/>.
    /// </param>
    /// <param name="message">
    /// The message to include in the exception when every element in <paramref name="subset"/>
    /// is also found in <paramref name="superset"/>. The message is shown in test results.
    /// </param>
    /// <param name="subsetExpression">
    /// The syntactic expression of subset as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    /// <param name="supersetExpression">
    /// The syntactic expression of superset as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// Thrown if all elements of <paramref name="subset"/> are contained in <paramref name="superset"/>.
    /// </exception>
    public static void IsNotSubsetOf<T>([NotNull] IEnumerable<T>? subset, [NotNull] IEnumerable<T>? superset, string? message = "", [CallerArgumentExpression(nameof(subset))] string subsetExpression = "", [CallerArgumentExpression(nameof(superset))] string supersetExpression = "")
    {
        CheckParameterNotNull(subset, "Assert.IsNotSubsetOf", "subset");
        CheckParameterNotNull(superset, "Assert.IsNotSubsetOf", "superset");
        IsNotSubsetOfImpl(subset, superset, EqualityComparer<T>.Default, comparerName: null, message, subsetExpression, supersetExpression);
    }

    /// <summary>
    /// Tests whether one collection is not a subset of another collection and throws
    /// an exception if all elements in the subset are also in the superset.
    /// </summary>
    /// <typeparam name="T">The type of the collection items.</typeparam>
    /// <param name="subset">
    /// The collection expected not to be a subset of <paramref name="superset"/>.
    /// </param>
    /// <param name="superset">
    /// The collection expected not to be a superset of <paramref name="subset"/>.
    /// </param>
    /// <param name="comparer">
    /// The equality comparer to use when comparing elements.
    /// </param>
    /// <param name="message">
    /// The message to include in the exception when every element in <paramref name="subset"/>
    /// is also found in <paramref name="superset"/>. The message is shown in test results.
    /// </param>
    /// <param name="subsetExpression">
    /// The syntactic expression of subset as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    /// <param name="supersetExpression">
    /// The syntactic expression of superset as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// Thrown if all elements of <paramref name="subset"/> are contained in <paramref name="superset"/>.
    /// </exception>
    public static void IsNotSubsetOf<T>([NotNull] IEnumerable<T>? subset, [NotNull] IEnumerable<T>? superset, [NotNull] IEqualityComparer<T>? comparer, string? message = "", [CallerArgumentExpression(nameof(subset))] string subsetExpression = "", [CallerArgumentExpression(nameof(superset))] string supersetExpression = "")
    {
        CheckParameterNotNull(subset, "Assert.IsNotSubsetOf", "subset");
        CheckParameterNotNull(superset, "Assert.IsNotSubsetOf", "superset");
        CheckParameterNotNull(comparer, "Assert.IsNotSubsetOf", "comparer");
        IsNotSubsetOfImpl(subset, superset, comparer, comparer.GetType().Name, message, subsetExpression, supersetExpression);
    }

    /// <summary>
    /// Tests whether one collection is not a subset of another collection and throws
    /// an exception if all elements in the subset are also in the superset.
    /// </summary>
    /// <param name="subset">
    /// The collection expected not to be a subset of <paramref name="superset"/>.
    /// </param>
    /// <param name="superset">
    /// The collection expected not to be a superset of <paramref name="subset"/>.
    /// </param>
    /// <param name="message">
    /// The message to include in the exception when every element in <paramref name="subset"/>
    /// is also found in <paramref name="superset"/>. The message is shown in test results.
    /// </param>
    /// <param name="subsetExpression">
    /// The syntactic expression of subset as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    /// <param name="supersetExpression">
    /// The syntactic expression of superset as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// Thrown if all elements of <paramref name="subset"/> are contained in <paramref name="superset"/>.
    /// </exception>
    public static void IsNotSubsetOf([NotNull] IEnumerable? subset, [NotNull] IEnumerable? superset, string? message = "", [CallerArgumentExpression(nameof(subset))] string subsetExpression = "", [CallerArgumentExpression(nameof(superset))] string supersetExpression = "")
    {
        CheckParameterNotNull(subset, "Assert.IsNotSubsetOf", "subset");
        CheckParameterNotNull(superset, "Assert.IsNotSubsetOf", "superset");

        IsNotSubsetOfImpl(subset.Cast<object?>(), superset.Cast<object?>(), EqualityComparer<object?>.Default, comparerName: null, message, subsetExpression, supersetExpression);
    }

    /// <summary>
    /// Tests whether one collection is not a subset of another collection and throws
    /// an exception if all elements in the subset are also in the superset.
    /// </summary>
    /// <param name="subset">
    /// The collection expected not to be a subset of <paramref name="superset"/>.
    /// </param>
    /// <param name="superset">
    /// The collection expected not to be a superset of <paramref name="subset"/>.
    /// </param>
    /// <param name="comparer">
    /// The equality comparer to use when comparing elements.
    /// </param>
    /// <param name="message">
    /// The message to include in the exception when every element in <paramref name="subset"/>
    /// is also found in <paramref name="superset"/>. The message is shown in test results.
    /// </param>
    /// <param name="subsetExpression">
    /// The syntactic expression of subset as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    /// <param name="supersetExpression">
    /// The syntactic expression of superset as given by the compiler via caller argument expression.
    /// Users shouldn't pass a value for this parameter.
    /// </param>
    /// <exception cref="AssertFailedException">
    /// Thrown if all elements of <paramref name="subset"/> are contained in <paramref name="superset"/>.
    /// </exception>
    public static void IsNotSubsetOf([NotNull] IEnumerable? subset, [NotNull] IEnumerable? superset, [NotNull] IEqualityComparer? comparer, string? message = "", [CallerArgumentExpression(nameof(subset))] string subsetExpression = "", [CallerArgumentExpression(nameof(superset))] string supersetExpression = "")
    {
        CheckParameterNotNull(subset, "Assert.IsNotSubsetOf", "subset");
        CheckParameterNotNull(superset, "Assert.IsNotSubsetOf", "superset");
        CheckParameterNotNull(comparer, "Assert.IsNotSubsetOf", "comparer");

        IsNotSubsetOfImpl(subset.Cast<object?>(), superset.Cast<object?>(), new NonGenericEqualityComparerAdapter(comparer), comparer.GetType().Name, message, subsetExpression, supersetExpression);
    }

    #endregion // IsNotSubsetOf

    private static void IsSubsetOfImpl<T>(IEnumerable<T?> subset, IEnumerable<T?> superset, IEqualityComparer<T> comparer, string? comparerName, string? message, string subsetExpression, string supersetExpression)
    {
        // Snapshot once so we don't enumerate twice (counting + rendering on failure)
        // and so lazy/single-pass enumerables behave deterministically.
        List<T?> subsetList = subset is List<T?> sl ? sl : [.. subset];
        List<T?> supersetList = superset is List<T?> spl ? spl : [.. superset];

        if (TryFindMissingElements(subsetList, supersetList, comparer, out List<T?>? missing))
        {
            ReportAssertIsSubsetOfFailed(subsetList, supersetList, missing, comparerName, message, subsetExpression, supersetExpression);
        }
    }

    private static void IsNotSubsetOfImpl<T>(IEnumerable<T?> subset, IEnumerable<T?> superset, IEqualityComparer<T> comparer, string? comparerName, string? message, string subsetExpression, string supersetExpression)
    {
        List<T?> subsetList = subset is List<T?> sl ? sl : [.. subset];
        List<T?> supersetList = superset is List<T?> spl ? spl : [.. superset];

        if (!TryFindMissingElements(subsetList, supersetList, comparer, out _))
        {
            ReportAssertIsNotSubsetOfFailed(subsetList, supersetList, comparerName, message, subsetExpression, supersetExpression);
        }
    }

    /// <summary>
    /// Determines whether <paramref name="subset"/> contains any element not present
    /// (with sufficient multiplicity) in <paramref name="superset"/>.
    /// </summary>
    /// <returns>
    /// <see langword="true"/> if at least one element is missing — in which case <paramref name="missing"/>
    /// holds the excess elements (in their first-seen order in <paramref name="subset"/>) — and
    /// <see langword="false"/> when every element of <paramref name="subset"/> is matched in
    /// <paramref name="superset"/>.
    /// </returns>
    private static bool TryFindMissingElements<T>(IEnumerable<T?> subset, IEnumerable<T?> superset, IEqualityComparer<T> comparer, [NotNullWhen(true)] out List<T?>? missing)
    {
#pragma warning disable CS8714 // The type cannot be used as type parameter in the generic type or method. Nullability of type argument doesn't match 'notnull' constraint.
        Dictionary<T, int> supersetCounts = CountElements(superset, comparer, out int supersetNulls);
#pragma warning restore CS8714

        missing = null;

        // Walk the subset in source order so excess elements appear in first-seen positional order
        // (with multiplicity preserved). For each element, decrement its remaining quota in the superset;
        // when the quota reaches zero, additional occurrences are reported as missing.
        foreach (T? element in subset)
        {
            if (element is null)
            {
                if (supersetNulls > 0)
                {
                    supersetNulls--;
                }
                else
                {
                    missing ??= [];
                    missing.Add(default);
                }

                continue;
            }

            if (supersetCounts.TryGetValue(element, out int remaining) && remaining > 0)
            {
                supersetCounts[element] = remaining - 1;
            }
            else
            {
                missing ??= [];
                missing.Add(element);
            }
        }

        return missing is not null;
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
    private static void ReportAssertIsSubsetOfFailed<T>(IEnumerable<T?> subset, IEnumerable<T?> superset, List<T?> missing, string? comparerName, string? message, string subsetExpression, string supersetExpression)
    {
        string subsetText = AssertionValueRenderer.RenderValue(subset);
        string supersetText = AssertionValueRenderer.RenderValue(superset);
        string missingText = AssertionValueRenderer.RenderValue(missing);

        EvidenceBlock evidence = EvidenceBlock.Create()
            .AddLine("missing:", missingText)
            .AddLine("subset:", subsetText)
            .AddLine("superset:", supersetText);

        if (comparerName is not null)
        {
            evidence.AddLine("comparer:", comparerName);
        }

        StructuredAssertionMessage structured = new(FrameworkMessages.IsSubsetOfFailedSummary);
        structured.WithUserMessage(message);
        structured.WithEvidence(evidence);
        structured.WithExpectedAndActual(supersetText, subsetText);
        structured.WithCallSiteExpression(BuildCallSiteWithComparer("Assert.IsSubsetOf", subsetExpression, supersetExpression, comparerName is not null));

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
        structured.WithExpectedAndActual(expectedText: null, actualText: subsetText);
        structured.WithCallSiteExpression(BuildCallSiteWithComparer("Assert.IsNotSubsetOf", subsetExpression, supersetExpression, comparerName is not null));

        ReportAssertFailed(structured);
    }

    private static string? BuildCallSiteWithComparer(string assertionMethodName, string subsetExpression, string supersetExpression, bool hasComparer)
        => hasComparer
            ? FormatCallSiteExpression(assertionMethodName, subsetExpression, supersetExpression, expression3: string.Empty, "<subset>", "<superset>", "<comparer>")
            : FormatCallSiteExpression(assertionMethodName, subsetExpression, supersetExpression, "<subset>", "<superset>");

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
