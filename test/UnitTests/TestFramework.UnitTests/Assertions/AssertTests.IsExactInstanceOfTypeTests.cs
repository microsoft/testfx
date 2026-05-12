// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using AwesomeAssertions;

namespace Microsoft.VisualStudio.TestPlatform.TestFramework.UnitTests;

[SuppressMessage("Usage", "CA2263:Prefer generic overload when type is known", Justification = "We want to test also the non-generic API")]
public partial class AssertTests
{
    public void ExactInstanceOfTypeShouldFailWhenValueIsNull()
    {
        Action action = () => Assert.IsExactInstanceOfType(null, typeof(AssertTests));
        action.Should().Throw<AssertFailedException>()
            .WithMessage($"Assertion failed. Expected value to be exactly of type AssertTests.{Environment.NewLine}{Environment.NewLine}expected type: Microsoft.VisualStudio.TestPlatform.TestFramework.UnitTests.AssertTests{Environment.NewLine}actual:        null{Environment.NewLine}{Environment.NewLine}Assert.IsExactInstanceOfType(null)");
    }

    public void ExactInstanceOfTypeShouldFailWhenTypeIsNull()
    {
        Action action = () => Assert.IsExactInstanceOfType(5, null);
        action.Should().Throw<AssertFailedException>()
            .WithMessage($"Assertion failed. Expected value to be exactly of type null.{Environment.NewLine}{Environment.NewLine}Assert.IsExactInstanceOfType(5)");
    }

    public void ExactInstanceOfTypeShouldFailWhenTypeIsMismatched()
    {
        Action action = () => Assert.IsExactInstanceOfType(5, typeof(string));
        action.Should().Throw<AssertFailedException>()
            .WithMessage($"Assertion failed. Expected value to be exactly of type String.{Environment.NewLine}{Environment.NewLine}expected type: System.String{Environment.NewLine}actual type:   System.Int32{Environment.NewLine}actual value:  5{Environment.NewLine}{Environment.NewLine}Assert.IsExactInstanceOfType(5)");
    }

    public void ExactInstanceOfTypeShouldPassOnSameInstance() => Assert.IsExactInstanceOfType(5, typeof(int));

    public void ExactInstanceOfTypeShouldFailOnHigherInstance()
    {
        Action action = () => Assert.IsExactInstanceOfType(5, typeof(object));
        action.Should().Throw<AssertFailedException>()
            .WithMessage($"Assertion failed. Expected value to be exactly of type Object.{Environment.NewLine}{Environment.NewLine}expected type: System.Object{Environment.NewLine}actual type:   System.Int32{Environment.NewLine}actual value:  5{Environment.NewLine}{Environment.NewLine}Assert.IsExactInstanceOfType(5)");
    }

    public void ExactInstanceOfTypeShouldFailOnDerivedType()
    {
        object x = new MemoryStream();
        Action action = () => Assert.IsExactInstanceOfType(x, typeof(Stream));
        action.Should().Throw<AssertFailedException>()
            .WithMessage($"Assertion failed. Expected value to be exactly of type Stream.{Environment.NewLine}{Environment.NewLine}expected type: System.IO.Stream{Environment.NewLine}actual type:   System.IO.MemoryStream{Environment.NewLine}actual value:  *{Environment.NewLine}{Environment.NewLine}Assert.IsExactInstanceOfType(x)");
    }

    public void ExactInstanceOfTypeShouldPassOnExactType()
    {
        object x = new MemoryStream();
        Assert.IsExactInstanceOfType(x, typeof(MemoryStream));
    }

    public void ExactInstanceOfType_WithStringMessage_ShouldFailWhenValueIsNull()
    {
        Action action = () => Assert.IsExactInstanceOfType(null, typeof(AssertTests), "User-provided message");
        action.Should().Throw<AssertFailedException>()
            .WithMessage($"Assertion failed. Expected value to be exactly of type AssertTests.{Environment.NewLine}User-provided message{Environment.NewLine}{Environment.NewLine}expected type: Microsoft.VisualStudio.TestPlatform.TestFramework.UnitTests.AssertTests{Environment.NewLine}actual:        null{Environment.NewLine}{Environment.NewLine}Assert.IsExactInstanceOfType(null)");
    }

