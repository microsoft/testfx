// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections;

using AwesomeAssertions;

using TestFramework.ForTestingMSTest;

namespace Microsoft.VisualStudio.TestPlatform.TestFramework.UnitTests;

public partial class AssertTests : TestContainer
{
    #region AllItemsAreNotNull

    public void AllItemsAreNotNull_Generic_NoNulls_ShouldPass()
        => Assert.AllItemsAreNotNull(new[] { "a", "b", "c" });

    public void AllItemsAreNotNull_Generic_Empty_ShouldPass()
        => Assert.AllItemsAreNotNull(Array.Empty<string>());

    public void AllItemsAreNotNull_Generic_ValueTypes_ShouldPass()
        => Assert.AllItemsAreNotNull(new[] { 1, 2, 3 });

    public void AllItemsAreNotNull_Generic_HasNull_ShouldFail()
    {
        Action action = () => Assert.AllItemsAreNotNull(new[] { "a", null, "b", null });
        action.Should().Throw<AssertFailedException>()
            .WithMessage(
                """
                Assertion failed. Expected all items in collection to be non-null.

                null indices: [1, 3]
                collection:   ["a", null, "b", null]

                Assert.AllItemsAreNotNull(new[] { "a", null, "b", null })
                """);
    }

    public void AllItemsAreNotNull_Generic_WithUserMessage_ShouldFail()
    {
        Action action = () => Assert.AllItemsAreNotNull(new[] { "a", null }, "User-provided message");
        action.Should().Throw<AssertFailedException>()
            .WithMessage(
                """
                Assertion failed. Expected all items in collection to be non-null.
                User-provided message

                null indices: [1]
                collection:   ["a", null]

                Assert.AllItemsAreNotNull(new[] { "a", null })
                """);
    }

    public void AllItemsAreNotNull_Generic_NullCollection_ShouldFail()
    {
        Action action = () => Assert.AllItemsAreNotNull((IEnumerable<string>?)null);
        action.Should().Throw<AssertFailedException>()
            .WithMessage("Assert.AllItemsAreNotNull failed. The parameter 'collection' is invalid. The value cannot be null.");
    }

    public void AllItemsAreNotNull_NonGeneric_NoNulls_ShouldPass()
    {
        ArrayList list = ["a", "b", "c"];
        Assert.AllItemsAreNotNull(list);
    }

    public void AllItemsAreNotNull_NonGeneric_HasNull_ShouldFail()
    {
        ArrayList list = ["a", null, "b"];
        Action action = () => Assert.AllItemsAreNotNull(list);
        action.Should().Throw<AssertFailedException>()
            .WithMessage(
                """
                Assertion failed. Expected all items in collection to be non-null.

                null indices: [1]
                collection:   ["a", null, "b"]

                Assert.AllItemsAreNotNull(list)
                """);
    }

    public void AllItemsAreNotNull_NonGeneric_NullCollection_ShouldFail()
    {
        Action action = () => Assert.AllItemsAreNotNull(null);
        action.Should().Throw<AssertFailedException>()
            .WithMessage("Assert.AllItemsAreNotNull failed. The parameter 'collection' is invalid. The value cannot be null.");
    }

    public void AllItemsAreNotNull_NonGeneric_WithUserMessage_ShouldFail()
    {
        ArrayList list = ["a", null];
        Action action = () => Assert.AllItemsAreNotNull(list, "User-provided message");
        action.Should().Throw<AssertFailedException>()
            .WithMessage(
                """
                Assertion failed. Expected all items in collection to be non-null.
                User-provided message

                null indices: [1]
                collection:   ["a", null]

                Assert.AllItemsAreNotNull(list)
                """);
    }

    public void AllItemsAreNotNull_Generic_NullableValueType_HasNull_ShouldFail()
    {
        Action action = () => Assert.AllItemsAreNotNull(new int?[] { 1, null, 3 });
        action.Should().Throw<AssertFailedException>()
            .WithMessage(
                """
                Assertion failed. Expected all items in collection to be non-null.

                null indices: [1]
                collection:   [1, null, 3]

                Assert.AllItemsAreNotNull(new int?[] { 1, null, 3 })
                """);
    }

    public void AllItemsAreNotNull_Generic_LazyEnumerable_HasNull_ShouldFail()
    {
        static IEnumerable<string?> Lazy()
        {
            yield return "a";
            yield return null;
            yield return "b";
        }

        Action action = () => Assert.AllItemsAreNotNull(Lazy());
        action.Should().Throw<AssertFailedException>()
            .WithMessage("*null indices: [1]*collection:*");
    }

    #endregion // AllItemsAreNotNull

    #region AllItemsAreUnique

