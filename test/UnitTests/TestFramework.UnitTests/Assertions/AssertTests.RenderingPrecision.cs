// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using AwesomeAssertions;

using TestFramework.ForTestingMSTest;

namespace Microsoft.VisualStudio.TestTools.UnitTesting.UnitTests;

/// <summary>
/// Regression tests for https://github.com/microsoft/testfx/issues/8963 — assertion failure messages
/// must render BCL values (DateTime, DateTimeOffset, TimeSpan, DateOnly, TimeOnly, float, double, decimal)
/// with enough precision and in a culture-invariant way so distinct values never collapse to the same text.
/// </summary>
public partial class AssertTests : TestContainer
{
    public void AreEqual_DateTime_RendersFullTickPrecisionInFailureMessage()
    {
        var d1 = new DateTime(2026, 6, 9, 13, 12, 21, DateTimeKind.Utc);
        DateTime d2 = d1.AddTicks(1);

        Action action = () => Assert.AreEqual(d1, d2);

        Exception ex = action.Should().Throw<AssertFailedException>().Which;
        ex.Message.Should().Contain("expected: 2026-06-09T13:12:21.0000000Z");
        ex.Message.Should().Contain("actual:   2026-06-09T13:12:21.0000001Z");
    }

    public void AreNotEqual_DateTime_RendersWithFullTickPrecisionInFailureMessage()
    {
        DateTime d = new DateTime(2026, 6, 9, 13, 12, 21, DateTimeKind.Utc).AddTicks(42);

        Action action = () => Assert.AreNotEqual(d, d);

        Exception ex = action.Should().Throw<AssertFailedException>().Which;
        ex.Message.Should().Contain("2026-06-09T13:12:21.0000042Z");
    }

    public void AreEqual_DateTimeOffset_RendersFullTickPrecisionInFailureMessage()
    {
        var d1 = new DateTimeOffset(2026, 6, 9, 13, 12, 21, TimeSpan.FromHours(2));
        DateTimeOffset d2 = d1.AddTicks(1);

        Action action = () => Assert.AreEqual(d1, d2);

        Exception ex = action.Should().Throw<AssertFailedException>().Which;
        ex.Message.Should().Contain("expected: 2026-06-09T13:12:21.0000000+02:00");
        ex.Message.Should().Contain("actual:   2026-06-09T13:12:21.0000001+02:00");
    }

    public void AreEqual_Double_RendersFullRoundTripPrecisionInFailureMessage()
    {
        double expected = 0.1 + 0.2;
        double actual = 0.3;

        Action action = () => Assert.AreEqual(expected, actual);

        Exception ex = action.Should().Throw<AssertFailedException>().Which;
        ex.Message.Should().Contain("0.30000000000000004");
        ex.Message.Should().Contain("0.3");
    }

    public void AreEqual_Double_RendersUsingInvariantCultureEvenInCommaLocale()
    {
        CultureInfo previousCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("fr-FR");

            Action action = () => Assert.AreEqual(1.5, 2.5);

            Exception ex = action.Should().Throw<AssertFailedException>().Which;
            ex.Message.Should().Contain("expected: 1.5");
            ex.Message.Should().Contain("actual:   2.5");
            ex.Message.Should().NotContain("1,5");
            ex.Message.Should().NotContain("2,5");
        }
        finally
        {
            CultureInfo.CurrentCulture = previousCulture;
        }
    }

    public void AreEqual_Float_DistinguishesTwoNearEqualFloats()
    {
        float a = 1.0f;
        byte[] bytes = BitConverter.GetBytes(a);
        uint asInt = BitConverter.ToUInt32(bytes, 0);
        float b = BitConverter.ToSingle(BitConverter.GetBytes(asInt + 1u), 0);

        Action action = () => Assert.AreEqual(a, b);

        Exception ex = action.Should().Throw<AssertFailedException>().Which;
        // Build the two renderings the same way the assertion does and assert the
        // failure message preserves enough precision to keep them distinguishable.
        string expectedRendering = AssertionValueRenderer.RenderValue(a);
        string actualRendering = AssertionValueRenderer.RenderValue(b);
        expectedRendering.Should().NotBe(actualRendering);
        ex.Message.Should().Contain(expectedRendering);
        ex.Message.Should().Contain(actualRendering);
    }

    public void AreEqual_TimeSpan_RendersSubSecondPrecisionInFailureMessage()
    {
        var t1 = TimeSpan.FromSeconds(1);
        TimeSpan t2 = t1.Add(TimeSpan.FromTicks(1));

        Action action = () => Assert.AreEqual(t1, t2);

        Exception ex = action.Should().Throw<AssertFailedException>().Which;
        ex.Message.Should().Contain("expected: 00:00:01");
        ex.Message.Should().Contain("actual:   00:00:01.0000001");
    }

    public void AreEqual_Decimal_RendersUsingInvariantCultureEvenInCommaLocale()
    {
        CultureInfo previousCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("fr-FR");

            Action action = () => Assert.AreEqual(1.5m, 2.5m);

            Exception ex = action.Should().Throw<AssertFailedException>().Which;
            ex.Message.Should().Contain("expected: 1.5");
            ex.Message.Should().Contain("actual:   2.5");
        }
        finally
        {
            CultureInfo.CurrentCulture = previousCulture;
        }
    }

