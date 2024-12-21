// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable enable

using Microsoft.VisualStudio.TestTools.UnitTesting;

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
        VerifyThrows(() => Assert.AreEqual(expected, actual, false, englishCulture));
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

    public void AreEqualStringIgnoreCaseCultureInfoNullabilityPostConditions()
    {
        CultureInfo? cultureInfo = GetCultureInfo();
        Assert.AreEqual("a", "a", false, cultureInfo);
        _ = cultureInfo.Calendar; // no warning
    }

    public void AreEqualStringIgnoreCaseCultureInfoMessageNullabilityPostConditions()
    {
        CultureInfo? cultureInfo = GetCultureInfo();
        Assert.AreEqual("a", "a", false, cultureInfo, "message");
        _ = cultureInfo.Calendar; // no warning
    }

    public void AreEqualStringIgnoreCaseCultureInfoMessageParametersNullabilityPostConditions()
    {
        CultureInfo? cultureInfo = GetCultureInfo();
        Assert.AreEqual("a", "a", false, cultureInfo, "message format {0} {1}", 1, 2);
        _ = cultureInfo.Calendar; // no warning
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
        Assert.AreEqual<dynamic>((dynamic?)null, (dynamic?)null);
        Assert.AreEqual<dynamic>((dynamic)1, (dynamic)1);
        Assert.AreEqual<dynamic>((dynamic)"a", (dynamic)"a");
        Assert.AreEqual<dynamic>((dynamic)'a', (dynamic)'a');
    }

#pragma warning restore IDE0004

    private CultureInfo? GetCultureInfo() => CultureInfo.CurrentCulture;

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
}
