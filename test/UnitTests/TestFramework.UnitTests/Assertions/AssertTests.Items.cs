﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.VisualStudio.TestPlatform.TestFramework.UnitTests;

public partial class AssertTests
{
    public void Count_WhenCountIsSame_ShouldPass()
    {
        var collection = new List<int> { 1, 2, 3 };
        Assert.HasCount(3, collection);
    }

    public void Count_InterpolatedString_WhenCountIsSame_ShouldPass()
    {
        DummyClassTrackingToStringCalls o = new();
        Assert.HasCount(0, Array.Empty<string>(), $"User-provided message: {o}");
        Verify(!o.WasToStringCalled);
    }

    public void Count_WhenCountIsNotSame_ShouldFail()
    {
        var collection = new List<int> { 1 };
        Exception ex = VerifyThrows(() => Assert.HasCount(3, collection));
        Verify(ex.Message == "Assert.HasCount failed. Expected collection of size 3. Actual: 1. ");
    }

    public void Count_MessageArgs_WhenCountIsNotSame_ShouldFail()
    {
        Exception ex = VerifyThrows(() => Assert.HasCount(1, Array.Empty<float>(), "User-provided message: System.Object type: {0}", new object().GetType()));
        Verify(ex.Message == "Assert.HasCount failed. Expected collection of size 1. Actual: 0. User-provided message: System.Object type: System.Object");
    }

    public async Task Count_InterpolatedString_WhenCountIsNotSame_ShouldFail()
    {
        DummyClassTrackingToStringCalls o = new();
        DateTime dateTime = DateTime.Now;
        Exception ex = await VerifyThrowsAsync(async () => Assert.HasCount(1, Array.Empty<int>(), $"User-provided message. {o}, {o,35}, {await GetHelloStringAsync()}, {new DummyIFormattable()}, {dateTime:tt}, {dateTime,5:tt}"));
        Verify(ex.Message == $"Assert.HasCount failed. Expected collection of size 1. Actual: 0. User-provided message. DummyClassTrackingToStringCalls,     DummyClassTrackingToStringCalls, Hello, DummyIFormattable.ToString(), {string.Format(null, "{0:tt}", dateTime)}, {string.Format(null, "{0,5:tt}", dateTime)}");
        Verify(o.WasToStringCalled);
    }

    public void NotAny_WhenEmpty_ShouldPass()
        => Assert.IsEmpty(Array.Empty<int>());

    public void NotAny_InterpolatedString_WhenEmpty_ShouldPass()
    {
        DummyClassTrackingToStringCalls o = new();
        Assert.IsEmpty(Array.Empty<string>(), $"User-provided message: {o}");
        Verify(!o.WasToStringCalled);
    }

    public void NotAny_WhenNotEmpty_ShouldFail()
    {
        var collection = new List<int> { 1 };
        Exception ex = VerifyThrows(() => Assert.IsEmpty(collection));
        Verify(ex.Message == "Assert.IsEmpty failed. Expected collection of size 0. Actual: 1. ");
    }

    public void NotAny_MessageArgs_WhenNotEmpty_ShouldFail()
    {
        var collection = new List<int> { 1 };
        Exception ex = VerifyThrows(() => Assert.IsEmpty(collection, "User-provided message: System.Object type: {0}", new object().GetType()));
        Verify(ex.Message == "Assert.IsEmpty failed. Expected collection of size 0. Actual: 1. User-provided message: System.Object type: System.Object");
    }

    public async Task NotAny_InterpolatedString_WhenNotEmpty_ShouldFail()
    {
        var collection = new List<int> { 1 };
        DummyClassTrackingToStringCalls o = new();
        DateTime dateTime = DateTime.Now;
        Exception ex = await VerifyThrowsAsync(async () => Assert.IsEmpty(collection, $"User-provided message. {o}, {o,35}, {await GetHelloStringAsync()}, {new DummyIFormattable()}, {dateTime:tt}, {dateTime,5:tt}"));
        Verify(ex.Message == $"Assert.IsEmpty failed. Expected collection of size 0. Actual: 1. User-provided message. DummyClassTrackingToStringCalls,     DummyClassTrackingToStringCalls, Hello, DummyIFormattable.ToString(), {string.Format(null, "{0:tt}", dateTime)}, {string.Format(null, "{0,5:tt}", dateTime)}");
        Verify(o.WasToStringCalled);
    }

    public void Single_WhenOneItem_ShouldPass()
    {
        var collection = new List<int> { 1 };
        int first = Assert.ContainsSingle(collection);
        Verify(first == 1);
    }

    public void Single_InterpolatedString_WhenOneItem_ShouldPass()
    {
        var collection = new List<int> { 1 };
        DummyClassTrackingToStringCalls o = new();
        Assert.ContainsSingle(collection, $"User-provided message: {o}");
        Verify(!o.WasToStringCalled);
    }

    public void Single_WhenNoItems_ShouldFail()
    {
        Exception ex = VerifyThrows(() => Assert.ContainsSingle(Array.Empty<int>()));
        Verify(ex.Message == "Assert.ContainsSingle failed. Expected collection of size 1. Actual: 0. ");
    }

