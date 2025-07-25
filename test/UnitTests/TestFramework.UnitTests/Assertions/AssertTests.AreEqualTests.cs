// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using TestFramework.ForTestingMSTest;

namespace Microsoft.VisualStudio.TestPlatform.TestFramework.UnitTests;

public partial class AssertTests : TestContainer
{
    public void AreNotEqualShouldFailWhenNotEqualType() =>
        VerifyThrows<AssertFailedException>(() => Assert.AreNotEqual(1, 1));

    public void AreNotEqualShouldFailWhenNotEqualTypeWithMessage()
    {
        Exception ex = VerifyThrows(() => Assert.AreNotEqual(1, 1, "A Message"));
        Verify(ex.Message.Contains("A Message"));
    }

    public void AreNotEqualShouldFailWhenNotEqualString() =>
        VerifyThrows<AssertFailedException>(() => Assert.AreNotEqual("A", "A"));

    public void AreNotEqualShouldFailWhenNotEqualStringWithMessage()
    {
        Exception ex = VerifyThrows(() => Assert.AreNotEqual("A", "A", "A Message"));
        Verify(ex.Message.Contains("A Message"));
    }

    [SuppressMessage("Globalization", "CA1304:Specify CultureInfo", Justification = "Testing the API without the culture")]
    public void AreNotEqualShouldFailWhenNotEqualStringAndCaseIgnored() =>
        VerifyThrows<AssertFailedException>(() => Assert.AreNotEqual("A", "a", true));

    public void AreNotEqualShouldFailWhenNotEqualInt() =>
        VerifyThrows<AssertFailedException>(() => Assert.AreNotEqual(1, 1));

    public void AreNotEqualShouldFailWhenNotEqualIntWithMessage()
    {
        Exception ex = VerifyThrows(() => Assert.AreNotEqual(1, 1, "A Message"));
        Verify(ex.Message.Contains("A Message"));
    }

    public void AreNotEqualShouldFailWhenNotEqualLong() =>
        VerifyThrows<AssertFailedException>(() => Assert.AreNotEqual(1L, 1L));

    public void AreNotEqualShouldFailWhenNotEqualLongWithMessage()
    {
        Exception ex = VerifyThrows(() => Assert.AreNotEqual(1L, 1L, "A Message"));
        Verify(ex.Message.Contains("A Message"));
    }

    public void AreNotEqualShouldFailWhenNotEqualLongWithDelta() =>
        VerifyThrows<AssertFailedException>(() => Assert.AreNotEqual(1L, 2L, 1L));

    public void AreNotEqualShouldFailWhenNotEqualDecimal() =>
        VerifyThrows<AssertFailedException>(() => Assert.AreNotEqual(0.1M, 0.1M));

    public void AreNotEqualShouldFailWhenNotEqualDecimalWithMessage()
    {
        Exception ex = VerifyThrows(() => Assert.AreNotEqual(0.1M, 0.1M, "A Message"));
        Verify(ex.Message.Contains("A Message"));
    }

    public void AreNotEqualShouldFailWhenNotEqualDecimalWithDelta() =>
        VerifyThrows<AssertFailedException>(() => Assert.AreNotEqual(0.1M, 0.2M, 0.1M));

    public void AreNotEqualShouldFailWhenNotEqualDouble() =>
        VerifyThrows<AssertFailedException>(() => Assert.AreNotEqual(0.1, 0.1));

    public void AreNotEqualShouldFailWhenNotEqualDoubleWithMessage()
    {
        Exception ex = VerifyThrows(() => Assert.AreNotEqual(0.1, 0.1, "A Message"));
        Verify(ex.Message.Contains("A Message"));
    }

    public void AreNotEqualShouldFailWhenNotEqualDoubleWithDelta() =>
        VerifyThrows<AssertFailedException>(() => Assert.AreNotEqual(0.1, 0.2, 0.1));

    public void AreNotEqualShouldFailWhenFloatDouble() =>
        VerifyThrows<AssertFailedException>(() => Assert.AreNotEqual(100E-2, 100E-2));

    public void AreNotEqualShouldFailWhenFloatDoubleWithMessage()
    {
        Exception ex = VerifyThrows(() => Assert.AreNotEqual(100E-2, 100E-2, "A Message"));
        Verify(ex.Message.Contains("A Message"));
    }

    public void AreNotEqualShouldFailWhenNotEqualFloatWithDelta() =>
        VerifyThrows<AssertFailedException>(() => Assert.AreNotEqual(100E-2, 200E-2, 100E-2));

    public void AreEqualShouldFailWhenNotEqualType() =>
        VerifyThrows<AssertFailedException>(() => Assert.AreEqual(null, "string"));

    public void AreEqualShouldFailWhenNotEqualTypeWithMessage()
    {
        Exception ex = VerifyThrows(() => Assert.AreEqual(null, "string", "A Message"));
        Verify(ex.Message.Contains("A Message"));
    }

    public void AreEqual_WithTurkishCultureAndIgnoreCase_Throws()
    {
        string expected = "i";
        string actual = "I";
        var turkishCulture = new CultureInfo("tr-TR");

        // In the tr-TR culture, "i" and "I" are not considered equal when doing a case-insensitive comparison.
        VerifyThrows(() => Assert.AreEqual(expected, actual, true, turkishCulture));
    }

    public void AreEqual_WithEnglishCultureAndIgnoreCase_DoesNotThrow()
    {
        string expected = "i";
        string actual = "I";
        var englishCulture = new CultureInfo("en-EN");

        // Will ignore case and won't make exception.
        Assert.AreEqual(expected, actual, true, englishCulture);
    }

    public void AreEqual_WithEnglishCultureAndDoesNotIgnoreCase_Throws()
    {
        string expected = "i";
        string actual = "I";
        var englishCulture = new CultureInfo("en-EN");

        // Won't ignore case.
        Exception ex = VerifyThrows(() => Assert.AreEqual(expected, actual, false, englishCulture));
        Verify(ex.Message == "Assert.AreEqual failed. Expected:<i>. Case is different for actual value:<I>. ");
    }

    public void AreEqual_WithTurkishCultureAndDoesNotIgnoreCase_Throws()
    {
        string expected = "i";
        string actual = "I";
        var turkishCulture = new CultureInfo("tr-TR");

        // Won't ignore case.
        VerifyThrows(() => Assert.AreEqual(expected, actual, false, turkishCulture));
    }

    public void AreEqualShouldFailWhenNotEqualStringWithMessage()
    {
        Exception ex = VerifyThrows(() => Assert.AreEqual("A", "a", "A Message"));
        Verify(ex.Message.Contains("A Message"));
    }

    [SuppressMessage("Globalization", "CA1304:Specify CultureInfo", Justification = "Testing the API without the culture")]
    public void AreEqualShouldFailWhenNotEqualStringAndCaseIgnored() =>
        VerifyThrows<AssertFailedException>(() => Assert.AreEqual("A", "a", false));

    public void AreEqualShouldFailWhenNotEqualInt() =>
        VerifyThrows<AssertFailedException>(() => Assert.AreEqual(1, 2));

    public void AreEqualShouldFailWhenNotEqualIntWithMessage()
    {
        Exception ex = VerifyThrows(() => Assert.AreEqual(1, 2, "A Message"));
        Verify(ex.Message.Contains("A Message"));
    }

    public void AreEqualShouldFailWhenNotEqualLong() =>
        VerifyThrows<AssertFailedException>(() => Assert.AreEqual(1L, 2L));

    public void AreEqualShouldFailWhenNotEqualLongWithMessage()
    {
        Exception ex = VerifyThrows(() => Assert.AreEqual(1L, 2L, "A Message"));
        Verify(ex.Message.Contains("A Message"));
    }

    public void AreEqualShouldFailWhenNotEqualLongWithDelta() =>
        VerifyThrows<AssertFailedException>(() => Assert.AreEqual(10L, 20L, 5L));

    public void AreEqualShouldFailWhenNotEqualDouble() =>
        VerifyThrows<AssertFailedException>(() => Assert.AreEqual(0.1, 0.2));

    public void AreEqualShouldFailWhenNotEqualDoubleWithMessage()
    {
        Exception ex = VerifyThrows(() => Assert.AreEqual(0.1, 0.2, "A Message"));
        Verify(ex.Message.Contains("A Message"));
    }

