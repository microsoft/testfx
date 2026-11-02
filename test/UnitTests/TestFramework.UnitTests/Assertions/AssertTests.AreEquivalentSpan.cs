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

    public void AreNotEquivalent_SpanMemoryStrictPolicy_DistinguishesAdditionalActualMembers()
    {
        object[] expected = [new { Id = 7 }];
        object[] actual = [new { Id = 7, Extra = 9 }];
        foreach (int shape in new[] { 0, 1, 2, 3 })
        {
            Action strict = () => Invoke(true);
            Action nonStrict = () => Invoke(false);
            strict.Should().NotThrow();
            AssertFailedException failure = nonStrict.Should().ThrowExactly<AssertFailedException>().Which;
            failure.Message.Replace("\r\n", "\n").Should().Be(
                """
                Assertion failed. Expected values to be structurally different.
                structural context

                not expected: [{ Id = 7 }]
                actual:       [{ Id = 7, Extra = 9 }]

                Assert.AreNotEquivalent(expected, actual)
                """.Replace("\r\n", "\n"));
            failure.ExpectedText.Should().Be("not equivalent to [{ Id = 7 }]");
            failure.ActualText.Should().Be("[{ Id = 7, Extra = 9 }]");

            void Invoke(bool strict)
            {
                switch (shape)
                {
                    case 0:
                        Assert.AreNotEquivalent((ReadOnlySpan<object>)expected, actual, strict, "structural context", "expected", "actual");
                        break;
                    case 1:
                        Assert.AreNotEquivalent(expected.AsSpan(), actual.AsSpan(), strict, "structural context", "expected", "actual");
                        break;
                    case 2:
                        Assert.AreNotEquivalent((ReadOnlyMemory<object>)expected, actual.AsMemory(), strict, "structural context", "expected", "actual");
                        break;
                    default:
                        Assert.AreNotEquivalent(expected.AsMemory(), actual.AsMemory(), strict, "structural context", "expected", "actual");
                        break;
                }
            }
        }

        object[] different = [new { Id = 8 }];
        Action readOnlyMemory = () => Assert.AreNotEquivalent((ReadOnlyMemory<object>)expected, different.AsMemory());
        readOnlyMemory.Should().NotThrow();
        different = [new { Id = 7 }];
        readOnlyMemory.Should().ThrowExactly<AssertFailedException>().Which.ActualText.Should().Be("[{ Id = 7 }]");
    }

    public void AreEquivalent_SpanMemoryStrictOverloads_KeepStrictPolicyInScopedFailures()
    {
        object[] expected = [new { Id = 7 }];
        object[] actual = [];
        Action[] nonStrict =
        [
            () => Assert.AreEquivalent(expected.AsSpan(), actual.AsSpan(), "strict context", "expected", "actual"),
            () => Assert.AreEquivalent((ReadOnlyMemory<object>)expected, actual.AsMemory(), "strict context", "expected", "actual"),
            () => Assert.AreEquivalent(expected.AsMemory(), actual.AsMemory(), "strict context", "expected", "actual"),
        ];
        Action[] strict =
        [
            () => Assert.AreEquivalent(expected.AsSpan(), actual.AsSpan(), true, "strict context", "expected", "actual"),
            () => Assert.AreEquivalent((ReadOnlyMemory<object>)expected, actual.AsMemory(), true, "strict context", "expected", "actual"),
            () => Assert.AreEquivalent(expected.AsMemory(), actual.AsMemory(), true, "strict context", "expected", "actual"),
        ];
        for (int i = 0; i < strict.Length; i++)
        {
            actual = [new { Id = 7, Extra = 9 }];
            nonStrict[i].Should().NotThrow();
            int continued = 0;
            Action rejected = () =>
            {
                using (Assert.Scope())
                {
                    strict[i]();
                    continued++;
                }
            };
            rejected.Should().ThrowExactly<AssertFailedException>().WithMessage(
                """
                Assertion failed. Expected values to be structurally equivalent (strict mode).
                Mismatch at '[0]': actual has unexpected members not present on expected: Extra.
                strict context

                Assert.AreEquivalent(expected, actual)
                """);
            continued.Should().Be(1);
            actual = [new { Id = 7 }];
            strict[i].Should().NotThrow();
        }
    }
}

#endif
