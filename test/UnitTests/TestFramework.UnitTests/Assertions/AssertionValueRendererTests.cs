// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Globalization;

using AwesomeAssertions;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using TestFramework.ForTestingMSTest;

namespace Microsoft.VisualStudio.TestPlatform.TestFramework.UnitTests;

public class AssertionValueRendererTests : TestContainer
{
    public void RenderValue_Null_ReturnsNull() =>
        AssertionValueRenderer.RenderValue(null).Should().Be("null");

    public void RenderValue_EmptyString_ReturnsQuotedEmpty() =>
        AssertionValueRenderer.RenderValue(string.Empty).Should().Be("\"\"");

    public void RenderValue_SimpleString_ReturnsQuotedString() =>
        AssertionValueRenderer.RenderValue("hello world").Should().Be("\"hello world\"");

    public void RenderValue_StringWithEmbeddedQuotes_EscapesQuotes() =>
        AssertionValueRenderer.RenderValue("she said \"hello\"").Should().Be("\"she said \\\"hello\\\"\"");

    public void RenderValue_StringWithNewline_EscapesNewline() =>
        AssertionValueRenderer.RenderValue("line1\nline2").Should().Be("\"line1\\nline2\"");

    public void RenderValue_StringWithCarriageReturn_EscapesCR() =>
        AssertionValueRenderer.RenderValue("line1\rline2").Should().Be("\"line1\\rline2\"");

    public void RenderValue_StringWithTab_EscapesTab() =>
        AssertionValueRenderer.RenderValue("col1\tcol2").Should().Be("\"col1\\tcol2\"");

    public void RenderValue_StringWithNullChar_EscapesNull() =>
        AssertionValueRenderer.RenderValue("abc\0def").Should().Be("\"abc\\0def\"");

    public void RenderValue_StringWithBackslash_EscapesBackslash() =>
        AssertionValueRenderer.RenderValue("path\\to\\file").Should().Be("\"path\\\\to\\\\file\"");

    public void RenderValue_WhitespaceOnlyString_ReturnsQuotedWhitespace() =>
        AssertionValueRenderer.RenderValue("   ").Should().Be("\"   \"");

    public void RenderValue_Integer_ReturnsUnquoted() =>
        AssertionValueRenderer.RenderValue(42).Should().Be("42");

    public void RenderValue_NegativeInteger_ReturnsUnquoted() =>
        AssertionValueRenderer.RenderValue(-7).Should().Be("-7");

    public void RenderValue_Double_ReturnsUnquoted() =>
        AssertionValueRenderer.RenderValue(3.14).Should().Be("3.14");

    public void RenderValue_Double_RendersFullPrecision()
    {
#if NET
        double d1 = 0.1 + 0.2;
        AssertionValueRenderer.RenderValue(d1).Should().Be("0.30000000000000004");
#else
        // .NET Framework "R" format yields the same string on this particular value
        // but its general behavior differs from .NET Core (15-digit-first fallback);
        // assert the precision-preserving property structurally instead.
        AssertionValueRenderer.RenderValue(0.1 + 0.2).Should().NotBe(AssertionValueRenderer.RenderValue(0.3));
#endif
    }

