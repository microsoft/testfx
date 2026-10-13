// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if NETCOREAPP3_1_OR_GREATER

using AwesomeAssertions;

using TestFramework.ForTestingMSTest;

namespace Microsoft.VisualStudio.TestTools.UnitTesting.UnitTests;

/// <summary>
/// Unit tests for the span/memory overloads of <c>Assert.AreSequenceEqual</c> and <c>Assert.AreNotSequenceEqual</c>.
/// </summary>
public partial class AssertTests : TestContainer
{
    #region AreSequenceEqual span/memory

    public void AreSequenceEqual_ReadOnlySpan_Equal_DoesNotThrow()
    {
        ReadOnlySpan<int> expected = [1, 2, 3];
        ReadOnlySpan<int> actual = [1, 2, 3];
        Assert.AreSequenceEqual(expected, actual);
    }

    public void AreSequenceEqual_ReadOnlySpan_Different_Throws()
    {
        ReadOnlySpan<int> expected = [1, 2, 3];
        ReadOnlySpan<int> actual = [3, 2, 1];
        bool threw = false;
        try
        {
            Assert.AreSequenceEqual(expected, actual);
        }
        catch (AssertFailedException)
        {
            threw = true;
        }

        threw.Should().BeTrue();
    }

    public void AreSequenceEqual_Span_Equal_DoesNotThrow()
    {
        Span<int> expected = [1, 2, 3];
        Span<int> actual = [1, 2, 3];
        Assert.AreSequenceEqual(expected, actual);
    }

    public void AreSequenceEqual_Memory_Equal_DoesNotThrow()
    {
        Memory<int> expected = new[] { 1, 2, 3 }.AsMemory();
        Memory<int> actual = new[] { 1, 2, 3 }.AsMemory();
        Assert.AreSequenceEqual(expected, actual);
    }

    public void AreSequenceEqual_ReadOnlyMemory_Different_Throws()
    {
        ReadOnlyMemory<int> expected = new[] { 1, 2, 3 }.AsMemory();
        ReadOnlyMemory<int> actual = new[] { 1, 2, 4 }.AsMemory();
        bool threw = false;
        try
        {
            Assert.AreSequenceEqual(expected, actual);
        }
        catch (AssertFailedException)
        {
            threw = true;
        }

        threw.Should().BeTrue();
    }

    public void AreSequenceEqual_ReadOnlySpan_InAnyOrder_DoesNotThrow()
    {
        ReadOnlySpan<int> expected = [1, 2, 3];
        ReadOnlySpan<int> actual = [3, 2, 1];
        Assert.AreSequenceEqual(expected, actual, SequenceOrder.InAnyOrder);
    }

    public void AreSequenceEqual_ReadOnlySpan_WithComparer_DoesNotThrow()
    {
        ReadOnlySpan<string> expected = ["a", "b"];
        ReadOnlySpan<string> actual = ["A", "B"];
        Assert.AreSequenceEqual(expected, actual, StringComparer.OrdinalIgnoreCase);
    }

    public void AreSequenceEqual_ReadOnlySpan_WithComparerAndOrder_DoesNotThrow()
    {
        ReadOnlySpan<string> expected = ["a", "b"];
        ReadOnlySpan<string> actual = ["B", "A"];
        Assert.AreSequenceEqual(expected, actual, StringComparer.OrdinalIgnoreCase, SequenceOrder.InAnyOrder);
    }

    public void AreSequenceEqual_Array_BindsWithoutAmbiguity_DoesNotThrow()
    {
        int[] expected = [1, 2, 3];
        int[] actual = [1, 2, 3];
        Assert.AreSequenceEqual(expected, actual);
    }

    #endregion

    #region AreNotSequenceEqual span/memory

    public void AreNotSequenceEqual_ReadOnlySpan_Different_DoesNotThrow()
    {
        ReadOnlySpan<int> notExpected = [1, 2, 3];
        ReadOnlySpan<int> actual = [3, 2, 1];
        Assert.AreNotSequenceEqual(notExpected, actual);
    }

    public void AreNotSequenceEqual_ReadOnlySpan_Equal_Throws()
    {
        ReadOnlySpan<int> notExpected = [1, 2, 3];
        ReadOnlySpan<int> actual = [1, 2, 3];
        bool threw = false;
        try
        {
            Assert.AreNotSequenceEqual(notExpected, actual);
        }
        catch (AssertFailedException)
        {
            threw = true;
        }

        threw.Should().BeTrue();
    }

    public void AreNotSequenceEqual_Memory_Different_DoesNotThrow()
    {
        Memory<int> notExpected = new[] { 1, 2, 3 }.AsMemory();
        Memory<int> actual = new[] { 9, 8, 7 }.AsMemory();
        Assert.AreNotSequenceEqual(notExpected, actual);
    }

