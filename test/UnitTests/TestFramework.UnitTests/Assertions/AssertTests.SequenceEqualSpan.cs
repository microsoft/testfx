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

    public void SequenceEqual_SpanMemoryOverloads_PreserveComparerOrderAndDiagnostics()
    {
        string[] expected = ["a", "b", "a"];
        string[] actual = [];
        var comparer = new SpanSequenceComparer();
        (Action Equal, Action NotEqual, int Mode)[] cases =
        [
            (() => Assert.AreSequenceEqual(expected.AsSpan(), actual.AsSpan(), SequenceOrder.InAnyOrder, "sequence context", "expected", "actual"),
                () => Assert.AreNotSequenceEqual(expected.AsSpan(), actual.AsSpan(), SequenceOrder.InAnyOrder, "sequence context", "expected", "actual"), 0),
            (() => Assert.AreSequenceEqual((ReadOnlyMemory<string>)expected, actual.AsMemory(), SequenceOrder.InAnyOrder, "sequence context", "expected", "actual"),
                () => Assert.AreNotSequenceEqual((ReadOnlyMemory<string>)expected, actual.AsMemory(), SequenceOrder.InAnyOrder, "sequence context", "expected", "actual"), 0),
            (() => Assert.AreSequenceEqual(expected.AsMemory(), actual.AsMemory(), SequenceOrder.InAnyOrder, "sequence context", "expected", "actual"),
                () => Assert.AreNotSequenceEqual(expected.AsMemory(), actual.AsMemory(), SequenceOrder.InAnyOrder, "sequence context", "expected", "actual"), 0),
            (() => Assert.AreSequenceEqual(expected.AsSpan(), actual.AsSpan(), comparer, "sequence context", "expected", "actual"),
                () => Assert.AreNotSequenceEqual(expected.AsSpan(), actual.AsSpan(), comparer, "sequence context", "expected", "actual"), 1),
            (() => Assert.AreSequenceEqual((ReadOnlyMemory<string>)expected, actual.AsMemory(), comparer, "sequence context", "expected", "actual"),
                () => Assert.AreNotSequenceEqual((ReadOnlyMemory<string>)expected, actual.AsMemory(), comparer, "sequence context", "expected", "actual"), 1),
            (() => Assert.AreSequenceEqual(expected.AsMemory(), actual.AsMemory(), comparer, "sequence context", "expected", "actual"),
                () => Assert.AreNotSequenceEqual(expected.AsMemory(), actual.AsMemory(), comparer, "sequence context", "expected", "actual"), 1),
            (() => Assert.AreSequenceEqual(expected.AsSpan(), actual.AsSpan(), comparer, SequenceOrder.InAnyOrder, "sequence context", "expected", "actual"),
                () => Assert.AreNotSequenceEqual(expected.AsSpan(), actual.AsSpan(), comparer, SequenceOrder.InAnyOrder, "sequence context", "expected", "actual"), 2),
            (() => Assert.AreSequenceEqual((ReadOnlyMemory<string>)expected, actual.AsMemory(), comparer, SequenceOrder.InAnyOrder, "sequence context", "expected", "actual"),
                () => Assert.AreNotSequenceEqual((ReadOnlyMemory<string>)expected, actual.AsMemory(), comparer, SequenceOrder.InAnyOrder, "sequence context", "expected", "actual"), 2),
            (() => Assert.AreSequenceEqual(expected.AsMemory(), actual.AsMemory(), comparer, SequenceOrder.InAnyOrder, "sequence context", "expected", "actual"),
                () => Assert.AreNotSequenceEqual(expected.AsMemory(), actual.AsMemory(), comparer, SequenceOrder.InAnyOrder, "sequence context", "expected", "actual"), 2),
        ];
        foreach ((Action equal, Action notEqual, int mode) in cases)
        {
            actual = mode switch
            {
                0 => ["b", "a", "a"],
                1 => ["A", "B", "A"],
                _ => ["B", "A", "A"],
            };
            string actualText = mode switch
            {
                0 => "[\"b\", \"a\", \"a\"]",
                1 => "[\"A\", \"B\", \"A\"]",
                _ => "[\"B\", \"A\", \"A\"]",
            };
            equal.Should().NotThrow();
            AssertFailedException inverse = CaptureSequenceSpanScopedFailure(notEqual);
            string inverseSummary = mode == 1 ? "Expected sequences to differ." : "Expected sequences to differ (in any order).";
            string inverseRows = $"notExpected: [\"a\", \"b\", \"a\"]\nactual:      {actualText}";
            if (mode != 0)
            {
                inverseRows += "\ncomparer:    SpanSequenceComparer";
            }

            string suffix = mode switch
            {
                0 => ", <order>",
                1 => ", <comparer>",
                _ => ", <comparer>, <order>",
            };
            string inverseMessage = $"""
                Assertion failed. {inverseSummary}
                sequence context

                {inverseRows}

                Assert.AreNotSequenceEqual(expected, actual{suffix})
                """;
            inverse.Message.Replace("\r\n", "\n").Should().Be(inverseMessage.Replace("\r\n", "\n"));
            inverse.ExpectedText.Should().Be("[\"a\", \"b\", \"a\"]");
            inverse.ActualText.Should().Be(actualText);

            actual = ["z", "z", "z"];
            notEqual.Should().NotThrow();
            AssertFailedException mismatch = CaptureSequenceSpanScopedFailure(equal);
            string equalSummary = mode == 1
                ? "Expected sequences to be equal.\nSequences have 3 element(s). 3 element(s) differ. First difference at index 0."
                : "Expected sequences to be equal (in any order).\nMissing 3 element(s) from actual. Found 3 unexpected element(s).";
            string equalRows = mode == 1
                ? "expected: [\"a\", \"b\", \"a\"]\nactual:   [\"z\", \"z\", \"z\"]"
                : "missing:    [\"a\", \"b\", \"a\"]\nunexpected: [\"z\", \"z\", \"z\"]";
            if (mode != 0)
            {
                equalRows += mode == 1 ? "\ncomparer: SpanSequenceComparer" : "\ncomparer:   SpanSequenceComparer";
            }

            string equalMessage = $"""
                Assertion failed. {equalSummary}
                sequence context

                {equalRows}

                Assert.AreSequenceEqual(expected, actual{suffix})
                """;
            mismatch.Message.Replace("\r\n", "\n").Should().Be(equalMessage.Replace("\r\n", "\n"));
            mismatch.ExpectedText.Should().Be("[\"a\", \"b\", \"a\"]");
            mismatch.ActualText.Should().Be("[\"z\", \"z\", \"z\"]");
        }
    }

    private static AssertFailedException CaptureSequenceSpanScopedFailure(Action assertion)
    {
        int continued = 0;
        Action action = () =>
        {
            using (Assert.Scope())
            {
                assertion();
                continued++;
            }
        };
        AssertFailedException failure = action.Should().ThrowExactly<AssertFailedException>().Which;
        continued.Should().Be(1);
        return failure;
    }

    private sealed class SpanSequenceComparer : IEqualityComparer<string>
    {
        public bool Equals(string? x, string? y) => StringComparer.OrdinalIgnoreCase.Equals(x, y);

        public int GetHashCode(string obj) => StringComparer.OrdinalIgnoreCase.GetHashCode(obj);
    }
}

#endif
