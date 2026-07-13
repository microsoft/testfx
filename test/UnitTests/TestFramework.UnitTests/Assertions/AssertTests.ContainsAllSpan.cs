// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if NETCOREAPP3_1_OR_GREATER

using AwesomeAssertions;

using TestFramework.ForTestingMSTest;

namespace Microsoft.VisualStudio.TestTools.UnitTesting.UnitTests;

/// <summary>
/// Unit tests for the span/memory overloads of <c>Assert.ContainsAll</c> and <c>Assert.DoesNotContainAll</c>.
/// </summary>
public partial class AssertTests : TestContainer
{
    #region ContainsAll span/memory

    public void ContainsAll_ReadOnlySpan_AllPresent_DoesNotThrow()
    {
        ReadOnlySpan<int> expected = [1, 2];
        ReadOnlySpan<int> collection = [1, 2, 3];
        Assert.ContainsAll(expected, collection);
    }

    public void ContainsAll_ReadOnlySpan_MissingElement_Throws()
    {
        ReadOnlySpan<int> expected = [1, 4];
        ReadOnlySpan<int> collection = [1, 2, 3];
        bool threw = false;
        try
        {
            Assert.ContainsAll(expected, collection);
        }
        catch (AssertFailedException)
        {
            threw = true;
        }

        threw.Should().BeTrue();
    }

    public void ContainsAll_Span_AllPresent_DoesNotThrow()
    {
        Span<int> expected = [3, 1];
        Span<int> collection = [1, 2, 3];
        Assert.ContainsAll(expected, collection);
    }

    public void ContainsAll_ReadOnlyMemory_AllPresent_DoesNotThrow()
    {
        ReadOnlyMemory<int> expected = new[] { 1, 2 }.AsMemory();
        ReadOnlyMemory<int> collection = new[] { 1, 2, 3 }.AsMemory();
        Assert.ContainsAll(expected, collection);
    }

    public void ContainsAll_Memory_MissingElement_Throws()
    {
        Memory<int> expected = new[] { 9 }.AsMemory();
        Memory<int> collection = new[] { 1, 2, 3 }.AsMemory();
        bool threw = false;
        try
        {
            Assert.ContainsAll(expected, collection);
        }
        catch (AssertFailedException)
        {
            threw = true;
        }

        threw.Should().BeTrue();
    }

    public void ContainsAll_ReadOnlySpan_WithComparer_UsesComparer()
    {
        ReadOnlySpan<string> expected = ["A"];
        ReadOnlySpan<string> collection = ["a", "b"];
        Assert.ContainsAll(expected, collection, StringComparer.OrdinalIgnoreCase);
    }

    public void ContainsAll_Array_BindsWithoutAmbiguity_DoesNotThrow()
    {
        int[] expected = [1, 2];
        int[] collection = [1, 2, 3];
        Assert.ContainsAll(expected, collection);
    }

    #endregion

    #region DoesNotContainAll span/memory

    public void DoesNotContainAll_ReadOnlySpan_NotAllPresent_DoesNotThrow()
    {
        ReadOnlySpan<int> notExpected = [1, 9];
        ReadOnlySpan<int> collection = [1, 2, 3];
        Assert.DoesNotContainAll(notExpected, collection);
    }

    public void DoesNotContainAll_ReadOnlySpan_AllPresent_Throws()
    {
        ReadOnlySpan<int> notExpected = [1, 2];
        ReadOnlySpan<int> collection = [1, 2, 3];
        bool threw = false;
        try
        {
            Assert.DoesNotContainAll(notExpected, collection);
        }
        catch (AssertFailedException)
        {
            threw = true;
        }

        threw.Should().BeTrue();
    }

    public void DoesNotContainAll_Memory_NotAllPresent_DoesNotThrow()
    {
        Memory<int> notExpected = new[] { 1, 9 }.AsMemory();
        Memory<int> collection = new[] { 1, 2, 3 }.AsMemory();
        Assert.DoesNotContainAll(notExpected, collection);
    }

    public void DoesNotContainAll_ReadOnlyMemory_WithComparer_AllPresent_Throws()
    {
        ReadOnlyMemory<string> notExpected = new[] { "A" }.AsMemory();
        ReadOnlyMemory<string> collection = new[] { "a", "b" }.AsMemory();
        bool threw = false;
        try
        {
            Assert.DoesNotContainAll(notExpected, collection, StringComparer.OrdinalIgnoreCase);
        }
        catch (AssertFailedException)
        {
            threw = true;
        }

        threw.Should().BeTrue();
    }

    public void DoesNotContainAll_Array_BindsWithoutAmbiguity_DoesNotThrow()
    {
        int[] notExpected = [1, 9];
        int[] collection = [1, 2, 3];
        Assert.DoesNotContainAll(notExpected, collection);
    }

    #endregion
}

#endif
