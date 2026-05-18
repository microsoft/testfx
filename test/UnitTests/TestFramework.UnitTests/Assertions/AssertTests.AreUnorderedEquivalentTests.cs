// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections;

using AwesomeAssertions;

using TestFramework.ForTestingMSTest;

namespace Microsoft.VisualStudio.TestPlatform.TestFramework.UnitTests;

public partial class AssertTests : TestContainer
{
    public void AreUnorderedEquivalent_IdenticalCollections_Passes()
        => Assert.AreUnorderedEquivalent([1, 2, 3], [1, 2, 3]);

    public void AreUnorderedEquivalent_SameElementsDifferentOrder_Passes()
        => Assert.AreUnorderedEquivalent([1, 2, 3], [3, 1, 2]);

    public void AreUnorderedEquivalent_DifferentMultiplicity_FailsWithMissingAndUnexpectedDiagnostics()
    {
        Action act = () => Assert.AreUnorderedEquivalent([1, 1, 2], [1, 2, 2]);
        AssertFailedException ex = act.Should().Throw<AssertFailedException>().Which;

        ex.Message.Should().Contain("Collections are not equivalent.")
            .And.Contain("Missing 1 element(s) from actual. Found 1 unexpected element(s).")
            .And.Contain("missing:    [1]")
            .And.Contain("unexpected: [2]")
            .And.Contain("Assert.AreUnorderedEquivalent([1, 1, 2], [1, 2, 2])");
        ex.ExpectedText.Should().Be("[1, 1, 2]");
        ex.ActualText.Should().Be("[1, 2, 2]");
    }

    public void AreUnorderedEquivalent_MissingOnly_FailsWithMissingDiagnostics()
    {
        Action act = () => Assert.AreUnorderedEquivalent([1, 2], [1]);
        act.Should().Throw<AssertFailedException>()
            .WithMessage("*Missing 1 element(s) from actual. Found 0 unexpected element(s).*missing:    [2]*unexpected: []*");
    }

    public void AreUnorderedEquivalent_UnexpectedOnly_FailsWithUnexpectedDiagnostics()
    {
        Action act = () => Assert.AreUnorderedEquivalent(Array.Empty<int>(), [1]);
        act.Should().Throw<AssertFailedException>()
            .WithMessage("*Missing 0 element(s) from actual. Found 1 unexpected element(s).*missing:    []*unexpected: [1]*");
    }

    public void AreUnorderedEquivalent_EmptyCollections_Pass()
        => Assert.AreUnorderedEquivalent(Array.Empty<int>(), Array.Empty<int>());

    public void AreUnorderedEquivalent_NullElementsDifferentOrder_Passes()
        => Assert.AreUnorderedEquivalent<int?>([null, 1, null], [null, null, 1]);

    public void AreUnorderedEquivalent_NullMultiplicityDifference_FailsWithNullDiagnostics()
    {
        Action act = () => Assert.AreUnorderedEquivalent<int?>([null, null, 1], [null, 1, 1]);
        act.Should().Throw<AssertFailedException>()
            .WithMessage("*Missing 1 element(s) from actual. Found 1 unexpected element(s).*missing:    [null]*unexpected: [1]*");
    }

    public void AreUnorderedEquivalent_BothNull_FailsWithExpectedParameterName()
    {
        Action act = () => Assert.AreUnorderedEquivalent<object>(null, null);
        act.Should().Throw<AssertFailedException>()
            .WithMessage("*parameter 'expected'*cannot be null*");
    }

    public void AreUnorderedEquivalent_NullActual_FailsWithActualParameterName()
    {
        Action act = () => Assert.AreUnorderedEquivalent([1], actual: null!);
        act.Should().Throw<AssertFailedException>()
            .WithMessage("*parameter 'actual'*cannot be null*");
    }

