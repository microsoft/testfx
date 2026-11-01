// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections;

using AwesomeAssertions;

using TestFramework.ForTestingMSTest;

namespace Microsoft.VisualStudio.TestPlatform.TestFramework.UnitTests;

public partial class AssertTests : TestContainer
{
    public void AreSequenceEqual_InOrder_IdenticalSequences_Passes()
        => Assert.AreSequenceEqual([1, 2, 3], [1, 2, 3]);

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
    {
        IEnumerable<string>? value = null;
        Assert.AreSequenceEqual<string>(value, value);
    }

    public void AreSequenceEqual_InOrder_OneNull_Fails()
    {
        IEnumerable<string> actual = ["a"];

        Action action = () => Assert.AreSequenceEqual<string>(null, actual);
        action.Should().Throw<AssertFailedException>()
            .WithMessage("*Expected sequences to be equal.*one side is null, the other is not.*expected: null*actual:   [\"a\"]*Assert.AreSequenceEqual(null, actual)*");
    }

    public void AreSequenceEqual_InOrder_BothEmpty_Passes()
        => Assert.AreSequenceEqual<int>([], []);

    public void AreSequenceEqual_InOrder_OneEmpty_Fails()
    {
        Action action = () => Assert.AreSequenceEqual([], [1]);
        action.Should().Throw<AssertFailedException>()
            .WithMessage("*different lengths (expected: 0, actual: 1)*");
    }

    public void AreSequenceEqual_InOrder_CustomComparer_PassesAndReportsComparerOnFailure()
    {
        Assert.AreSequenceEqual(["a", "B"], ["A", "b"], new CaseInsensitiveStringComparer());

        string[] expected = ["a"];
        string[] actual = ["b"];

        Action action = () => Assert.AreSequenceEqual(expected, actual, new CaseInsensitiveStringComparer());
        action.Should().Throw<AssertFailedException>()
            .Which.Message.Should().Be(
                """
                Assertion failed. Expected sequences to be equal.
                Sequences have 1 element(s). 1 element(s) differ. First difference at index 0.

                expected: ["a"]
                actual:   ["b"]
                comparer: CaseInsensitiveStringComparer

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
    {
        IEnumerable<string>? value = null;
        Assert.AreSequenceEqual<string>(value, value, SequenceOrder.InAnyOrder);
    }

    public void AreSequenceEqual_InAnyOrder_OneNull_Fails()
    {
        IEnumerable<string> expected = ["a"];
        Action action = () => Assert.AreSequenceEqual<string>(expected, null, SequenceOrder.InAnyOrder);
        action.Should().Throw<AssertFailedException>()
            .WithMessage("*Expected sequences to be equal (in any order).*one side is null, the other is not.*");
    }

    public void AreSequenceEqual_InAnyOrder_BothEmpty_Passes()
        => Assert.AreSequenceEqual<int>([], [], SequenceOrder.InAnyOrder);

    public void AreSequenceEqual_InAnyOrder_OneEmpty_Fails()
    {
        Action action = () => Assert.AreSequenceEqual([], [1], SequenceOrder.InAnyOrder);
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

                notExpected: [1, 2, 3]
                actual:      [1, 2, 3]

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

                notExpected: [1, 2, 2]
                actual:      [2, 1, 2]

                Assert.AreNotSequenceEqual(notExpected, actual, <order>)
                """);
    }

    public void AreNotSequenceEqual_InAnyOrder_DifferentMultisets_Passes()
        => Assert.AreNotSequenceEqual([1, 2, 2], [1, 2, 3], SequenceOrder.InAnyOrder);

    public void AreNotSequenceEqual_BothNull_Fails()
    {
        IEnumerable<string>? value = null;
        Action action = () => Assert.AreNotSequenceEqual<string>(value, value);
        action.Should().Throw<AssertFailedException>()
            .WithMessage("Assertion failed. Expected sequences to differ.*notExpected: null*actual:      null*");
    }

