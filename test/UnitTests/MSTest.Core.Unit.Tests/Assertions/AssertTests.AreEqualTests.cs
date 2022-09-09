// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestPlatform.TestFramework.UnitTests;

extern alias FrameworkV1;
extern alias FrameworkV2;

using System;
using System.Globalization;
using System.Threading.Tasks;
using MSTestAdapter.TestUtilities;

using Assert = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.Assert;
using StringAssert = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.StringAssert;
using TestClass = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.TestClassAttribute;
using TestFrameworkV2 = FrameworkV2.Microsoft.VisualStudio.TestTools.UnitTesting;
using TestMethod = FrameworkV1::Microsoft.VisualStudio.TestTools.UnitTesting.TestMethodAttribute;

public partial class AssertTests
{
    #region AreNotEqual tests.

    [TestMethod]
    public void AreNotEqualShouldFailWhenNotEqualType()
    {
        static void action() => TestFrameworkV2.Assert.AreNotEqual(null, null);
        ActionUtility.ActionShouldThrowExceptionOfType(action, typeof(TestFrameworkV2.AssertFailedException));
    }

    [TestMethod]
    public void AreNotEqualShouldFailWhenNotEqualTypeWithMessage()
    {
        var ex = ActionUtility.PerformActionAndReturnException(() => TestFrameworkV2.Assert.AreNotEqual(null, null, "A Message"));
        Assert.IsNotNull(ex);
        StringAssert.Contains(ex.Message, "A Message");
    }

    [TestMethod]
    public void AreNotEqualShouldFailWhenNotEqualString()
    {
        static void action() => TestFrameworkV2.Assert.AreNotEqual("A", "A");
        ActionUtility.ActionShouldThrowExceptionOfType(action, typeof(TestFrameworkV2.AssertFailedException));
    }

    [TestMethod]
    public void AreNotEqualShouldFailWhenNotEqualStringWithMessage()
    {
        var ex = ActionUtility.PerformActionAndReturnException(() => TestFrameworkV2.Assert.AreNotEqual("A", "A", "A Message"));
        Assert.IsNotNull(ex);
        StringAssert.Contains(ex.Message, "A Message");
    }

    [TestMethod]
    public void AreNotEqualShouldFailWhenNotEqualStringAndCaseIgnored()
    {
        static void action() => TestFrameworkV2.Assert.AreNotEqual("A", "a", true);
        ActionUtility.ActionShouldThrowExceptionOfType(action, typeof(TestFrameworkV2.AssertFailedException));
    }

    [TestMethod]
    public void AreNotEqualShouldFailWhenNotEqualInt()
    {
        static void action() => TestFrameworkV2.Assert.AreNotEqual(1, 1);
        ActionUtility.ActionShouldThrowExceptionOfType(action, typeof(TestFrameworkV2.AssertFailedException));
    }

    [TestMethod]
    public void AreNotEqualShouldFailWhenNotEqualIntWithMessage()
    {
        var ex = ActionUtility.PerformActionAndReturnException(() => TestFrameworkV2.Assert.AreNotEqual(1, 1, "A Message"));
        Assert.IsNotNull(ex);
        StringAssert.Contains(ex.Message, "A Message");
    }

    [TestMethod]
    public void AreNotEqualShouldFailWhenNotEqualLong()
    {
        static void action() => TestFrameworkV2.Assert.AreNotEqual(1L, 1L);
        ActionUtility.ActionShouldThrowExceptionOfType(action, typeof(TestFrameworkV2.AssertFailedException));
    }

    [TestMethod]
    public void AreNotEqualShouldFailWhenNotEqualLongWithMessage()
    {
        var ex = ActionUtility.PerformActionAndReturnException(() => TestFrameworkV2.Assert.AreNotEqual(1L, 1L, "A Message"));
        Assert.IsNotNull(ex);
        StringAssert.Contains(ex.Message, "A Message");
    }