    public void ExactInstanceOfType_WithStringMessage_ShouldFailWhenTypeIsNull()
    {
        Action action = () => Assert.IsExactInstanceOfType(5, null, "User-provided message");
        action.Should().Throw<AssertFailedException>()
            .WithMessage($"Assertion failed. Expected value to be exactly of type null.{Environment.NewLine}User-provided message{Environment.NewLine}{Environment.NewLine}Assert.IsExactInstanceOfType(5)");
    }

    public void ExactInstanceOfType_WithStringMessage_ShouldFailWhenTypeIsMismatched()
    {
        Action action = () => Assert.IsExactInstanceOfType(5, typeof(string), "User-provided message");
        action.Should().Throw<AssertFailedException>()
            .WithMessage($"Assertion failed. Expected value to be exactly of type String.{Environment.NewLine}User-provided message{Environment.NewLine}{Environment.NewLine}expected type: System.String{Environment.NewLine}actual type:   System.Int32{Environment.NewLine}actual value:  5{Environment.NewLine}{Environment.NewLine}Assert.IsExactInstanceOfType(5)");
    }

    public void ExactInstanceOfType_WithStringMessage_ShouldPassWhenTypeIsCorrect()
        => Assert.IsExactInstanceOfType(5, typeof(int), "User-provided message");

    public async Task ExactInstanceOfType_WithInterpolatedString_ShouldFailWhenValueIsNull()
    {
        DummyClassTrackingToStringCalls o = new();
        DateTime dateTime = DateTime.Now;
        Func<Task> action = async () => Assert.IsExactInstanceOfType(null, typeof(AssertTests), $"User-provided message. {o}, {o,35}, {await GetHelloStringAsync()}, {new DummyIFormattable()}, {dateTime:tt}, {dateTime,5:tt}");
        (await action.Should().ThrowAsync<AssertFailedException>())
            .WithMessage($"Assertion failed. Expected value to be exactly of type AssertTests.{Environment.NewLine}User-provided message. DummyClassTrackingToStringCalls,     DummyClassTrackingToStringCalls, Hello, DummyIFormattable.ToString(), {string.Format(null, "{0:tt}", dateTime)}, {string.Format(null, "{0,5:tt}", dateTime)}{Environment.NewLine}{Environment.NewLine}expected type: Microsoft.VisualStudio.TestPlatform.TestFramework.UnitTests.AssertTests{Environment.NewLine}actual:        null{Environment.NewLine}{Environment.NewLine}Assert.IsExactInstanceOfType(null)");
        o.WasToStringCalled.Should().BeTrue();
    }

    public void ExactInstanceOfType_WithInterpolatedString_ShouldFailWhenTypeIsNull()
    {
        DummyClassTrackingToStringCalls o = new();
        Action action = () => Assert.IsExactInstanceOfType(5, null, $"User-provided message {o}");
        action.Should().Throw<AssertFailedException>()
            .WithMessage($"Assertion failed. Expected value to be exactly of type null.{Environment.NewLine}User-provided message DummyClassTrackingToStringCalls{Environment.NewLine}{Environment.NewLine}Assert.IsExactInstanceOfType(5)");
        o.WasToStringCalled.Should().BeTrue();
    }

    public void ExactInstanceOfType_WithInterpolatedString_ShouldFailWhenTypeIsMismatched()
    {
        DummyClassTrackingToStringCalls o = new();
        Action action = () => Assert.IsExactInstanceOfType(5, typeof(string), $"User-provided message {o}");
        action.Should().Throw<AssertFailedException>()
            .WithMessage($"Assertion failed. Expected value to be exactly of type String.{Environment.NewLine}User-provided message DummyClassTrackingToStringCalls{Environment.NewLine}{Environment.NewLine}expected type: System.String{Environment.NewLine}actual type:   System.Int32{Environment.NewLine}actual value:  5{Environment.NewLine}{Environment.NewLine}Assert.IsExactInstanceOfType(5)");
        o.WasToStringCalled.Should().BeTrue();
    }

    public void ExactInstanceOfType_WithInterpolatedString_ShouldPassWhenTypeIsCorrect()
    {
        DummyClassTrackingToStringCalls o = new();
        Assert.IsExactInstanceOfType(5, typeof(int), $"User-provided message {o}");
        o.WasToStringCalled.Should().BeFalse();
    }

    public void ExactInstanceNotOfTypeShouldPassWhenValueIsNull() => Assert.IsNotExactInstanceOfType(null, typeof(object));

