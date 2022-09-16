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
        var ex = ActionUtility.PerformActionAndReturnException(() => TestFrameworkV2.Assert.IsFalse(nullBool));
        Assert.IsNotNull(ex);
        StringAssert.Contains(ex.Message, "Assert.IsFalse failed");
    }

    public void IsTrueNullableBooleansShouldFailWithNull()
    {
        bool? nullBool = null;

        var ex = ActionUtility.PerformActionAndReturnException(() => TestFrameworkV2.Assert.IsTrue(nullBool));
        Assert.IsNotNull(ex);
        StringAssert.Contains(ex.Message, "Assert.IsTrue failed");
    }
}
