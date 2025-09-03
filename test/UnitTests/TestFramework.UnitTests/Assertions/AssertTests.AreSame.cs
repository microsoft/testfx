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
        action.Should().Throw<Exception>().And.Message.Should().Be("Assert.AreSame failed. 'expected' expression: 'new object()', 'actual' expression: 'new object()'.");
    }

    public void AreSame_StringMessage_PassSameObject_ShouldPass()
    {
        object o = new();
        Assert.AreSame(o, o, "User-provided message");
    }

    public void AreSame_StringMessage_PassDifferentObject_ShouldFail()
    {
        Action action = () => Assert.AreSame(new object(), new object(), "User-provided message");
        action.Should().Throw<Exception>().And.Message.Should().Be("Assert.AreSame failed. 'expected' expression: 'new object()', 'actual' expression: 'new object()'. User-provided message");
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
        (await action.Should().ThrowAsync<Exception>()).And.Message.Should().Be($"Assert.AreSame failed. 'expected' expression: 'new object()', 'actual' expression: 'new object()'. User-provided message. DummyClassTrackingToStringCalls,     DummyClassTrackingToStringCalls, Hello, DummyIFormattable.ToString(), {string.Format(null, "{0:tt}", dateTime)}, {string.Format(null, "{0,5:tt}", dateTime)}");
        o.WasToStringCalled.Should().BeTrue();
    }

    public void AreNotSame_PassDifferentObject_ShouldPass()
        => Assert.AreNotSame(new object(), new object());

    public void AreSame_BothAreValueTypes_ShouldFailWithSpecializedMessage()
    {
        Action action = () => Assert.AreSame(1, 1);
        action.Should().Throw<Exception>().And.Message.Should().Be("Assert.AreSame failed. Do not pass value types to AreSame(). Values converted to Object will never be the same. Consider using AreEqual(). 'expected' expression: '1', 'actual' expression: '1'.");
    }

    public void AreSame_StringMessage_BothAreValueTypes_ShouldFailWithSpecializedMessage()
    {
        Action action = () => Assert.AreSame(1, 1, "User-provided message");
        action.Should().Throw<Exception>().And.Message.Should().Be("Assert.AreSame failed. Do not pass value types to AreSame(). Values converted to Object will never be the same. Consider using AreEqual(). 'expected' expression: '1', 'actual' expression: '1'. User-provided message");
    }

    public void AreSame_InterpolatedString_BothAreValueTypes_ShouldFailWithSpecializedMessage()
    {
        Action action = () => Assert.AreSame(1, 1, $"User-provided message {new object().GetType()}");
        action.Should().Throw<Exception>().And.Message.Should().Be("Assert.AreSame failed. Do not pass value types to AreSame(). Values converted to Object will never be the same. Consider using AreEqual(). 'expected' expression: '1', 'actual' expression: '1'. User-provided message System.Object");
    }

    public void AreNotSame_PassSameObject_ShouldFail()
    {
        object o = new();
        Action action = () => Assert.AreNotSame(o, o);
        action.Should().Throw<Exception>().And.Message.Should().Be("Assert.AreNotSame failed. 'notExpected' expression: 'o', 'actual' expression: 'o'.");
    }

    public void AreNotSame_StringMessage_PassDifferentObject_ShouldPass()
        => Assert.AreNotSame(new object(), new object(), "User-provided message");

    public void AreNotSame_StringMessage_PassSameObject_ShouldFail()
    {
        object o = new();
        Action action = () => Assert.AreNotSame(o, o, "User-provided message");
        action.Should().Throw<Exception>().And.Message.Should().Be("Assert.AreNotSame failed. 'notExpected' expression: 'o', 'actual' expression: 'o'. User-provided message");
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
        (await action.Should().ThrowAsync<Exception>()).And.Message.Should().Be($"Assert.AreNotSame failed. 'notExpected' expression: 'o', 'actual' expression: 'o'. User-provided message. DummyClassTrackingToStringCalls,     DummyClassTrackingToStringCalls, Hello, DummyIFormattable.ToString(), {string.Format(null, "{0:tt}", dateTime)}, {string.Format(null, "{0,5:tt}", dateTime)}");
        o.WasToStringCalled.Should().BeTrue();
    }
}
