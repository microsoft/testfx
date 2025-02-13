﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.VisualStudio.TestTools.UnitTesting;

using TestFramework.ForTestingMSTest;

namespace Microsoft.VisualStudio.TestPlatform.TestFramework.UnitTests;

public partial class AssertTests : TestContainer
{
    public void IsNull_PassNull_ShouldPass()
        => Assert.IsNull(null);

    public void IsNull_PassNonNull_ShouldFail()
    {
        Exception ex = VerifyThrows(() => Assert.IsNull(new object()));
        Verify(ex.Message == "Assert.IsNull failed. ");
    }

    public void IsNull_StringMessage_PassNull_ShouldPass()
        => Assert.IsNull(null, "User-provided message");

    public void IsNull_StringMessage_PassNonNull_ShouldFail()
    {
        Exception ex = VerifyThrows(() => Assert.IsNull(new object(), "User-provided message"));
        Verify(ex.Message == "Assert.IsNull failed. User-provided message");
    }

    public void IsNull_InterpolatedString_PassNull_ShouldPass()
    {
        DummyClassTrackingToStringCalls o = new();
        Assert.IsNull(null, $"User-provided message {o}");
        Verify(!o.WasToStringCalled);
    }

    public async Task IsNull_InterpolatedString_PassNonNull_ShouldFail()
    {
        DummyClassTrackingToStringCalls o = new();
        DateTime dateTime = DateTime.Now;
        Exception ex = await VerifyThrowsAsync(async () => Assert.IsNull(new object(), $"User-provided message. {o}, {o,35}, {await GetHelloStringAsync()}, {new DummyIFormattable()}, {dateTime:tt}, {dateTime,5:tt}"));
        Verify(ex.Message == $"Assert.IsNull failed. User-provided message. DummyClassTrackingToStringCalls,     DummyClassTrackingToStringCalls, Hello, DummyIFormattable.ToString(), {string.Format(null, "{0:tt}", dateTime)}, {string.Format(null, "{0,5:tt}", dateTime)}");
        Verify(o.WasToStringCalled);
    }

    public void IsNull_MessageFormat_PassNull_ShouldPass()
        => Assert.IsNull(null, "User-provided message {0}", new object().GetType());

    public void IsNull_MessageFormat_PassNonNull_ShouldFail()
    {
        Exception ex = VerifyThrows(() => Assert.IsNull(new object(), "User-provided message {0}", new object().GetType()));
        Verify(ex.Message == "Assert.IsNull failed. User-provided message System.Object");
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
        Verify(!o.WasToStringCalled);
        _ = obj.ToString(); // No potential NRE warning
    }

    public void IsNotNull_WhenNonNullNullableValueAndCompositeMessage_DoesNotThrowAndLearnNotNull()
    {
        object? obj = GetObj();
        Assert.IsNotNull(obj, "my message with {0}", "some arg");
        _ = obj.ToString(); // No potential NRE warning
    }

    public void IsNotNull_PassNull_ShouldFail()
    {
        Exception ex = VerifyThrows(() => Assert.IsNotNull(null));
        Verify(ex.Message == "Assert.IsNotNull failed. ");
    }

    public void IsNotNull_StringMessage_PassNonNull_ShouldFail()
    {
        Exception ex = VerifyThrows(() => Assert.IsNotNull(null, "User-provided message"));
        Verify(ex.Message == "Assert.IsNotNull failed. User-provided message");
    }

    public async Task IsNotNull_InterpolatedString_PassNonNull_ShouldFail()
    {
        DummyClassTrackingToStringCalls o = new();
        DateTime dateTime = DateTime.Now;
        Exception ex = await VerifyThrowsAsync(async () => Assert.IsNotNull(null, $"User-provided message. {o}, {o,35}, {await GetHelloStringAsync()}, {new DummyIFormattable()}, {dateTime:tt}, {dateTime,5:tt}"));
        Verify(ex.Message == $"Assert.IsNotNull failed. User-provided message. DummyClassTrackingToStringCalls,     DummyClassTrackingToStringCalls, Hello, DummyIFormattable.ToString(), {string.Format(null, "{0:tt}", dateTime)}, {string.Format(null, "{0,5:tt}", dateTime)}");
        Verify(o.WasToStringCalled);
    }

    public void IsNotNull_MessageFormat_PassNonNull_ShouldFail()
    {
        Exception ex = VerifyThrows(() => Assert.IsNotNull(null, "User-provided message {0}", new object().GetType()));
        Verify(ex.Message == "Assert.IsNotNull failed. User-provided message System.Object");
    }
}
