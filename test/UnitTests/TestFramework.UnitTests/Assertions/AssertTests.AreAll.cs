// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections;

using AwesomeAssertions;

using TestFramework.ForTestingMSTest;

namespace Microsoft.VisualStudio.TestPlatform.TestFramework.UnitTests;

public partial class AssertTests : TestContainer
{
    #region AreAllNotNull

    public void AreAllNotNull_Generic_NoNulls_ShouldPass()
        => Assert.AreAllNotNull(new[] { "a", "b", "c" });

    public void AreAllNotNull_Generic_Empty_ShouldPass()
        => Assert.AreAllNotNull(Array.Empty<string>());

    public void AreAllNotNull_Generic_ValueTypes_ShouldPass()
        => Assert.AreAllNotNull(new[] { 1, 2, 3 });

    public void AreAllNotNull_Generic_HasNull_ShouldFail()
    {
        Action action = () => Assert.AreAllNotNull(new[] { "a", null, "b", null });
        action.Should().Throw<AssertFailedException>()
            .WithMessage(
                """
                Assertion failed. Expected all items in collection to be non-null.

                null indices: [1, 3]
                collection:   ["a", null, "b", null]

                Assert.AreAllNotNull(new[] { "a", null, "b", null })
                """);
    }

    public void AreAllNotNull_Generic_WithUserMessage_ShouldFail()
    {
        Action action = () => Assert.AreAllNotNull(new[] { "a", null }, "User-provided message");
        action.Should().Throw<AssertFailedException>()
            .WithMessage(
                """
                Assertion failed. Expected all items in collection to be non-null.
                User-provided message

                null indices: [1]
                collection:   ["a", null]

                Assert.AreAllNotNull(new[] { "a", null })
                """);
    }

    public void AreAllNotNull_Generic_NullCollection_ShouldFail()
    {
        Action action = () => Assert.AreAllNotNull((IEnumerable<string>?)null);
        action.Should().Throw<AssertFailedException>()
            .WithMessage("Assert.AreAllNotNull failed. The parameter 'collection' is invalid. The value cannot be null.");
    }

    public void AreAllNotNull_NonGeneric_NoNulls_ShouldPass()
    {
        ArrayList list = ["a", "b", "c"];
        Assert.AreAllNotNull(list);
    }

    public void AreAllNotNull_NonGeneric_HasNull_ShouldFail()
    {
        ArrayList list = ["a", null, "b"];
        Action action = () => Assert.AreAllNotNull(list);
        action.Should().Throw<AssertFailedException>()
            .WithMessage(
                """
                Assertion failed. Expected all items in collection to be non-null.

                null indices: [1]
                collection:   ["a", null, "b"]

                Assert.AreAllNotNull(list)
                """);
    }

    public void AreAllNotNull_NonGeneric_NullCollection_ShouldFail()
    {
        Action action = () => Assert.AreAllNotNull(null);
        action.Should().Throw<AssertFailedException>()
            .WithMessage("Assert.AreAllNotNull failed. The parameter 'collection' is invalid. The value cannot be null.");
    }

    public void AreAllNotNull_NonGeneric_WithUserMessage_ShouldFail()
    {
        ArrayList list = ["a", null];
        Action action = () => Assert.AreAllNotNull(list, "User-provided message");
        action.Should().Throw<AssertFailedException>()
            .WithMessage(
                """
                Assertion failed. Expected all items in collection to be non-null.
                User-provided message

                null indices: [1]
                collection:   ["a", null]

                Assert.AreAllNotNull(list)
                """);
    }

    public void AreAllNotNull_Generic_NullableValueType_HasNull_ShouldFail()
    {
        Action action = () => Assert.AreAllNotNull(new int?[] { 1, null, 3 });
        action.Should().Throw<AssertFailedException>()
            .WithMessage(
                """
                Assertion failed. Expected all items in collection to be non-null.

                null indices: [1]
                collection:   [1, null, 3]

                Assert.AreAllNotNull(new int?[] { 1, null, 3 })
                """);
    }

