// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// Determines whether a generated combinatorial data row is allowed.
/// </summary>
/// <param name="values">The values in the completed data row.</param>
/// <returns><see langword="true"/> if the row is allowed; otherwise, <see langword="false"/>.</returns>
public delegate bool CombinatorialTheoryDataPredicate(object?[] values);

/// <summary>
/// Builds test data from hand-authored base rows and generated columns.
/// </summary>
public sealed class CombinatorialTheoryDataBuilder
{
    private readonly List<object?[]> _baseRows = [];
    private readonly List<object?[]> _generatedColumns = [];
    private readonly List<object?[]> _explicitTestCases = [];
    private readonly List<CombinatorialTheoryDataPredicate> _predicates = [];
    private int? _baseColumnCount;

    /// <summary>
    /// Adds complete hand-authored rows for the base columns.
    /// </summary>
    /// <param name="rows">The rows to add. Every row must have the same width.</param>
    /// <returns>This builder.</returns>
    public CombinatorialTheoryDataBuilder AddRows(params object?[][] rows)
    {
        if (rows.Length == 0)
        {
            throw new ArgumentException("At least one row is required.", nameof(rows));
        }

        EnsureBaseRowsMayBeAdded();
        for (int i = 0; i < rows.Length; i++)
        {
            if (rows[i] is null)
            {
                throw new ArgumentException("Base rows cannot be null.", nameof(rows));
            }

            AddBaseRow(rows[i]);
        }

        return this;
    }

    /// <summary>
    /// Adds complete hand-authored rows for the base columns.
    /// </summary>
    /// <param name="rows">The rows to add. Every row must have the same width.</param>
    /// <returns>This builder.</returns>
    public CombinatorialTheoryDataBuilder AddRows(IEnumerable<IReadOnlyList<object?>> rows)
    {
        if (rows is null)
        {
            throw new ArgumentNullException(nameof(rows));
        }

        EnsureBaseRowsMayBeAdded();
        bool any = false;
        foreach (IReadOnlyList<object?> row in rows)
        {
            if (row is null)
            {
                throw new ArgumentException("Base rows cannot be null.", nameof(rows));
            }

            any = true;
            object?[] rowCopy = new object?[row.Count];
            for (int i = 0; i < row.Count; i++)
            {
                rowCopy[i] = row[i];
            }

            AddBaseRow(rowCopy);
        }

        return !any
            ? throw new ArgumentException("At least one row is required.", nameof(rows))
            : this;
    }

    /// <summary>
    /// Adds a generated column with the specified candidate values.
    /// </summary>
    /// <typeparam name="T">The type of values in the column.</typeparam>
    /// <param name="values">The candidate values for the column.</param>
    /// <returns>This builder.</returns>
    public CombinatorialTheoryDataBuilder AddValues<T>(params T[] values)
    {
        if (values.Length == 0)
        {
            throw new ArgumentException("At least one value is required.", nameof(values));
        }

        object?[] valuesCopy = new object?[values.Length];
        for (int i = 0; i < values.Length; i++)
        {
            valuesCopy[i] = values[i];
        }

        _generatedColumns.Add(valuesCopy);
        return this;
    }

    /// <summary>
    /// Adds a generated column with the specified candidate values.
    /// </summary>
    /// <typeparam name="T">The type of values in the column.</typeparam>
    /// <param name="values">The candidate values for the column.</param>
    /// <returns>This builder.</returns>
    public CombinatorialTheoryDataBuilder AddValues<T>(IEnumerable<T> values)
    {
        if (values is null)
        {
            throw new ArgumentNullException(nameof(values));
        }

        object?[] valuesCopy = values.Cast<object?>().ToArray();
        if (valuesCopy.Length == 0)
        {
            throw new ArgumentException("At least one value is required.", nameof(values));
        }

        _generatedColumns.Add(valuesCopy);
        return this;
    }

