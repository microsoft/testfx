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
}