    public void AreNotSequenceEqual_CustomComparerFailure_ReportsComparer()
    {
        string[] notExpected = ["a"];
        string[] actual = ["A"];

        Action action = () => Assert.AreNotSequenceEqual(notExpected, actual, new CaseInsensitiveStringComparer());
        action.Should().Throw<AssertFailedException>()
            .Which.Message.Should().Be(
                """
                Assertion failed. Expected sequences to differ.

                notExpected: ["a"]
                actual:      ["A"]
                comparer:    CaseInsensitiveStringComparer

                Assert.AreNotSequenceEqual(notExpected, actual, <comparer>)
                """);
    }

    public void AreNotSequenceEqual_OneNull_Passes()
        => Assert.AreNotSequenceEqual([1], null);

    public void AreSequenceEqual_InvalidOrder_Throws()
    {
        Action action = () => Assert.AreSequenceEqual([1], [1], (SequenceOrder)999);
        action.Should().Throw<ArgumentOutOfRangeException>();
    }

    public void AreSequenceEqual_InAnyOrder_CustomComparerFailure_ReportsComparer()
    {
        string[] expected = ["a"];
        string[] actual = ["c"];

        Action action = () => Assert.AreSequenceEqual(expected, actual, new CaseInsensitiveStringComparer(), SequenceOrder.InAnyOrder);
        action.Should().Throw<AssertFailedException>()
            .Which.Message.Should().Be(
                """
                Assertion failed. Expected sequences to be equal (in any order).
                Missing 1 element(s) from actual. Found 1 unexpected element(s).

                missing:    ["a"]
                unexpected: ["c"]
                comparer:   CaseInsensitiveStringComparer

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

    public void AreSequenceEqual_RecordElements_UsesValueEquality()
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
            return EnumerationCount > 1
                ? throw new InvalidOperationException("Sequence was enumerated more than once.")
                : _items.GetEnumerator();
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

#if NETCOREAPP3_1_OR_GREATER
    public void SequenceAssertions_ConcreteCollectionOverloads_KeepComparerAndOrder()
    {
        string[] expected = ["a", "b", "a"];
        List<string> expectedList = [.. expected];
        List<string> actual = [];
        var comparer = new CaseInsensitiveStringComparer();
        (Action Equal, Action NotEqual, bool AnyOrder, bool IgnoreCase)[] cases =
        [
            (() => Assert.AreSequenceEqual<string, List<string>, List<string>>(expectedList, actual, comparer, "concrete context"),
                () => Assert.AreNotSequenceEqual<string, List<string>, List<string>>(expectedList, actual, comparer, "concrete context"), false, true),
            (() => Assert.AreSequenceEqual<string, List<string>, List<string>>(expectedList, actual, comparer, SequenceOrder.InAnyOrder, "concrete context"),
                () => Assert.AreNotSequenceEqual<string, List<string>, List<string>>(expectedList, actual, comparer, SequenceOrder.InAnyOrder, "concrete context"), true, true),
            (() => Assert.AreSequenceEqual(expected, actual, "concrete context"),
                () => Assert.AreNotSequenceEqual(expected, actual, "concrete context"), false, false),
            (() => Assert.AreSequenceEqual(expected, actual, SequenceOrder.InAnyOrder, "concrete context"),
                () => Assert.AreNotSequenceEqual(expected, actual, SequenceOrder.InAnyOrder, "concrete context"), true, false),
            (() => Assert.AreSequenceEqual(expected, actual.ToArray(), comparer, SequenceOrder.InAnyOrder, "concrete context"),
                () => Assert.AreNotSequenceEqual(expected, actual.ToArray(), comparer, SequenceOrder.InAnyOrder, "concrete context"), true, true),
        ];
        foreach ((Action equal, Action notEqual, bool anyOrder, bool ignoreCase) in cases)
        {
            actual = anyOrder ? ["b", "a", "a"] : ["a", "b", "a"];
            if (ignoreCase)
            {
                actual = [.. actual.Select(value => value.ToUpperInvariant())];
            }

            equal.Should().NotThrow();
            AssertFailedException inverse = CaptureSequenceScopedFailure(notEqual);
            inverse.ExpectedText.Should().Be("[\"a\", \"b\", \"a\"]");
            inverse.Message.Should().StartWith(anyOrder
                ? "Assertion failed. Expected sequences to differ (in any order)."
                : "Assertion failed. Expected sequences to differ.");
            inverse.Message.Should().Contain("concrete context");
            actual = ["z", "z", "z"];
            notEqual.Should().NotThrow();
            AssertFailedException mismatch = CaptureSequenceScopedFailure(equal);
            mismatch.ExpectedText.Should().Be("[\"a\", \"b\", \"a\"]");
            mismatch.ActualText.Should().Be("[\"z\", \"z\", \"z\"]");
            mismatch.Message.Should().Contain("concrete context");
        }
    }
#endif

    public void SequenceAssertions_NonGenericOverloads_KeepComparerOrderAndFailureEvidence()
    {
        IEnumerable expected = new ArrayList { "a", "b", "a" };
        IEnumerable actual = Array.Empty<string>();
        var comparer = new CaseInsensitiveNonGenericComparer();
        (Action Equal, Action NotEqual, int Mode)[] cases =
        [
            (() => Assert.AreSequenceEqual(expected, actual, "non-generic context"),
                () => Assert.AreNotSequenceEqual(expected, actual, "non-generic context"), 0),
            (() => Assert.AreSequenceEqual(expected, actual, SequenceOrder.InAnyOrder, "non-generic context"),
                () => Assert.AreNotSequenceEqual(expected, actual, SequenceOrder.InAnyOrder, "non-generic context"), 1),
            (() => Assert.AreSequenceEqual(expected, actual, comparer, "non-generic context"),
                () => Assert.AreNotSequenceEqual(expected, actual, comparer, "non-generic context"), 2),
            (() => Assert.AreSequenceEqual(expected, actual, comparer, SequenceOrder.InAnyOrder, "non-generic context"),
                () => Assert.AreNotSequenceEqual(expected, actual, comparer, SequenceOrder.InAnyOrder, "non-generic context"), 3),
        ];
        foreach ((Action equal, Action notEqual, int mode) in cases)
        {
            string[] matching = mode switch
            {
                0 => ["a", "b", "a"],
                1 => ["b", "a", "a"],
                2 => ["A", "B", "A"],
                _ => ["B", "A", "A"],
            };
            actual = new ArrayList(matching);
            equal.Should().NotThrow();
            AssertFailedException inverse = CaptureSequenceScopedFailure(notEqual);
            inverse.ExpectedText.Should().Be("[\"a\", \"b\", \"a\"]");
            inverse.Message.Should().Contain("non-generic context");

            actual = new ArrayList { "z", "z", "z" };
            notEqual.Should().NotThrow();
            AssertFailedException mismatch = CaptureSequenceScopedFailure(equal);
            mismatch.ExpectedText.Should().Be("[\"a\", \"b\", \"a\"]");
            mismatch.ActualText.Should().Be("[\"z\", \"z\", \"z\"]");
            mismatch.Message.Should().Contain("non-generic context");
            if (mode >= 2)
            {
                mismatch.Message.Should().Contain("CaseInsensitiveNonGenericComparer");
            }
        }
    }

    public void SequenceAssertions_AnyOrder_NullsHaveIndependentMultiplicity()
    {
        IEnumerable<string?> expected = new string?[] { null, "a", null };
        IEnumerable<string?> actual = new string?[] { "a", null, null };
        Assert.AreSequenceEqual(expected, actual, SequenceOrder.InAnyOrder);
        CaptureSequenceScopedFailure(() => Assert.AreNotSequenceEqual(expected, actual, SequenceOrder.InAnyOrder))
            .ActualText.Should().Be("[\"a\", null, null]");

        (string?[] Expected, string?[] Actual, string Missing, string Unexpected)[] cases =
        [
            ([null, "a", null], [null, "a"], "[null]", "[]"),
            ([null, "a"], [null, "a", null], "[]", "[null]"),
        ];
        foreach ((string?[] left, string?[] right, string missing, string unexpected) in cases)
        {
            expected = left;
            actual = right;
            Action differs = () => Assert.AreNotSequenceEqual(expected, actual, SequenceOrder.InAnyOrder);
            differs.Should().NotThrow();
            AssertFailedException exception = CaptureSequenceScopedFailure(() => Assert.AreSequenceEqual(expected, actual, SequenceOrder.InAnyOrder));
            string summary = missing == "[]"
                ? "Missing 0 element(s) from actual. Found 1 unexpected element(s)."
                : "Missing 1 element(s) from actual. Found 0 unexpected element(s).";
            string message = $"""
                Assertion failed. Expected sequences to be equal (in any order).
                {summary}

                missing:    {missing}
                unexpected: {unexpected}

                Assert.AreSequenceEqual(expected, actual, <order>)
                """;
            exception.Message.Replace("\r\n", "\n").Should().Be(message.Replace("\r\n", "\n"));
        }
    }

    public void SequenceAssertions_NullAndLengthMismatches_ContinueWithinScope()
    {
        var comparer = new CaseInsensitiveStringComparer();
        foreach (bool nullExpected in new[] { true, false })
        {
            IEnumerable<string>? expected = nullExpected ? null : new[] { "a" };
            IEnumerable<string>? actual = nullExpected ? new[] { "a" } : null;
            Action differs = () => Assert.AreNotSequenceEqual(expected, actual, comparer);
            differs.Should().NotThrow();
            AssertFailedException failure = CaptureSequenceScopedFailure(
                () => Assert.AreSequenceEqual(expected, actual, comparer, message: null, expectedExpression: string.Empty, actualExpression: string.Empty));
            string expectedText = nullExpected ? "null" : "[\"a\"]";
            string actualText = nullExpected ? "[\"a\"]" : "null";
            string message = $"""
                Assertion failed. Expected sequences to be equal.
                one side is null, the other is not.

                expected: {expectedText}
                actual:   {actualText}
                comparer: CaseInsensitiveStringComparer
                """;
            failure.Message.Replace("\r\n", "\n").Should().Be(message.Replace("\r\n", "\n"));
            failure.ExpectedText.Should().Be(expectedText);
            failure.ActualText.Should().Be(actualText);
        }

        IEnumerable<string> left = new[] { "a", "b" };
        IEnumerable<string> right = new[] { "A" };
        Assert.AreNotSequenceEqual(left, right, comparer);
        CaptureSequenceScopedFailure(() => Assert.AreSequenceEqual(left, right, comparer))
            .Message.Should().Contain("different lengths (expected: 2, actual: 1)").And.Contain("comparer: CaseInsensitiveStringComparer");

        CaptureSequenceScopedFailure(() => Assert.AreNotSequenceEqual(left, left)).ExpectedText.Should().Be("[\"a\", \"b\"]");
    }

    public void SequenceAssertions_InvalidOrder_IsRejectedBeforeReferenceEquality()
    {
        IEnumerable<int> values = new[] { 1, 2 };
        foreach (SequenceOrder order in new[] { (SequenceOrder)(-1), (SequenceOrder)int.MaxValue })
        {
            Action equal = () => Assert.AreSequenceEqual(values, values, order);
            Action notEqual = () => Assert.AreNotSequenceEqual(values, values, order);
            equal.Should().ThrowExactly<ArgumentOutOfRangeException>().Which.ParamName.Should().Be("order");
            notEqual.Should().ThrowExactly<ArgumentOutOfRangeException>().Which.ParamName.Should().Be("order");
        }
    }

    private static AssertFailedException CaptureSequenceScopedFailure(Action assertion)
    {
        int continued = 0;
        Action action = () =>
        {
            using (Assert.Scope())
            {
                assertion();
                continued++;
            }
        };
        AssertFailedException failure = action.Should().ThrowExactly<AssertFailedException>().Which;
        continued.Should().Be(1);
        return failure;
    }
}
