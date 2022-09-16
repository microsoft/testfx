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
    public void InstanceOfTypeShouldFailWhenValueIsNull()
    {
        static void action() => TestFrameworkV2.Assert.IsInstanceOfType(null, typeof(AssertTests));
        ActionUtility.ActionShouldThrowExceptionOfType(action, typeof(TestFrameworkV2.AssertFailedException));
    }

    public void InstanceOfTypeShouldFailWhenTypeIsNull()
    {
        static void action() => TestFrameworkV2.Assert.IsInstanceOfType(5, null);
        ActionUtility.ActionShouldThrowExceptionOfType(action, typeof(TestFrameworkV2.AssertFailedException));
    }

    public void InstanceOfTypeShouldPassOnSameInstance()
    {
        TestFrameworkV2.Assert.IsInstanceOfType(5, typeof(int));
    }

    public void InstanceOfTypeShouldPassOnHigherInstance()
    {
        TestFrameworkV2.Assert.IsInstanceOfType(5, typeof(object));
    }

    public void InstanceNotOfTypeShouldFailWhenValueIsNull()
    {
        TestFrameworkV2.Assert.IsNotInstanceOfType(null, typeof(object));
    }

    public void InstanceNotOfTypeShouldFailWhenTypeIsNull()
    {
        static void action() => TestFrameworkV2.Assert.IsNotInstanceOfType(5, null);
        ActionUtility.ActionShouldThrowExceptionOfType(action, typeof(TestFrameworkV2.AssertFailedException));
    }

    public void InstanceNotOfTypeShouldPassOnWrongInstance()
    {
        TestFrameworkV2.Assert.IsNotInstanceOfType(5L, typeof(int));
    }

    public void InstanceNotOfTypeShouldPassOnSubInstance()
    {
        TestFrameworkV2.Assert.IsNotInstanceOfType(new object(), typeof(int));
    }

    [TestMethod]
    public void IsInstanceOfTypeUsingGenericType_WhenValueIsNull_Fails()
    {
        static void action() => TestFrameworkV2.Assert.IsInstanceOfType<AssertTests>(null);
        ActionUtility.ActionShouldThrowExceptionOfType(action, typeof(TestFrameworkV2.AssertFailedException));
    }

    [TestMethod]
    public void IsInstanceOfTypeUsingGenericType_OnSameInstance_DoesNotThrow()
    {
        TestFrameworkV2.Assert.IsInstanceOfType<int>(5);
    }

    [TestMethod]
    public void IsInstanceOfTypeUsingGenericType_OnHigherInstance_DoesNotThrow()
    {
        TestFrameworkV2.Assert.IsInstanceOfType<object>(5);
    }

    [TestMethod]
    public void IsNotInstanceOfTypeUsingGenericType_WhenValueIsNull_DoesNotThrow()
    {
        TestFrameworkV2.Assert.IsNotInstanceOfType<object>(null);
    }

    [TestMethod]
    public void IsNotInstanceOfType_OnWrongInstanceUsingGenericType_DoesNotThrow()
    {
        TestFrameworkV2.Assert.IsNotInstanceOfType<int>(5L);
    }

    [TestMethod]
    public void IsNotInstanceOfTypeUsingGenericType_OnSubInstance_DoesNotThrow()
    {
        TestFrameworkV2.Assert.IsNotInstanceOfType<int>(new object());
    }
}
