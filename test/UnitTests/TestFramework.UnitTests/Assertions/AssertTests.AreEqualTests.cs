// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using AwesomeAssertions;

using TestFramework.ForTestingMSTest;

namespace Microsoft.VisualStudio.TestPlatform.TestFramework.UnitTests;

public partial class AssertTests : TestContainer
{
    public void AreNotEqualShouldFailWhenNotEqualType()
    {
        Action action = () => Assert.AreNotEqual(1, 1);
        action.Should().Throw<AssertFailedException>();
    }

    public void AreNotEqualShouldFailWhenNotEqualTypeWithMessage()
    {
        Action action = () => Assert.AreNotEqual(1, 1, "A Message");
        action.Should().Throw<Exception>()
            .And.Message.Should().Contain("A Message");
    }

    public void AreNotEqualShouldFailWhenNotEqualString()
    {
        Action action = () => Assert.AreNotEqual("A", "A");
        action.Should().Throw<AssertFailedException>();
    }

    public void AreNotEqualShouldFailWhenNotEqualStringWithMessage()
    {
        Action action = () => Assert.AreNotEqual("A", "A", "A Message");
        action.Should().Throw<Exception>()
            .And.Message.Should().Contain("A Message");
    }

    [SuppressMessage("Globalization", "CA1304:Specify CultureInfo", Justification = "Testing the API without the culture")]
    public void AreNotEqualShouldFailWhenNotEqualStringAndCaseIgnored()
    {
        Action action = () => Assert.AreNotEqual("A", "a", true);
        action.Should().Throw<AssertFailedException>();
    }

    public void AreNotEqualShouldFailWhenNotEqualInt()
    {
        Action action = () => Assert.AreNotEqual(1, 1);
        action.Should().Throw<AssertFailedException>();
    }

    public void AreNotEqualShouldFailWhenNotEqualIntWithMessage()
    {
        Action action = () => Assert.AreNotEqual(1, 1, "A Message");
        action.Should().Throw<Exception>()
            .And.Message.Should().Contain("A Message");
    }

    public void AreNotEqualShouldFailWhenNotEqualLong()
    {
        Action action = () => Assert.AreNotEqual(1L, 1L);
        action.Should().Throw<AssertFailedException>();
    }

    public void AreNotEqualShouldFailWhenNotEqualLongWithMessage()
    {
        Action action = () => Assert.AreNotEqual(1L, 1L, "A Message");
        action.Should().Throw<Exception>()
            .And.Message.Should().Contain("A Message");
    }

    public void AreNotEqualShouldFailWhenNotEqualLongWithDelta()
    {
        Action action = () => Assert.AreNotEqual(1L, 2L, 1L);
        action.Should().Throw<AssertFailedException>();
    }

    public void AreNotEqualShouldFailWhenNotEqualDecimal()
    {
        Action action = () => Assert.AreNotEqual(0.1M, 0.1M);
        action.Should().Throw<AssertFailedException>();
    }

    public void AreNotEqualShouldFailWhenNotEqualDecimalWithMessage()
    {
        Action action = () => Assert.AreNotEqual(0.1M, 0.1M, "A Message");
        action.Should().Throw<Exception>()
            .And.Message.Should().Contain("A Message");
    }

    public void AreNotEqualShouldFailWhenNotEqualDecimalWithDelta()
    {
        Action action = () => Assert.AreNotEqual(0.1M, 0.2M, 0.1M);
        action.Should().Throw<AssertFailedException>();
    }

    public void AreNotEqualShouldFailWhenNotEqualDouble()
    {
        Action action = () => Assert.AreNotEqual(0.1, 0.1);
        action.Should().Throw<AssertFailedException>();
    }

    public void AreNotEqualShouldFailWhenNotEqualDoubleWithMessage()
    {
        Action action = () => Assert.AreNotEqual(0.1, 0.1, "A Message");
        action.Should().Throw<Exception>()
            .And.Message.Should().Contain("A Message");
    }

    public void AreNotEqualShouldFailWhenNotEqualDoubleWithDelta()
    {
        Action action = () => Assert.AreNotEqual(0.1, 0.2, 0.1);
        action.Should().Throw<AssertFailedException>();
    }

    public void AreNotEqualShouldFailWhenFloatDouble()
    {
        Action action = () => Assert.AreNotEqual(100E-2, 100E-2);
        action.Should().Throw<AssertFailedException>();
    }

    public void AreNotEqualShouldFailWhenFloatDoubleWithMessage()
    {
        Action action = () => Assert.AreNotEqual(100E-2, 100E-2, "A Message");
        action.Should().Throw<Exception>()
            .And.Message.Should().Contain("A Message");
    }

    public void AreNotEqualShouldFailWhenNotEqualFloatWithDelta()
    {
        Action action = () => Assert.AreNotEqual(100E-2, 200E-2, 100E-2);
        action.Should().Throw<AssertFailedException>();
    }

    public void AreEqualShouldFailWhenNotEqualType()
    {
        Action action = () => Assert.AreEqual(null, "string");
        action.Should().Throw<AssertFailedException>();
    }

    public void AreEqualShouldFailWhenNotEqualTypeWithMessage()
    {
        Action action = () => Assert.AreEqual(null, "string", "A Message");
        action.Should().Throw<Exception>()
            .And.Message.Should().Contain("A Message");
    }

    public void AreEqual_WithTurkishCultureAndIgnoreCase_Throws()
    {
        string expected = "i";
        string actual = "I";
        var turkishCulture = new CultureInfo("tr-TR");

        // In the tr-TR culture, "i" and "I" are not considered equal when doing a case-insensitive comparison.
        Action action = () => Assert.AreEqual(expected, actual, true, turkishCulture);
        action.Should().Throw<Exception>();
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
        Action action = () => Assert.AreEqual(expected, actual, false, englishCulture);
        action.Should().Throw<Exception>()
            .And.Message.Should().Be("Assert.AreEqual failed. Expected:<i>. Case is different for actual value:<I>. 'expected' expression: 'expected', 'actual' expression: 'actual'.");
    }

    public void AreEqual_WithTurkishCultureAndDoesNotIgnoreCase_Throws()
    {
        string expected = "i";
        string actual = "I";
        var turkishCulture = new CultureInfo("tr-TR");

        // Won't ignore case.
        Action action = () => Assert.AreEqual(expected, actual, false, turkishCulture);
        action.Should().Throw<Exception>();
    }

    public void AreEqualShouldFailWhenNotEqualStringWithMessage()
    {
        Action action = () => Assert.AreEqual("A", "a", "A Message");
        action.Should().Throw<Exception>()
            .And.Message.Should().Contain("A Message");
    }

    [SuppressMessage("Globalization", "CA1304:Specify CultureInfo", Justification = "Testing the API without the culture")]
    public void AreEqualShouldFailWhenNotEqualStringAndCaseIgnored()
    {
        Action action = () => Assert.AreEqual("A", "a", false);
        action.Should().Throw<AssertFailedException>();
    }

    public void AreEqualShouldFailWhenNotEqualInt()
    {
        Action action = () => Assert.AreEqual(1, 2);
        action.Should().Throw<AssertFailedException>();
    }

    public void AreEqualShouldFailWhenNotEqualIntWithMessage()
    {
        Action action = () => Assert.AreEqual(1, 2, "A Message");
        action.Should().Throw<Exception>()
            .And.Message.Should().Contain("A Message");
    }

    public void AreEqualShouldFailWhenNotEqualLong()
    {
        Action action = () => Assert.AreEqual(1L, 2L);
        action.Should().Throw<AssertFailedException>();
    }

    public void AreEqualShouldFailWhenNotEqualLongWithMessage()
    {
        Action action = () => Assert.AreEqual(1L, 2L, "A Message");
        action.Should().Throw<Exception>()
            .And.Message.Should().Contain("A Message");
    }

    public void AreEqualShouldFailWhenNotEqualLongWithDelta()
    {
        Action action = () => Assert.AreEqual(10L, 20L, 5L);
        action.Should().Throw<AssertFailedException>();
    }

    public void AreEqualShouldFailWhenNotEqualDouble()
    {
        Action action = () => Assert.AreEqual(0.1, 0.2);
        action.Should().Throw<AssertFailedException>();
    }

    public void AreEqualShouldFailWhenNotEqualDoubleWithMessage()
    {
        Action action = () => Assert.AreEqual(0.1, 0.2, "A Message");
        action.Should().Throw<Exception>()
            .And.Message.Should().Contain("A Message");
    }

    public void AreEqualShouldFailWhenNotEqualDoubleWithDelta()
    {
        Action action = () => Assert.AreEqual(0.1, 0.2, 0.05);
        action.Should().Throw<AssertFailedException>();
    }

    public void AreEqualShouldFailWhenNotEqualDecimal()
    {
        static void Action() => Assert.AreEqual(0.1M, 0.2M);
        Action action = Action;
        action.Should().Throw<AssertFailedException>();
    }

    public void AreEqualShouldFailWhenNotEqualDecimalWithMessage()
    {
        Action action = () => Assert.AreEqual(0.1M, 0.2M, "A Message");
        action.Should().Throw<Exception>()
            .And.Message.Should().Contain("A Message");
    }

    public void AreEqualShouldFailWhenNotEqualDecimalWithDelta()
    {
        Action action = () => Assert.AreEqual(0.1M, 0.2M, 0.05M);
        action.Should().Throw<AssertFailedException>();
    }

    public void AreEqualShouldFailWhenFloatDouble()
    {
        Action action = () => Assert.AreEqual(100E-2, 200E-2);
        action.Should().Throw<AssertFailedException>();
    }

