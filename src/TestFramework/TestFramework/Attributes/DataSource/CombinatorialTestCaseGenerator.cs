// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestTools.UnitTesting;

/// <summary>
/// Determines whether a generated test case, represented by one selected value index per dimension, is allowed.
/// </summary>
/// <param name="indices">The selected zero-based value index for each dimension.</param>
/// <returns><see langword="true"/> if the test case is allowed; otherwise, <see langword="false"/>.</returns>
public delegate bool CombinatorialIndexPredicate(int[] indices);

/// <summary>
/// Generates exhaustive combinations and permutations.
/// </summary>
public static class CombinatorialTestCaseGenerator
{
    /// <summary>
    /// Generates every possible combination of value indices across the specified dimensions.
    /// </summary>
    /// <param name="dimensionSizes">The number of candidate values in each dimension.</param>
    /// <param name="isTestCaseAllowed">An optional predicate that rejects test cases.</param>
    /// <returns>One test case per array, with one selected value index per dimension.</returns>
    public static int[][] GenerateCombinations(
        int[] dimensionSizes,
        CombinatorialIndexPredicate? isTestCaseAllowed = null)
    {
        int[] dimensions = ValidateAndCopyDimensions(dimensionSizes);
        if (dimensions.Length == 0 || dimensions.Contains(0))
        {
            return [];
        }

        List<int[]> results = [];
        int[] current = new int[dimensions.Length];
        FillCombinations(dimensions, current, 0, isTestCaseAllowed, results);
        return results.ToArray();
    }

    /// <summary>
    /// Generates every permutation of the supplied values.
    /// </summary>
    /// <typeparam name="T">The type of value to permute.</typeparam>
    /// <param name="values">The values to permute.</param>
    /// <returns>Every positional permutation of <paramref name="values"/>.</returns>
    /// <remarks>Input positions are distinct, so equal input values may produce equal output rows.</remarks>
    public static T[][] GeneratePermutations<T>(T[] values)
    {
        if (values.Length == 0)
        {
            return [];
        }

        T[] valueCopy = [.. values];
        List<T[]> results = [];
        FillPermutations(valueCopy, new T[valueCopy.Length], new bool[valueCopy.Length], 0, results);
        return results.ToArray();
    }

    private static void FillPermutations<T>(T[] values, T[] current, bool[] usedIndices, int outputIndex, List<T[]> results)
    {
        if (outputIndex == current.Length)
        {
            results.Add([.. current]);
            return;
        }

        for (int valueIndex = 0; valueIndex < values.Length; valueIndex++)
        {
            if (usedIndices[valueIndex])
            {
                continue;
            }

            usedIndices[valueIndex] = true;
            current[outputIndex] = values[valueIndex];
            FillPermutations(values, current, usedIndices, outputIndex + 1, results);
            usedIndices[valueIndex] = false;
        }
    }

    private static void FillCombinations(
        int[] dimensions,
        int[] current,
        int dimension,
        CombinatorialIndexPredicate? isTestCaseAllowed,
        List<int[]> results)
    {
        for (int valueIndex = 0; valueIndex < dimensions[dimension]; valueIndex++)
        {
            current[dimension] = valueIndex;
            if (dimension + 1 < dimensions.Length)
            {
                FillCombinations(dimensions, current, dimension + 1, isTestCaseAllowed, results);
            }
            else if (isTestCaseAllowed?.Invoke([.. current]) ?? true)
            {
                results.Add([.. current]);
            }
        }
    }

    private static int[] ValidateAndCopyDimensions(int[] dimensionSizes)
    {
        if (dimensionSizes is null)
        {
            throw new ArgumentNullException(nameof(dimensionSizes));
        }

        int[] dimensions = [.. dimensionSizes];
        for (int i = 0; i < dimensions.Length; i++)
        {
            if (dimensions[i] < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(dimensionSizes), dimensions[i], "Dimension sizes cannot be negative.");
            }
        }

        return dimensions;
    }
}
