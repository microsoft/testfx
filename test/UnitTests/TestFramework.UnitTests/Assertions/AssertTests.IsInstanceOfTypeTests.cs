// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestPlatform.TestFramework.UnitTests;

[SuppressMessage("Usage", "CA2263:Prefer generic overload when type is known", Justification = "We want to test also the non-generic API")]
public partial class AssertTests
{
    public void InstanceOfTypeShouldFailWhenValueIsNull()
    {
        Exception ex = VerifyThrows<AssertFailedException>(() => Assert.IsInstanceOfType(null, typeof(AssertTests)));
        Verify(ex.Message == "Assert.IsInstanceOfType failed. ");
    }

    public void InstanceOfTypeShouldFailWhenTypeIsNull()
    {
        Exception ex = VerifyThrows<AssertFailedException>(() => Assert.IsInstanceOfType(5, null));
        Verify(ex.Message == "Assert.IsInstanceOfType failed. ");
    }

    public void InstanceOfTypeShouldFailWhenTypeIsMismatched()
    {
        Exception ex = VerifyThrows<AssertFailedException>(() => Assert.IsInstanceOfType(5, typeof(string)));
        Verify(ex.Message == "Assert.IsInstanceOfType failed.  Expected type:<System.String>. Actual type:<System.Int32>.");
    }

    public void InstanceOfTypeShouldPassOnSameInstance() => Assert.IsInstanceOfType(5, typeof(int));

    public void InstanceOfTypeShouldPassOnHigherInstance() => Assert.IsInstanceOfType(5, typeof(object));

    public void InstanceOfType_WithStringMessage_ShouldFailWhenValueIsNull()
    {
        Exception ex = VerifyThrows<AssertFailedException>(() => Assert.IsInstanceOfType(null, typeof(AssertTests), "User-provided message"));
        Verify(ex.Message == "Assert.IsInstanceOfType failed. User-provided message");
    }

    public void InstanceOfType_WithStringMessage_ShouldFailWhenTypeIsNull()
    {
        Exception ex = VerifyThrows<AssertFailedException>(() => Assert.IsInstanceOfType(5, null, "User-provided message"));
        Verify(ex.Message == "Assert.IsInstanceOfType failed. User-provided message");
    }

    public void InstanceOfType_WithStringMessage_ShouldFailWhenTypeIsMismatched()
    {
        Exception ex = VerifyThrows<AssertFailedException>(() => Assert.IsInstanceOfType(5, typeof(string), "User-provided message"));
        Verify(ex.Message == "Assert.IsInstanceOfType failed. User-provided message Expected type:<System.String>. Actual type:<System.Int32>.");
    }

    public void InstanceOfType_WithStringMessage_ShouldPassWhenTypeIsCorrect()
        => Assert.IsInstanceOfType(5, typeof(int), "User-provided message");

    public async Task InstanceOfType_WithInterpolatedString_ShouldFailWhenValueIsNull()
    {
        DummyClassTrackingToStringCalls o = new();
        DateTime dateTime = DateTime.Now;
        Exception ex = await VerifyThrowsAsync<AssertFailedException>(async () => Assert.IsInstanceOfType(null, typeof(AssertTests), $"User-provided message. {o}, {o,35}, {await GetHelloStringAsync()}, {new DummyIFormattable()}, {dateTime:tt}, {dateTime,5:tt}"));
        Verify(ex.Message == $"Assert.IsInstanceOfType failed. User-provided message. DummyClassTrackingToStringCalls,     DummyClassTrackingToStringCalls, Hello, DummyIFormattable.ToString(), {string.Format(null, "{0:tt}", dateTime)}, {string.Format(null, "{0,5:tt}", dateTime)}");
        Verify(o.WasToStringCalled);
    }

    public void InstanceOfType_WithInterpolatedString_ShouldFailWhenTypeIsNull()
    {
        DummyClassTrackingToStringCalls o = new();
        Exception ex = VerifyThrows<AssertFailedException>(() => Assert.IsInstanceOfType(5, null, $"User-provided message {o}"));
        Verify(ex.Message == "Assert.IsInstanceOfType failed. User-provided message DummyClassTrackingToStringCalls");
        Verify(o.WasToStringCalled);
    }

    public void InstanceOfType_WithInterpolatedString_ShouldFailWhenTypeIsMismatched()
    {
        DummyClassTrackingToStringCalls o = new();
        Exception ex = VerifyThrows<AssertFailedException>(() => Assert.IsInstanceOfType(5, typeof(string), $"User-provided message {o}"));
        Verify(ex.Message == "Assert.IsInstanceOfType failed. User-provided message DummyClassTrackingToStringCalls Expected type:<System.String>. Actual type:<System.Int32>.");
        Verify(o.WasToStringCalled);
    }

    public void InstanceOfType_WithInterpolatedString_ShouldPassWhenTypeIsCorrect()
    {
        DummyClassTrackingToStringCalls o = new();
        Assert.IsInstanceOfType(5, typeof(int), $"User-provided message {o}");
        Verify(!o.WasToStringCalled);
    }

    public void InstanceNotOfTypeShouldFailWhenValueIsNull() => Assert.IsNotInstanceOfType(null, typeof(object));

    public void InstanceNotOfTypeShouldFailWhenTypeIsNull() =>
        VerifyThrows<AssertFailedException>(() => Assert.IsNotInstanceOfType(5, null));

    public void InstanceNotOfTypeShouldPassOnWrongInstance() => Assert.IsNotInstanceOfType(5L, typeof(int));

    public void InstanceNotOfTypeShouldPassOnSubInstance() => Assert.IsNotInstanceOfType(new object(), typeof(int));