    [TestMethod]
    public void AreNotEqualShouldFailWhenNotEqualLongWithDelta()
    {
        static void action() => TestFrameworkV2.Assert.AreNotEqual(1L, 2L, 1L);
        ActionUtility.ActionShouldThrowExceptionOfType(action, typeof(TestFrameworkV2.AssertFailedException));
    }

    [TestMethod]
    public void AreNotEqualShouldFailWhenNotEqualDecimal()
    {
        static void action() => TestFrameworkV2.Assert.AreNotEqual(0.1M, 0.1M);
        ActionUtility.ActionShouldThrowExceptionOfType(action, typeof(TestFrameworkV2.AssertFailedException));
    }

    [TestMethod]
    public void AreNotEqualShouldFailWhenNotEqualDecimalWithMessage()
    {
        var ex = ActionUtility.PerformActionAndReturnException(() => TestFrameworkV2.Assert.AreNotEqual(0.1M, 0.1M, "A Message"));
        Assert.IsNotNull(ex);
        StringAssert.Contains(ex.Message, "A Message");
    }

    [TestMethod]
    public void AreNotEqualShouldFailWhenNotEqualDecimalWithDelta()
    {
        static void action() => TestFrameworkV2.Assert.AreNotEqual(0.1M, 0.2M, 0.1M);
        ActionUtility.ActionShouldThrowExceptionOfType(action, typeof(TestFrameworkV2.AssertFailedException));
    }

    [TestMethod]
    public void AreNotEqualShouldFailWhenNotEqualDouble()
    {
        static void action() => TestFrameworkV2.Assert.AreNotEqual(0.1, 0.1);
        ActionUtility.ActionShouldThrowExceptionOfType(action, typeof(TestFrameworkV2.AssertFailedException));
    }

    [TestMethod]
    public void AreNotEqualShouldFailWhenNotEqualDoubleWithMessage()
    {
        var ex = ActionUtility.PerformActionAndReturnException(() => TestFrameworkV2.Assert.AreNotEqual(0.1, 0.1, "A Message"));
        Assert.IsNotNull(ex);
        StringAssert.Contains(ex.Message, "A Message");
    }

    [TestMethod]
    public void AreNotEqualShouldFailWhenNotEqualDoubleWithDelta()
    {
        static void action() => TestFrameworkV2.Assert.AreNotEqual(0.1, 0.2, 0.1);
        ActionUtility.ActionShouldThrowExceptionOfType(action, typeof(TestFrameworkV2.AssertFailedException));
    }

    [TestMethod]
    public void AreNotEqualShouldFailWhenFloatDouble()
    {
        static void action() => TestFrameworkV2.Assert.AreNotEqual(100E-2, 100E-2);
        ActionUtility.ActionShouldThrowExceptionOfType(action, typeof(TestFrameworkV2.AssertFailedException));
    }

    [TestMethod]
    public void AreNotEqualShouldFailWhenFloatDoubleWithMessage()
    {
        var ex = ActionUtility.PerformActionAndReturnException(() => TestFrameworkV2.Assert.AreNotEqual(100E-2, 100E-2, "A Message"));
        Assert.IsNotNull(ex);
        StringAssert.Contains(ex.Message, "A Message");
    }

    [TestMethod]
    public void AreNotEqualShouldFailWhenNotEqualFloatWithDelta()
    {
        static void action() => TestFrameworkV2.Assert.AreNotEqual(100E-2, 200E-2, 100E-2);
        ActionUtility.ActionShouldThrowExceptionOfType(action, typeof(TestFrameworkV2.AssertFailedException));
    }
    #endregion

    #region AreEqual tests.
    [TestMethod]
    public void AreEqualShouldFailWhenNotEqualType()
    {
        static void action() => TestFrameworkV2.Assert.AreEqual(null, "string");
        ActionUtility.ActionShouldThrowExceptionOfType(action, typeof(TestFrameworkV2.AssertFailedException));
    }

