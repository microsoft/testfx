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
            .And.Message.Should().Be("Assert.IsFalse failed. ");
    }

    public void IsFalseNullableBooleanShouldFailWithTrue()
    {
        bool? nullBool = true;
        Action action = () => Assert.IsFalse(nullBool);
        action.Should().Throw<Exception>()
            .And.Message.Should().Be("Assert.IsFalse failed. ");
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
            .And.Message.Should().Be("Assert.IsFalse failed. ");
    }

    public void IsFalseBooleanShouldNotFailWithFalse()
        => Assert.IsFalse(false);

    public void IsFalseNullableBooleanStringMessageShouldFailWithNull()
    {
        bool? nullBool = null;
        Action action = () => Assert.IsFalse(nullBool, "User-provided message");
        action.Should().Throw<Exception>()
            .And.Message.Should().Be("Assert.IsFalse failed. User-provided message");
    }

    public void IsFalseNullableBooleanStringMessageShouldFailWithTrue()
    {
        bool? nullBool = true;
        Action action = () => Assert.IsFalse(nullBool, "User-provided message");
        action.Should().Throw<Exception>()
            .And.Message.Should().Be("Assert.IsFalse failed. User-provided message");
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
            .And.Message.Should().Be("Assert.IsFalse failed. User-provided message");
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
            .And.Message.Should().Be($"Assert.IsFalse failed. User-provided message. DummyClassTrackingToStringCalls,     DummyClassTrackingToStringCalls, Hello, DummyIFormattable.ToString(), {string.Format(null, "{0:tt}", dateTime)}, {string.Format(null, "{0,5:tt}", dateTime)}");
    }

    public async Task IsFalseNullableBooleanInterpolatedStringMessageShouldFailWithTrue()
    {
        bool? nullBool = true;
        DummyClassTrackingToStringCalls o = new();
        DateTime dateTime = DateTime.Now;
        Func<Task> action = async () => Assert.IsFalse(nullBool, $"User-provided message. {o}, {o,35}, {await GetHelloStringAsync()}, {new DummyIFormattable()}, {dateTime:tt}, {dateTime,5:tt}");
        (await action.Should().ThrowAsync<Exception>())
            .And.Message.Should().Be($"Assert.IsFalse failed. User-provided message. DummyClassTrackingToStringCalls,     DummyClassTrackingToStringCalls, Hello, DummyIFormattable.ToString(), {string.Format(null, "{0:tt}", dateTime)}, {string.Format(null, "{0,5:tt}", dateTime)}");
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
            .And.Message.Should().Be($"Assert.IsFalse failed. User-provided message. DummyClassTrackingToStringCalls,     DummyClassTrackingToStringCalls, Hello, DummyIFormattable.ToString(), {string.Format(null, "{0:tt}", dateTime)}, {string.Format(null, "{0,5:tt}", dateTime)}");
    }

    public void IsFalseBooleanInterpolatedStringMessageShouldNotFailWithFalse()
        => Assert.IsFalse(false, $"User-provided message. Input: {false}");

    public void IsFalseNullableBooleanMessageArgsShouldFailWithNull()
    {
        bool? nullBool = null;
        Action action = () => Assert.IsFalse(nullBool, "User-provided message. Input: {0}", nullBool);
        action.Should().Throw<Exception>()
            .And.Message.Should().Be("Assert.IsFalse failed. User-provided message. Input: ");
    }

    public void IsFalseNullableBooleanMessageArgsShouldFailWithTrue()
    {
        bool? nullBool = true;
        Action action = () => Assert.IsFalse(nullBool, "User-provided message. Input: {0}", nullBool);
        action.Should().Throw<Exception>()
            .And.Message.Should().Be("Assert.IsFalse failed. User-provided message. Input: True");
    }

    public void IsFalseNullableBooleanMessageArgsShouldNotFailWithFalse()
    {
        bool? nullBool = false;
        Assert.IsFalse(nullBool, "User-provided message. Input: {0}", nullBool);
    }

    public void IsFalseBooleanMessageArgsShouldFailWithTrue()
    {
        Action action = () => Assert.IsFalse(true, "User-provided message. Input: {0}", true);
        action.Should().Throw<Exception>()
            .And.Message.Should().Be("Assert.IsFalse failed. User-provided message. Input: True");
    }

    public void IsFalseBooleanMessageArgsShouldNotFailWithFalse()
        => Assert.IsFalse(false, "User-provided message. Input: {0}", false);

    public void IsTrueNullableBooleanShouldFailWithNull()
    {
        bool? nullBool = null;
        Action action = () => Assert.IsTrue(nullBool);
        action.Should().Throw<Exception>()
            .And.Message.Should().Be("Assert.IsTrue failed. ");
    }

    public void IsTrueNullableBooleanShouldFailWithFalse()
    {
        bool? nullBool = false;
        Action action = () => Assert.IsTrue(nullBool);
        action.Should().Throw<Exception>()
            .And.Message.Should().Be("Assert.IsTrue failed. ");
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
            .And.Message.Should().Be("Assert.IsTrue failed. ");
    }

    public void IsTrueBooleanShouldNotFailWithTrue()
        => Assert.IsTrue(true);

    public void IsTrueNullableBooleanStringMessageShouldFailWithNull()
    {
        bool? nullBool = null;
        Action action = () => Assert.IsTrue(nullBool, "User-provided message");
        action.Should().Throw<Exception>()
            .And.Message.Should().Be("Assert.IsTrue failed. User-provided message");
    }

    public void IsTrueNullableBooleanStringMessageShouldFailWithFalse()
    {
        bool? nullBool = false;
        Action action = () => Assert.IsTrue(nullBool, "User-provided message");
        action.Should().Throw<Exception>()
            .And.Message.Should().Be("Assert.IsTrue failed. User-provided message");
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
            .And.Message.Should().Be("Assert.IsTrue failed. User-provided message");
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
            .And.Message.Should().Be($"Assert.IsTrue failed. User-provided message. DummyClassTrackingToStringCalls,     DummyClassTrackingToStringCalls, Hello, DummyIFormattable.ToString(), {string.Format(null, "{0:tt}", dateTime)}, {string.Format(null, "{0,5:tt}", dateTime)}");
    }

    public async Task IsTrueNullableBooleanInterpolatedStringMessageShouldFailWithFalse()
    {
        bool? nullBool = false;
        DummyClassTrackingToStringCalls o = new();
        DateTime dateTime = DateTime.Now;
        Func<Task> action = async () => Assert.IsTrue(nullBool, $"User-provided message. {o}, {o,35}, {await GetHelloStringAsync()}, {new DummyIFormattable()}, {dateTime:tt}, {dateTime,5:tt}");
        (await action.Should().ThrowAsync<Exception>())
            .And.Message.Should().Be($"Assert.IsTrue failed. User-provided message. DummyClassTrackingToStringCalls,     DummyClassTrackingToStringCalls, Hello, DummyIFormattable.ToString(), {string.Format(null, "{0:tt}", dateTime)}, {string.Format(null, "{0,5:tt}", dateTime)}");
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
            .And.Message.Should().Be($"Assert.IsTrue failed. User-provided message. DummyClassTrackingToStringCalls,     DummyClassTrackingToStringCalls, Hello, DummyIFormattable.ToString(), {string.Format(null, "{0:tt}", dateTime)}, {string.Format(null, "{0,5:tt}", dateTime)}");
    }

    public void IsTrueBooleanInterpolatedStringMessageShouldNotFailWithTrue()
        => Assert.IsTrue(true, $"User-provided message. Input: {true}");

    public void IsTrueNullableBooleanMessageArgsShouldFailWithNull()
    {
        bool? nullBool = null;
        Action action = () => Assert.IsTrue(nullBool, "User-provided message. Input: {0}", nullBool);
        action.Should().Throw<Exception>()
            .And.Message.Should().Be("Assert.IsTrue failed. User-provided message. Input: ");
    }

    public void IsTrueNullableBooleanMessageArgsShouldFailWithFalse()
    {
        bool? nullBool = false;
        Action action = () => Assert.IsTrue(nullBool, "User-provided message. Input: {0}", nullBool);
        action.Should().Throw<Exception>()
            .And.Message.Should().Be("Assert.IsTrue failed. User-provided message. Input: False");
    }

    public void IsTrueNullableBooleanMessageArgsShouldNotFailWithTrue()
    {
        bool? nullBool = true;
        Assert.IsTrue(nullBool, "User-provided message. Input: {0}", nullBool);
    }

    public void IsTrueBooleanMessageArgsShouldFailWithFalse()
    {
        Action action = () => Assert.IsTrue(false, "User-provided message. Input: {0}", false);
        action.Should().Throw<Exception>()
            .And.Message.Should().Be("Assert.IsTrue failed. User-provided message. Input: False");
    }

    public void IsTrueBooleanMessageArgsShouldNotFailWithTrue()
        => Assert.IsTrue(true, "User-provided message. Input: {0}", true);
}