    public void AreEqualShouldFailWhenNotEqualDoubleWithDelta() =>
        VerifyThrows<AssertFailedException>(() => Assert.AreEqual(0.1, 0.2, 0.05));

    public void AreEqualShouldFailWhenNotEqualDecimal()
    {
        static void Action() => Assert.AreEqual(0.1M, 0.2M);
        VerifyThrows<AssertFailedException>(Action);
    }

    public void AreEqualShouldFailWhenNotEqualDecimalWithMessage()
    {
        Exception ex = VerifyThrows(() => Assert.AreEqual(0.1M, 0.2M, "A Message"));
        Verify(ex.Message.Contains("A Message"));
    }

    public void AreEqualShouldFailWhenNotEqualDecimalWithDelta() =>
        VerifyThrows<AssertFailedException>(() => Assert.AreEqual(0.1M, 0.2M, 0.05M));

    public void AreEqualShouldFailWhenFloatDouble() =>
        VerifyThrows<AssertFailedException>(() => Assert.AreEqual(100E-2, 200E-2));

    public void AreEqualShouldFailWhenFloatDoubleWithMessage()
    {
        Exception ex = VerifyThrows(() => Assert.AreEqual(100E-2, 200E-2, "A Message"));
        Verify(ex.Message.Contains("A Message"));
    }

    public void AreEqualShouldFailWhenNotEqualFloatWithDelta() =>
        VerifyThrows<AssertFailedException>(() => Assert.AreEqual(100E-2, 200E-2, 50E-2));

    public void AreEqualTwoObjectsShouldFail() =>
        VerifyThrows<AssertFailedException>(() => Assert.AreEqual(new object(), new object()));

    public void AreEqualTwoObjectsDifferentTypeShouldFail()
    {
        AssertFailedException ex = VerifyThrows<AssertFailedException>(() => Assert.AreEqual(new object(), 1));
        Verify(ex.Message.Contains("Assert.AreEqual failed. Expected:<System.Object (System.Object)>. Actual:<1 (System.Int32)>."));
    }

    public void AreEqualWithTypeOverridingEqualsShouldWork()
    {
        var a = new TypeOverridesEquals();
        var b = new TypeOverridesEquals();
        Assert.AreEqual(a, b);
    }

    public void AreEqualWithTypeImplementingIEquatableShouldWork()
    {
        var a = new EquatableType();
        var b = new EquatableType();
        Assert.AreEqual(a, b);
    }

    public void AreEqualWithTypeOverridingEqualsUsingCustomerComparerShouldFail()
    {
        static void Action()
        {
            var a = new TypeOverridesEquals();
            var b = new TypeOverridesEquals();
            Assert.AreEqual(a, b, new TypeOverridesEqualsEqualityComparer());
        }

        VerifyThrows<AssertFailedException>(Action);
    }

    public void AreEqualUsingCustomIEquatable()
    {
        var instanceOfA = new A { Id = "SomeId" };
        var instanceOfB = new B { Id = "SomeId" };

        // This call works because B implements IEquatable<A>
        Assert.AreEqual(instanceOfA, instanceOfB);

        // This one doesn't work
        VerifyThrows(() => Assert.AreEqual(instanceOfB, instanceOfA));
    }

#pragma warning disable IDE0004

    // IDE0004: at least on param needs to be cast to dynamic so it is more readable if both are cast to dynamic
    public void AreEqualUsingDynamicsDoesNotFail()
    {
        // Assert.AreEqual<dynamic>((dynamic?)null, (dynamic?)null);
        // Assert.AreEqual<dynamic>((dynamic)1, (dynamic)1);
        // Assert.AreEqual<dynamic>((dynamic)"a", (dynamic)"a");
        // Assert.AreEqual<dynamic>((dynamic)'a', (dynamic)'a');
    }

#pragma warning restore IDE0004

    public void GenericAreEqual_InterpolatedString_EqualValues_ShouldPass()
    {
        DummyClassTrackingToStringCalls o = new();
        Assert.AreEqual(o, o, $"User-provided message: {o}");
        Verify(!o.WasToStringCalled);
    }

    public async Task GenericAreEqual_InterpolatedString_DifferentValues_ShouldFail()
    {
        DummyClassTrackingToStringCalls o = new();
        DateTime dateTime = DateTime.Now;
        Exception ex = await VerifyThrowsAsync(async () => Assert.AreEqual(0, 1, $"User-provided message. {o}, {o,35}, {await GetHelloStringAsync()}, {new DummyIFormattable()}, {dateTime:tt}, {dateTime,5:tt}"));
        Verify(ex.Message == $"Assert.AreEqual failed. Expected:<0>. Actual:<1>. User-provided message. DummyClassTrackingToStringCalls,     DummyClassTrackingToStringCalls, Hello, DummyIFormattable.ToString(), {string.Format(null, "{0:tt}", dateTime)}, {string.Format(null, "{0,5:tt}", dateTime)}");
        Verify(o.WasToStringCalled);
    }

    public void GenericAreNotEqual_InterpolatedString_DifferentValues_ShouldPass()
    {
        DummyClassTrackingToStringCalls o = new();
        Assert.AreNotEqual(0, 1, $"User-provided message: {o}");
        Verify(!o.WasToStringCalled);
    }

    public async Task GenericAreNotEqual_InterpolatedString_SameValues_ShouldFail()
    {
        DummyClassTrackingToStringCalls o = new();
        DateTime dateTime = DateTime.Now;
        Exception ex = await VerifyThrowsAsync(async () => Assert.AreNotEqual(0, 0, $"User-provided message. {o}, {o,35}, {await GetHelloStringAsync()}, {new DummyIFormattable()}, {dateTime:tt}, {dateTime,5:tt}"));
        Verify(ex.Message == $"Assert.AreNotEqual failed. Expected any value except:<0>. Actual:<0>. User-provided message. DummyClassTrackingToStringCalls,     DummyClassTrackingToStringCalls, Hello, DummyIFormattable.ToString(), {string.Format(null, "{0:tt}", dateTime)}, {string.Format(null, "{0,5:tt}", dateTime)}");
        Verify(o.WasToStringCalled);
    }

    public void FloatAreEqual_InterpolatedString_EqualValues_ShouldPass()
    {
        DummyClassTrackingToStringCalls o = new();
        Assert.AreEqual(1.0f, 1.1f, delta: 0.2f, $"User-provided message: {o}");
        Verify(!o.WasToStringCalled);
    }

    public async Task FloatAreEqual_InterpolatedString_DifferentValues_ShouldFail()
    {
        DummyClassTrackingToStringCalls o = new();
        DateTime dateTime = DateTime.Now;
        Exception ex = await VerifyThrowsAsync(async () => Assert.AreEqual(1.0f, 1.1f, 0.001f, $"User-provided message. {o}, {o,35}, {await GetHelloStringAsync()}, {new DummyIFormattable()}, {dateTime:tt}, {dateTime,5:tt}"));
        Verify(ex.Message == $"Assert.AreEqual failed. Expected a difference no greater than <0.001> between expected value <1> and actual value <1.1>. User-provided message. DummyClassTrackingToStringCalls,     DummyClassTrackingToStringCalls, Hello, DummyIFormattable.ToString(), {string.Format(null, "{0:tt}", dateTime)}, {string.Format(null, "{0,5:tt}", dateTime)}");
        Verify(o.WasToStringCalled);
    }

    public void FloatAreNotEqual_InterpolatedString_DifferentValues_ShouldPass()
    {
        DummyClassTrackingToStringCalls o = new();
        Assert.AreNotEqual(1.0f, 1.1f, 0.001f, $"User-provided message: {o}");
        Verify(!o.WasToStringCalled);
    }