    public void AllItemsAreUnique_Generic_AllUnique_ShouldPass()
        => Assert.AllItemsAreUnique(new[] { 1, 2, 3 });

    public void AllItemsAreUnique_Generic_Empty_ShouldPass()
        => Assert.AllItemsAreUnique(Array.Empty<int>());

    public void AllItemsAreUnique_Generic_SingleNull_ShouldPass()
        => Assert.AllItemsAreUnique(new string?[] { "a", null, "b" });

    public void AllItemsAreUnique_Generic_HasDuplicate_ShouldFail()
    {
        Action action = () => Assert.AllItemsAreUnique(new[] { 1, 2, 3, 4, 3 });
        action.Should().Throw<AssertFailedException>()
            .WithMessage(
                """
                Assertion failed. Expected all items in collection to be unique.

                duplicates: [3]
                collection: [1, 2, 3, 4, 3]

                Assert.AllItemsAreUnique(new[] { 1, 2, 3, 4, 3 })
                """);
    }

    public void AllItemsAreUnique_Generic_HasMultipleDuplicates_ShouldFail()
    {
        Action action = () => Assert.AllItemsAreUnique(new[] { 1, 2, 3, 2, 1, 1 });
        action.Should().Throw<AssertFailedException>()
            .WithMessage(
                """
                Assertion failed. Expected all items in collection to be unique.

                duplicates: [2, 1]
                collection: [1, 2, 3, 2, 1, 1]

                Assert.AllItemsAreUnique(new[] { 1, 2, 3, 2, 1, 1 })
                """);
    }

    public void AllItemsAreUnique_Generic_NullDuplicate_ShouldFail()
    {
        Action action = () => Assert.AllItemsAreUnique(new string?[] { "a", null, "b", null });
        action.Should().Throw<AssertFailedException>()
            .WithMessage(
                """
                Assertion failed. Expected all items in collection to be unique.

                duplicates: [null]
                collection: ["a", null, "b", null]

                Assert.AllItemsAreUnique(new string?[] { "a", null, "b", null })
                """);
    }

    public void AllItemsAreUnique_Generic_WithUserMessage_ShouldFail()
    {
        Action action = () => Assert.AllItemsAreUnique(new[] { 1, 1 }, "User-provided message");
        action.Should().Throw<AssertFailedException>()
            .WithMessage(
                """
                Assertion failed. Expected all items in collection to be unique.
                User-provided message

                duplicates: [1]
                collection: [1, 1]

                Assert.AllItemsAreUnique(new[] { 1, 1 })
                """);
    }

    public void AllItemsAreUnique_Generic_WithComparer_AllUnique_ShouldPass()
        => Assert.AllItemsAreUnique(new[] { "a", "B", "c" }, StringComparer.OrdinalIgnoreCase);

    public void AllItemsAreUnique_Generic_WithComparer_HasDuplicate_ShouldFail()
    {
        Action action = () => Assert.AllItemsAreUnique(new[] { "A", "B", "a" }, StringComparer.OrdinalIgnoreCase);
        action.Should().Throw<AssertFailedException>()
            .WithMessage(
                """
                Assertion failed. Expected all items in collection to be unique.

                duplicates: ["a"]
                collection: ["A", "B", "a"]

                Assert.AllItemsAreUnique(new[] { "A", "B", "a" }, <comparer>)
                """);
    }

    public void AllItemsAreUnique_Generic_NullCollection_ShouldFail()
    {
        Action action = () => Assert.AllItemsAreUnique((IEnumerable<int>?)null);
        action.Should().Throw<AssertFailedException>()
            .WithMessage("Assert.AllItemsAreUnique failed. The parameter 'collection' is invalid. The value cannot be null.");
    }

    public void AllItemsAreUnique_Generic_NullComparer_ShouldFail()
    {
        Action action = () => Assert.AllItemsAreUnique(new[] { 1, 2 }, (IEqualityComparer<int>?)null);
        action.Should().Throw<AssertFailedException>()
            .WithMessage("Assert.AllItemsAreUnique failed. The parameter 'comparer' is invalid. The value cannot be null.");
    }

    public void AllItemsAreUnique_NonGeneric_AllUnique_ShouldPass()
    {
        ArrayList list = [1, "a", 3.5];
        Assert.AllItemsAreUnique(list);
    }

    public void AllItemsAreUnique_NonGeneric_HasDuplicate_ShouldFail()
    {
        ArrayList list = [1, 2, 3, 2];
        Action action = () => Assert.AllItemsAreUnique(list);
        action.Should().Throw<AssertFailedException>()
            .WithMessage(
                """
                Assertion failed. Expected all items in collection to be unique.

                duplicates: [2]
                collection: [1, 2, 3, 2]

                Assert.AllItemsAreUnique(list)
                """);
    }

