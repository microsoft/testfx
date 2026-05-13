// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using AwesomeAssertions;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using TestFramework.ForTestingMSTest;

namespace Microsoft.VisualStudio.TestPlatform.TestFramework.UnitTests;

public class AssertFailedExceptionTests : TestContainer
{
    public void ExpectedText_DefaultsToNull()
    {
        var exception = new AssertFailedException("test message");

        exception.ExpectedText.Should().BeNull();
    }

    public void ActualText_DefaultsToNull()
    {
        var exception = new AssertFailedException("test message");

        exception.ActualText.Should().BeNull();
    }

    public void ExpectedAndActualText_CanBeSet()
    {
        var exception = new AssertFailedException("test message")
        {
            ExpectedText = "42",
            ActualText = "37",
        };

        exception.ExpectedText.Should().Be("42");
        exception.ActualText.Should().Be("37");
    }

    public void ThrowAssertFailed_WithStructuredMessage_PopulatesExpectedAndActualTextAndDataEntries()
    {
        EvidenceBlock evidence = EvidenceBlock.Create()
            .AddLine("expected:", "42")
            .AddLine("actual:", "37");

        StructuredAssertionMessage message = new("Expected values to be equal.");
        message.WithEvidence(evidence);
        message.WithExpectedAndActual("42", "37");

        AssertFailedException? caught = null;
        try
        {
            Assert.ThrowAssertFailed(message);
        }
        catch (AssertFailedException ex)
        {
            caught = ex;
        }

        caught.Should().NotBeNull();
        caught!.ExpectedText.Should().Be("42");
        caught.ActualText.Should().Be("37");
        caught.Data["assert.expected"].Should().Be("42");
        caught.Data["assert.actual"].Should().Be("37");
    }

    public void ThrowAssertFailed_WithStructuredMessage_NullExpectedAndActual_DoesNotPopulateDataEntries()
    {
        StructuredAssertionMessage message = new("Condition failed.");

        AssertFailedException? caught = null;
        try
        {
            Assert.ThrowAssertFailed(message);
        }
        catch (AssertFailedException ex)
        {
            caught = ex;
        }

        caught.Should().NotBeNull();
        caught!.ExpectedText.Should().BeNull();
        caught.ActualText.Should().BeNull();
        caught.Data.Contains("assert.expected").Should().BeFalse();
        caught.Data.Contains("assert.actual").Should().BeFalse();
    }
}
