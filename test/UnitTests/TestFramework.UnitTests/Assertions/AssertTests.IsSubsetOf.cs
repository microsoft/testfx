// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections;

using AwesomeAssertions;

using TestFramework.ForTestingMSTest;

namespace Microsoft.VisualStudio.TestPlatform.TestFramework.UnitTests;

public partial class AssertTests : TestContainer
{
    public void IsSubsetOf_Generic_AllItemsPresent_ShouldPass()
        => Assert.IsSubsetOf([1, 2], [1, 2, 3]);

    public void IsSubsetOf_Generic_EmptySubset_ShouldPass()
        => Assert.IsSubsetOf<int>([], [1, 2, 3]);

    public void IsSubsetOf_Generic_DuplicateMultiplicityPresent_ShouldPass()
        => Assert.IsSubsetOf([1, 1], [1, 1, 2]);

    public void IsSubsetOf_Generic_DuplicateMultiplicityMissing_ShouldFail()
    {
        Action action = () => Assert.IsSubsetOf([1, 1, 1], [1, 1], "User-provided message");

        action.Should().Throw<AssertFailedException>()
            .WithMessage(
                """
                Assertion failed. Expected subset to be a subset of superset.
                User-provided message

                missing:  [1]
                subset:   [1, 1, 1]
                superset: [1, 1]

                Assert.IsSubsetOf([1, 1, 1], [1, 1])
                """);
    }

    public void IsSubsetOf_Generic_WithComparer_ShouldUseComparer()
        => Assert.IsSubsetOf(["A"], ["a", "b"], new CaseInsensitiveStringComparer());

    public void IsSubsetOf_Generic_NullSubset_ShouldFail()
    {
        IEnumerable<int> superset = [1, 2];
        Action action = () => Assert.IsSubsetOf(null, superset);

        action.Should().Throw<AssertFailedException>()
            .WithMessage("Assert.IsSubsetOf failed. The parameter 'subset' is invalid. The value cannot be null.");
    }

    public void IsSubsetOf_Generic_NullSuperset_ShouldFail()
    {
        IEnumerable<int>? superset = null;
        Action action = () => Assert.IsSubsetOf([1], superset);

        action.Should().Throw<AssertFailedException>()
            .WithMessage("Assert.IsSubsetOf failed. The parameter 'superset' is invalid. The value cannot be null.");
    }

    public void IsSubsetOf_Generic_NullComparer_ShouldFail()
    {
        Action action = () => Assert.IsSubsetOf([1], [1], (IEqualityComparer<int>?)null);

        action.Should().Throw<AssertFailedException>()
            .WithMessage("Assert.IsSubsetOf failed. The parameter 'comparer' is invalid. The value cannot be null.");
    }

    public void IsSubsetOf_NonGeneric_WithComparer_ShouldPass()
        => Assert.IsSubsetOf(new ArrayList { "A" }, new ArrayList { "a", "b" }, new CaseInsensitiveStringComparer());

    public void IsSubsetOf_AssertFailedException_ShouldPopulateExpectedAndActual()
    {
        Action action = () => Assert.IsSubsetOf([1, 2], [1]);

        AssertFailedException exception = action.Should().Throw<AssertFailedException>().Which;
        exception.ExpectedText.Should().Be("[1, 2]");
        exception.ActualText.Should().Be("[1]");
    }

    public void IsNotSubsetOf_Generic_MissingItem_ShouldPass()
        => Assert.IsNotSubsetOf([1, 3], [1, 2]);

    public void IsNotSubsetOf_Generic_AllItemsPresent_ShouldFail()
    {
        Action action = () => Assert.IsNotSubsetOf([1, 2], [1, 2, 3]);

        action.Should().Throw<AssertFailedException>()
            .WithMessage(
                """
                Assertion failed. Expected subset to not be a subset of superset.

                subset:   [1, 2]
                superset: [1, 2, 3]

                Assert.IsNotSubsetOf([1, 2], [1, 2, 3])
                """);
    }