    public void AreUnorderedEquivalent_CustomComparer_IsRespected()
        => Assert.AreUnorderedEquivalent(["Alpha", "beta"], ["BETA", "alpha"], StringComparer.OrdinalIgnoreCase);

    public void AreUnorderedEquivalent_CustomComparerType_IsIncludedInFailureMessage()
    {
        string[] expected = ["Alpha"];
        string[] actual = ["ALPHA", "beta"];

        Action act = () => Assert.AreUnorderedEquivalent(expected, actual, StringComparer.OrdinalIgnoreCase);
        act.Should().Throw<AssertFailedException>()
            .WithMessage("*comparer:   OrdinalIgnoreCaseComparer*Assert.AreUnorderedEquivalent(expected, actual, <comparer>)*");
    }

    public void AreUnorderedEquivalent_NonGenericOverload_MatchesGenericSemantics()
    {
        IEnumerable expected = new ArrayList { "Alpha", "beta" };
        IEnumerable actual = new ArrayList { "BETA", "alpha" };

        Assert.AreUnorderedEquivalent(expected, actual, StringComparer.OrdinalIgnoreCase);
    }

    public void AreUnorderedEquivalent_RecordElements_UseValueEquality()
        => Assert.AreUnorderedEquivalent([new ValueRecord(1, "a"), new ValueRecord(2, "b")], [new ValueRecord(2, "b"), new ValueRecord(1, "a")]);

    public void AreUnorderedEquivalent_LazyEnumerables_AreMaterializedOnce()
    {
        SingleUseEnumerable<int> expected = new([1, 1, 2]);
        SingleUseEnumerable<int> actual = new([1, 2, 2]);

        Action act = () => Assert.AreUnorderedEquivalent(expected, actual);
        act.Should().Throw<AssertFailedException>();
        expected.EnumerationCount.Should().Be(1);
        actual.EnumerationCount.Should().Be(1);
    }

    public void AreNotUnorderedEquivalent_IdenticalCollections_Fail()
    {
        Action act = () => Assert.AreNotUnorderedEquivalent([1, 2, 3], [1, 2, 3]);
        act.Should().Throw<AssertFailedException>()
            .WithMessage("*Collections are unexpectedly equivalent.*Assert.AreNotUnorderedEquivalent([1, 2, 3], [1, 2, 3])*");
    }

    public void AreNotUnorderedEquivalent_SameElementsDifferentOrder_Fail()
    {
        Action act = () => Assert.AreNotUnorderedEquivalent([1, 2, 3], [3, 2, 1]);
        act.Should().Throw<AssertFailedException>()
            .WithMessage("*Collections are unexpectedly equivalent.*");
    }

    public void AreNotUnorderedEquivalent_DifferentCollections_Pass()
        => Assert.AreNotUnorderedEquivalent([1, 2, 3], [1, 2, 4]);

    public void AreNotUnorderedEquivalent_DifferentMultiplicity_Pass()
        => Assert.AreNotUnorderedEquivalent([1, 1, 2], [1, 2, 2]);

    public void AreNotUnorderedEquivalent_CustomComparer_IsRespected()
    {
        Action act = () => Assert.AreNotUnorderedEquivalent(["Alpha"], ["ALPHA"], StringComparer.OrdinalIgnoreCase);
        act.Should().Throw<AssertFailedException>()
            .WithMessage("*Assert.AreNotUnorderedEquivalent([\"Alpha\"], [\"ALPHA\"], <comparer>)*");
    }

    private sealed record ValueRecord(int Id, string Name);

    private sealed class SingleUseEnumerable<T>(IEnumerable<T> source) : IEnumerable<T>
    {
        private bool _hasBeenEnumerated;

        public int EnumerationCount { get; private set; }

        public IEnumerator<T> GetEnumerator()
        {
            if (_hasBeenEnumerated)
            {
                throw new InvalidOperationException("Sequence was enumerated more than once.");
            }

            _hasBeenEnumerated = true;
            EnumerationCount++;
            return source.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