    public async Task FloatAreNotEqual_InterpolatedString_SameValues_ShouldFail()
    {
        DummyClassTrackingToStringCalls o = new();
        DateTime dateTime = DateTime.Now;
        Exception ex = await VerifyThrowsAsync(async () => Assert.AreNotEqual(1.0f, 1.1f, 0.2f, $"User-provided message. {o}, {o,35}, {await GetHelloStringAsync()}, {new DummyIFormattable()}, {dateTime:tt}, {dateTime,5:tt}"));
        Verify(ex.Message == $"Assert.AreNotEqual failed. Expected a difference greater than <0.2> between expected value <1> and actual value <1.1>. User-provided message. DummyClassTrackingToStringCalls,     DummyClassTrackingToStringCalls, Hello, DummyIFormattable.ToString(), {string.Format(null, "{0:tt}", dateTime)}, {string.Format(null, "{0,5:tt}", dateTime)}");
        Verify(o.WasToStringCalled);
    }

    public void DecimalAreEqual_InterpolatedString_EqualValues_ShouldPass()
    {
        DummyClassTrackingToStringCalls o = new();
        Assert.AreEqual(1.0m, 1.1m, delta: 0.2m, $"User-provided message: {o}");
        Verify(!o.WasToStringCalled);
    }

    public async Task DecimalAreEqual_InterpolatedString_DifferentValues_ShouldFail()
    {
        DummyClassTrackingToStringCalls o = new();
        DateTime dateTime = DateTime.Now;
        Exception ex = await VerifyThrowsAsync(async () => Assert.AreEqual(1.0m, 1.1m, 0.001m, $"User-provided message. {o}, {o,35}, {await GetHelloStringAsync()}, {new DummyIFormattable()}, {dateTime:tt}, {dateTime,5:tt}"));
        Verify(ex.Message == $"Assert.AreEqual failed. Expected a difference no greater than <0.001> between expected value <1.0> and actual value <1.1>. User-provided message. DummyClassTrackingToStringCalls,     DummyClassTrackingToStringCalls, Hello, DummyIFormattable.ToString(), {string.Format(null, "{0:tt}", dateTime)}, {string.Format(null, "{0,5:tt}", dateTime)}");
        Verify(o.WasToStringCalled);
    }

    public void DecimalAreNotEqual_InterpolatedString_DifferentValues_ShouldPass()
    {
        DummyClassTrackingToStringCalls o = new();
        Assert.AreNotEqual(1.0m, 1.1m, 0.001m, $"User-provided message: {o}");
        Verify(!o.WasToStringCalled);
    }

    public async Task DecimalAreNotEqual_InterpolatedString_SameValues_ShouldFail()
    {
        DummyClassTrackingToStringCalls o = new();
        DateTime dateTime = DateTime.Now;
        Exception ex = await VerifyThrowsAsync(async () => Assert.AreNotEqual(1.0m, 1.1m, 0.2m, $"User-provided message. {o}, {o,35}, {await GetHelloStringAsync()}, {new DummyIFormattable()}, {dateTime:tt}, {dateTime,5:tt}"));
        Verify(ex.Message == $"Assert.AreNotEqual failed. Expected a difference greater than <0.2> between expected value <1.0> and actual value <1.1>. User-provided message. DummyClassTrackingToStringCalls,     DummyClassTrackingToStringCalls, Hello, DummyIFormattable.ToString(), {string.Format(null, "{0:tt}", dateTime)}, {string.Format(null, "{0,5:tt}", dateTime)}");
        Verify(o.WasToStringCalled);
    }

    public void LongAreEqual_InterpolatedString_EqualValues_ShouldPass()
    {
        DummyClassTrackingToStringCalls o = new();
        Assert.AreEqual(1L, 2L, delta: 1L, $"User-provided message: {o}");
        Verify(!o.WasToStringCalled);
    }

    public async Task LongAreEqual_InterpolatedString_DifferentValues_ShouldFail()
    {
        DummyClassTrackingToStringCalls o = new();
        DateTime dateTime = DateTime.Now;
        Exception ex = await VerifyThrowsAsync(async () => Assert.AreEqual(1L, 2L, 0L, $"User-provided message. {o}, {o,35}, {await GetHelloStringAsync()}, {new DummyIFormattable()}, {dateTime:tt}, {dateTime,5:tt}"));
        Verify(ex.Message == $"Assert.AreEqual failed. Expected a difference no greater than <0> between expected value <1> and actual value <2>. User-provided message. DummyClassTrackingToStringCalls,     DummyClassTrackingToStringCalls, Hello, DummyIFormattable.ToString(), {string.Format(null, "{0:tt}", dateTime)}, {string.Format(null, "{0,5:tt}", dateTime)}");
        Verify(o.WasToStringCalled);
    }

    public void LongAreNotEqual_InterpolatedString_DifferentValues_ShouldPass()
    {
        DummyClassTrackingToStringCalls o = new();
        Assert.AreNotEqual(1L, 2L, 0L, $"User-provided message: {o}");
        Verify(!o.WasToStringCalled);
    }

    public async Task LongAreNotEqual_InterpolatedString_SameValues_ShouldFail()
    {
        DummyClassTrackingToStringCalls o = new();
        DateTime dateTime = DateTime.Now;
        Exception ex = await VerifyThrowsAsync(async () => Assert.AreNotEqual(1L, 2L, 1L, $"User-provided message. {o}, {o,35}, {await GetHelloStringAsync()}, {new DummyIFormattable()}, {dateTime:tt}, {dateTime,5:tt}"));
        Verify(ex.Message == $"Assert.AreNotEqual failed. Expected a difference greater than <1> between expected value <1> and actual value <2>. User-provided message. DummyClassTrackingToStringCalls,     DummyClassTrackingToStringCalls, Hello, DummyIFormattable.ToString(), {string.Format(null, "{0:tt}", dateTime)}, {string.Format(null, "{0,5:tt}", dateTime)}");
        Verify(o.WasToStringCalled);
    }

    public void DoubleAreEqual_InterpolatedString_EqualValues_ShouldPass()
    {
        DummyClassTrackingToStringCalls o = new();
        Assert.AreEqual(1.0d, 1.1d, delta: 0.2d, $"User-provided message: {o}");
        Verify(!o.WasToStringCalled);
    }

    public async Task DoubleAreEqual_InterpolatedString_DifferentValues_ShouldFail()
    {
        DummyClassTrackingToStringCalls o = new();
        DateTime dateTime = DateTime.Now;
        Exception ex = await VerifyThrowsAsync(async () => Assert.AreEqual(1.0d, 1.1d, 0.001d, $"User-provided message. {o}, {o,35}, {await GetHelloStringAsync()}, {new DummyIFormattable()}, {dateTime:tt}, {dateTime,5:tt}"));
        Verify(ex.Message == $"Assert.AreEqual failed. Expected a difference no greater than <0.001> between expected value <1> and actual value <1.1>. User-provided message. DummyClassTrackingToStringCalls,     DummyClassTrackingToStringCalls, Hello, DummyIFormattable.ToString(), {string.Format(null, "{0:tt}", dateTime)}, {string.Format(null, "{0,5:tt}", dateTime)}");
        Verify(o.WasToStringCalled);
    }

    public void DoubleAreNotEqual_InterpolatedString_DifferentValues_ShouldPass()
    {
        DummyClassTrackingToStringCalls o = new();
        Assert.AreNotEqual(1.0d, 1.1d, 0.001d, $"User-provided message: {o}");
        Verify(!o.WasToStringCalled);
    }

    public async Task DoubleAreNotEqual_InterpolatedString_SameValues_ShouldFail()
    {
        DummyClassTrackingToStringCalls o = new();
        DateTime dateTime = DateTime.Now;
        Exception ex = await VerifyThrowsAsync(async () => Assert.AreNotEqual(1.0d, 1.1d, 0.2d, $"User-provided message. {o}, {o,35}, {await GetHelloStringAsync()}, {new DummyIFormattable()}, {dateTime:tt}, {dateTime,5:tt}"));
        Verify(ex.Message == $"Assert.AreNotEqual failed. Expected a difference greater than <0.2> between expected value <1> and actual value <1.1>. User-provided message. DummyClassTrackingToStringCalls,     DummyClassTrackingToStringCalls, Hello, DummyIFormattable.ToString(), {string.Format(null, "{0:tt}", dateTime)}, {string.Format(null, "{0,5:tt}", dateTime)}");
        Verify(o.WasToStringCalled);
    }

