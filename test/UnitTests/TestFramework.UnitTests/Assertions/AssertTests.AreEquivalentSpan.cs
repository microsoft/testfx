// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if NETCOREAPP3_1_OR_GREATER

using AwesomeAssertions;

using TestFramework.ForTestingMSTest;

namespace Microsoft.VisualStudio.TestTools.UnitTesting.UnitTests;

/// <summary>
/// Unit tests for the span/memory overloads of <c>Assert.AreEquivalent</c> and <c>Assert.AreNotEquivalent</c>.
/// </summary>
public partial class AssertTests : TestContainer
{
    #region AreEquivalent span/memory

    public void AreEquivalent_ReadOnlySpan_Equal_DoesNotThrow()
    {
        ReadOnlySpan<int> expected = [1, 2, 3];
        ReadOnlySpan<int> actual = [1, 2, 3];
        Assert.AreEquivalent(expected, actual);
    }

    public void AreEquivalent_ReadOnlySpan_Different_Throws()
    {
        ReadOnlySpan<int> expected = [1, 2, 3];
        ReadOnlySpan<int> actual = [1, 2, 4];
        bool threw = false;
        try
        {
            Assert.AreEquivalent(expected, actual);
        }
        catch (AssertFailedException)
        {
            threw = true;
        }

        threw.Should().BeTrue();
    }

    public void AreEquivalent_Span_Equal_DoesNotThrow()
    {
        Span<int> expected = [1, 2, 3];
        Span<int> actual = [1, 2, 3];
        Assert.AreEquivalent(expected, actual);
    }

    public void AreEquivalent_Memory_Equal_DoesNotThrow()
    {
        Memory<int> expected = new[] { 1, 2, 3 }.AsMemory();
        Memory<int> actual = new[] { 1, 2, 3 }.AsMemory();
        Assert.AreEquivalent(expected, actual);
    }

    public void AreEquivalent_ReadOnlyMemory_Different_Throws()
    {
        ReadOnlyMemory<int> expected = new[] { 1, 2, 3 }.AsMemory();
        ReadOnlyMemory<int> actual = new[] { 9, 8, 7 }.AsMemory();
        bool threw = false;
        try
        {
            Assert.AreEquivalent(expected, actual);
        }
        catch (AssertFailedException)
        {
            threw = true;
        }

        threw.Should().BeTrue();
    }

    public void AreEquivalent_ReadOnlySpan_Strict_Equal_DoesNotThrow()
    {
        ReadOnlySpan<int> expected = [1, 2, 3];
        ReadOnlySpan<int> actual = [1, 2, 3];
        Assert.AreEquivalent(expected, actual, strict: true);
    }

    public void AreEquivalent_Array_BindsWithoutAmbiguity_DoesNotThrow()
    {
        int[] expected = [1, 2, 3];
        int[] actual = [1, 2, 3];
        Assert.AreEquivalent(expected, actual);
    }

    #endregion

    #region AreNotEquivalent span/memory

    public void AreNotEquivalent_ReadOnlySpan_Different_DoesNotThrow()
    {
        ReadOnlySpan<int> notExpected = [1, 2, 3];
        ReadOnlySpan<int> actual = [4, 5, 6];
        Assert.AreNotEquivalent(notExpected, actual);
    }

    public void AreNotEquivalent_ReadOnlySpan_Equal_Throws()
    {
        ReadOnlySpan<int> notExpected = [1, 2, 3];
        ReadOnlySpan<int> actual = [1, 2, 3];
        bool threw = false;
        try
        {
            Assert.AreNotEquivalent(notExpected, actual);
        }
        catch (AssertFailedException)
        {
            threw = true;
        }

        threw.Should().BeTrue();
    }

    public void AreNotEquivalent_Memory_Different_DoesNotThrow()
    {
        Memory<int> notExpected = new[] { 1, 2, 3 }.AsMemory();
        Memory<int> actual = new[] { 9, 8, 7 }.AsMemory();
        Assert.AreNotEquivalent(notExpected, actual);
    }

    public void AreNotEquivalent_Array_BindsWithoutAmbiguity_DoesNotThrow()
    {
        int[] notExpected = [1, 2, 3];
        int[] actual = [4, 5, 6];
        Assert.AreNotEquivalent(notExpected, actual);
    }

    #endregion
}

#endif
