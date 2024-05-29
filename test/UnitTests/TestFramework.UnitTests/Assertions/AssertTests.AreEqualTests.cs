// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable enable

using System.Diagnostics.CodeAnalysis;
using System.Globalization;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using TestFramework.ForTestingMSTest;

namespace Microsoft.VisualStudio.TestPlatform.TestFramework.UnitTests;

public partial class AssertTests : TestContainer
{
    public void AreNotEqualShouldFailWhenNotEqualType()
    {
        static void Action() => Assert.AreNotEqual(1, 1);
        Exception ex = VerifyThrows(Action);
        Verify(ex is AssertFailedException);
    }

    public void AreNotEqualShouldFailWhenNotEqualTypeWithMessage()
    {
        Exception ex = VerifyThrows(() => Assert.AreNotEqual(1, 1, "A Message"));
        Verify(ex.Message.Contains("A Message"));
    }

    public void AreNotEqualShouldFailWhenNotEqualString()
    {
        static void Action() => Assert.AreNotEqual("A", "A");
        Exception ex = VerifyThrows(Action);
        Verify(ex is AssertFailedException);
    }

    public void AreNotEqualShouldFailWhenNotEqualStringWithMessage()
    {
        Exception ex = VerifyThrows(() => Assert.AreNotEqual("A", "A", "A Message"));
        Verify(ex.Message.Contains("A Message"));
    }

    [SuppressMessage("Globalization", "CA1304:Specify CultureInfo", Justification = "Testing the API without the culture")]
    public void AreNotEqualShouldFailWhenNotEqualStringAndCaseIgnored()
    {
        static void Action() => Assert.AreNotEqual("A", "a", true);
        Exception ex = VerifyThrows(Action);
        Verify(ex is AssertFailedException);
    }

    public void AreNotEqualShouldFailWhenNotEqualInt()
    {
        static void Action() => Assert.AreNotEqual(1, 1);
        Exception ex = VerifyThrows(Action);
        Verify(ex is AssertFailedException);
    }

    public void AreNotEqualShouldFailWhenNotEqualIntWithMessage()
    {
        Exception? ex = VerifyThrows(() => Assert.AreNotEqual(1, 1, "A Message"));
        Verify(ex.Message.Contains("A Message"));
    }

    public void AreNotEqualShouldFailWhenNotEqualLong()
    {
        static void Action() => Assert.AreNotEqual(1L, 1L);
        Exception ex = VerifyThrows(Action);
        Verify(ex is AssertFailedException);
    }

    public void AreNotEqualShouldFailWhenNotEqualLongWithMessage()
    {
        Exception ex = VerifyThrows(() => Assert.AreNotEqual(1L, 1L, "A Message"));
        Verify(ex.Message.Contains("A Message"));
    }

    public void AreNotEqualShouldFailWhenNotEqualLongWithDelta()
    {
        static void Action() => Assert.AreNotEqual(1L, 2L, 1L);
        Exception ex = VerifyThrows(Action);
        Verify(ex is AssertFailedException);
    }

    public void AreNotEqualShouldFailWhenNotEqualDecimal()
    {
        static void Action() => Assert.AreNotEqual(0.1M, 0.1M);
        Exception ex = VerifyThrows(Action);
        Verify(ex is AssertFailedException);
    }

    public void AreNotEqualShouldFailWhenNotEqualDecimalWithMessage()
    {
        Exception ex = VerifyThrows(() => Assert.AreNotEqual(0.1M, 0.1M, "A Message"));
        Verify(ex.Message.Contains("A Message"));
    }

    public void AreNotEqualShouldFailWhenNotEqualDecimalWithDelta()
    {
        static void Action() => Assert.AreNotEqual(0.1M, 0.2M, 0.1M);
        Exception ex = VerifyThrows(Action);
        Verify(ex is AssertFailedException);
    }

    public void AreNotEqualShouldFailWhenNotEqualDouble()
    {
        static void Action() => Assert.AreNotEqual(0.1, 0.1);
        Exception ex = VerifyThrows(Action);
        Verify(ex is AssertFailedException);
    }

    public void AreNotEqualShouldFailWhenNotEqualDoubleWithMessage()
    {
        Exception? ex = VerifyThrows(() => Assert.AreNotEqual(0.1, 0.1, "A Message"));
        Verify(ex.Message.Contains("A Message"));
    }

    public void AreNotEqualShouldFailWhenNotEqualDoubleWithDelta()
    {
        static void Action() => Assert.AreNotEqual(0.1, 0.2, 0.1);
        Exception ex = VerifyThrows(Action);
        Verify(ex is AssertFailedException);
    }

    public void AreNotEqualShouldFailWhenFloatDouble()
    {
        static void Action() => Assert.AreNotEqual(100E-2, 100E-2);
        Exception ex = VerifyThrows(Action);
        Verify(ex is AssertFailedException);
    }

    public void AreNotEqualShouldFailWhenFloatDoubleWithMessage()
    {
        Exception ex = VerifyThrows(() => Assert.AreNotEqual(100E-2, 100E-2, "A Message"));
        Verify(ex.Message.Contains("A Message"));
    }

    public void AreNotEqualShouldFailWhenNotEqualFloatWithDelta()
    {
        static void Action() => Assert.AreNotEqual(100E-2, 200E-2, 100E-2);
        Exception ex = VerifyThrows(Action);
        Verify(ex is AssertFailedException);
    }