    public void AllItemsAreUnique_NonGeneric_WithComparer_HasDuplicate_ShouldFail()
    {
        ArrayList list = ["A", "B", "a"];
        Action action = () => Assert.AllItemsAreUnique(list, StringComparer.OrdinalIgnoreCase);
        action.Should().Throw<AssertFailedException>()
            .WithMessage(
                """
                Assertion failed. Expected all items in collection to be unique.

                duplicates: ["a"]
                collection: ["A", "B", "a"]

                Assert.AllItemsAreUnique(list, <comparer>)
                """);
    }

    public void AllItemsAreUnique_NonGeneric_NullCollection_ShouldFail()
    {
        Action action = () => Assert.AllItemsAreUnique(null);
        action.Should().Throw<AssertFailedException>()
            .WithMessage("Assert.AllItemsAreUnique failed. The parameter 'collection' is invalid. The value cannot be null.");
    }

    public void AllItemsAreUnique_NonGeneric_NullComparer_ShouldFail()
    {
        ArrayList list = [1, 2];
        Action action = () => Assert.AllItemsAreUnique(list, (IEqualityComparer?)null);
        action.Should().Throw<AssertFailedException>()
            .WithMessage("Assert.AllItemsAreUnique failed. The parameter 'comparer' is invalid. The value cannot be null.");
    }

    public void AllItemsAreUnique_NonGeneric_WithUserMessage_ShouldFail()
    {
        ArrayList list = [1, 1];
        Action action = () => Assert.AllItemsAreUnique(list, "User-provided message");
        action.Should().Throw<AssertFailedException>()
            .WithMessage(
                """
                Assertion failed. Expected all items in collection to be unique.
                User-provided message

                duplicates: [1]
                collection: [1, 1]

                Assert.AllItemsAreUnique(list)
                """);
    }

    public void AllItemsAreUnique_NonGeneric_SingleNull_ShouldPass()
    {
        ArrayList list = ["a", null, "b"];
        Assert.AllItemsAreUnique(list);
    }

    public void AllItemsAreUnique_NonGeneric_NullDuplicate_ShouldFail()
    {
        ArrayList list = ["a", null, null];
        Action action = () => Assert.AllItemsAreUnique(list);
        action.Should().Throw<AssertFailedException>()
            .WithMessage(
                """
                Assertion failed. Expected all items in collection to be unique.

                duplicates: [null]
                collection: ["a", null, null]

                Assert.AllItemsAreUnique(list)
                """);
    }

    public void AllItemsAreUnique_Generic_ManyNulls_ReportsNullOnce_ShouldFail()
    {
        Action action = () => Assert.AllItemsAreUnique(new string?[] { null, "a", null, "b", null });
        action.Should().Throw<AssertFailedException>()
            .WithMessage(
                """
                Assertion failed. Expected all items in collection to be unique.

                duplicates: [null]
                collection: [null, "a", null, "b", null]

                Assert.AllItemsAreUnique(new string?[] { null, "a", null, "b", null })
                """);
    }

    public void AllItemsAreUnique_Generic_ManyDuplicatesOfSameValue_ReportsValueOnce_ShouldFail()
    {
        Action action = () => Assert.AllItemsAreUnique(new[] { 5, 5, 5, 5 });
        action.Should().Throw<AssertFailedException>()
            .WithMessage(
                """
                Assertion failed. Expected all items in collection to be unique.

                duplicates: [5]
                collection: [5, 5, 5, 5]

                Assert.AllItemsAreUnique(new[] { 5, 5, 5, 5 })
                """);
    }

    #endregion // AllItemsAreUnique

    #region AllItemsAreInstancesOfType

    public void AllItemsAreInstancesOfType_Generic_AllMatch_ShouldPass()
        => Assert.AllItemsAreInstancesOfType<string>(new object[] { "a", "b", "c" });

    public void AllItemsAreInstancesOfType_Generic_DerivedTypes_ShouldPass()
        => Assert.AllItemsAreInstancesOfType<DerivedAllItemsBase>(new object[] { new DerivedAllItemsA(), new DerivedAllItemsB() });

    public void AllItemsAreInstancesOfType_Generic_BaseTypeAcceptsDerived_ShouldPass()
        => Assert.AllItemsAreInstancesOfType<object>(new object[] { 1, "two", 3.0 });

    public void AllItemsAreInstancesOfType_Generic_Empty_ShouldPass()
        => Assert.AllItemsAreInstancesOfType<string>(Array.Empty<object>());

    public void AllItemsAreInstancesOfType_Generic_NullElement_ShouldPass()
        => Assert.AllItemsAreInstancesOfType<string>(new object?[] { "a", null, "b" });