    public void IsInstanceOfTypeUsingGenericType_WhenValueIsNull_Fails()
    {
        Exception ex = VerifyThrows<AssertFailedException>(() => Assert.IsInstanceOfType<AssertTests>(null));
        Verify(ex.Message == "Assert.IsInstanceOfType failed. ");
    }

    public void IsInstanceOfTypeUsingGenericType_WhenTypeMismatch_Fails()
    {
        Exception ex = VerifyThrows<AssertFailedException>(() => Assert.IsInstanceOfType<string>(5));
        Verify(ex.Message == "Assert.IsInstanceOfType failed.  Expected type:<System.String>. Actual type:<System.Int32>.");
    }

    public void IsInstanceOfTypeUsingGenericTypeWithOutParameter_WhenValueIsNull_Fails()
    {
        AssertTests? assertTests = null;
        VerifyThrows<AssertFailedException>(() => assertTests = Assert.IsInstanceOfType<AssertTests>(null));
        Verify(assertTests is null);
    }

    public void IsInstanceOfTypeUsingGenericTypeWithOutParameter_WhenTypeMismatch_Fails()
    {
        Exception ex = VerifyThrows<AssertFailedException>(() => Assert.IsInstanceOfType<string>(5));
        Verify(ex.Message == "Assert.IsInstanceOfType failed.  Expected type:<System.String>. Actual type:<System.Int32>.");
    }

    public void IsInstanceOfTypeUsingGenericType_OnSameInstance_DoesNotThrow() => Assert.IsInstanceOfType<int>(5);

    public void IsInstanceOfTypeUsingGenericTypeWithOutParameter_OnSameInstance_DoesNotThrow()
    {
        int instance = Assert.IsInstanceOfType<int>(5);
        Verify(instance == 5);
    }

    public void IsInstanceOfTypeUsingGenericTypeWithOutParameter_OnSameInstanceReferenceType_DoesNotThrow()
    {
        object testInstance = new AssertTests();
        AssertTests instance = Assert.IsInstanceOfType<AssertTests>(testInstance);
        Verify(testInstance == instance);
    }

    public void IsInstanceOfTypeUsingGenericType_OnHigherInstance_DoesNotThrow() => Assert.IsInstanceOfType<object>(5);

    public void IsInstanceOfTypeUsingGenericTypeWithOutParameter_OnHigherInstance_DoesNotThrow()
    {
        object testInstance = new AssertTests();
        object instance = Assert.IsInstanceOfType<object>(testInstance);
        Verify(instance == testInstance);
    }

    public void IsNotInstanceOfTypeUsingGenericType_WhenValueIsNull_DoesNotThrow() => Assert.IsNotInstanceOfType<object>(null);

    public void IsNotInstanceOfType_OnWrongInstanceUsingGenericType_DoesNotThrow() => Assert.IsNotInstanceOfType<int>(5L);

    public void IsNotInstanceOfTypeUsingGenericType_OnSubInstance_DoesNotThrow() => Assert.IsNotInstanceOfType<int>(new object());

    public void IsInstanceOfType_WhenNonNullNullableValue_LearnNonNull()
    {
        object? obj = GetObj();
        Assert.IsInstanceOfType(obj, typeof(object));
        _ = obj.ToString(); // no warning about possible null
    }

    public void IsInstanceOfType_WhenNonNullNullableType_LearnNonNull()
    {
        Type? objType = GetObjType();
        Assert.IsInstanceOfType(new object(), objType);
        _ = objType.ToString(); // no warning about possible null
    }

    public void IsInstanceOfTypeGeneric_WhenNonNullNullableValue_LearnNonNull()
    {
        object? obj = GetObj();
        Assert.IsInstanceOfType<object>(obj);
        _ = obj.ToString(); // no warning about possible null
    }

    public void IsInstanceOfTypeGenericWithOutParameter_WhenNonNullNullableValue_LearnNonNull()
    {
        object? obj = GetObj();
        Assert.IsInstanceOfType<object>(obj);
        _ = obj.ToString(); // no warning about possible null
    }

    public void IsInstanceOfType_WhenNonNullNullableValueAndMessage_LearnNonNull()
    {
        object? obj = GetObj();
        Assert.IsInstanceOfType(obj, typeof(object), "my message");
        _ = obj.ToString(); // no warning about possible null
    }

    public void IsInstanceOfType_WhenNonNullNullableTypeAndMessage_LearnNonNull()
    {
        Type? objType = GetObjType();
        Assert.IsInstanceOfType(new object(), objType, "my message");
        _ = objType.ToString(); // no warning about possible null
    }

    public void IsInstanceOfTypeGeneric_WhenNonNullNullableValueAndMessage_LearnNonNull()
    {
        object? obj = GetObj();
        Assert.IsInstanceOfType<object>(obj, "my message");
        _ = obj.ToString(); // no warning about possible null
    }

    public void IsInstanceOfTypeGenericWithOutParameter_WhenNonNullNullableValueAndMessage_LearnNonNull()
    {
        object? obj = GetObj();
        Assert.IsInstanceOfType<object>(obj, "my message");
        _ = obj.ToString(); // no warning about possible null
    }

    public void IsNotInstanceOfType_WhenNonNullNullableType_LearnNonNull()
    {
        Type? intType = GetIntType();
        Assert.IsNotInstanceOfType(new object(), intType);
        _ = intType.ToString(); // no warning about possible null
    }

    public void IsNotInstanceOfType_WhenNonNullNullableTypeAndMessage_LearnNonNull()
    {
        Type? intType = GetIntType();
        Assert.IsNotInstanceOfType(new object(), intType, "my message");
        _ = intType.ToString(); // no warning about possible null
    }

    private object? GetObj() => new();

    private Type? GetObjType() => typeof(object);

    private Type? GetIntType() => typeof(int);
}