    [TestMethod]
    public void AreEqualShouldFailWhenNotEqualTypeWithMessage()
    {
        var ex = ActionUtility.PerformActionAndReturnException(() => TestFrameworkV2.Assert.AreEqual(null, "string", "A Message"));
        Assert.IsNotNull(ex);
        StringAssert.Contains(ex.Message, "A Message");
    }

    [TestMethod]
    public void AreEqual_WithTurkishCultureAndIgnoreCase_Throws()
    {
        var expected = "i";
        var actual = "I";
        var turkishCulture = new CultureInfo("tr-TR");

        // In the tr-TR culture, "i" and "I" are not considered equal when doing a case-insensitive comparison.
        var ex = ActionUtility.PerformActionAndReturnException(() => TestFrameworkV2.Assert.AreEqual(expected, actual, true, turkishCulture));
        Assert.IsNotNull(ex);
    }

    [TestMethod]
    public void AreEqual_WithEnglishCultureAndIgnoreCase_DoesNotThrow()
    {
        var expected = "i";
        var actual = "I";
        var englishCulture = new CultureInfo("en-EN");

        // Will ignore case and won't make exeption.
        var ex = ActionUtility.PerformActionAndReturnException(() => TestFrameworkV2.Assert.AreEqual(expected, actual, true, englishCulture));
        Assert.IsNull(ex);
    }

    [TestMethod]
    public void AreEqual_WithEnglishCultureAndDoesNotIgnoreCase_Throws()
    {
        var expected = "i";
        var actual = "I";
        var englishCulture = new CultureInfo("en-EN");

        // Won't ignore case.
        var ex = ActionUtility.PerformActionAndReturnException(() => TestFrameworkV2.Assert.AreEqual(expected, actual, false, englishCulture));
        Assert.IsNotNull(ex);
    }

    [TestMethod]
    public void AreEqual_WithTurkishCultureAndDoesNotIgnoreCase_Throws()
    {
        var expected = "i";
        var actual = "I";
        var turkishCulture = new CultureInfo("tr-TR");

        // Won't ignore case.
        var ex = ActionUtility.PerformActionAndReturnException(() => TestFrameworkV2.Assert.AreEqual(expected, actual, false, turkishCulture));
        Assert.IsNotNull(ex);
    }

    [TestMethod]
    public void AreEqualShouldFailWhenNotEqualStringWithMessage()
    {
        var ex = ActionUtility.PerformActionAndReturnException(() => TestFrameworkV2.Assert.AreEqual("A", "a", "A Message"));
        Assert.IsNotNull(ex);
        StringAssert.Contains(ex.Message, "A Message");
    }

    [TestMethod]
    public void AreEqualShouldFailWhenNotEqualStringAndCaseIgnored()
    {
        static void action() => TestFrameworkV2.Assert.AreEqual("A", "a", false);
        ActionUtility.ActionShouldThrowExceptionOfType(action, typeof(TestFrameworkV2.AssertFailedException));
    }

    [TestMethod]
    public void AreEqualShouldFailWhenNotEqualInt()
    {
        static void action() => TestFrameworkV2.Assert.AreEqual(1, 2);
        ActionUtility.ActionShouldThrowExceptionOfType(action, typeof(TestFrameworkV2.AssertFailedException));
    }

    [TestMethod]
    public void AreEqualShouldFailWhenNotEqualIntWithMessage()
    {
        var ex = ActionUtility.PerformActionAndReturnException(() => TestFrameworkV2.Assert.AreEqual(1, 2, "A Message"));
        Assert.IsNotNull(ex);
        StringAssert.Contains(ex.Message, "A Message");
    }

    [TestMethod]
    public void AreEqualShouldFailWhenNotEqualLong()
    {
        static void action() => TestFrameworkV2.Assert.AreEqual(1L, 2L);
        ActionUtility.ActionShouldThrowExceptionOfType(action, typeof(TestFrameworkV2.AssertFailedException));
    }

