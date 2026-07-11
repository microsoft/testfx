// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if NETCOREAPP3_1_OR_GREATER

using AwesomeAssertions;

using TestFramework.ForTestingMSTest;

namespace Microsoft.VisualStudio.TestTools.UnitTesting.UnitTests;

/// <summary>
/// Unit tests for the span/memory overloads of <c>Assert.ContainsSingle</c>.
/// </summary>
public partial class AssertTests : TestContainer
{
    #region ContainsSingle span/memory

    public void ContainsSingle_ReadOnlySpan_SingleElement_ReturnsItem()
    {
        ReadOnlySpan<int> span = [42];
        int result = Assert.ContainsSingle(span);
        result.Should().Be(42);
    }

    public void ContainsSingle_ReadOnlySpan_MultipleElements_Throws()
    {
        ReadOnlySpan<int> span = [1, 2];
        bool threw = false;
        try
        {
            Assert.ContainsSingle(span);
        }
        catch (AssertFailedException)
        {
            threw = true;
        }

        threw.Should().BeTrue();
    }

    public void ContainsSingle_ReadOnlySpan_Empty_Throws()
    {
        ReadOnlySpan<int> span = [];
        bool threw = false;
        try
        {
            Assert.ContainsSingle(span);
        }
        catch (AssertFailedException)
        {
            threw = true;
        }

        threw.Should().BeTrue();
    }

    public void ContainsSingle_Span_SingleElement_ReturnsItem()
    {
        Span<int> span = [7];
        int result = Assert.ContainsSingle(span);
        result.Should().Be(7);
    }

    public void ContainsSingle_ReadOnlyMemory_SingleElement_ReturnsItem()
    {
        ReadOnlyMemory<int> memory = new[] { 5 }.AsMemory();
        int result = Assert.ContainsSingle(memory);
        result.Should().Be(5);
    }

    public void ContainsSingle_Memory_MultipleElements_Throws()
    {
        Memory<int> memory = new[] { 1, 2, 3 }.AsMemory();
        bool threw = false;
        try
        {
            Assert.ContainsSingle(memory);
        }
        catch (AssertFailedException)
        {
            threw = true;
        }

        threw.Should().BeTrue();
    }

    public void ContainsSingle_ReadOnlySpan_WithPredicate_SingleMatch_ReturnsItem()
    {
        ReadOnlySpan<int> span = [1, 2, 3];
        int result = Assert.ContainsSingle(static i => i == 2, span);
        result.Should().Be(2);
    }

    public void ContainsSingle_Span_WithPredicate_MultipleMatches_Throws()
    {
        Span<int> span = [1, 2, 3, 4];
        bool threw = false;
        try
        {
            Assert.ContainsSingle(static i => i > 2, span);
        }
        catch (AssertFailedException)
        {
            threw = true;
        }

        threw.Should().BeTrue();
    }

    public void ContainsSingle_Memory_WithPredicate_SingleMatch_ReturnsItem()
    {
        Memory<int> memory = new[] { 1, 2, 3 }.AsMemory();
        int result = Assert.ContainsSingle(static i => i == 3, memory);
        result.Should().Be(3);
    }

    public void ContainsSingle_Array_BindsWithoutAmbiguity_ReturnsItem()
    {
        int[] array = [99];
        int result = Assert.ContainsSingle(array);
        result.Should().Be(99);
    }

    #endregion
}

#endif