    public void AreAllNotNull_Generic_LazyEnumerable_HasNull_ShouldFail()
    {
        static IEnumerable<string?> Lazy()
        {
            yield return "a";
            yield return null;
            yield return "b";
        }

        Action action = () => Assert.AreAllNotNull(Lazy());
        action.Should().Throw<AssertFailedException>()
            .WithMessage("*null indices: [1]*collection:*");
    }

    #endregion // AreAllNotNull

    #region AreAllDistinct

    public void AreAllDistinct_Generic_AllDistinct_ShouldPass()
        => Assert.AreAllDistinct(new[] { 1, 2, 3 });

    public void AreAllDistinct_Generic_Empty_ShouldPass()
        => Assert.AreAllDistinct(Array.Empty<int>());

    public void AreAllDistinct_Generic_SingleNull_ShouldPass()
        => Assert.AreAllDistinct(new string?[] { "a", null, "b" });

    public void AreAllDistinct_Generic_HasDuplicate_ShouldFail()
    {
        Action action = () => Assert.AreAllDistinct(new[] { 1, 2, 3, 4, 3 });
        action.Should().Throw<AssertFailedException>()
            .WithMessage(
                """
                Assertion failed. Expected all items in collection to be distinct.

                duplicates: [3]
                collection: [1, 2, 3, 4, 3]

                Assert.AreAllDistinct(new[] { 1, 2, 3, 4, 3 })
                """);
    }

    public void AreAllDistinct_Generic_HasMultipleDuplicates_ShouldFail()
    {
        Action action = () => Assert.AreAllDistinct(new[] { 1, 2, 3, 2, 1, 1 });
        action.Should().Throw<AssertFailedException>()
            .WithMessage(
                """
                Assertion failed. Expected all items in collection to be distinct.

                duplicates: [2, 1]
                collection: [1, 2, 3, 2, 1, 1]

                Assert.AreAllDistinct(new[] { 1, 2, 3, 2, 1, 1 })
                """);
    }

    public void AreAllDistinct_Generic_NullDuplicate_ShouldFail()
    {
        Action action = () => Assert.AreAllDistinct(new string?[] { "a", null, "b", null });
        action.Should().Throw<AssertFailedException>()
            .WithMessage(
                """
                Assertion failed. Expected all items in collection to be distinct.

                duplicates: [null]
                collection: ["a", null, "b", null]

                Assert.AreAllDistinct(new string?[] { "a", null, "b", null })
                """);
    }

    public void AreAllDistinct_Generic_WithUserMessage_ShouldFail()
    {
        Action action = () => Assert.AreAllDistinct(new[] { 1, 1 }, "User-provided message");
        action.Should().Throw<AssertFailedException>()
            .WithMessage(
                """
                Assertion failed. Expected all items in collection to be distinct.
                User-provided message

                duplicates: [1]
                collection: [1, 1]

                Assert.AreAllDistinct(new[] { 1, 1 })
                """);
    }

    public void AreAllDistinct_Generic_WithComparer_AllDistinct_ShouldPass()
        => Assert.AreAllDistinct(new[] { "a", "B", "c" }, new CaseInsensitiveStringComparer());

    public void AreAllDistinct_Generic_WithComparer_HasDuplicate_ShouldFail()
    {
        string comparerTypeName = typeof(CaseInsensitiveStringComparer).FullName!;
        string expectedMessage = """
            Assertion failed. Expected all items in collection to be distinct.

            duplicates: ["a"]
            collection: ["A", "B", "a"]
            comparer:   __COMPARER__

            Assert.AreAllDistinct(new[] { "A", "B", "a" }, <comparer>)
            """.Replace("__COMPARER__", comparerTypeName);
        Action action = () => Assert.AreAllDistinct(new[] { "A", "B", "a" }, new CaseInsensitiveStringComparer());
        action.Should().Throw<AssertFailedException>()
            .WithMessage(expectedMessage);
    }

    public void AreAllDistinct_Generic_NullCollection_ShouldFail()
    {
        Action action = () => Assert.AreAllDistinct((IEnumerable<int>?)null);
        action.Should().Throw<AssertFailedException>()
            .WithMessage("Assert.AreAllDistinct failed. The parameter 'collection' is invalid. The value cannot be null.");
    }