    public void AreEqualShouldFailWhenFloatDoubleWithMessage()
    {
        Action action = () => Assert.AreEqual(100E-2, 200E-2, "A Message");
        action.Should().Throw<Exception>()
            .And.Message.Should().Contain("A Message");
    }

    public void AreEqualShouldFailWhenNotEqualFloatWithDelta()
    {
        Action action = () => Assert.AreEqual(100E-2, 200E-2, 50E-2);
        action.Should().Throw<AssertFailedException>();
    }

    public void AreEqualTwoObjectsShouldFail()
    {
        Action action = () => Assert.AreEqual(new object(), new object());
        action.Should().Throw<AssertFailedException>();
    }

    public void AreEqualTwoObjectsDifferentTypeShouldFail()
    {
        Action action = () => Assert.AreEqual(new object(), 1);
        action.Should().Throw<AssertFailedException>()
            .And.Message.Should().Contain("Assert.AreEqual failed. Expected:<System.Object (System.Object)>. Actual:<1 (System.Int32)>.");
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

        Action action = Action;
        action.Should().Throw<AssertFailedException>();
    }

    public void AreEqualUsingCustomIEquatable()
    {
        var instanceOfA = new A { Id = "SomeId" };
        var instanceOfB = new B { Id = "SomeId" };

        // This call works because we call the Equals override of "expected".
        // The Equals override of 'B' will return true.
        Assert.AreEqual<object>(instanceOfB, instanceOfA);

        // This one doesn't work, because we call the Equals override of "expected".
        // The Equals override of 'A' will return false.
        Action action = () => Assert.AreEqual<object>(instanceOfA, instanceOfB);
        action.Should().Throw<Exception>();
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
        o.WasToStringCalled.Should().BeFalse();
    }

    public async Task GenericAreEqual_InterpolatedString_DifferentValues_ShouldFail()
    {
        DummyClassTrackingToStringCalls o = new();
        DateTime dateTime = DateTime.Now;
        Func<Task> action = async () => Assert.AreEqual(0, 1, $"User-provided message. {o}, {o,35}, {await GetHelloStringAsync()}, {new DummyIFormattable()}, {dateTime:tt}, {dateTime,5:tt}");
        (await action.Should().ThrowAsync<Exception>())
            .And.Message.Should().Be($"Assert.AreEqual failed. Expected:<0>. Actual:<1>. 'expected' expression: '0', 'actual' expression: '1'. User-provided message. DummyClassTrackingToStringCalls,     DummyClassTrackingToStringCalls, Hello, DummyIFormattable.ToString(), {string.Format(null, "{0:tt}", dateTime)}, {string.Format(null, "{0,5:tt}", dateTime)}");
        o.WasToStringCalled.Should().BeTrue();
    }

    public void GenericAreNotEqual_InterpolatedString_DifferentValues_ShouldPass()
    {
        DummyClassTrackingToStringCalls o = new();
        Assert.AreNotEqual(0, 1, $"User-provided message: {o}");
        o.WasToStringCalled.Should().BeFalse();
    }

    public async Task GenericAreNotEqual_InterpolatedString_SameValues_ShouldFail()
    {
        DummyClassTrackingToStringCalls o = new();
        DateTime dateTime = DateTime.Now;
        Func<Task> action = async () => Assert.AreNotEqual(0, 0, $"User-provided message. {o}, {o,35}, {await GetHelloStringAsync()}, {new DummyIFormattable()}, {dateTime:tt}, {dateTime,5:tt}");
        (await action.Should().ThrowAsync<Exception>())
            .And.Message.Should().Be($"Assert.AreNotEqual failed. Expected any value except:<0>. Actual:<0>. 'notExpected' expression: '0', 'actual' expression: '0'. User-provided message. DummyClassTrackingToStringCalls,     DummyClassTrackingToStringCalls, Hello, DummyIFormattable.ToString(), {string.Format(null, "{0:tt}", dateTime)}, {string.Format(null, "{0,5:tt}", dateTime)}");
        o.WasToStringCalled.Should().BeTrue();
    }

    public void FloatAreEqual_InterpolatedString_EqualValues_ShouldPass()
    {
        DummyClassTrackingToStringCalls o = new();
        Assert.AreEqual(1.0f, 1.1f, delta: 0.2f, $"User-provided message: {o}");
        o.WasToStringCalled.Should().BeFalse();
    }

    public async Task FloatAreEqual_InterpolatedString_DifferentValues_ShouldFail()
    {
        DummyClassTrackingToStringCalls o = new();
        DateTime dateTime = DateTime.Now;
        Func<Task> action = async () => Assert.AreEqual(1.0f, 1.1f, 0.001f, $"User-provided message. {o}, {o,35}, {await GetHelloStringAsync()}, {new DummyIFormattable()}, {dateTime:tt}, {dateTime,5:tt}");
        (await action.Should().ThrowAsync<Exception>())
            .And.Message.Should().Be($"Assert.AreEqual failed. Expected a difference no greater than <0.001> between expected value <1> and actual value <1.1>. 'expected' expression: '1.0f', 'actual' expression: '1.1f'. User-provided message. DummyClassTrackingToStringCalls,     DummyClassTrackingToStringCalls, Hello, DummyIFormattable.ToString(), {string.Format(null, "{0:tt}", dateTime)}, {string.Format(null, "{0,5:tt}", dateTime)}");
        o.WasToStringCalled.Should().BeTrue();
    }

    public void FloatAreNotEqual_InterpolatedString_DifferentValues_ShouldPass()
    {
        DummyClassTrackingToStringCalls o = new();
        Assert.AreNotEqual(1.0f, 1.1f, 0.001f, $"User-provided message: {o}");
        o.WasToStringCalled.Should().BeFalse();
    }

    public async Task FloatAreNotEqual_InterpolatedString_SameValues_ShouldFail()
    {
        DummyClassTrackingToStringCalls o = new();
        DateTime dateTime = DateTime.Now;
        Func<Task> action = async () => Assert.AreNotEqual(1.0f, 1.1f, 0.2f, $"User-provided message. {o}, {o,35}, {await GetHelloStringAsync()}, {new DummyIFormattable()}, {dateTime:tt}, {dateTime,5:tt}");
        (await action.Should().ThrowAsync<Exception>())
            .And.Message.Should().Be($"Assert.AreNotEqual failed. Expected a difference greater than <0.2> between expected value <1> and actual value <1.1>. 'notExpected' expression: '1.0f', 'actual' expression: '1.1f'. User-provided message. DummyClassTrackingToStringCalls,     DummyClassTrackingToStringCalls, Hello, DummyIFormattable.ToString(), {string.Format(null, "{0:tt}", dateTime)}, {string.Format(null, "{0,5:tt}", dateTime)}");
        o.WasToStringCalled.Should().BeTrue();
    }

    public void DecimalAreEqual_InterpolatedString_EqualValues_ShouldPass()
    {
        DummyClassTrackingToStringCalls o = new();
        Assert.AreEqual(1.0m, 1.1m, delta: 0.2m, $"User-provided message: {o}");
        o.WasToStringCalled.Should().BeFalse();
    }

    public async Task DecimalAreEqual_InterpolatedString_DifferentValues_ShouldFail()
    {
        DummyClassTrackingToStringCalls o = new();
        DateTime dateTime = DateTime.Now;
        Func<Task> action = async () => Assert.AreEqual(1.0m, 1.1m, 0.001m, $"User-provided message. {o}, {o,35}, {await GetHelloStringAsync()}, {new DummyIFormattable()}, {dateTime:tt}, {dateTime,5:tt}");
        (await action.Should().ThrowAsync<Exception>())
            .And.Message.Should().Be($"Assert.AreEqual failed. Expected a difference no greater than <0.001> between expected value <1.0> and actual value <1.1>. 'expected' expression: '1.0m', 'actual' expression: '1.1m'. User-provided message. DummyClassTrackingToStringCalls,     DummyClassTrackingToStringCalls, Hello, DummyIFormattable.ToString(), {string.Format(null, "{0:tt}", dateTime)}, {string.Format(null, "{0,5:tt}", dateTime)}");
        o.WasToStringCalled.Should().BeTrue();
    }

    public void DecimalAreNotEqual_InterpolatedString_DifferentValues_ShouldPass()
    {
        DummyClassTrackingToStringCalls o = new();
        Assert.AreNotEqual(1.0m, 1.1m, 0.001m, $"User-provided message: {o}");
        o.WasToStringCalled.Should().BeFalse();
    }

    public async Task DecimalAreNotEqual_InterpolatedString_SameValues_ShouldFail()
    {
        DummyClassTrackingToStringCalls o = new();
        DateTime dateTime = DateTime.Now;
        Func<Task> action = async () => Assert.AreNotEqual(1.0m, 1.1m, 0.2m, $"User-provided message. {o}, {o,35}, {await GetHelloStringAsync()}, {new DummyIFormattable()}, {dateTime:tt}, {dateTime,5:tt}");
        (await action.Should().ThrowAsync<Exception>())
            .And.Message.Should().Be($"Assert.AreNotEqual failed. Expected a difference greater than <0.2> between expected value <1.0> and actual value <1.1>. 'notExpected' expression: '1.0m', 'actual' expression: '1.1m'. User-provided message. DummyClassTrackingToStringCalls,     DummyClassTrackingToStringCalls, Hello, DummyIFormattable.ToString(), {string.Format(null, "{0:tt}", dateTime)}, {string.Format(null, "{0,5:tt}", dateTime)}");
        o.WasToStringCalled.Should().BeTrue();
    }

