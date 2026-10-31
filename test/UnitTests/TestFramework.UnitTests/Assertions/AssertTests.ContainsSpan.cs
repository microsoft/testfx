// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if NETCOREAPP3_1_OR_GREATER

using AwesomeAssertions;

using TestFramework.ForTestingMSTest;

namespace Microsoft.VisualStudio.TestTools.UnitTesting.UnitTests;

/// <summary>
/// Unit tests for the span/memory overloads of <c>Assert.Contains</c> and <c>Assert.DoesNotContain</c>.
/// </summary>
public partial class AssertTests : TestContainer
{
    #region Contains span/memory

    public void Contains_ReadOnlySpan_ItemExists_DoesNotThrow()
    {
        ReadOnlySpan<int> span = [1, 2, 3];
        Assert.Contains(2, span);
    }

    public void Contains_ReadOnlySpan_ItemMissing_Throws()
    {
        ReadOnlySpan<int> span = [1, 2, 3];
        bool threw = false;
        try
        {
            Assert.Contains(20, span);
        }
        catch (AssertFailedException)
        {
            threw = true;
        }

        threw.Should().BeTrue();
    }

    public void Contains_Span_ItemExists_DoesNotThrow()
    {
        Span<int> span = [1, 2, 3];
        Assert.Contains(3, span);
    }

    public void Contains_ReadOnlyMemory_ItemExists_DoesNotThrow()
    {
        ReadOnlyMemory<int> memory = new[] { 1, 2, 3 }.AsMemory();
        Assert.Contains(1, memory);
    }

    public void Contains_Memory_ItemMissing_Throws()
    {
        Memory<int> memory = new[] { 1, 2, 3 }.AsMemory();
        bool threw = false;
        try
        {
            Assert.Contains(99, memory);
        }
        catch (AssertFailedException)
        {
            threw = true;
        }

        threw.Should().BeTrue();
    }

    public void Contains_ReadOnlySpan_WithComparer_UsesComparer()
    {
        ReadOnlySpan<string> span = ["a", "b", "c"];
        Assert.Contains("A", span, StringComparer.OrdinalIgnoreCase);
    }

    public void Contains_Span_WithPredicate_MatchExists_DoesNotThrow()
    {
        Span<int> span = [1, 2, 3];
        Assert.Contains(static i => i > 2, span);
    }

    public void Contains_Array_BindsWithoutAmbiguity_DoesNotThrow()
    {
        // Array converts to both IEnumerable<T> and Span<T>; this proves the call is unambiguous.
        int[] array = [1, 2, 3];
        Assert.Contains(2, array);
    }

    public void Contains_Array_WithComparer_UsesComparer()
    {
        string[] array = ["a"];
        Assert.Contains("A", array, StringComparer.OrdinalIgnoreCase);
    }

    #endregion

    #region DoesNotContain span/memory

    public void DoesNotContain_ReadOnlySpan_ItemMissing_DoesNotThrow()
    {
        ReadOnlySpan<int> span = [1, 2, 3];
        Assert.DoesNotContain(20, span);
    }

    public void DoesNotContain_ReadOnlySpan_ItemExists_Throws()
    {
        ReadOnlySpan<int> span = [1, 2, 3];
        bool threw = false;
        try
        {
            Assert.DoesNotContain(2, span);
        }
        catch (AssertFailedException)
        {
            threw = true;
        }

        threw.Should().BeTrue();
    }

    public void DoesNotContain_Memory_ItemMissing_DoesNotThrow()
    {
        Memory<int> memory = new[] { 1, 2, 3 }.AsMemory();
        Assert.DoesNotContain(99, memory);
    }

    public void DoesNotContain_ReadOnlyMemory_WithComparer_ItemExists_Throws()
    {
        ReadOnlyMemory<string> memory = new[] { "a", "b" }.AsMemory();
        bool threw = false;
        try
        {
            Assert.DoesNotContain("A", memory, StringComparer.OrdinalIgnoreCase);
        }
        catch (AssertFailedException)
        {
            threw = true;
        }

        threw.Should().BeTrue();
    }

    public void DoesNotContain_Span_WithPredicate_NoMatch_DoesNotThrow()
    {
        Span<int> span = [1, 2, 3];
        Assert.DoesNotContain(static i => i > 100, span);
    }

    public void DoesNotContain_Array_BindsWithoutAmbiguity_DoesNotThrow()
    {
        int[] array = [1, 2, 3];
        Assert.DoesNotContain(20, array);
    }

    public void DoesNotContain_Array_WithComparer_UsesComparer()
    {
        string[] array = ["a"];
        bool threw = false;
        try
        {
            Assert.DoesNotContain("A", array, StringComparer.OrdinalIgnoreCase);
        }
        catch (AssertFailedException)
        {
            threw = true;
        }

        threw.Should().BeTrue();
    }