    public void AreAllDistinct_Generic_NullComparer_ShouldFail()
    {
        Action action = () => Assert.AreAllDistinct(new[] { 1, 2 }, (IEqualityComparer<int>?)null);
        action.Should().Throw<AssertFailedException>()
            .WithMessage("Assert.AreAllDistinct failed. The parameter 'comparer' is invalid. The value cannot be null.");
    }

    public void AreAllDistinct_NonGeneric_AllDistinct_ShouldPass()
    {
        ArrayList list = [1, "a", 3.5];
        Assert.AreAllDistinct(list);
    }

    public void AreAllDistinct_NonGeneric_HasDuplicate_ShouldFail()
    {
        ArrayList list = [1, 2, 3, 2];
        Action action = () => Assert.AreAllDistinct(list);
        action.Should().Throw<AssertFailedException>()
            .WithMessage(
                """
                Assertion failed. Expected all items in collection to be distinct.

                duplicates: [2]
                collection: [1, 2, 3, 2]

                Assert.AreAllDistinct(list)
                """);
    }

    public void AreAllDistinct_NonGeneric_WithComparer_HasDuplicate_ShouldFail()
    {
        string comparerTypeName = typeof(CaseInsensitiveStringComparer).FullName!;
        string expectedMessage = """
            Assertion failed. Expected all items in collection to be distinct.

            duplicates: ["a"]
            collection: ["A", "B", "a"]
            comparer:   __COMPARER__

            Assert.AreAllDistinct(list, <comparer>)
            """.Replace("__COMPARER__", comparerTypeName);
        ArrayList list = ["A", "B", "a"];
        Action action = () => Assert.AreAllDistinct(list, new CaseInsensitiveStringComparer());
        action.Should().Throw<AssertFailedException>()
            .WithMessage(expectedMessage);
    }

    public void AreAllDistinct_NonGeneric_NullCollection_ShouldFail()
    {
        Action action = () => Assert.AreAllDistinct(null);
        action.Should().Throw<AssertFailedException>()
            .WithMessage("Assert.AreAllDistinct failed. The parameter 'collection' is invalid. The value cannot be null.");
    }

    public void AreAllDistinct_NonGeneric_NullComparer_ShouldFail()
    {
        ArrayList list = [1, 2];
        Action action = () => Assert.AreAllDistinct(list, (IEqualityComparer?)null);
        action.Should().Throw<AssertFailedException>()
            .WithMessage("Assert.AreAllDistinct failed. The parameter 'comparer' is invalid. The value cannot be null.");
    }

    public void AreAllDistinct_NonGeneric_WithUserMessage_ShouldFail()
    {
        ArrayList list = [1, 1];
        Action action = () => Assert.AreAllDistinct(list, "User-provided message");
        action.Should().Throw<AssertFailedException>()
            .WithMessage(
                """
                Assertion failed. Expected all items in collection to be distinct.
                User-provided message

                duplicates: [1]
                collection: [1, 1]

                Assert.AreAllDistinct(list)
                """);
    }

    public void AreAllDistinct_NonGeneric_SingleNull_ShouldPass()
    {
        ArrayList list = ["a", null, "b"];
        Assert.AreAllDistinct(list);
    }

    public void AreAllDistinct_NonGeneric_NullDuplicate_ShouldFail()
    {
        ArrayList list = ["a", null, null];
        Action action = () => Assert.AreAllDistinct(list);
        action.Should().Throw<AssertFailedException>()
            .WithMessage(
                """
                Assertion failed. Expected all items in collection to be distinct.

                duplicates: [null]
                collection: ["a", null, null]

                Assert.AreAllDistinct(list)
                """);
    }

    public void AreAllDistinct_Generic_ManyNulls_ReportsNullOnce_ShouldFail()
    {
        Action action = () => Assert.AreAllDistinct(new string?[] { null, "a", null, "b", null });
        action.Should().Throw<AssertFailedException>()
            .WithMessage(
                """
                Assertion failed. Expected all items in collection to be distinct.

                duplicates: [null]
                collection: [null, "a", null, "b", null]

                Assert.AreAllDistinct(new string?[] { null, "a", null, "b", null })
                """);
    }