    public void LongAreEqual_InterpolatedString_EqualValues_ShouldPass()
    {
        DummyClassTrackingToStringCalls o = new();
        Assert.AreEqual(1L, 2L, delta: 1L, $"User-provided message: {o}");
        o.WasToStringCalled.Should().BeFalse();
    }

    public async Task LongAreEqual_InterpolatedString_DifferentValues_ShouldFail()
    {
        DummyClassTrackingToStringCalls o = new();
        DateTime dateTime = DateTime.Now;
        Func<Task> action = async () => Assert.AreEqual(1L, 2L, 0L, $"User-provided message. {o}, {o,35}, {await GetHelloStringAsync()}, {new DummyIFormattable()}, {dateTime:tt}, {dateTime,5:tt}");
        (await action.Should().ThrowAsync<Exception>())
            .And.Message.Should().Be($"Assert.AreEqual failed. Expected a difference no greater than <0> between expected value <1> and actual value <2>. 'expected' expression: '1L', 'actual' expression: '2L'. User-provided message. DummyClassTrackingToStringCalls,     DummyClassTrackingToStringCalls, Hello, DummyIFormattable.ToString(), {string.Format(null, "{0:tt}", dateTime)}, {string.Format(null, "{0,5:tt}", dateTime)}");
        o.WasToStringCalled.Should().BeTrue();
    }

    public void LongAreNotEqual_InterpolatedString_DifferentValues_ShouldPass()
    {
        DummyClassTrackingToStringCalls o = new();
        Assert.AreNotEqual(1L, 2L, 0L, $"User-provided message: {o}");
        o.WasToStringCalled.Should().BeFalse();
    }

    public async Task LongAreNotEqual_InterpolatedString_SameValues_ShouldFail()
    {
        DummyClassTrackingToStringCalls o = new();
        DateTime dateTime = DateTime.Now;
        Func<Task> action = async () => Assert.AreNotEqual(1L, 2L, 1L, $"User-provided message. {o}, {o,35}, {await GetHelloStringAsync()}, {new DummyIFormattable()}, {dateTime:tt}, {dateTime,5:tt}");
        (await action.Should().ThrowAsync<Exception>())
            .And.Message.Should().Be($"Assert.AreNotEqual failed. Expected a difference greater than <1> between expected value <1> and actual value <2>. 'notExpected' expression: '1L', 'actual' expression: '2L'. User-provided message. DummyClassTrackingToStringCalls,     DummyClassTrackingToStringCalls, Hello, DummyIFormattable.ToString(), {string.Format(null, "{0:tt}", dateTime)}, {string.Format(null, "{0,5:tt}", dateTime)}");
        o.WasToStringCalled.Should().BeTrue();
    }

    public void DoubleAreEqual_InterpolatedString_EqualValues_ShouldPass()
    {
        DummyClassTrackingToStringCalls o = new();
        Assert.AreEqual(1.0d, 1.1d, delta: 0.2d, $"User-provided message: {o}");
        o.WasToStringCalled.Should().BeFalse();
    }

    public async Task DoubleAreEqual_InterpolatedString_DifferentValues_ShouldFail()
    {
        DummyClassTrackingToStringCalls o = new();
        DateTime dateTime = DateTime.Now;
        Func<Task> action = async () => Assert.AreEqual(1.0d, 1.1d, 0.001d, $"User-provided message. {o}, {o,35}, {await GetHelloStringAsync()}, {new DummyIFormattable()}, {dateTime:tt}, {dateTime,5:tt}");
        (await action.Should().ThrowAsync<Exception>())
            .And.Message.Should().Be($"Assert.AreEqual failed. Expected a difference no greater than <0.001> between expected value <1> and actual value <1.1>. 'expected' expression: '1.0d', 'actual' expression: '1.1d'. User-provided message. DummyClassTrackingToStringCalls,     DummyClassTrackingToStringCalls, Hello, DummyIFormattable.ToString(), {string.Format(null, "{0:tt}", dateTime)}, {string.Format(null, "{0,5:tt}", dateTime)}");
        o.WasToStringCalled.Should().BeTrue();
    }

    public void DoubleAreNotEqual_InterpolatedString_DifferentValues_ShouldPass()
    {
        DummyClassTrackingToStringCalls o = new();
        Assert.AreNotEqual(1.0d, 1.1d, 0.001d, $"User-provided message: {o}");
        o.WasToStringCalled.Should().BeFalse();
    }

    public async Task DoubleAreNotEqual_InterpolatedString_SameValues_ShouldFail()
    {
        DummyClassTrackingToStringCalls o = new();
        DateTime dateTime = DateTime.Now;
        Func<Task> action = async () => Assert.AreNotEqual(1.0d, 1.1d, 0.2d, $"User-provided message. {o}, {o,35}, {await GetHelloStringAsync()}, {new DummyIFormattable()}, {dateTime:tt}, {dateTime,5:tt}");
        (await action.Should().ThrowAsync<Exception>())
            .And.Message.Should().Be($"Assert.AreNotEqual failed. Expected a difference greater than <0.2> between expected value <1> and actual value <1.1>. 'notExpected' expression: '1.0d', 'actual' expression: '1.1d'. User-provided message. DummyClassTrackingToStringCalls,     DummyClassTrackingToStringCalls, Hello, DummyIFormattable.ToString(), {string.Format(null, "{0:tt}", dateTime)}, {string.Format(null, "{0,5:tt}", dateTime)}");
        o.WasToStringCalled.Should().BeTrue();
    }

    public void FloatAreEqual_ExpectedIsNumeric_ActualIsNumeric_ExpectedAndActualNotEquals_DeltaIsNaN_ShouldFail()
    {
        Action action = () => Assert.AreEqual(1.0f, 2.0f, float.NaN);
        action.Should().Throw<Exception>().And.Message.Should().BeOneOf(
            """
            Specified argument was out of the range of valid values.
            Parameter name: delta
            """,
            "Specified argument was out of the range of valid values. (Parameter 'delta')");
    }

    public void FloatAreEqual_ExpectedIsNumeric_ActualIsNumeric_ExpectedAndActualEquals_DeltaIsNaN_ShouldFail()
    {
        Action action = () => Assert.AreEqual(1.0f, 1.0f, float.NaN);
        action.Should().Throw<Exception>().And.Message.Should().BeOneOf(
            """
            Specified argument was out of the range of valid values.
            Parameter name: delta
            """,
            "Specified argument was out of the range of valid values. (Parameter 'delta')");
    }

    public void FloatAreEqual_ExpectedIsNaN_ActualIsNumeric_DeltaIsNaN_ShouldFail()
    {
        Action action = () => Assert.AreEqual(float.NaN, 1.0f, float.NaN);
        action.Should().Throw<Exception>().And.Message.Should().BeOneOf(
            """
            Specified argument was out of the range of valid values.
            Parameter name: delta
            """,
            "Specified argument was out of the range of valid values. (Parameter 'delta')");
    }

    public void FloatAreEqual_ExpectedIsNaN_ActualIsNaN_DeltaIsNaN_ShouldFail()
    {
        Action action = () => Assert.AreEqual(float.NaN, float.NaN, float.NaN);
        action.Should().Throw<Exception>().And.Message.Should().BeOneOf(
            """
            Specified argument was out of the range of valid values.
            Parameter name: delta
            """,
            "Specified argument was out of the range of valid values. (Parameter 'delta')");
    }

    public void FloatAreEqual_ExpectedIsNumeric_ActualIsNaN_DeltaIsNaN_ShouldFail()
    {
        Action action = () => Assert.AreEqual(1.0f, float.NaN, float.NaN);
        action.Should().Throw<Exception>().And.Message.Should().BeOneOf(
            """
            Specified argument was out of the range of valid values.
            Parameter name: delta
            """,
            "Specified argument was out of the range of valid values. (Parameter 'delta')");
    }

    public void FloatAreEqual_ExpectedIsNumeric_ActualIsNumeric_ExpectedAndActualNotEquals_DeltaIsNegative_ShouldFail()
    {
        Action action = () => Assert.AreEqual(1.0f, 2.0f, -1.0f);
        action.Should().Throw<Exception>().And.Message.Should().BeOneOf(
            """
            Specified argument was out of the range of valid values.
            Parameter name: delta
            """,
            "Specified argument was out of the range of valid values. (Parameter 'delta')");
    }

    public void FloatAreEqual_ExpectedIsNumeric_ActualIsNumeric_ExpectedAndActualEquals_DeltaIsNegative_ShouldFail()
    {
        Action action = () => Assert.AreEqual(1.0f, 1.0f, -1.0f);
        action.Should().Throw<Exception>().And.Message.Should().BeOneOf(
            """
            Specified argument was out of the range of valid values.
            Parameter name: delta
            """,
            "Specified argument was out of the range of valid values. (Parameter 'delta')");
    }

    public void FloatAreEqual_ExpectedIsNaN_ActualIsNumeric_DeltaIsNegative_ShouldFail()
    {
        Action action = () => Assert.AreEqual(float.NaN, 1.0f, -1.0f);
        action.Should().Throw<Exception>().And.Message.Should().BeOneOf(
            """
            Specified argument was out of the range of valid values.
            Parameter name: delta
            """,
            "Specified argument was out of the range of valid values. (Parameter 'delta')");
    }

    public void FloatAreEqual_ExpectedIsNaN_ActualIsNaN_DeltaIsNegative_ShouldFail()
    {
        Action action = () => Assert.AreEqual(float.NaN, float.NaN, -1.0f);
        action.Should().Throw<Exception>().And.Message.Should().BeOneOf(
            """
            Specified argument was out of the range of valid values.
            Parameter name: delta
            """,
            "Specified argument was out of the range of valid values. (Parameter 'delta')");
    }

