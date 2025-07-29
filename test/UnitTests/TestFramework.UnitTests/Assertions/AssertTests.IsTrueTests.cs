// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using FluentAssertions;

namespace Microsoft.VisualStudio.TestPlatform.TestFramework.UnitTests;

public partial class AssertTests
{
    public void IsFalseNullableBooleanShouldFailWithNull()
    {
        bool? nullBool = null;
        Exception ex = VerifyThrows(() => Assert.IsFalse(nullBool));
        ex.Message.Should().Be("Assert.IsFalse failed. ");
    }

    public void IsFalseNullableBooleanShouldFailWithTrue()
    {
        bool? nullBool = true;
        Exception ex = VerifyThrows(() => Assert.IsFalse(nullBool));
        ex.Message.Should().Be("Assert.IsFalse failed. ");
    }

    public void IsFalseNullableBooleanShouldNotFailWithFalse()
    {
        bool? nullBool = false;
        Assert.IsFalse(nullBool);
    }

    public void IsFalseBooleanShouldFailWithTrue()
    {
        Exception ex = VerifyThrows(() => Assert.IsFalse(true));
        ex.Message.Should().Be("Assert.IsFalse failed. ");
    }

    public void IsFalseBooleanShouldNotFailWithFalse()
        => Assert.IsFalse(false);

    public void IsFalseNullableBooleanStringMessageShouldFailWithNull()
    {
        bool? nullBool = null;
        Exception ex = VerifyThrows(() => Assert.IsFalse(nullBool, "User-provided message"));
        ex.Message.Should().Be("Assert.IsFalse failed. User-provided message");
    }

    public void IsFalseNullableBooleanStringMessageShouldFailWithTrue()
    {
        bool? nullBool = true;
        Exception ex = VerifyThrows(() => Assert.IsFalse(nullBool, "User-provided message"));
        ex.Message.Should().Be("Assert.IsFalse failed. User-provided message");
    }

    public void IsFalseNullableBooleanStringMessageShouldNotFailWithFalse()
    {
        bool? nullBool = false;
        Assert.IsFalse(nullBool, "User-provided message");
    }

    public void IsFalseBooleanStringMessageShouldFailWithTrue()
    {
        Exception ex = VerifyThrows(() => Assert.IsFalse(true, "User-provided message"));
        ex.Message.Should().Be("Assert.IsFalse failed. User-provided message");
    }

    public void IsFalseBooleanStringMessageShouldNotFailWithFalse()
        => Assert.IsFalse(false, "User-provided message");

    public async Task IsFalseNullableBooleanInterpolatedStringMessageShouldFailWithNull()
    {
        bool? nullBool = null;
        DummyClassTrackingToStringCalls o = new();
        DateTime dateTime = DateTime.Now;
        Exception ex = await VerifyThrowsAsync(async () => Assert.IsFalse(nullBool, $"User-provided message. {o}, {o,35}, {await GetHelloStringAsync()}, {new DummyIFormattable()}, {dateTime:tt}, {dateTime,5:tt}"));
        ex.Message.Should().Be($"Assert.IsFalse failed. User-provided message. DummyClassTrackingToStringCalls,     DummyClassTrackingToStringCalls, Hello, DummyIFormattable.ToString(), {string.Format(null, "{0:tt}", dateTime)}, {string.Format(null, "{0,5:tt}", dateTime)}");
    }

    public async Task IsFalseNullableBooleanInterpolatedStringMessageShouldFailWithTrue()
    {
        bool? nullBool = true;
        DummyClassTrackingToStringCalls o = new();
        DateTime dateTime = DateTime.Now;
        Exception ex = await VerifyThrowsAsync(async () => Assert.IsFalse(nullBool, $"User-provided message. {o}, {o,35}, {await GetHelloStringAsync()}, {new DummyIFormattable()}, {dateTime:tt}, {dateTime,5:tt}"));
        ex.Message.Should().Be($"Assert.IsFalse failed. User-provided message. DummyClassTrackingToStringCalls,     DummyClassTrackingToStringCalls, Hello, DummyIFormattable.ToString(), {string.Format(null, "{0:tt}", dateTime)}, {string.Format(null, "{0,5:tt}", dateTime)}");
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
        Exception ex = await VerifyThrowsAsync(async () => Assert.IsFalse(true, $"User-provided message. {o}, {o,35}, {await GetHelloStringAsync()}, {new DummyIFormattable()}, {dateTime:tt}, {dateTime,5:tt}"));
        ex.Message.Should().Be($"Assert.IsFalse failed. User-provided message. DummyClassTrackingToStringCalls,     DummyClassTrackingToStringCalls, Hello, DummyIFormattable.ToString(), {string.Format(null, "{0:tt}", dateTime)}, {string.Format(null, "{0,5:tt}", dateTime)}");
    }