    public void AreAllDistinct_Generic_ManyDuplicatesOfSameValue_ReportsValueOnce_ShouldFail()
    {
        Action action = () => Assert.AreAllDistinct(new[] { 5, 5, 5, 5 });
        action.Should().Throw<AssertFailedException>()
            .WithMessage(
                """
                Assertion failed. Expected all items in collection to be distinct.

                duplicates: [5]
                collection: [5, 5, 5, 5]

                Assert.AreAllDistinct(new[] { 5, 5, 5, 5 })
                """);
    }

    // Pins the current behavior: null elements are short-circuited and never passed to the user-provided comparer.
    // A comparer that treats null as equal to "" is therefore not consulted, and [null, ""] is considered distinct.
    public void AreAllDistinct_Generic_ComparerTreatsNullAsEmpty_NullAndEmpty_ShouldPass()
        => Assert.AreAllDistinct(new string?[] { null, string.Empty }, new NullEqualsEmptyStringComparer());

    // Pins the current behavior: a comparer that throws on null is never invoked with a null argument because
    // null elements are short-circuited before the comparer is consulted.
    public void AreAllDistinct_Generic_ComparerThrowsOnNull_WithNulls_ShouldNotInvokeComparerForNull()
        => Assert.AreAllDistinct(new string?[] { null, "a", "b" }, new ThrowOnNullStringComparer());

    private sealed class CaseInsensitiveStringComparer : IEqualityComparer<string?>, IEqualityComparer
    {
        public bool Equals(string? x, string? y) => StringComparer.OrdinalIgnoreCase.Equals(x, y);

        public int GetHashCode(string? obj) => obj is null ? 0 : StringComparer.OrdinalIgnoreCase.GetHashCode(obj);

        bool IEqualityComparer.Equals(object? x, object? y) => Equals(x as string, y as string);

        int IEqualityComparer.GetHashCode(object obj) => obj is string value ? GetHashCode(value) : 0;
    }

    private sealed class NullEqualsEmptyStringComparer : IEqualityComparer<string?>
    {
        public bool Equals(string? x, string? y) => (x ?? string.Empty) == (y ?? string.Empty);

        public int GetHashCode(string? obj) => (obj ?? string.Empty).GetHashCode();
    }

    private sealed class ThrowOnNullStringComparer : IEqualityComparer<string?>
    {
        public bool Equals(string? x, string? y)
            => x is null || y is null
                ? throw new InvalidOperationException("Comparer should not be invoked with null.")
                : x == y;

        public int GetHashCode(string? obj)
            => obj is null
                ? throw new InvalidOperationException("Comparer should not be invoked with null.")
                : obj.GetHashCode();
    }

    #endregion // AreAllDistinct

    #region AreAllOfType

    public void AreAllOfType_Generic_AllMatch_ShouldPass()
        => Assert.AreAllOfType<string>(new object[] { "a", "b", "c" });

    public void AreAllOfType_Generic_DerivedTypes_ShouldPass()
        => Assert.AreAllOfType<DerivedAllItemsBase>(new object[] { new DerivedAllItemsA(), new DerivedAllItemsB() });

    public void AreAllOfType_Generic_BaseTypeAcceptsDerived_ShouldPass()
        => Assert.AreAllOfType<object>(new object[] { 1, "two", 3.0 });

    public void AreAllOfType_Generic_Empty_ShouldPass()
        => Assert.AreAllOfType<string>(Array.Empty<object>());

    public void AreAllOfType_Generic_NullElement_ShouldFail()
    {
        Action action = () => Assert.AreAllOfType<string>(new object?[] { "a", null, "b" });
        action.Should().Throw<AssertFailedException>()
            .WithMessage(
                """
                Assertion failed. Expected all items in collection to be of the specified type.

                expected type: System.String (or derived)
                mismatches:    [index 1: <null>]
                collection:    ["a", null, "b"]

                Assert.AreAllOfType<TExpected>(new object?[] { "a", null, "b" })
                """);
    }