    public void FloatAreEqual_ExpectedIsNumeric_ActualIsNaN_DeltaIsNegative_ShouldFail()
    {
        Action action = () => Assert.AreEqual(1.0f, float.NaN, -1.0f);
        action.Should().Throw<Exception>().And.Message.Should().BeOneOf(
            """
            Specified argument was out of the range of valid values.
            Parameter name: delta
            """,
            "Specified argument was out of the range of valid values. (Parameter 'delta')");
    }

    public void FloatAreEqual_ExpectedIsNumeric_ActualIsNumeric_ExpectedAndActualNotEquals_DeltaIsNegativeInf_ShouldFail()
    {
        Action action = () => Assert.AreEqual(1.0f, 2.0f, float.NegativeInfinity);
        action.Should().Throw<Exception>().And.Message.Should().BeOneOf(
            """
            Specified argument was out of the range of valid values.
            Parameter name: delta
            """,
            "Specified argument was out of the range of valid values. (Parameter 'delta')");
    }

    public void FloatAreEqual_ExpectedIsNumeric_ActualIsNumeric_ExpectedAndActualEquals_DeltaIsNegativeInf_ShouldFail()
    {
        Action action = () => Assert.AreEqual(1.0f, 1.0f, float.NegativeInfinity);
        action.Should().Throw<Exception>().And.Message.Should().BeOneOf(
            """
            Specified argument was out of the range of valid values.
            Parameter name: delta
            """,
            "Specified argument was out of the range of valid values. (Parameter 'delta')");
    }

    public void FloatAreEqual_ExpectedIsNaN_ActualIsNumeric_DeltaIsNegativeInf_ShouldFail()
    {
        Action action = () => Assert.AreEqual(float.NaN, 1.0f, float.NegativeInfinity);
        action.Should().Throw<Exception>().And.Message.Should().BeOneOf(
            """
            Specified argument was out of the range of valid values.
            Parameter name: delta
            """,
            "Specified argument was out of the range of valid values. (Parameter 'delta')");
    }

    public void FloatAreEqual_ExpectedIsNaN_ActualIsNaN_DeltaIsNegativeInf_ShouldFail()
    {
        Action action = () => Assert.AreEqual(float.NaN, float.NaN, -1.0f);
        action.Should().Throw<Exception>().And.Message.Should().BeOneOf(
            """
            Specified argument was out of the range of valid values.
            Parameter name: delta
            """,
            "Specified argument was out of the range of valid values. (Parameter 'delta')");
    }

    public void FloatAreEqual_ExpectedIsNumeric_ActualIsNaN_DeltaIsNegativeInf_ShouldFail()
    {
        Action action = () => Assert.AreEqual(1.0f, float.NaN, float.NegativeInfinity);
        action.Should().Throw<Exception>().And.Message.Should().BeOneOf(
            """
            Specified argument was out of the range of valid values.
            Parameter name: delta
            """,
            "Specified argument was out of the range of valid values. (Parameter 'delta')");
    }

    public void FloatAreEqual_ExpectedIsNumeric_ActualIsNumeric_ExpectedAndActualDifferenceGreaterThanDeltaPositive_DeltaIsNumeric_ShouldFail()
    {
        Action action = () => Assert.AreEqual(5.0f, 2.0f, 2.0f); // difference is 3. Delta is 2
        action.Should().Throw<Exception>().And.Message.Should().Be("Assert.AreEqual failed. Expected a difference no greater than <2> between expected value <5> and actual value <2>. 'expected' expression: '5.0f', 'actual' expression: '2.0f'.");
    }

    public void FloatAreEqual_ExpectedIsNumeric_ActualIsNumeric_ExpectedAndActualDifferenceGreaterThanDeltaNegative_DeltaIsNumeric_ShouldFail()
    {
        Action action = () => Assert.AreEqual(2.0f, 5.0f, 2.0f); // difference is -3. Delta is 2
        action.Should().Throw<Exception>().And.Message.Should().Be("Assert.AreEqual failed. Expected a difference no greater than <2> between expected value <2> and actual value <5>. 'expected' expression: '2.0f', 'actual' expression: '5.0f'.");
    }

    public void FloatAreEqual_ExpectedIsNumeric_ActualIsNumeric_ExpectedAndActualDifferenceLessThanDeltaPositive_DeltaIsNumeric_ShouldPass()
        => Assert.AreEqual(5.0f, 4.0f, 2.0f); // difference is 1. Delta is 2

    public void FloatAreEqual_ExpectedIsNumeric_ActualIsNumeric_ExpectedAndActualDifferenceLessThanDeltaNegative_DeltaIsNumeric_ShouldFail()
        => Assert.AreEqual(4.0f, 5.0f, 2.0f); // difference is -1. Delta is 2

    public void FloatAreEqual_ExpectedIsNumeric_ActualIsNaN_DeltaIsNumeric_ShouldFail()
    {
        Action action = () => Assert.AreEqual(5.0f, float.NaN, 2.0f);
        action.Should().Throw<Exception>().And.Message.Should().Be("Assert.AreEqual failed. Expected a difference no greater than <2> between expected value <5> and actual value <NaN>. 'expected' expression: '5.0f', 'actual' expression: 'float.NaN'.");
    }

    public void FloatAreEqual_ExpectedIsNaN_ActualIsNumeric_DeltaIsNumeric_ShouldFail()
    {
        Action action = () => Assert.AreEqual(float.NaN, 5.0f, 2.0f);
        action.Should().Throw<Exception>().And.Message.Should().Be("Assert.AreEqual failed. Expected a difference no greater than <2> between expected value <NaN> and actual value <5>. 'expected' expression: 'float.NaN', 'actual' expression: '5.0f'.");
    }

    public void FloatAreEqual_ExpectedIsNaN_ActualIsNaN_DeltaIsNumeric_ShouldPass()
        => Assert.AreEqual(float.NaN, float.NaN, 2.0f);

    public void FloatAreNotEqual_ExpectedIsNumeric_ActualIsNumeric_ExpectedAndActualNotEquals_DeltaIsNaN_ShouldFail()
    {
        Action action = () => Assert.AreNotEqual(1.0f, 2.0f, float.NaN);
        action.Should().Throw<Exception>()
            .And.Message.Should().BeOneOf(
            """
            Specified argument was out of the range of valid values.
            Parameter name: delta
            """,
            "Specified argument was out of the range of valid values. (Parameter 'delta')");
    }

    public void FloatAreNotEqual_ExpectedIsNumeric_ActualIsNumeric_ExpectedAndActualEquals_DeltaIsNaN_ShouldFail()
    {
        Action action = () => Assert.AreNotEqual(1.0f, 1.0f, float.NaN);
        action.Should().Throw<Exception>()
            .And.Message.Should().BeOneOf(
            """
            Specified argument was out of the range of valid values.
            Parameter name: delta
            """,
            "Specified argument was out of the range of valid values. (Parameter 'delta')");
    }

    public void FloatAreNotEqual_ExpectedIsNaN_ActualIsNumeric_DeltaIsNaN_ShouldFail()
    {
        Action action = () => Assert.AreNotEqual(float.NaN, 1.0f, float.NaN);
        action.Should().Throw<Exception>().And.Message.Should().BeOneOf(
            """
            Specified argument was out of the range of valid values.
            Parameter name: delta
            """,
            "Specified argument was out of the range of valid values. (Parameter 'delta')");
    }

    public void FloatAreNotEqual_ExpectedIsNaN_ActualIsNaN_DeltaIsNaN_ShouldFail()
    {
        Action action = () => Assert.AreNotEqual(float.NaN, float.NaN, float.NaN);
        action.Should().Throw<Exception>().And.Message.Should().BeOneOf(
            """
            Specified argument was out of the range of valid values.
            Parameter name: delta
            """,
            "Specified argument was out of the range of valid values. (Parameter 'delta')");
    }

    public void FloatAreNotEqual_ExpectedIsNumeric_ActualIsNaN_DeltaIsNaN_ShouldFail()
    {
        Action action = () => Assert.AreNotEqual(1.0f, float.NaN, float.NaN);
        action.Should().Throw<Exception>().And.Message.Should().BeOneOf(
            """
            Specified argument was out of the range of valid values.
            Parameter name: delta
            """,
            "Specified argument was out of the range of valid values. (Parameter 'delta')");
    }

    public void FloatAreNotEqual_ExpectedIsNumeric_ActualIsNumeric_ExpectedAndActualNotEquals_DeltaIsNegative_ShouldFail()
    {
        Action action = () => Assert.AreNotEqual(1.0f, 2.0f, -1.0f);
        action.Should().Throw<Exception>()
            .And.Message.Should().BeOneOf(
            """
            Specified argument was out of the range of valid values.
            Parameter name: delta
            """,
            "Specified argument was out of the range of valid values. (Parameter 'delta')");
    }

    public void FloatAreNotEqual_ExpectedIsNumeric_ActualIsNumeric_ExpectedAndActualEquals_DeltaIsNegative_ShouldFail()
    {
        Action action = () => Assert.AreNotEqual(1.0f, 1.0f, -1.0f);
        action.Should().Throw<Exception>().And.Message.Should().BeOneOf(
            """
            Specified argument was out of the range of valid values.
            Parameter name: delta
            """,
            "Specified argument was out of the range of valid values. (Parameter 'delta')");
    }

    public void FloatAreNotEqual_ExpectedIsNaN_ActualIsNumeric_DeltaIsNegative_ShouldFail()
    {
        Action action = () => Assert.AreNotEqual(float.NaN, 1.0f, -1.0f);
        action.Should().Throw<Exception>().And.Message.Should().BeOneOf(
            """
            Specified argument was out of the range of valid values.
            Parameter name: delta
            """,
            "Specified argument was out of the range of valid values. (Parameter 'delta')");
    }

