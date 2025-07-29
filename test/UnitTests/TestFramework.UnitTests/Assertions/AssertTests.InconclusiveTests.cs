// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using FluentAssertions;

namespace Microsoft.VisualStudio.TestPlatform.TestFramework.UnitTests;

public partial class AssertTests
{
    // See https://github.com/dotnet/sdk/issues/25373
    public void InconclusiveDoesNotThrowWhenMessageContainsInvalidStringFormatCompositeAndNoArgumentsPassed()
    {
        Exception ex = VerifyThrows<AssertInconclusiveException>(() => Assert.Inconclusive("{"));
        Verify(ex.Message.Contains("Assert.Inconclusive failed. {"));
    }

    // See https://github.com/dotnet/sdk/issues/25373
    [SuppressMessage("Usage", "CA2241:Provide correct arguments to formatting methods", Justification = "We want to test invalid format")]
    public void InconclusiveThrowsWhenMessageContainsInvalidStringFormatComposite() =>
        VerifyThrows<FormatException>(() => Assert.Inconclusive("{", "arg"));
}