    public void FloatAreEqual_ExpectedIsNumeric_ActualIsNumeric_ExpectedAndActualNotEquals_DeltaIsNaN_ShouldFail()
    {
        Exception ex = VerifyThrows(() => Assert.AreEqual(1.0f, 2.0f, float.NaN));
        Verify(ex.Message is """
            Specified argument was out of the range of valid values.
            Parameter name: delta
            """ or "Specified argument was out of the range of valid values. (Parameter 'delta')");
    }

    public void FloatAreEqual_ExpectedIsNumeric_ActualIsNumeric_ExpectedAndActualEquals_DeltaIsNaN_ShouldFail()
    {
        Exception ex = VerifyThrows(() => Assert.AreEqual(1.0f, 1.0f, float.NaN));
        Verify(ex.Message is """
            Specified argument was out of the range of valid values.
            Parameter name: delta
            """ or "Specified argument was out of the range of valid values. (Parameter 'delta')");
    }

    public void FloatAreEqual_ExpectedIsNaN_ActualIsNumeric_DeltaIsNaN_ShouldFail()
    {
        Exception ex = VerifyThrows(() => Assert.AreEqual(float.NaN, 1.0f, float.NaN));
        Verify(ex.Message is """
            Specified argument was out of the range of valid values.
            Parameter name: delta
            """ or "Specified argument was out of the range of valid values. (Parameter 'delta')");
    }

    public void FloatAreEqual_ExpectedIsNaN_ActualIsNaN_DeltaIsNaN_ShouldFail()
    {
        Exception ex = VerifyThrows(() => Assert.AreEqual(float.NaN, float.NaN, float.NaN));
        Verify(ex.Message is """
            Specified argument was out of the range of valid values.
            Parameter name: delta
            """ or "Specified argument was out of the range of valid values. (Parameter 'delta')");
    }

    public void FloatAreEqual_ExpectedIsNumeric_ActualIsNaN_DeltaIsNaN_ShouldFail()
    {
        Exception ex = VerifyThrows(() => Assert.AreEqual(1.0f, float.NaN, float.NaN));
        Verify(ex.Message is """
            Specified argument was out of the range of valid values.
            Parameter name: delta
            """ or "Specified argument was out of the range of valid values. (Parameter 'delta')");
    }

    public void FloatAreEqual_ExpectedIsNumeric_ActualIsNumeric_ExpectedAndActualNotEquals_DeltaIsNegative_ShouldFail()
    {
        Exception ex = VerifyThrows(() => Assert.AreEqual(1.0f, 2.0f, -1.0f));
        Verify(ex.Message is """
            Specified argument was out of the range of valid values.
            Parameter name: delta
            """ or "Specified argument was out of the range of valid values. (Parameter 'delta')");
    }

    public void FloatAreEqual_ExpectedIsNumeric_ActualIsNumeric_ExpectedAndActualEquals_DeltaIsNegative_ShouldFail()
    {
        Exception ex = VerifyThrows(() => Assert.AreEqual(1.0f, 1.0f, -1.0f));
        Verify(ex.Message is """
            Specified argument was out of the range of valid values.
            Parameter name: delta
            """ or "Specified argument was out of the range of valid values. (Parameter 'delta')");
    }

    public void FloatAreEqual_ExpectedIsNaN_ActualIsNumeric_DeltaIsNegative_ShouldFail()
    {
        Exception ex = VerifyThrows(() => Assert.AreEqual(float.NaN, 1.0f, -1.0f));
        Verify(ex.Message is """
            Specified argument was out of the range of valid values.
            Parameter name: delta
            """ or "Specified argument was out of the range of valid values. (Parameter 'delta')");
    }

    public void FloatAreEqual_ExpectedIsNaN_ActualIsNaN_DeltaIsNegative_ShouldFail()
    {
        Exception ex = VerifyThrows(() => Assert.AreEqual(float.NaN, float.NaN, -1.0f));
        Verify(ex.Message is """
            Specified argument was out of the range of valid values.
            Parameter name: delta
            """ or "Specified argument was out of the range of valid values. (Parameter 'delta')");
    }

    public void FloatAreEqual_ExpectedIsNumeric_ActualIsNaN_DeltaIsNegative_ShouldFail()
    {
        Exception ex = VerifyThrows(() => Assert.AreEqual(1.0f, float.NaN, -1.0f));
        Verify(ex.Message is """
            Specified argument was out of the range of valid values.
            Parameter name: delta
            """ or "Specified argument was out of the range of valid values. (Parameter 'delta')");
    }

    public void FloatAreEqual_ExpectedIsNumeric_ActualIsNumeric_ExpectedAndActualNotEquals_DeltaIsNegativeInf_ShouldFail()
    {
        Exception ex = VerifyThrows(() => Assert.AreEqual(1.0f, 2.0f, float.NegativeInfinity));
        Verify(ex.Message is """
            Specified argument was out of the range of valid values.
            Parameter name: delta
            """ or "Specified argument was out of the range of valid values. (Parameter 'delta')");
    }

    public void FloatAreEqual_ExpectedIsNumeric_ActualIsNumeric_ExpectedAndActualEquals_DeltaIsNegativeInf_ShouldFail()
    {
        Exception ex = VerifyThrows(() => Assert.AreEqual(1.0f, 1.0f, float.NegativeInfinity));
        Verify(ex.Message is """
            Specified argument was out of the range of valid values.
            Parameter name: delta
            """ or "Specified argument was out of the range of valid values. (Parameter 'delta')");
    }

    public void FloatAreEqual_ExpectedIsNaN_ActualIsNumeric_DeltaIsNegativeInf_ShouldFail()
    {
        Exception ex = VerifyThrows(() => Assert.AreEqual(float.NaN, 1.0f, float.NegativeInfinity));
        Verify(ex.Message is """
            Specified argument was out of the range of valid values.
            Parameter name: delta
            """ or "Specified argument was out of the range of valid values. (Parameter 'delta')");
    }

    public void FloatAreEqual_ExpectedIsNaN_ActualIsNaN_DeltaIsNegativeInf_ShouldFail()
    {
        Exception ex = VerifyThrows(() => Assert.AreEqual(float.NaN, float.NaN, -1.0f));
        Verify(ex.Message is """
            Specified argument was out of the range of valid values.
            Parameter name: delta
            """ or "Specified argument was out of the range of valid values. (Parameter 'delta')");
    }

    public void FloatAreEqual_ExpectedIsNumeric_ActualIsNaN_DeltaIsNegativeInf_ShouldFail()
    {
        Exception ex = VerifyThrows(() => Assert.AreEqual(1.0f, float.NaN, float.NegativeInfinity));
        Verify(ex.Message is """
            Specified argument was out of the range of valid values.
            Parameter name: delta
            """ or "Specified argument was out of the range of valid values. (Parameter 'delta')");
    }

    public void FloatAreEqual_ExpectedIsNumeric_ActualIsNumeric_ExpectedAndActualDifferenceGreaterThanDeltaPositive_DeltaIsNumeric_ShouldFail()
    {
        Exception ex = VerifyThrows(() => Assert.AreEqual(5.0f, 2.0f, 2.0f)); // difference is 3. Delta is 2
        Verify(ex.Message == "Assert.AreEqual failed. Expected a difference no greater than <2> between expected value <5> and actual value <2>. ");
    }

    public void FloatAreEqual_ExpectedIsNumeric_ActualIsNumeric_ExpectedAndActualDifferenceGreaterThanDeltaNegative_DeltaIsNumeric_ShouldFail()
    {
        Exception ex = VerifyThrows(() => Assert.AreEqual(2.0f, 5.0f, 2.0f)); // difference is -3. Delta is 2
        Verify(ex.Message == "Assert.AreEqual failed. Expected a difference no greater than <2> between expected value <2> and actual value <5>. ");
    }

    public void FloatAreEqual_ExpectedIsNumeric_ActualIsNumeric_ExpectedAndActualDifferenceLessThanDeltaPositive_DeltaIsNumeric_ShouldPass()
        => Assert.AreEqual(5.0f, 4.0f, 2.0f); // difference is 1. Delta is 2

    public void FloatAreEqual_ExpectedIsNumeric_ActualIsNumeric_ExpectedAndActualDifferenceLessThanDeltaNegative_DeltaIsNumeric_ShouldFail()
        => Assert.AreEqual(4.0f, 5.0f, 2.0f); // difference is -1. Delta is 2

