// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestPlatform.TestFramework.UnitTests;

using System.Globalization;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using global::TestFramework.ForTestingMSTest;

public partial class AssertTests : TestContainer
{
    public void AreNotEqualShouldFailWhenNotEqualType()
    {
        static void action() => Assert.AreNotEqual(null, null);
        var ex = VerifyThrows(action);
        Verify(ex is AssertFailedException);
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
        Verify(ex is AssertFailedException);
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
        Verify(ex is AssertFailedException);
    }

    public void AreNotEqualShouldFailWhenNotEqualInt()
    {
        static void action() => Assert.AreNotEqual(1, 1);
        var ex = VerifyThrows(action);
        Verify(ex is AssertFailedException);
    }

    public void AreNotEqualShouldFailWhenNotEqualIntWithMessage()
    {
        var ex = VerifyThrows(() => Assert.AreNotEqual(1, 1, "A Message"));
        Verify(ex is not null);
        Verify(ex.Message.Contains("A Message"));
    }

    public void AreNotEqualShouldFailWhenNotEqualLong()
    {
        static void action() => Assert.AreNotEqual(1L, 1L);
        var ex = VerifyThrows(action);
        Verify(ex is AssertFailedException);
    }

    public void AreNotEqualShouldFailWhenNotEqualLongWithMessage()
    {
        var ex = VerifyThrows(() => Assert.AreNotEqual(1L, 1L, "A Message"));
        Verify(ex is not null);
        Verify(ex.Message.Contains("A Message"));
    }

    public void AreNotEqualShouldFailWhenNotEqualLongWithDelta()
    {
        static void action() => Assert.AreNotEqual(1L, 2L, 1L);
        var ex = VerifyThrows(action);
        Verify(ex is AssertFailedException);
    }

    public void AreNotEqualShouldFailWhenNotEqualDecimal()
    {
        static void action() => Assert.AreNotEqual(0.1M, 0.1M);
        var ex = VerifyThrows(action);
        Verify(ex is AssertFailedException);
    }

    public void AreNotEqualShouldFailWhenNotEqualDecimalWithMessage()
    {
        var ex = VerifyThrows(() => Assert.AreNotEqual(0.1M, 0.1M, "A Message"));
        Verify(ex is not null);
        Verify(ex.Message.Contains("A Message"));
    }

    public void AreNotEqualShouldFailWhenNotEqualDecimalWithDelta()
    {
        static void action() => Assert.AreNotEqual(0.1M, 0.2M, 0.1M);
        var ex = VerifyThrows(action);
        Verify(ex is AssertFailedException);
    }

    public void AreNotEqualShouldFailWhenNotEqualDouble()
    {
        static void action() => Assert.AreNotEqual(0.1, 0.1);
        var ex = VerifyThrows(action);
        Verify(ex is AssertFailedException);
    }

    public void AreNotEqualShouldFailWhenNotEqualDoubleWithMessage()
    {
        var ex = VerifyThrows(() => Assert.AreNotEqual(0.1, 0.1, "A Message"));
        Verify(ex is not null);
        Verify(ex.Message.Contains("A Message"));
    }

    public void AreNotEqualShouldFailWhenNotEqualDoubleWithDelta()
    {
        static void action() => Assert.AreNotEqual(0.1, 0.2, 0.1);
        var ex = VerifyThrows(action);
        Verify(ex is AssertFailedException);
    }

    public void AreNotEqualShouldFailWhenFloatDouble()
    {
        static void action() => Assert.AreNotEqual(100E-2, 100E-2);
        var ex = VerifyThrows(action);
        Verify(ex is AssertFailedException);
    }

    public void AreNotEqualShouldFailWhenFloatDoubleWithMessage()
    {
        var ex = VerifyThrows(() => Assert.AreNotEqual(100E-2, 100E-2, "A Message"));
        Verify(ex is not null);
        Verify(ex.Message.Contains("A Message"));
    }

    public void AreNotEqualShouldFailWhenNotEqualFloatWithDelta()
    {
        static void action() => Assert.AreNotEqual(100E-2, 200E-2, 100E-2);
        var ex = VerifyThrows(action);
        Verify(ex is AssertFailedException);
    }

    public void AreEqualShouldFailWhenNotEqualType()
    {
        static void action() => Assert.AreEqual(null, "string");
        var ex = VerifyThrows(action);
        Verify(ex is AssertFailedException);
    }

    public void AreEqualShouldFailWhenNotEqualTypeWithMessage()
    {
        var ex = VerifyThrows(() => Assert.AreEqual(null, "string", "A Message"));
        Verify(ex is not null);
        Verify(ex.Message.Contains("A Message"));
    }

    public void AreEqual_WithTurkishCultureAndIgnoreCase_Throws()
    {
        var expected = "i";
        var actual = "I";
        var turkishCulture = new CultureInfo("tr-TR");

        // In the tr-TR culture, "i" and "I" are not considered equal when doing a case-insensitive comparison.
        var ex = VerifyThrows(() => Assert.AreEqual(expected, actual, true, turkishCulture));
        Verify(ex is not null);
    }

