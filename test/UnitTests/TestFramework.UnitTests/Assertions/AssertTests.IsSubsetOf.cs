// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections;

using AwesomeAssertions;

using TestFramework.ForTestingMSTest;

namespace Microsoft.VisualStudio.TestPlatform.TestFramework.UnitTests;

public partial class AssertTests : TestContainer
{
    public void IsSubsetOf_Generic_AllPresent_ShouldPass()
        => Assert.IsSubsetOf(new[] { 1, 2 }, new[] { 1, 2, 3 });

    public void IsSubsetOf_Generic_EmptySubset_ShouldPass()
        => Assert.IsSubsetOf(Array.Empty<int>(), new[] { 1, 2, 3 });

    public void IsSubsetOf_Generic_DuplicatesWithinSupersetMultiplicity_ShouldPass()
        => Assert.IsSubsetOf(new[] { 1, 1, 2 }, new[] { 1, 1, 2, 3 });

    public void IsSubsetOf_Generic_DuplicatesExceedSupersetMultiplicity_ShouldFail()
    {
        Action action = () => Assert.IsSubsetOf(new[] { 1, 1, 1 }, new[] { 1, 1 });
        action.Should().Throw<AssertFailedException>()
            .WithMessage(
                """
                Assertion failed. Expected collection to be a subset of the specified superset.

                missing:  [1]
                subset:   [1, 1, 1]
                superset: [1, 1]

                Assert.IsSubsetOf(new[] { 1, 1, 1 }, new[] { 1, 1 })
                """);
    }

    public void IsSubsetOf_Generic_MissingElement_ShouldFail()
    {
        Action action = () => Assert.IsSubsetOf(new[] { 1, 2, 3 }, new[] { 1, 2 });
        action.Should().Throw<AssertFailedException>()
            .WithMessage(
                """
                Assertion failed. Expected collection to be a subset of the specified superset.

                missing:  [3]
                subset:   [1, 2, 3]
                superset: [1, 2]

                Assert.IsSubsetOf(new[] { 1, 2, 3 }, new[] { 1, 2 })
                """);
    }

    public void IsSubsetOf_Generic_NullInSubsetButNotInSuperset_ShouldFail()
    {
        Action action = () => Assert.IsSubsetOf(new string?[] { "a", null }, new string?[] { "a" });
        action.Should().Throw<AssertFailedException>()
            .WithMessage(
                """
                Assertion failed. Expected collection to be a subset of the specified superset.

                missing:  [null]
                subset:   ["a", null]
                superset: ["a"]

                Assert.IsSubsetOf(new string?[] { "a", null }, new string?[] { "a" })
                """);
    }

    public void IsSubsetOf_Generic_StringMessage_MissingElement_ShouldFail()
    {
        Action action = () => Assert.IsSubsetOf(new[] { 1, 2, 3 }, new[] { 1, 2 }, "User-provided message");
        action.Should().Throw<AssertFailedException>()
            .WithMessage(
                """
                Assertion failed. Expected collection to be a subset of the specified superset.
                User-provided message

                missing:  [3]
                subset:   [1, 2, 3]
                superset: [1, 2]

                Assert.IsSubsetOf(new[] { 1, 2, 3 }, new[] { 1, 2 })
                """);
    }

    public void IsSubsetOf_Generic_WithComparer_AllPresent_ShouldPass()
        => Assert.IsSubsetOf(new[] { "A" }, new[] { "a", "b" }, StringComparer.OrdinalIgnoreCase);

    public void IsSubsetOf_Generic_WithComparer_MissingElement_ShouldFail()
    {
        Action action = () => Assert.IsSubsetOf(new[] { "A", "C" }, new[] { "a", "b" }, StringComparer.OrdinalIgnoreCase);
        action.Should().Throw<AssertFailedException>()
            .WithMessage(
                """
                Assertion failed. Expected collection to be a subset of the specified superset.

                missing:  ["C"]
                subset:   ["A", "C"]
                superset: ["a", "b"]
                comparer: OrdinalIgnoreCaseComparer

                Assert.IsSubsetOf(new[] { "A", "C" }, new[] { "a", "b" }, <comparer>)
                """);
    }

    public void IsSubsetOf_Generic_NullSubset_ShouldFail()
    {
        Action action = () => Assert.IsSubsetOf(null, new[] { 1, 2 });
        action.Should().Throw<AssertFailedException>()
            .WithMessage("Assert.IsSubsetOf failed. The parameter 'subset' is invalid. The value cannot be null.");
    }

    public void IsSubsetOf_Generic_NullSuperset_ShouldFail()
    {
        Action action = () => Assert.IsSubsetOf(new[] { 1, 2 }, null);
        action.Should().Throw<AssertFailedException>()
            .WithMessage("Assert.IsSubsetOf failed. The parameter 'superset' is invalid. The value cannot be null.");
    }