    public void IsFalseBooleanInterpolatedStringMessageShouldNotFailWithFalse()
        => Assert.IsFalse(false, $"User-provided message. Input: {false}");

    public void IsFalseNullableBooleanMessageArgsShouldFailWithNull()
    {
        bool? nullBool = null;
        Exception ex = VerifyThrows(() => Assert.IsFalse(nullBool, "User-provided message. Input: {0}", nullBool));
        ex.Message.Should().Be("Assert.IsFalse failed. User-provided message. Input: ");
    }

    public void IsFalseNullableBooleanMessageArgsShouldFailWithTrue()
    {
        bool? nullBool = true;
        Exception ex = VerifyThrows(() => Assert.IsFalse(nullBool, "User-provided message. Input: {0}", nullBool));
        ex.Message.Should().Be("Assert.IsFalse failed. User-provided message. Input: True");
    }

    public void IsFalseNullableBooleanMessageArgsShouldNotFailWithFalse()
    {
        bool? nullBool = false;
        Assert.IsFalse(nullBool, "User-provided message. Input: {0}", nullBool);
    }

    public void IsFalseBooleanMessageArgsShouldFailWithTrue()
    {
        Exception ex = VerifyThrows(() => Assert.IsFalse(true, "User-provided message. Input: {0}", true));
        ex.Message.Should().Be("Assert.IsFalse failed. User-provided message. Input: True");
    }

    public void IsFalseBooleanMessageArgsShouldNotFailWithFalse()
        => Assert.IsFalse(false, "User-provided message. Input: {0}", false);

    public void IsTrueNullableBooleanShouldFailWithNull()
    {
        bool? nullBool = null;
        Exception ex = VerifyThrows(() => Assert.IsTrue(nullBool));
        ex.Message.Should().Be("Assert.IsTrue failed. ");
    }

    public void IsTrueNullableBooleanShouldFailWithFalse()
    {
        bool? nullBool = false;
        Exception ex = VerifyThrows(() => Assert.IsTrue(nullBool));
        ex.Message.Should().Be("Assert.IsTrue failed. ");
    }

    public void IsTrueNullableBooleanShouldNotFailWithTrue()
    {
        bool? nullBool = true;
        Assert.IsTrue(nullBool);
    }

    public void IsTrueBooleanShouldFailWithFalse()
    {
        Exception ex = VerifyThrows(() => Assert.IsTrue(false));
        ex.Message.Should().Be("Assert.IsTrue failed. ");
    }

    public void IsTrueBooleanShouldNotFailWithTrue()
        => Assert.IsTrue(true);

    public void IsTrueNullableBooleanStringMessageShouldFailWithNull()
    {
        bool? nullBool = null;
        Exception ex = VerifyThrows(() => Assert.IsTrue(nullBool, "User-provided message"));
        ex.Message.Should().Be("Assert.IsTrue failed. User-provided message");
    }

    public void IsTrueNullableBooleanStringMessageShouldFailWithFalse()
    {
        bool? nullBool = false;
        Exception ex = VerifyThrows(() => Assert.IsTrue(nullBool, "User-provided message"));
        ex.Message.Should().Be("Assert.IsTrue failed. User-provided message");
    }

    public void IsTrueNullableBooleanStringMessageShouldNotFailWithTrue()
    {
        bool? nullBool = true;
        Assert.IsTrue(nullBool, "User-provided message");
    }

