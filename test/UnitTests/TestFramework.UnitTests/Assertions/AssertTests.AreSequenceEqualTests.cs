// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections;

using AwesomeAssertions;
using TestFramework.ForTestingMSTest;

namespace Microsoft.VisualStudio.TestPlatform.TestFramework.UnitTests;

public partial class AssertTests : TestContainer
{
    public void AreSequenceEqual_InOrder_IdenticalSequences_Passes()
        => Assert.AreSequenceEqual(new[] { 1, 2, 3 }, new[] { 1, 2, 3 });

    public void AreSequenceEqual_InOrder_DifferentLengths_Fails()
    {
        int[] expected = [1, 2, 3];
        int[] actual = [1, 2];

        Action action = () => Assert.AreSequenceEqual(expected, actual);
        action.Should().Throw<AssertFailedException>()
            .Which.Message.Should().Be(
                """
                Assertion failed. Expected sequences to be equal.
                Sequences have different lengths (expected: 3, actual: 2).

                expected: [1, 2, 3]
                actual:   [1, 2]

                Assert.AreSequenceEqual(expected, actual)
                """);
    }

    public void AreSequenceEqual_InOrder_SameLengthDifferentElements_FailsWithFirstDifferenceIndex()
    {
        int[] expected = [1, 2, 3, 4];
        int[] actual = [1, 9, 3, 8];

        Action action = () => Assert.AreSequenceEqual(expected, actual);
        action.Should().Throw<AssertFailedException>()
            .Which.Message.Should().Be(
                """
                Assertion failed. Expected sequences to be equal.
                Sequences have 4 element(s). 2 element(s) differ. First difference at index 1.

                expected: [1, 2, 3, 4]
                actual:   [1, 9, 3, 8]

                Assert.AreSequenceEqual(expected, actual)
                """);
    }

    public void AreSequenceEqual_InOrder_BothNull_Passes()
        => Assert.AreSequenceEqual<string>(null, null);

    public void AreSequenceEqual_InOrder_OneNull_Fails()
    {
        string[] actual = ["a"];

        Action action = () => Assert.AreSequenceEqual<string>(null, actual);
        action.Should().Throw<AssertFailedException>()
            .WithMessage("*Expected sequences to be equal.*one side is null, the other is not.*expected: null*actual:   [\"a\"]*Assert.AreSequenceEqual(null, actual)*");
    }

    public void AreSequenceEqual_InOrder_BothEmpty_Passes()
        => Assert.AreSequenceEqual(Array.Empty<int>(), Array.Empty<int>());

    public void AreSequenceEqual_InOrder_OneEmpty_Fails()
    {
        Action action = () => Assert.AreSequenceEqual(Array.Empty<int>(), new[] { 1 });
        action.Should().Throw<AssertFailedException>()
            .WithMessage("*different lengths (expected: 0, actual: 1)*");
    }

    public void AreSequenceEqual_InOrder_CustomComparer_PassesAndReportsComparerOnFailure()
    {
        Assert.AreSequenceEqual(["a", "B"], ["A", "b"], StringComparer.OrdinalIgnoreCase);

        string[] expected = ["a"];
        string[] actual = ["b"];

        Action action = () => Assert.AreSequenceEqual(expected, actual, StringComparer.OrdinalIgnoreCase);
        action.Should().Throw<AssertFailedException>()
            .Which.Message.Should().Be(
                """
                Assertion failed. Expected sequences to be equal.
                Sequences have 1 element(s). 1 element(s) differ. First difference at index 0.

                expected: ["a"]
                actual:   ["b"]
                comparer: OrdinalIgnoreCaseComparer

                Assert.AreSequenceEqual(expected, actual, <comparer>)
                """);
    }

    public void AreSequenceEqual_InAnyOrder_SameMultisetDifferentOrder_Passes()
        => Assert.AreSequenceEqual([1, 2, 2, 3], [3, 2, 1, 2], SequenceOrder.InAnyOrder);

    public void AreSequenceEqual_InAnyOrder_DifferentMultiplicity_Fails()
    {
        int[] expected = [1, 1, 2];
        int[] actual = [1, 2, 2];

        Action action = () => Assert.AreSequenceEqual(expected, actual, SequenceOrder.InAnyOrder);
        action.Should().Throw<AssertFailedException>()
            .Which.Message.Should().Be(
                """
                Assertion failed. Expected sequences to be equal (in any order).
                Missing 1 element(s) from actual. Found 1 unexpected element(s).

                missing:    [1]
                unexpected: [2]

                Assert.AreSequenceEqual(expected, actual, <order>)
                """);
    }