    public void AreAllOfType_Generic_HasMismatch_ShouldFail()
    {
        Action action = () => Assert.AreAllOfType<string>(new object[] { "a", 42, "b", true });
        action.Should().Throw<AssertFailedException>()
            .WithMessage(
                """
                Assertion failed. Expected all items in collection to be of the specified type.

                expected type: System.String (or derived)
                mismatches:    [index 1: System.Int32, index 3: System.Boolean]
                collection:    ["a", 42, "b", true]

                Assert.AreAllOfType<TExpected>(new object[] { "a", 42, "b", true })
                """);
    }

    public void AreAllOfType_Generic_WithUserMessage_ShouldFail()
    {
        Action action = () => Assert.AreAllOfType<string>(new object[] { 1 }, "User-provided message");
        action.Should().Throw<AssertFailedException>()
            .WithMessage(
                """
                Assertion failed. Expected all items in collection to be of the specified type.
                User-provided message

                expected type: System.String (or derived)
                mismatches:    [index 0: System.Int32]
                collection:    [1]

                Assert.AreAllOfType<TExpected>(new object[] { 1 })
                """);
    }

    public void AreAllOfType_Generic_HasMismatch_ShouldPopulateExpectedAndActualPayload()
    {
        try
        {
            Assert.AreAllOfType<string>(new object[] { 1 });
        }
        catch (AssertFailedException ex)
        {
            ex.ExpectedText.Should().Be("System.String (or derived)");
            ex.ActualText.Should().Be("[1]");
            ex.Data["assert.expected"].Should().Be("System.String (or derived)");
            ex.Data["assert.actual"].Should().Be("[1]");
            return;
        }

        throw new AssertFailedException("Expected AssertFailedException was not thrown.");
    }

    public void AreAllOfType_Generic_NullCollection_ShouldFail()
    {
        Action action = () => Assert.AreAllOfType<string>(null);
        action.Should().Throw<AssertFailedException>()
            .WithMessage("Assert.AreAllOfType failed. The parameter 'collection' is invalid. The value cannot be null.");
    }

    public void AreAllOfType_NonGeneric_AllMatch_ShouldPass()
    {
        ArrayList list = ["a", "b"];
        Assert.AreAllOfType(typeof(string), list);
    }

    public void AreAllOfType_NonGeneric_HasMismatch_ShouldFail()
    {
        ArrayList list = ["a", 42];
        Action action = () => Assert.AreAllOfType(typeof(string), list);
        action.Should().Throw<AssertFailedException>()
            .WithMessage(
                """
                Assertion failed. Expected all items in collection to be of the specified type.

                expected type: System.String (or derived)
                mismatches:    [index 1: System.Int32]
                collection:    ["a", 42]

                Assert.AreAllOfType(typeof(string), list)
                """);
    }

    public void AreAllOfType_NonGeneric_NullCollection_ShouldFail()
    {
        Action action = () => Assert.AreAllOfType(typeof(string), null);
        action.Should().Throw<AssertFailedException>()
            .WithMessage("Assert.AreAllOfType failed. The parameter 'collection' is invalid. The value cannot be null.");
    }

    public void AreAllOfType_NonGeneric_NullExpectedType_ShouldFail()
    {
        ArrayList list = ["a"];
        Action action = () => Assert.AreAllOfType(null, list);
        action.Should().Throw<AssertFailedException>()
            .WithMessage("Assert.AreAllOfType failed. The parameter 'expectedType' is invalid. The value cannot be null.");
    }

    public void AreAllOfType_NonGeneric_WithUserMessage_ShouldFail()
    {
        ArrayList list = [1];
        Action action = () => Assert.AreAllOfType(typeof(string), list, "User-provided message");
        action.Should().Throw<AssertFailedException>()
            .WithMessage(
                """
                Assertion failed. Expected all items in collection to be of the specified type.
                User-provided message

                expected type: System.String (or derived)
                mismatches:    [index 0: System.Int32]
                collection:    [1]

                Assert.AreAllOfType(typeof(string), list)
                """);
    }

    public void AreAllOfType_NonGeneric_DerivedTypes_ShouldPass()
    {
        ArrayList list = [new DerivedAllItemsA(), new DerivedAllItemsB()];
        Assert.AreAllOfType(typeof(DerivedAllItemsBase), list);
    }

