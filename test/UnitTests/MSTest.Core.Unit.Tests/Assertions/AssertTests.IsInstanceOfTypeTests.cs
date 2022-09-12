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
    public void InstanceOfTypeShouldFailWhenValueIsNull()
    {
        static void action() => TestFrameworkV2.Assert.IsInstanceOfType(null, typeof(AssertTests));
        ActionUtility.ActionShouldThrowExceptionOfType(action, typeof(TestFrameworkV2.AssertFailedException));
    }

    [TestMethod]
    public void InstanceOfTypeShouldFailWhenTypeIsNull()
    {
        static void action() => TestFrameworkV2.Assert.IsInstanceOfType(5, null);
        ActionUtility.ActionShouldThrowExceptionOfType(action, typeof(TestFrameworkV2.AssertFailedException));
    }

    [TestMethod]
    public void InstanceOfTypeShouldPassOnSameInstance()
    {
        TestFrameworkV2.Assert.IsInstanceOfType(5, typeof(int));
    }

    [TestMethod]
    public void InstanceOfTypeShouldPassOnHigherInstance()
    {
        TestFrameworkV2.Assert.IsInstanceOfType(5, typeof(object));
    }

    [TestMethod]
    public void InstanceNotOfTypeShouldFailWhenValueIsNull()
    {
        Action action = () => TestFrameworkV2.Assert.IsNotInstanceOfType(null, typeof(AssertTests));
    }

    [TestMethod]
    public void InstanceNotOfTypeShouldFailWhenTypeIsNull()
    {
        static void action() => TestFrameworkV2.Assert.IsNotInstanceOfType(5, null);
        ActionUtility.ActionShouldThrowExceptionOfType(action, typeof(TestFrameworkV2.AssertFailedException));
    }

    [TestMethod]
    public void InstanceNotOfTypeShouldPassOnWrongInstance()
    {
        TestFrameworkV2.Assert.IsNotInstanceOfType(5L, typeof(int));
    }

    [TestMethod]
    public void InstanceNotOfTypeShouldPassOnSubInstance()
    {
        TestFrameworkV2.Assert.IsNotInstanceOfType(new object(), typeof(int));
    }
}
