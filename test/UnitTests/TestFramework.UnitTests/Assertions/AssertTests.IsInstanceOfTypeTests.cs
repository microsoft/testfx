// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable enable

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.VisualStudio.TestPlatform.TestFramework.UnitTests;

public partial class AssertTests
{
    public void InstanceOfTypeShouldFailWhenValueIsNull()
    {
        static void Action() => Assert.IsInstanceOfType(null, typeof(AssertTests));
        Exception ex = VerifyThrows(Action);
        Verify(ex is AssertFailedException);
    }

    public void InstanceOfTypeShouldFailWhenTypeIsNull()
    {
        static void Action() => Assert.IsInstanceOfType(5, null);
        Exception ex = VerifyThrows(Action);
        Verify(ex is AssertFailedException);
    }

    public void InstanceOfTypeShouldPassOnSameInstance() => Assert.IsInstanceOfType(5, typeof(int));

    public void InstanceOfTypeShouldPassOnHigherInstance() => Assert.IsInstanceOfType(5, typeof(object));

    public void InstanceNotOfTypeShouldFailWhenValueIsNull() => Assert.IsNotInstanceOfType(null, typeof(object));

    public void InstanceNotOfTypeShouldFailWhenTypeIsNull()
    {
        static void Action() => Assert.IsNotInstanceOfType(5, null);
        Exception ex = VerifyThrows(Action);
        Verify(ex is AssertFailedException);
    }

    public void InstanceNotOfTypeShouldPassOnWrongInstance() => Assert.IsNotInstanceOfType(5L, typeof(int));

    public void InstanceNotOfTypeShouldPassOnSubInstance() => Assert.IsNotInstanceOfType(new object(), typeof(int));

    [TestMethod]
    public void IsInstanceOfTypeUsingGenericType_WhenValueIsNull_Fails()
    {
        static void Action() => Assert.IsInstanceOfType<AssertTests>(null);
        Exception ex = VerifyThrows(Action);
        Verify(ex is AssertFailedException);
    }

    [TestMethod]
    public void IsInstanceOfTypeUsingGenericTypeWithOutParameter_WhenValueIsNull_Fails()
    {
        AssertTests? assertTests = null;
        void Action() => Assert.IsInstanceOfType<AssertTests>(null, out assertTests);
        Exception ex = VerifyThrows(Action);
        Verify(ex is AssertFailedException);
        Verify(assertTests is null);
    }

    [TestMethod]
    public void IsInstanceOfTypeUsingGenericType_OnSameInstance_DoesNotThrow() => Assert.IsInstanceOfType<int>(5);

    [TestMethod]
    public void IsInstanceOfTypeUsingGenericTypeWithOutParameter_OnSameInstance_DoesNotThrow()
    {
        Assert.IsInstanceOfType<int>(5, out int instance);
        Verify(instance == 5);
    }

    [TestMethod]
    public void IsInstanceOfTypeUsingGenericTypeWithOutParameter_OnSameInstanceReferenceType_DoesNotThrow()
    {
        object testInstance = new AssertTests();
        Assert.IsInstanceOfType<AssertTests>(testInstance, out AssertTests instance);
        Verify(testInstance == instance);
    }

    [TestMethod]
    public void IsInstanceOfTypeUsingGenericType_OnHigherInstance_DoesNotThrow() => Assert.IsInstanceOfType<object>(5);

    [TestMethod]
    public void IsInstanceOfTypeUsingGenericTypeWithOutParameter_OnHigherInstance_DoesNotThrow()
    {
        object testInstance = new AssertTests();
        Assert.IsInstanceOfType<object>(testInstance, out object instance);
        Verify(instance == testInstance);
    }

    [TestMethod]
    public void IsNotInstanceOfTypeUsingGenericType_WhenValueIsNull_DoesNotThrow() => Assert.IsNotInstanceOfType<object>(null);

    [TestMethod]
    public void IsNotInstanceOfType_OnWrongInstanceUsingGenericType_DoesNotThrow() => Assert.IsNotInstanceOfType<int>(5L);

    [TestMethod]
    public void IsNotInstanceOfTypeUsingGenericType_OnSubInstance_DoesNotThrow() => Assert.IsNotInstanceOfType<int>(new object());

    [TestMethod]
    public void IsInstanceOfType_WhenNonNullNullableValue_LearnNonNull()
    {
        object? obj = GetObj();
        Assert.IsInstanceOfType(obj, typeof(object));
        _ = obj.ToString(); // no warning about possible null
    }