    public void FloatAreNotEqual_ExpectedIsNaN_ActualIsNaN_DeltaIsNegative_ShouldFail()
    {
        Action action = () => Assert.AreNotEqual(float.NaN, float.NaN, -1.0f);
        action.Should().Throw<Exception>().And.Message.Should().BeOneOf(
            """
            Specified argument was out of the range of valid values.
            Parameter name: delta
            """,
            "Specified argument was out of the range of valid values. (Parameter 'delta')");
    }

    public void FloatAreNotEqual_ExpectedIsNumeric_ActualIsNaN_DeltaIsNegative_ShouldFail()
    {
        Action action = () => Assert.AreNotEqual(1.0f, float.NaN, -1.0f);
        action.Should().Throw<Exception>().And.Message.Should().BeOneOf(
            """
            Specified argument was out of the range of valid values.
            Parameter name: delta
            """,
            "Specified argument was out of the range of valid values. (Parameter 'delta')");
    }

    public void FloatAreNotEqual_ExpectedIsNumeric_ActualIsNumeric_ExpectedAndActualNotEquals_DeltaIsNegativeInf_ShouldFail()
    {
        Action action = () => Assert.AreNotEqual(1.0f, 2.0f, -1.0f);
        action.Should().Throw<Exception>()
            .And.Message.Should().BeOneOf(
            """
            Specified argument was out of the range of valid values.
            Parameter name: delta
            """,
            "Specified argument was out of the range of valid values. (Parameter 'delta')");
    }

    public void FloatAreNotEqual_ExpectedIsNumeric_ActualIsNumeric_ExpectedAndActualEquals_DeltaIsNegativeInf_ShouldFail()
    {
        Action action = () => Assert.AreNotEqual(1.0f, 1.0f, float.NegativeInfinity);
        action.Should().Throw<Exception>()
            .And.Message.Should().BeOneOf(
            """
            Specified argument was out of the range of valid values.
            Parameter name: delta
            """,
            "Specified argument was out of the range of valid values. (Parameter 'delta')");
    }

    public void FloatAreNotEqual_ExpectedIsNaN_ActualIsNumeric_DeltaIsNegativeInf_ShouldFail()
    {
        Action action = () => Assert.AreNotEqual(float.NaN, 1.0f, float.NegativeInfinity);
        action.Should().Throw<Exception>()
            .And.Message.Should().BeOneOf(
            """
            Specified argument was out of the range of valid values.
            Parameter name: delta
            """,
            "Specified argument was out of the range of valid values. (Parameter 'delta')");
    }

    public void FloatAreNotEqual_ExpectedIsNaN_ActualIsNaN_DeltaIsNegativeInf_ShouldFail()
    {
        Action action = () => Assert.AreNotEqual(float.NaN, float.NaN, float.NegativeInfinity);
        action.Should().Throw<Exception>()
            .And.Message.Should().BeOneOf(
            """
            Specified argument was out of the range of valid values.
            Parameter name: delta
            """,
            "Specified argument was out of the range of valid values. (Parameter 'delta')");
    }

    public void FloatAreNotEqual_ExpectedIsNumeric_ActualIsNaN_DeltaIsNegativeInf_ShouldFail()
    {
        Action action = () => Assert.AreNotEqual(1.0f, float.NaN, float.NegativeInfinity);
        action.Should().Throw<Exception>()
            .And.Message.Should().BeOneOf(
            """
            Specified argument was out of the range of valid values.
            Parameter name: delta
            """,
            "Specified argument was out of the range of valid values. (Parameter 'delta')");
    }

    public void FloatAreNotEqual_ExpectedIsNumeric_ActualIsNumeric_ExpectedAndActualDifferenceGreaterThanDeltaPositive_DeltaIsNumeric_ShouldPass()
        => Assert.AreNotEqual(5.0f, 2.0f, 2.0f); // difference is 3. Delta is 2

    public void FloatAreNotEqual_ExpectedIsNumeric_ActualIsNumeric_ExpectedAndActualDifferenceGreaterThanDeltaNegative_DeltaIsNumeric_ShouldPass()
        => Assert.AreNotEqual(2.0f, 5.0f, 2.0f); // difference is -3. Delta is 2

    public void FloatAreNotEqual_ExpectedIsNumeric_ActualIsNumeric_ExpectedAndActualDifferenceLessThanDeltaPositive_DeltaIsNumeric_ShouldFail()
    {
        Action action = () => Assert.AreNotEqual(5.0f, 4.0f, 2.0f); // difference is 1. Delta is 2
        action.Should().Throw<Exception>().And.Message.Should().Be("Assert.AreNotEqual failed. Expected a difference greater than <2> between expected value <5> and actual value <4>. 'notExpected' expression: '5.0f', 'actual' expression: '4.0f'.");
    }

    public void FloatAreNotEqual_ExpectedIsNumeric_ActualIsNumeric_ExpectedAndActualDifferenceLessThanDeltaNegative_DeltaIsNumeric_ShouldFail()
    {
        Action action = () => Assert.AreNotEqual(4.0f, 5.0f, 2.0f); // difference is -1. Delta is 2
        action.Should().Throw<Exception>().And.Message.Should().Be("Assert.AreNotEqual failed. Expected a difference greater than <2> between expected value <4> and actual value <5>. 'notExpected' expression: '4.0f', 'actual' expression: '5.0f'.");
    }

    public void FloatAreNotEqual_ExpectedIsNumeric_ActualIsNaN_DeltaIsNumeric_ShouldPass() => Assert.AreNotEqual(5.0f, float.NaN, 2.0f);

    public void FloatAreNotEqual_ExpectedIsNaN_ActualIsNumeric_DeltaIsNumeric_ShouldPass()
        => Assert.AreNotEqual(float.NaN, 5.0f, 2.0f);

    public void FloatAreNotEqual_ExpectedIsNaN_ActualIsNaN_DeltaIsNumeric_ShouldFail()
    {
        Action action = () => Assert.AreNotEqual(float.NaN, float.NaN, 2.0f);
        action.Should().Throw<Exception>().And.Message.Should().Be("Assert.AreNotEqual failed. Expected a difference greater than <2> between expected value <NaN> and actual value <NaN>. 'notExpected' expression: 'float.NaN', 'actual' expression: 'float.NaN'.");
    }

    public void DoubleAreEqual_ExpectedIsNumeric_ActualIsNumeric_ExpectedAndActualNotEquals_DeltaIsNaN_ShouldFail()
    {
        Action action = () => Assert.AreEqual(1.0d, 2.0d, double.NaN);
        action.Should().Throw<Exception>().And.Message.Should().BeOneOf(
            """
            Specified argument was out of the range of valid values.
            Parameter name: delta
            """,
            "Specified argument was out of the range of valid values. (Parameter 'delta')");
    }

    public void DoubleAreEqual_ExpectedIsNumeric_ActualIsNumeric_ExpectedAndActualEquals_DeltaIsNaN_ShouldFail()
    {
        Action action = () => Assert.AreEqual(1.0d, 1.0d, double.NaN);
        action.Should().Throw<Exception>().And.Message.Should().BeOneOf(
            """
            Specified argument was out of the range of valid values.
            Parameter name: delta
            """,
            "Specified argument was out of the range of valid values. (Parameter 'delta')");
    }

    public void DoubleAreEqual_ExpectedIsNaN_ActualIsNumeric_DeltaIsNaN_ShouldFail()
    {
        Action action = () => Assert.AreEqual(double.NaN, 1.0d, double.NaN);
        action.Should().Throw<Exception>()
            .And.Message.Should().BeOneOf(
            """
            Specified argument was out of the range of valid values.
            Parameter name: delta
            """,
            "Specified argument was out of the range of valid values. (Parameter 'delta')");
    }

    public void DoubleAreEqual_ExpectedIsNaN_ActualIsNaN_DeltaIsNaN_ShouldFail()
    {
        Action action = () => Assert.AreEqual(double.NaN, double.NaN, double.NaN);
        action.Should().Throw<Exception>()
            .And.Message.Should().BeOneOf(
            """
            Specified argument was out of the range of valid values.
            Parameter name: delta
            """,
            "Specified argument was out of the range of valid values. (Parameter 'delta')");
    }

    public void DoubleAreEqual_ExpectedIsNumeric_ActualIsNaN_DeltaIsNaN_ShouldFail()
    {
        Action action = () => Assert.AreEqual(1.0d, double.NaN, double.NaN);
        action.Should().Throw<Exception>()
            .And.Message.Should().BeOneOf(
            """
            Specified argument was out of the range of valid values.
            Parameter name: delta
            """,
            "Specified argument was out of the range of valid values. (Parameter 'delta')");
    }

    public void DoubleAreEqual_ExpectedIsNumeric_ActualIsNumeric_ExpectedAndActualNotEquals_DeltaIsNegative_ShouldFail()
    {
        Action action = () => Assert.AreEqual(1.0d, 2.0d, -1.0d);
        action.Should().Throw<Exception>()
            .And.Message.Should().BeOneOf(
            """
            Specified argument was out of the range of valid values.
            Parameter name: delta
            """,
            "Specified argument was out of the range of valid values. (Parameter 'delta')");
    }

    public void DoubleAreEqual_ExpectedIsNumeric_ActualIsNumeric_ExpectedAndActualEquals_DeltaIsNegative_ShouldFail()
    {
        Action action = () => Assert.AreEqual(1.0d, 1.0d, -1.0d);
        action.Should().Throw<Exception>()
            .And.Message.Should().BeOneOf(
            """
            Specified argument was out of the range of valid values.
            Parameter name: delta
            """,
            "Specified argument was out of the range of valid values. (Parameter 'delta')");
    }

