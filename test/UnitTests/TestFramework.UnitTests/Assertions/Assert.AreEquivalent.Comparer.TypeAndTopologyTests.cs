// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Reflection;

using AwesomeAssertions;

using TestFramework.ForTestingMSTest;

namespace Microsoft.VisualStudio.TestPlatform.TestFramework.UnitTests;

public sealed class AreEquivalentComparerTypeAndTopologyTests : TestContainer
{
    public AreEquivalentComparerTypeAndTopologyTests()
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

    public void AreEquivalent_TypeAndNonTypeValues_ReportsTypeMismatch()
    {
        object expected = new TypeDelegator(typeof(int));
        object actual = new EmptyValue();
        Action compare = () => Assert.AreEquivalent<object>(expected, actual);

        AssertFailedException failure = compare.Should().ThrowExactly<AssertFailedException>().Which;
        failure.Message.Should().Contain("incompatible types");
    }

    public void AreEquivalent_UnequalTypeValues_ReportsValueMismatch()
    {
        object expected = new TypeDelegator(typeof(int));
        object actual = new TypeDelegator(typeof(string));
        Action compare = () => Assert.AreEquivalent<object>(expected, actual);

        AssertFailedException failure = compare.Should().ThrowExactly<AssertFailedException>().Which;
        failure.Message.Should().Contain("values are not equal");
        failure.Message.Should().NotContain("incompatible types");
    }

    public void AreEquivalent_EquivalentTypeValues_AreEquivalent()
    {
        object expected = new TypeDelegator(typeof(int));
        object actual = new TypeDelegator(typeof(int));

        Assert.AreEquivalent<object>(expected, actual);
    }

    public void AreEquivalent_Failure_UsesPlaceholdersForMissingCallSiteExpressions()
    {
        Action missingExpected = () => Assert.AreEquivalent(1, 2, expectedExpression: string.Empty, actualExpression: "actual");
        Action missingActual = () => Assert.AreEquivalent(1, 2, expectedExpression: "expected", actualExpression: string.Empty);

        missingExpected.Should().ThrowExactly<AssertFailedException>()
            .Which.Message.Should().Contain("Assert.AreEquivalent(<expected>, actual)");
        missingActual.Should().ThrowExactly<AssertFailedException>()
            .Which.Message.Should().Contain("Assert.AreEquivalent(expected, <actual>)");
    }

    public void AreEquivalent_ValueTypeToReferenceTypePairs_AreComparedStructurally()
    {
        object sharedValue = new ValueNode("same");
        var expected = new MixedHolder(sharedValue, sharedValue);
        var actual = new MixedHolder(new ReferenceNode("same"), new ReferenceNode("same"));

        Assert.AreEquivalent(expected, actual);
    }

    private sealed class EmptyValue
    {
    }

    private readonly struct ValueNode(string value)
    {
        public string Value { get; } = value;
    }

    private sealed class ReferenceNode(string value)
    {
        public string Value { get; } = value;
    }

    private sealed class MixedHolder(object first, object second)
    {
        public object First { get; } = first;

        public object Second { get; } = second;
    }
}
