// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using AwesomeAssertions;

using TestFramework.ForTestingMSTest;

namespace Microsoft.VisualStudio.TestPlatform.TestFramework.UnitTests.Attributes;

public class CombinatorialGenerationTests : TestContainer
{
    public void GenerateCombinationsReturnsCartesianProductInStableOrder()
    {
        int[][] rows = CombinatorialTestCaseGenerator.GenerateCombinations([2, 3]);

        AssertRows(
            rows,
            [
                [0, 0],
                [0, 1],
                [0, 2],
                [1, 0],
                [1, 1],
                [1, 2],
            ]);
    }

    public void GenerateCombinationsAppliesPredicate()
    {
        int[][] rows = CombinatorialTestCaseGenerator.GenerateCombinations([3, 3], indices => indices[0] < indices[1]);

        AssertRows(rows, [[0, 1], [0, 2], [1, 2]]);
    }

    public void GenerateCombinationsIsolatesPredicateFromTraversalState()
    {
        int[][] rows = CombinatorialTestCaseGenerator.GenerateCombinations(
            [2, 2],
            indices =>
            {
                indices[0] = 42;
                return true;
            });

        AssertRows(rows, [[0, 0], [0, 1], [1, 0], [1, 1]]);
    }

    public void GenerateCombinationsHandlesEmptyAndInvalidDimensions()
    {
        CombinatorialTestCaseGenerator.GenerateCombinations([]).Should().BeEmpty();
        CombinatorialTestCaseGenerator.GenerateCombinations([2, 0, 3]).Should().BeEmpty();
        Action action = () => CombinatorialTestCaseGenerator.GenerateCombinations([2, -1]);
        action.Should().Throw<ArgumentOutOfRangeException>();
    }

    private static void AssertRows<T>(T[][] actual, T[][] expected)
    {
        actual.Should().HaveCount(expected.Length);
        for (int i = 0; i < expected.Length; i++)
        {
            actual[i].Should().Equal(expected[i]);
        }
    }
}
