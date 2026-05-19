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
        action.Should().Throw<AssertFailedException>()
            .Which.Message.Should().Be(
                """
                Assertion failed. Expected strings to be equal.
                Strings differ only in case.
                Strings have same length (1) and differ at 1 location(s). First difference at index 0.

                expected: "i"
                actual:   "I"
                culture:  en-EN

                Assert.AreEqual(expected, actual)
                """);
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
            .Which.Message.Should().Be(
                """
                Assertion failed. Expected values to be equal, but they are of different types.

                expected:      System.Object
                expected type: System.Object
                actual:        1
                actual type:   System.Int32

                Assert.AreEqual(new object(), 1)
                """);
    }

    public void AreNotEqual_PopulatesExpectedAndActualTextWithNotPrefix()
    {
        Action action = () => Assert.AreNotEqual(0, 0);
        AssertFailedException ex = action.Should().Throw<AssertFailedException>().Which;
        ex.ExpectedText.Should().Be("not 0");
        ex.ActualText.Should().Be("0");
        ex.Data["assert.expected"].Should().Be("not 0");
        ex.Data["assert.actual"].Should().Be("0");
    }

    public void AreEqualWithDelta_PopulatesExpectedAndActualText()
    {
        Action action = () => Assert.AreEqual(5.0f, 2.0f, 2.0f);
        AssertFailedException ex = action.Should().Throw<AssertFailedException>().Which;
        ex.ExpectedText.Should().Be("5");
        ex.ActualText.Should().Be("2");
        ex.Data["assert.expected"].Should().Be("5");
        ex.Data["assert.actual"].Should().Be("2");
    }

    public void AreNotEqualWithDelta_PopulatesExpectedAndActualTextWithNotPrefix()
    {
        Action action = () => Assert.AreNotEqual(5.0f, 4.0f, 2.0f);
        AssertFailedException ex = action.Should().Throw<AssertFailedException>().Which;
        ex.ExpectedText.Should().Be("not 5");
        ex.ActualText.Should().Be("4");
        ex.Data["assert.expected"].Should().Be("not 5");
        ex.Data["assert.actual"].Should().Be("4");
    }

    public void AreNotEqual_FailsWithStructuredMessage()
    {
        Action action = () => Assert.AreNotEqual(0, 0);
        action.Should().Throw<AssertFailedException>()
            .Which.Message.Should().Be(
                """
                Assertion failed. Expected values to not be equal.

                notExpected: 0
                actual:      0

                Assert.AreNotEqual(0, 0)
                """);
    }

    public void AreEqual_MultilineExpectedExpression_UsesPlaceholderInCallSite()
    {
        Action action = () => Assert.AreEqual(
            """
            line1
            line2
            """,
            "different");
        action.Should().Throw<AssertFailedException>()
            .Which.Message.Should().EndWith("Assert.AreEqual(<expected>, \"different\")");
    }

    public void AreEqual_MultilineActualExpression_UsesPlaceholderInCallSite()
    {
        Action action = () => Assert.AreEqual(
            "different",
            """
            line1
            line2
            """);
        action.Should().Throw<AssertFailedException>()
            .Which.Message.Should().EndWith("Assert.AreEqual(\"different\", <actual>)");
    }

    public void AreNotEqual_MultilineNotExpectedExpression_UsesPlaceholderInCallSite()
    {
        string value = "x";
        Action action = () => Assert.AreNotEqual(
            """
            x
            """,
            value);
        action.Should().Throw<AssertFailedException>()
            .Which.Message.Should().EndWith("Assert.AreNotEqual(<notExpected>, value)");
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
            .Which.Message.Should().Be(
                $"""
                Assertion failed. Expected values to be equal.
                User-provided message. DummyClassTrackingToStringCalls,     DummyClassTrackingToStringCalls, Hello, DummyIFormattable.ToString(), {string.Format(null, "{0:tt}", dateTime)}, {string.Format(null, "{0,5:tt}", dateTime)}

                expected: 0
                actual:   1

                Assert.AreEqual(0, 1)
                """);
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
            .Which.Message.Should().Be(
                $"""
                Assertion failed. Expected values to not be equal.
                User-provided message. DummyClassTrackingToStringCalls,     DummyClassTrackingToStringCalls, Hello, DummyIFormattable.ToString(), {string.Format(null, "{0:tt}", dateTime)}, {string.Format(null, "{0,5:tt}", dateTime)}

                notExpected: 0
                actual:      0

                Assert.AreNotEqual(0, 0)
                """);
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
            .Which.Message.Should().Be(CreateDeltaAreEqualFailureMessage(
                "1",
                "1.1",
                "0.001",
                "Assert.AreEqual(1.0f, 1.1f, <delta>)",
                $"User-provided message. DummyClassTrackingToStringCalls,     DummyClassTrackingToStringCalls, Hello, DummyIFormattable.ToString(), {string.Format(null, "{0:tt}", dateTime)}, {string.Format(null, "{0,5:tt}", dateTime)}"));
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
            .Which.Message.Should().Be(CreateDeltaAreNotEqualFailureMessage(
                "1",
                "1.1",
                "0.2",
                "Assert.AreNotEqual(1.0f, 1.1f, <delta>)",
                $"User-provided message. DummyClassTrackingToStringCalls,     DummyClassTrackingToStringCalls, Hello, DummyIFormattable.ToString(), {string.Format(null, "{0:tt}", dateTime)}, {string.Format(null, "{0,5:tt}", dateTime)}"));
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
            .Which.Message.Should().Be(CreateDeltaAreEqualFailureMessage(
                "1.0",
                "1.1",
                "0.001",
                "Assert.AreEqual(1.0m, 1.1m, <delta>)",
                $"User-provided message. DummyClassTrackingToStringCalls,     DummyClassTrackingToStringCalls, Hello, DummyIFormattable.ToString(), {string.Format(null, "{0:tt}", dateTime)}, {string.Format(null, "{0,5:tt}", dateTime)}"));
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
            .Which.Message.Should().Be(CreateDeltaAreNotEqualFailureMessage(
                "1.0",
                "1.1",
                "0.2",
                "Assert.AreNotEqual(1.0m, 1.1m, <delta>)",
                $"User-provided message. DummyClassTrackingToStringCalls,     DummyClassTrackingToStringCalls, Hello, DummyIFormattable.ToString(), {string.Format(null, "{0:tt}", dateTime)}, {string.Format(null, "{0,5:tt}", dateTime)}"));
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
            .Which.Message.Should().Be(CreateDeltaAreEqualFailureMessage(
                "1",
                "2",
                "0",
                "Assert.AreEqual(1L, 2L, <delta>)",
                $"User-provided message. DummyClassTrackingToStringCalls,     DummyClassTrackingToStringCalls, Hello, DummyIFormattable.ToString(), {string.Format(null, "{0:tt}", dateTime)}, {string.Format(null, "{0,5:tt}", dateTime)}"));
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
            .Which.Message.Should().Be(CreateDeltaAreNotEqualFailureMessage(
                "1",
                "2",
                "1",
                "Assert.AreNotEqual(1L, 2L, <delta>)",
                $"User-provided message. DummyClassTrackingToStringCalls,     DummyClassTrackingToStringCalls, Hello, DummyIFormattable.ToString(), {string.Format(null, "{0:tt}", dateTime)}, {string.Format(null, "{0,5:tt}", dateTime)}"));
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
            .Which.Message.Should().Be(CreateDeltaAreEqualFailureMessage(
                "1",
                "1.1",
                "0.001",
                "Assert.AreEqual(1.0d, 1.1d, <delta>)",
                $"User-provided message. DummyClassTrackingToStringCalls,     DummyClassTrackingToStringCalls, Hello, DummyIFormattable.ToString(), {string.Format(null, "{0:tt}", dateTime)}, {string.Format(null, "{0,5:tt}", dateTime)}"));
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
            .Which.Message.Should().Be(CreateDeltaAreNotEqualFailureMessage(
                "1",
                "1.1",
                "0.2",
                "Assert.AreNotEqual(1.0d, 1.1d, <delta>)",
                $"User-provided message. DummyClassTrackingToStringCalls,     DummyClassTrackingToStringCalls, Hello, DummyIFormattable.ToString(), {string.Format(null, "{0:tt}", dateTime)}, {string.Format(null, "{0,5:tt}", dateTime)}"));
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
        action.Should().Throw<Exception>().Which.Message.Should().Be(CreateDeltaAreEqualFailureMessage("5", "2", "2", "Assert.AreEqual(5.0f, 2.0f, <delta>)"));
    }

    public void FloatAreEqual_ExpectedIsNumeric_ActualIsNumeric_ExpectedAndActualDifferenceGreaterThanDeltaNegative_DeltaIsNumeric_ShouldFail()
    {
        Action action = () => Assert.AreEqual(2.0f, 5.0f, 2.0f); // difference is -3. Delta is 2
        action.Should().Throw<Exception>().Which.Message.Should().Be(CreateDeltaAreEqualFailureMessage("2", "5", "2", "Assert.AreEqual(2.0f, 5.0f, <delta>)"));
    }

    public void FloatAreEqual_ExpectedIsNumeric_ActualIsNumeric_ExpectedAndActualDifferenceLessThanDeltaPositive_DeltaIsNumeric_ShouldPass()
        => Assert.AreEqual(5.0f, 4.0f, 2.0f); // difference is 1. Delta is 2

    public void FloatAreEqual_ExpectedIsNumeric_ActualIsNumeric_ExpectedAndActualDifferenceLessThanDeltaNegative_DeltaIsNumeric_ShouldFail()
        => Assert.AreEqual(4.0f, 5.0f, 2.0f); // difference is -1. Delta is 2

    public void FloatAreEqual_ExpectedIsNumeric_ActualIsNaN_DeltaIsNumeric_ShouldFail()
    {
        Action action = () => Assert.AreEqual(5.0f, float.NaN, 2.0f);
        action.Should().Throw<Exception>().Which.Message.Should().Be(CreateDeltaAreEqualFailureMessage("5", "NaN", "2", "Assert.AreEqual(5.0f, float.NaN, <delta>)"));
    }

    public void FloatAreEqual_ExpectedIsNaN_ActualIsNumeric_DeltaIsNumeric_ShouldFail()
    {
        Action action = () => Assert.AreEqual(float.NaN, 5.0f, 2.0f);
        action.Should().Throw<Exception>().Which.Message.Should().Be(CreateDeltaAreEqualFailureMessage("NaN", "5", "2", "Assert.AreEqual(float.NaN, 5.0f, <delta>)"));
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
        action.Should().Throw<Exception>().Which.Message.Should().Be(CreateDeltaAreNotEqualFailureMessage("5", "4", "2", "Assert.AreNotEqual(5.0f, 4.0f, <delta>)"));
    }

    public void FloatAreNotEqual_ExpectedIsNumeric_ActualIsNumeric_ExpectedAndActualDifferenceLessThanDeltaNegative_DeltaIsNumeric_ShouldFail()
    {
        Action action = () => Assert.AreNotEqual(4.0f, 5.0f, 2.0f); // difference is -1. Delta is 2
        action.Should().Throw<Exception>().Which.Message.Should().Be(CreateDeltaAreNotEqualFailureMessage("4", "5", "2", "Assert.AreNotEqual(4.0f, 5.0f, <delta>)"));
    }

    public void FloatAreNotEqual_ExpectedIsNumeric_ActualIsNaN_DeltaIsNumeric_ShouldPass() => Assert.AreNotEqual(5.0f, float.NaN, 2.0f);

    public void FloatAreNotEqual_ExpectedIsNaN_ActualIsNumeric_DeltaIsNumeric_ShouldPass()
        => Assert.AreNotEqual(float.NaN, 5.0f, 2.0f);

    public void FloatAreNotEqual_ExpectedIsNaN_ActualIsNaN_DeltaIsNumeric_ShouldFail()
    {
        Action action = () => Assert.AreNotEqual(float.NaN, float.NaN, 2.0f);
        action.Should().Throw<Exception>().Which.Message.Should().Be(CreateDeltaAreNotEqualFailureMessage("NaN", "NaN", "2", "Assert.AreNotEqual(float.NaN, float.NaN, <delta>)"));
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
        action.Should().Throw<Exception>()
            .Which.Message.Should().Be(CreateDeltaAreEqualFailureMessage("5", "2", "2", "Assert.AreEqual(5.0d, 2.0d, <delta>)"));
    }

    public void DoubleAreEqual_ExpectedIsNumeric_ActualIsNumeric_ExpectedAndActualDifferenceGreaterThanDeltaNegative_DeltaIsNumeric_ShouldFail()
    {
        Action action = () => Assert.AreEqual(2.0d, 5.0d, 2.0d); // difference is -3. Delta is 2
        action.Should().Throw<Exception>()
            .Which.Message.Should().Be(CreateDeltaAreEqualFailureMessage("2", "5", "2", "Assert.AreEqual(2.0d, 5.0d, <delta>)"));
    }

    public void DoubleAreEqual_ExpectedIsNumeric_ActualIsNumeric_ExpectedAndActualDifferenceLessThanDeltaPositive_DeltaIsNumeric_ShouldPass()
        => Assert.AreEqual(5.0d, 4.0d, 2.0d); // difference is 1. Delta is 2

    public void DoubleAreEqual_ExpectedIsNumeric_ActualIsNumeric_ExpectedAndActualDifferenceLessThanDeltaNegative_DeltaIsNumeric_ShouldFail()
        => Assert.AreEqual(4.0d, 5.0d, 2.0d); // difference is -1. Delta is 2

    public void DoubleAreEqual_ExpectedIsNumeric_ActualIsNaN_DeltaIsNumeric_ShouldFail()
    {
        Action action = () => Assert.AreEqual(5.0d, double.NaN, 2.0d);
        action.Should().Throw<Exception>()
            .Which.Message.Should().Be(CreateDeltaAreEqualFailureMessage("5", "NaN", "2", "Assert.AreEqual(5.0d, double.NaN, <delta>)"));
    }

    public void DoubleAreEqual_ExpectedIsNaN_ActualIsNumeric_DeltaIsNumeric_ShouldFail()
    {
        Action action = () => Assert.AreEqual(double.NaN, 5.0d, 2.0d);
        action.Should().Throw<Exception>()
            .Which.Message.Should().Be(CreateDeltaAreEqualFailureMessage("NaN", "5", "2", "Assert.AreEqual(double.NaN, 5.0d, <delta>)"));
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
        action.Should().Throw<Exception>()
            .Which.Message.Should().Be(CreateDeltaAreNotEqualFailureMessage("5", "4", "2", "Assert.AreNotEqual(5.0d, 4.0d, <delta>)"));
    }

    public void DoubleAreNotEqual_ExpectedIsNumeric_ActualIsNumeric_ExpectedAndActualDifferenceLessThanDeltaNegative_DeltaIsNumeric_ShouldFail()
    {
        Action action = () => Assert.AreNotEqual(4.0d, 5.0d, 2.0d); // difference is -1. Delta is 2
        action.Should().Throw<Exception>()
            .Which.Message.Should().Be(CreateDeltaAreNotEqualFailureMessage("4", "5", "2", "Assert.AreNotEqual(4.0d, 5.0d, <delta>)"));
    }

    public void DoubleAreNotEqual_ExpectedIsNumeric_ActualIsNaN_DeltaIsNumeric_ShouldPass() => Assert.AreNotEqual(5.0d, double.NaN, 2.0d);

    public void DoubleAreNotEqual_ExpectedIsNaN_ActualIsNumeric_DeltaIsNumeric_ShouldPass()
        => Assert.AreNotEqual(double.NaN, 5.0d, 2.0d);

    public void DoubleAreNotEqual_ExpectedIsNaN_ActualIsNaN_DeltaIsNumeric_ShouldFail()
    {
        Action action = () => Assert.AreNotEqual(double.NaN, double.NaN, 2.0d);
        action.Should().Throw<Exception>()
            .Which.Message.Should().Be(CreateDeltaAreNotEqualFailureMessage("NaN", "NaN", "2", "Assert.AreNotEqual(double.NaN, double.NaN, <delta>)"));
    }

    private static string CreateDeltaAreEqualFailureMessage(string expected, string actual, string delta, string callSite, string? userMessage = null)
    {
        string[] lines = userMessage is null
            ?
            [
                "Assertion failed. Expected values to be equal within tolerance.",
                string.Empty,
                $"expected: {expected}",
                $"actual:   {actual}",
                $"delta:    {delta}",
                string.Empty,
                callSite,
            ]
            :
            [
                "Assertion failed. Expected values to be equal within tolerance.",
                userMessage,
                string.Empty,
                $"expected: {expected}",
                $"actual:   {actual}",
                $"delta:    {delta}",
                string.Empty,
                callSite,
            ];
        return string.Join(Environment.NewLine, lines);
    }

    private static string CreateDeltaAreNotEqualFailureMessage(string notExpected, string actual, string delta, string callSite, string? userMessage = null)
    {
        string[] lines = userMessage is null
            ?
            [
                "Assertion failed. Expected values to differ beyond tolerance.",
                string.Empty,
                $"not expected: {notExpected}",
                $"actual:       {actual}",
                $"delta:        {delta}",
                string.Empty,
                callSite,
            ]
            :
            [
                "Assertion failed. Expected values to differ beyond tolerance.",
                userMessage,
                string.Empty,
                $"not expected: {notExpected}",
                $"actual:       {actual}",
                $"delta:        {delta}",
                string.Empty,
                callSite,
            ];
        return string.Join(Environment.NewLine, lines);
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
            .Which.Message.Should().Be(
                """
                Assertion failed. Expected strings to be equal.
                Strings have same length (4) and differ at 1 location(s). First difference at index 0.

                expected: "baaa"
                actual:   "aaaa"

                Assert.AreEqual("baaa", "aaaa")
                """);
    }

    public void AreEqualStringDifferenceAtEnd()
    {
        Action action = () => Assert.AreEqual("aaaa", "aaab");
        action.Should().Throw<AssertFailedException>()
            .Which.Message.Should().Be(
                """
                Assertion failed. Expected strings to be equal.
                Strings have same length (4) and differ at 1 location(s). First difference at index 3.

                expected: "aaaa"
                actual:   "aaab"

                Assert.AreEqual("aaaa", "aaab")
                """);
    }

    public void AreEqualStringWithSpecialCharactersShouldEscape()
    {
        Action action = () => Assert.AreEqual("aa\ta", "aa a");
        AssertFailedException ex = action.Should().Throw<AssertFailedException>().Which;

        ex.Message.Should().Contain("Strings have same length (4) and differ at 1 location(s). First difference at index 2.");
        ex.Message.Should().Contain("expected: \"aa\\ta\"");
        ex.Message.Should().Contain("actual:   \"aa a\"");
        ex.Message.Should().Contain("Assert.AreEqual(\"aa\\ta\", \"aa a\")");
    }

    // Long-string truncation is intentionally not yet implemented; documents the current full-string render.
    public void AreEqualLongStringsShowsFullStrings()
    {
        string expected = new string('a', 100) + "b" + new string('c', 100);
        string actual = new string('a', 100) + "d" + new string('c', 100);

        Action action = () => Assert.AreEqual(expected, actual);
        action.Should().Throw<AssertFailedException>()
            .Which.Message.Should().Be(
                """
                Assertion failed. Expected strings to be equal.
                Strings have same length (201) and differ at 1 location(s). First difference at index 100.

                expected: "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaabcccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccc"
                actual:   "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaadcccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccc"

                Assert.AreEqual(expected, actual)
                """);
    }

    public void AreEqualStringWithCultureShouldUseEnhancedMessage()
    {
        Action action = () => Assert.AreEqual("aaaa", "aaab", false, CultureInfo.InvariantCulture);
        AssertFailedException ex = action.Should().Throw<AssertFailedException>().Which;

        ex.Message.Should().StartWith(
            """
            Assertion failed. Expected strings to be equal.
            Strings have same length (4) and differ at 1 location(s). First difference at index 3.

            expected: "aaaa"
            actual:   "aaab"
            """);
        ex.Message.Should().Contain($"{Environment.NewLine}culture:");
        ex.Message.Should().EndWith(
            """

            Assert.AreEqual("aaaa", "aaab")
            """);
    }

    public void AreEqualStringWithDifferentLength()
    {
        Action action = () => Assert.AreEqual("aaaa", "aaa");
        action.Should().Throw<AssertFailedException>()
            .Which.Message.Should().Be(
                """
                Assertion failed. Expected strings to be equal.
                Strings have different lengths (expected: 4, actual: 3) and differ at 1 location(s). First difference at index 3.

                expected: "aaaa"
                actual:   "aaa"

                Assert.AreEqual("aaaa", "aaa")
                """);
    }

    public void AreEqualShorterExpectedString()
    {
        Action action = () => Assert.AreEqual("aaa", "aaab");
        action.Should().Throw<AssertFailedException>()
            .Which.Message.Should().Be(
                """
                Assertion failed. Expected strings to be equal.
                Strings have different lengths (expected: 3, actual: 4) and differ at 1 location(s). First difference at index 3.

                expected: "aaa"
                actual:   "aaab"

                Assert.AreEqual("aaa", "aaab")
                """);
    }

    public void AreEqualStringWithUserMessage()
    {
        Action action = () => Assert.AreEqual("aaaa", "aaab", "My custom message");
        action.Should().Throw<AssertFailedException>()
            .Which.Message.Should().Be(
                """
                Assertion failed. Expected strings to be equal.
                Strings have same length (4) and differ at 1 location(s). First difference at index 3.
                My custom message

                expected: "aaaa"
                actual:   "aaab"

                Assert.AreEqual("aaaa", "aaab")
                """);
    }

    public void AreEqualStringWithEmojis()
    {
        Action action = () => Assert.AreEqual("🥰", "aaab");
        action.Should().Throw<AssertFailedException>()
            .Which.Message.Should().Be(
                """
                Assertion failed. Expected strings to be equal.
                Strings have different lengths (expected: 2, actual: 4) and differ at 1 location(s). First difference at index 0.

                expected: "🥰"
                actual:   "aaab"

                Assert.AreEqual("🥰", "aaab")
                """);
    }

    public void AreEqualStringSpecificWithIgnoreCaseAndCultureUsesComparisonAwareDiffIndex()
    {
        Action action = () => Assert.AreEqual("straße", "STRAẞE!", true, new CultureInfo("de-DE"));
        action.Should().Throw<AssertFailedException>()
            .Which.Message.Should().Be(
                """
                Assertion failed. Expected strings to be equal.
                Strings have different lengths (expected: 6, actual: 7) and differ at 1 location(s). First difference at index 6.

                expected:    "straße"
                actual:      "STRAẞE!"
                ignore case: true
                culture:     de-DE

                Assert.AreEqual("straße", "STRAẞE!")
                """);
    }

    public void AreEqualStringSpecificWithNullExpectedUsesStructuredMessage()
    {
        string? expected = null;
        string actual = "string";

        Action action = () => Assert.AreEqual(expected, actual, false);
        action.Should().Throw<AssertFailedException>()
            .Which.Message.Should().Be(
                """
                Assertion failed. Expected strings to be equal.

                expected: null
                actual:   "string"

                Assert.AreEqual(expected, actual)
                """);
    }

    public void AreEqualStringSpecificWithMultilineStringsUsesStructuredMessage()
    {
        string expected = "line one\nline two\nline three";
        string actual = "line one\nLINE TWO\nline three";

        Action action = () => Assert.AreEqual(expected, actual, false);
        AssertFailedException ex = action.Should().Throw<AssertFailedException>().Which;

        ex.Message.Should().Contain("Assertion failed. Expected strings to be equal.");
        ex.Message.Should().Contain("line one");
        ex.Message.Should().Contain("LINE TWO");
        ex.Message.Should().Contain("Assert.AreEqual(expected, actual)");
    }

    public void AreEqualStringSpecificWhenEqualDoesNotThrow()
        => FluentActions.Invoking(() => Assert.AreEqual("Straße", "STRAẞE", true, new CultureInfo("de-DE"))).Should().NotThrow();

    public void AreNotEqualStringSpecificShowsStructuredMessage()
    {
        string notExpected = "A";
        string actual = "A";

        Action action = () => Assert.AreNotEqual(notExpected, actual, false);
        AssertFailedException ex = action.Should().Throw<AssertFailedException>().Which;

        ex.Message.Should().Be(
            """
            Assertion failed. Expected strings to differ.

            not expected: "A"
            actual:       "A"

            Assert.AreNotEqual(notExpected, actual)
            """);
        ex.ExpectedText.Should().Be("not \"A\"");
        ex.ActualText.Should().Be("\"A\"");
        ex.Data["assert.expected"].Should().Be("not \"A\"");
        ex.Data["assert.actual"].Should().Be("\"A\"");
    }

    public void AreNotEqualStringSpecificWithIgnoreCaseAndCultureShowsEvidence()
    {
        Action action = () => Assert.AreNotEqual("Straße", "STRAẞE", true, new CultureInfo("de-DE"));
        action.Should().Throw<AssertFailedException>()
            .Which.Message.Should().Be(
                """
                Assertion failed. Expected strings to differ (case-insensitive).

                not expected: "Straße"
                actual:       "STRAẞE"
                ignore case:  true
                culture:      de-DE

                Assert.AreNotEqual("Straße", "STRAẞE")
                """);
    }

    public void AreNotEqualStringSpecificWithBothNullShowsStructuredMessage()
    {
        string? notExpected = null;
        string? actual = null;

        Action action = () => Assert.AreNotEqual(notExpected, actual, false);
        action.Should().Throw<AssertFailedException>()
            .Which.Message.Should().Be(
                """
                Assertion failed. Expected strings to differ.

                not expected: null
                actual:       null

                Assert.AreNotEqual(notExpected, actual)
                """);
    }

    public void AreNotEqualStringSpecificWhenDifferentDoesNotThrow()
        => FluentActions.Invoking(() => Assert.AreNotEqual("Straße", "STRASSE!", true, new CultureInfo("de-DE"))).Should().NotThrow();
}