    public void DoubleAreEqual_ExpectedIsNaN_ActualIsNumeric_DeltaIsNegative_ShouldFail()
    {
        Action action = () => Assert.AreEqual(double.NaN, 1.0d, -1.0d);
        action.Should().Throw<Exception>()
            .And.Message.Should().BeOneOf(
            """
            Specified argument was out of the range of valid values.
            Parameter name: delta
            """,
            "Specified argument was out of the range of valid values. (Parameter 'delta')");
    }

    public void DoubleAreEqual_ExpectedIsNaN_ActualIsNaN_DeltaIsNegative_ShouldFail()
    {
        Action action = () => Assert.AreEqual(double.NaN, double.NaN, -1.0d);
        action.Should().Throw<Exception>()
            .And.Message.Should().BeOneOf(
            """
            Specified argument was out of the range of valid values.
            Parameter name: delta
            """,
            "Specified argument was out of the range of valid values. (Parameter 'delta')");
    }

    public void DoubleAreEqual_ExpectedIsNumeric_ActualIsNaN_DeltaIsNegative_ShouldFail()
    {
        Action action = () => Assert.AreEqual(1.0d, double.NaN, -1.0d);
        action.Should().Throw<Exception>()
            .And.Message.Should().BeOneOf(
            """
            Specified argument was out of the range of valid values.
            Parameter name: delta
            """,
            "Specified argument was out of the range of valid values. (Parameter 'delta')");
    }

    public void DoubleAreEqual_ExpectedIsNumeric_ActualIsNumeric_ExpectedAndActualNotEquals_DeltaIsNegativeInf_ShouldFail()
    {
        Action action = () => Assert.AreEqual(1.0d, 2.0d, double.NegativeInfinity);
        action.Should().Throw<Exception>()
            .And.Message.Should().BeOneOf(
            """
            Specified argument was out of the range of valid values.
            Parameter name: delta
            """,
            "Specified argument was out of the range of valid values. (Parameter 'delta')");
    }

    public void DoubleAreEqual_ExpectedIsNumeric_ActualIsNumeric_ExpectedAndActualEquals_DeltaIsNegativeInf_ShouldFail()
    {
        Action action = () => Assert.AreEqual(1.0d, 1.0d, double.NegativeInfinity);
        action.Should().Throw<Exception>()
            .And.Message.Should().BeOneOf(
            """
            Specified argument was out of the range of valid values.
            Parameter name: delta
            """,
            "Specified argument was out of the range of valid values. (Parameter 'delta')");
    }

    public void DoubleAreEqual_ExpectedIsNaN_ActualIsNumeric_DeltaIsNegativeInf_ShouldFail()
    {
        Action action = () => Assert.AreEqual(double.NaN, 1.0d, double.NegativeInfinity);
        action.Should().Throw<Exception>()
            .And.Message.Should().BeOneOf(
            """
            Specified argument was out of the range of valid values.
            Parameter name: delta
            """,
            "Specified argument was out of the range of valid values. (Parameter 'delta')");
    }

    public void DoubleAreEqual_ExpectedIsNaN_ActualIsNaN_DeltaIsNegativeInf_ShouldFail()
    {
        Action action = () => Assert.AreEqual(double.NaN, double.NaN, double.NegativeInfinity);
        action.Should().Throw<Exception>()
            .And.Message.Should().BeOneOf(
            """
            Specified argument was out of the range of valid values.
            Parameter name: delta
            """, "Specified argument was out of the range of valid values. (Parameter 'delta')");
    }

    public void DoubleAreEqual_ExpectedIsNumeric_ActualIsNaN_DeltaIsNegativeInf_ShouldFail()
    {
        Action action = () => Assert.AreEqual(1.0d, double.NaN, double.NegativeInfinity);
        action.Should().Throw<Exception>()
            .And.Message.Should().BeOneOf(
            """
            Specified argument was out of the range of valid values.
            Parameter name: delta
            """,
            "Specified argument was out of the range of valid values. (Parameter 'delta')");
    }

    public void DoubleAreEqual_ExpectedIsNumeric_ActualIsNumeric_ExpectedAndActualDifferenceGreaterThanDeltaPositive_DeltaIsNumeric_ShouldFail()
    {
        Action action = () => Assert.AreEqual(5.0d, 2.0d, 2.0d); // difference is 3. Delta is 2
        action.Should().Throw<Exception>().And
            .Message.Should().Be("Assert.AreEqual failed. Expected a difference no greater than <2> between expected value <5> and actual value <2>. 'expected' expression: '5.0d', 'actual' expression: '2.0d'.");
    }

    public void DoubleAreEqual_ExpectedIsNumeric_ActualIsNumeric_ExpectedAndActualDifferenceGreaterThanDeltaNegative_DeltaIsNumeric_ShouldFail()
    {
        Action action = () => Assert.AreEqual(2.0d, 5.0d, 2.0d); // difference is -3. Delta is 2
        action.Should().Throw<Exception>().And
            .Message.Should().Be("Assert.AreEqual failed. Expected a difference no greater than <2> between expected value <2> and actual value <5>. 'expected' expression: '2.0d', 'actual' expression: '5.0d'.");
    }

    public void DoubleAreEqual_ExpectedIsNumeric_ActualIsNumeric_ExpectedAndActualDifferenceLessThanDeltaPositive_DeltaIsNumeric_ShouldPass()
        => Assert.AreEqual(5.0d, 4.0d, 2.0d); // difference is 1. Delta is 2

    public void DoubleAreEqual_ExpectedIsNumeric_ActualIsNumeric_ExpectedAndActualDifferenceLessThanDeltaNegative_DeltaIsNumeric_ShouldFail()
        => Assert.AreEqual(4.0d, 5.0d, 2.0d); // difference is -1. Delta is 2

    public void DoubleAreEqual_ExpectedIsNumeric_ActualIsNaN_DeltaIsNumeric_ShouldFail()
    {
        Action action = () => Assert.AreEqual(5.0d, double.NaN, 2.0d);
        action.Should().Throw<Exception>().And
            .Message.Should().Be("Assert.AreEqual failed. Expected a difference no greater than <2> between expected value <5> and actual value <NaN>. 'expected' expression: '5.0d', 'actual' expression: 'double.NaN'.");
    }

    public void DoubleAreEqual_ExpectedIsNaN_ActualIsNumeric_DeltaIsNumeric_ShouldFail()
    {
        Action action = () => Assert.AreEqual(double.NaN, 5.0d, 2.0d);
        action.Should().Throw<Exception>().And
            .Message.Should().Be("Assert.AreEqual failed. Expected a difference no greater than <2> between expected value <NaN> and actual value <5>. 'expected' expression: 'double.NaN', 'actual' expression: '5.0d'.");
    }

    public void DoubleAreEqual_ExpectedIsNaN_ActualIsNaN_DeltaIsNumeric_ShouldPass()
        => Assert.AreEqual(double.NaN, double.NaN, 2.0d);

    public void DoubleAreNotEqual_ExpectedIsNumeric_ActualIsNumeric_ExpectedAndActualNotEquals_DeltaIsNaN_ShouldFail()
    {
        Action action = () => Assert.AreNotEqual(1.0d, 2.0d, double.NaN);
        action.Should().Throw<Exception>()
            .And.Message.Should().BeOneOf(
            """
            Specified argument was out of the range of valid values.
            Parameter name: delta
            """,
            "Specified argument was out of the range of valid values. (Parameter 'delta')");
    }

    public void DoubleAreNotEqual_ExpectedIsNumeric_ActualIsNumeric_ExpectedAndActualEquals_DeltaIsNaN_ShouldFail()
    {
        Action action = () => Assert.AreNotEqual(1.0d, 1.0d, double.NaN);
        action.Should().Throw<Exception>()
            .And.Message.Should().BeOneOf(
            """
            Specified argument was out of the range of valid values.
            Parameter name: delta
            """,
            "Specified argument was out of the range of valid values. (Parameter 'delta')");
    }

    public void DoubleAreNotEqual_ExpectedIsNaN_ActualIsNumeric_DeltaIsNaN_ShouldFail()
    {
        Action action = () => Assert.AreNotEqual(double.NaN, 1.0d, double.NaN);
        action.Should().Throw<Exception>()
            .And.Message.Should().BeOneOf(
            """
            Specified argument was out of the range of valid values.
            Parameter name: delta
            """,
            "Specified argument was out of the range of valid values. (Parameter 'delta')");
    }

    public void DoubleAreNotEqual_ExpectedIsNaN_ActualIsNaN_DeltaIsNaN_ShouldFail()
    {
        Action action = () => Assert.AreNotEqual(double.NaN, double.NaN, double.NaN);
        action.Should().Throw<Exception>()
            .And.Message.Should().BeOneOf(
            """
            Specified argument was out of the range of valid values.
            Parameter name: delta
            """,
            "Specified argument was out of the range of valid values. (Parameter 'delta')");
    }

    public void DoubleAreNotEqual_ExpectedIsNumeric_ActualIsNaN_DeltaIsNaN_ShouldFail()
    {
        Action action = () => Assert.AreNotEqual(1.0d, double.NaN, double.NaN);
        action.Should().Throw<Exception>()
            .And.Message.Should().BeOneOf(
            """
            Specified argument was out of the range of valid values.
            Parameter name: delta
            """,
            "Specified argument was out of the range of valid values. (Parameter 'delta')");
    }