    public void ExactInstanceNotOfTypeShouldFailWhenTypeIsNull()
    {
        Action action = () => Assert.IsNotExactInstanceOfType(5, null);
        action.Should().Throw<AssertFailedException>();
    }

    public void ExactInstanceNotOfTypeShouldPassOnWrongInstance() => Assert.IsNotExactInstanceOfType(5L, typeof(int));

    public void ExactInstanceNotOfTypeShouldPassOnSubInstance() => Assert.IsNotExactInstanceOfType(new object(), typeof(int));

    public void ExactInstanceNotOfTypeShouldPassOnDerivedType()
    {
        object x = new MemoryStream();
        Assert.IsNotExactInstanceOfType(x, typeof(Stream));
    }

    public void ExactInstanceNotOfTypeShouldFailOnExactType()
    {
        object x = new MemoryStream();
        Action action = () => Assert.IsNotExactInstanceOfType(x, typeof(MemoryStream));
        action.Should().Throw<AssertFailedException>()
            .WithMessage($"Assertion failed. Expected value to not be exactly of type MemoryStream.{Environment.NewLine}{Environment.NewLine}not expected type: System.IO.MemoryStream{Environment.NewLine}actual value:      *{Environment.NewLine}{Environment.NewLine}Assert.IsNotExactInstanceOfType(x)");
    }

    public void IsExactInstanceOfTypeUsingGenericType_WhenValueIsNull_Fails()
    {
        Action action = () => Assert.IsExactInstanceOfType<AssertTests>(null);
        action.Should().Throw<AssertFailedException>()
            .WithMessage($"Assertion failed. Expected value to be exactly of type AssertTests.{Environment.NewLine}{Environment.NewLine}expected type: Microsoft.VisualStudio.TestPlatform.TestFramework.UnitTests.AssertTests{Environment.NewLine}actual:        null{Environment.NewLine}{Environment.NewLine}Assert.IsExactInstanceOfType(null)");
    }

    public void IsExactInstanceOfTypeUsingGenericType_WhenTypeMismatch_Fails()
    {
        Action action = () => Assert.IsExactInstanceOfType<string>(5);
        action.Should().Throw<AssertFailedException>()
            .WithMessage($"Assertion failed. Expected value to be exactly of type String.{Environment.NewLine}{Environment.NewLine}expected type: System.String{Environment.NewLine}actual type:   System.Int32{Environment.NewLine}actual value:  5{Environment.NewLine}{Environment.NewLine}Assert.IsExactInstanceOfType(5)");
    }

    public void IsExactInstanceOfTypeUsingGenericType_WhenDerivedType_Fails()
    {
        object x = new MemoryStream();
        Action action = () => Assert.IsExactInstanceOfType<Stream>(x);
        action.Should().Throw<AssertFailedException>()
            .WithMessage($"Assertion failed. Expected value to be exactly of type Stream.{Environment.NewLine}{Environment.NewLine}expected type: System.IO.Stream{Environment.NewLine}actual type:   System.IO.MemoryStream{Environment.NewLine}actual value:  *{Environment.NewLine}{Environment.NewLine}Assert.IsExactInstanceOfType(x)");
    }

    public void IsExactInstanceOfTypeUsingGenericType_OnSameInstance_DoesNotThrow() => Assert.IsExactInstanceOfType<int>(5);

    public void IsExactInstanceOfTypeUsingGenericTypeWithReturn_OnSameInstance_DoesNotThrow()
    {
        int instance = Assert.IsExactInstanceOfType<int>(5);
        instance.Should().Be(5);
    }

    public void IsExactInstanceOfTypeUsingGenericTypeWithReturn_OnSameInstanceReferenceType_DoesNotThrow()
    {
        object testInstance = new AssertTests();
        AssertTests instance = Assert.IsExactInstanceOfType<AssertTests>(testInstance);
        testInstance.Should().BeSameAs(instance);
    }

    public void IsExactInstanceOfTypeUsingGenericType_OnHigherInstance_Fails()
    {
        Action action = () => Assert.IsExactInstanceOfType<object>(5);
        action.Should().Throw<AssertFailedException>()
            .WithMessage($"Assertion failed. Expected value to be exactly of type Object.{Environment.NewLine}{Environment.NewLine}expected type: System.Object{Environment.NewLine}actual type:   System.Int32{Environment.NewLine}actual value:  5{Environment.NewLine}{Environment.NewLine}Assert.IsExactInstanceOfType(5)");
    }

