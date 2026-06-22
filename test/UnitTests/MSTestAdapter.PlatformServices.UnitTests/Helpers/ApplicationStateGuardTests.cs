// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using AwesomeAssertions;

using TestFramework.ForTestingMSTest;

namespace Microsoft.VisualStudio.TestPlatform.MSTestAdapter.UnitTests;

public class ApplicationStateGuardTests : TestContainer
{
    public void EnsureWithMessageThrowsInvalidOperationException()
    {
        Action action = () => ApplicationStateGuard.Ensure(false, "boom");

        action.Should().Throw<InvalidOperationException>().WithMessage("boom");
    }

    public void UnreachableReturnsUnreachableException()
    {
        Exception exception = ApplicationStateGuard.Unreachable();

        exception.Should().BeOfType<UnreachableException>();
        exception.Message.Should().Contain("thought to be unreachable");
    }
}
