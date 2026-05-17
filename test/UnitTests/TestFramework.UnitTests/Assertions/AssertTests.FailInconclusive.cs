// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using AwesomeAssertions;

namespace Microsoft.VisualStudio.TestPlatform.TestFramework.UnitTests;

public partial class AssertTests
{
    public void FailWithoutMessageShouldUseStructuredPrefixOnly()
    {
        Action action = () => Assert.Fail();
        action.Should().Throw<AssertFailedException>()
            .WithMessage("Assertion failed.");
    }

    public void FailWithMessageShouldPlaceUserMessageOnOwnLine()
    {
        Action action = () => Assert.Fail("custom reason");
        action.Should().Throw<AssertFailedException>()
            .WithMessage(
                """
                Assertion failed.
                custom reason
                """);
    }

    public void FailWithEmptyMessageShouldNotAddBlankLine()
    {
        Action action = () => Assert.Fail(string.Empty);
        action.Should().Throw<AssertFailedException>()
            .WithMessage("Assertion failed.");
    }

    public void InconclusiveWithoutMessageShouldUseInconclusivePrefix()
    {
        Action action = () => Assert.Inconclusive();
        action.Should().Throw<AssertInconclusiveException>()
            .WithMessage("Assert.Inconclusive.");
    }

    public void InconclusiveWithMessageShouldAppendUserMessage()
    {
        Action action = () => Assert.Inconclusive("db unavailable");
        action.Should().Throw<AssertInconclusiveException>()
            .WithMessage("Assert.Inconclusive. db unavailable");
    }

}