    public void AreSequenceEqual_InAnyOrder_MissingOnly_Fails()
    {
        Action action = () => Assert.AreSequenceEqual([1, 2], [1], SequenceOrder.InAnyOrder);
        action.Should().Throw<AssertFailedException>()
            .WithMessage("*Missing 1 element(s) from actual. Found 0 unexpected element(s).*missing:    [2]*unexpected: []*");
    }

    public void AreSequenceEqual_InAnyOrder_UnexpectedOnly_Fails()
    {
        Action action = () => Assert.AreSequenceEqual([1], [1, 2], SequenceOrder.InAnyOrder);
        action.Should().Throw<AssertFailedException>()
            .WithMessage("*Missing 0 element(s) from actual. Found 1 unexpected element(s).*missing:    []*unexpected: [2]*");
    }

    public void AreSequenceEqual_InAnyOrder_BothNull_Passes()
        => Assert.AreSequenceEqual<string>(null, null, SequenceOrder.InAnyOrder);

    public void AreSequenceEqual_InAnyOrder_OneNull_Fails()
    {
        Action action = () => Assert.AreSequenceEqual<string>(["a"], null, SequenceOrder.InAnyOrder);
        action.Should().Throw<AssertFailedException>()
            .WithMessage("*Expected sequences to be equal (in any order).*one side is null, the other is not.*");
    }

    public void AreSequenceEqual_InAnyOrder_BothEmpty_Passes()
        => Assert.AreSequenceEqual(Array.Empty<int>(), Array.Empty<int>(), SequenceOrder.InAnyOrder);

    public void AreSequenceEqual_InAnyOrder_OneEmpty_Fails()
    {
        Action action = () => Assert.AreSequenceEqual(Array.Empty<int>(), new[] { 1 }, SequenceOrder.InAnyOrder);
        action.Should().Throw<AssertFailedException>()
            .WithMessage("*Missing 0 element(s) from actual. Found 1 unexpected element(s).*unexpected: [1]*");
    }

    public void AreSequenceEqual_InAnyOrder_CustomComparer_Passes()
        => Assert.AreSequenceEqual(["a", "B"], ["b", "A"], StringComparer.OrdinalIgnoreCase, SequenceOrder.InAnyOrder);

    public void AreSequenceEqual_NonGeneric_InOrder_Parity()
    {
        IEnumerable expected = new ArrayList { "a", "b" };
        IEnumerable actual = new ArrayList { "a", "c" };

        Action action = () => Assert.AreSequenceEqual(expected, actual);
        action.Should().Throw<AssertFailedException>()
            .WithMessage("*First difference at index 1*Assert.AreSequenceEqual(expected, actual)*");
    }

    public void AreSequenceEqual_NonGeneric_InAnyOrder_Parity()
    {
        IEnumerable expected = new ArrayList { "a", "b" };
        IEnumerable actual = new ArrayList { "b", "c" };

        Action action = () => Assert.AreSequenceEqual(expected, actual, SequenceOrder.InAnyOrder);
        action.Should().Throw<AssertFailedException>()
            .WithMessage("*missing:    [\"a\"]*unexpected: [\"c\"]*");
    }

    public void AreSequenceEqual_NonGeneric_CustomComparer_Parity()
        => Assert.AreSequenceEqual(new ArrayList { "a", "B" }, new ArrayList { "A", "b" }, new CaseInsensitiveNonGenericComparer(), SequenceOrder.InAnyOrder);

    public void AreNotSequenceEqual_IdenticalSequences_Fails()
    {
        int[] notExpected = [1, 2, 3];
        int[] actual = [1, 2, 3];

        Action action = () => Assert.AreNotSequenceEqual(notExpected, actual);
        action.Should().Throw<AssertFailedException>()
            .Which.Message.Should().Be(
                """
                Assertion failed. Expected sequences to differ.

                Assert.AreNotSequenceEqual(notExpected, actual)
                """);
    }

    public void AreNotSequenceEqual_DifferentSequences_Passes()
        => Assert.AreNotSequenceEqual([1, 2, 3], [1, 4, 3]);

    public void AreNotSequenceEqual_InAnyOrder_IdenticalMultisets_Fails()
    {
        int[] notExpected = [1, 2, 2];
        int[] actual = [2, 1, 2];

        Action action = () => Assert.AreNotSequenceEqual(notExpected, actual, SequenceOrder.InAnyOrder);
        action.Should().Throw<AssertFailedException>()
            .Which.Message.Should().Be(
                """
                Assertion failed. Expected sequences to differ (in any order).

                Assert.AreNotSequenceEqual(notExpected, actual, <order>)
                """);
    }

