// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;

using AwesomeAssertions;

using MSTest.Analyzers.Shared;

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.PlatformServices.SourceGeneration.UnitTests;

[TestClass]
public sealed class EquatableArrayTests
{
    [TestMethod]
    public void DefaultAndEmpty_AreValueEquivalentAndEnumerateNoElements()
    {
        EquatableArray<int> defaultArray = default;
        EquatableArray<int> emptyArray = EquatableArray<int>.Empty;

        defaultArray.Count.Should().Be(0);
        defaultArray.Length.Should().Be(0);
        defaultArray.AsImmutableArray().IsDefault.Should().BeFalse();
        defaultArray.Items.Should().BeEmpty();
        emptyArray.Items.Should().BeEmpty();
        defaultArray.Equals(emptyArray).Should().BeTrue();
        emptyArray.Equals(defaultArray).Should().BeTrue();
        defaultArray.GetHashCode().Should().Be(17);
        emptyArray.GetHashCode().Should().Be(17);

        List<int> enumerated = [];
        foreach (int item in defaultArray)
        {
            enumerated.Add(item);
        }

        enumerated.Should().BeEmpty();
        Action indexEmpty = () => _ = defaultArray[0];
        indexEmpty.Should().ThrowExactly<IndexOutOfRangeException>();
    }

    [TestMethod]
    public void Equals_IndependentlyAllocatedValuesIncludingNull_AreEqual()
    {
        EquatableArray<ValueElement> left = new([new(2), null!, new(9)]);
        EquatableArray<ValueElement> right = new([new(2), null!, new(9)]);

        left.Equals(right).Should().BeTrue();
        right.Equals(left).Should().BeTrue();
        left.Equals((object)right).Should().BeTrue();
        left.GetHashCode().Should().Be(508378);
        right.GetHashCode().Should().Be(508378);
    }

    [TestMethod]
    public void Equals_NullElementAndValue_AreNotEqualInEitherDirection()
    {
        EquatableArray<ValueElement> withNull = new([new(2), null!, new(9)]);
        EquatableArray<ValueElement> withValue = new([new(2), new(5), new(9)]);

        withNull.Equals(withValue).Should().BeFalse();
        withValue.Equals(withNull).Should().BeFalse();
    }

    [TestMethod]
    [DataRow(0, DisplayName = "Different first element")]
    [DataRow(1, DisplayName = "Different interior element")]
    [DataRow(2, DisplayName = "Different last element")]
    public void Equals_DifferenceAtAnyPosition_IsNotEqual(int changedIndex)
    {
        EquatableArray<int> original = new([2, 5, 9]);
        int[] changed = [2, 5, 9];
        changed[changedIndex] = 17;
        EquatableArray<int> different = new([.. changed]);

        original.Equals(different).Should().BeFalse();
        different.Equals(original).Should().BeFalse();
    }

    [TestMethod]
    public void Equals_DifferentLengthsOrOrder_AreNotEqual()
    {
        EquatableArray<int> original = new([2, 5, 9]);
        EquatableArray<int> shorter = new([2, 5]);
        EquatableArray<int> longer = new([2, 5, 9, 12]);
        EquatableArray<int> reversed = new([9, 5, 2]);

        original.Equals(shorter).Should().BeFalse();
        shorter.Equals(original).Should().BeFalse();
        original.Equals(longer).Should().BeFalse();
        original.Equals(reversed).Should().BeFalse();
        original.Equals(EquatableArray<int>.Empty).Should().BeFalse();
    }

    [TestMethod]
    public void Equals_BoxedValues_RejectsNullOtherTypesAndDifferentElementTypes()
    {
        EquatableArray<int> array = new([2, 5, 9]);
        ImmutableArray<int> immutable = [2, 5, 9];
        object[] differentValues = [immutable, new EquatableArray<long>([2, 5, 9]), new EquatableArray<int>([2, 5, 10])];

        array.Equals(null).Should().BeFalse();
        foreach (object different in differentValues)
        {
            array.Equals(different).Should().BeFalse();
        }
    }

    [TestMethod]
    public void GetHashCode_FoldsEveryElementInOrder()
    {
        EquatableArray<int> forward = new([2, 5, 9]);
        EquatableArray<int> reverse = new([9, 5, 2]);

        forward.GetHashCode().Should().Be(508533);
        reverse.GetHashCode().Should().Be(515253);
    }

    [TestMethod]
    public void EnumerationAndIndexer_PreserveAllElementsAndRejectAdjacentInvalidIndexes()
    {
        EquatableArray<int> array = new([2, 5, 9]);
        List<int> enumerated = [];
        foreach (int item in array)
        {
            enumerated.Add(item);
        }

        array.Length.Should().Be(3);
        array.Count.Should().Be(3);
        array.Items.Should().Equal(2, 5, 9);
        enumerated.Should().Equal(2, 5, 9);
        array[0].Should().Be(2);
        array[1].Should().Be(5);
        array[2].Should().Be(9);
        Action beforeFirst = () => _ = array[-1];
        Action afterLast = () => _ = array[3];
        beforeFirst.Should().ThrowExactly<IndexOutOfRangeException>();
        afterLast.Should().ThrowExactly<IndexOutOfRangeException>();
    }

    [TestMethod]
    public void ToEquatableArray_MaterializesIndependentSnapshot()
    {
        List<int> source = [2, 5, 9];

        var snapshot = source.ToEquatableArray();
        source[0] = 17;
        source.Add(23);

        snapshot.Items.Should().Equal(2, 5, 9);
        snapshot.Equals(new EquatableArray<int>([2, 5, 9])).Should().BeTrue();
        Enumerable.Empty<int>().ToEquatableArray().Equals(EquatableArray<int>.Empty).Should().BeTrue();
    }

    [TestMethod]
    public void WhereNotNull_RemovesNullsAtEveryPositionWithoutDroppingEmptyStringsOrReordering()
    {
        string?[] source = [null, "first", null, string.Empty, "last", null];

        source.WhereNotNull().Should().Equal("first", string.Empty, "last");
        new string?[] { null, null }.WhereNotNull().Should().BeEmpty();
        Array.Empty<string?>().WhereNotNull().Should().BeEmpty();
    }

    [TestMethod]
    public void ToEquatableArray_NullSource_IsRejectedByImmutableArrayMaterialization()
    {
        Action materialize = () => EquatableArrayExtensions.ToEquatableArray<int>(null!);

        materialize.Should().ThrowExactly<ArgumentNullException>().Which.ParamName.Should().Be("items");
    }

    [TestMethod]
    public void WhereNotNull_NullSource_IsRejectedBeforeReturningAnEnumerable()
    {
        Action filter = () => EquatableArrayExtensions.WhereNotNull<string>(null!);

        filter.Should().ThrowExactly<ArgumentNullException>().Which.ParamName.Should().Be("source");
    }

    private sealed class ValueElement(int value) : IEquatable<ValueElement>
    {
        public int Value { get; } = value;

        public bool Equals(ValueElement? other) => other is not null && Value == other.Value;

        public override bool Equals(object? obj) => obj is ValueElement other && Equals(other);

        public override int GetHashCode() => Value;
    }
}