    [TestMethod]
    public void IsInstanceOfType_WhenNonNullNullableType_LearnNonNull()
    {
        Type? objType = GetObjType();
        Assert.IsInstanceOfType(new object(), objType);
        _ = objType.ToString(); // no warning about possible null
    }

    [TestMethod]
    public void IsInstanceOfTypeGeneric_WhenNonNullNullableValue_LearnNonNull()
    {
        object? obj = GetObj();
        Assert.IsInstanceOfType<object>(obj);
        _ = obj.ToString(); // no warning about possible null
    }

    [TestMethod]
    public void IsInstanceOfTypeGenericWithOutParameter_WhenNonNullNullableValue_LearnNonNull()
    {
        object? obj = GetObj();
        Assert.IsInstanceOfType<object>(obj, out object _);
        _ = obj.ToString(); // no warning about possible null
    }

    [TestMethod]
    public void IsInstanceOfType_WhenNonNullNullableValueAndMessage_LearnNonNull()
    {
        object? obj = GetObj();
        Assert.IsInstanceOfType(obj, typeof(object), "my message");
        _ = obj.ToString(); // no warning about possible null
    }

    [TestMethod]
    public void IsInstanceOfType_WhenNonNullNullableTypeAndMessage_LearnNonNull()
    {
        Type? objType = GetObjType();
        Assert.IsInstanceOfType(new object(), objType, "my message");
        _ = objType.ToString(); // no warning about possible null
    }

    [TestMethod]
    public void IsInstanceOfTypeGeneric_WhenNonNullNullableValueAndMessage_LearnNonNull()
    {
        object? obj = GetObj();
        Assert.IsInstanceOfType<object>(obj, "my message");
        _ = obj.ToString(); // no warning about possible null
    }

    [TestMethod]
    public void IsInstanceOfTypeGenericWithOutParameter_WhenNonNullNullableValueAndMessage_LearnNonNull()
    {
        object? obj = GetObj();
        Assert.IsInstanceOfType<object>(obj, out object _, "my message");
        _ = obj.ToString(); // no warning about possible null
    }

    [TestMethod]
    public void IsInstanceOfType_WhenNonNullNullableValueAndCompositeMessage_LearnNonNull()
    {
        object? obj = GetObj();
        Assert.IsInstanceOfType(obj, typeof(object), "my message with {0}", "arg");
        _ = obj.ToString(); // no warning about possible null
    }

    [TestMethod]
    public void IsInstanceOfType_WhenNonNullNullableTypeAndCompositeMessage_LearnNonNull()
    {
        Type? objType = GetObjType();
        Assert.IsInstanceOfType(new object(), objType, "my message with {0}", "arg");
        _ = objType.ToString(); // no warning about possible null
    }

    [TestMethod]
    public void IsInstanceOfTypeGeneric_WhenNonNullNullableValueAndCompositeMessage_LearnNonNull()
    {
        object? obj = GetObj();
        Assert.IsInstanceOfType<object>(obj, "my message with {0}", "arg");
        _ = obj.ToString(); // no warning about possible null
    }

    [TestMethod]
    public void IsInstanceOfTypeGenericWithOutParameter_WhenNonNullNullableValueAndCompositeMessage_LearnNonNull()
    {
        object? obj = GetObj();
        Assert.IsInstanceOfType<object>(obj, out object _, "my message with {0}", "arg");
        _ = obj.ToString(); // no warning about possible null
    }

    [TestMethod]
    public void IsNotInstanceOfType_WhenNonNullNullableType_LearnNonNull()
    {
        Type? intType = GetIntType();
        Assert.IsNotInstanceOfType(new object(), intType);
        _ = intType.ToString(); // no warning about possible null
    }

    [TestMethod]
    public void IsNotInstanceOfType_WhenNonNullNullableTypeAndMessage_LearnNonNull()
    {
        Type? intType = GetIntType();
        Assert.IsNotInstanceOfType(new object(), intType, "my message");
        _ = intType.ToString(); // no warning about possible null
    }

    [TestMethod]
    public void IsNotInstanceOfType_WhenNonNullNullableTypeAndCompositeMessage_LearnNonNull()
    {
        Type? intType = GetIntType();
        Assert.IsNotInstanceOfType(new object(), intType, "my message with {0}", "arg");
        _ = intType.ToString(); // no warning about possible null
    }

    private object? GetObj() => new();

    private Type? GetObjType() => typeof(object);

    private Type? GetIntType() => typeof(int);
}