    public void IsExactInstanceOfTypeUsingGenericTypeWithReturn_OnExactType_DoesNotThrow()
    {
        object testInstance = new AssertTests();
        AssertTests instance = Assert.IsExactInstanceOfType<AssertTests>(testInstance);
        instance.Should().BeSameAs(testInstance);
    }

    public void IsNotExactInstanceOfTypeUsingGenericType_WhenValueIsNull_DoesNotThrow() => Assert.IsNotExactInstanceOfType<object>(null);

    public void IsNotExactInstanceOfType_OnWrongInstanceUsingGenericType_DoesNotThrow() => Assert.IsNotExactInstanceOfType<int>(5L);

    public void IsNotExactInstanceOfTypeUsingGenericType_OnSubInstance_DoesNotThrow() => Assert.IsNotExactInstanceOfType<int>(new object());

    public void IsNotExactInstanceOfTypeUsingGenericType_OnDerivedType_DoesNotThrow()
    {
        object x = new MemoryStream();
        Assert.IsNotExactInstanceOfType<Stream>(x);
    }

    public void IsNotExactInstanceOfTypeUsingGenericType_OnExactType_Fails()
    {
        object x = new MemoryStream();
        Action action = () => Assert.IsNotExactInstanceOfType<MemoryStream>(x);
        action.Should().Throw<AssertFailedException>()
            .WithMessage($"Assertion failed. Expected value to not be exactly of type MemoryStream.{Environment.NewLine}{Environment.NewLine}not expected type: System.IO.MemoryStream{Environment.NewLine}actual value:      *{Environment.NewLine}{Environment.NewLine}Assert.IsNotExactInstanceOfType(x)");
    }

    public void IsExactInstanceOfType_WhenNonNullNullableValue_LearnNonNull()
    {
        object? obj = GetObj();
        Assert.IsExactInstanceOfType(obj, typeof(object));
        _ = obj.ToString(); // no warning about possible null
    }

    public void IsExactInstanceOfType_WhenNonNullNullableType_LearnNonNull()
    {
        Type? objType = GetObjType();
        Assert.IsExactInstanceOfType(new object(), objType);
        _ = objType.ToString(); // no warning about possible null
    }

    public void IsExactInstanceOfTypeGeneric_WhenNonNullNullableValue_LearnNonNull()
    {
        object? obj = GetObj();
        Assert.IsExactInstanceOfType<object>(obj);
        _ = obj.ToString(); // no warning about possible null
    }

    public void IsExactInstanceOfTypeGenericWithReturn_WhenNonNullNullableValue_LearnNonNull()
    {
        object? obj = GetObj();
        Assert.IsExactInstanceOfType<object>(obj);
        _ = obj.ToString(); // no warning about possible null
    }

    public void IsExactInstanceOfType_WhenNonNullNullableValueAndMessage_LearnNonNull()
    {
        object? obj = GetObj();
        Assert.IsExactInstanceOfType(obj, typeof(object), "my message");
        _ = obj.ToString(); // no warning about possible null
    }

    public void IsExactInstanceOfType_WhenNonNullNullableTypeAndMessage_LearnNonNull()
    {
        Type? objType = GetObjType();
        Assert.IsExactInstanceOfType(new object(), objType, "my message");
        _ = objType.ToString(); // no warning about possible null
    }

    public void IsExactInstanceOfTypeGeneric_WhenNonNullNullableValueAndMessage_LearnNonNull()
    {
        object? obj = GetObj();
        Assert.IsExactInstanceOfType<object>(obj, "my message");
        _ = obj.ToString(); // no warning about possible null
    }

    public void IsExactInstanceOfTypeGenericWithReturn_WhenNonNullNullableValueAndMessage_LearnNonNull()
    {
        object? obj = GetObj();
        Assert.IsExactInstanceOfType<object>(obj, "my message");
        _ = obj.ToString(); // no warning about possible null
    }

    public void IsNotExactInstanceOfType_WhenNonNullNullableType_LearnNonNull()
    {
        Type? intType = GetIntType();
        Assert.IsNotExactInstanceOfType(new object(), intType);
        _ = intType.ToString(); // no warning about possible null
    }

    public void IsNotExactInstanceOfType_WhenNonNullNullableTypeAndMessage_LearnNonNull()
    {
        Type? intType = GetIntType();
        Assert.IsNotExactInstanceOfType(new object(), intType, "my message");
        _ = intType.ToString(); // no warning about possible null
    }
}
