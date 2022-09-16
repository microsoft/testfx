// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestPlatform.TestFramework.UnitTests;

using System;
using System.Globalization;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using global::TestFramework.ForTestingMSTest;

public partial class AssertTests : TestContainer
{
    public void AreNotEqualShouldFailWhenNotEqualType()
    {
        static void action() => Assert.AreNotEqual(null, null);
        var ex = VerifyThrows(action);
        Verify(ex.GetType() == typeof(AssertFailedException));
    }

    public void AreNotEqualShouldFailWhenNotEqualTypeWithMessage()
    {
        var ex = VerifyThrows(() => Assert.AreNotEqual(null, null, "A Message"));
        Verify(ex != null);
        Verify(ex.Message.Contains("A Message"));
    }

    public void AreNotEqualShouldFailWhenNotEqualString()
    {
        static void action() => Assert.AreNotEqual("A", "A");
        var ex = VerifyThrows(action);
        Verify(ex.GetType() == typeof(AssertFailedException));
    }

    public void AreNotEqualShouldFailWhenNotEqualStringWithMessage()
    {
        var ex = VerifyThrows(() => Assert.AreNotEqual("A", "A", "A Message"));
        Verify(ex != null);
        Verify(ex.Message.Contains("A Message"));
    }

    public void AreNotEqualShouldFailWhenNotEqualStringAndCaseIgnored()
    {
        static void action() => Assert.AreNotEqual("A", "a", true);
        var ex = VerifyThrows(action);
        Verify(ex.GetType() == typeof(AssertFailedException));
    }

    public void AreNotEqualShouldFailWhenNotEqualInt()
    {
        static void action() => TestFrameworkV2.Assert.AreNotEqual(1, 1);
        ActionUtility.ActionShouldThrowExceptionOfType(action, typeof(TestFrameworkV2.AssertFailedException));
    }

    public void AreNotEqualShouldFailWhenNotEqualIntWithMessage()
    {
        var ex = ActionUtility.PerformActionAndReturnException(() => TestFrameworkV2.Assert.AreNotEqual(1, 1, "A Message"));
        Assert.IsNotNull(ex);
        StringAssert.Contains(ex.Message, "A Message");
    }

    public void AreNotEqualShouldFailWhenNotEqualLong()
    {
        static void action() => TestFrameworkV2.Assert.AreNotEqual(1L, 1L);
        ActionUtility.ActionShouldThrowExceptionOfType(action, typeof(TestFrameworkV2.AssertFailedException));
    }

    public void AreNotEqualShouldFailWhenNotEqualLongWithMessage()
    {
        var ex = ActionUtility.PerformActionAndReturnException(() => TestFrameworkV2.Assert.AreNotEqual(1L, 1L, "A Message"));
        Assert.IsNotNull(ex);
        StringAssert.Contains(ex.Message, "A Message");
    }

    public void AreNotEqualShouldFailWhenNotEqualLongWithDelta()
    {
        static void action() => TestFrameworkV2.Assert.AreNotEqual(1L, 2L, 1L);
        ActionUtility.ActionShouldThrowExceptionOfType(action, typeof(TestFrameworkV2.AssertFailedException));
    }

    public void AreNotEqualShouldFailWhenNotEqualDecimal()
    {
        static void action() => TestFrameworkV2.Assert.AreNotEqual(0.1M, 0.1M);
        ActionUtility.ActionShouldThrowExceptionOfType(action, typeof(TestFrameworkV2.AssertFailedException));
    }

    public void AreNotEqualShouldFailWhenNotEqualDecimalWithMessage()
    {
        var ex = ActionUtility.PerformActionAndReturnException(() => TestFrameworkV2.Assert.AreNotEqual(0.1M, 0.1M, "A Message"));
        Assert.IsNotNull(ex);
        StringAssert.Contains(ex.Message, "A Message");
    }

    public void AreNotEqualShouldFailWhenNotEqualDecimalWithDelta()
    {
        static void action() => TestFrameworkV2.Assert.AreNotEqual(0.1M, 0.2M, 0.1M);
        ActionUtility.ActionShouldThrowExceptionOfType(action, typeof(TestFrameworkV2.AssertFailedException));
    }

    public void AreNotEqualShouldFailWhenNotEqualDouble()
    {
        static void action() => TestFrameworkV2.Assert.AreNotEqual(0.1, 0.1);
        ActionUtility.ActionShouldThrowExceptionOfType(action, typeof(TestFrameworkV2.AssertFailedException));
    }

