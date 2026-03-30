// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Immutable;

using AwesomeAssertions;

using MSTest.SourceGeneration.Helpers;

namespace Microsoft.Testing.Framework.SourceGeneration.UnitTests;

/// <summary>
/// Regression tests for PR #5053 / Issue #4970: EquatableArray must implement value equality
/// so that TestTypeInfo comparisons work correctly for incremental source generation.
/// Without proper equality, the source generator would re-run unnecessarily on every keystroke.
/// </summary>
[TestClass]
public sealed class EquatableArrayEqualityTests : TestBase
{
    [TestMethod]
    public void Equals_SameElements_ReturnsTrue()
    {
        EquatableArray<int> a = ImmutableArray.Create(1, 2, 3);
        EquatableArray<int> b = ImmutableArray.Create(1, 2, 3);

        a.Equals(b).Should().BeTrue();
    }

    [TestMethod]
    public void Equals_DifferentElements_ReturnsFalse()
    {
        EquatableArray<int> a = ImmutableArray.Create(1, 2, 3);
        EquatableArray<int> b = ImmutableArray.Create(1, 2, 4);

        a.Equals(b).Should().BeFalse();
    }

    [TestMethod]
    public void Equals_DifferentLengths_ReturnsFalse()
    {
        EquatableArray<int> a = ImmutableArray.Create(1, 2, 3);
        EquatableArray<int> b = ImmutableArray.Create(1, 2);

        a.Equals(b).Should().BeFalse();
    }

    [TestMethod]
    public void Equals_BothEmpty_ReturnsTrue()
    {
        EquatableArray<int> a = ImmutableArray<int>.Empty;
        EquatableArray<int> b = ImmutableArray<int>.Empty;

        a.Equals(b).Should().BeTrue();
    }

    [TestMethod]
    public void Equals_OneEmptyOneNot_ReturnsFalse()
    {
        EquatableArray<int> a = ImmutableArray<int>.Empty;
        EquatableArray<int> b = ImmutableArray.Create(1);

        a.Equals(b).Should().BeFalse();
    }

    [TestMethod]
    public void OperatorEquals_SameElements_ReturnsTrue()
    {
        EquatableArray<int> a = ImmutableArray.Create(1, 2, 3);
        EquatableArray<int> b = ImmutableArray.Create(1, 2, 3);

        (a == b).Should().BeTrue();
    }

    [TestMethod]
    public void OperatorNotEquals_DifferentElements_ReturnsTrue()
    {
        EquatableArray<int> a = ImmutableArray.Create(1, 2, 3);
        EquatableArray<int> b = ImmutableArray.Create(4, 5, 6);

        (a != b).Should().BeTrue();
    }

    [TestMethod]
    public void GetHashCode_SameElements_ReturnsSameHash()
    {
        EquatableArray<int> a = ImmutableArray.Create(10, 20, 30);
        EquatableArray<int> b = ImmutableArray.Create(10, 20, 30);

        a.GetHashCode().Should().Be(b.GetHashCode());
    }

    [TestMethod]
    public void GetHashCode_EmptyArray_ReturnsZero()
    {
        EquatableArray<int> empty = default;

        empty.GetHashCode().Should().Be(0);
    }

    [TestMethod]
    public void Equals_WithTupleElements_WorksForSourceGenScenarios()
    {
        // TestTypeInfo uses EquatableArray<(string, int, int)> for declaration references.
        // This test verifies tuple equality works correctly for the incremental generator.
        EquatableArray<(string, int, int)> a = ImmutableArray.Create(
            ("File1.cs", 10, 20),
            ("File2.cs", 30, 40));
        EquatableArray<(string, int, int)> b = ImmutableArray.Create(
            ("File1.cs", 10, 20),
            ("File2.cs", 30, 40));

        a.Equals(b).Should().BeTrue();
        a.GetHashCode().Should().Be(b.GetHashCode());
    }

    [TestMethod]
    public void Equals_WithTupleElements_DetectsDifferences()
    {
        EquatableArray<(string, int, int)> a = ImmutableArray.Create(
            ("File1.cs", 10, 20));
        EquatableArray<(string, int, int)> b = ImmutableArray.Create(
            ("File1.cs", 10, 21));

        a.Equals(b).Should().BeFalse();
    }

    [TestMethod]
    public void Equals_WithStringElements_ValueEquality()
    {
        // TestMethodInfo uses EquatableArray<(string Key, string? Value)> for test properties.
        EquatableArray<string> a = ImmutableArray.Create("alpha", "beta");
        EquatableArray<string> b = ImmutableArray.Create("alpha", "beta");

        a.Equals(b).Should().BeTrue();
        (a == b).Should().BeTrue();
    }

    [TestMethod]
    public void ObjectEquals_WithEquatableArray_ReturnsTrue()
    {
        EquatableArray<int> a = ImmutableArray.Create(1, 2, 3);
        object b = (EquatableArray<int>)ImmutableArray.Create(1, 2, 3);

        a.Equals(b).Should().BeTrue();
    }

    [TestMethod]
    public void ObjectEquals_WithNonEquatableArray_ReturnsFalse()
    {
        EquatableArray<int> a = ImmutableArray.Create(1, 2, 3);

        a.Equals("not an array").Should().BeFalse();
    }

    [TestMethod]
    public void ImplicitConversion_RoundTrips_PreservesEquality()
    {
        var original = ImmutableArray.Create(1, 2, 3);

        EquatableArray<int> equatable = original;
        ImmutableArray<int> roundTripped = equatable;

        roundTripped.SequenceEqual(original).Should().BeTrue();
    }
}