    public void FloatAreEqual_ExpectedIsNumeric_ActualIsNaN_DeltaIsNumeric_ShouldFail()
    {
        Exception ex = VerifyThrows(() => Assert.AreEqual(5.0f, float.NaN, 2.0f));
        Verify(ex.Message == "Assert.AreEqual failed. Expected a difference no greater than <2> between expected value <5> and actual value <NaN>. ");
    }

    public void FloatAreEqual_ExpectedIsNaN_ActualIsNumeric_DeltaIsNumeric_ShouldFail()
    {
        Exception ex = VerifyThrows(() => Assert.AreEqual(float.NaN, 5.0f, 2.0f));
        Verify(ex.Message == "Assert.AreEqual failed. Expected a difference no greater than <2> between expected value <NaN> and actual value <5>. ");
    }

    public void FloatAreEqual_ExpectedIsNaN_ActualIsNaN_DeltaIsNumeric_ShouldPass()
        => Assert.AreEqual(float.NaN, float.NaN, 2.0f);

    public void FloatAreNotEqual_ExpectedIsNumeric_ActualIsNumeric_ExpectedAndActualNotEquals_DeltaIsNaN_ShouldFail()
    {
        Exception ex = VerifyThrows(() => Assert.AreNotEqual(1.0f, 2.0f, float.NaN));
        Verify(ex.Message is """
            Specified argument was out of the range of valid values.
            Parameter name: delta
            """ or "Specified argument was out of the range of valid values. (Parameter 'delta')");
    }

    public void FloatAreNotEqual_ExpectedIsNumeric_ActualIsNumeric_ExpectedAndActualEquals_DeltaIsNaN_ShouldFail()
    {
        Exception ex = VerifyThrows(() => Assert.AreNotEqual(1.0f, 1.0f, float.NaN));
        Verify(ex.Message is """
            Specified argument was out of the range of valid values.
            Parameter name: delta
            """ or "Specified argument was out of the range of valid values. (Parameter 'delta')");
    }

    public void FloatAreNotEqual_ExpectedIsNaN_ActualIsNumeric_DeltaIsNaN_ShouldFail()
    {
        Exception ex = VerifyThrows(() => Assert.AreNotEqual(float.NaN, 1.0f, float.NaN));
        Verify(ex.Message is """
            Specified argument was out of the range of valid values.
            Parameter name: delta
            """ or "Specified argument was out of the range of valid values. (Parameter 'delta')");
    }

    public void FloatAreNotEqual_ExpectedIsNaN_ActualIsNaN_DeltaIsNaN_ShouldFail()
    {
        Exception ex = VerifyThrows(() => Assert.AreNotEqual(float.NaN, float.NaN, float.NaN));
        Verify(ex.Message is """
            Specified argument was out of the range of valid values.
            Parameter name: delta
            """ or "Specified argument was out of the range of valid values. (Parameter 'delta')");
    }

    public void FloatAreNotEqual_ExpectedIsNumeric_ActualIsNaN_DeltaIsNaN_ShouldFail()
    {
        Exception ex = VerifyThrows(() => Assert.AreNotEqual(1.0f, float.NaN, float.NaN));
        Verify(ex.Message is """
            Specified argument was out of the range of valid values.
            Parameter name: delta
            """ or "Specified argument was out of the range of valid values. (Parameter 'delta')");
    }

    public void FloatAreNotEqual_ExpectedIsNumeric_ActualIsNumeric_ExpectedAndActualNotEquals_DeltaIsNegative_ShouldFail()
    {
        Exception ex = VerifyThrows(() => Assert.AreNotEqual(1.0f, 2.0f, -1.0f));
        Verify(ex.Message is """
            Specified argument was out of the range of valid values.
            Parameter name: delta
            """ or "Specified argument was out of the range of valid values. (Parameter 'delta')");
    }

    public void FloatAreNotEqual_ExpectedIsNumeric_ActualIsNumeric_ExpectedAndActualEquals_DeltaIsNegative_ShouldFail()
    {
        Exception ex = VerifyThrows(() => Assert.AreNotEqual(1.0f, 1.0f, -1.0f));
        Verify(ex.Message is """
            Specified argument was out of the range of valid values.
            Parameter name: delta
            """ or "Specified argument was out of the range of valid values. (Parameter 'delta')");
    }

    public void FloatAreNotEqual_ExpectedIsNaN_ActualIsNumeric_DeltaIsNegative_ShouldFail()
    {
        Exception ex = VerifyThrows(() => Assert.AreNotEqual(float.NaN, 1.0f, -1.0f));
        Verify(ex.Message is """
            Specified argument was out of the range of valid values.
            Parameter name: delta
            """ or "Specified argument was out of the range of valid values. (Parameter 'delta')");
    }

    public void FloatAreNotEqual_ExpectedIsNaN_ActualIsNaN_DeltaIsNegative_ShouldFail()
    {
        Exception ex = VerifyThrows(() => Assert.AreNotEqual(float.NaN, float.NaN, -1.0f));
        Verify(ex.Message is """
            Specified argument was out of the range of valid values.
            Parameter name: delta
            """ or "Specified argument was out of the range of valid values. (Parameter 'delta')");
    }

    public void FloatAreNotEqual_ExpectedIsNumeric_ActualIsNaN_DeltaIsNegative_ShouldFail()
    {
        Exception ex = VerifyThrows(() => Assert.AreNotEqual(1.0f, float.NaN, -1.0f));
        Verify(ex.Message is """
            Specified argument was out of the range of valid values.
            Parameter name: delta
            """ or "Specified argument was out of the range of valid values. (Parameter 'delta')");
    }

    public void FloatAreNotEqual_ExpectedIsNumeric_ActualIsNumeric_ExpectedAndActualNotEquals_DeltaIsNegativeInf_ShouldFail()
    {
        Exception ex = VerifyThrows(() => Assert.AreNotEqual(1.0f, 2.0f, -1.0f));
        Verify(ex.Message is """
            Specified argument was out of the range of valid values.
            Parameter name: delta
            """ or "Specified argument was out of the range of valid values. (Parameter 'delta')");
    }

    public void FloatAreNotEqual_ExpectedIsNumeric_ActualIsNumeric_ExpectedAndActualEquals_DeltaIsNegativeInf_ShouldFail()
    {
        Exception ex = VerifyThrows(() => Assert.AreNotEqual(1.0f, 1.0f, float.NegativeInfinity));
        Verify(ex.Message is """
            Specified argument was out of the range of valid values.
            Parameter name: delta
            """ or "Specified argument was out of the range of valid values. (Parameter 'delta')");
    }

    public void FloatAreNotEqual_ExpectedIsNaN_ActualIsNumeric_DeltaIsNegativeInf_ShouldFail()
    {
        Exception ex = VerifyThrows(() => Assert.AreNotEqual(float.NaN, 1.0f, float.NegativeInfinity));
        Verify(ex.Message is """
            Specified argument was out of the range of valid values.
            Parameter name: delta
            """ or "Specified argument was out of the range of valid values. (Parameter 'delta')");
    }

    public void FloatAreNotEqual_ExpectedIsNaN_ActualIsNaN_DeltaIsNegativeInf_ShouldFail()
    {
        Exception ex = VerifyThrows(() => Assert.AreNotEqual(float.NaN, float.NaN, float.NegativeInfinity));
        Verify(ex.Message is """
            Specified argument was out of the range of valid values.
            Parameter name: delta
            """ or "Specified argument was out of the range of valid values. (Parameter 'delta')");
    }

    public void FloatAreNotEqual_ExpectedIsNumeric_ActualIsNaN_DeltaIsNegativeInf_ShouldFail()
    {
        Exception ex = VerifyThrows(() => Assert.AreNotEqual(1.0f, float.NaN, float.NegativeInfinity));
        Verify(ex.Message is """
            Specified argument was out of the range of valid values.
            Parameter name: delta
            """ or "Specified argument was out of the range of valid values. (Parameter 'delta')");
    }