    public void ArrayInterpolatedMessageOverloads_NullCollection_ThrowArgumentNullException()
    {
        int[] collection = null!;

        Action containsSingle = () => Assert.ContainsSingle<int>(collection, $"collection: {collection}");
        Action hasCount = () => Assert.HasCount<int>(0, collection, $"collection: {collection}");
        Action isEmpty = () => Assert.IsEmpty<int>(collection, $"collection: {collection}");
        Action isNotEmpty = () => Assert.IsNotEmpty<int>(collection, $"collection: {collection}");

        containsSingle.Should().Throw<ArgumentNullException>();
        hasCount.Should().Throw<ArgumentNullException>();
        isEmpty.Should().Throw<ArgumentNullException>();
        isNotEmpty.Should().Throw<ArgumentNullException>();
    }

    #endregion

    public void Contains_SpanMemoryComparerOverloads_PreserveComparerAndFailureContext()
    {
        string[] values = ["alpha", "beta"];
        foreach (int shape in new[] { 0, 1, 2 })
        {
            Action contains = () => Invoke("ALPHA", shouldContain: true);
            Action missing = () => Invoke("GAMMA", shouldContain: true);
            Action excludes = () => Invoke("GAMMA", shouldContain: false);
            Action unexpected = () => Invoke("ALPHA", shouldContain: false);

            contains.Should().NotThrow();
            excludes.Should().NotThrow();
            AssertFailedException missingException = missing.Should().ThrowExactly<AssertFailedException>().Which;
            missingException.ExpectedText.Should().Be("\"GAMMA\"");
            missingException.Message.Should().Contain("comparer context").And.EndWith("Assert.Contains(item, collection)");
            AssertFailedException unexpectedException = unexpected.Should().ThrowExactly<AssertFailedException>().Which;
            unexpectedException.ExpectedText.Should().Be("\"ALPHA\"");
            unexpectedException.Message.Should().Contain("comparer context").And.EndWith("Assert.DoesNotContain(item, collection)");

            void Invoke(string item, bool shouldContain)
            {
                if (shape == 0)
                {
                    Span<string> collection = values;
                    if (shouldContain)
                    {
                        Assert.Contains(item, collection, StringComparer.OrdinalIgnoreCase, "comparer context");
                    }
                    else
                    {
                        Assert.DoesNotContain(item, collection, StringComparer.OrdinalIgnoreCase, "comparer context");
                    }
                }
                else if (shape == 1)
                {
                    Memory<string> collection = values;
                    if (shouldContain)
                    {
                        Assert.Contains(item, collection, StringComparer.OrdinalIgnoreCase, "comparer context");
                    }
                    else
                    {
                        Assert.DoesNotContain(item, collection, StringComparer.OrdinalIgnoreCase, "comparer context");
                    }
                }
                else
                {
                    ReadOnlyMemory<string> collection = values;
                    if (shouldContain)
                    {
                        Assert.Contains(item, collection, StringComparer.OrdinalIgnoreCase, "comparer context");
                    }
                    else
                    {
                        Assert.DoesNotContain(item, collection, StringComparer.OrdinalIgnoreCase, "comparer context");
                    }
                }
            }
        }
    }

    public void Contains_SpanMemoryPredicateOverloads_RejectTheInverseCondition()
    {
        int[] values = [1, 42, 3];
        foreach (int shape in new[] { 0, 1, 2 })
        {
            foreach (bool shouldContain in new[] { true, false })
            {
                int continued = 0;
                Func<int, bool> predicate = shouldContain ? value => value == 42 : value => value == 99;
                Action accepted = Invoke;
                accepted.Should().NotThrow();

                predicate = shouldContain ? value => value == 99 : value => value == 42;
                Action rejected = () =>
                {
                    using (Assert.Scope())
                    {
                        Invoke();
                        continued++;
                    }
                };
                string expected = shouldContain
                    ? """
                        Assertion failed. Expected collection to contain an element matching the predicate.
                        predicate context

                        Assert.Contains(predicate, collection)
                        """
                    : """
                        Assertion failed. Expected collection to not contain an element matching the predicate.
                        predicate context

                        Assert.DoesNotContain(predicate, collection)
                        """;

                rejected.Should().ThrowExactly<AssertFailedException>().Which.Message.Replace("\r\n", "\n")
                    .Should().Be(expected.Replace("\r\n", "\n"));
                continued.Should().Be(1);

                void Invoke()
                {
                    if (shape == 0)
                    {
                        Span<int> collection = values;
                        if (shouldContain)
                        {
                            Assert.Contains(predicate, collection, "predicate context");
                        }
                        else
                        {
                            Assert.DoesNotContain(predicate, collection, "predicate context");
                        }
                    }
                    else if (shape == 1)
                    {
                        Memory<int> collection = values;
                        if (shouldContain)
                        {
                            Assert.Contains(predicate, collection, "predicate context");
                        }
                        else
                        {
                            Assert.DoesNotContain(predicate, collection, "predicate context");
                        }
                    }
                    else
                    {
                        ReadOnlyMemory<int> collection = values;
                        if (shouldContain)
                        {
                            Assert.Contains(predicate, collection, "predicate context");
                        }
                        else
                        {
                            Assert.DoesNotContain(predicate, collection, "predicate context");
                        }
                    }
                }
            }
        }
    }
}

#endif
