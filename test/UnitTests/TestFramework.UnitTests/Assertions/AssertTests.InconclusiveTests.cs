// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using AwesomeAssertions;

namespace Microsoft.VisualStudio.TestPlatform.TestFramework.UnitTests;

public partial class AssertTests
{
    // See https://github.com/dotnet/sdk/issues/25373
    public void InconclusiveDoesNotThrowWhenMessageContainsInvalidStringFormatCompositeAndNoArgumentsPassed()
    {
        Action action = () => Assert.Inconclusive("{");
        action.Should().Throw<AssertInconclusiveException>()
            .And.Message.Should().Contain("Assert.Inconclusive failed. {");
    }
}
