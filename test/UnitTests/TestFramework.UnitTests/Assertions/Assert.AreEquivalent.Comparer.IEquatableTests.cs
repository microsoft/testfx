// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Reflection;

using AwesomeAssertions;

using TestFramework.ForTestingMSTest;

namespace Microsoft.VisualStudio.TestPlatform.TestFramework.UnitTests;

public sealed class AreEquivalentComparerIEquatableTests : TestContainer
{
    public AreEquivalentComparerIEquatableTests()
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

    public void AreEquivalent_IEquatableExplicitImplementation_DeterminesEquality()
    {
        var expected = new ExplicitEquatable("same-key", "expected-only");
        var actual = new ExplicitEquatable("same-key", "actual-only");

        Assert.AreEquivalent(expected, actual);
    }

    public void AreEquivalent_IEquatableReturningFalse_RejectsMatchingMembers()
    {
        var expected = new AlwaysUnequal("same visible value");
        var actual = new AlwaysUnequal("same visible value");

        Action compare = () => Assert.AreEquivalent(expected, actual);

        compare.Should().ThrowExactly<AssertFailedException>()
            .WithMessage("*Mismatch at '<root>'*values are not equal*");
    }

    public void AreEquivalent_IEquatableThrows_ReportsUnderlyingException()
    {
        Action compare = () => Assert.AreEquivalent(new ThrowingEquatable(), new ThrowingEquatable());

        AssertFailedException failure = compare.Should().ThrowExactly<AssertFailedException>().Which;
        failure.Message.Should().Contain("Mismatch at '<root>'");
        failure.Message.Should().Contain("IEquatable");
        failure.Message.Should().Contain(nameof(InvalidOperationException));
        failure.Message.Should().Contain("equatable-sentinel");
        failure.Message.Should().NotContain(nameof(TargetInvocationException));
    }

    public void AreEquivalent_IEquatableAssertionFailure_PropagatesOriginalAssertion()
    {
        Action compare = () => Assert.AreEquivalent(new AssertionThrowingEquatable(), new AssertionThrowingEquatable());

        AssertFailedException failure = compare.Should().ThrowExactly<AssertFailedException>().Which;
        failure.Message.Should().Contain("equatable-assertion-sentinel");
        failure.Message.Should().NotContain("Mismatch at");
    }

    public void AreEquivalent_IEquatableDeclaredTypeGuard_IncompatibleActualFallsBackToMembers()
    {
        object expected = new ExpectedInterfaceValueHolder(new EquatableInterfaceValue("expected-key"));
        object actual = new ActualInterfaceValueHolder(new StructuralValue("actual-key"));

        Action compare = () => Assert.AreEquivalent(expected, actual);

        AssertFailedException failure = compare.Should().ThrowExactly<AssertFailedException>().Which;
        failure.Message.Should().Contain("Mismatch at 'Value.Key'");
        failure.ExpectedText.Should().Be("\"expected-key\"");
        failure.ActualText.Should().Be("\"actual-key\"");
    }

    public void GetIEquatableEqualsMethod_ObjectAndString_SkipsInterfaceLookup()
    {
        // AreEquivalent handles strings in the primitive-like branch before it asks this helper.
        // Exercise the private lookup directly to pin its string exclusion independently.
        Type comparerType = typeof(Assert).GetNestedType("EquivalenceComparer", BindingFlags.NonPublic)!;
        MethodInfo method = comparerType.GetMethod("GetIEquatableEqualsMethod", BindingFlags.NonPublic | BindingFlags.Static)!;

        method.Invoke(null, [typeof(object)]).Should().BeNull();
        method.Invoke(null, [typeof(string)]).Should().BeNull();
    }

    private sealed class ExplicitEquatable(string key, string visibleValue) : IEquatable<ExplicitEquatable>
    {
        public string Key { get; } = key;

        public string VisibleValue { get; } = visibleValue;

        bool IEquatable<ExplicitEquatable>.Equals(ExplicitEquatable? other)
            => other is not null && Key == other.Key;
    }

    private sealed class AlwaysUnequal(string visibleValue) : IEquatable<AlwaysUnequal>
    {
        public string VisibleValue { get; } = visibleValue;

        public bool Equals(AlwaysUnequal? other)
            => false;
    }

    private sealed class ThrowingEquatable : IEquatable<ThrowingEquatable>
    {
        public bool Equals(ThrowingEquatable? other)
            => throw new InvalidOperationException("equatable-sentinel");
    }

    private sealed class AssertionThrowingEquatable : IEquatable<AssertionThrowingEquatable>
    {
        public bool Equals(AssertionThrowingEquatable? other)
        {
            Assert.Fail("equatable-assertion-sentinel");
            return false;
        }
    }

    private interface IEquatableValue : IEquatable<IEquatableValue>
    {
        string Key { get; }
    }

    private sealed class EquatableInterfaceValue(string key) : IEquatableValue
    {
        public string Key { get; } = key;

        bool IEquatable<IEquatableValue>.Equals(IEquatableValue? other)
            => other is not null && Key == other.Key;
    }

    private sealed class StructuralValue(string key)
    {
        public string Key { get; } = key;
    }

    private sealed class ExpectedInterfaceValueHolder(IEquatableValue value)
    {
        public IEquatableValue Value { get; } = value;
    }

    private sealed class ActualInterfaceValueHolder(StructuralValue value)
    {
        public StructuralValue Value { get; } = value;
    }
}