    public void IsTrueBooleanStringMessageShouldFailWithFalse()
    {
        Exception ex = VerifyThrows(() => Assert.IsTrue(false, "User-provided message"));
        ex.Message.Should().Be("Assert.IsTrue failed. User-provided message");
    }

    public void IsTrueBooleanStringMessageShouldNotFailWithTrue()
        => Assert.IsTrue(true, "User-provided message");

    public async Task IsTrueNullableBooleanInterpolatedStringMessageShouldFailWithNull()
    {
        bool? nullBool = null;
        DummyClassTrackingToStringCalls o = new();
        DateTime dateTime = DateTime.Now;
        Exception ex = await VerifyThrowsAsync(async () => Assert.IsTrue(nullBool, $"User-provided message. {o}, {o,35}, {await GetHelloStringAsync()}, {new DummyIFormattable()}, {dateTime:tt}, {dateTime,5:tt}"));
        ex.Message.Should().Be($"Assert.IsTrue failed. User-provided message. DummyClassTrackingToStringCalls,     DummyClassTrackingToStringCalls, Hello, DummyIFormattable.ToString(), {string.Format(null, "{0:tt}", dateTime)}, {string.Format(null, "{0,5:tt}", dateTime)}");
    }

    public async Task IsTrueNullableBooleanInterpolatedStringMessageShouldFailWithFalse()
    {
        bool? nullBool = false;
        DummyClassTrackingToStringCalls o = new();
        DateTime dateTime = DateTime.Now;
        Exception ex = await VerifyThrowsAsync(async () => Assert.IsTrue(nullBool, $"User-provided message. {o}, {o,35}, {await GetHelloStringAsync()}, {new DummyIFormattable()}, {dateTime:tt}, {dateTime,5:tt}"));
        ex.Message.Should().Be($"Assert.IsTrue failed. User-provided message. DummyClassTrackingToStringCalls,     DummyClassTrackingToStringCalls, Hello, DummyIFormattable.ToString(), {string.Format(null, "{0:tt}", dateTime)}, {string.Format(null, "{0,5:tt}", dateTime)}");
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
        Exception ex = await VerifyThrowsAsync(async () => Assert.IsTrue(false, $"User-provided message. {o}, {o,35}, {await GetHelloStringAsync()}, {new DummyIFormattable()}, {dateTime:tt}, {dateTime,5:tt}"));
        ex.Message.Should().Be($"Assert.IsTrue failed. User-provided message. DummyClassTrackingToStringCalls,     DummyClassTrackingToStringCalls, Hello, DummyIFormattable.ToString(), {string.Format(null, "{0:tt}", dateTime)}, {string.Format(null, "{0,5:tt}", dateTime)}");
    }

    public void IsTrueBooleanInterpolatedStringMessageShouldNotFailWithTrue()
        => Assert.IsTrue(true, $"User-provided message. Input: {true}");

    public void IsTrueNullableBooleanMessageArgsShouldFailWithNull()
    {
        bool? nullBool = null;
        Exception ex = VerifyThrows(() => Assert.IsTrue(nullBool, "User-provided message. Input: {0}", nullBool));
        ex.Message.Should().Be("Assert.IsTrue failed. User-provided message. Input: ");
    }

    public void IsTrueNullableBooleanMessageArgsShouldFailWithFalse()
    {
        bool? nullBool = false;
        Exception ex = VerifyThrows(() => Assert.IsTrue(nullBool, "User-provided message. Input: {0}", nullBool));
        ex.Message.Should().Be("Assert.IsTrue failed. User-provided message. Input: False");
    }

    public void IsTrueNullableBooleanMessageArgsShouldNotFailWithTrue()
    {
        bool? nullBool = true;
        Assert.IsTrue(nullBool, "User-provided message. Input: {0}", nullBool);
    }

    public void IsTrueBooleanMessageArgsShouldFailWithFalse()
    {
        Exception ex = VerifyThrows(() => Assert.IsTrue(false, "User-provided message. Input: {0}", false));
        ex.Message.Should().Be("Assert.IsTrue failed. User-provided message. Input: False");
    }

    public void IsTrueBooleanMessageArgsShouldNotFailWithTrue()
        => Assert.IsTrue(true, "User-provided message. Input: {0}", true);
}
