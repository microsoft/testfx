// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using AwesomeAssertions;

namespace Microsoft.VisualStudio.TestPlatform.TestFramework.UnitTests;

[SuppressMessage("Usage", "CA2263:Prefer generic overload when type is known", Justification = "We want to test also the non-generic API")]
public partial class AssertTests
{
    public void InstanceOfTypeShouldFailWhenValueIsNull()
    {
        Action action = () => Assert.IsInstanceOfType(null, typeof(AssertTests));
        action.Should().Throw<AssertFailedException>()
            .WithMessage(
                """
                Assertion failed. Expected value to be of type AssertTests (or derived).

                expected type: Microsoft.VisualStudio.TestPlatform.TestFramework.UnitTests.AssertTests (or derived)
                actual:        null

                Assert.IsInstanceOfType(null)
                """);
    }

    public void InstanceOfTypeShouldFailWhenTypeIsNull()
    {
        Action action = () => Assert.IsInstanceOfType(5, null);
        action.Should().Throw<AssertFailedException>()
            .WithMessage(
                """
                Assertion failed. Cannot check type because the expected type argument is null.

                Assert.IsInstanceOfType(5)
                """);
    }

    public void InstanceOfTypeShouldFailWhenTypeIsMismatched()
    {
        Action action = () => Assert.IsInstanceOfType(5, typeof(string));
        action.Should().Throw<AssertFailedException>()
            .WithMessage(
                """
                Assertion failed. Expected value to be of type String (or derived).

                expected type: System.String (or derived)
                actual type:   System.Int32

                Assert.IsInstanceOfType(5)
                """);
    }

    public void InstanceOfTypeShouldPassOnSameInstance() => Assert.IsInstanceOfType(5, typeof(int));

    public void InstanceOfTypeShouldPassOnHigherInstance() => Assert.IsInstanceOfType(5, typeof(object));

    public void InstanceOfType_WithStringMessage_ShouldFailWhenValueIsNull()
    {
        Action action = () => Assert.IsInstanceOfType(null, typeof(AssertTests), "User-provided message");
        action.Should().Throw<AssertFailedException>()
            .WithMessage(
                """
                Assertion failed. Expected value to be of type AssertTests (or derived).
                User-provided message

                expected type: Microsoft.VisualStudio.TestPlatform.TestFramework.UnitTests.AssertTests (or derived)
                actual:        null

                Assert.IsInstanceOfType(null)
                """);
    }

    public void InstanceOfType_WithStringMessage_ShouldFailWhenTypeIsNull()
    {
        Action action = () => Assert.IsInstanceOfType(5, null, "User-provided message");
        action.Should().Throw<AssertFailedException>()
            .WithMessage(
                """
                Assertion failed. Cannot check type because the expected type argument is null.
                User-provided message

                Assert.IsInstanceOfType(5)
                """);
    }

    public void InstanceOfType_WithStringMessage_ShouldFailWhenTypeIsMismatched()
    {
        Action action = () => Assert.IsInstanceOfType(5, typeof(string), "User-provided message");
        action.Should().Throw<AssertFailedException>()
            .WithMessage(
                """
                Assertion failed. Expected value to be of type String (or derived).
                User-provided message

                expected type: System.String (or derived)
                actual type:   System.Int32

                Assert.IsInstanceOfType(5)
                """);
    }

    public void InstanceOfType_WithStringMessage_ShouldPassWhenTypeIsCorrect()
        => Assert.IsInstanceOfType(5, typeof(int), "User-provided message");

