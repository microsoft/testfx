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
    public void IsInstanceOfType_WhenValueIsNull_Fails()
    {
        static void action() => TestFrameworkV2.Assert.IsInstanceOfType(null, typeof(AssertTests));
        ActionUtility.ActionShouldThrowExceptionOfType(action, typeof(TestFrameworkV2.AssertFailedException));
    }

    [TestMethod]
    public void IsInstanceOfType_WhenTypeIsNull_Fails()
    {
        static void action() => TestFrameworkV2.Assert.IsInstanceOfType(5, null);
        ActionUtility.ActionShouldThrowExceptionOfType(action, typeof(TestFrameworkV2.AssertFailedException));
    }

    [TestMethod]
    public void IsInstanceOfType_OnSameInstance_Passes()
    {
        TestFrameworkV2.Assert.IsInstanceOfType(5, typeof(int));
    }

    [TestMethod]
    public void IsInstanceOfType_OnHigherInstance_Passes()
    {
        TestFrameworkV2.Assert.IsInstanceOfType(5, typeof(object));
    }

    [TestMethod]
    public void IsNotInstanceOfType_WhenValueIsNull_Fails()
    {
        Action action = () => TestFrameworkV2.Assert.IsNotInstanceOfType(null, typeof(AssertTests));
    }

    [TestMethod]
    public void IsNotInstanceOfType_WhenTypeIsNull_Fails()
    {
        static void action() => TestFrameworkV2.Assert.IsNotInstanceOfType(5, null);
        ActionUtility.ActionShouldThrowExceptionOfType(action, typeof(TestFrameworkV2.AssertFailedException));
    }

    [TestMethod]
    public void IsNotInstanceOfType_OnWrongInstance_Passes()
    {
        TestFrameworkV2.Assert.IsNotInstanceOfType(5L, typeof(int));
    }

    [TestMethod]
    public void IsNotInstanceOfType_OnSubInstance_Passes()
    {
        TestFrameworkV2.Assert.IsNotInstanceOfType(new object(), typeof(int));
    }

    [TestMethod]
    public void IsInstanceOfType_WhenValueIsNullUsingGenericType_Fails()
    {
        static void action() => TestFrameworkV2.Assert.IsInstanceOfType<AssertTests>(null);
        ActionUtility.ActionShouldThrowExceptionOfType(action, typeof(TestFrameworkV2.AssertFailedException));
    }

    [TestMethod]
    public void IsInstanceOfType_OnSameInstanceUsingGenericType_Passes()
    {
        TestFrameworkV2.Assert.IsInstanceOfType<int>(5);
    }

    [TestMethod]
    public void IsInstanceOfType_OnHigherInstanceUsingGenericType_Passes()
    {
        TestFrameworkV2.Assert.IsInstanceOfType<object>(5);
    }

    [TestMethod]
    public void IsNotInstanceOfType_WhenValueIsNullUsingGenericType_Fails()
    {
        Action action = () => TestFrameworkV2.Assert.IsNotInstanceOfType<AssertTests>(null);
    }

    [TestMethod]
    public void IsNotInstanceOfType_OnWrongInstanceUsingGenericType_Passes()
    {
        TestFrameworkV2.Assert.IsNotInstanceOfType<int>(5L);
    }

    [TestMethod]
    public void IsNotInstanceOfType_OnSubInstanceUsingGenericType_Passes()
    {
        TestFrameworkV2.Assert.IsNotInstanceOfType<int>(new object());
    }
}
