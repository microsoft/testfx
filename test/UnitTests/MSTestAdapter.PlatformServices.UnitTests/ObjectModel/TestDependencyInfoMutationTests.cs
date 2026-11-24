// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using AwesomeAssertions;

using Microsoft.VisualStudio.TestPlatform.MSTest.TestAdapter.ObjectModel;

using TestFramework.ForTestingMSTest;

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests.ObjectModel;

public sealed class TestDependencyInfoMutationTests : TestContainer
{
    public void DescribeTarget_WhenNeitherClassNorMethodIsNamed_RendersTheWildcard()
    {
        // No class and no method is the "everything of the dependent's own class" shape; it has to be rendered
        // as something a reader can see in a diagnostic, not as an empty string.
        new TestDependencyInfo(null, null, proceedOnFailure: false).DescribeTarget().Should().Be("*");
        new TestDependencyInfo(string.Empty, string.Empty, proceedOnFailure: true).DescribeTarget().Should().Be("*");
    }

    public void DescribeTarget_RendersEveryCombinationOfClassAndMethod()
    {
        new TestDependencyInfo(null, "Setup", proceedOnFailure: false).DescribeTarget().Should().Be("Setup");
        new TestDependencyInfo("Ns.ClassA", null, proceedOnFailure: false).DescribeTarget().Should().Be("Ns.ClassA.*");
        new TestDependencyInfo("Ns.ClassA", "Setup", proceedOnFailure: false).DescribeTarget().Should().Be("Ns.ClassA.Setup");
    }

    public void Decode_WhenThePayloadIsEmpty_DecodesToTheConservativeSkipOnFailure()
    {
        // Malformed data fails closed: with nothing to read, the dependent must keep the ordinary skip rather than
        // silently gain the "proceed on failure" relaxation.
        var decoded = TestDependencyInfo.Decode(string.Empty);

        decoded.ProceedOnFailure.Should().BeFalse();
        decoded.TargetClassFullName.Should().BeNull();
        decoded.TargetMethodName.Should().BeNull();
    }

    public void Decode_WhenThePayloadHasNoSeparator_ReadsItAsAMethodOfTheDependentsOwnClass()
    {
        var skipping = TestDependencyInfo.Decode("SSetup");
        skipping.ProceedOnFailure.Should().BeFalse();
        skipping.TargetClassFullName.Should().BeNull();
        skipping.TargetMethodName.Should().Be("Setup");

        var proceeding = TestDependencyInfo.Decode("PSetup");
        proceeding.ProceedOnFailure.Should().BeTrue();
        proceeding.TargetClassFullName.Should().BeNull();
        proceeding.TargetMethodName.Should().Be("Setup");

        // Without a recognized flag the whole payload is data, and the dependent keeps the ordinary skip.
        var unflagged = TestDependencyInfo.Decode("Run");
        unflagged.ProceedOnFailure.Should().BeFalse();
        unflagged.TargetClassFullName.Should().BeNull();
        unflagged.TargetMethodName.Should().Be("Run");
    }

    public void Decode_WhenThePayloadHasASeparator_SplitsItIntoClassAndMethod()
    {
        var decoded = TestDependencyInfo.Decode("PNs.ClassA\nSetup");

        decoded.ProceedOnFailure.Should().BeTrue();
        decoded.TargetClassFullName.Should().Be("Ns.ClassA");
        decoded.TargetMethodName.Should().Be("Setup");

        var classOnly = TestDependencyInfo.Decode("SNs.ClassA\n");
        classOnly.ProceedOnFailure.Should().BeFalse();
        classOnly.TargetClassFullName.Should().Be("Ns.ClassA");
        classOnly.TargetMethodName.Should().BeNull();
    }

    public void EncodeThenDecode_PreservesNamesThatStartWithAFlagCharacter()
    {
        // The flag is a fixed-width prefix precisely so that a class or method whose name starts with 'P' or 'S'
        // is never mistaken for one. That only holds when the encoder writes a flag for *both* values.
        foreach (bool proceedOnFailure in new[] { false, true })
        {
            foreach (string className in new[] { "Services.Orders", "Payments.Orders", "Ns.Orders" })
            {
                var original = new TestDependencyInfo(className, "Setup", proceedOnFailure);

                var roundTripped = TestDependencyInfo.Decode(TestDependencyInfo.Encode(original));

                roundTripped.TargetClassFullName.Should().Be(className, $"proceedOnFailure: {proceedOnFailure}");
                roundTripped.TargetMethodName.Should().Be("Setup", $"proceedOnFailure: {proceedOnFailure}");
                roundTripped.ProceedOnFailure.Should().Be(proceedOnFailure, $"class: {className}");
            }

            var methodOnly = new TestDependencyInfo(null, "Setup", proceedOnFailure);
            var methodOnlyRoundTripped = TestDependencyInfo.Decode(TestDependencyInfo.Encode(methodOnly));
            methodOnlyRoundTripped.TargetClassFullName.Should().BeNull();
            methodOnlyRoundTripped.TargetMethodName.Should().Be("Setup");
            methodOnlyRoundTripped.ProceedOnFailure.Should().Be(proceedOnFailure);
        }
    }

    public void Encode_DistinguishesTheTwoFlagsByTheirFirstCharacter()
    {
        string skipping = TestDependencyInfo.Encode(new TestDependencyInfo("Ns.ClassA", "Setup", proceedOnFailure: false));
        string proceeding = TestDependencyInfo.Encode(new TestDependencyInfo("Ns.ClassA", "Setup", proceedOnFailure: true));

        skipping.Should().Be("SNs.ClassA\nSetup");
        proceeding.Should().Be("PNs.ClassA\nSetup");
    }
}
