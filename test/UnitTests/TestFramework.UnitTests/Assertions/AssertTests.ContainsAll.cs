// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections;

using AwesomeAssertions;

using TestFramework.ForTestingMSTest;

namespace Microsoft.VisualStudio.TestPlatform.TestFramework.UnitTests;

public partial class AssertTests : TestContainer
{
    public void ContainsAll_Generic_AllPresent_ShouldPass()
        => Assert.ContainsAll(new[] { 1, 2 }, new[] { 1, 2, 3 });

    public void ContainsAll_Generic_EmptyExpected_ShouldPass()
        => Assert.ContainsAll(Array.Empty<int>(), new[] { 1, 2, 3 });

    public void ContainsAll_Generic_DuplicatesWithinCollectionMultiplicity_ShouldPass()
        => Assert.ContainsAll(new[] { 1, 1, 2 }, new[] { 1, 1, 2, 3 });

    public void ContainsAll_Generic_DuplicatesExceedCollectionMultiplicity_ShouldFail()
    {
        Action action = () => Assert.ContainsAll(new[] { 1, 1, 1 }, new[] { 1, 1 });
        action.Should().Throw<AssertFailedException>()
            .WithMessage(
                """
                Assertion failed. Expected collection to contain all specified items.

                missing:    [1]
                expected:   [1, 1, 1]
                collection: [1, 1]

                Assert.ContainsAll(new[] { 1, 1, 1 }, new[] { 1, 1 })
                """);
    }

    public void ContainsAll_Generic_MultipleMissing_PreservesFirstSeenOrderAndMultiplicity()
    {
        // Walk: 3 ✓, 1 ✓, 1 → missing(1), 4 → missing(4), 1 → missing(1)  =>  [1, 4, 1]
        Action action = () => Assert.ContainsAll(new[] { 3, 1, 1, 4, 1 }, new[] { 1, 2, 3 });
        action.Should().Throw<AssertFailedException>()
            .WithMessage(
                """
                Assertion failed. Expected collection to contain all specified items.

                missing:    [1, 4, 1]
                expected:   [3, 1, 1, 4, 1]
                collection: [1, 2, 3]

                Assert.ContainsAll(new[] { 3, 1, 1, 4, 1 }, new[] { 1, 2, 3 })
                """);
    }

    public void ContainsAll_Generic_ExcessInMiddleOfMatchingRun_ReportsOnlyExcess()
    {
        Action action = () => Assert.ContainsAll(new[] { 1, 2, 1, 3 }, new[] { 1, 2, 3 });
        action.Should().Throw<AssertFailedException>()
            .WithMessage(
                """
                Assertion failed. Expected collection to contain all specified items.

                missing:    [1]
                expected:   [1, 2, 1, 3]
                collection: [1, 2, 3]

                Assert.ContainsAll(new[] { 1, 2, 1, 3 }, new[] { 1, 2, 3 })
                """);
    }

    public void DoesNotContainAll_Generic_DuplicatesExceedCollectionMultiplicity_ShouldPass()
        => Assert.DoesNotContainAll(new[] { 1, 1 }, new[] { 1 });

    public void DoesNotContainAll_Generic_DuplicatesWithinCollectionMultiplicity_ShouldFail()
    {
        Action action = () => Assert.DoesNotContainAll(new[] { 1, 1 }, new[] { 1, 1, 2 });
        action.Should().Throw<AssertFailedException>()
            .WithMessage(
                """
                Assertion failed. Expected collection to not contain all specified items.

                expected:   [1, 1]
                collection: [1, 1, 2]

                Assert.DoesNotContainAll(new[] { 1, 1 }, new[] { 1, 1, 2 })
                """);
    }

    public void ContainsAll_Generic_MissingElement_ShouldFail()
    {
        Action action = () => Assert.ContainsAll(new[] { 1, 2, 3 }, new[] { 1, 2 });
        action.Should().Throw<AssertFailedException>()
            .WithMessage(
                """
                Assertion failed. Expected collection to contain all specified items.

                missing:    [3]
                expected:   [1, 2, 3]
                collection: [1, 2]

                Assert.ContainsAll(new[] { 1, 2, 3 }, new[] { 1, 2 })
                """);
    }

    public void ContainsAll_Generic_NullInExpectedButNotInCollection_ShouldFail()
    {
        Action action = () => Assert.ContainsAll(new string?[] { "a", null }, new string?[] { "a" });
        action.Should().Throw<AssertFailedException>()
            .WithMessage(
                """
                Assertion failed. Expected collection to contain all specified items.

                missing:    [null]
                expected:   ["a", null]
                collection: ["a"]

                Assert.ContainsAll(new string?[] { "a", null }, new string?[] { "a" })
                """);
    }