    public async Task InstanceOfType_WithInterpolatedString_ShouldFailWhenValueIsNull()
    {
        DummyClassTrackingToStringCalls o = new();
        DateTime dateTime = DateTime.Now;
        Func<Task> action = async () => Assert.IsInstanceOfType(null, typeof(AssertTests), $"User-provided message. {o}, {o,35}, {await GetHelloStringAsync()}, {new DummyIFormattable()}, {dateTime:tt}, {dateTime,5:tt}");
        (await action.Should().ThrowAsync<AssertFailedException>())
            .WithMessage(
                $$"""
                Assertion failed. Expected value to be of type AssertTests (or derived).
                User-provided message. DummyClassTrackingToStringCalls,     DummyClassTrackingToStringCalls, Hello, DummyIFormattable.ToString(), {{string.Format(null, "{0:tt}", dateTime)}}, {{string.Format(null, "{0,5:tt}", dateTime)}}

                expected type: Microsoft.VisualStudio.TestPlatform.TestFramework.UnitTests.AssertTests (or derived)
                actual:        null

                Assert.IsInstanceOfType(null)
                """);
        o.WasToStringCalled.Should().BeTrue();
    }

    public void InstanceOfType_WithInterpolatedString_ShouldFailWhenTypeIsNull()
    {
        DummyClassTrackingToStringCalls o = new();
        Action action = () => Assert.IsInstanceOfType(5, null, $"User-provided message {o}");
        action.Should().Throw<AssertFailedException>()
            .WithMessage(
                """
                Assertion failed. Cannot check type because the expected type argument is null.
                User-provided message DummyClassTrackingToStringCalls

                Assert.IsInstanceOfType(5)
                """);
        o.WasToStringCalled.Should().BeTrue();
    }

    public void InstanceOfType_WithInterpolatedString_ShouldFailWhenTypeIsMismatched()
    {
        DummyClassTrackingToStringCalls o = new();
        Action action = () => Assert.IsInstanceOfType(5, typeof(string), $"User-provided message {o}");
        action.Should().Throw<AssertFailedException>()
            .WithMessage(
                """
                Assertion failed. Expected value to be of type String (or derived).
                User-provided message DummyClassTrackingToStringCalls

                expected type: System.String (or derived)
                actual type:   System.Int32

                Assert.IsInstanceOfType(5)
                """);
        o.WasToStringCalled.Should().BeTrue();
    }

    public void InstanceOfType_WithInterpolatedString_ShouldPassWhenTypeIsCorrect()
    {
        DummyClassTrackingToStringCalls o = new();
        Assert.IsInstanceOfType(5, typeof(int), $"User-provided message {o}");
        o.WasToStringCalled.Should().BeFalse();
    }

    public void InstanceNotOfTypeShouldFailWhenValueIsNull() => Assert.IsNotInstanceOfType(null, typeof(object));

    public void InstanceNotOfTypeShouldFailWhenTypeIsNull()
    {
        Action action = () => Assert.IsNotInstanceOfType(5, null);
        action.Should().Throw<AssertFailedException>();
    }

    public void InstanceNotOfTypeShouldPassOnWrongInstance() => Assert.IsNotInstanceOfType(5L, typeof(int));

    public void InstanceNotOfTypeShouldPassOnSubInstance() => Assert.IsNotInstanceOfType(new object(), typeof(int));

    public void IsInstanceOfTypeUsingGenericType_WhenValueIsNull_Fails()
    {
        Action action = () => Assert.IsInstanceOfType<AssertTests>(null);
        action.Should().Throw<AssertFailedException>()
            .WithMessage(
                """
                Assertion failed. Expected value to be of type AssertTests (or derived).

                expected type: Microsoft.VisualStudio.TestPlatform.TestFramework.UnitTests.AssertTests (or derived)
                actual:        null

                Assert.IsInstanceOfType(null)
                """);
    }

    public void IsInstanceOfTypeUsingGenericType_WhenTypeMismatch_Fails()
    {
        Action action = () => Assert.IsInstanceOfType<string>(5);
        action.Should().Throw<AssertFailedException>()
            .WithMessage(
                """
                Assertion failed. Expected value to be of type String (or derived).

                expected type: System.String (or derived)
                actual type:   System.Int32

                Assert.IsInstanceOfType(5)
                """);
    }

    public void IsInstanceOfTypeUsingGenericTypeWithOutParameter_WhenValueIsNull_Fails()
    {
        Action action = () => Assert.IsInstanceOfType<AssertTests>(null);
        action.Should().Throw<AssertFailedException>();
    }