    /// <summary>
    /// Adds a complete hand-authored test case after the generated rows.
    /// </summary>
    /// <param name="values">The values in the test case.</param>
    /// <returns>This builder.</returns>
    public CombinatorialTheoryDataBuilder AddTestCase(params object?[] values)
    {
        _explicitTestCases.Add(values.ToArray());
        return this;
    }

    /// <summary>
    /// Adds a constraint that generated test cases must satisfy.
    /// </summary>
    /// <param name="isTestCaseAllowed">The predicate to evaluate against completed generated rows.</param>
    /// <returns>This builder.</returns>
    public CombinatorialTheoryDataBuilder Where(CombinatorialTheoryDataPredicate isTestCaseAllowed)
    {
        if (isTestCaseAllowed is null)
        {
            throw new ArgumentNullException(nameof(isTestCaseAllowed));
        }

        _predicates.Add(isTestCaseAllowed);
        return this;
    }

    /// <summary>
    /// Builds every possible combination of the configured base rows and generated columns.
    /// </summary>
    /// <returns>The generated rows followed by explicitly added test cases.</returns>
    public IReadOnlyCollection<object?[]> BuildCombinations()
    {
        int totalColumns = (_baseColumnCount ?? 0) + _generatedColumns.Count;
        foreach (object?[] testCase in _explicitTestCases)
        {
            if (testCase.Length != totalColumns)
            {
                throw new InvalidOperationException(
                    $"Expected explicit test cases to have {totalColumns} values, but found {testCase.Length}.");
            }
        }

        List<object?[]> dimensions = [];
        if (_baseRows.Count > 0)
        {
            dimensions.Add(_baseRows.Cast<object?>().ToArray());
        }

        dimensions.AddRange(_generatedColumns);
        int[] dimensionSizes = dimensions.Select(dimension => dimension.Length).ToArray();
        CombinatorialIndexPredicate? indexPredicate = _predicates.Count == 0
            ? null
            : indices => IsAllowed(dimensions, indices);
        int[][] selections = CombinatorialTestCaseGenerator.GenerateCombinations(dimensionSizes, indexPredicate);

        var results = new List<object?[]>(selections.Length + _explicitTestCases.Count);
        foreach (int[] selection in selections)
        {
            results.Add(Flatten(dimensions, selection));
        }

        foreach (object?[] testCase in _explicitTestCases)
        {
            results.Add([.. testCase]);
        }

        return results;
    }

    private void AddBaseRow(object?[] row)
    {
        if (_baseColumnCount is int expectedWidth && row.Length != expectedWidth)
        {
            throw new ArgumentException($"Expected a base row with {expectedWidth} values, but found {row.Length}.", nameof(row));
        }

        _baseColumnCount ??= row.Length;
        _baseRows.Add([.. row]);
    }

    private void EnsureBaseRowsMayBeAdded()
    {
        if (_generatedColumns.Count > 0)
        {
            throw new InvalidOperationException($"{nameof(AddRows)} must be called before {nameof(AddValues)}.");
        }
    }

    private object?[] Flatten(List<object?[]> dimensions, int[] indices)
    {
        int outputLength = (_baseColumnCount ?? 0) + _generatedColumns.Count;
        object?[] values = new object?[outputLength];
        int outputIndex = 0;
        int dimensionIndex = 0;

        if (_baseRows.Count > 0)
        {
            object?[] baseRow = _baseRows[indices[dimensionIndex]];
            Array.Copy(baseRow, 0, values, 0, baseRow.Length);
            outputIndex += baseRow.Length;
            dimensionIndex++;
        }

        for (; dimensionIndex < dimensions.Count; dimensionIndex++)
        {
            values[outputIndex++] = dimensions[dimensionIndex][indices[dimensionIndex]];
        }

        return values;
    }

    private bool IsAllowed(List<object?[]> dimensions, int[] indices)
    {
        object?[] values = Flatten(dimensions, indices);
        foreach (CombinatorialTheoryDataPredicate predicate in _predicates)
        {
            if (!predicate([.. values]))
            {
                return false;
            }
        }

        return true;
    }
}