    public void IsSubsetOf_Generic_NullComparer_ShouldFail()
    {
        Action action = () => Assert.IsSubsetOf(new[] { 1 }, new[] { 1 }, (IEqualityComparer<int>?)null);
        action.Should().Throw<AssertFailedException>()
            .WithMessage("Assert.IsSubsetOf failed. The parameter 'comparer' is invalid. The value cannot be null.");
    }

    public void IsSubsetOf_NonGeneric_AllPresent_ShouldPass()
        => Assert.IsSubsetOf(new ArrayList { 1, 2 }, new ArrayList { 1, 2, 3 });

    public void IsSubsetOf_NonGeneric_MissingElement_ShouldFail()
    {
        IEnumerable subset = new ArrayList { 1, 2, 3 };
        IEnumerable superset = new ArrayList { 1, 2 };
        Action action = () => Assert.IsSubsetOf(subset, superset);
        action.Should().Throw<AssertFailedException>()
            .WithMessage(
                """
                Assertion failed. Expected collection to be a subset of the specified superset.

                missing:  [3]
                subset:   [1, 2, 3]
                superset: [1, 2]

                Assert.IsSubsetOf(subset, superset)
                """);
    }

    public void IsSubsetOf_NonGeneric_WithComparer_MissingElement_ShouldFail()
    {
        IEnumerable subset = new ArrayList { "A", "C" };
        IEnumerable superset = new ArrayList { "a", "b" };
        Action action = () => Assert.IsSubsetOf(subset, superset, StringComparer.OrdinalIgnoreCase);
        action.Should().Throw<AssertFailedException>()
            .WithMessage(
                """
                Assertion failed. Expected collection to be a subset of the specified superset.

                missing:  ["C"]
                subset:   ["A", "C"]
                superset: ["a", "b"]
                comparer: OrdinalIgnoreCaseComparer

                Assert.IsSubsetOf(subset, superset, <comparer>)
                """);
    }

    public void IsNotSubsetOf_Generic_MissingElement_ShouldPass()
        => Assert.IsNotSubsetOf(new[] { 1, 2, 3 }, new[] { 1, 2 });

    public void IsNotSubsetOf_Generic_AllPresent_ShouldFail()
    {
        Action action = () => Assert.IsNotSubsetOf(new[] { 1, 2 }, new[] { 1, 2, 3 });
        action.Should().Throw<AssertFailedException>()
            .WithMessage(
                """
                Assertion failed. Expected collection to not be a subset of the specified superset.

                subset:   [1, 2]
                superset: [1, 2, 3]

                Assert.IsNotSubsetOf(new[] { 1, 2 }, new[] { 1, 2, 3 })
                """);
    }

    public void IsNotSubsetOf_Generic_StringMessage_AllPresent_ShouldFail()
    {
        Action action = () => Assert.IsNotSubsetOf(new[] { 1 }, new[] { 1, 2 }, "User-provided message");
        action.Should().Throw<AssertFailedException>()
            .WithMessage(
                """
                Assertion failed. Expected collection to not be a subset of the specified superset.
                User-provided message

                subset:   [1]
                superset: [1, 2]

                Assert.IsNotSubsetOf(new[] { 1 }, new[] { 1, 2 })
                """);
    }

    public void IsNotSubsetOf_Generic_WithComparer_MissingElement_ShouldPass()
        => Assert.IsNotSubsetOf(new[] { "A", "C" }, new[] { "a", "b" }, StringComparer.OrdinalIgnoreCase);

    public void IsNotSubsetOf_Generic_WithComparer_AllPresent_ShouldFail()
    {
        Action action = () => Assert.IsNotSubsetOf(new[] { "A" }, new[] { "a", "b" }, StringComparer.OrdinalIgnoreCase);
        action.Should().Throw<AssertFailedException>()
            .WithMessage(
                """
                Assertion failed. Expected collection to not be a subset of the specified superset.

                subset:   ["A"]
                superset: ["a", "b"]
                comparer: OrdinalIgnoreCaseComparer

                Assert.IsNotSubsetOf(new[] { "A" }, new[] { "a", "b" }, <comparer>)
                """);
    }

    public void IsNotSubsetOf_Generic_NullSubset_ShouldFail()
    {
        Action action = () => Assert.IsNotSubsetOf(null, new[] { 1, 2 });
        action.Should().Throw<AssertFailedException>()
            .WithMessage("Assert.IsNotSubsetOf failed. The parameter 'subset' is invalid. The value cannot be null.");
    }

    public void IsNotSubsetOf_Generic_NullSuperset_ShouldFail()
    {
        Action action = () => Assert.IsNotSubsetOf(new[] { 1, 2 }, null);
        action.Should().Throw<AssertFailedException>()
            .WithMessage("Assert.IsNotSubsetOf failed. The parameter 'superset' is invalid. The value cannot be null.");
    }

    public void IsNotSubsetOf_Generic_NullComparer_ShouldFail()
    {
        Action action = () => Assert.IsNotSubsetOf(new[] { 1 }, new[] { 1 }, (IEqualityComparer<int>?)null);
        action.Should().Throw<AssertFailedException>()
            .WithMessage("Assert.IsNotSubsetOf failed. The parameter 'comparer' is invalid. The value cannot be null.");
    }