    public void RenderValue_Double_RendersInvariantCulture()
    {
        // Even under cultures whose decimal separator is a comma, we always emit a dot.
        CultureInfo previousCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("fr-FR");
            AssertionValueRenderer.RenderValue(3.14).Should().Be("3.14");
        }
        finally
        {
            CultureInfo.CurrentCulture = previousCulture;
        }
    }

    public void RenderValue_Double_NaN_RendersNaN() =>
        AssertionValueRenderer.RenderValue(double.NaN).Should().Be("NaN");

    public void RenderValue_Double_PositiveInfinity_RendersInfinity() =>
        AssertionValueRenderer.RenderValue(double.PositiveInfinity).Should().Be("Infinity");

    public void RenderValue_Double_NegativeInfinity_RendersInfinity() =>
        AssertionValueRenderer.RenderValue(double.NegativeInfinity).Should().Be("-Infinity");

    public void RenderValue_Float_RendersInvariantCulture()
    {
        CultureInfo previousCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("fr-FR");
            AssertionValueRenderer.RenderValue(3.5f).Should().Be("3.5");
        }
        finally
        {
            CultureInfo.CurrentCulture = previousCulture;
        }
    }

    public void RenderValue_Float_DistinguishesNearEqualValues()
    {
        // Construct two adjacent float values via byte-level bit manipulation so the test
        // works on net48 (which lacks BitConverter.SingleToInt32Bits) and net6+.
        float a = 1.0f;
        byte[] bytes = BitConverter.GetBytes(a);
        uint asInt = BitConverter.ToUInt32(bytes, 0);
        float b = BitConverter.ToSingle(BitConverter.GetBytes(asInt + 1u), 0);
        AssertionValueRenderer.RenderValue(a).Should().NotBe(AssertionValueRenderer.RenderValue(b));
    }

    public void RenderValue_Decimal_RendersInvariantCulture()
    {
        CultureInfo previousCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("fr-FR");
            AssertionValueRenderer.RenderValue(1.5m).Should().Be("1.5");
        }
        finally
        {
            CultureInfo.CurrentCulture = previousCulture;
        }
    }

    public void RenderValue_DateTime_RendersWithFullTickPrecision()
    {
        DateTime dt = new DateTime(2026, 6, 9, 13, 12, 21, DateTimeKind.Unspecified).AddTicks(1234567);
        AssertionValueRenderer.RenderValue(dt).Should().Be("2026-06-09T13:12:21.1234567");
    }

    public void RenderValue_DateTime_DistinguishesTwoValuesOneTickApart()
    {
        var d1 = new DateTime(2026, 6, 9, 13, 12, 21, DateTimeKind.Utc);
        DateTime d2 = d1.AddTicks(1);
        AssertionValueRenderer.RenderValue(d1).Should().NotBe(AssertionValueRenderer.RenderValue(d2));
    }

    public void RenderValue_DateTime_PreservesUtcKind()
    {
        var dt = new DateTime(2026, 6, 9, 13, 12, 21, DateTimeKind.Utc);
        AssertionValueRenderer.RenderValue(dt).Should().Be("2026-06-09T13:12:21.0000000Z");
    }

    public void RenderValue_DateTime_RendersInvariantCulture()
    {
        CultureInfo previousCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("fr-FR");
            var dt = new DateTime(2026, 6, 9, 13, 12, 21, DateTimeKind.Unspecified);
            AssertionValueRenderer.RenderValue(dt).Should().Be("2026-06-09T13:12:21.0000000");
        }
        finally
        {
            CultureInfo.CurrentCulture = previousCulture;
        }
    }

    public void RenderValue_DateTimeOffset_RendersWithOffsetAndTicks()
    {
        DateTimeOffset dto = new DateTimeOffset(2026, 6, 9, 13, 12, 21, TimeSpan.FromHours(2)).AddTicks(7);
        AssertionValueRenderer.RenderValue(dto).Should().Be("2026-06-09T13:12:21.0000007+02:00");
    }

    public void RenderValue_DateTimeOffset_DistinguishesTwoValuesOneTickApart()
    {
        var d1 = new DateTimeOffset(2026, 6, 9, 13, 12, 21, TimeSpan.Zero);
        DateTimeOffset d2 = d1.AddTicks(1);
        AssertionValueRenderer.RenderValue(d1).Should().NotBe(AssertionValueRenderer.RenderValue(d2));
    }

    public void RenderValue_TimeSpan_RendersInvariantConstantFormat()
    {
        var ts = new TimeSpan(1, 2, 3, 4, 5);
        AssertionValueRenderer.RenderValue(ts).Should().Be("1.02:03:04.0050000");
    }

    public void RenderValue_TimeSpan_DistinguishesOneTickApart()
    {
        var t1 = TimeSpan.FromSeconds(1);
        TimeSpan t2 = t1.Add(TimeSpan.FromTicks(1));
        AssertionValueRenderer.RenderValue(t1).Should().NotBe(AssertionValueRenderer.RenderValue(t2));
    }

