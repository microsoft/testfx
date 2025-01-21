// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.VisualStudio.TestPlatform.TestFramework.UnitTests;

public partial class AssertTests
{
    public void HasCount_WhenCountIsSame_ShouldPass()
    {
        var collection = new List<int> { 1, 2, 3 };
        Assert.HasCount(3, collection);
    }

    public void HasCount_InterpolatedString_WhenCountIsSame_ShouldPass()
    {
        DummyClassTrackingToStringCalls o = new();
        Assert.HasCount(0, Array.Empty<string>(), $"User-provided message: {o}");
        Verify(!o.WasToStringCalled);
    }

    public void HasCount_WhenCountIsNotSame_ShouldFail()
    {
        var collection = new List<int> { 1 };
        Exception ex = VerifyThrows(() => Assert.HasCount(3, collection));
        Verify(ex.Message == "Assert.HasCount failed. Expected count of 3. Actual: 1. ");
    }

    public void HasCount_MessageArgs_WhenCountIsNotSame_ShouldFail()
    {
        Exception ex = VerifyThrows(() => Assert.HasCount(1, Array.Empty<float>(), "User-provided message: System.Object type: {0}", new object().GetType()));
        Verify(ex.Message == "Assert.HasCount failed. Expected count of 1. Actual: 0. User-provided message: System.Object type: System.Object");
    }

    public async Task HasCount_InterpolatedString_WhenCountIsNotSame_ShouldFail()
    {
        DummyClassTrackingToStringCalls o = new();
        DateTime dateTime = DateTime.Now;
        Exception ex = await VerifyThrowsAsync(async () => Assert.HasCount(1, Array.Empty<int>(), $"User-provided message. {o}, {o,35}, {await GetHelloStringAsync()}, {new DummyIFormattable()}, {dateTime:tt}, {dateTime,5:tt}"));
        Verify(ex.Message == $"Assert.HasCount failed. Expected count of 1. Actual: 0. User-provided message. DummyClassTrackingToStringCalls,     DummyClassTrackingToStringCalls, Hello, DummyIFormattable.ToString(), {string.Format(null, "{0:tt}", dateTime)}, {string.Format(null, "{0,5:tt}", dateTime)}");
        Verify(o.WasToStringCalled);
    }
}