    public void Single_WhenMultipleItems_ShouldFail()
    {
        Exception ex = VerifyThrows(() => Assert.ContainsSingle([1, 2, 3]));
        Verify(ex.Message == "Assert.ContainsSingle failed. Expected collection of size 1. Actual: 3. ");
    }

    public void Single_MessageArgs_WhenNoItem_ShouldFail()
    {
        Exception ex = VerifyThrows(() => Assert.ContainsSingle(Array.Empty<float>(), "User-provided message: System.Object type: {0}", new object().GetType()));
        Verify(ex.Message == "Assert.ContainsSingle failed. Expected collection of size 1. Actual: 0. User-provided message: System.Object type: System.Object");
    }

    public void Single_MessageArgs_WhenMultipleItems_ShouldFail()
    {
        Exception ex = VerifyThrows(() => Assert.ContainsSingle([1, 2, 3], "User-provided message: System.Object type: {0}", new object().GetType()));
        Verify(ex.Message == "Assert.ContainsSingle failed. Expected collection of size 1. Actual: 3. User-provided message: System.Object type: System.Object");
    }

    public async Task Single_InterpolatedString_WhenNoItem_ShouldFail()
    {
        DummyClassTrackingToStringCalls o = new();
        DateTime dateTime = DateTime.Now;
        Exception ex = await VerifyThrowsAsync(async () => Assert.ContainsSingle(Array.Empty<int>(), $"User-provided message. {o}, {o,35}, {await GetHelloStringAsync()}, {new DummyIFormattable()}, {dateTime:tt}, {dateTime,5:tt}"));
        Verify(ex.Message == $"Assert.ContainsSingle failed. Expected collection of size 1. Actual: 0. User-provided message. DummyClassTrackingToStringCalls,     DummyClassTrackingToStringCalls, Hello, DummyIFormattable.ToString(), {string.Format(null, "{0:tt}", dateTime)}, {string.Format(null, "{0,5:tt}", dateTime)}");
        Verify(o.WasToStringCalled);
    }

    public async Task Single_InterpolatedString_WhenMultipleItems_ShouldFail()
    {
        DummyClassTrackingToStringCalls o = new();
        DateTime dateTime = DateTime.Now;
        Exception ex = await VerifyThrowsAsync(async () => Assert.ContainsSingle([1, 2, 3], $"User-provided message. {o}, {o,35}, {await GetHelloStringAsync()}, {new DummyIFormattable()}, {dateTime:tt}, {dateTime,5:tt}"));
        Verify(ex.Message == $"Assert.ContainsSingle failed. Expected collection of size 1. Actual: 3. User-provided message. DummyClassTrackingToStringCalls,     DummyClassTrackingToStringCalls, Hello, DummyIFormattable.ToString(), {string.Format(null, "{0:tt}", dateTime)}, {string.Format(null, "{0,5:tt}", dateTime)}");
        Verify(o.WasToStringCalled);
    }

    public void Any_WhenOneItem_ShouldPass()
    {
        var collection = new List<int> { 1 };
        Assert.IsNotEmpty(collection);
    }

    public void Any_WhenMultipleItems_ShouldPass()
    {
        var collection = new List<int> { 1, 2, 3 };
        Assert.IsNotEmpty(collection);
    }

    public void Any_InterpolatedString_WhenAnyOneItem_ShouldPass()
    {
        DummyClassTrackingToStringCalls o = new();
        Assert.IsNotEmpty([1], $"User-provided message: {o}");
        Verify(!o.WasToStringCalled);
    }

    public void Any_InterpolatedString_WhenMultipleItems_ShouldPass()
    {
        DummyClassTrackingToStringCalls o = new();
        Assert.IsNotEmpty([1, 2, 3], $"User-provided message: {o}");
        Verify(!o.WasToStringCalled);
    }

    public void Any_WhenNoItem_ShouldFail()
    {
        Exception ex = VerifyThrows(() => Assert.IsNotEmpty(Array.Empty<int>()));
        Verify(ex.Message == "Assert.IsNotEmpty failed. Expected collection to contain any item but it is empty. ");
    }

    public void Any_MessageArgs_WhenNoItem_ShouldFail()
    {
        Exception ex = VerifyThrows(() => Assert.IsNotEmpty(Array.Empty<float>(), "User-provided message: System.Object type: {0}", new object().GetType()));
        Verify(ex.Message == "Assert.IsNotEmpty failed. Expected collection to contain any item but it is empty. User-provided message: System.Object type: System.Object");
    }

    public async Task Any_InterpolatedString_WhenNoItem_ShouldFail()
    {
        DummyClassTrackingToStringCalls o = new();
        DateTime dateTime = DateTime.Now;
        Exception ex = await VerifyThrowsAsync(async () => Assert.IsNotEmpty(Array.Empty<string>(), $"User-provided message. {o}, {o,35}, {await GetHelloStringAsync()}, {new DummyIFormattable()}, {dateTime:tt}, {dateTime,5:tt}"));
        Verify(ex.Message == $"Assert.IsNotEmpty failed. Expected collection to contain any item but it is empty. User-provided message. DummyClassTrackingToStringCalls,     DummyClassTrackingToStringCalls, Hello, DummyIFormattable.ToString(), {string.Format(null, "{0:tt}", dateTime)}, {string.Format(null, "{0,5:tt}", dateTime)}");
        Verify(o.WasToStringCalled);
    }
}
