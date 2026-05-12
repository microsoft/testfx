// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using AwesomeAssertions;

namespace Microsoft.VisualStudio.TestPlatform.TestFramework.UnitTests;

public partial class AssertTests
{
    public void AreSame_PassSameObject_ShouldPass()
    {
        object o = new();
        Assert.AreSame(o, o);
    }

    public void AreSame_PassDifferentObject_ShouldFail()
    {
        Action action = () => Assert.AreSame(new object(), new object());
        action.Should().Throw<Exception>()
            .WithMessage($"Assertion failed. Expected both values to refer to the same object.{Environment.NewLine}{Environment.NewLine}expected: System.Object (hash: 0x*){Environment.NewLine}actual:   System.Object (hash: 0x*){Environment.NewLine}{Environment.NewLine}Assert.AreSame(new object(), new object())");
    }

    public void AreSame_StringMessage_PassSameObject_ShouldPass()
    {
        object o = new();
        Assert.AreSame(o, o, "User-provided message");
    }

    public void AreSame_StringMessage_PassDifferentObject_ShouldFail()
    {
        Action action = () => Assert.AreSame(new object(), new object(), "User-provided message");
        action.Should().Throw<Exception>()
            .WithMessage($"Assertion failed. Expected both values to refer to the same object.{Environment.NewLine}User-provided message{Environment.NewLine}{Environment.NewLine}expected: System.Object (hash: 0x*){Environment.NewLine}actual:   System.Object (hash: 0x*){Environment.NewLine}{Environment.NewLine}Assert.AreSame(new object(), new object())");
    }

    public void AreSame_InterpolatedString_PassSameObject_ShouldPass()
    {
        DummyClassTrackingToStringCalls o = new();
        Assert.AreSame(o, o, $"User-provided message: {o}");
        o.WasToStringCalled.Should().BeFalse();
    }

    public async Task AreSame_InterpolatedString_PassDifferentObject_ShouldFail()
    {
        DummyClassTrackingToStringCalls o = new();
        DateTime dateTime = DateTime.Now;
        Func<Task> action = async () => Assert.AreSame(new object(), new object(), $"User-provided message. {o}, {o,35}, {await GetHelloStringAsync()}, {new DummyIFormattable()}, {dateTime:tt}, {dateTime,5:tt}");
        (await action.Should().ThrowAsync<Exception>()).WithMessage($"Assertion failed. Expected both values to refer to the same object.{Environment.NewLine}User-provided message. DummyClassTrackingToStringCalls,     DummyClassTrackingToStringCalls, Hello, DummyIFormattable.ToString(), {string.Format(null, "{0:tt}", dateTime)}, {string.Format(null, "{0,5:tt}", dateTime)}{Environment.NewLine}{Environment.NewLine}expected: System.Object (hash: 0x*){Environment.NewLine}actual:   System.Object (hash: 0x*){Environment.NewLine}{Environment.NewLine}Assert.AreSame(new object(), new object())");
        o.WasToStringCalled.Should().BeTrue();
    }

    public void AreNotSame_PassDifferentObject_ShouldPass()
        => Assert.AreNotSame(new object(), new object());

    public void AreSame_BothAreValueTypes_ShouldFailWithSpecializedMessage()
    {
        Action action = () => Assert.AreSame(1, 1);
        action.Should().Throw<Exception>()
            .WithMessage($"Assertion failed. Expected both values to refer to the same object.{Environment.NewLine}Do not pass value types to AreSame \u2014 value types are boxed on each call, so references will never be the same.{Environment.NewLine}{Environment.NewLine}Assert.AreSame(1, 1)");
    }

    public void AreSame_StringMessage_BothAreValueTypes_ShouldFailWithSpecializedMessage()
    {
        Action action = () => Assert.AreSame(1, 1, "User-provided message");
        action.Should().Throw<Exception>()
            .WithMessage($"Assertion failed. Expected both values to refer to the same object.{Environment.NewLine}Do not pass value types to AreSame \u2014 value types are boxed on each call, so references will never be the same.{Environment.NewLine}User-provided message{Environment.NewLine}{Environment.NewLine}Assert.AreSame(1, 1)");
    }

