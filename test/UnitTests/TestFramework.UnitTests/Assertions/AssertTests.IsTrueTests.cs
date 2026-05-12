// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using AwesomeAssertions;

namespace Microsoft.VisualStudio.TestPlatform.TestFramework.UnitTests;

public partial class AssertTests
{
    public void IsFalseNullableBooleanShouldFailWithNull()
    {
        bool? nullBool = null;
        Action action = () => Assert.IsFalse(nullBool);
        action.Should().Throw<Exception>()
            .WithMessage($"Assertion failed. Expected condition to be false.{Environment.NewLine}{Environment.NewLine}actual: null{Environment.NewLine}{Environment.NewLine}Assert.IsFalse(nullBool)");
    }

    public void IsFalseNullableBooleanShouldFailWithTrue()
    {
        bool? nullBool = true;
        Action action = () => Assert.IsFalse(nullBool);
        action.Should().Throw<Exception>()
            .WithMessage($"Assertion failed. Expected condition to be false.{Environment.NewLine}{Environment.NewLine}actual: true{Environment.NewLine}{Environment.NewLine}Assert.IsFalse(nullBool)");
    }

    public void IsFalseNullableBooleanShouldNotFailWithFalse()
    {
        bool? nullBool = false;
        Assert.IsFalse(nullBool);
    }

    public void IsFalseBooleanShouldFailWithTrue()
    {
        Action action = () => Assert.IsFalse(true);
        action.Should().Throw<Exception>()
            .WithMessage($"Assertion failed. Expected condition to be false.{Environment.NewLine}{Environment.NewLine}actual: true{Environment.NewLine}{Environment.NewLine}Assert.IsFalse(true)");
    }

    public void IsFalseBooleanShouldNotFailWithFalse()
        => Assert.IsFalse(false);

    public void IsFalseNullableBooleanStringMessageShouldFailWithNull()
    {
        bool? nullBool = null;
        Action action = () => Assert.IsFalse(nullBool, "User-provided message");
        action.Should().Throw<Exception>()
            .WithMessage($"Assertion failed. Expected condition to be false.{Environment.NewLine}User-provided message{Environment.NewLine}{Environment.NewLine}actual: null{Environment.NewLine}{Environment.NewLine}Assert.IsFalse(nullBool)");
    }

    public void IsFalseNullableBooleanStringMessageShouldFailWithTrue()
    {
        bool? nullBool = true;
        Action action = () => Assert.IsFalse(nullBool, "User-provided message");
        action.Should().Throw<Exception>()
            .WithMessage($"Assertion failed. Expected condition to be false.{Environment.NewLine}User-provided message{Environment.NewLine}{Environment.NewLine}actual: true{Environment.NewLine}{Environment.NewLine}Assert.IsFalse(nullBool)");
    }

    public void IsFalseNullableBooleanStringMessageShouldNotFailWithFalse()
    {
        bool? nullBool = false;
        Assert.IsFalse(nullBool, "User-provided message");
    }

    public void IsFalseBooleanStringMessageShouldFailWithTrue()
    {
        Action action = () => Assert.IsFalse(true, "User-provided message");
        action.Should().Throw<Exception>()
            .WithMessage($"Assertion failed. Expected condition to be false.{Environment.NewLine}User-provided message{Environment.NewLine}{Environment.NewLine}actual: true{Environment.NewLine}{Environment.NewLine}Assert.IsFalse(true)");
    }

    public void IsFalseBooleanStringMessageShouldNotFailWithFalse()
        => Assert.IsFalse(false, "User-provided message");

    public async Task IsFalseNullableBooleanInterpolatedStringMessageShouldFailWithNull()
    {
        bool? nullBool = null;
        DummyClassTrackingToStringCalls o = new();
        DateTime dateTime = DateTime.Now;
        Func<Task> action = async () => Assert.IsFalse(nullBool, $"User-provided message. {o}, {o,35}, {await GetHelloStringAsync()}, {new DummyIFormattable()}, {dateTime:tt}, {dateTime,5:tt}");
        (await action.Should().ThrowAsync<Exception>())
            .WithMessage($"Assertion failed. Expected condition to be false.{Environment.NewLine}User-provided message. DummyClassTrackingToStringCalls,     DummyClassTrackingToStringCalls, Hello, DummyIFormattable.ToString(), {string.Format(null, "{0:tt}", dateTime)}, {string.Format(null, "{0,5:tt}", dateTime)}{Environment.NewLine}{Environment.NewLine}actual: null{Environment.NewLine}{Environment.NewLine}Assert.IsFalse(nullBool)");
    }