    public void AreEqual_WithEnglishCultureAndIgnoreCase_DoesNotThrow()
    {
        var expected = "i";
        var actual = "I";
        var englishCulture = new CultureInfo("en-EN");

        // Will ignore case and won't make exeption.
        Assert.AreEqual(expected, actual, true, englishCulture);
    }

    public void AreEqual_WithEnglishCultureAndDoesNotIgnoreCase_Throws()
    {
        var expected = "i";
        var actual = "I";
        var englishCulture = new CultureInfo("en-EN");

        // Won't ignore case.
        var ex = VerifyThrows(() => Assert.AreEqual(expected, actual, false, englishCulture));
        Verify(ex is not null);
    }

    public void AreEqual_WithTurkishCultureAndDoesNotIgnoreCase_Throws()
    {
        var expected = "i";
        var actual = "I";
        var turkishCulture = new CultureInfo("tr-TR");

        // Won't ignore case.
        var ex = VerifyThrows(() => Assert.AreEqual(expected, actual, false, turkishCulture));
        Verify(ex is not null);
    }

    public void AreEqualShouldFailWhenNotEqualStringWithMessage()
    {
        var ex = VerifyThrows(() => Assert.AreEqual("A", "a", "A Message"));
        Verify(ex is not null);
        Verify(ex.Message.Contains("A Message"));
    }

    public void AreEqualShouldFailWhenNotEqualStringAndCaseIgnored()
    {
        static void action() => Assert.AreEqual("A", "a", false);
        var ex = VerifyThrows(action);
        Verify(ex is AssertFailedException);
    }

    public void AreEqualShouldFailWhenNotEqualInt()
    {
        static void action() => Assert.AreEqual(1, 2);
        var ex = VerifyThrows(action);
        Verify(ex is AssertFailedException);
    }

    public void AreEqualShouldFailWhenNotEqualIntWithMessage()
    {
        var ex = VerifyThrows(() => Assert.AreEqual(1, 2, "A Message"));
        Verify(ex is not null);
        Verify(ex.Message.Contains("A Message"));
    }

    public void AreEqualShouldFailWhenNotEqualLong()
    {
        static void action() => Assert.AreEqual(1L, 2L);
        var ex = VerifyThrows(action);
        Verify(ex is AssertFailedException);
    }

    public void AreEqualShouldFailWhenNotEqualLongWithMessage()
    {
        var ex = VerifyThrows(() => Assert.AreEqual(1L, 2L, "A Message"));
        Verify(ex is not null);
        Verify(ex.Message.Contains("A Message"));
    }

    public void AreEqualShouldFailWhenNotEqualLongWithDelta()
    {
        static void action() => Assert.AreEqual(10L, 20L, 5L);
        var ex = VerifyThrows(action);
        Verify(ex is AssertFailedException);
    }

    public void AreEqualShouldFailWhenNotEqualDouble()
    {
        static void action() => Assert.AreEqual(0.1, 0.2);
        var ex = VerifyThrows(action);
        Verify(ex is AssertFailedException);
    }

    public void AreEqualShouldFailWhenNotEqualDoubleWithMessage()
    {
        var ex = VerifyThrows(() => Assert.AreEqual(0.1, 0.2, "A Message"));
        Verify(ex is not null);
        Verify(ex.Message.Contains("A Message"));
    }

    public void AreEqualShouldFailWhenNotEqualDoubleWithDelta()
    {
        static void action() => Assert.AreEqual(0.1, 0.2, 0.05);
        var ex = VerifyThrows(action);
        Verify(ex is AssertFailedException);
    }

    public void AreEqualShouldFailWhenNotEqualDecimal()
    {
        static void action() => Assert.AreEqual(0.1M, 0.2M);
        var ex = VerifyThrows(action);
        Verify(ex is AssertFailedException);
    }
   
    public void AreEqualShouldFailWhenNotEqualDecimalWithMessage()
    {
        var ex = VerifyThrows(() => Assert.AreEqual(0.1M, 0.2M, "A Message"));
        Verify(ex is not null);
        Verify(ex.Message.Contains("A Message"));
    }

    public void AreEqualShouldFailWhenNotEqualDecimalWithDelta()
    {
        static void action() => Assert.AreEqual(0.1M, 0.2M, 0.05M);
        var ex = VerifyThrows(action);
        Verify(ex is AssertFailedException);
    }

    public void AreEqualShouldFailWhenFloatDouble()
    {
        static void action() => Assert.AreEqual(100E-2, 200E-2);
        var ex = VerifyThrows(action);
        Verify(ex is AssertFailedException);
    }
    
    public void AreEqualShouldFailWhenFloatDoubleWithMessage()
    {
        var ex = VerifyThrows(() => Assert.AreEqual(100E-2, 200E-2, "A Message"));
        Verify(ex is not null);
        Verify(ex.Message.Contains("A Message"));
    }
   
    public void AreEqualShouldFailWhenNotEqualFloatWithDelta()
    {
        static void action() => Assert.AreEqual(100E-2, 200E-2, 50E-2);
        var ex = VerifyThrows(action);
        Verify(ex is AssertFailedException);
    }
}
