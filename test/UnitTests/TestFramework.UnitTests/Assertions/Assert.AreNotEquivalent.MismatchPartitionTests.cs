// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections;

using AwesomeAssertions;

using TestFramework.ForTestingMSTest;

namespace Microsoft.VisualStudio.TestPlatform.TestFramework.UnitTests;

public sealed class AreNotEquivalentMismatchPartitionTests : TestContainer
{
    public AreNotEquivalentMismatchPartitionTests()
        => EquivalenceComparerCaches.Reset();

    protected override void Dispose(bool disposing)
    {
        try
        {
            EquivalenceComparerCaches.Reset();
        }
        finally
        {
            base.Dispose(disposing);
        }
    }

    public void AreNotEquivalent_DifferentRuntimeTypesPasses()
    {
        object notExpected = 1;
        object actual = "1";

        Assert.AreNotEquivalent(notExpected, actual);
    }

    public void AreNotEquivalent_NullMismatchPasses()
    {
        object? notExpected = null;
        object actual = "value";

        Assert.AreNotEquivalent(notExpected, actual);
    }

    public void AreNotEquivalent_TopologyMismatchPasses()
    {
        SharedNode shared = new("same");
        PairOfNodes notExpected = new(shared, shared);
        PairOfNodes actual = new(new SharedNode("same"), new SharedNode("same"));

        Assert.AreNotEquivalent(notExpected, actual);
    }

    public void AreNotEquivalent_CollectionLengthMismatchPasses()
        => Assert.AreNotEquivalent<object>(new[] { 1 }, new[] { 1, 2 });

    public void AreNotEquivalent_MissingDictionaryKeyPasses()
    {
        IDictionary<string, int> notExpected = new Dictionary<string, int> { ["key"] = 1 };
        IDictionary<string, int> actual = new Dictionary<string, int>();

        Assert.AreNotEquivalent<object>(notExpected, actual);
    }

    public void AreNotEquivalent_UnexpectedDictionaryKeyInStrictModePasses()
    {
        IDictionary<string, int> notExpected = new Dictionary<string, int>();
        IDictionary<string, int> actual = new Dictionary<string, int> { ["extra"] = 1 };

        Assert.AreNotEquivalent<object>(notExpected, actual, strict: true);
    }

    public void AreNotEquivalent_MissingMemberPasses()
    {
        object notExpected = new WithValue(1);
        object actual = new EmptyShape();

        Assert.AreNotEquivalent(notExpected, actual);
    }

    public void AreNotEquivalent_ExtraActualMemberInStrictModePasses()
    {
        Shape notExpected = new(1);
        ShapeWithExtra actual = new(1, "extra");

        Assert.AreNotEquivalent<object>(notExpected, actual, strict: true);
    }

    public void AreNotEquivalent_DefaultOverloadUsesNonStrictComparison()
    {
        Shape notExpected = new(1);
        ShapeWithExtra actual = new(1, "extra");
        Action compare = () => Assert.AreNotEquivalent<object>(notExpected, actual);

        compare.Should().ThrowExactly<AssertFailedException>()
            .WithMessage("*Expected values to be structurally different*");
    }

    public void AreNotEquivalent_ActualEnumerationFailureIsAComparisonFailure()
    {
        IEnumerable<int> notExpected = [1];
        IEnumerable<int> actual = new ThrowingEnumerable();
        Action compare = () => Assert.AreNotEquivalent(notExpected, actual, strict: false, notExpectedExpression: "notExpected", actualExpression: string.Empty);

        AssertFailedException failure = compare.Should().ThrowExactly<AssertFailedException>().Which;
        failure.Message.Should().Contain("enumerating the actual collection threw InvalidOperationException: enumeration failure");
        failure.Message.Should().Contain("Could not complete structural comparison.");
        failure.Message.Should().Contain("Assert.AreNotEquivalent(notExpected, <actual>)");
    }

    public void AreNotEquivalent_ActualMemberFailureIsAComparisonFailure()
    {
        Shape notExpected = new(1);
        ThrowingShape actual = new();
        Action compare = () => Assert.AreNotEquivalent<Shape>(notExpected, actual, strict: true, notExpectedExpression: string.Empty, actualExpression: "actual");

        AssertFailedException failure = compare.Should().ThrowExactly<AssertFailedException>().Which;
        failure.Message.Should().Contain("reading the actual member threw InvalidOperationException: member failure");
        failure.Message.Should().Contain("Could not complete structural comparison (strict mode)");
        failure.Message.Should().Contain("Assert.AreNotEquivalent(<notExpected>, actual)");
    }