    public void ContainsAll_Generic_StringMessage_MissingElement_ShouldFail()
    {
        Action action = () => Assert.ContainsAll(new[] { 1, 2, 3 }, new[] { 1, 2 }, "User-provided message");
        action.Should().Throw<AssertFailedException>()
            .WithMessage(
                """
                Assertion failed. Expected collection to contain all specified items.
                User-provided message

                missing:    [3]
                expected:   [1, 2, 3]
                collection: [1, 2]

                Assert.ContainsAll(new[] { 1, 2, 3 }, new[] { 1, 2 })
                """);
    }

    public void ContainsAll_Generic_WithComparer_AllPresent_ShouldPass()
        => Assert.ContainsAll(new[] { "A" }, new[] { "a", "b" }, new CaseInsensitiveStringComparer());

    public void ContainsAll_Generic_WithComparer_MissingElement_ShouldFail()
    {
        Action action = () => Assert.ContainsAll(new[] { "A", "C" }, new[] { "a", "b" }, new CaseInsensitiveStringComparer());
        action.Should().Throw<AssertFailedException>()
            .WithMessage(
                """
                Assertion failed. Expected collection to contain all specified items.

                missing:    ["C"]
                expected:   ["A", "C"]
                collection: ["a", "b"]
                comparer:   CaseInsensitiveStringComparer

                Assert.ContainsAll(new[] { "A", "C" }, new[] { "a", "b" }, <comparer>)
                """);
    }

    public void ContainsAll_Generic_NullExpected_ShouldFail()
    {
        Action action = () => Assert.ContainsAll(null, new[] { 1, 2 });
        action.Should().Throw<AssertFailedException>()
            .WithMessage("Assert.ContainsAll failed. The parameter 'expected' is invalid. The value cannot be null.");
    }

    public void ContainsAll_Generic_NullCollection_ShouldFail()
    {
        Action action = () => Assert.ContainsAll(new[] { 1, 2 }, null);
        action.Should().Throw<AssertFailedException>()
            .WithMessage("Assert.ContainsAll failed. The parameter 'collection' is invalid. The value cannot be null.");
    }

    public void ContainsAll_Generic_NullComparer_ShouldFail()
    {
        Action action = () => Assert.ContainsAll(new[] { 1 }, new[] { 1 }, (IEqualityComparer<int>?)null);
        action.Should().Throw<AssertFailedException>()
            .WithMessage("Assert.ContainsAll failed. The parameter 'comparer' is invalid. The value cannot be null.");
    }

    public void ContainsAll_NonGeneric_AllPresent_ShouldPass()
        => Assert.ContainsAll(new ArrayList { 1, 2 }, new ArrayList { 1, 2, 3 });

    public void ContainsAll_NonGeneric_WithComparer_AllPresent_ShouldPass()
        => Assert.ContainsAll(new ArrayList { "A", "b" }, new ArrayList { "a", "B", "c" }, new CaseInsensitiveStringComparer());

    public void DoesNotContainAll_NonGeneric_WithComparer_MissingElement_ShouldPass()
        => Assert.DoesNotContainAll(new ArrayList { "A", "C" }, new ArrayList { "a", "b" }, new CaseInsensitiveStringComparer());

    public void ContainsAll_NonGeneric_MissingElement_ShouldFail()
    {
        IEnumerable expected = new ArrayList { 1, 2, 3 };
        IEnumerable collection = new ArrayList { 1, 2 };
        Action action = () => Assert.ContainsAll(expected, collection);
        action.Should().Throw<AssertFailedException>()
            .WithMessage(
                """
                Assertion failed. Expected collection to contain all specified items.

                missing:    [3]
                expected:   [1, 2, 3]
                collection: [1, 2]

                Assert.ContainsAll(expected, collection)
                """);
    }

    public void ContainsAll_NonGeneric_WithComparer_MissingElement_ShouldFail()
    {
        IEnumerable expected = new ArrayList { "A", "C" };
        IEnumerable collection = new ArrayList { "a", "b" };
        Action action = () => Assert.ContainsAll(expected, collection, new CaseInsensitiveStringComparer());
        action.Should().Throw<AssertFailedException>()
            .WithMessage(
                """
                Assertion failed. Expected collection to contain all specified items.

                missing:    ["C"]
                expected:   ["A", "C"]
                collection: ["a", "b"]
                comparer:   CaseInsensitiveStringComparer

                Assert.ContainsAll(expected, collection, <comparer>)
                """);
    }

