// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using AwesomeAssertions;

using TestFramework.ForTestingMSTest;

namespace Microsoft.VisualStudio.TestPlatform.TestFramework.UnitTests;

public partial class AssertTests : TestContainer
{
    public void IsNull_PassNull_ShouldPass()
        => Assert.IsNull(null);

    public void IsNull_PassNonNull_ShouldFail()
    {
        Action action = () => Assert.IsNull(new object());
        action.Should().Throw<Exception>()
            .WithMessage(
                """
                Assertion failed. Expected value to be null.

                actual: System.Object

                Assert.IsNull(new object())
                """);
    }

    public void IsNull_StringMessage_PassNull_ShouldPass()
        => Assert.IsNull(null, "User-provided message");

    public void IsNull_StringMessage_PassNonNull_ShouldFail()
    {
        Action action = () => Assert.IsNull(new object(), "User-provided message");
        action.Should().Throw<Exception>()
            .WithMessage(
                """
                Assertion failed. Expected value to be null.
                User-provided message

                actual: System.Object

                Assert.IsNull(new object())
                """);
    }

    public void IsNull_InterpolatedString_PassNull_ShouldPass()
    {
        DummyClassTrackingToStringCalls o = new();
        Assert.IsNull(null, $"User-provided message {o}");
        o.WasToStringCalled.Should().BeFalse();
    }

    public async Task IsNull_InterpolatedString_PassNonNull_ShouldFail()
    {
        DummyClassTrackingToStringCalls o = new();
        DateTime dateTime = DateTime.Now;
        Func<Task> action = async () => Assert.IsNull(new object(), $"User-provided message. {o}, {o,35}, {await GetHelloStringAsync()}, {new DummyIFormattable()}, {dateTime:tt}, {dateTime,5:tt}");
        (await action.Should().ThrowAsync<Exception>())
            .WithMessage(
                $"""
                Assertion failed. Expected value to be null.
                User-provided message. DummyClassTrackingToStringCalls,     DummyClassTrackingToStringCalls, Hello, DummyIFormattable.ToString(), {string.Format(null, "{0:tt}", dateTime)}, {string.Format(null, "{0,5:tt}", dateTime)}

                actual: System.Object

                Assert.IsNull(new object())
                """);
        o.WasToStringCalled.Should().BeTrue();
    }

    public void IsNotNull_WhenNonNullNullableValue_DoesNotThrowAndLearnNotNull()
    {
        object? obj = GetObj();
        object result = Assert.IsNotNull(obj);
        result.Should().BeSameAs(obj);
        _ = obj.ToString(); // No potential NRE warning
    }

    public void IsNotNull_WhenNonNullNullableValueAndMessage_DoesNotThrowAndLearnNotNull()
    {
        object? obj = GetObj();
        object result = Assert.IsNotNull(obj, "my message");
        result.Should().BeSameAs(obj);
        _ = obj.ToString(); // No potential NRE warning
    }

    public void IsNotNull_WhenNonNullNullableValueAndInterpolatedStringMessage_DoesNotThrowAndLearnNotNull()
    {
        object? obj = GetObj();
        DummyClassTrackingToStringCalls o = new();
        object result = Assert.IsNotNull(obj, $"my message {o}");
        result.Should().BeSameAs(obj);
        o.WasToStringCalled.Should().BeFalse();
        _ = obj.ToString(); // No potential NRE warning
    }

    public void IsNotNull_WithReferenceType_ReturnsSameInstance()
    {
        StringBuilder? value = new("checked value");

        StringBuilder result = Assert.IsNotNull(value);
        result.Should().BeSameAs(value);
        result = Assert.IsNotNull(value, "my message");
        result.Should().BeSameAs(value);
        result = Assert.IsNotNull(value, $"my message {value}");
        result.Should().BeSameAs(value);
    }

    public void IsNotNull_WithNullableTypeArgument_ReturnsNonNullableValue()
    {
        string? value = "checked value";

        string result = Assert.IsNotNull<string?>(value);
        result.Should().Be("checked value");
        result = Assert.IsNotNull<string?>(value, "my message");
        result.Should().Be("checked value");
        result = Assert.IsNotNull<string?>(value, $"my message {value}");
        result.Should().Be("checked value");
    }

    public void IsNotNull_WithNonNullableValueType_ReturnsValue()
    {
        int result = Assert.IsNotNull(42);
        result.Should().Be(42);
        result = Assert.IsNotNull(42, "my message");
        result.Should().Be(42);
        result = Assert.IsNotNull(42, $"my message {42}");
        result.Should().Be(42);
    }

    public void IsNotNull_WithNullableValueType_ReturnsValue()
    {
        int? value = 42;

        int? result = Assert.IsNotNull(value);
        result.Should().Be(42);
        result = Assert.IsNotNull(value, "my message");
        result.Should().Be(42);
        result = Assert.IsNotNull(value, $"my message {value}");
        result.Should().Be(42);
    }

    public void IsNotNull_WithNullNullableValueType_ShouldFail()
    {
        int? value = null;
        Action action = () => Assert.IsNotNull(value);

        action.Should().Throw<AssertFailedException>()
            .WithMessage(
                """
                Assertion failed. Expected value to not be null.

                actual: null

                Assert.IsNotNull(value)
                """);
    }

    public void IsNotNull_EvaluatesArgumentOnce()
    {
        object expected = new();
        int evaluations = 0;

        object result = Assert.IsNotNull(GetValue());
        result.Should().BeSameAs(expected);
        evaluations.Should().Be(1);
        result = Assert.IsNotNull(GetValue(), "my message");
        result.Should().BeSameAs(expected);
        evaluations.Should().Be(2);
        result = Assert.IsNotNull(GetValue(), $"my message {evaluations}");
        result.Should().BeSameAs(expected);
        evaluations.Should().Be(3);

        object? GetValue()
        {
            evaluations++;
            return expected;
        }
    }

    public void IsNotNull_PassNull_ShouldFail()
    {
        Action action = () => Assert.IsNotNull<object>(null);
        action.Should().Throw<Exception>()
            .WithMessage(
                """
                Assertion failed. Expected value to not be null.

                actual: null

                Assert.IsNotNull(null)
                """);
    }

    public void IsNotNull_StringMessage_PassNonNull_ShouldFail()
    {
        Action action = () => Assert.IsNotNull<object>(null, "User-provided message");
        action.Should().Throw<Exception>()
            .WithMessage(
                """
                Assertion failed. Expected value to not be null.
                User-provided message

                actual: null

                Assert.IsNotNull(null)
                """);
    }

    public async Task IsNotNull_InterpolatedString_PassNonNull_ShouldFail()
    {
        DummyClassTrackingToStringCalls o = new();
        DateTime dateTime = DateTime.Now;
        Func<Task> action = async () => Assert.IsNotNull<object>(null, $"User-provided message. {o}, {o,35}, {await GetHelloStringAsync()}, {new DummyIFormattable()}, {dateTime:tt}, {dateTime,5:tt}");
        (await action.Should().ThrowAsync<Exception>())
            .WithMessage(
                $"""
                Assertion failed. Expected value to not be null.
                User-provided message. DummyClassTrackingToStringCalls,     DummyClassTrackingToStringCalls, Hello, DummyIFormattable.ToString(), {string.Format(null, "{0:tt}", dateTime)}, {string.Format(null, "{0,5:tt}", dateTime)}

                actual: null

                Assert.IsNotNull(null)
                """);
        o.WasToStringCalled.Should().BeTrue();
    }
}