    public void AreSame_InterpolatedString_BothAreValueTypes_ShouldFailWithSpecializedMessage()
    {
        Action action = () => Assert.AreSame(1, 1, $"User-provided message {new object().GetType()}");
        action.Should().Throw<Exception>()
            .WithMessage($"Assertion failed. Expected both values to refer to the same object.{Environment.NewLine}Do not pass value types to AreSame \u2014 value types are boxed on each call, so references will never be the same.{Environment.NewLine}User-provided message System.Object{Environment.NewLine}{Environment.NewLine}Assert.AreSame(1, 1)");
    }

    public void AreSame_ExpectedNull_ShouldFailWithNullMessage()
    {
        object? expected = null;
        Action action = () => Assert.AreSame(expected, new object());
        action.Should().Throw<Exception>()
            .WithMessage($"Assertion failed. Expected both values to refer to the same object.{Environment.NewLine}{Environment.NewLine}expected: null{Environment.NewLine}actual:   System.Object (hash: 0x*){Environment.NewLine}{Environment.NewLine}Assert.AreSame(expected, new object())");
    }

    public void AreSame_ActualNull_ShouldFailWithNullMessage()
    {
        object? actual = null;
        Action action = () => Assert.AreSame(new object(), actual);
        action.Should().Throw<Exception>()
            .WithMessage($"Assertion failed. Expected both values to refer to the same object.{Environment.NewLine}{Environment.NewLine}expected: System.Object (hash: 0x*){Environment.NewLine}actual:   null{Environment.NewLine}{Environment.NewLine}Assert.AreSame(new object(), actual)");
    }

    public void AreSame_StringMessage_ExpectedNull_ShouldFailWithNullMessage()
    {
        object? expected = null;
        Action action = () => Assert.AreSame(expected, new object(), "User-provided message");
        action.Should().Throw<Exception>()
            .WithMessage($"Assertion failed. Expected both values to refer to the same object.{Environment.NewLine}User-provided message{Environment.NewLine}{Environment.NewLine}expected: null{Environment.NewLine}actual:   System.Object (hash: 0x*){Environment.NewLine}{Environment.NewLine}Assert.AreSame(expected, new object())");
    }

    public void AreSame_StringMessage_ActualNull_ShouldFailWithNullMessage()
    {
        object? actual = null;
        Action action = () => Assert.AreSame(new object(), actual, "User-provided message");
        action.Should().Throw<Exception>()
            .WithMessage($"Assertion failed. Expected both values to refer to the same object.{Environment.NewLine}User-provided message{Environment.NewLine}{Environment.NewLine}expected: System.Object (hash: 0x*){Environment.NewLine}actual:   null{Environment.NewLine}{Environment.NewLine}Assert.AreSame(new object(), actual)");
    }

    public void AreSame_InterpolatedString_ExpectedNull_ShouldFailWithNullMessage()
    {
        object? expected = null;
        Action action = () => Assert.AreSame(expected, new object(), $"User-provided message {new object().GetType()}");
        action.Should().Throw<Exception>()
            .WithMessage($"Assertion failed. Expected both values to refer to the same object.{Environment.NewLine}User-provided message System.Object{Environment.NewLine}{Environment.NewLine}expected: null{Environment.NewLine}actual:   System.Object (hash: 0x*){Environment.NewLine}{Environment.NewLine}Assert.AreSame(expected, new object())");
    }

    public void AreSame_InterpolatedString_ActualNull_ShouldFailWithNullMessage()
    {
        object? actual = null;
        Action action = () => Assert.AreSame(new object(), actual, $"User-provided message {new object().GetType()}");
        action.Should().Throw<Exception>()
            .WithMessage($"Assertion failed. Expected both values to refer to the same object.{Environment.NewLine}User-provided message System.Object{Environment.NewLine}{Environment.NewLine}expected: System.Object (hash: 0x*){Environment.NewLine}actual:   null{Environment.NewLine}{Environment.NewLine}Assert.AreSame(new object(), actual)");
    }