#if NET6_0_OR_GREATER
    public void RenderValue_DateOnly_RendersIsoDate()
    {
        var d = new DateOnly(2026, 6, 9);
        AssertionValueRenderer.RenderValue(d).Should().Be("2026-06-09");
    }

    public void RenderValue_DateOnly_RendersInvariantCulture()
    {
        CultureInfo previousCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("fr-FR");
            var d = new DateOnly(2026, 6, 9);
            AssertionValueRenderer.RenderValue(d).Should().Be("2026-06-09");
        }
        finally
        {
            CultureInfo.CurrentCulture = previousCulture;
        }
    }

    public void RenderValue_TimeOnly_RendersWithSubSecondPrecision()
    {
        TimeOnly t = new TimeOnly(13, 12, 21, 456).Add(TimeSpan.FromTicks(7));
        AssertionValueRenderer.RenderValue(t).Should().Be("13:12:21.4560007");
    }

    public void RenderValue_TimeOnly_DistinguishesTwoValuesOneTickApart()
    {
        var t1 = new TimeOnly(13, 12, 21);
        TimeOnly t2 = t1.Add(TimeSpan.FromTicks(1));
        AssertionValueRenderer.RenderValue(t1).Should().NotBe(AssertionValueRenderer.RenderValue(t2));
    }
#endif

    public void RenderValue_DateTimeInList_RendersEachWithFullPrecision()
    {
        var d1 = new DateTime(2026, 6, 9, 13, 12, 21, DateTimeKind.Utc);
        var list = new List<DateTime> { d1, d1.AddTicks(1) };
        AssertionValueRenderer.RenderValue(list).Should().Be(
            "[2026-06-09T13:12:21.0000000Z, 2026-06-09T13:12:21.0000001Z]");
    }

    public void RenderValue_BoolTrue_ReturnsLowercase() =>
        AssertionValueRenderer.RenderValue(true).Should().Be("true");

    public void RenderValue_BoolFalse_ReturnsLowercase() =>
        AssertionValueRenderer.RenderValue(false).Should().Be("false");

    public void RenderValue_ListOfInts_ReturnsJsonArray()
    {
        var list = new List<int> { 1, 2, 3 };
        AssertionValueRenderer.RenderValue(list).Should().Be("[1, 2, 3]");
    }

    public void RenderValue_EmptyList_ReturnsEmptyBrackets() =>
        AssertionValueRenderer.RenderValue(new List<int>()).Should().Be("[]");

    public void RenderValue_ListOfStrings_ReturnsQuotedElements()
    {
        var list = new List<string> { "apple", "cherry", "date" };
        AssertionValueRenderer.RenderValue(list).Should().Be("[\"apple\", \"cherry\", \"date\"]");
    }

    public void RenderValue_ListWithNull_RendersNullElement()
    {
        var list = new List<string?> { "apple", null, "date" };
        AssertionValueRenderer.RenderValue(list).Should().Be("[\"apple\", null, \"date\"]");
    }

    public void RenderValue_ObjectWithToString_ReturnsToString() =>
        AssertionValueRenderer.RenderValue(new ObjectWithCustomToString("my-object")).Should().Be("my-object");

    public void RenderValue_Char_ReturnsSingleQuoted() =>
        AssertionValueRenderer.RenderValue('a').Should().Be("'a'");

    public void RenderValue_CharNewline_ReturnsEscaped() =>
        AssertionValueRenderer.RenderValue('\n').Should().Be("'\\n'");

    public void RenderValue_ObjectWhoseToStringThrows_ReturnsTypeAndExceptionName() =>
        AssertionValueRenderer.RenderValue(new ObjectWithThrowingToString()).Should().Be(
            "Microsoft.VisualStudio.TestPlatform.TestFramework.UnitTests.AssertionValueRendererTests+ObjectWithThrowingToString (ToString threw InvalidOperationException)");

    private sealed class ObjectWithCustomToString
    {
        private readonly string _value;

        public ObjectWithCustomToString(string value)
        {
            _value = value;
        }

        public override string ToString() => _value;
    }

    private sealed class ObjectWithThrowingToString
    {
        public override string ToString() => throw new InvalidOperationException("boom");
    }
}