    public void AreAllOfType_NonGeneric_NullElement_ShouldFail()
    {
        ArrayList list = ["a", null, "b"];
        Action action = () => Assert.AreAllOfType(typeof(string), list);
        action.Should().Throw<AssertFailedException>()
            .WithMessage(
                """
                Assertion failed. Expected all items in collection to be of the specified type.

                expected type: System.String (or derived)
                mismatches:    [index 1: <null>]
                collection:    ["a", null, "b"]

                Assert.AreAllOfType(typeof(string), list)
                """);
    }

    public void AreAllOfType_NonGeneric_Empty_ShouldPass()
    {
        ArrayList list = [];
        Assert.AreAllOfType(typeof(string), list);
    }

    public void AreAllOfType_Generic_BoxedValueType_AllMatch_ShouldPass()
        => Assert.AreAllOfType<int>(new object[] { 1, 2, 3 });

    public void AreAllOfType_Generic_BoxedValueType_HasMismatch_ShouldFail()
    {
        Action action = () => Assert.AreAllOfType<int>(new object[] { 1, "x" });
        action.Should().Throw<AssertFailedException>()
            .WithMessage(
                """
                Assertion failed. Expected all items in collection to be of the specified type.

                expected type: System.Int32 (or derived)
                mismatches:    [index 1: System.String]
                collection:    [1, "x"]

                Assert.AreAllOfType<TExpected>(new object[] { 1, "x" })
                """);
    }

    // Pins the current behavior of `AreAllOfType<T?>` for nullable value types: a `null` element fails because
    // `typeof(int?).IsInstanceOfType(null)` returns false. Boxed non-null nullable values are accepted because
    // boxing erases the `Nullable<T>` wrapper.
    public void AreAllOfType_Generic_NullableValueType_NullElement_ShouldFail()
    {
        Action action = () => Assert.AreAllOfType<int?>(new object?[] { 1, null, 3 });
        action.Should().Throw<AssertFailedException>()
            .WithMessage("*mismatches:    [index 1: <null>]*");
    }

    public void AreAllOfType_Generic_StructMismatch_ShouldFail()
    {
        Action action = () => Assert.AreAllOfType<DateTime>(new object[] { DateTime.UtcNow, "x" });
        action.Should().Throw<AssertFailedException>()
            .WithMessage(
                """
                *expected type: System.DateTime (or derived)
                mismatches:    [index 1: System.String]*
                """);
    }

    public void AreAllOfType_NonGeneric_BoxedValueType_AllMatch_ShouldPass()
    {
        ArrayList list = [1, 2, 3];
        Assert.AreAllOfType(typeof(int), list);
    }

    public void AreAllOfType_NonGeneric_BoxedValueType_HasMismatch_ShouldFail()
    {
        ArrayList list = [1, "x"];
        Action action = () => Assert.AreAllOfType(typeof(int), list);
        action.Should().Throw<AssertFailedException>()
            .WithMessage(
                """
                Assertion failed. Expected all items in collection to be of the specified type.

                expected type: System.Int32 (or derived)
                mismatches:    [index 1: System.String]
                collection:    [1, "x"]

                Assert.AreAllOfType(typeof(int), list)
                """);
    }

    public void AreAllOfType_Generic_Interface_AllImplement_ShouldPass()
        => Assert.AreAllOfType<IDisposable>(new object[] { new System.IO.MemoryStream(), new System.IO.MemoryStream() });

    public void AreAllOfType_NonGeneric_Interface_HasMismatch_ShouldFail()
    {
        ArrayList list = [new System.IO.MemoryStream(), "not-disposable-string"];
        Action action = () => Assert.AreAllOfType(typeof(IDisposable), list);
        action.Should().Throw<AssertFailedException>()
            .WithMessage("*expected type: System.IDisposable (or derived)*mismatches:    [index 1: System.String]*");
    }

    private class DerivedAllItemsBase;

    private sealed class DerivedAllItemsA : DerivedAllItemsBase;

    private sealed class DerivedAllItemsB : DerivedAllItemsBase;

    #endregion // AreAllOfType
}