    public void AreNotSame_PassSameObject_ShouldFail()
    {
        object o = new();
        Action action = () => Assert.AreNotSame(o, o);
        action.Should().Throw<Exception>()
            .WithMessage($"Assertion failed. Expected values to refer to different objects.{Environment.NewLine}Both variables refer to the same object.{Environment.NewLine}{Environment.NewLine}Assert.AreNotSame(o, o)");
    }

    public void AreNotSame_StringMessage_PassDifferentObject_ShouldPass()
        => Assert.AreNotSame(new object(), new object(), "User-provided message");

    public void AreNotSame_StringMessage_PassSameObject_ShouldFail()
    {
        object o = new();
        Action action = () => Assert.AreNotSame(o, o, "User-provided message");
        action.Should().Throw<Exception>()
            .WithMessage($"Assertion failed. Expected values to refer to different objects.{Environment.NewLine}Both variables refer to the same object.{Environment.NewLine}User-provided message{Environment.NewLine}{Environment.NewLine}Assert.AreNotSame(o, o)");
    }

    public void AreNotSame_InterpolatedString_PassDifferentObject_ShouldPass()
    {
        DummyClassTrackingToStringCalls o = new();
        Assert.AreNotSame(new object(), new object(), $"User-provided message: {o}");
        o.WasToStringCalled.Should().BeFalse();
    }

    public async Task AreNotSame_InterpolatedString_PassSameObject_ShouldFail()
    {
        DummyClassTrackingToStringCalls o = new();
        DateTime dateTime = DateTime.Now;
        Func<Task> action = async () => Assert.AreNotSame(o, o, $"User-provided message. {o}, {o,35}, {await GetHelloStringAsync()}, {new DummyIFormattable()}, {dateTime:tt}, {dateTime,5:tt}");
        (await action.Should().ThrowAsync<Exception>()).WithMessage($"Assertion failed. Expected values to refer to different objects.{Environment.NewLine}Both variables refer to the same object.{Environment.NewLine}User-provided message. DummyClassTrackingToStringCalls,     DummyClassTrackingToStringCalls, Hello, DummyIFormattable.ToString(), {string.Format(null, "{0:tt}", dateTime)}, {string.Format(null, "{0,5:tt}", dateTime)}{Environment.NewLine}{Environment.NewLine}Assert.AreNotSame(o, o)");
        o.WasToStringCalled.Should().BeTrue();
    }

    public void AreNotSame_BothNull_ShouldFailWithNullMessage()
    {
        object? notExpected = null;
        object? actual = null;
        Action action = () => Assert.AreNotSame(notExpected, actual);
        action.Should().Throw<Exception>()
            .WithMessage($"Assertion failed. Expected values to refer to different objects.{Environment.NewLine}Both values are null.{Environment.NewLine}{Environment.NewLine}Assert.AreNotSame(notExpected, actual)");
    }

    public void AreNotSame_StringMessage_BothNull_ShouldFailWithNullMessage()
    {
        object? notExpected = null;
        object? actual = null;
        Action action = () => Assert.AreNotSame(notExpected, actual, "User-provided message");
        action.Should().Throw<Exception>()
            .WithMessage($"Assertion failed. Expected values to refer to different objects.{Environment.NewLine}Both values are null.{Environment.NewLine}User-provided message{Environment.NewLine}{Environment.NewLine}Assert.AreNotSame(notExpected, actual)");
    }

    public void AreNotSame_InterpolatedString_BothNull_ShouldFailWithNullMessage()
    {
        object? notExpected = null;
        object? actual = null;
        Action action = () => Assert.AreNotSame(notExpected, actual, $"User-provided message {new object().GetType()}");
        action.Should().Throw<Exception>()
            .WithMessage($"Assertion failed. Expected values to refer to different objects.{Environment.NewLine}Both values are null.{Environment.NewLine}User-provided message System.Object{Environment.NewLine}{Environment.NewLine}Assert.AreNotSame(notExpected, actual)");
    }
}
