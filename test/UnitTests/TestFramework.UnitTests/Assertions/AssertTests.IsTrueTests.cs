// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.VisualStudio.TestPlatform.TestFramework.UnitTests;

public partial class AssertTests
{
    public void IsFalseNullableBooleansShouldFailWithNull()
    {
        bool? nullBool = null;
        Exception ex = VerifyThrows(() => Assert.IsFalse(nullBool));
        Verify(ex.Message.Contains("Assert.IsFalse failed"));
    }

    public void IsTrueNullableBooleansShouldFailWithNull()
    {
        bool? nullBool = null;

        Exception ex = VerifyThrows(() => Assert.IsTrue(nullBool));
        Verify(ex.Message.Contains("Assert.IsTrue failed"));
    }
}