    public void IsInstanceOfTypeUsingGenericType_OnSameInstance_DoesNotThrow() => Assert.IsInstanceOfType<int>(5);

    public void IsInstanceOfTypeUsingGenericTypeWithOutParameter_OnSameInstance_DoesNotThrow()
    {
        int instance = Assert.IsInstanceOfType<int>(5);
        instance.Should().Be(5);
    }

    public void IsInstanceOfTypeUsingGenericTypeWithOutParameter_OnSameInstanceReferenceType_DoesNotThrow()
    {
        object testInstance = new AssertTests();
        AssertTests instance = Assert.IsInstanceOfType<AssertTests>(testInstance);
        testInstance.Should().BeSameAs(instance);
    }

    public void IsInstanceOfTypeUsingGenericType_OnHigherInstance_DoesNotThrow() => Assert.IsInstanceOfType<object>(5);

    public void IsInstanceOfTypeUsingGenericTypeWithOutParameter_OnHigherInstance_DoesNotThrow()
    {
        object testInstance = new AssertTests();
        object instance = Assert.IsInstanceOfType<object>(testInstance);
        instance.Should().BeSameAs(testInstance);
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

    public void IsNotInstanceOfType_OnExactType_ShouldFailWithActualTypeEvidence()
    {
        Action action = () => Assert.IsNotInstanceOfType(5, typeof(int));
        action.Should().Throw<AssertFailedException>()
            .WithMessage(
                """
                Assertion failed. Expected value to not be of type Int32 (or derived).

                not expected type: System.Int32 (or derived)
                actual type:       System.Int32

                Assert.IsNotInstanceOfType(5)
                """);
    }

    public void IsNotInstanceOfType_OnDerivedType_ShouldFailWithActualTypeEvidence()
    {
        object x = new MemoryStream();
        Action action = () => Assert.IsNotInstanceOfType(x, typeof(Stream));
        action.Should().Throw<AssertFailedException>()
            .WithMessage(
                """
                Assertion failed. Expected value to not be of type Stream (or derived).

                not expected type: System.IO.Stream (or derived)
                actual type:       System.IO.MemoryStream

                Assert.IsNotInstanceOfType(x)
                """);
    }

    public void IsNotInstanceOfType_WhenTypeIsNull_ShouldFailWithDedicatedMessage()
    {
        Action action = () => Assert.IsNotInstanceOfType(5, null);
        action.Should().Throw<AssertFailedException>()
            .WithMessage(
                """
                Assertion failed. Cannot check type because the not-expected type argument is null.

                Assert.IsNotInstanceOfType(5)
                """);
    }

    public void IsInstanceOfType_OnFailure_ShouldPopulateExpectedAndActualPayload()
    {
        try
        {
            Assert.IsInstanceOfType(5, typeof(string));
        }
        catch (AssertFailedException ex)
        {
            ex.ExpectedText.Should().Be("System.String (or derived)");
            ex.ActualText.Should().Be("System.Int32");
            ex.Data["assert.expected"].Should().Be("System.String (or derived)");
            ex.Data["assert.actual"].Should().Be("System.Int32");
            return;
        }

        throw new AssertFailedException("Expected AssertFailedException was not thrown.");
    }

    public void IsNotInstanceOfType_OnFailure_ShouldPopulateExpectedAndActualPayload()
    {
        try
        {
            Assert.IsNotInstanceOfType(5, typeof(int));
        }
        catch (AssertFailedException ex)
        {
            ex.ExpectedText.Should().Be("System.Int32 (or derived)");
            ex.ActualText.Should().Be("System.Int32");
            ex.Data["assert.expected"].Should().Be("System.Int32 (or derived)");
            ex.Data["assert.actual"].Should().Be("System.Int32");
            return;
        }

        throw new AssertFailedException("Expected AssertFailedException was not thrown.");
    }

    private object? GetObj() => new();

    private Type? GetObjType() => typeof(object);

    private Type? GetIntType() => typeof(int);
}
