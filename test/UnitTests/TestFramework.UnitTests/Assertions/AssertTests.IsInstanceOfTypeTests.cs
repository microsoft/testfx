// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.VisualStudio.TestPlatform.TestFramework.UnitTests;
public partial class AssertTests
{
    public void InstanceOfTypeShouldFailWhenValueIsNull()
    {
        static void Action() => Assert.IsInstanceOfType(null, typeof(AssertTests));
        var ex = VerifyThrows(Action);
        Verify(ex is AssertFailedException);
    }

    public void InstanceOfTypeShouldFailWhenTypeIsNull()
    {
        static void Action() => Assert.IsInstanceOfType(5, null);
        var ex = VerifyThrows(Action);
        Verify(ex is AssertFailedException);
    }

    public void InstanceOfTypeShouldPassOnSameInstance()
    {
        Assert.IsInstanceOfType(5, typeof(int));
    }

    public void InstanceOfTypeShouldPassOnHigherInstance()
    {
        Assert.IsInstanceOfType(5, typeof(object));
    }

    public void InstanceNotOfTypeShouldFailWhenValueIsNull()
    {
        Assert.IsNotInstanceOfType(null, typeof(object));
    }

    public void InstanceNotOfTypeShouldFailWhenTypeIsNull()
    {
        static void Action() => Assert.IsNotInstanceOfType(5, null);
        var ex = VerifyThrows(Action);
        Verify(ex is AssertFailedException);
    }

    public void InstanceNotOfTypeShouldPassOnWrongInstance()
    {
        Assert.IsNotInstanceOfType(5L, typeof(int));
    }

    public void InstanceNotOfTypeShouldPassOnSubInstance()
    {
        Assert.IsNotInstanceOfType(new object(), typeof(int));
    }

    [TestMethod]
    public void IsInstanceOfTypeUsingGenericType_WhenValueIsNull_Fails()
    {
        static void Action() => Assert.IsInstanceOfType<AssertTests>(null);
        var ex = VerifyThrows(Action);
        Verify(ex is AssertFailedException);
    }

    [TestMethod]
    public void IsInstanceOfTypeUsingGenericType_OnSameInstance_DoesNotThrow()
    {
        Assert.IsInstanceOfType<int>(5);
    }

    [TestMethod]
    public void IsInstanceOfTypeUsingGenericType_OnHigherInstance_DoesNotThrow()
    {
        Assert.IsInstanceOfType<object>(5);
    }

    [TestMethod]
    public void IsNotInstanceOfTypeUsingGenericType_WhenValueIsNull_DoesNotThrow()
    {
        Assert.IsNotInstanceOfType<object>(null);
    }

    [TestMethod]
    public void IsNotInstanceOfType_OnWrongInstanceUsingGenericType_DoesNotThrow()
    {
        Assert.IsNotInstanceOfType<int>(5L);
    }

    [TestMethod]
    public void IsNotInstanceOfTypeUsingGenericType_OnSubInstance_DoesNotThrow()
    {
        Assert.IsNotInstanceOfType<int>(new object());
    }
}