    public void DoubleAreNotEqual_ExpectedIsNumeric_ActualIsNumeric_ExpectedAndActualNotEquals_DeltaIsNegative_ShouldFail()
    {
        Action action = () => Assert.AreNotEqual(1.0d, 2.0d, -1.0d);
        action.Should().Throw<Exception>()
            .And.Message.Should().BeOneOf(
            """
            Specified argument was out of the range of valid values.
            Parameter name: delta
            """,
            "Specified argument was out of the range of valid values. (Parameter 'delta')");
    }

    public void DoubleAreNotEqual_ExpectedIsNumeric_ActualIsNumeric_ExpectedAndActualEquals_DeltaIsNegative_ShouldFail()
    {
        Action action = () => Assert.AreNotEqual(1.0d, 1.0d, -1.0d);
        action.Should().Throw<Exception>()
            .And.Message.Should().BeOneOf(
            """
            Specified argument was out of the range of valid values.
            Parameter name: delta
            """,
            "Specified argument was out of the range of valid values. (Parameter 'delta')");
    }

    public void DoubleAreNotEqual_ExpectedIsNaN_ActualIsNumeric_DeltaIsNegative_ShouldFail()
    {
        Action action = () => Assert.AreNotEqual(double.NaN, 1.0d, -1.0d);
        action.Should().Throw<Exception>()
            .And.Message.Should().BeOneOf(
            """
            Specified argument was out of the range of valid values.
            Parameter name: delta
            """,
            "Specified argument was out of the range of valid values. (Parameter 'delta')");
    }

    public void DoubleAreNotEqual_ExpectedIsNaN_ActualIsNaN_DeltaIsNegative_ShouldFail()
    {
        Action action = () => Assert.AreNotEqual(double.NaN, double.NaN, -1.0d);
        action.Should().Throw<Exception>()
            .And.Message.Should().BeOneOf(
            """
            Specified argument was out of the range of valid values.
            Parameter name: delta
            """,
            "Specified argument was out of the range of valid values. (Parameter 'delta')");
    }

    public void DoubleAreNotEqual_ExpectedIsNumeric_ActualIsNaN_DeltaIsNegative_ShouldFail()
    {
        Action action = () => Assert.AreNotEqual(1.0d, double.NaN, -1.0d);
        action.Should().Throw<Exception>()
            .And.Message.Should().BeOneOf(
            """
            Specified argument was out of the range of valid values.
            Parameter name: delta
            """,
            "Specified argument was out of the range of valid values. (Parameter 'delta')");
    }

    public void DoubleAreNotEqual_ExpectedIsNumeric_ActualIsNumeric_ExpectedAndActualNotEquals_DeltaIsNegativeInf_ShouldFail()
    {
        Action action = () => Assert.AreNotEqual(1.0d, 2.0d, double.NegativeInfinity);
        action.Should().Throw<Exception>()
            .And.Message.Should().BeOneOf(
            """
            Specified argument was out of the range of valid values.
            Parameter name: delta
            """,
            "Specified argument was out of the range of valid values. (Parameter 'delta')");
    }

    public void DoubleAreNotEqual_ExpectedIsNumeric_ActualIsNumeric_ExpectedAndActualEquals_DeltaIsNegativeInf_ShouldFail()
    {
        Action action = () => Assert.AreNotEqual(1.0d, 1.0d, double.NegativeInfinity);
        action.Should().Throw<Exception>()
            .And.Message.Should().BeOneOf(
            """
            Specified argument was out of the range of valid values.
            Parameter name: delta
            """,
            "Specified argument was out of the range of valid values. (Parameter 'delta')");
    }

    public void DoubleAreNotEqual_ExpectedIsNaN_ActualIsNumeric_DeltaIsNegativeInf_ShouldFail()
    {
        Action action = () => Assert.AreNotEqual(double.NaN, 1.0d, double.NegativeInfinity);
        action.Should().Throw<Exception>()
            .And.Message.Should().BeOneOf(
            """
            Specified argument was out of the range of valid values.
            Parameter name: delta
            """,
            "Specified argument was out of the range of valid values. (Parameter 'delta')");
    }

    public void DoubleAreNotEqual_ExpectedIsNaN_ActualIsNaN_DeltaIsNegativeInf_ShouldFail()
    {
        Action action = () => Assert.AreNotEqual(double.NaN, double.NaN, double.NegativeInfinity);
        action.Should().Throw<Exception>().And
            .Message.Should().BeOneOf(
                """
                Specified argument was out of the range of valid values.
                Parameter name: delta
                """,
                "Specified argument was out of the range of valid values. (Parameter 'delta')");
    }

    public void DoubleAreNotEqual_ExpectedIsNumeric_ActualIsNaN_DeltaIsNegativeInf_ShouldFail()
    {
        Action action = () => Assert.AreNotEqual(1.0d, double.NaN, double.NegativeInfinity);
        action.Should().Throw<Exception>().And
            .Message.Should().BeOneOf(
                """
                Specified argument was out of the range of valid values.
                Parameter name: delta
                """,
                "Specified argument was out of the range of valid values. (Parameter 'delta')");
    }

    public void DoubleAreNotEqual_ExpectedIsNumeric_ActualIsNumeric_ExpectedAndActualDifferenceGreaterThanDeltaPositive_DeltaIsNumeric_ShouldPass()
        => Assert.AreNotEqual(5.0d, 2.0d, 2.0d); // difference is 3. Delta is 2

    public void DoubleAreNotEqual_ExpectedIsNumeric_ActualIsNumeric_ExpectedAndActualDifferenceGreaterThanDeltaNegative_DeltaIsNumeric_ShouldPass()
        => Assert.AreNotEqual(2.0d, 5.0d, 2.0d); // difference is -3. Delta is 2

    public void DoubleAreNotEqual_ExpectedIsNumeric_ActualIsNumeric_ExpectedAndActualDifferenceLessThanDeltaPositive_DeltaIsNumeric_ShouldFail()
    {
        Action action = () => Assert.AreNotEqual(5.0d, 4.0d, 2.0d); // difference is 1. Delta is 2
        action.Should().Throw<Exception>().And
            .Message.Should().Be("Assert.AreNotEqual failed. Expected a difference greater than <2> between expected value <5> and actual value <4>. 'notExpected' expression: '5.0d', 'actual' expression: '4.0d'.");
    }

    public void DoubleAreNotEqual_ExpectedIsNumeric_ActualIsNumeric_ExpectedAndActualDifferenceLessThanDeltaNegative_DeltaIsNumeric_ShouldFail()
    {
        Action action = () => Assert.AreNotEqual(4.0d, 5.0d, 2.0d); // difference is -1. Delta is 2
        action.Should().Throw<Exception>().And
            .Message.Should().Be("Assert.AreNotEqual failed. Expected a difference greater than <2> between expected value <4> and actual value <5>. 'notExpected' expression: '4.0d', 'actual' expression: '5.0d'.");
    }

    public void DoubleAreNotEqual_ExpectedIsNumeric_ActualIsNaN_DeltaIsNumeric_ShouldPass() => Assert.AreNotEqual(5.0d, double.NaN, 2.0d);

    public void DoubleAreNotEqual_ExpectedIsNaN_ActualIsNumeric_DeltaIsNumeric_ShouldPass()
        => Assert.AreNotEqual(double.NaN, 5.0d, 2.0d);

