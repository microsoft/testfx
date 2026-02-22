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
            .WithMessage("""
Assert.IsExactInstanceOfType failed.
  value: (null)
""");
    }

    public void ExactInstanceOfTypeShouldFailWhenTypeIsNull()
    {
        Action action = () => Assert.IsExactInstanceOfType(5, null);
        action.Should().Throw<AssertFailedException>()
            .WithMessage("""
Assert.IsExactInstanceOfType failed.
  value: 5
""");
    }

    public void ExactInstanceOfTypeShouldFailWhenTypeIsMismatched()
    {
        Action action = () => Assert.IsExactInstanceOfType(5, typeof(string));
        action.Should().Throw<AssertFailedException>()
            .WithMessage("""
Assert.IsExactInstanceOfType failed.
Expected value to be exactly of the specified type.
  value: 5
  expectedType: System.String
  actualType: System.Int32
""");
    }

    public void ExactInstanceOfTypeShouldPassOnSameInstance() => Assert.IsExactInstanceOfType(5, typeof(int));

    public void ExactInstanceOfTypeShouldFailOnHigherInstance()
    {
        Action action = () => Assert.IsExactInstanceOfType(5, typeof(object));
        action.Should().Throw<AssertFailedException>()
            .WithMessage("""
Assert.IsExactInstanceOfType failed.
Expected value to be exactly of the specified type.
  value: 5
  expectedType: System.Object
  actualType: System.Int32
""");
    }

    public void ExactInstanceOfTypeShouldFailOnDerivedType()
    {
        object x = new MemoryStream();
        Action action = () => Assert.IsExactInstanceOfType(x, typeof(Stream));
        action.Should().Throw<AssertFailedException>()
            .WithMessage("""
Assert.IsExactInstanceOfType failed.
Expected value to be exactly of the specified type.
  value (x): <System.IO.MemoryStream>
  expectedType: System.IO.Stream
  actualType: System.IO.MemoryStream
""");
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
            .WithMessage("""
Assert.IsExactInstanceOfType failed. User-provided message
  value: (null)
""");
    }

    public void ExactInstanceOfType_WithStringMessage_ShouldFailWhenTypeIsNull()
    {
        Action action = () => Assert.IsExactInstanceOfType(5, null, "User-provided message");
        action.Should().Throw<AssertFailedException>()
            .WithMessage("""
Assert.IsExactInstanceOfType failed. User-provided message
  value: 5
""");
    }

    public void ExactInstanceOfType_WithStringMessage_ShouldFailWhenTypeIsMismatched()
    {
        Action action = () => Assert.IsExactInstanceOfType(5, typeof(string), "User-provided message");
        action.Should().Throw<AssertFailedException>()
            .WithMessage("""
Assert.IsExactInstanceOfType failed. User-provided message
Expected value to be exactly of the specified type.
  value: 5
  expectedType: System.String
  actualType: System.Int32
""");
    }

    public void ExactInstanceOfType_WithStringMessage_ShouldPassWhenTypeIsCorrect()
        => Assert.IsExactInstanceOfType(5, typeof(int), "User-provided message");

    public async Task ExactInstanceOfType_WithInterpolatedString_ShouldFailWhenValueIsNull()
    {
        DummyClassTrackingToStringCalls o = new();
        DateTime dateTime = DateTime.Now;
        Func<Task> action = async () => Assert.IsExactInstanceOfType(null, typeof(AssertTests), $"User-provided message. {o}, {o,35}, {await GetHelloStringAsync()}, {new DummyIFormattable()}, {dateTime:tt}, {dateTime,5:tt}");
        (await action.Should().ThrowAsync<AssertFailedException>())
            .WithMessage("""
                Assert.IsExactInstanceOfType failed. User-provided message. DummyClassTrackingToStringCalls,     DummyClassTrackingToStringCalls, Hello, DummyIFormattable.ToString()*
                  value: (null)
                """);
        o.WasToStringCalled.Should().BeTrue();
    }

    public void ExactInstanceOfType_WithInterpolatedString_ShouldFailWhenTypeIsNull()
    {
        DummyClassTrackingToStringCalls o = new();
        Action action = () => Assert.IsExactInstanceOfType(5, null, $"User-provided message {o}");
        action.Should().Throw<AssertFailedException>()
            .WithMessage("""
Assert.IsExactInstanceOfType failed. User-provided message DummyClassTrackingToStringCalls
  value: 5
""");
        o.WasToStringCalled.Should().BeTrue();
    }

    public void ExactInstanceOfType_WithInterpolatedString_ShouldFailWhenTypeIsMismatched()
    {
        DummyClassTrackingToStringCalls o = new();
        Action action = () => Assert.IsExactInstanceOfType(5, typeof(string), $"User-provided message {o}");
        action.Should().Throw<AssertFailedException>()
            .WithMessage("""
Assert.IsExactInstanceOfType failed. User-provided message DummyClassTrackingToStringCalls
Expected value to be exactly of the specified type.
  value: 5
  expectedType: System.String
  actualType: System.Int32
""");
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
            .WithMessage("""
Assert.IsNotExactInstanceOfType failed.
Value should not be exactly of the specified type.
  value (x): <System.IO.MemoryStream>
  wrongType: System.IO.MemoryStream
  actualType: System.IO.MemoryStream
""");
    }

    public void IsExactInstanceOfTypeUsingGenericType_WhenValueIsNull_Fails()
    {
        Action action = () => Assert.IsExactInstanceOfType<AssertTests>(null);
        action.Should().Throw<AssertFailedException>()
            .WithMessage("""
Assert.IsExactInstanceOfType failed.
  value: (null)
""");
    }

    public void IsExactInstanceOfTypeUsingGenericType_WhenTypeMismatch_Fails()
    {
        Action action = () => Assert.IsExactInstanceOfType<string>(5);
        action.Should().Throw<AssertFailedException>()
            .WithMessage("""
Assert.IsExactInstanceOfType failed.
Expected value to be exactly of the specified type.
  value: 5
  expectedType: System.String
  actualType: System.Int32
""");
    }

    public void IsExactInstanceOfTypeUsingGenericType_WhenDerivedType_Fails()
    {
        object x = new MemoryStream();
        Action action = () => Assert.IsExactInstanceOfType<Stream>(x);
        action.Should().Throw<AssertFailedException>()
            .WithMessage("""
Assert.IsExactInstanceOfType failed.
Expected value to be exactly of the specified type.
  value (x): <System.IO.MemoryStream>
  expectedType: System.IO.Stream
  actualType: System.IO.MemoryStream
""");
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
            .WithMessage("""
Assert.IsExactInstanceOfType failed.
Expected value to be exactly of the specified type.
  value: 5
  expectedType: System.Object
  actualType: System.Int32
""");
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
            .WithMessage("""
Assert.IsNotExactInstanceOfType failed.
Value should not be exactly of the specified type.
  value (x): <System.IO.MemoryStream>
  wrongType: System.IO.MemoryStream
  actualType: System.IO.MemoryStream
""");
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

    #region IsExactInstanceOfType/IsNotExactInstanceOfType truncation and newline escaping

    public void IsExactInstanceOfType_WithLongExpression_ShouldTruncateExpression()
    {
        object aVeryLongVariableNameThatExceedsOneHundredCharactersInLengthToTestTruncationBehaviorOfExpressionDisplayXYZ = "hello";

        Action action = () => Assert.IsExactInstanceOfType(aVeryLongVariableNameThatExceedsOneHundredCharactersInLengthToTestTruncationBehaviorOfExpressionDisplayXYZ, typeof(int));
        action.Should().Throw<AssertFailedException>()
            .WithMessage("""
                Assert.IsExactInstanceOfType failed.
                Expected value to be exactly of the specified type.
                  value (aVeryLongVariableNameThatExceedsOneHundredCharactersInLengthToTestTruncationBehaviorOfExpressionDisp...): "hello"
                  expectedType: System.Int32
                  actualType: System.String
                """);
    }

    public void IsExactInstanceOfType_WithLongToStringValue_ShouldTruncateValue()
    {
        var obj = new ObjectWithLongToString();

        Action action = () => Assert.IsExactInstanceOfType(obj, typeof(int));
        action.Should().Throw<AssertFailedException>()
            .WithMessage($"""
                Assert.IsExactInstanceOfType failed.
                Expected value to be exactly of the specified type.
                  value (obj): {new string('L', 256)}... (300 chars)
                  expectedType: System.Int32
                  actualType: *ObjectWithLongToString
                """);
    }

    public void IsExactInstanceOfType_WithNewlineInToString_ShouldEscapeNewlines()
    {
        var obj = new ObjectWithNewlineToString();

        Action action = () => Assert.IsExactInstanceOfType(obj, typeof(int));
        action.Should().Throw<AssertFailedException>()
            .WithMessage("""
                Assert.IsExactInstanceOfType failed.
                Expected value to be exactly of the specified type.
                  value (obj): line1\r\nline2\nline3
                  expectedType: System.Int32
                  actualType: *ObjectWithNewlineToString
                """);
    }

    public void IsNotExactInstanceOfType_WithLongExpression_ShouldTruncateExpression()
    {
        string aVeryLongVariableNameThatExceedsOneHundredCharactersInLengthToTestTruncationBehaviorOfExpressionDisplayXYZ = "hello";

        Action action = () => Assert.IsNotExactInstanceOfType(aVeryLongVariableNameThatExceedsOneHundredCharactersInLengthToTestTruncationBehaviorOfExpressionDisplayXYZ, typeof(string));
        action.Should().Throw<AssertFailedException>()
            .WithMessage("""
                Assert.IsNotExactInstanceOfType failed.
                Value should not be exactly of the specified type.
                  value (aVeryLongVariableNameThatExceedsOneHundredCharactersInLengthToTestTruncationBehaviorOfExpressionDisp...): "hello"
                  wrongType: System.String
                  actualType: System.String
                """);
    }

    public void IsNotExactInstanceOfType_WithLongToStringValue_ShouldTruncateValue()
    {
        var obj = new ObjectWithLongToString();

        Action action = () => Assert.IsNotExactInstanceOfType(obj, typeof(ObjectWithLongToString));
        action.Should().Throw<AssertFailedException>()
            .WithMessage($"""
                Assert.IsNotExactInstanceOfType failed.
                Value should not be exactly of the specified type.
                  value (obj): {new string('L', 256)}... (300 chars)
                  wrongType: *ObjectWithLongToString
                  actualType: *ObjectWithLongToString
                """);
    }

    public void IsNotExactInstanceOfType_WithNewlineInToString_ShouldEscapeNewlines()
    {
        var obj = new ObjectWithNewlineToString();

        Action action = () => Assert.IsNotExactInstanceOfType(obj, typeof(ObjectWithNewlineToString));
        action.Should().Throw<AssertFailedException>()
            .WithMessage("""
                Assert.IsNotExactInstanceOfType failed.
                Value should not be exactly of the specified type.
                  value (obj): line1\r\nline2\nline3
                  wrongType: *ObjectWithNewlineToString
                  actualType: *ObjectWithNewlineToString
                """);
    }

    #endregion
}
