// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.VisualStudio.TestPlatform.TestFramework.UnitTests;

public partial class AssertTests
{
    public void IsFalseNullableBooleanShouldFailWithNull()
    {
        bool? nullBool = null;
        Exception ex = VerifyThrows(() => Assert.IsFalse(nullBool));
        Verify(ex.Message == "Assert.IsFalse failed. ");
    }

    public void IsFalseNullableBooleanShouldFailWithTrue()
    {
        bool? nullBool = true;
        Exception ex = VerifyThrows(() => Assert.IsFalse(nullBool));
        Verify(ex.Message == "Assert.IsFalse failed. ");
    }

    public void IsFalseNullableBooleanShouldNotFailWithFalse()
    {
        bool? nullBool = false;
        Assert.IsFalse(nullBool);
    }

    public void IsFalseBooleanShouldFailWithTrue()
    {
        Exception ex = VerifyThrows(() => Assert.IsFalse(true));
        Verify(ex.Message == "Assert.IsFalse failed. ");
    }

    public void IsFalseBooleanShouldNotFailWithFalse()
        => Assert.IsFalse(false);

    public void IsFalseNullableBooleanStringMessageShouldFailWithNull()
    {
        bool? nullBool = null;
        Exception ex = VerifyThrows(() => Assert.IsFalse(nullBool, "User-provided message"));
        Verify(ex.Message == "Assert.IsFalse failed. User-provided message");
    }

    public void IsFalseNullableBooleanStringMessageShouldFailWithTrue()
    {
        bool? nullBool = true;
        Exception ex = VerifyThrows(() => Assert.IsFalse(nullBool, "User-provided message"));
        Verify(ex.Message == "Assert.IsFalse failed. User-provided message");
    }

    public void IsFalseNullableBooleanStringMessageShouldNotFailWithFalse()
    {
        bool? nullBool = false;
        Assert.IsFalse(nullBool, "User-provided message");
    }

    public void IsFalseBooleanStringMessageShouldFailWithTrue()
    {
        Exception ex = VerifyThrows(() => Assert.IsFalse(true, "User-provided message"));
        Verify(ex.Message == "Assert.IsFalse failed. User-provided message");
    }

    public void IsFalseBooleanStringMessageShouldNotFailWithFalse()
        => Assert.IsFalse(false, "User-provided message");

    public void IsFalseNullableBooleanInterpolatedStringMessageShouldFailWithNull()
    {
        bool? nullBool = null;
        Exception ex = VerifyThrows(() => Assert.IsFalse(nullBool, $"User-provided message. Input: {nullBool}"));
        Verify(ex.Message == "Assert.IsFalse failed. User-provided message. Input: ");
    }

    public void IsFalseNullableBooleanInterpolatedStringMessageShouldFailWithTrue()
    {
        bool? nullBool = true;
        Exception ex = VerifyThrows(() => Assert.IsFalse(nullBool, $"User-provided message. Input: {nullBool}"));
        Verify(ex.Message == "Assert.IsFalse failed. User-provided message. Input: True");
    }

    public void IsFalseNullableBooleanInterpolatedStringMessageShouldNotFailWithFalse()
    {
        bool? nullBool = false;
        Assert.IsFalse(nullBool, $"User-provided message. Input: {nullBool}");
    }

    public void IsFalseBooleanInterpolatedStringMessageShouldFailWithTrue()
    {
        Exception ex = VerifyThrows(() => Assert.IsFalse(true, $"User-provided message. Input: {true}"));
        Verify(ex.Message == "Assert.IsFalse failed. User-provided message. Input: True");
    }

    public void IsFalseBooleanInterpolatedStringMessageShouldNotFailWithFalse()
        => Assert.IsFalse(false, $"User-provided message. Input: {false}");

    public void IsFalseNullableBooleanMessageArgsShouldFailWithNull()
    {
        bool? nullBool = null;
        Exception ex = VerifyThrows(() => Assert.IsFalse(nullBool, "User-provided message. Input: {0}", nullBool));
        Verify(ex.Message == "Assert.IsFalse failed. User-provided message. Input: ");
    }

    public void IsFalseNullableBooleanMessageArgsShouldFailWithTrue()
    {
        bool? nullBool = true;
        Exception ex = VerifyThrows(() => Assert.IsFalse(nullBool, "User-provided message. Input: {0}", nullBool));
        Verify(ex.Message == "Assert.IsFalse failed. User-provided message. Input: True");
    }

    public void IsFalseNullableBooleanMessageArgsShouldNotFailWithFalse()
    {
        bool? nullBool = false;
        Assert.IsFalse(nullBool, "User-provided message. Input: {0}", nullBool);
    }

    public void IsFalseBooleanMessageArgsShouldFailWithTrue()
    {
        Exception ex = VerifyThrows(() => Assert.IsFalse(true, "User-provided message. Input: {0}", true));
        Verify(ex.Message == "Assert.IsFalse failed. User-provided message. Input: True");
    }

    public void IsFalseBooleanMessageArgsShouldNotFailWithFalse()
        => Assert.IsFalse(false, "User-provided message. Input: {0}", false);

