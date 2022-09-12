// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestPlatform.TestFramework.UnitTests;

extern alias FrameworkV1;
extern alias FrameworkV2;

using System;
using System.Globalization;
using System.Threading.Tasks;
using MSTestAdapter.TestUtilities;

using Assert = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.Assert;
using StringAssert = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.StringAssert;
using TestClass = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.TestClassAttribute;
using TestFrameworkV2 = FrameworkV2.Microsoft.VisualStudio.TestTools.UnitTesting;
using TestMethod = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.TestMethodAttribute;

public partial class AssertTests
{
    [TestMethod]
    public void IsFalseNullableBooleansShouldFailWithNull()
    {
        bool? nullBool = null;
        var ex = ActionUtility.PerformActionAndReturnException(() => TestFrameworkV2.Assert.IsFalse(nullBool));
        Assert.IsNotNull(ex);
        StringAssert.Contains(ex.Message, "Assert.IsFalse failed");
    }

    [TestMethod]
    public void IsTrueNullableBooleansShouldFailWithNull()
    {
        bool? nullBool = null;

        var ex = ActionUtility.PerformActionAndReturnException(() => TestFrameworkV2.Assert.IsTrue(nullBool));
        Assert.IsNotNull(ex);
        StringAssert.Contains(ex.Message, "Assert.IsTrue failed");
    }
}
