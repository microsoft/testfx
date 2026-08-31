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

    public void GeneratePermutationsReturnsEveryPositionalPermutation()
    {
        AssertRows(
            CombinatorialTestCaseGenerator.GeneratePermutations([1, 2, 3]),
            [
                [1, 2, 3],
                [1, 3, 2],
                [2, 1, 3],
                [2, 3, 1],
                [3, 1, 2],
                [3, 2, 1],
            ]);
        CombinatorialTestCaseGenerator.GeneratePermutations([1, 1, 2]).Should().HaveCount(6);
        CombinatorialTestCaseGenerator.GeneratePermutations<int>([]).Should().BeEmpty();
    }

    public void BuilderExpandsBaseRowsGeneratedColumnsAndExplicitCases()
    {
        IReadOnlyCollection<object?[]> rows = new CombinatorialTheoryDataBuilder()
            .AddRows([10, 0], [5, 2])
            .AddValues(true, false)
            .AddTestCase(6, 2, false)
            .BuildCombinations();

        AssertRows(
            rows.ToArray(),
            [
                [10, 0, true],
                [10, 0, false],
                [5, 2, true],
                [5, 2, false],
                [6, 2, false],
            ]);
    }

    public void BuilderAppliesConstraintsOnlyToGeneratedRows()
    {
        IReadOnlyCollection<object?[]> rows = new CombinatorialTheoryDataBuilder()
            .AddRows([10, 0], [5, 2])
            .AddValues(true, false)
            .Where(row => (int)row[0]! > 5)
            .Where(row => (bool)row[2]!)
            .AddTestCase(1, 2, false)
            .BuildCombinations();

        AssertRows(rows.ToArray(), [[10, 0, true], [1, 2, false]]);
    }

    public void BuilderIsolatesPredicatesFromEachOther()
    {
        IReadOnlyCollection<object?[]> rows = new CombinatorialTheoryDataBuilder()
            .AddValues(1, 2)
            .Where(row =>
            {
                row[0] = 42;
                return true;
            })
            .Where(row => (int)row[0]! < 3)
            .BuildCombinations();

        AssertRows(rows.ToArray(), [[1], [2]]);
    }

    public void BuilderSnapshotsInputs()
    {
        object?[] baseRow = [1, 2];
        int[] values = [3, 4];
        CombinatorialTheoryDataBuilder builder = new CombinatorialTheoryDataBuilder()
            .AddRows(baseRow)
            .AddValues(values);

        baseRow[0] = 99;
        values[0] = 99;

        AssertRows(builder.BuildCombinations().ToArray(), [[1, 2, 3], [1, 2, 4]]);
    }

    public void BuilderValidatesConfiguration()
    {
        Action rowsAfterValues = () => new CombinatorialTheoryDataBuilder().AddValues(1).AddRows([2]);
        Action inconsistentRows = () => new CombinatorialTheoryDataBuilder().AddRows([1, 2], [3]);
        Action emptyValues = () => new CombinatorialTheoryDataBuilder().AddValues(Array.Empty<int>());
        Action incorrectExplicitWidth = () => new CombinatorialTheoryDataBuilder().AddValues(1).AddTestCase(1, 2).BuildCombinations();

        rowsAfterValues.Should().Throw<InvalidOperationException>();
        inconsistentRows.Should().Throw<ArgumentException>();
        emptyValues.Should().Throw<ArgumentException>();
        incorrectExplicitWidth.Should().Throw<InvalidOperationException>();
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
