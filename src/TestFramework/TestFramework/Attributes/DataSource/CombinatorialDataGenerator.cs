// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestTools.UnitTesting.Combinatorial;

/// <summary>
/// Generates exhaustive combinations of values for use with data-driven tests.
/// </summary>
public static class CombinatorialDataGenerator
{
    /// <summary>
    /// Generates the Cartesian product of the supplied dimensions.
    /// </summary>
    /// <param name="dimensions">The candidate values for each position in a generated row.</param>
    /// <returns>Every possible combination, with one value selected from each dimension.</returns>
    /// <remarks>
    /// The returned rows can be filtered or projected with LINQ and supplied to a test through
    /// <see cref="DynamicDataAttribute"/>.
    /// </remarks>
    public static IEnumerable<object?[]> Generate(params object?[][] dimensions)
    {
        if (dimensions is null)
        {
            throw new ArgumentNullException(nameof(dimensions));
        }

        object?[][] candidateValues = new object?[dimensions.Length][];
        int[] dimensionSizes = new int[dimensions.Length];
        for (int i = 0; i < dimensions.Length; i++)
        {
            object?[] dimension = dimensions[i]
                ?? throw new ArgumentException(
                    string.Format(CultureInfo.CurrentCulture, FrameworkMessages.CombinatorialDimensionCannotBeNull, i),
                    nameof(dimensions));
            candidateValues[i] = [.. dimension];
            dimensionSizes[i] = dimension.Length;
        }

        int[][] testCases = CombinatorialTestCaseGenerator.GenerateCombinations(dimensionSizes);
        return testCases.Select(indices =>
            indices.Select((valueIndex, dimensionIndex) => candidateValues[dimensionIndex][valueIndex])
                .ToArray());
    }
}