    [TestMethod]
    public void AreEqualShouldFailWhenNotEqualLongWithMessage()
    {
        var ex = ActionUtility.PerformActionAndReturnException(() => TestFrameworkV2.Assert.AreEqual(1L, 2L, "A Message"));
        Assert.IsNotNull(ex);
        StringAssert.Contains(ex.Message, "A Message");
    }

    [TestMethod]
    public void AreEqualShouldFailWhenNotEqualLongWithDelta()
    {
        static void action() => TestFrameworkV2.Assert.AreEqual(10L, 20L, 5L);
        ActionUtility.ActionShouldThrowExceptionOfType(action, typeof(TestFrameworkV2.AssertFailedException));
    }

    [TestMethod]
    public void AreEqualShouldFailWhenNotEqualDouble()
    {
        static void action() => TestFrameworkV2.Assert.AreEqual(0.1, 0.2);
        ActionUtility.ActionShouldThrowExceptionOfType(action, typeof(TestFrameworkV2.AssertFailedException));
    }

    [TestMethod]
    public void AreEqualShouldFailWhenNotEqualDoubleWithMessage()
    {
        var ex = ActionUtility.PerformActionAndReturnException(() => TestFrameworkV2.Assert.AreEqual(0.1, 0.2, "A Message"));
        Assert.IsNotNull(ex);
        StringAssert.Contains(ex.Message, "A Message");
    }

    [TestMethod]
    public void AreEqualShouldFailWhenNotEqualDoubleWithDelta()
    {
        static void action() => TestFrameworkV2.Assert.AreEqual(0.1, 0.2, 0.05);
        ActionUtility.ActionShouldThrowExceptionOfType(action, typeof(TestFrameworkV2.AssertFailedException));
    }

    [TestMethod]
    public void AreEqualShouldFailWhenNotEqualDecimal()
    {
        static void action() => TestFrameworkV2.Assert.AreEqual(0.1M, 0.2M);
        ActionUtility.ActionShouldThrowExceptionOfType(action, typeof(TestFrameworkV2.AssertFailedException));
    }

    [TestMethod]
    public void AreEqualShouldFailWhenNotEqualDecimalWithMessage()
    {
        var ex = ActionUtility.PerformActionAndReturnException(() => TestFrameworkV2.Assert.AreEqual(0.1M, 0.2M, "A Message"));
        Assert.IsNotNull(ex);
        StringAssert.Contains(ex.Message, "A Message");
    }

    [TestMethod]
    public void AreEqualShouldFailWhenNotEqualDecimalWithDelta()
    {
        static void action() => TestFrameworkV2.Assert.AreEqual(0.1M, 0.2M, 0.05M);
        ActionUtility.ActionShouldThrowExceptionOfType(action, typeof(TestFrameworkV2.AssertFailedException));
    }

    [TestMethod]
    public void AreEqualShouldFailWhenFloatDouble()
    {
        static void action() => TestFrameworkV2.Assert.AreEqual(100E-2, 200E-2);
        ActionUtility.ActionShouldThrowExceptionOfType(action, typeof(TestFrameworkV2.AssertFailedException));
    }

    [TestMethod]
    public void AreEqualShouldFailWhenFloatDoubleWithMessage()
    {
        var ex = ActionUtility.PerformActionAndReturnException(() => TestFrameworkV2.Assert.AreEqual(100E-2, 200E-2, "A Message"));
        Assert.IsNotNull(ex);
        StringAssert.Contains(ex.Message, "A Message");
    }

    [TestMethod]
    public void AreEqualShouldFailWhenNotEqualFloatWithDelta()
    {
        static void action() => TestFrameworkV2.Assert.AreEqual(100E-2, 200E-2, 50E-2);
        ActionUtility.ActionShouldThrowExceptionOfType(action, typeof(TestFrameworkV2.AssertFailedException));
    }

    #endregion
}
