// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using AwesomeAssertions;

using Microsoft.VisualStudio.TestTools.UnitTesting.Combinatorial;

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

    public void PublicGeneratorReturnsValuesAndSupportsLinqConstraints()
    {
        object?[][] rows = CombinatorialDataGenerator.Generate([1, 2, 3], [1, 2, 3])
            .Where(row => (int)row[0]! < (int)row[1]!)
            .ToArray();

        AssertRows(rows, [[1, 2], [1, 3], [2, 3]]);
    }

    public void PublicGeneratorSnapshotsDimensions()
    {
        object?[] first = [1, 2];
        IEnumerable<object?[]> generatedRows = CombinatorialDataGenerator.Generate(first, ["a"]);

        first[0] = 42;

        AssertRows(generatedRows.ToArray(), [[1, "a"], [2, "a"]]);
    }

    public void GenerateCombinationsHandlesEmptyAndInvalidDimensions()
    {
        CombinatorialTestCaseGenerator.GenerateCombinations([]).Should().BeEmpty();
        CombinatorialTestCaseGenerator.GenerateCombinations([2, 0, 3]).Should().BeEmpty();
        Action action = () => CombinatorialTestCaseGenerator.GenerateCombinations([2, -1]);
        action.Should().Throw<ArgumentOutOfRangeException>();
    }

    public void PublicGeneratorRejectsNullDimensions()
    {
        Action nullDimensions = () => CombinatorialDataGenerator.Generate(null!);
        Action nullDimension = () => CombinatorialDataGenerator.Generate([1], null!);

        nullDimensions.Should().Throw<ArgumentNullException>();
        nullDimension.Should().Throw<ArgumentException>().WithMessage("*position 1*");
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