    public void DoesNotContainAll_Generic_MissingElement_ShouldPass()
        => Assert.DoesNotContainAll(new[] { 1, 2, 3 }, new[] { 1, 2 });

    public void DoesNotContainAll_Generic_AllPresent_ShouldFail()
    {
        Action action = () => Assert.DoesNotContainAll(new[] { 1, 2 }, new[] { 1, 2, 3 });
        action.Should().Throw<AssertFailedException>()
            .WithMessage(
                """
                Assertion failed. Expected collection to not contain all specified items.

                expected:   [1, 2]
                collection: [1, 2, 3]

                Assert.DoesNotContainAll(new[] { 1, 2 }, new[] { 1, 2, 3 })
                """);
    }

    public void DoesNotContainAll_Generic_StringMessage_AllPresent_ShouldFail()
    {
        Action action = () => Assert.DoesNotContainAll(new[] { 1 }, new[] { 1, 2 }, "User-provided message");
        action.Should().Throw<AssertFailedException>()
            .WithMessage(
                """
                Assertion failed. Expected collection to not contain all specified items.
                User-provided message

                expected:   [1]
                collection: [1, 2]

                Assert.DoesNotContainAll(new[] { 1 }, new[] { 1, 2 })
                """);
    }

    public void DoesNotContainAll_Generic_WithComparer_MissingElement_ShouldPass()
        => Assert.DoesNotContainAll(new[] { "A", "C" }, new[] { "a", "b" }, new CaseInsensitiveStringComparer());

    public void DoesNotContainAll_Generic_WithComparer_AllPresent_ShouldFail()
    {
        Action action = () => Assert.DoesNotContainAll(new[] { "A" }, new[] { "a", "b" }, new CaseInsensitiveStringComparer());
        action.Should().Throw<AssertFailedException>()
            .WithMessage(
                """
                Assertion failed. Expected collection to not contain all specified items.

                expected:   ["A"]
                collection: ["a", "b"]
                comparer:   CaseInsensitiveStringComparer

                Assert.DoesNotContainAll(new[] { "A" }, new[] { "a", "b" }, <comparer>)
                """);
    }

    public void DoesNotContainAll_Generic_NullExpected_ShouldFail()
    {
        Action action = () => Assert.DoesNotContainAll(null, new[] { 1, 2 });
        action.Should().Throw<AssertFailedException>()
            .WithMessage("Assert.DoesNotContainAll failed. The parameter 'expected' is invalid. The value cannot be null.");
    }

    public void DoesNotContainAll_Generic_NullCollection_ShouldFail()
    {
        Action action = () => Assert.DoesNotContainAll(new[] { 1, 2 }, null);
        action.Should().Throw<AssertFailedException>()
            .WithMessage("Assert.DoesNotContainAll failed. The parameter 'collection' is invalid. The value cannot be null.");
    }

    public void DoesNotContainAll_Generic_NullComparer_ShouldFail()
    {
        Action action = () => Assert.DoesNotContainAll(new[] { 1 }, new[] { 1 }, (IEqualityComparer<int>?)null);
        action.Should().Throw<AssertFailedException>()
            .WithMessage("Assert.DoesNotContainAll failed. The parameter 'comparer' is invalid. The value cannot be null.");
    }

    public void ContainsAll_NonGeneric_NullExpected_ShouldFail()
    {
        Action action = () => Assert.ContainsAll(null, new ArrayList { 1, 2 });
        action.Should().Throw<AssertFailedException>()
            .WithMessage("Assert.ContainsAll failed. The parameter 'expected' is invalid. The value cannot be null.");
    }

    public void ContainsAll_NonGeneric_NullCollection_ShouldFail()
    {
        Action action = () => Assert.ContainsAll(new ArrayList { 1 }, null);
        action.Should().Throw<AssertFailedException>()
            .WithMessage("Assert.ContainsAll failed. The parameter 'collection' is invalid. The value cannot be null.");
    }

    public void ContainsAll_NonGeneric_NullComparer_ShouldFail()
    {
        Action action = () => Assert.ContainsAll(new ArrayList { 1 }, new ArrayList { 1 }, (IEqualityComparer?)null);
        action.Should().Throw<AssertFailedException>()
            .WithMessage("Assert.ContainsAll failed. The parameter 'comparer' is invalid. The value cannot be null.");
    }

