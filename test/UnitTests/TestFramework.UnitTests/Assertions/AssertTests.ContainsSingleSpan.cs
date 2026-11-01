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

    public void ContainsSingle_InterpolatedSpanAndMemory_Singleton_ReturnsItemWithoutFormatting()
    {
        int formatCalls = 0;
        string FormatMessage()
        {
            formatCalls++;
            return "span context";
        }

        int[] array = [17];
        Span<int> span = [23];
        ReadOnlySpan<int> readOnlySpan = [31];
        Memory<int> memory = new[] { 43 };
        ReadOnlyMemory<int> readOnlyMemory = new[] { 59 };

        Assert.ContainsSingle(array, $"{FormatMessage()}").Should().Be(17);
        Assert.ContainsSingle(span, $"{FormatMessage()}").Should().Be(23);
        Assert.ContainsSingle(readOnlySpan, $"{FormatMessage()}").Should().Be(31);
        Assert.ContainsSingle(memory, $"{FormatMessage()}").Should().Be(43);
        Assert.ContainsSingle(readOnlyMemory, $"{FormatMessage()}").Should().Be(59);
        formatCalls.Should().Be(0);
    }

    public void ContainsSingle_InterpolatedSpanAndMemory_EmptyOrMultipleElements_ReportsCountAndMessage()
    {
        int[][] cases = [[], [17, 23]];
        foreach (int[] values in cases)
        {
            int formatCalls = 0;
            string FormatMessage()
            {
                formatCalls++;
                return "span context";
            }

            Action[] assertions =
            [
                () =>
                {
                    int[] collection = values;
                    Assert.ContainsSingle(collection, $"{FormatMessage()}");
                },
                () =>
                {
                    Span<int> collection = values;
                    Assert.ContainsSingle(collection, $"{FormatMessage()}");
                },
                () =>
                {
                    ReadOnlySpan<int> collection = values;
                    Assert.ContainsSingle(collection, $"{FormatMessage()}");
                },
                () =>
                {
                    Memory<int> collection = values;
                    Assert.ContainsSingle(collection, $"{FormatMessage()}");
                },
                () =>
                {
                    ReadOnlyMemory<int> collection = values;
                    Assert.ContainsSingle(collection, $"{FormatMessage()}");
                },
            ];
            foreach (Action assertion in assertions)
            {
                int previousFormatCalls = formatCalls;
                VerifyContainsSingleSpanFailure(assertion, values.Length, withPredicate: false);
                formatCalls.Should().Be(previousFormatCalls + 1);
            }
        }
    }

    public void ContainsSingle_PredicateSpanAndMemory_ReturnsTheOnlyMatchingItem()
    {
        int[] values = [1, 42, 3];
        Func<int, bool> predicate = static value => value == 42;
        Span<int> span = values;
        ReadOnlySpan<int> readOnlySpan = values;
        Memory<int> memory = values;
        ReadOnlyMemory<int> readOnlyMemory = values;

        Assert.ContainsSingle<int>(predicate, values).Should().Be(42);
        Assert.ContainsSingle<int, List<int>>(predicate, [1, 42, 3]).Should().Be(42);
        Assert.ContainsSingle(predicate, span).Should().Be(42);
        Assert.ContainsSingle(predicate, readOnlySpan).Should().Be(42);
        Assert.ContainsSingle(predicate, memory).Should().Be(42);
        Assert.ContainsSingle(predicate, readOnlyMemory).Should().Be(42);
    }

    public void ContainsSingle_PredicateSpanAndMemory_NoOrMultipleMatches_ReportsExactCount()
    {
        (int[] Values, int Matches)[] cases =
        [
            ([], 0),
            ([1, 3], 0),
            ([42, 3, 42], 2),
        ];
        Func<int, bool> predicate = static value => value == 42;
        foreach ((int[] values, int matches) in cases)
        {
            Action[] assertions =
            [
                () =>
                {
                    int[] collection = values;
                    Assert.ContainsSingle(predicate, collection, "predicate context");
                },
                () =>
                {
                    Span<int> collection = values;
                    Assert.ContainsSingle(predicate, collection, "predicate context");
                },
                () =>
                {
                    ReadOnlySpan<int> collection = values;
                    Assert.ContainsSingle(predicate, collection, "predicate context");
                },
                () =>
                {
                    Memory<int> collection = values;
                    Assert.ContainsSingle(predicate, collection, "predicate context");
                },
                () =>
                {
                    ReadOnlyMemory<int> collection = values;
                    Assert.ContainsSingle(predicate, collection, "predicate context");
                },
            ];
            foreach (Action assertion in assertions)
            {
                VerifyContainsSingleSpanFailure(assertion, matches, withPredicate: true);
            }
        }
    }

    private static void VerifyContainsSingleSpanFailure(Action assertion, int actualCount, bool withPredicate)
    {
        string expected = withPredicate
            ? $"""
                Assertion failed. Expected collection to contain exactly one element matching the predicate.
                predicate context

                expected matches: 1
                actual matches:   {actualCount}

                Assert.ContainsSingle(predicate, collection)
                """
            : $"""
                Assertion failed. Expected collection to contain exactly one element.
                span context

                expected count: 1
                actual count:   {actualCount}

                Assert.ContainsSingle(collection)
                """;
        AssertFailedException exception = assertion.Should().ThrowExactly<AssertFailedException>().Which;

        exception.Message.Replace("\r\n", "\n").Should().Be(expected.Replace("\r\n", "\n"));
        exception.ExpectedText.Should().Be("1");
        exception.ActualText.Should().Be(actualCount.ToString(CultureInfo.InvariantCulture));
    }

    #endregion
}

#endif
