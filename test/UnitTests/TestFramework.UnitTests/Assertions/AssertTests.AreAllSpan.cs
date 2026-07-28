// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#if NETCOREAPP3_1_OR_GREATER

using AwesomeAssertions;

using TestFramework.ForTestingMSTest;

namespace Microsoft.VisualStudio.TestTools.UnitTesting.UnitTests;

/// <summary>
/// Unit tests for the span/memory overloads of <c>Assert.AreAllDistinct</c>, <c>Assert.AreAllNotNull</c>, and <c>Assert.AreAllOfType</c>.
/// </summary>
public partial class AssertTests : TestContainer
{
    #region AreAllDistinct span/memory

    public void AreAllDistinct_ReadOnlySpan_AllUnique_DoesNotThrow()
    {
        ReadOnlySpan<int> span = [1, 2, 3];
        Assert.AreAllDistinct(span);
    }

    public void AreAllDistinct_ReadOnlySpan_Duplicate_Throws()
    {
        ReadOnlySpan<int> span = [1, 2, 2];
        bool threw = false;
        try
        {
            Assert.AreAllDistinct(span);
        }
        catch (AssertFailedException)
        {
            threw = true;
        }

        threw.Should().BeTrue();
    }

    public void AreAllDistinct_Span_AllUnique_DoesNotThrow()
    {
        Span<int> span = [4, 5, 6];
        Assert.AreAllDistinct(span);
    }

    public void AreAllDistinct_Memory_Duplicate_Throws()
    {
        Memory<int> memory = new[] { 1, 1 }.AsMemory();
        bool threw = false;
        try
        {
            Assert.AreAllDistinct(memory);
        }
        catch (AssertFailedException)
        {
            threw = true;
        }

        threw.Should().BeTrue();
    }

    public void AreAllDistinct_ReadOnlyMemory_WithComparer_Duplicate_Throws()
    {
        ReadOnlyMemory<string> memory = new[] { "a", "A" }.AsMemory();
        bool threw = false;
        try
        {
            Assert.AreAllDistinct(memory, StringComparer.OrdinalIgnoreCase);
        }
        catch (AssertFailedException)
        {
            threw = true;
        }

        threw.Should().BeTrue();
    }

    public void AreAllDistinct_Array_BindsWithoutAmbiguity_DoesNotThrow()
    {
        int[] array = [1, 2, 3];
        Assert.AreAllDistinct(array);
    }

    #endregion

    #region AreAllNotNull span/memory

    public void AreAllNotNull_ReadOnlySpan_NoNulls_DoesNotThrow()
    {
        ReadOnlySpan<string?> span = ["a", "b"];
        Assert.AreAllNotNull(span);
    }

    public void AreAllNotNull_ReadOnlySpan_ContainsNull_Throws()
    {
        ReadOnlySpan<string?> span = ["a", null];
        bool threw = false;
        try
        {
            Assert.AreAllNotNull(span);
        }
        catch (AssertFailedException)
        {
            threw = true;
        }

        threw.Should().BeTrue();
    }

    public void AreAllNotNull_Memory_ContainsNull_Throws()
    {
        Memory<string?> memory = new string?[] { null, "b" }.AsMemory();
        bool threw = false;
        try
        {
            Assert.AreAllNotNull(memory);
        }
        catch (AssertFailedException)
        {
            threw = true;
        }

        threw.Should().BeTrue();
    }

    public void AreAllNotNull_Array_BindsWithoutAmbiguity_DoesNotThrow()
    {
        string?[] array = ["a", "b"];
        Assert.AreAllNotNull(array);
    }

    #endregion

    #region AreAllOfType span/memory

    public void AreAllOfType_Type_ReadOnlySpan_AllMatch_DoesNotThrow()
    {
        ReadOnlySpan<object> span = ["a", "b"];
        Assert.AreAllOfType(typeof(string), span);
    }

    public void AreAllOfType_Type_ReadOnlySpan_MixedTypes_Throws()
    {
        ReadOnlySpan<object> span = ["a", 1];
        bool threw = false;
        try
        {
            Assert.AreAllOfType(typeof(string), span);
        }
        catch (AssertFailedException)
        {
            threw = true;
        }

        threw.Should().BeTrue();
    }

    public void AreAllOfType_Generic_Span_AllMatch_DoesNotThrow()
    {
        Span<object> span = ["a", "b"];
        Assert.AreAllOfType<string, object>(span);
    }

    public void AreAllOfType_Generic_Memory_MixedTypes_Throws()
    {
        Memory<object> memory = new object[] { "a", 1 }.AsMemory();
        bool threw = false;
        try
        {
            Assert.AreAllOfType<string, object>(memory);
        }
        catch (AssertFailedException)
        {
            threw = true;
        }

        threw.Should().BeTrue();
    }

    public void AreAllOfType_Type_Array_BindsWithoutAmbiguity_DoesNotThrow()
    {
        object[] array = ["a", "b"];
        Assert.AreAllOfType(typeof(string), array);
    }

    #endregion
}

#endif