    public void AreEqualShouldFailWhenNotEqualType()
    {
        static void Action() => Assert.AreEqual(null, "string");
        Exception ex = VerifyThrows(Action);
        Verify(ex is AssertFailedException);
    }

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
        Exception? ex = VerifyThrows(() => Assert.AreEqual(expected, actual, false, turkishCulture));
        Verify(ex is not null);
    }

    public void AreEqualShouldFailWhenNotEqualStringWithMessage()
    {
        Exception? ex = VerifyThrows(() => Assert.AreEqual("A", "a", "A Message"));
        Verify(ex is not null);
        Verify(ex!.Message.Contains("A Message"));
    }

    [SuppressMessage("Globalization", "CA1304:Specify CultureInfo", Justification = "Testing the API without the culture")]
    public void AreEqualShouldFailWhenNotEqualStringAndCaseIgnored()
    {
        static void Action() => Assert.AreEqual("A", "a", false);
        Exception ex = VerifyThrows(Action);
        Verify(ex is AssertFailedException);
    }

    public void AreEqualShouldFailWhenNotEqualInt()
    {
        static void Action() => Assert.AreEqual(1, 2);
        Exception ex = VerifyThrows(Action);
        Verify(ex is AssertFailedException);
    }

    public void AreEqualShouldFailWhenNotEqualIntWithMessage()
    {
        Exception? ex = VerifyThrows(() => Assert.AreEqual(1, 2, "A Message"));
        Verify(ex is not null);
        Verify(ex!.Message.Contains("A Message"));
    }

    public void AreEqualShouldFailWhenNotEqualLong()
    {
        static void Action() => Assert.AreEqual(1L, 2L);
        Exception ex = VerifyThrows(Action);
        Verify(ex is AssertFailedException);
    }

    public void AreEqualShouldFailWhenNotEqualLongWithMessage()
    {
        Exception? ex = VerifyThrows(() => Assert.AreEqual(1L, 2L, "A Message"));
        Verify(ex is not null);
        Verify(ex!.Message.Contains("A Message"));
    }

    public void AreEqualShouldFailWhenNotEqualLongWithDelta()
    {
        static void Action() => Assert.AreEqual(10L, 20L, 5L);
        Exception ex = VerifyThrows(Action);
        Verify(ex is AssertFailedException);
    }

    public void AreEqualShouldFailWhenNotEqualDouble()
    {
        static void Action() => Assert.AreEqual(0.1, 0.2);
        Exception ex = VerifyThrows(Action);
        Verify(ex is AssertFailedException);
    }

    public void AreEqualShouldFailWhenNotEqualDoubleWithMessage()
    {
        Exception? ex = VerifyThrows(() => Assert.AreEqual(0.1, 0.2, "A Message"));
        Verify(ex is not null);
        Verify(ex!.Message.Contains("A Message"));
    }

    public void AreEqualShouldFailWhenNotEqualDoubleWithDelta()
    {
        static void Action() => Assert.AreEqual(0.1, 0.2, 0.05);
        Exception ex = VerifyThrows(Action);
        Verify(ex is AssertFailedException);
    }

    public void AreEqualShouldFailWhenNotEqualDecimal()
    {
        static void Action() => Assert.AreEqual(0.1M, 0.2M);
        Exception ex = VerifyThrows(Action);
        Verify(ex is AssertFailedException);
    }

    public void AreEqualShouldFailWhenNotEqualDecimalWithMessage()
    {
        Exception? ex = VerifyThrows(() => Assert.AreEqual(0.1M, 0.2M, "A Message"));
        Verify(ex is not null);
        Verify(ex!.Message.Contains("A Message"));
    }

    public void AreEqualShouldFailWhenNotEqualDecimalWithDelta()
    {
        static void Action() => Assert.AreEqual(0.1M, 0.2M, 0.05M);
        Exception ex = VerifyThrows(Action);
        Verify(ex is AssertFailedException);
    }

    public void AreEqualShouldFailWhenFloatDouble()
    {
        static void Action() => Assert.AreEqual(100E-2, 200E-2);
        Exception ex = VerifyThrows(Action);
        Verify(ex is AssertFailedException);
    }

    public void AreEqualShouldFailWhenFloatDoubleWithMessage()
    {
        Exception ex = VerifyThrows(() => Assert.AreEqual(100E-2, 200E-2, "A Message"));
        Verify(ex.Message.Contains("A Message"));
    }

    public void AreEqualShouldFailWhenNotEqualFloatWithDelta()
    {
        static void Action() => Assert.AreEqual(100E-2, 200E-2, 50E-2);
        Exception ex = VerifyThrows(Action);
        Verify(ex is AssertFailedException);
    }

    public void AreEqualTwoObjectsShouldFail()
    {
        static void Action() => Assert.AreEqual(new object(), new object());
        Exception ex = VerifyThrows(Action);
        Verify(ex is AssertFailedException);
    }

    public void AreEqualTwoObjectsDifferentTypeShouldFail()
    {
        static void Action() => Assert.AreEqual(new object(), 1);
        Exception ex = VerifyThrows(Action);
        Verify(ex is AssertFailedException);
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

        Exception ex = VerifyThrows(Action);
        Verify(ex is AssertFailedException);
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

    private CultureInfo? GetCultureInfo() => CultureInfo.CurrentCulture;

    private class TypeOverridesEquals
    {
        public override bool Equals(object? obj) => true;

        public override int GetHashCode() => throw new System.NotImplementedException();
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