    public void DoesNotContainAll_NonGeneric_NullExpected_ShouldFail()
    {
        Action action = () => Assert.DoesNotContainAll(null, new ArrayList { 1, 2 });
        action.Should().Throw<AssertFailedException>()
            .WithMessage("Assert.DoesNotContainAll failed. The parameter 'expected' is invalid. The value cannot be null.");
    }

    public void DoesNotContainAll_NonGeneric_NullCollection_ShouldFail()
    {
        Action action = () => Assert.DoesNotContainAll(new ArrayList { 1 }, null);
        action.Should().Throw<AssertFailedException>()
            .WithMessage("Assert.DoesNotContainAll failed. The parameter 'collection' is invalid. The value cannot be null.");
    }

    public void DoesNotContainAll_NonGeneric_NullComparer_ShouldFail()
    {
        Action action = () => Assert.DoesNotContainAll(new ArrayList { 1 }, new ArrayList { 1 }, (IEqualityComparer?)null);
        action.Should().Throw<AssertFailedException>()
            .WithMessage("Assert.DoesNotContainAll failed. The parameter 'comparer' is invalid. The value cannot be null.");
    }

    public void DoesNotContainAll_Generic_BothEmpty_ShouldFail()
    {
        Action action = () => Assert.DoesNotContainAll(Array.Empty<int>(), Array.Empty<int>());
        action.Should().Throw<AssertFailedException>()
            .WithMessage(
                """
                Assertion failed. Expected collection to not contain all specified items.

                expected:   []
                collection: []

                Assert.DoesNotContainAll(Array.Empty<int>(), Array.Empty<int>())
                """);
    }

    public void DoesNotContainAll_Generic_EmptyExpected_ShouldFail()
    {
        Action action = () => Assert.DoesNotContainAll(Array.Empty<int>(), new[] { 1 });
        action.Should().Throw<AssertFailedException>()
            .WithMessage(
                """
                Assertion failed. Expected collection to not contain all specified items.

                expected:   []
                collection: [1]

                Assert.DoesNotContainAll(Array.Empty<int>(), new[] { 1 })
                """);
    }

    public void DoesNotContainAll_NonGeneric_WithComparer_AllPresent_ShouldFail()
    {
        IEnumerable expected = new ArrayList { "A" };
        IEnumerable collection = new ArrayList { "a", "b" };
        Action action = () => Assert.DoesNotContainAll(expected, collection, new CaseInsensitiveStringComparer());
        action.Should().Throw<AssertFailedException>()
            .WithMessage(
                """
                Assertion failed. Expected collection to not contain all specified items.

                expected:   ["A"]
                collection: ["a", "b"]
                comparer:   CaseInsensitiveStringComparer

                Assert.DoesNotContainAll(expected, collection, <comparer>)
                """);
    }

    public void ContainsAll_Generic_NullInCollectionButNotInExpected_ShouldPass()
        => Assert.ContainsAll(new string?[] { "a" }, new string?[] { "a", null });

    public void ContainsAll_AssertFailedException_PopulatesExpectedAndActual()
    {
        Action action = () => Assert.ContainsAll(new[] { 1, 2, 3 }, new[] { 1, 2 });
        AssertFailedException ex = action.Should().Throw<AssertFailedException>().Which;
        ex.ExpectedText.Should().Be("[1, 2, 3]");
        ex.ActualText.Should().Be("[1, 2]");
    }

    public void DoesNotContainAll_AssertFailedException_PopulatesActualOnly()
    {
        Action action = () => Assert.DoesNotContainAll(new[] { 1 }, new[] { 1, 2 });
        AssertFailedException ex = action.Should().Throw<AssertFailedException>().Which;
        ex.ExpectedText.Should().BeNull();
        ex.ActualText.Should().Be("[1, 2]");
    }

    private sealed class CaseInsensitiveStringComparer : IEqualityComparer<string>, IEqualityComparer
    {
        public bool Equals(string? x, string? y) => StringComparer.OrdinalIgnoreCase.Equals(x, y);

        public int GetHashCode(string obj) => StringComparer.OrdinalIgnoreCase.GetHashCode(obj);

        bool IEqualityComparer.Equals(object? x, object? y) => StringComparer.OrdinalIgnoreCase.Equals(x as string, y as string);

        public int GetHashCode(object obj) => obj is string s ? StringComparer.OrdinalIgnoreCase.GetHashCode(s) : 0;
    }
}