    public void DoubleAreNotEqual_ExpectedIsNaN_ActualIsNaN_DeltaIsNumeric_ShouldFail()
    {
        Action action = () => Assert.AreNotEqual(double.NaN, double.NaN, 2.0d);
        action.Should().Throw<Exception>().And
            .Message.Should().Be("Assert.AreNotEqual failed. Expected a difference greater than <2> between expected value <NaN> and actual value <NaN>. 'notExpected' expression: 'double.NaN', 'actual' expression: 'double.NaN'.");
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

    public void AreEqualStringDifferenceAtBeginning()
    {
        Action action = () => Assert.AreEqual("baaa", "aaaa");
        action.Should().Throw<AssertFailedException>()
            .And.Message.Should().Be("""
            Assert.AreEqual failed. String lengths are both 4 but differ at index 0. 'expected' expression: '"baaa"', 'actual' expression: '"aaaa"'.
            Expected: "baaa"
            But was:  "aaaa"
            -----------^
            """);
    }

    public void AreEqualStringDifferenceAtEnd()
    {
        Action action = () => Assert.AreEqual("aaaa", "aaab");
        action.Should().Throw<AssertFailedException>()
            .And.Message.Should().Be("""
            Assert.AreEqual failed. String lengths are both 4 but differ at index 3. 'expected' expression: '"aaaa"', 'actual' expression: '"aaab"'.
            Expected: "aaaa"
            But was:  "aaab"
            --------------^
            """);
    }

    public void AreEqualStringWithSpecialCharactersShouldEscape()
    {
        Action action = () => Assert.AreEqual("aa\ta", "aa a");
        action.Should().Throw<AssertFailedException>()
            .And.Message.Should().Be("""
            Assert.AreEqual failed. String lengths are both 4 but differ at index 2. 'expected' expression: '"aa\ta"', 'actual' expression: '"aa a"'.
            Expected: "aa␉a"
            But was:  "aa a"
            -------------^
            """);
    }

    public void AreEqualLongStringsShouldTruncateAndShowContext()
    {
        string expected = new string('a', 100) + "b" + new string('c', 100);
        string actual = new string('a', 100) + "d" + new string('c', 100);

        Action action = () => Assert.AreEqual(expected, actual);
        action.Should().Throw<AssertFailedException>()
            .And.Message.Should().Be("""
            Assert.AreEqual failed. String lengths are both 201 but differ at index 100. 'expected' expression: 'expected', 'actual' expression: 'actual'.
            Expected: "...aaaaaaaaaaaaaaaaaabcccccccccccccccc..."
            But was:  "...aaaaaaaaaaaaaaaaaadcccccccccccccccc..."
            --------------------------------^
            """);
    }

    public void AreEqualStringWithCultureShouldUseEnhancedMessage()
    {
        Action action = () => Assert.AreEqual("aaaa", "aaab", false, CultureInfo.InvariantCulture);
        action.Should().Throw<Exception>()
            .And.Message.Should().Be("""
            Assert.AreEqual failed. String lengths are both 4 but differ at index 3. 'expected' expression: '"aaaa"', 'actual' expression: '"aaab"'.
            Expected: "aaaa"
            But was:  "aaab"
            --------------^
            """);
    }

    public void AreEqualStringWithDifferentLength()
    {
        Action action = () => Assert.AreEqual("aaaa", "aaa");
        action.Should().Throw<AssertFailedException>()
            .And.Message.Should().Be("""
            Assert.AreEqual failed. Expected string length 4 but was 3. 'expected' expression: '"aaaa"', 'actual' expression: '"aaa"'.
            Expected: "aaaa"
            But was:  "aaa"
            --------------^
            """);
    }

    public void AreEqualShorterExpectedString()
    {
        Action action = () => Assert.AreEqual("aaa", "aaab");
        action.Should().Throw<AssertFailedException>()
            .And.Message.Should().Be("""
            Assert.AreEqual failed. Expected string length 3 but was 4. 'expected' expression: '"aaa"', 'actual' expression: '"aaab"'.
            Expected: "aaa"
            But was:  "aaab"
            --------------^
            """);
    }

    public void AreEqualStringWithUserMessage()
    {
        Action action = () => Assert.AreEqual("aaaa", "aaab", "My custom message");
        action.Should().Throw<AssertFailedException>()
            .And.Message.Should().Be("""
            Assert.AreEqual failed. String lengths are both 4 but differ at index 3. 'expected' expression: '"aaaa"', 'actual' expression: '"aaab"'. My custom message
            Expected: "aaaa"
            But was:  "aaab"
            --------------^
            """);
    }

    public void AreEqualStringWithEmojis()
    {
        Action action = () => Assert.AreEqual("🥰", "aaab");
        action.Should().Throw<AssertFailedException>().And
            .Message.Should().Be("""
            Assert.AreEqual failed. Expected string length 2 but was 4. 'expected' expression: '"🥰"', 'actual' expression: '"aaab"'.
            Expected: "🥰"
            But was:  "aaab"
            -----------^
            """);
    }

    public void CreateStringPreviews_DiffPointsToCorrectPlaceInNonShortenedString()
    {
        int preview = 9;
        int length = 1;
        int diffIndex = 0;
        string stringPreview = FormatStringPreview(StringPreviewHelper.CreateStringPreviews(DigitString(length, diffIndex), DigitString(length, diffIndex), diffIndex, preview));
        StringPreviewsAreEqual(
            """
            "X"
            "X"
            _^
            """,
            stringPreview);
    }

    public void CreateStringPreviews_DiffPointsToCorrectPlaceInShortenedStringWithEndCut()
    {
        int preview = 9;
        int length = preview + 10;
        int diffIndex = 0;
        string stringPreview = FormatStringPreview(StringPreviewHelper.CreateStringPreviews(DigitString(length, diffIndex), DigitString(length, diffIndex), diffIndex, preview));
        StringPreviewsAreEqual(
            """
            "X12345..."
            "X12345..."
            _^
            """, stringPreview);
    }

    public void CreateStringPreviews_DiffPointsToCorrectPlaceInShortenedStringWithStartCut()
    {
        int preview = 9;
        int length = 10;
        int diffIndex = 9;
        string stringPreview = FormatStringPreview(StringPreviewHelper.CreateStringPreviews(DigitString(length, diffIndex), DigitString(length, diffIndex), diffIndex: diffIndex, preview));
        StringPreviewsAreEqual(
            """
            "...45678X"
            "...45678X"
            _________^
            """,
            stringPreview);
    }

    public void CreateStringPreviews_ShowWholeStringWhenDifferenceIsAtTheEndAndJustOneStringDoesNotFit()
    {
        int preview = 21;
        int length = 50;
        int diffIndex = 16;
        string stringPreview = FormatStringPreview(StringPreviewHelper.CreateStringPreviews(DigitString(preview, diffIndex), DigitString(length, diffIndex), diffIndex: diffIndex, preview));
        StringPreviewsAreEqual(
            """
            "0123456789012345X7890"
            "0123456789012345X7..."
            _________________^
            """,
            stringPreview);
    }

    public void CreateStringPreviews_MakeSureWeDontPointToEndEllipsis()
    {
        // We will mask last 3 chars of the string, so we need to make sure that the diff index is not pointing to the end ellipsis.
        int preview = 25;
        int length = 50;
        int diffIndex = 24;

        string stringPreview = FormatStringPreview(StringPreviewHelper.CreateStringPreviews(DigitString(preview, diffIndex), DigitString(length, diffIndex), diffIndex: diffIndex, preview));
        StringPreviewsAreEqual(
            """
            "...8901234567890123X"
            "...8901234567890123X56..."
            ____________________^
            """,
            stringPreview);
    }


    public void CreateStringPreviews_MakeSureWeDontPointToEndEllipsis_WhenLongerStringOneCharLargerThanPreviewWindow()
    {
        // We will mask last 3 chars of the string, so we need to make sure that the diff index is not pointing to the end ellipsis.
        int preview = 15;
        int diffIndex = preview - 1;

        string stringPreview = FormatStringPreview(StringPreviewHelper.CreateStringPreviews(DigitString(preview, diffIndex), DigitString(preview + 1, diffIndex), diffIndex: diffIndex, preview));
        StringPreviewsAreEqual(
            """
            "...890123X"
            "...890123X5"
            __________^
            """,
            stringPreview);
    }

    public void CreateStringPreviews_MakeSureWeDontPointToEndEllipsis_WhenLongerStringIsBarelyLonger()
    {
        // We will mask last 3 chars of the string, so we need to make sure that the diff index is not pointing to the end ellipsis.
        int preview = 25;

        string stringPreview = FormatStringPreview(StringPreviewHelper.CreateStringPreviews("01234567890123456789012345678901234567890123X", "01234567890123456789012345678901234567890123X56", diffIndex: 44, preview));
        StringPreviewsAreEqual(
            """
            "...8901234567890123X"
            "...8901234567890123X56"
            ____________________^
            """,
            stringPreview);
    }

    public void CreateStringPreviews_DiffPointsAfterLastCharacterWhenStringsAreAllTheSameCharactersUntilTheEndOfTheShorterOne()
    {
        int preview = 9;
        int diffIndex = 3;
        string stringPreview = FormatStringPreview(StringPreviewHelper.CreateStringPreviews("aaa", "aaaX", diffIndex, preview));
        stringPreview.Should().Be("""
            "aaa"
            "aaaX"
            ____^
            """);
    }

    public void CreateStringPreviews_DiffNeverPointsAtEllipsis_Generated()
    {
        // Generate all combinations of string lengths and diff to see if in any of them we point to ellipsis.
        StringBuilder s = new();
        foreach (int a in Enumerable.Range(1, 20))
        {
            foreach (int e in Enumerable.Range(1, 20))
            {
                foreach (int d in Enumerable.Range(1, Math.Min(a, e)))
                {
                    string p = FormatStringPreview(StringPreviewHelper.CreateStringPreviews(DigitString(e, d), DigitString(a, d), diffIndex: d, 11));

                    string[] lines = p.Split("\n");
                    int diffIndicator = lines[2].IndexOf('^');
                    bool line0PointsOnEllipsis = lines[0].Length > diffIndicator && lines[0][diffIndicator] == '.';
                    bool line1PointsOnEllipsis = lines[1].Length > diffIndicator && lines[1][diffIndicator] == '.';

                    if (line0PointsOnEllipsis || line1PointsOnEllipsis)
                    {
                        string text = $"""
                            Failed for:
                            Expected={e}, Actual={a}, DiffIndex={d}
                            string result = FormatStringPreview(StringPreviewHelper.CreateStringPreviews(DigitString({e}, {d}), DigitString({a}, {d}), diffIndex: {d}, 11));
                            {p}
                            """;

                        s.AppendLine(text);
                        s.AppendLine();
                    }
                }
            }
        }

        if (s.Length > 0)
        {
            throw new InvalidOperationException($"Some combinations pointed to ellipsis:\n{s}");
        }
    }

    private string FormatStringPreview(Tuple<string, string, int> tuple)
        => $"""
            "{tuple.Item1}"
            "{tuple.Item2}"
            {new string('_', tuple.Item3 + 1)}{'^'}
            """;

    private static string DigitString(int length, int diffIndex)
    {
        const string digits = "0123456789";
        if (length <= 0)
        {
            return string.Empty;
        }

        var result = new StringBuilder(length);
        for (int i = 0; i < length; i++)
        {
            if (i == diffIndex)
            {
                // Use 'X' to indicate a difference should be at this index.
                // To make it easier to see where the arrow should point, even though both strings are the same (we provide the diff index externally).
                result.Append('X');
                continue;
            }

            result.Append(digits[i % digits.Length]);
        }

        return result.ToString();
    }

    private void StringPreviewsAreEqual(string expected, string actual)
    {
        if (expected != actual)
        {
            throw new InvalidOperationException(
                $"""
                Actual:
                {actual}

                Expected:
                {expected}
                """);
        }
    }
}