    public void AreNotEquivalent_EquivalentStrictValuesUseStrictSummaryAndPlaceholders()
    {
        Action compare = () => Assert.AreNotEquivalent(1, 1, strict: true, notExpectedExpression: "notExpected", actualExpression: string.Empty);
        Action compareWithMissingNotExpectedExpression = () => Assert.AreNotEquivalent(1, 1, strict: true, notExpectedExpression: string.Empty, actualExpression: "actual");

        AssertFailedException failure = compare.Should().ThrowExactly<AssertFailedException>().Which;
        failure.Message.Should().Contain("Expected values to be structurally different (strict mode)");
        failure.Message.Should().Contain("Assert.AreNotEquivalent(notExpected, <actual>)");

        AssertFailedException missingExpressionFailure = compareWithMissingNotExpectedExpression.Should().ThrowExactly<AssertFailedException>().Which;
        missingExpressionFailure.Message.Should().Contain("Assert.AreNotEquivalent(<notExpected>, actual)");
    }

    public void AreNotEquivalent_DepthLimitIsAComparisonFailure()
    {
        object notExpected = "same";
        object actual = "same";
        for (int i = 0; i < 257; i++)
        {
            notExpected = new NestedValue(notExpected);
            actual = new NestedValue(actual);
        }

        Action compare = () => Assert.AreNotEquivalent<object>(notExpected, actual);

        compare.Should().ThrowExactly<AssertFailedException>()
            .WithMessage("*maximum supported depth of 256*");
    }

#if NETCOREAPP3_1_OR_GREATER
    public void AreNotEquivalent_ReadOnlySpanDefaultUsesNonStrictComparison()
    {
        Shape[] notExpected = [new(1)];
        Shape[] actual = [new ShapeWithExtra(1, "extra")];
        ReadOnlySpan<Shape> notExpectedSpan = notExpected;
        ReadOnlySpan<Shape> actualSpan = actual;
        AssertFailedException? failure = null;
        try
        {
            Assert.AreNotEquivalent(notExpectedSpan, actualSpan);
        }
        catch (AssertFailedException exception)
        {
            failure = exception;
        }

        failure.Should().NotBeNull();
    }

    public void AreNotEquivalent_SpanDefaultUsesNonStrictComparison()
    {
        Shape[] notExpected = [new(1)];
        Shape[] actual = [new ShapeWithExtra(1, "extra")];
        Span<Shape> notExpectedSpan = notExpected;
        Span<Shape> actualSpan = actual;
        AssertFailedException? failure = null;
        try
        {
            Assert.AreNotEquivalent(notExpectedSpan, actualSpan);
        }
        catch (AssertFailedException exception)
        {
            failure = exception;
        }

        failure.Should().NotBeNull();
    }
#endif

    private sealed class SharedNode(string name)
    {
        public string Name { get; } = name;
    }

    private sealed class PairOfNodes(SharedNode left, SharedNode right)
    {
        public SharedNode Left { get; } = left;

        public SharedNode Right { get; } = right;
    }

    private sealed class WithValue(int value)
    {
        public int Value { get; } = value;
    }

    private sealed class EmptyShape
    {
    }

    private class Shape(int value)
    {
        public int Value { get; } = value;
    }

    private sealed class ShapeWithExtra(int value, string extra) : Shape(value)
    {
        public string Extra { get; } = extra;
    }

    private sealed class ThrowingShape : Shape
    {
        public ThrowingShape()
            : base(1)
        {
        }

        public new int Value
            => throw new InvalidOperationException("member failure");
    }

    private sealed class NestedValue(object value)
    {
        public object Value { get; } = value;
    }

    private sealed class ThrowingEnumerable : IEnumerable<int>
    {
        public IEnumerator<int> GetEnumerator()
            => new ThrowingEnumerator();

        IEnumerator IEnumerable.GetEnumerator()
            => GetEnumerator();
    }

    private sealed class ThrowingEnumerator : IEnumerator<int>
    {
        public int Current => throw new InvalidOperationException("enumeration failure");

        object IEnumerator.Current => Current;

        public bool MoveNext()
            => throw new InvalidOperationException("enumeration failure");

        public void Reset()
            => throw new NotSupportedException();

        public void Dispose()
        {
        }
    }
}
