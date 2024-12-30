// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable enable

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.VisualStudio.TestPlatform.TestFramework.UnitTests;

public partial class AssertTests
{
    private sealed class DummyClassTrackingToStringCalls
    {
        public bool WasToStringCalled { get; private set; }

        public override string ToString()
        {
            WasToStringCalled = true;
            return nameof(DummyClassTrackingToStringCalls);
        }
    }

    public void AreSame_PassSameObject_ShouldPass()
    {
        object o = new();
        Assert.AreSame(o, o);
    }

    public void AreSame_PassDifferentObject_ShouldFail()
    {
        Exception ex = VerifyThrows(() => Assert.AreSame(new object(), new object()));
        Verify(ex.Message == "Assert.AreSame failed. ");
    }

    public void AreSame_StringMessage_PassSameObject_ShouldPass()
    {
        object o = new();
        Assert.AreSame(o, o, "User-provided message");
    }

    public void AreSame_StringMessage_PassDifferentObject_ShouldFail()
    {
        Exception ex = VerifyThrows(() => Assert.AreSame(new object(), new object(), "User-provided message"));
        Verify(ex.Message == "Assert.AreSame failed. User-provided message");
    }

    public void AreSame_InterpolatedString_PassSameObject_ShouldPass()
    {
        DummyClassTrackingToStringCalls o = new();
        Assert.AreSame(o, o, $"User-provided message: {o}");
        Verify(!o.WasToStringCalled);
    }

    public void AreSame_InterpolatedString_PassDifferentObject_ShouldFail()
    {
        DummyClassTrackingToStringCalls o = new();
        Exception ex = VerifyThrows(() => Assert.AreSame(new object(), new object(), $"User-provided message. {o}"));
        Verify(ex.Message == "Assert.AreSame failed. User-provided message. DummyClassTrackingToStringCalls");
        Verify(o.WasToStringCalled);
    }

    public void AreSame_MessageArgs_PassSameObject_ShouldPass()
    {
        object o = new();
        Assert.AreSame(o, o, "User-provided message: {0}", new object().GetType());
    }

    public void AreSame_MessageArgs_PassDifferentObject_ShouldFail()
    {
        Exception ex = VerifyThrows(() => Assert.AreSame(new object(), new object(), "User-provided message: System.Object type: {0}", new object().GetType()));
        Verify(ex.Message == "Assert.AreSame failed. User-provided message: System.Object type: System.Object");
    }

    public void AreNotSame_PassDifferentObject_ShouldPass()
        => Assert.AreNotSame(new object(), new object());

    public void AreSame_BothAreValueTypes_ShouldFailWithSpecializedMessage()
    {
        Exception ex = VerifyThrows(() => Assert.AreSame(1, 1));
        Verify(ex.Message == "Assert.AreSame failed. Do not pass value types to AreSame(). Values converted to Object will never be the same. Consider using AreEqual(). ");
    }

    public void AreSame_StringMessage_BothAreValueTypes_ShouldFailWithSpecializedMessage()
    {
        Exception ex = VerifyThrows(() => Assert.AreSame(1, 1, "User-provided message"));
        Verify(ex.Message == "Assert.AreSame failed. Do not pass value types to AreSame(). Values converted to Object will never be the same. Consider using AreEqual(). User-provided message");
    }

    public void AreSame_InterpolatedString_BothAreValueTypes_ShouldFailWithSpecializedMessage()
    {
        Exception ex = VerifyThrows(() => Assert.AreSame(1, 1, $"User-provided message {new object().GetType()}"));
        Verify(ex.Message == "Assert.AreSame failed. Do not pass value types to AreSame(). Values converted to Object will never be the same. Consider using AreEqual(). User-provided message System.Object");
    }

    public void AreSame_MessageArgs_BothAreValueTypes_ShouldFailWithSpecializedMessage()
    {
        Exception ex = VerifyThrows(() => Assert.AreSame(1, 1, "User-provided message {0}", new object().GetType()));
        Verify(ex.Message == "Assert.AreSame failed. Do not pass value types to AreSame(). Values converted to Object will never be the same. Consider using AreEqual(). User-provided message System.Object");
    }

    public void AreNotSame_PassSameObject_ShouldFail()
    {
        object o = new();
        Exception ex = VerifyThrows(() => Assert.AreNotSame(o, o));
        Verify(ex.Message == "Assert.AreNotSame failed. ");
    }

    public void AreNotSame_StringMessage_PassDifferentObject_ShouldPass()
        => Assert.AreNotSame(new object(), new object(), "User-provided message");

    public void AreNotSame_StringMessage_PassSameObject_ShouldFail()
    {
        object o = new();
        Exception ex = VerifyThrows(() => Assert.AreNotSame(o, o, "User-provided message"));
        Verify(ex.Message == "Assert.AreNotSame failed. User-provided message");
    }

    public void AreNotSame_InterpolatedString_PassDifferentObject_ShouldPass()
    {
        DummyClassTrackingToStringCalls o = new();
        Assert.AreNotSame(new object(), new object(), $"User-provided message: {o}");
        Verify(!o.WasToStringCalled);
    }

    public void AreNotSame_InterpolatedString_PassSameObject_ShouldFail()
    {
        DummyClassTrackingToStringCalls o = new();
        Exception ex = VerifyThrows(() => Assert.AreNotSame(o, o, $"User-provided message. {o}"));
        Verify(ex.Message == "Assert.AreNotSame failed. User-provided message. DummyClassTrackingToStringCalls");
        Verify(o.WasToStringCalled);
    }

    public void AreNotSame_MessageArgs_PassDifferentObject_ShouldPass()
        => Assert.AreNotSame(new object(), new object(), "User-provided message: {0}", new object().GetType());

    public void AreNotSame_MessageArgs_PassSameObject_ShouldFail()
    {
        object o = new();
        Exception ex = VerifyThrows(() => Assert.AreNotSame(o, o, "User-provided message: System.Object type: {0}", new object().GetType()));
        Verify(ex.Message == "Assert.AreNotSame failed. User-provided message: System.Object type: System.Object");
    }
}
