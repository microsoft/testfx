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

        action.Should().Throw<AssertFailedException>()
            .WithMessage(
                """
                Assertion failed. Expected values to be equal.

                expected: 2026-06-09T13:12:21.0000000Z
                actual:   2026-06-09T13:12:21.0000001Z

                Assert.AreEqual(d1, d2)
                """);
    }

    public void AreNotEqual_DateTime_RendersWithFullTickPrecisionInFailureMessage()
    {
        DateTime d = new DateTime(2026, 6, 9, 13, 12, 21, DateTimeKind.Utc).AddTicks(42);

        Action action = () => Assert.AreNotEqual(d, d);

        action.Should().Throw<AssertFailedException>()
            .WithMessage(
                """
                Assertion failed. Expected values to differ.

                notExpected: 2026-06-09T13:12:21.0000042Z
                actual:      2026-06-09T13:12:21.0000042Z

                Assert.AreNotEqual(d, d)
                """);
    }

    public void AreEqual_DateTimeOffset_RendersFullTickPrecisionInFailureMessage()
    {
        var d1 = new DateTimeOffset(2026, 6, 9, 13, 12, 21, TimeSpan.FromHours(2));
        DateTimeOffset d2 = d1.AddTicks(1);

        Action action = () => Assert.AreEqual(d1, d2);

        action.Should().Throw<AssertFailedException>()
            .WithMessage(
                """
                Assertion failed. Expected values to be equal.

                expected: 2026-06-09T13:12:21.0000000+02:00
                actual:   2026-06-09T13:12:21.0000001+02:00

                Assert.AreEqual(d1, d2)
                """);
    }

    public void AreEqual_Double_RendersFullRoundTripPrecisionInFailureMessage()
    {
        double expected = 0.1 + 0.2;
        double actual = 0.3;

        Action action = () => Assert.AreEqual(expected, actual);

        action.Should().Throw<AssertFailedException>()
            .WithMessage(
                """
                Assertion failed. Expected values to be equal.

                expected: 0.30000000000000004
                actual:   0.3

                Assert.AreEqual(expected, actual)
                """);
    }

    public void AreEqual_Double_RendersUsingInvariantCultureEvenInCommaLocale()
    {
        CultureInfo previousCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("fr-FR");

            Action action = () => Assert.AreEqual(1.5, 2.5);

            action.Should().Throw<AssertFailedException>()
                .WithMessage(
                    """
                    Assertion failed. Expected values to be equal.

                    expected: 1.5
                    actual:   2.5

                    Assert.AreEqual(1.5, 2.5)
                    """);
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

        // The exact "R" format rendering of an adjacent float differs across .NET Framework and modern
        // .NET (e.g. "1.00000012" vs "1.0000001"), so build the expected message from the renderer rather
        // than hard-coding it. AssertionValueRendererTests verifies that the two renderings differ.
        string expectedRendering = AssertionValueRenderer.RenderValue(a);
        string actualRendering = AssertionValueRenderer.RenderValue(b);
        expectedRendering.Should().NotBe(actualRendering);

        action.Should().Throw<AssertFailedException>()
            .WithMessage(
                $"""
                Assertion failed. Expected values to be equal.

                expected: {expectedRendering}
                actual:   {actualRendering}

                Assert.AreEqual(a, b)
                """);
    }

    public void AreEqual_TimeSpan_RendersSubSecondPrecisionInFailureMessage()
    {
        var t1 = TimeSpan.FromSeconds(1);
        TimeSpan t2 = t1.Add(TimeSpan.FromTicks(1));

        Action action = () => Assert.AreEqual(t1, t2);

        action.Should().Throw<AssertFailedException>()
            .WithMessage(
                """
                Assertion failed. Expected values to be equal.

                expected: 00:00:01
                actual:   00:00:01.0000001

                Assert.AreEqual(t1, t2)
                """);
    }

    public void AreEqual_Decimal_RendersUsingInvariantCultureEvenInCommaLocale()
    {
        CultureInfo previousCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("fr-FR");

            Action action = () => Assert.AreEqual(1.5m, 2.5m);

            action.Should().Throw<AssertFailedException>()
                .WithMessage(
                    """
                    Assertion failed. Expected values to be equal.

                    expected: 1.5
                    actual:   2.5

                    Assert.AreEqual(1.5m, 2.5m)
                    """);
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

        action.Should().Throw<AssertFailedException>()
            .WithMessage(
                """
                Assertion failed. Expected values to be equal.

                expected: 13:12:21.0000000
                actual:   13:12:21.0000001

                Assert.AreEqual(t1, t2)
                """);
    }

    public void AreEqual_DateOnly_RendersIsoDateInFailureMessage()
    {
        var d1 = new DateOnly(2026, 6, 9);
        var d2 = new DateOnly(2026, 6, 10);

        Action action = () => Assert.AreEqual(d1, d2);

        action.Should().Throw<AssertFailedException>()
            .WithMessage(
                """
                Assertion failed. Expected values to be equal.

                expected: 2026-06-09
                actual:   2026-06-10

                Assert.AreEqual(d1, d2)
                """);
    }