    public async Task IsFalseNullableBooleanInterpolatedStringMessageShouldFailWithTrue()
    {
        bool? nullBool = true;
        DummyClassTrackingToStringCalls o = new();
        DateTime dateTime = DateTime.Now;
        Func<Task> action = async () => Assert.IsFalse(nullBool, $"User-provided message. {o}, {o,35}, {await GetHelloStringAsync()}, {new DummyIFormattable()}, {dateTime:tt}, {dateTime,5:tt}");
        (await action.Should().ThrowAsync<Exception>())
            .WithMessage($"Assertion failed. Expected condition to be false.{Environment.NewLine}User-provided message. DummyClassTrackingToStringCalls,     DummyClassTrackingToStringCalls, Hello, DummyIFormattable.ToString(), {string.Format(null, "{0:tt}", dateTime)}, {string.Format(null, "{0,5:tt}", dateTime)}{Environment.NewLine}{Environment.NewLine}actual: true{Environment.NewLine}{Environment.NewLine}Assert.IsFalse(nullBool)");
    }

    public void IsFalseNullableBooleanInterpolatedStringMessageShouldNotFailWithFalse()
    {
        bool? nullBool = false;
        Assert.IsFalse(nullBool, $"User-provided message. Input: {nullBool}");
    }

    public async Task IsFalseBooleanInterpolatedStringMessageShouldFailWithTrue()
    {
        DummyClassTrackingToStringCalls o = new();
        DateTime dateTime = DateTime.Now;
        Func<Task> action = async () => Assert.IsFalse(true, $"User-provided message. {o}, {o,35}, {await GetHelloStringAsync()}, {new DummyIFormattable()}, {dateTime:tt}, {dateTime,5:tt}");
        (await action.Should().ThrowAsync<Exception>())
            .WithMessage($"Assertion failed. Expected condition to be false.{Environment.NewLine}User-provided message. DummyClassTrackingToStringCalls,     DummyClassTrackingToStringCalls, Hello, DummyIFormattable.ToString(), {string.Format(null, "{0:tt}", dateTime)}, {string.Format(null, "{0,5:tt}", dateTime)}{Environment.NewLine}{Environment.NewLine}actual: true{Environment.NewLine}{Environment.NewLine}Assert.IsFalse(true)");
    }

    public void IsFalseBooleanInterpolatedStringMessageShouldNotFailWithFalse()
        => Assert.IsFalse(false, $"User-provided message. Input: {false}");

    public void IsTrueNullableBooleanShouldFailWithNull()
    {
        bool? nullBool = null;
        Action action = () => Assert.IsTrue(nullBool);
        action.Should().Throw<Exception>()
            .WithMessage($"Assertion failed. Expected condition to be true.{Environment.NewLine}{Environment.NewLine}actual: null{Environment.NewLine}{Environment.NewLine}Assert.IsTrue(nullBool)");
    }

    public void IsTrueNullableBooleanShouldFailWithFalse()
    {
        bool? nullBool = false;
        Action action = () => Assert.IsTrue(nullBool);
        action.Should().Throw<Exception>()
            .WithMessage($"Assertion failed. Expected condition to be true.{Environment.NewLine}{Environment.NewLine}actual: false{Environment.NewLine}{Environment.NewLine}Assert.IsTrue(nullBool)");
    }

    public void IsTrueNullableBooleanShouldNotFailWithTrue()
    {
        bool? nullBool = true;
        Assert.IsTrue(nullBool);
    }

    public void IsTrueBooleanShouldFailWithFalse()
    {
        Action action = () => Assert.IsTrue(false);
        action.Should().Throw<Exception>()
            .WithMessage($"Assertion failed. Expected condition to be true.{Environment.NewLine}{Environment.NewLine}actual: false{Environment.NewLine}{Environment.NewLine}Assert.IsTrue(false)");
    }

    public void IsTrueBooleanShouldNotFailWithTrue()
        => Assert.IsTrue(true);

    public void IsTrueNullableBooleanStringMessageShouldFailWithNull()
    {
        bool? nullBool = null;
        Action action = () => Assert.IsTrue(nullBool, "User-provided message");
        action.Should().Throw<Exception>()
            .WithMessage($"Assertion failed. Expected condition to be true.{Environment.NewLine}User-provided message{Environment.NewLine}{Environment.NewLine}actual: null{Environment.NewLine}{Environment.NewLine}Assert.IsTrue(nullBool)");
    }

