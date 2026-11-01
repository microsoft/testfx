// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.UnitTests.Helpers;

[TestClass]
public sealed class RoslynHashCodeTests
{
    private static readonly int[] DistinctHashInputs = [0, 1, -1, int.MinValue, 0x12345678, int.MaxValue, 43];

    [TestMethod]
    [DataRow(1)]
    [DataRow(2)]
    [DataRow(3)]
    [DataRow(4)]
    [DataRow(5)]
    [DataRow(6)]
    [DataRow(7)]
    [DataRow(8)]
    public void Combine_EachPositionDiffusesDistinctHashInputsAndVisitsOperandsInOrder(int arity)
    {
        for (int position = 0; position < arity; position++)
        {
            HashSet<int> outputs = [];
            foreach (int value in DistinctHashInputs)
            {
                List<int> visits = [];
                HashInput[] inputs = [.. Enumerable.Range(0, arity).Select(index => new HashInput(index == position ? value : 17 + index, index, visits))];

                outputs.Add(Combine(inputs));

                Assert.AreSequenceEqual(Enumerable.Range(0, arity), visits);
            }

            // With all other words fixed, xxHash32 is a composition of bijections in this word.
            // This is seed-independent, not an assumption that arbitrary unequal objects never collide.
            Assert.HasCount(7, outputs, $"Every distinct hash word must contribute at position {position}.");
        }
    }

    [TestMethod]
    [DataRow(1)]
    [DataRow(2)]
    [DataRow(3)]
    [DataRow(4)]
    [DataRow(5)]
    [DataRow(6)]
    [DataRow(7)]
    [DataRow(8)]
    [DataRow(9)]
    [DataRow(10)]
    [DataRow(11)]
    [DataRow(12)]
    public void ToHashCode_IncrementalQueuesRespectEachInputAndComparer(int length)
    {
        for (int position = 0; position < length; position++)
        {
            HashSet<int> outputs = [];
            foreach (int value in DistinctHashInputs)
            {
                RoslynHashCode hash = default;
                List<int> visits = [];
                for (int index = 0; index < length; index++)
                {
                    hash.Add(new HashInput(index == position ? value : 31 + index, index, visits));
                }

                outputs.Add(hash.ToHashCode());
                Assert.AreSequenceEqual(Enumerable.Range(0, length), visits);
            }

            Assert.HasCount(7, outputs, $"The queued or accumulated word at position {position} must contribute.");
        }
    }

    [TestMethod]
    public void Add_CustomComparerControlsNonNullHashingAndNullUsesZero()
    {
        TrackingComparer comparer = new();
        RoslynHashCode actual = default;
        actual.Add("alpha", comparer);
        actual.Add<string?>(null, comparer);
        actual.Add("beta", comparer);
        actual.Add("z", comparer: null);
        Assert.AreSequenceEqual(["alpha", "beta"], comparer.Visits);
        RoslynHashCode equivalentHashWords = default;
        equivalentHashWords.Add(5);
        equivalentHashWords.Add(0);
        equivalentHashWords.Add(4);
        equivalentHashWords.Add("z");
        Assert.AreEqual(equivalentHashWords.ToHashCode(), actual.ToHashCode());
        Assert.AreEqual(RoslynHashCode.Combine(0), RoslynHashCode.Combine<string?>(null));
        RoslynHashCode empty = default;
        RoslynHashCode anotherEmpty = default;
        Assert.AreEqual(empty.ToHashCode(), anotherEmpty.ToHashCode());
    }

    [TestMethod]
    public void ObjectMembers_RejectUnsupportedEqualityAndHashCode()
    {
        object boxed = default(RoslynHashCode);

        Assert.AreEqual(
            "HashCode is a mutable struct and should not be compared with other HashCodes. Use ToHashCode to retrieve the computed hash code.",
            Assert.ThrowsExactly<NotSupportedException>(() => boxed.GetHashCode()).Message);
        Assert.AreEqual(
            "HashCode is a mutable struct and should not be compared with other HashCodes.",
            Assert.ThrowsExactly<NotSupportedException>(() => boxed.Equals(new object())).Message);
    }

    private static int Combine(HashInput[] values)
        => values.Length switch
        {
            1 => RoslynHashCode.Combine(values[0]),
            2 => RoslynHashCode.Combine(values[0], values[1]),
            3 => RoslynHashCode.Combine(values[0], values[1], values[2]),
            4 => RoslynHashCode.Combine(values[0], values[1], values[2], values[3]),
            5 => RoslynHashCode.Combine(values[0], values[1], values[2], values[3], values[4]),
            6 => RoslynHashCode.Combine(values[0], values[1], values[2], values[3], values[4], values[5]),
            7 => RoslynHashCode.Combine(values[0], values[1], values[2], values[3], values[4], values[5], values[6]),
            8 => RoslynHashCode.Combine(values[0], values[1], values[2], values[3], values[4], values[5], values[6], values[7]),
            _ => throw new ArgumentOutOfRangeException(nameof(values)),
        };

    private sealed class HashInput(int hash, int position, List<int> visits)
    {
        public override int GetHashCode()
        {
            visits.Add(position);
            return hash;
        }
    }

    private sealed class TrackingComparer : IEqualityComparer<string?>
    {
        public List<string> Visits { get; } = [];

        public bool Equals(string? left, string? right) => StringComparer.Ordinal.Equals(left, right);

        public int GetHashCode(string? value)
        {
            Assert.IsNotNull(value);
            Visits.Add(value);
            return value.Length;
        }
    }
}