#endif

    public void AreEqual_WithDoubleDelta_RendersFullPrecisionInFailureMessage()
    {
        double expected = 1.0;
        double actual = 1.0 + 1e-10;

        Action action = () => Assert.AreEqual(expected, actual, 1e-12);

        action.Should().Throw<AssertFailedException>()
            .WithMessage(
                """
                Assertion failed. Expected values to be equal within tolerance.

                expected: 1
                actual:   1.0000000001
                delta:    1E-12

                Assert.AreEqual(expected, actual, <delta>)
                """);
    }

    public void IsInRange_DateTime_RendersBoundsAndValueWithTickPrecision()
    {
        var min = new DateTime(2026, 6, 9, 0, 0, 0, DateTimeKind.Utc);
        DateTime max = min.AddTicks(1000);
        DateTime value = max.AddTicks(1);

        Action action = () => Assert.IsInRange(min, max, value);

        action.Should().Throw<AssertFailedException>()
            .WithMessage(
                """
                Assertion failed. Expected value to be within the inclusive range.

                expected: [2026-06-09T00:00:00.0000000Z, 2026-06-09T00:00:00.0001000Z]
                actual:   2026-06-09T00:00:00.0001001Z

                Assert.IsInRange(min, max, value)
                """);
    }

    public void IsGreaterThan_DateTime_RendersBothValuesWithTickPrecision()
    {
        var a = new DateTime(2026, 6, 9, 13, 12, 21, DateTimeKind.Utc);
        DateTime b = a.AddTicks(1);

        Action action = () => Assert.IsGreaterThan(b, a);

        action.Should().Throw<AssertFailedException>()
            .WithMessage(
                """
                Assertion failed. Expected value to be greater than the lower bound.

                lower bound: 2026-06-09T13:12:21.0000001Z
                actual:      2026-06-09T13:12:21.0000000Z

                Assert.IsGreaterThan(b, a)
                """);
    }

    public void Contains_DateTimeNotInCollection_RendersTargetWithTickPrecision()
    {
        var d1 = new DateTime(2026, 6, 9, 13, 12, 21, DateTimeKind.Utc);
        DateTime target = d1.AddTicks(1);
        DateTime[] collection = [d1, d1.AddTicks(2)];

        Action action = () => Assert.Contains(target, collection);

        action.Should().Throw<AssertFailedException>()
            .WithMessage(
                """
                Assertion failed. Expected collection to contain the specified element.

                expected: 2026-06-09T13:12:21.0000001Z

                Assert.Contains(target, collection)
                """);
    }

    public void ContainsAll_DateTimeMissing_RendersExpectedAndCollectionWithTickPrecision()
    {
        var d1 = new DateTime(2026, 6, 9, 13, 12, 21, DateTimeKind.Utc);
        DateTime missing = d1.AddTicks(1);
        DateTime[] collection = [d1];
        DateTime[] expected = [d1, missing];

        Action action = () => Assert.ContainsAll(expected, collection);

        action.Should().Throw<AssertFailedException>()
            .WithMessage(
                """
                Assertion failed. Expected collection to contain all specified items.

                missing:    [2026-06-09T13:12:21.0000001Z]
                expected:   [2026-06-09T13:12:21.0000000Z, 2026-06-09T13:12:21.0000001Z]
                collection: [2026-06-09T13:12:21.0000000Z]

                Assert.ContainsAll(expected, collection)
                """);
    }

    public void AreSequenceEqual_DateTime_RendersMismatchedElementsWithTickPrecision()
    {
        var d1 = new DateTime(2026, 6, 9, 13, 12, 21, DateTimeKind.Utc);
        DateTime[] expected = [d1];
        DateTime[] actual = [d1.AddTicks(1)];

        Action action = () => Assert.AreSequenceEqual(expected, actual);

        action.Should().Throw<AssertFailedException>()
            .WithMessage(
                """
                Assertion failed. Expected sequences to be equal.
                Sequences have 1 element(s). 1 element(s) differ. First difference at index 0.

                expected: [2026-06-09T13:12:21.0000000Z]
                actual:   [2026-06-09T13:12:21.0000001Z]

                Assert.AreSequenceEqual(expected, actual)
                """);
    }
}