    public void IsSubsetOf_NonGeneric_NullSubset_ShouldFail()
    {
        Action action = () => Assert.IsSubsetOf(null, new ArrayList { 1, 2 });
        action.Should().Throw<AssertFailedException>()
            .WithMessage("Assert.IsSubsetOf failed. The parameter 'subset' is invalid. The value cannot be null.");
    }

    public void IsSubsetOf_NonGeneric_NullSuperset_ShouldFail()
    {
        Action action = () => Assert.IsSubsetOf(new ArrayList { 1 }, null);
        action.Should().Throw<AssertFailedException>()
            .WithMessage("Assert.IsSubsetOf failed. The parameter 'superset' is invalid. The value cannot be null.");
    }

    public void IsSubsetOf_NonGeneric_NullComparer_ShouldFail()
    {
        Action action = () => Assert.IsSubsetOf(new ArrayList { 1 }, new ArrayList { 1 }, (IEqualityComparer?)null);
        action.Should().Throw<AssertFailedException>()
            .WithMessage("Assert.IsSubsetOf failed. The parameter 'comparer' is invalid. The value cannot be null.");
    }

    public void IsNotSubsetOf_NonGeneric_NullSubset_ShouldFail()
    {
        Action action = () => Assert.IsNotSubsetOf(null, new ArrayList { 1, 2 });
        action.Should().Throw<AssertFailedException>()
            .WithMessage("Assert.IsNotSubsetOf failed. The parameter 'subset' is invalid. The value cannot be null.");
    }

    public void IsNotSubsetOf_NonGeneric_NullSuperset_ShouldFail()
    {
        Action action = () => Assert.IsNotSubsetOf(new ArrayList { 1 }, null);
        action.Should().Throw<AssertFailedException>()
            .WithMessage("Assert.IsNotSubsetOf failed. The parameter 'superset' is invalid. The value cannot be null.");
    }

    public void IsNotSubsetOf_NonGeneric_NullComparer_ShouldFail()
    {
        Action action = () => Assert.IsNotSubsetOf(new ArrayList { 1 }, new ArrayList { 1 }, (IEqualityComparer?)null);
        action.Should().Throw<AssertFailedException>()
            .WithMessage("Assert.IsNotSubsetOf failed. The parameter 'comparer' is invalid. The value cannot be null.");
    }

    public void IsNotSubsetOf_Generic_BothEmpty_ShouldFail()
    {
        Action action = () => Assert.IsNotSubsetOf(Array.Empty<int>(), Array.Empty<int>());
        action.Should().Throw<AssertFailedException>()
            .WithMessage(
                """
                Assertion failed. Expected collection to not be a subset of the specified superset.

                subset:   []
                superset: []

                Assert.IsNotSubsetOf(Array.Empty<int>(), Array.Empty<int>())
                """);
    }

    public void IsNotSubsetOf_Generic_EmptySubset_ShouldFail()
    {
        Action action = () => Assert.IsNotSubsetOf(Array.Empty<int>(), new[] { 1 });
        action.Should().Throw<AssertFailedException>()
            .WithMessage(
                """
                Assertion failed. Expected collection to not be a subset of the specified superset.

                subset:   []
                superset: [1]

                Assert.IsNotSubsetOf(Array.Empty<int>(), new[] { 1 })
                """);
    }

    public void IsNotSubsetOf_NonGeneric_WithComparer_AllPresent_ShouldFail()
    {
        IEnumerable subset = new ArrayList { "A" };
        IEnumerable superset = new ArrayList { "a", "b" };
        Action action = () => Assert.IsNotSubsetOf(subset, superset, StringComparer.OrdinalIgnoreCase);
        action.Should().Throw<AssertFailedException>()
            .WithMessage(
                """
                Assertion failed. Expected collection to not be a subset of the specified superset.

                subset:   ["A"]
                superset: ["a", "b"]
                comparer: OrdinalIgnoreCaseComparer

                Assert.IsNotSubsetOf(subset, superset, <comparer>)
                """);
    }

    public void IsSubsetOf_Generic_NullInSupersetButNotInSubset_ShouldPass()
        => Assert.IsSubsetOf(new string?[] { "a" }, new string?[] { "a", null });

    public void IsSubsetOf_AssertFailedException_PopulatesExpectedAndActual()
    {
        Action action = () => Assert.IsSubsetOf(new[] { 1, 2, 3 }, new[] { 1, 2 });
        AssertFailedException ex = action.Should().Throw<AssertFailedException>().Which;
        ex.ExpectedText.Should().Be("[1, 2]");
        ex.ActualText.Should().Be("[1, 2, 3]");
    }

    public void IsNotSubsetOf_AssertFailedException_PopulatesActualOnly()
    {
        Action action = () => Assert.IsNotSubsetOf(new[] { 1 }, new[] { 1, 2 });
        AssertFailedException ex = action.Should().Throw<AssertFailedException>().Which;
        ex.ExpectedText.Should().BeNull();
        ex.ActualText.Should().Be("[1]");
    }
}