    public void FloatAreNotEqual_ExpectedIsNumeric_ActualIsNumeric_ExpectedAndActualDifferenceGreaterThanDeltaPositive_DeltaIsNumeric_ShouldPass()
        => Assert.AreNotEqual(5.0f, 2.0f, 2.0f); // difference is 3. Delta is 2

    public void FloatAreNotEqual_ExpectedIsNumeric_ActualIsNumeric_ExpectedAndActualDifferenceGreaterThanDeltaNegative_DeltaIsNumeric_ShouldPass()
        => Assert.AreNotEqual(2.0f, 5.0f, 2.0f); // difference is -3. Delta is 2

    public void FloatAreNotEqual_ExpectedIsNumeric_ActualIsNumeric_ExpectedAndActualDifferenceLessThanDeltaPositive_DeltaIsNumeric_ShouldFail()
    {
        Exception ex = VerifyThrows(() => Assert.AreNotEqual(5.0f, 4.0f, 2.0f)); // difference is 1. Delta is 2
        Verify(ex.Message == "Assert.AreNotEqual failed. Expected a difference greater than <2> between expected value <5> and actual value <4>. ");
    }

    public void FloatAreNotEqual_ExpectedIsNumeric_ActualIsNumeric_ExpectedAndActualDifferenceLessThanDeltaNegative_DeltaIsNumeric_ShouldFail()
    {
        Exception ex = VerifyThrows(() => Assert.AreNotEqual(4.0f, 5.0f, 2.0f)); // difference is -1. Delta is 2
        Verify(ex.Message == "Assert.AreNotEqual failed. Expected a difference greater than <2> between expected value <4> and actual value <5>. ");
    }

    public void FloatAreNotEqual_ExpectedIsNumeric_ActualIsNaN_DeltaIsNumeric_ShouldPass() => Assert.AreNotEqual(5.0f, float.NaN, 2.0f);

    public void FloatAreNotEqual_ExpectedIsNaN_ActualIsNumeric_DeltaIsNumeric_ShouldPass()
        => Assert.AreNotEqual(float.NaN, 5.0f, 2.0f);

    public void FloatAreNotEqual_ExpectedIsNaN_ActualIsNaN_DeltaIsNumeric_ShouldFail()
    {
        Exception ex = VerifyThrows(() => Assert.AreNotEqual(float.NaN, float.NaN, 2.0f));
        Verify(ex.Message == "Assert.AreNotEqual failed. Expected a difference greater than <2> between expected value <NaN> and actual value <NaN>. ");
    }

    public void DoubleAreEqual_ExpectedIsNumeric_ActualIsNumeric_ExpectedAndActualNotEquals_DeltaIsNaN_ShouldFail()
    {
        Exception ex = VerifyThrows(() => Assert.AreEqual(1.0d, 2.0d, double.NaN));
        Verify(ex.Message is """
            Specified argument was out of the range of valid values.
            Parameter name: delta
            """ or "Specified argument was out of the range of valid values. (Parameter 'delta')");
    }

    public void DoubleAreEqual_ExpectedIsNumeric_ActualIsNumeric_ExpectedAndActualEquals_DeltaIsNaN_ShouldFail()
    {
        Exception ex = VerifyThrows(() => Assert.AreEqual(1.0d, 1.0d, double.NaN));
        Verify(ex.Message is """
            Specified argument was out of the range of valid values.
            Parameter name: delta
            """ or "Specified argument was out of the range of valid values. (Parameter 'delta')");
    }

    public void DoubleAreEqual_ExpectedIsNaN_ActualIsNumeric_DeltaIsNaN_ShouldFail()
    {
        Exception ex = VerifyThrows(() => Assert.AreEqual(double.NaN, 1.0d, double.NaN));
        Verify(ex.Message is """
            Specified argument was out of the range of valid values.
            Parameter name: delta
            """ or "Specified argument was out of the range of valid values. (Parameter 'delta')");
    }

    public void DoubleAreEqual_ExpectedIsNaN_ActualIsNaN_DeltaIsNaN_ShouldFail()
    {
        Exception ex = VerifyThrows(() => Assert.AreEqual(double.NaN, double.NaN, double.NaN));
        Verify(ex.Message is """
            Specified argument was out of the range of valid values.
            Parameter name: delta
            """ or "Specified argument was out of the range of valid values. (Parameter 'delta')");
    }

    public void DoubleAreEqual_ExpectedIsNumeric_ActualIsNaN_DeltaIsNaN_ShouldFail()
    {
        Exception ex = VerifyThrows(() => Assert.AreEqual(1.0d, double.NaN, double.NaN));
        Verify(ex.Message is """
            Specified argument was out of the range of valid values.
            Parameter name: delta
            """ or "Specified argument was out of the range of valid values. (Parameter 'delta')");
    }

    public void DoubleAreEqual_ExpectedIsNumeric_ActualIsNumeric_ExpectedAndActualNotEquals_DeltaIsNegative_ShouldFail()
    {
        Exception ex = VerifyThrows(() => Assert.AreEqual(1.0d, 2.0d, -1.0d));
        Verify(ex.Message is """
            Specified argument was out of the range of valid values.
            Parameter name: delta
            """ or "Specified argument was out of the range of valid values. (Parameter 'delta')");
    }

    public void DoubleAreEqual_ExpectedIsNumeric_ActualIsNumeric_ExpectedAndActualEquals_DeltaIsNegative_ShouldFail()
    {
        Exception ex = VerifyThrows(() => Assert.AreEqual(1.0d, 1.0d, -1.0d));
        Verify(ex.Message is """
            Specified argument was out of the range of valid values.
            Parameter name: delta
            """ or "Specified argument was out of the range of valid values. (Parameter 'delta')");
    }

    public void DoubleAreEqual_ExpectedIsNaN_ActualIsNumeric_DeltaIsNegative_ShouldFail()
    {
        Exception ex = VerifyThrows(() => Assert.AreEqual(double.NaN, 1.0d, -1.0d));
        Verify(ex.Message is """
            Specified argument was out of the range of valid values.
            Parameter name: delta
            """ or "Specified argument was out of the range of valid values. (Parameter 'delta')");
    }

    public void DoubleAreEqual_ExpectedIsNaN_ActualIsNaN_DeltaIsNegative_ShouldFail()
    {
        Exception ex = VerifyThrows(() => Assert.AreEqual(double.NaN, double.NaN, -1.0d));
        Verify(ex.Message is """
            Specified argument was out of the range of valid values.
            Parameter name: delta
            """ or "Specified argument was out of the range of valid values. (Parameter 'delta')");
    }

    public void DoubleAreEqual_ExpectedIsNumeric_ActualIsNaN_DeltaIsNegative_ShouldFail()
    {
        Exception ex = VerifyThrows(() => Assert.AreEqual(1.0d, double.NaN, -1.0d));
        Verify(ex.Message is """
            Specified argument was out of the range of valid values.
            Parameter name: delta
            """ or "Specified argument was out of the range of valid values. (Parameter 'delta')");
    }

    public void DoubleAreEqual_ExpectedIsNumeric_ActualIsNumeric_ExpectedAndActualNotEquals_DeltaIsNegativeInf_ShouldFail()
    {
        Exception ex = VerifyThrows(() => Assert.AreEqual(1.0d, 2.0d, double.NegativeInfinity));
        Verify(ex.Message is """
            Specified argument was out of the range of valid values.
            Parameter name: delta
            """ or "Specified argument was out of the range of valid values. (Parameter 'delta')");
    }

    public void DoubleAreEqual_ExpectedIsNumeric_ActualIsNumeric_ExpectedAndActualEquals_DeltaIsNegativeInf_ShouldFail()
    {
        Exception ex = VerifyThrows(() => Assert.AreEqual(1.0d, 1.0d, double.NegativeInfinity));
        Verify(ex.Message is """
            Specified argument was out of the range of valid values.
            Parameter name: delta
            """ or "Specified argument was out of the range of valid values. (Parameter 'delta')");
    }

    public void DoubleAreEqual_ExpectedIsNaN_ActualIsNumeric_DeltaIsNegativeInf_ShouldFail()
    {
        Exception ex = VerifyThrows(() => Assert.AreEqual(double.NaN, 1.0d, double.NegativeInfinity));
        Verify(ex.Message is """
            Specified argument was out of the range of valid values.
            Parameter name: delta
            """ or "Specified argument was out of the range of valid values. (Parameter 'delta')");
    }

