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
            .And.Message.Should().Be("Assert.IsNull failed. 'value' expression: 'new object()'.");
    }

    public void IsNull_StringMessage_PassNull_ShouldPass()
        => Assert.IsNull(null, "User-provided message");

    public void IsNull_StringMessage_PassNonNull_ShouldFail()
    {
        Action action = () => Assert.IsNull(new object(), "User-provided message");
        action.Should().Throw<Exception>()
            .And.Message.Should().Be("Assert.IsNull failed. 'value' expression: 'new object()'. User-provided message");
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
            .And.Message.Should().Be($"Assert.IsNull failed. 'value' expression: 'new object()'. User-provided message. DummyClassTrackingToStringCalls,     DummyClassTrackingToStringCalls, Hello, DummyIFormattable.ToString(), {string.Format(null, "{0:tt}", dateTime)}, {string.Format(null, "{0,5:tt}", dateTime)}");
        o.WasToStringCalled.Should().BeTrue();
    }

    public void IsNotNull_WhenNonNullNullableValue_DoesNotThrowAndLearnNotNull()
    {
        object? obj = GetObj();
        Assert.IsNotNull(obj);
        _ = obj.ToString(); // No potential NRE warning
    }

    public void IsNotNull_WhenNonNullNullableValueAndMessage_DoesNotThrowAndLearnNotNull()
    {
        object? obj = GetObj();
        Assert.IsNotNull(obj, "my message");
        _ = obj.ToString(); // No potential NRE warning
    }

    public void IsNotNull_WhenNonNullNullableValueAndInterpolatedStringMessage_DoesNotThrowAndLearnNotNull()
    {
        object? obj = GetObj();
        DummyClassTrackingToStringCalls o = new();
        Assert.IsNotNull(obj, $"my message {o}");
        o.WasToStringCalled.Should().BeFalse();
        _ = obj.ToString(); // No potential NRE warning
    }

    public void IsNotNull_PassNull_ShouldFail()
    {
        Action action = () => Assert.IsNotNull(null);
        action.Should().Throw<Exception>()
            .And.Message.Should().Be("Assert.IsNotNull failed. 'value' expression: 'null'.");
    }

    public void IsNotNull_StringMessage_PassNonNull_ShouldFail()
    {
        Action action = () => Assert.IsNotNull(null, "User-provided message");
        action.Should().Throw<Exception>()
            .And.Message.Should().Be("Assert.IsNotNull failed. 'value' expression: 'null'. User-provided message");
    }

    public async Task IsNotNull_InterpolatedString_PassNonNull_ShouldFail()
    {
        DummyClassTrackingToStringCalls o = new();
        DateTime dateTime = DateTime.Now;
        Func<Task> action = async () => Assert.IsNotNull(null, $"User-provided message. {o}, {o,35}, {await GetHelloStringAsync()}, {new DummyIFormattable()}, {dateTime:tt}, {dateTime,5:tt}");
        (await action.Should().ThrowAsync<Exception>())
            .And.Message.Should().Be($"Assert.IsNotNull failed. 'value' expression: 'null'. User-provided message. DummyClassTrackingToStringCalls,     DummyClassTrackingToStringCalls, Hello, DummyIFormattable.ToString(), {string.Format(null, "{0:tt}", dateTime)}, {string.Format(null, "{0,5:tt}", dateTime)}");
        o.WasToStringCalled.Should().BeTrue();
    }

}