    public void AllItemsAreInstancesOfType_Generic_HasMismatch_ShouldFail()
    {
        Action action = () => Assert.AllItemsAreInstancesOfType<string>(new object[] { "a", 42, "b", true });
        action.Should().Throw<AssertFailedException>()
            .WithMessage(
                """
                Assertion failed. Expected all items in collection to be instances of the specified type.

                expected type: System.String (or derived)
                mismatches:    [index 1: System.Int32, index 3: System.Boolean]
                collection:    ["a", 42, "b", true]

                Assert.AllItemsAreInstancesOfType<TExpected>(new object[] { "a", 42, "b", true })
                """);
    }

    public void AllItemsAreInstancesOfType_Generic_WithUserMessage_ShouldFail()
    {
        Action action = () => Assert.AllItemsAreInstancesOfType<string>(new object[] { 1 }, "User-provided message");
        action.Should().Throw<AssertFailedException>()
            .WithMessage(
                """
                Assertion failed. Expected all items in collection to be instances of the specified type.
                User-provided message

                expected type: System.String (or derived)
                mismatches:    [index 0: System.Int32]
                collection:    [1]

                Assert.AllItemsAreInstancesOfType<TExpected>(new object[] { 1 })
                """);
    }

    public void AllItemsAreInstancesOfType_Generic_NullCollection_ShouldFail()
    {
        Action action = () => Assert.AllItemsAreInstancesOfType<string>(null);
        action.Should().Throw<AssertFailedException>()
            .WithMessage("Assert.AllItemsAreInstancesOfType failed. The parameter 'collection' is invalid. The value cannot be null.");
    }

    public void AllItemsAreInstancesOfType_NonGeneric_AllMatch_ShouldPass()
    {
        ArrayList list = ["a", "b"];
        Assert.AllItemsAreInstancesOfType(list, typeof(string));
    }

    public void AllItemsAreInstancesOfType_NonGeneric_HasMismatch_ShouldFail()
    {
        ArrayList list = ["a", 42];
        Action action = () => Assert.AllItemsAreInstancesOfType(list, typeof(string));
        action.Should().Throw<AssertFailedException>()
            .WithMessage(
                """
                Assertion failed. Expected all items in collection to be instances of the specified type.

                expected type: System.String (or derived)
                mismatches:    [index 1: System.Int32]
                collection:    ["a", 42]

                Assert.AllItemsAreInstancesOfType(list, typeof(string))
                """);
    }

    public void AllItemsAreInstancesOfType_NonGeneric_NullCollection_ShouldFail()
    {
        Action action = () => Assert.AllItemsAreInstancesOfType(null, typeof(string));
        action.Should().Throw<AssertFailedException>()
            .WithMessage("Assert.AllItemsAreInstancesOfType failed. The parameter 'collection' is invalid. The value cannot be null.");
    }

    public void AllItemsAreInstancesOfType_NonGeneric_NullExpectedType_ShouldFail()
    {
        ArrayList list = ["a"];
        Action action = () => Assert.AllItemsAreInstancesOfType(list, null);
        action.Should().Throw<AssertFailedException>()
            .WithMessage("Assert.AllItemsAreInstancesOfType failed. The parameter 'expectedType' is invalid. The value cannot be null.");
    }

    public void AllItemsAreInstancesOfType_NonGeneric_WithUserMessage_ShouldFail()
    {
        ArrayList list = [1];
        Action action = () => Assert.AllItemsAreInstancesOfType(list, typeof(string), "User-provided message");
        action.Should().Throw<AssertFailedException>()
            .WithMessage(
                """
                Assertion failed. Expected all items in collection to be instances of the specified type.
                User-provided message

                expected type: System.String (or derived)
                mismatches:    [index 0: System.Int32]
                collection:    [1]

                Assert.AllItemsAreInstancesOfType(list, typeof(string))
                """);
    }

    public void AllItemsAreInstancesOfType_NonGeneric_DerivedTypes_ShouldPass()
    {
        ArrayList list = [new DerivedAllItemsA(), new DerivedAllItemsB()];
        Assert.AllItemsAreInstancesOfType(list, typeof(DerivedAllItemsBase));
    }

    public void AllItemsAreInstancesOfType_NonGeneric_NullElement_ShouldPass()
    {
        ArrayList list = ["a", null, "b"];
        Assert.AllItemsAreInstancesOfType(list, typeof(string));
    }

    public void AllItemsAreInstancesOfType_NonGeneric_Empty_ShouldPass()
    {
        ArrayList list = [];
        Assert.AllItemsAreInstancesOfType(list, typeof(string));
    }

    private class DerivedAllItemsBase;

    private sealed class DerivedAllItemsA : DerivedAllItemsBase;

    private sealed class DerivedAllItemsB : DerivedAllItemsBase;

    #endregion // AllItemsAreInstancesOfType
}