    public void DoubleAreEqual_ExpectedIsNaN_ActualIsNaN_DeltaIsNegativeInf_ShouldFail()
    {
        Exception ex = VerifyThrows(() => Assert.AreEqual(double.NaN, double.NaN, double.NegativeInfinity));
        Verify(ex.Message is """
            Specified argument was out of the range of valid values.
            Parameter name: delta
            """ or "Specified argument was out of the range of valid values. (Parameter 'delta')");
    }

    public void DoubleAreEqual_ExpectedIsNumeric_ActualIsNaN_DeltaIsNegativeInf_ShouldFail()
    {
        Exception ex = VerifyThrows(() => Assert.AreEqual(1.0d, double.NaN, double.NegativeInfinity));
        Verify(ex.Message is """
            Specified argument was out of the range of valid values.
            Parameter name: delta
            """ or "Specified argument was out of the range of valid values. (Parameter 'delta')");
    }

    public void DoubleAreEqual_ExpectedIsNumeric_ActualIsNumeric_ExpectedAndActualDifferenceGreaterThanDeltaPositive_DeltaIsNumeric_ShouldFail()
    {
        Exception ex = VerifyThrows(() => Assert.AreEqual(5.0d, 2.0d, 2.0d)); // difference is 3. Delta is 2
        Verify(ex.Message == "Assert.AreEqual failed. Expected a difference no greater than <2> between expected value <5> and actual value <2>. ");
    }

    public void DoubleAreEqual_ExpectedIsNumeric_ActualIsNumeric_ExpectedAndActualDifferenceGreaterThanDeltaNegative_DeltaIsNumeric_ShouldFail()
    {
        Exception ex = VerifyThrows(() => Assert.AreEqual(2.0d, 5.0d, 2.0d)); // difference is -3. Delta is 2
        Verify(ex.Message == "Assert.AreEqual failed. Expected a difference no greater than <2> between expected value <2> and actual value <5>. ");
    }

    public void DoubleAreEqual_ExpectedIsNumeric_ActualIsNumeric_ExpectedAndActualDifferenceLessThanDeltaPositive_DeltaIsNumeric_ShouldPass()
        => Assert.AreEqual(5.0d, 4.0d, 2.0d); // difference is 1. Delta is 2

    public void DoubleAreEqual_ExpectedIsNumeric_ActualIsNumeric_ExpectedAndActualDifferenceLessThanDeltaNegative_DeltaIsNumeric_ShouldFail()
        => Assert.AreEqual(4.0d, 5.0d, 2.0d); // difference is -1. Delta is 2

    public void DoubleAreEqual_ExpectedIsNumeric_ActualIsNaN_DeltaIsNumeric_ShouldFail()
    {
        Exception ex = VerifyThrows(() => Assert.AreEqual(5.0d, double.NaN, 2.0d));
        Verify(ex.Message == "Assert.AreEqual failed. Expected a difference no greater than <2> between expected value <5> and actual value <NaN>. ");
    }

    public void DoubleAreEqual_ExpectedIsNaN_ActualIsNumeric_DeltaIsNumeric_ShouldFail()
    {
        Exception ex = VerifyThrows(() => Assert.AreEqual(double.NaN, 5.0d, 2.0d));
        Verify(ex.Message == "Assert.AreEqual failed. Expected a difference no greater than <2> between expected value <NaN> and actual value <5>. ");
    }

    public void DoubleAreEqual_ExpectedIsNaN_ActualIsNaN_DeltaIsNumeric_ShouldPass()
        => Assert.AreEqual(double.NaN, double.NaN, 2.0d);

    public void DoubleAreNotEqual_ExpectedIsNumeric_ActualIsNumeric_ExpectedAndActualNotEquals_DeltaIsNaN_ShouldFail()
    {
        Exception ex = VerifyThrows(() => Assert.AreNotEqual(1.0d, 2.0d, double.NaN));
        Verify(ex.Message is """
            Specified argument was out of the range of valid values.
            Parameter name: delta
            """ or "Specified argument was out of the range of valid values. (Parameter 'delta')");
    }

    public void DoubleAreNotEqual_ExpectedIsNumeric_ActualIsNumeric_ExpectedAndActualEquals_DeltaIsNaN_ShouldFail()
    {
        Exception ex = VerifyThrows(() => Assert.AreNotEqual(1.0d, 1.0d, double.NaN));
        Verify(ex.Message is """
            Specified argument was out of the range of valid values.
            Parameter name: delta
            """ or "Specified argument was out of the range of valid values. (Parameter 'delta')");
    }

    public void DoubleAreNotEqual_ExpectedIsNaN_ActualIsNumeric_DeltaIsNaN_ShouldFail()
    {
        Exception ex = VerifyThrows(() => Assert.AreNotEqual(double.NaN, 1.0d, double.NaN));
        Verify(ex.Message is """
            Specified argument was out of the range of valid values.
            Parameter name: delta
            """ or "Specified argument was out of the range of valid values. (Parameter 'delta')");
    }

    public void DoubleAreNotEqual_ExpectedIsNaN_ActualIsNaN_DeltaIsNaN_ShouldFail()
    {
        Exception ex = VerifyThrows(() => Assert.AreNotEqual(double.NaN, double.NaN, double.NaN));
        Verify(ex.Message is """
            Specified argument was out of the range of valid values.
            Parameter name: delta
            """ or "Specified argument was out of the range of valid values. (Parameter 'delta')");
    }

    public void DoubleAreNotEqual_ExpectedIsNumeric_ActualIsNaN_DeltaIsNaN_ShouldFail()
    {
        Exception ex = VerifyThrows(() => Assert.AreNotEqual(1.0d, double.NaN, double.NaN));
        Verify(ex.Message is """
            Specified argument was out of the range of valid values.
            Parameter name: delta
            """ or "Specified argument was out of the range of valid values. (Parameter 'delta')");
    }

    public void DoubleAreNotEqual_ExpectedIsNumeric_ActualIsNumeric_ExpectedAndActualNotEquals_DeltaIsNegative_ShouldFail()
    {
        Exception ex = VerifyThrows(() => Assert.AreNotEqual(1.0d, 2.0d, -1.0d));
        Verify(ex.Message is """
            Specified argument was out of the range of valid values.
            Parameter name: delta
            """ or "Specified argument was out of the range of valid values. (Parameter 'delta')");
    }

    public void DoubleAreNotEqual_ExpectedIsNumeric_ActualIsNumeric_ExpectedAndActualEquals_DeltaIsNegative_ShouldFail()
    {
        Exception ex = VerifyThrows(() => Assert.AreNotEqual(1.0d, 1.0d, -1.0d));
        Verify(ex.Message is """
            Specified argument was out of the range of valid values.
            Parameter name: delta
            """ or "Specified argument was out of the range of valid values. (Parameter 'delta')");
    }

    public void DoubleAreNotEqual_ExpectedIsNaN_ActualIsNumeric_DeltaIsNegative_ShouldFail()
    {
        Exception ex = VerifyThrows(() => Assert.AreNotEqual(double.NaN, 1.0d, -1.0d));
        Verify(ex.Message is """
            Specified argument was out of the range of valid values.
            Parameter name: delta
            """ or "Specified argument was out of the range of valid values. (Parameter 'delta')");
    }

    public void DoubleAreNotEqual_ExpectedIsNaN_ActualIsNaN_DeltaIsNegative_ShouldFail()
    {
        Exception ex = VerifyThrows(() => Assert.AreNotEqual(double.NaN, double.NaN, -1.0d));
        Verify(ex.Message is """
            Specified argument was out of the range of valid values.
            Parameter name: delta
            """ or "Specified argument was out of the range of valid values. (Parameter 'delta')");
    }

    public void DoubleAreNotEqual_ExpectedIsNumeric_ActualIsNaN_DeltaIsNegative_ShouldFail()
    {
        Exception ex = VerifyThrows(() => Assert.AreNotEqual(1.0d, double.NaN, -1.0d));
        Verify(ex.Message is """
            Specified argument was out of the range of valid values.
            Parameter name: delta
            """ or "Specified argument was out of the range of valid values. (Parameter 'delta')");
    }