    public void IsTrueNullableBooleanStringMessageShouldFailWithFalse()
    {
        bool? nullBool = false;
        Action action = () => Assert.IsTrue(nullBool, "User-provided message");
        action.Should().Throw<Exception>()
            .WithMessage($"Assertion failed. Expected condition to be true.{Environment.NewLine}User-provided message{Environment.NewLine}{Environment.NewLine}actual: false{Environment.NewLine}{Environment.NewLine}Assert.IsTrue(nullBool)");
    }

    public void IsTrueNullableBooleanStringMessageShouldNotFailWithTrue()
    {
        bool? nullBool = true;
        Assert.IsTrue(nullBool, "User-provided message");
    }

    public void IsTrueBooleanStringMessageShouldFailWithFalse()
    {
        Action action = () => Assert.IsTrue(false, "User-provided message");
        action.Should().Throw<Exception>()
            .WithMessage($"Assertion failed. Expected condition to be true.{Environment.NewLine}User-provided message{Environment.NewLine}{Environment.NewLine}actual: false{Environment.NewLine}{Environment.NewLine}Assert.IsTrue(false)");
    }

    public void IsTrueBooleanStringMessageShouldNotFailWithTrue()
        => Assert.IsTrue(true, "User-provided message");

    public async Task IsTrueNullableBooleanInterpolatedStringMessageShouldFailWithNull()
    {
        bool? nullBool = null;
        DummyClassTrackingToStringCalls o = new();
        DateTime dateTime = DateTime.Now;
        Func<Task> action = async () => Assert.IsTrue(nullBool, $"User-provided message. {o}, {o,35}, {await GetHelloStringAsync()}, {new DummyIFormattable()}, {dateTime:tt}, {dateTime,5:tt}");
        (await action.Should().ThrowAsync<Exception>())
            .WithMessage($"Assertion failed. Expected condition to be true.{Environment.NewLine}User-provided message. DummyClassTrackingToStringCalls,     DummyClassTrackingToStringCalls, Hello, DummyIFormattable.ToString(), {string.Format(null, "{0:tt}", dateTime)}, {string.Format(null, "{0,5:tt}", dateTime)}{Environment.NewLine}{Environment.NewLine}actual: null{Environment.NewLine}{Environment.NewLine}Assert.IsTrue(nullBool)");
    }

    public async Task IsTrueNullableBooleanInterpolatedStringMessageShouldFailWithFalse()
    {
        bool? nullBool = false;
        DummyClassTrackingToStringCalls o = new();
        DateTime dateTime = DateTime.Now;
        Func<Task> action = async () => Assert.IsTrue(nullBool, $"User-provided message. {o}, {o,35}, {await GetHelloStringAsync()}, {new DummyIFormattable()}, {dateTime:tt}, {dateTime,5:tt}");
        (await action.Should().ThrowAsync<Exception>())
            .WithMessage($"Assertion failed. Expected condition to be true.{Environment.NewLine}User-provided message. DummyClassTrackingToStringCalls,     DummyClassTrackingToStringCalls, Hello, DummyIFormattable.ToString(), {string.Format(null, "{0:tt}", dateTime)}, {string.Format(null, "{0,5:tt}", dateTime)}{Environment.NewLine}{Environment.NewLine}actual: false{Environment.NewLine}{Environment.NewLine}Assert.IsTrue(nullBool)");
    }

    public void IsTrueNullableBooleanInterpolatedStringMessageShouldNotFailWithTrue()
    {
        bool? nullBool = true;
        Assert.IsTrue(nullBool, $"User-provided message. Input: {nullBool}");
    }

    public async Task IsTrueBooleanInterpolatedStringMessageShouldFailWithFalse()
    {
        DummyClassTrackingToStringCalls o = new();
        DateTime dateTime = DateTime.Now;
        Func<Task> action = async () => Assert.IsTrue(false, $"User-provided message. {o}, {o,35}, {await GetHelloStringAsync()}, {new DummyIFormattable()}, {dateTime:tt}, {dateTime,5:tt}");
        (await action.Should().ThrowAsync<Exception>())
            .WithMessage($"Assertion failed. Expected condition to be true.{Environment.NewLine}User-provided message. DummyClassTrackingToStringCalls,     DummyClassTrackingToStringCalls, Hello, DummyIFormattable.ToString(), {string.Format(null, "{0:tt}", dateTime)}, {string.Format(null, "{0,5:tt}", dateTime)}{Environment.NewLine}{Environment.NewLine}actual: false{Environment.NewLine}{Environment.NewLine}Assert.IsTrue(false)");
    }

    public void IsTrueBooleanInterpolatedStringMessageShouldNotFailWithTrue()
        => Assert.IsTrue(true, $"User-provided message. Input: {true}");
}