    public void IsNotSubsetOf_Generic_EmptySubset_ShouldFail()
    {
        Action action = () => Assert.IsNotSubsetOf<int>([], [1]);

        action.Should().Throw<AssertFailedException>();
    }

    public void IsNotSubsetOf_Generic_WithComparer_ShouldUseComparer()
        => Assert.IsNotSubsetOf(["A", "C"], ["a", "b"], new CaseInsensitiveStringComparer());

    public void IsNotSubsetOf_NonGeneric_WithComparer_ShouldFail()
    {
        Action action = () => Assert.IsNotSubsetOf(
            new ArrayList { "A" },
            new ArrayList { "a", "b" },
            new CaseInsensitiveStringComparer());

        action.Should().Throw<AssertFailedException>();
    }

#if NETCOREAPP3_1_OR_GREATER

    public void IsSubsetOf_SpanAndMemoryOverloads_ShouldPass()
    {
        Span<int> spanSubset = [1];
        Span<int> spanSuperset = [1, 2];
        ReadOnlySpan<int> readOnlySpanSubset = spanSubset;
        ReadOnlySpan<int> readOnlySpanSuperset = spanSuperset;
        Memory<int> memorySubset = new[] { 1 }.AsMemory();
        Memory<int> memorySuperset = new[] { 1, 2 }.AsMemory();
        ReadOnlyMemory<int> readOnlyMemorySubset = memorySubset;
        ReadOnlyMemory<int> readOnlyMemorySuperset = memorySuperset;

        Assert.IsSubsetOf(spanSubset, spanSuperset);
        Assert.IsSubsetOf(spanSubset, spanSuperset, EqualityComparer<int>.Default);
        Assert.IsSubsetOf(readOnlySpanSubset, readOnlySpanSuperset);
        Assert.IsSubsetOf(readOnlySpanSubset, readOnlySpanSuperset, EqualityComparer<int>.Default);
        Assert.IsSubsetOf(memorySubset, memorySuperset);
        Assert.IsSubsetOf(memorySubset, memorySuperset, EqualityComparer<int>.Default);
        Assert.IsSubsetOf(readOnlyMemorySubset, readOnlyMemorySuperset);
        Assert.IsSubsetOf(readOnlyMemorySubset, readOnlyMemorySuperset, EqualityComparer<int>.Default);
    }

    public void IsNotSubsetOf_SpanAndMemoryOverloads_ShouldPass()
    {
        Span<int> spanSubset = [3];
        Span<int> spanSuperset = [1, 2];
        ReadOnlySpan<int> readOnlySpanSubset = spanSubset;
        ReadOnlySpan<int> readOnlySpanSuperset = spanSuperset;
        Memory<int> memorySubset = new[] { 3 }.AsMemory();
        Memory<int> memorySuperset = new[] { 1, 2 }.AsMemory();
        ReadOnlyMemory<int> readOnlyMemorySubset = memorySubset;
        ReadOnlyMemory<int> readOnlyMemorySuperset = memorySuperset;

        Assert.IsNotSubsetOf(spanSubset, spanSuperset);
        Assert.IsNotSubsetOf(spanSubset, spanSuperset, EqualityComparer<int>.Default);
        Assert.IsNotSubsetOf(readOnlySpanSubset, readOnlySpanSuperset);
        Assert.IsNotSubsetOf(readOnlySpanSubset, readOnlySpanSuperset, EqualityComparer<int>.Default);
        Assert.IsNotSubsetOf(memorySubset, memorySuperset);
        Assert.IsNotSubsetOf(memorySubset, memorySuperset, EqualityComparer<int>.Default);
        Assert.IsNotSubsetOf(readOnlyMemorySubset, readOnlyMemorySuperset);
        Assert.IsNotSubsetOf(readOnlyMemorySubset, readOnlyMemorySuperset, EqualityComparer<int>.Default);
    }

#endif
}
