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

    #endregion
}

#endif