#if NET6_0_OR_GREATER
    public void AreEqual_TimeOnly_RendersSubSecondPrecisionInFailureMessage()
    {
        var t1 = new TimeOnly(13, 12, 21);
        TimeOnly t2 = t1.Add(TimeSpan.FromTicks(1));

        Action action = () => Assert.AreEqual(t1, t2);

        Exception ex = action.Should().Throw<AssertFailedException>().Which;
        ex.Message.Should().Contain("expected: 13:12:21.0000000");
        ex.Message.Should().Contain("actual:   13:12:21.0000001");
    }

    public void AreEqual_DateOnly_RendersIsoDateInFailureMessage()
    {
        var d1 = new DateOnly(2026, 6, 9);
        var d2 = new DateOnly(2026, 6, 10);

        Action action = () => Assert.AreEqual(d1, d2);

        Exception ex = action.Should().Throw<AssertFailedException>().Which;
        ex.Message.Should().Contain("expected: 2026-06-09");
        ex.Message.Should().Contain("actual:   2026-06-10");
    }
#endif

    public void AreEqual_WithDoubleDelta_RendersFullPrecisionInFailureMessage()
    {
        double expected = 1.0;
        double actual = 1.0 + 1e-10;

        Action action = () => Assert.AreEqual(expected, actual, 1e-12);

        Exception ex = action.Should().Throw<AssertFailedException>().Which;
        ex.Message.Should().Contain("1.0000000001");
        ex.Message.Should().Contain("delta:");
    }

    public void IsInRange_DateTime_RendersBoundsAndValueWithTickPrecision()
    {
        var min = new DateTime(2026, 6, 9, 0, 0, 0, DateTimeKind.Utc);
        DateTime max = min.AddTicks(1000);
        DateTime value = max.AddTicks(1);

        Action action = () => Assert.IsInRange(min, max, value);

        Exception ex = action.Should().Throw<AssertFailedException>().Which;
        ex.Message.Should().Contain("2026-06-09T00:00:00.0000000Z");
        ex.Message.Should().Contain("2026-06-09T00:00:00.0001000Z");
        ex.Message.Should().Contain("2026-06-09T00:00:00.0001001Z");
    }

    public void IsGreaterThan_DateTime_RendersBothValuesWithTickPrecision()
    {
        var a = new DateTime(2026, 6, 9, 13, 12, 21, DateTimeKind.Utc);
        DateTime b = a.AddTicks(1);

        Action action = () => Assert.IsGreaterThan(b, a);

        Exception ex = action.Should().Throw<AssertFailedException>().Which;
        ex.Message.Should().Contain("2026-06-09T13:12:21.0000000Z");
        ex.Message.Should().Contain("2026-06-09T13:12:21.0000001Z");
    }

    public void Contains_DateTimeNotInCollection_RendersTargetWithTickPrecision()
    {
        var d1 = new DateTime(2026, 6, 9, 13, 12, 21, DateTimeKind.Utc);
        DateTime target = d1.AddTicks(1);
        DateTime[] collection = [d1, d1.AddTicks(2)];

        Action action = () => Assert.Contains(target, collection);

        Exception ex = action.Should().Throw<AssertFailedException>().Which;
        ex.Message.Should().Contain("2026-06-09T13:12:21.0000001Z");
    }

    public void ContainsAll_DateTimeMissing_RendersExpectedAndCollectionWithTickPrecision()
    {
        var d1 = new DateTime(2026, 6, 9, 13, 12, 21, DateTimeKind.Utc);
        DateTime missing = d1.AddTicks(1);
        DateTime[] collection = [d1];
        DateTime[] expected = [d1, missing];

        Action action = () => Assert.ContainsAll(expected, collection);

        Exception ex = action.Should().Throw<AssertFailedException>().Which;
        ex.Message.Should().Contain("2026-06-09T13:12:21.0000000Z");
        ex.Message.Should().Contain("2026-06-09T13:12:21.0000001Z");
    }

    public void AreSequenceEqual_DateTime_RendersMismatchedElementsWithTickPrecision()
    {
        var d1 = new DateTime(2026, 6, 9, 13, 12, 21, DateTimeKind.Utc);
        DateTime[] expected = [d1];
        DateTime[] actual = [d1.AddTicks(1)];

        Action action = () => Assert.AreSequenceEqual(expected, actual);

        Exception ex = action.Should().Throw<AssertFailedException>().Which;
        ex.Message.Should().Contain("2026-06-09T13:12:21.0000000Z");
        ex.Message.Should().Contain("2026-06-09T13:12:21.0000001Z");
    }
}
