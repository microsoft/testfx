// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestPlatform.TestFramework.UnitTests;

using System;
using System.Globalization;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using global::TestFramework.ForTestingMSTest;

public partial class AssertTests
{
    public void IsFalseNullableBooleansShouldFailWithNull()
    {
        bool? nullBool = null;
        var ex = VerifyThrows(() => Assert.IsFalse(nullBool));
        Verify(ex != null);
        Verify(ex.Message.Contains("Assert.IsFalse failed"));
    }

    public void IsTrueNullableBooleansShouldFailWithNull()
    {
        bool? nullBool = null;

        var ex = VerifyThrows(() => Assert.IsTrue(nullBool));
        Verify(ex != null);
        Verify(ex.Message.Contains("Assert.IsTrue failed"));
    }
}