    public void AreNotEqualShouldFailWhenNotEqualDoubleWithMessage()
    {
        var ex = ActionUtility.PerformActionAndReturnException(() => TestFrameworkV2.Assert.AreNotEqual(0.1, 0.1, "A Message"));
        Assert.IsNotNull(ex);
        StringAssert.Contains(ex.Message, "A Message");
    }

    public void AreNotEqualShouldFailWhenNotEqualDoubleWithDelta()
    {
        static void action() => TestFrameworkV2.Assert.AreNotEqual(0.1, 0.2, 0.1);
        ActionUtility.ActionShouldThrowExceptionOfType(action, typeof(TestFrameworkV2.AssertFailedException));
    }

    public void AreNotEqualShouldFailWhenFloatDouble()
    {
        static void action() => TestFrameworkV2.Assert.AreNotEqual(100E-2, 100E-2);
        ActionUtility.ActionShouldThrowExceptionOfType(action, typeof(TestFrameworkV2.AssertFailedException));
    }

    public void AreNotEqualShouldFailWhenFloatDoubleWithMessage()
    {
        var ex = ActionUtility.PerformActionAndReturnException(() => TestFrameworkV2.Assert.AreNotEqual(100E-2, 100E-2, "A Message"));
        Assert.IsNotNull(ex);
        StringAssert.Contains(ex.Message, "A Message");
    }

    public void AreNotEqualShouldFailWhenNotEqualFloatWithDelta()
    {
        static void action() => TestFrameworkV2.Assert.AreNotEqual(100E-2, 200E-2, 100E-2);
        ActionUtility.ActionShouldThrowExceptionOfType(action, typeof(TestFrameworkV2.AssertFailedException));
    }

    public void AreEqualShouldFailWhenNotEqualType()
    {
        static void action() => TestFrameworkV2.Assert.AreEqual(null, "string");
        ActionUtility.ActionShouldThrowExceptionOfType(action, typeof(TestFrameworkV2.AssertFailedException));
    }

    public void AreEqualShouldFailWhenNotEqualTypeWithMessage()
    {
        var ex = ActionUtility.PerformActionAndReturnException(() => TestFrameworkV2.Assert.AreEqual(null, "string", "A Message"));
        Assert.IsNotNull(ex);
        StringAssert.Contains(ex.Message, "A Message");
    }

    public void AreEqual_WithTurkishCultureAndIgnoreCase_Throws()
    {
        var expected = "i";
        var actual = "I";
        var turkishCulture = new CultureInfo("tr-TR");

        // In the tr-TR culture, "i" and "I" are not considered equal when doing a case-insensitive comparison.
        var ex = ActionUtility.PerformActionAndReturnException(() => TestFrameworkV2.Assert.AreEqual(expected, actual, true, turkishCulture));
        Assert.IsNotNull(ex);
    }

    public void AreEqual_WithEnglishCultureAndIgnoreCase_DoesNotThrow()
    {
        var expected = "i";
        var actual = "I";
        var englishCulture = new CultureInfo("en-EN");

        // Will ignore case and won't make exeption.
        var ex = ActionUtility.PerformActionAndReturnException(() => TestFrameworkV2.Assert.AreEqual(expected, actual, true, englishCulture));
        Assert.IsNull(ex);
    }

    public void AreEqual_WithEnglishCultureAndDoesNotIgnoreCase_Throws()
    {
        var expected = "i";
        var actual = "I";
        var englishCulture = new CultureInfo("en-EN");

        // Won't ignore case.
        var ex = ActionUtility.PerformActionAndReturnException(() => TestFrameworkV2.Assert.AreEqual(expected, actual, false, englishCulture));
        Assert.IsNotNull(ex);
    }

    public void AreEqual_WithTurkishCultureAndDoesNotIgnoreCase_Throws()
    {
        var expected = "i";
        var actual = "I";
        var turkishCulture = new CultureInfo("tr-TR");

        // Won't ignore case.
        var ex = ActionUtility.PerformActionAndReturnException(() => TestFrameworkV2.Assert.AreEqual(expected, actual, false, turkishCulture));
        Assert.IsNotNull(ex);
    }

    public void AreEqualShouldFailWhenNotEqualStringWithMessage()
    {
        var ex = ActionUtility.PerformActionAndReturnException(() => TestFrameworkV2.Assert.AreEqual("A", "a", "A Message"));
        Assert.IsNotNull(ex);
        StringAssert.Contains(ex.Message, "A Message");
    }

    public void AreEqualShouldFailWhenNotEqualStringAndCaseIgnored()
    {
        static void action() => TestFrameworkV2.Assert.AreEqual("A", "a", false);
        ActionUtility.ActionShouldThrowExceptionOfType(action, typeof(TestFrameworkV2.AssertFailedException));
    }