    public void DoubleAreNotEqual_ExpectedIsNumeric_ActualIsNumeric_ExpectedAndActualNotEquals_DeltaIsNegativeInf_ShouldFail()
    {
        Exception ex = VerifyThrows(() => Assert.AreNotEqual(1.0d, 2.0d, double.NegativeInfinity));
        Verify(ex.Message is """
            Specified argument was out of the range of valid values.
            Parameter name: delta
            """ or "Specified argument was out of the range of valid values. (Parameter 'delta')");
    }

    public void DoubleAreNotEqual_ExpectedIsNumeric_ActualIsNumeric_ExpectedAndActualEquals_DeltaIsNegativeInf_ShouldFail()
    {
        Exception ex = VerifyThrows(() => Assert.AreNotEqual(1.0d, 1.0d, double.NegativeInfinity));
        Verify(ex.Message is """
            Specified argument was out of the range of valid values.
            Parameter name: delta
            """ or "Specified argument was out of the range of valid values. (Parameter 'delta')");
    }

    public void DoubleAreNotEqual_ExpectedIsNaN_ActualIsNumeric_DeltaIsNegativeInf_ShouldFail()
    {
        Exception ex = VerifyThrows(() => Assert.AreNotEqual(double.NaN, 1.0d, double.NegativeInfinity));
        Verify(ex.Message is """
            Specified argument was out of the range of valid values.
            Parameter name: delta
            """ or "Specified argument was out of the range of valid values. (Parameter 'delta')");
    }

    public void DoubleAreNotEqual_ExpectedIsNaN_ActualIsNaN_DeltaIsNegativeInf_ShouldFail()
    {
        Exception ex = VerifyThrows(() => Assert.AreNotEqual(double.NaN, double.NaN, double.NegativeInfinity));
        Verify(ex.Message is """
            Specified argument was out of the range of valid values.
            Parameter name: delta
            """ or "Specified argument was out of the range of valid values. (Parameter 'delta')");
    }

    public void DoubleAreNotEqual_ExpectedIsNumeric_ActualIsNaN_DeltaIsNegativeInf_ShouldFail()
    {
        Exception ex = VerifyThrows(() => Assert.AreNotEqual(1.0d, double.NaN, double.NegativeInfinity));
        Verify(ex.Message is """
            Specified argument was out of the range of valid values.
            Parameter name: delta
            """ or "Specified argument was out of the range of valid values. (Parameter 'delta')");
    }

    public void DoubleAreNotEqual_ExpectedIsNumeric_ActualIsNumeric_ExpectedAndActualDifferenceGreaterThanDeltaPositive_DeltaIsNumeric_ShouldPass()
        => Assert.AreNotEqual(5.0d, 2.0d, 2.0d); // difference is 3. Delta is 2

    public void DoubleAreNotEqual_ExpectedIsNumeric_ActualIsNumeric_ExpectedAndActualDifferenceGreaterThanDeltaNegative_DeltaIsNumeric_ShouldPass()
        => Assert.AreNotEqual(2.0d, 5.0d, 2.0d); // difference is -3. Delta is 2

    public void DoubleAreNotEqual_ExpectedIsNumeric_ActualIsNumeric_ExpectedAndActualDifferenceLessThanDeltaPositive_DeltaIsNumeric_ShouldFail()
    {
        Exception ex = VerifyThrows(() => Assert.AreNotEqual(5.0d, 4.0d, 2.0d)); // difference is 1. Delta is 2
        Verify(ex.Message == "Assert.AreNotEqual failed. Expected a difference greater than <2> between expected value <5> and actual value <4>. ");
    }

    public void DoubleAreNotEqual_ExpectedIsNumeric_ActualIsNumeric_ExpectedAndActualDifferenceLessThanDeltaNegative_DeltaIsNumeric_ShouldFail()
    {
        Exception ex = VerifyThrows(() => Assert.AreNotEqual(4.0d, 5.0d, 2.0d)); // difference is -1. Delta is 2
        Verify(ex.Message == "Assert.AreNotEqual failed. Expected a difference greater than <2> between expected value <4> and actual value <5>. ");
    }

    public void DoubleAreNotEqual_ExpectedIsNumeric_ActualIsNaN_DeltaIsNumeric_ShouldPass() => Assert.AreNotEqual(5.0d, double.NaN, 2.0d);

    public void DoubleAreNotEqual_ExpectedIsNaN_ActualIsNumeric_DeltaIsNumeric_ShouldPass()
        => Assert.AreNotEqual(double.NaN, 5.0d, 2.0d);

    public void DoubleAreNotEqual_ExpectedIsNaN_ActualIsNaN_DeltaIsNumeric_ShouldFail()
    {
        Exception ex = VerifyThrows(() => Assert.AreNotEqual(double.NaN, double.NaN, 2.0d));
        Verify(ex.Message == "Assert.AreNotEqual failed. Expected a difference greater than <2> between expected value <NaN> and actual value <NaN>. ");
    }

    private class TypeOverridesEquals
    {
        public override bool Equals(object? obj) => true;

        public override int GetHashCode() => throw new NotImplementedException();
    }

    private sealed class EquatableType : IEquatable<EquatableType>
    {
        public bool Equals(EquatableType? other) => true;

        public override bool Equals(object? obj) => Equals(obj as EquatableType);

        public override int GetHashCode() => 0;
    }

    private sealed class TypeOverridesEqualsEqualityComparer : EqualityComparer<TypeOverridesEquals>
    {
        public override bool Equals(TypeOverridesEquals? x, TypeOverridesEquals? y) => false;

        public override int GetHashCode(TypeOverridesEquals obj) => throw new NotImplementedException();
    }

    private class A : IEquatable<A>
    {
        public string Id { get; set; } = string.Empty;

        public bool Equals(A? other)
            => other?.Id == Id;

        public override bool Equals(object? obj)
            => Equals(obj as A);

        public override int GetHashCode()
            => Id.GetHashCode() + 123;
    }

    private class B : IEquatable<A>
    {
        public string Id { get; set; } = string.Empty;

        public override bool Equals(object? obj)
            => Equals(obj as A);

        public bool Equals(A? other)
            => other?.Id == Id;

        public override int GetHashCode()
            => Id.GetHashCode() + 1234;
    }

    public void AreEqualStringDifferenceShouldDifference()
    {
        Exception ex = VerifyThrows(() => Assert.AreEqual("aaaa", "aaab"));
        Verify(ex.Message == """
            String lengths are both 4. Strings differ at index 3.
            Expected: "aaaa"
            But was:  "aaab"
            --------------^ 
            """);
    }

    public void AreEqualStringDifferenceAtBeginning()
    {
        Exception ex = VerifyThrows(() => Assert.AreEqual("baaa", "aaaa"));
        Verify(ex.Message == """
            String lengths are both 4. Strings differ at index 0.
            Expected: "baaa"
            But was:  "aaaa"
            -----------^ 
            """);
    }

    public void AreEqualStringWithSpecialCharactersShouldEscape()
    {
        Exception ex = VerifyThrows(() => Assert.AreEqual("aa\ta", "aa a"));
        Verify(ex.Message == """
            String lengths are both 4. Strings differ at index 2.
            Expected: "aa\ta"
            But was:  "aa a"
            -------------^ 
            """);
    }

    public void AreEqualLongStringsShouldTruncateAndShowContext()
    {
        string expected = new string('a', 100) + "b" + new string('c', 100);
        string actual = new string('a', 100) + "d" + new string('c', 100);

        Exception ex = VerifyThrows(() => Assert.AreEqual(expected, actual));
        Verify(ex.Message == """
            String lengths are both 201. Strings differ at index 100.
            Expected: "...aaaaabccccc..."
            But was:  "...aaaaadccccc..."
            -------------------^ 
            """);
    }

    public void AreEqualStringWithCultureShouldUseEnhancedMessage()
    {
        Exception ex = VerifyThrows(() => Assert.AreEqual("aaaa", "aaab", false, CultureInfo.InvariantCulture));
        Verify(ex.Message == """
            String lengths are both 4. Strings differ at index 3.
            Expected: "aaaa"
            But was:  "aaab"
            --------------^ 
            """);
    }
}
