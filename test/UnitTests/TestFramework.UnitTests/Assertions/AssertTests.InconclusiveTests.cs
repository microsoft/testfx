// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestPlatform.TestFramework.UnitTests;

using System;

using Microsoft.VisualStudio.TestTools.UnitTesting;

public partial class AssertTests
{
    // See https://github.com/dotnet/sdk/issues/25373
    public void InconclusiveDoesNotThrowWhenMessageContainsInvalidStringFormatCompositeAndNoArgumentsPassed()
    {
        var ex = VerifyThrows(() => Assert.Inconclusive("{"));

        Verify(ex is not null);
        Verify(typeof(AssertInconclusiveException) == ex.GetType());
        Verify(ex.Message.Contains("Assert.Inconclusive failed. {"));
    }

    // See https://github.com/dotnet/sdk/issues/25373
    public void InconclusiveThrowsWhenMessageContainsInvalidStringFormatComposite()
    {
        var ex = VerifyThrows(() => Assert.Inconclusive("{", "arg"));

        Verify(ex is not null);
        Verify(ex is FormatException);
    }
}