    public void AreNotSequenceEqual_InAnyOrder_DifferentMultisets_Passes()
        => Assert.AreNotSequenceEqual([1, 2, 2], [1, 2, 3], SequenceOrder.InAnyOrder);

    public void AreNotSequenceEqual_BothNull_Fails()
    {
        Action action = () => Assert.AreNotSequenceEqual<string>(null, null);
        action.Should().Throw<AssertFailedException>()
            .WithMessage("Assertion failed. Expected sequences to differ.*");
    }

    public void AreNotSequenceEqual_OneNull_Passes()
        => Assert.AreNotSequenceEqual(new[] { 1 }, null);

    public void AreSequenceEqual_InvalidOrder_Throws()
    {
        Action action = () => Assert.AreSequenceEqual([1], [1], (SequenceOrder)999);
        action.Should().Throw<ArgumentOutOfRangeException>();
    }

    public void AreSequenceEqual_InAnyOrder_CustomComparerFailure_ReportsComparer()
    {
        string[] expected = ["a"];
        string[] actual = ["c"];

        Action action = () => Assert.AreSequenceEqual(expected, actual, StringComparer.OrdinalIgnoreCase, SequenceOrder.InAnyOrder);
        action.Should().Throw<AssertFailedException>()
            .Which.Message.Should().Be(
                """
                Assertion failed. Expected sequences to be equal (in any order).
                Missing 1 element(s) from actual. Found 1 unexpected element(s).

                missing:    ["a"]
                unexpected: ["c"]
                comparer:   OrdinalIgnoreCaseComparer

                Assert.AreSequenceEqual(expected, actual, <comparer>, <order>)
                """);
    }

    public void AreSequenceEqual_InOrder_EnumeratesEachSequenceOnlyOnce()
    {
        var expected = new SingleEnumerationEnumerable<int>([1, 2, 3]);
        var actual = new SingleEnumerationEnumerable<int>([1, 2, 3]);

        Assert.AreSequenceEqual(expected, actual);
        expected.EnumerationCount.Should().Be(1);
        actual.EnumerationCount.Should().Be(1);
    }

    public void AreSequenceEqual_InAnyOrder_EnumeratesEachSequenceOnlyOnce()
    {
        var expected = new SingleEnumerationEnumerable<int>([1, 2, 3]);
        var actual = new SingleEnumerationEnumerable<int>([3, 2, 1]);

        Assert.AreSequenceEqual(expected, actual, SequenceOrder.InAnyOrder);
        expected.EnumerationCount.Should().Be(1);
        actual.EnumerationCount.Should().Be(1);
    }

    public void AreSequenceEqual_DefaultComparerForReferenceTypesWithoutEqualsOverride_UsesReferenceEquality()
    {
        Action action = () => Assert.AreSequenceEqual([new PlainReferenceType(1)], [new PlainReferenceType(1)]);
        action.Should().Throw<AssertFailedException>();
    }

    public void AreSequenceEqual_RecordElements_UseValueEquality()
        => Assert.AreSequenceEqual([new ValueRecord(1), new ValueRecord(2)], [new ValueRecord(1), new ValueRecord(2)]);

    public void AreSequenceEqual_DoubleNaN_UsesDefaultEqualityComparer()
        => Assert.AreSequenceEqual([double.NaN], [double.NaN]);

    private sealed class SingleEnumerationEnumerable<T>(IEnumerable<T> items) : IEnumerable<T>
    {
        private readonly IEnumerable<T> _items = items;

        public int EnumerationCount { get; private set; }

        public IEnumerator<T> GetEnumerator()
        {
            EnumerationCount++;
            if (EnumerationCount > 1)
            {
                throw new InvalidOperationException("Sequence was enumerated more than once.");
            }

            return _items.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    private sealed class PlainReferenceType(int value)
    {
        public int Value { get; } = value;
    }

    private sealed record ValueRecord(int Value);

    private sealed class CaseInsensitiveNonGenericComparer : IEqualityComparer
    {
        public new bool Equals(object? x, object? y)
            => StringComparer.OrdinalIgnoreCase.Equals(x, y);

        public int GetHashCode(object obj)
            => StringComparer.OrdinalIgnoreCase.GetHashCode((string)obj);
    }
}