    public void IsTrueNullableBooleanShouldFailWithNull()
    {
        bool? nullBool = null;
        Exception ex = VerifyThrows(() => Assert.IsTrue(nullBool));
        Verify(ex.Message == "Assert.IsTrue failed. ");
    }

    public void IsTrueNullableBooleanShouldFailWithFalse()
    {
        bool? nullBool = false;
        Exception ex = VerifyThrows(() => Assert.IsTrue(nullBool));
        Verify(ex.Message == "Assert.IsTrue failed. ");
    }

    public void IsTrueNullableBooleanShouldNotFailWithTrue()
    {
        bool? nullBool = true;
        Assert.IsTrue(nullBool);
    }

    public void IsTrueBooleanShouldFailWithFalse()
    {
        Exception ex = VerifyThrows(() => Assert.IsTrue(false));
        Verify(ex.Message == "Assert.IsTrue failed. ");
    }

    public void IsTrueBooleanShouldNotFailWithTrue()
        => Assert.IsTrue(true);

    public void IsTrueNullableBooleanStringMessageShouldFailWithNull()
    {
        bool? nullBool = null;
        Exception ex = VerifyThrows(() => Assert.IsTrue(nullBool, "User-provided message"));
        Verify(ex.Message == "Assert.IsTrue failed. User-provided message");
    }

    public void IsTrueNullableBooleanStringMessageShouldFailWithFalse()
    {
        bool? nullBool = false;
        Exception ex = VerifyThrows(() => Assert.IsTrue(nullBool, "User-provided message"));
        Verify(ex.Message == "Assert.IsTrue failed. User-provided message");
    }

    public void IsTrueNullableBooleanStringMessageShouldNotFailWithTrue()
    {
        bool? nullBool = true;
        Assert.IsTrue(nullBool, "User-provided message");
    }

    public void IsTrueBooleanStringMessageShouldFailWithFalse()
    {
        Exception ex = VerifyThrows(() => Assert.IsTrue(false, "User-provided message"));
        Verify(ex.Message == "Assert.IsTrue failed. User-provided message");
    }

    public void IsTrueBooleanStringMessageShouldNotFailWithTrue()
        => Assert.IsTrue(true, "User-provided message");

    public void IsTrueNullableBooleanInterpolatedStringMessageShouldFailWithNull()
    {
        bool? nullBool = null;
        Exception ex = VerifyThrows(() => Assert.IsTrue(nullBool, $"User-provided message. Input: {nullBool}"));
        Verify(ex.Message == "Assert.IsTrue failed. User-provided message. Input: ");
    }

    public void IsTrueNullableBooleanInterpolatedStringMessageShouldFailWithFalse()
    {
        bool? nullBool = false;
        Exception ex = VerifyThrows(() => Assert.IsTrue(nullBool, $"User-provided message. Input: {nullBool}"));
        Verify(ex.Message == "Assert.IsTrue failed. User-provided message. Input: False");
    }

    public void IsTrueNullableBooleanInterpolatedStringMessageShouldNotFailWithTrue()
    {
        bool? nullBool = true;
        Assert.IsTrue(nullBool, $"User-provided message. Input: {nullBool}");
    }

    public void IsTrueBooleanInterpolatedStringMessageShouldFailWithFalse()
    {
        Exception ex = VerifyThrows(() => Assert.IsTrue(false, $"User-provided message. Input: {false}"));
        Verify(ex.Message == "Assert.IsTrue failed. User-provided message. Input: False");
    }

    public void IsTrueBooleanInterpolatedStringMessageShouldNotFailWithTrue()
        => Assert.IsTrue(true, $"User-provided message. Input: {true}");

    public void IsTrueNullableBooleanMessageArgsShouldFailWithNull()
    {
        bool? nullBool = null;
        Exception ex = VerifyThrows(() => Assert.IsTrue(nullBool, "User-provided message. Input: {0}", nullBool));
        Verify(ex.Message == "Assert.IsTrue failed. User-provided message. Input: ");
    }

    public void IsTrueNullableBooleanMessageArgsShouldFailWithFalse()
    {
        bool? nullBool = false;
        Exception ex = VerifyThrows(() => Assert.IsTrue(nullBool, "User-provided message. Input: {0}", nullBool));
        Verify(ex.Message == "Assert.IsTrue failed. User-provided message. Input: False");
    }

    public void IsTrueNullableBooleanMessageArgsShouldNotFailWithTrue()
    {
        bool? nullBool = true;
        Assert.IsTrue(nullBool, "User-provided message. Input: {0}", nullBool);
    }

    public void IsTrueBooleanMessageArgsShouldFailWithFalse()
    {
        Exception ex = VerifyThrows(() => Assert.IsTrue(false, "User-provided message. Input: {0}", false));
        Verify(ex.Message == "Assert.IsTrue failed. User-provided message. Input: False");
    }

    public void IsTrueBooleanMessageArgsShouldNotFailWithTrue()
        => Assert.IsTrue(true, "User-provided message. Input: {0}", true);
}