    public void AreEqualShouldFailWhenNotEqualInt()
    {
        static void action() => TestFrameworkV2.Assert.AreEqual(1, 2);
        ActionUtility.ActionShouldThrowExceptionOfType(action, typeof(TestFrameworkV2.AssertFailedException));
    }

    public void AreEqualShouldFailWhenNotEqualIntWithMessage()
    {
        var ex = ActionUtility.PerformActionAndReturnException(() => TestFrameworkV2.Assert.AreEqual(1, 2, "A Message"));
        Assert.IsNotNull(ex);
        StringAssert.Contains(ex.Message, "A Message");
    }

    public void AreEqualShouldFailWhenNotEqualLong()
    {
        static void action() => TestFrameworkV2.Assert.AreEqual(1L, 2L);
        ActionUtility.ActionShouldThrowExceptionOfType(action, typeof(TestFrameworkV2.AssertFailedException));
    }

    public void AreEqualShouldFailWhenNotEqualLongWithMessage()
    {
        var ex = ActionUtility.PerformActionAndReturnException(() => TestFrameworkV2.Assert.AreEqual(1L, 2L, "A Message"));
        Assert.IsNotNull(ex);
        StringAssert.Contains(ex.Message, "A Message");
    }

    public void AreEqualShouldFailWhenNotEqualLongWithDelta()
    {
        static void action() => TestFrameworkV2.Assert.AreEqual(10L, 20L, 5L);
        ActionUtility.ActionShouldThrowExceptionOfType(action, typeof(TestFrameworkV2.AssertFailedException));
    }

    public void AreEqualShouldFailWhenNotEqualDouble()
    {
        static void action() => TestFrameworkV2.Assert.AreEqual(0.1, 0.2);
        ActionUtility.ActionShouldThrowExceptionOfType(action, typeof(TestFrameworkV2.AssertFailedException));
    }

    public void AreEqualShouldFailWhenNotEqualDoubleWithMessage()
    {
        var ex = ActionUtility.PerformActionAndReturnException(() => TestFrameworkV2.Assert.AreEqual(0.1, 0.2, "A Message"));
        Assert.IsNotNull(ex);
        StringAssert.Contains(ex.Message, "A Message");
    }

    public void AreEqualShouldFailWhenNotEqualDoubleWithDelta()
    {
        static void action() => TestFrameworkV2.Assert.AreEqual(0.1, 0.2, 0.05);
        ActionUtility.ActionShouldThrowExceptionOfType(action, typeof(TestFrameworkV2.AssertFailedException));
    }

    public void AreEqualShouldFailWhenNotEqualDecimal()
    {
        static void action() => TestFrameworkV2.Assert.AreEqual(0.1M, 0.2M);
        ActionUtility.ActionShouldThrowExceptionOfType(action, typeof(TestFrameworkV2.AssertFailedException));
    }
   
    public void AreEqualShouldFailWhenNotEqualDecimalWithMessage()
    {
        var ex = ActionUtility.PerformActionAndReturnException(() => TestFrameworkV2.Assert.AreEqual(0.1M, 0.2M, "A Message"));
        Assert.IsNotNull(ex);
        StringAssert.Contains(ex.Message, "A Message");
    }

    public void AreEqualShouldFailWhenNotEqualDecimalWithDelta()
    {
        static void action() => TestFrameworkV2.Assert.AreEqual(0.1M, 0.2M, 0.05M);
        ActionUtility.ActionShouldThrowExceptionOfType(action, typeof(TestFrameworkV2.AssertFailedException));
    }

    public void AreEqualShouldFailWhenFloatDouble()
    {
        static void action() => TestFrameworkV2.Assert.AreEqual(100E-2, 200E-2);
        ActionUtility.ActionShouldThrowExceptionOfType(action, typeof(TestFrameworkV2.AssertFailedException));
    }
    
    public void AreEqualShouldFailWhenFloatDoubleWithMessage()
    {
        var ex = ActionUtility.PerformActionAndReturnException(() => TestFrameworkV2.Assert.AreEqual(100E-2, 200E-2, "A Message"));
        Assert.IsNotNull(ex);
        StringAssert.Contains(ex.Message, "A Message");
    }
   
    public void AreEqualShouldFailWhenNotEqualFloatWithDelta()
    {
        static void action() => TestFrameworkV2.Assert.AreEqual(100E-2, 200E-2, 50E-2);
        ActionUtility.ActionShouldThrowExceptionOfType(action, typeof(TestFrameworkV2.AssertFailedException));
    }
}