    public void AreNotSequenceEqual_ReadOnlySpan_WithComparer_Equal_Throws()
    {
        ReadOnlySpan<string> notExpected = ["a", "b"];
        ReadOnlySpan<string> actual = ["A", "B"];
        bool threw = false;
        try
        {
            Assert.AreNotSequenceEqual(notExpected, actual, StringComparer.OrdinalIgnoreCase);
        }
        catch (AssertFailedException)
        {
            threw = true;
        }

        threw.Should().BeTrue();
    }

    public void AreNotSequenceEqual_Array_BindsWithoutAmbiguity_DoesNotThrow()
    {
        int[] notExpected = [1, 2, 3];
        int[] actual = [3, 2, 1];
        Assert.AreNotSequenceEqual(notExpected, actual);
    }

    public void AreNotSequenceEqual_SpanAndMemory_OrderAndComparerArePreserved()
    {
        string[] notExpected = ["a", "a", "b"];
        string[] actual = ["B", "A", "A"];
        Action[] equivalent =
        [
            () => Assert.AreNotSequenceEqual((ReadOnlySpan<string>)notExpected, actual, StringComparer.OrdinalIgnoreCase, SequenceOrder.InAnyOrder, "coverage sentinel"),
            () => Assert.AreNotSequenceEqual(notExpected.AsSpan(), actual.AsSpan(), StringComparer.OrdinalIgnoreCase, SequenceOrder.InAnyOrder, "coverage sentinel"),
            () => Assert.AreNotSequenceEqual((ReadOnlyMemory<string>)notExpected, actual.AsMemory(), StringComparer.OrdinalIgnoreCase, SequenceOrder.InAnyOrder, "coverage sentinel"),
            () => Assert.AreNotSequenceEqual(notExpected.AsMemory(), actual.AsMemory(), StringComparer.OrdinalIgnoreCase, SequenceOrder.InAnyOrder, "coverage sentinel"),
        ];
        foreach (Action action in equivalent)
        {
            action.Should().Throw<AssertFailedException>().WithMessage("*coverage sentinel*");
        }

        // Both ordering and comparer change the answer for these same non-degenerate inputs.
        Assert.AreNotSequenceEqual((ReadOnlySpan<string>)notExpected, actual, StringComparer.OrdinalIgnoreCase, SequenceOrder.InOrder);
        Assert.AreNotSequenceEqual(notExpected.AsSpan(), actual.AsSpan(), StringComparer.OrdinalIgnoreCase, SequenceOrder.InOrder);
        Assert.AreNotSequenceEqual((ReadOnlyMemory<string>)notExpected, actual.AsMemory(), StringComparer.OrdinalIgnoreCase, SequenceOrder.InOrder);
        Assert.AreNotSequenceEqual(notExpected.AsMemory(), actual.AsMemory(), StringComparer.OrdinalIgnoreCase, SequenceOrder.InOrder);
        Assert.AreNotSequenceEqual((ReadOnlySpan<string>)notExpected, actual, SequenceOrder.InAnyOrder);
        Assert.AreNotSequenceEqual(notExpected.AsSpan(), actual.AsSpan(), SequenceOrder.InAnyOrder);
        Assert.AreNotSequenceEqual((ReadOnlyMemory<string>)notExpected, actual.AsMemory(), SequenceOrder.InAnyOrder);
        Assert.AreNotSequenceEqual(notExpected.AsMemory(), actual.AsMemory(), SequenceOrder.InAnyOrder);
    }

    public void AreNotSequenceEqual_SpanAndMemory_EqualValuesReportMessage()
    {
        int[] notExpected = [1, 2, 3];
        int[] actual = [1, 2, 3];
        Action[] equivalent =
        [
            () => Assert.AreNotSequenceEqual((ReadOnlySpan<int>)notExpected, actual, "coverage sentinel"),
            () => Assert.AreNotSequenceEqual(notExpected.AsSpan(), actual.AsSpan(), "coverage sentinel"),
            () => Assert.AreNotSequenceEqual((ReadOnlyMemory<int>)notExpected, actual.AsMemory(), "coverage sentinel"),
            () => Assert.AreNotSequenceEqual(notExpected.AsMemory(), actual.AsMemory(), "coverage sentinel"),
            () => Assert.AreNotSequenceEqual((ReadOnlySpan<int>)notExpected, actual, comparer: null, "coverage sentinel"),
            () => Assert.AreNotSequenceEqual(notExpected.AsSpan(), actual.AsSpan(), comparer: null, "coverage sentinel"),
            () => Assert.AreNotSequenceEqual((ReadOnlyMemory<int>)notExpected, actual.AsMemory(), comparer: null, "coverage sentinel"),
            () => Assert.AreNotSequenceEqual(notExpected.AsMemory(), actual.AsMemory(), comparer: null, "coverage sentinel"),
        ];
        foreach (Action action in equivalent)
        {
            action.Should().Throw<AssertFailedException>().WithMessage("*coverage sentinel*");
        }
    }

    #endregion
}

#endif
