// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using AwesomeAssertions;

using TestFramework.ForTestingMSTest;

namespace Microsoft.VisualStudio.TestPlatform.TestFramework.UnitTests.Filtering;

public class TestFilterResultTests : TestContainer
{
    public void Run_HasActionRunAndNullReason()
    {
        TestFilterResult result = TestFilterResult.Run;
        result.Action.Should().Be(TestFilterAction.Run);
        result.SkipReason.Should().BeNull();
    }

    public void Drop_HasActionDropAndNullReason()
    {
        TestFilterResult result = TestFilterResult.Drop;
        result.Action.Should().Be(TestFilterAction.Drop);
        result.SkipReason.Should().BeNull();
    }

    public void Default_DefaultsToRunAction()
    {
        // The XML doc on TestFilterResult.Run promises that a default(TestFilterResult) value
        // behaves like Run. A filter that forgets to assign a result still runs the test.
        var result = default(TestFilterResult);
        result.Action.Should().Be(TestFilterAction.Run);
        result.SkipReason.Should().BeNull();
    }

    public void Skip_WithValidReason_SetsActionAndReason()
    {
        var result = TestFilterResult.Skip("Skipped because of CI policy.");
        result.Action.Should().Be(TestFilterAction.Skip);
        result.SkipReason.Should().Be("Skipped because of CI policy.");
    }

    public void Skip_WhenReasonIsNull_ThrowsArgumentNullException()
    {
        Action act = static () => TestFilterResult.Skip(null!);
        act.Should().Throw<ArgumentNullException>().WithParameterName("reason");
    }

    public void Skip_WhenReasonIsEmpty_ThrowsArgumentException()
    {
        // The XML doc promises a "non-empty human-readable explanation" — empty strings would
        // surface as unactionable skipped tests in TRX / IDE output, so they are rejected.
        Action act = static () => TestFilterResult.Skip(string.Empty);
        act.Should().Throw<ArgumentException>().WithParameterName("reason");
    }

    public void Skip_WhenReasonIsWhitespace_ThrowsArgumentException()
    {
        Action act = static () => TestFilterResult.Skip("   ");
        act.Should().Throw<ArgumentException>().WithParameterName("reason");
    }

    public void Equality_TwoSkipResultsWithSameReasonAreEqual()
    {
        var first = TestFilterResult.Skip("reason");
        var second = TestFilterResult.Skip("reason");

        first.Equals(second).Should().BeTrue();
        (first == second).Should().BeTrue();
        (first != second).Should().BeFalse();
        first.GetHashCode().Should().Be(second.GetHashCode());
    }

    public void Equality_TwoSkipResultsWithDifferentReasonsAreNotEqual()
    {
        var first = TestFilterResult.Skip("reason A");
        var second = TestFilterResult.Skip("reason B");

        first.Equals(second).Should().BeFalse();
        (first == second).Should().BeFalse();
        (first != second).Should().BeTrue();
    }

    public void Equality_RunAndDropAreNotEqual()
    {
        (TestFilterResult.Run == TestFilterResult.Drop).Should().BeFalse();
        TestFilterResult.Run.Equals(TestFilterResult.Drop).Should().BeFalse();
    }

    public void Equality_BoxedComparisonAgainstNonResultIsFalse()
    {
        TestFilterResult.Run.Equals("not a result").Should().BeFalse();
        TestFilterResult.Run.Equals(null).Should().BeFalse();
    }
}
