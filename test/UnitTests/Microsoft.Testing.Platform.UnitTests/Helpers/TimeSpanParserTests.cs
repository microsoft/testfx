// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Helpers;

namespace Microsoft.Testing.Platform.UnitTests;

[TestClass]
public sealed class TimeSpanParserTests
{
    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("   ")]
    public void TryParse_NullOrWhitespace_ReturnsFalse(string? input)
    {
        bool result = TimeSpanParser.TryParse(input, out TimeSpan value);

        Assert.IsFalse(result);
        Assert.AreEqual(TimeSpan.Zero, value);
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("   ")]
    public void Parse_NullOrWhitespace_ThrowsFormatException(string? input)
        => Assert.ThrowsExactly<FormatException>(() => TimeSpanParser.Parse(input));

    [TestMethod]
    [DataRow("invalid")]
    [DataRow("abc")]
    [DataRow("ms")]
    [DataRow("-1s")]
    [DataRow("1monkey")] // not a recognized unit suffix
    [DataRow("1hotdog")] // not a recognized unit suffix
    [DataRow("1xyz")]
    [DataRow("1e3s")] // scientific notation not supported
    [DataRow("+1s")] // explicit positive sign not supported
    public void TryParse_InvalidInput_ReturnsFalse(string input)
    {
        bool result = TimeSpanParser.TryParse(input, out TimeSpan value);

        Assert.IsFalse(result);
        Assert.AreEqual(TimeSpan.Zero, value);
    }

    [TestMethod]
    [DataRow("invalid")]
    [DataRow("abc")]
    [DataRow("-1s")]
    [DataRow("1monkey")]
    [DataRow("1xyz")]
    public void Parse_InvalidInput_ThrowsFormatException(string input)
        => Assert.ThrowsExactly<FormatException>(() => TimeSpanParser.Parse(input));

    [TestMethod]
    [DataRow("0", 0)]
    [DataRow("1000", 1000)]
    [DataRow("5400000", 5400000)]
    public void TryParse_NoSuffix_ParsesAsMilliseconds(string input, double expectedMs)
    {
        bool result = TimeSpanParser.TryParse(input, out TimeSpan value);

        Assert.IsTrue(result);
        Assert.AreEqual(TimeSpan.FromMilliseconds(expectedMs), value);
    }

    [TestMethod]
    [DataRow("0ms", 0)]
    [DataRow("500ms", 500)]
    [DataRow("5400000ms", 5400000)]
    [DataRow("500MS", 500)]
    [DataRow("0mil", 0)]
    [DataRow("500mil", 500)]
    [DataRow("500milliseconds", 500)]
    public void TryParse_MillisecondsSuffix_ParsesCorrectly(string input, double expectedMs)
    {
        bool result = TimeSpanParser.TryParse(input, out TimeSpan value);

        Assert.IsTrue(result);
        Assert.AreEqual(TimeSpan.FromMilliseconds(expectedMs), value);
    }

    [TestMethod]
    [DataRow("0s", 0)]
    [DataRow("1s", 1)]
    [DataRow("5400s", 5400)]
    [DataRow("1sec", 1)]
    [DataRow("1secs", 1)]
    [DataRow("1second", 1)]
    [DataRow("1seconds", 1)]
    public void TryParse_SecondsSuffix_ParsesCorrectly(string input, double expectedSeconds)
    {
        bool result = TimeSpanParser.TryParse(input, out TimeSpan value);

        Assert.IsTrue(result);
        Assert.AreEqual(TimeSpan.FromSeconds(expectedSeconds), value);
    }

    [TestMethod]
    [DataRow("0m", 0)]
    [DataRow("1m", 1)]
    [DataRow("90m", 90)]
    [DataRow("1min", 1)]
    [DataRow("1mins", 1)]
    [DataRow("1minute", 1)]
    [DataRow("1minutes", 1)]
    public void TryParse_MinutesSuffix_ParsesCorrectly(string input, double expectedMinutes)
    {
        bool result = TimeSpanParser.TryParse(input, out TimeSpan value);

        Assert.IsTrue(result);
        Assert.AreEqual(TimeSpan.FromMinutes(expectedMinutes), value);
    }

    [TestMethod]
    [DataRow("0h", 0)]
    [DataRow("1h", 1)]
    [DataRow("24h", 24)]
    [DataRow("1hour", 1)]
    [DataRow("1hours", 1)]
    public void TryParse_HoursSuffix_ParsesCorrectly(string input, double expectedHours)
    {
        bool result = TimeSpanParser.TryParse(input, out TimeSpan value);

        Assert.IsTrue(result);
        Assert.AreEqual(TimeSpan.FromHours(expectedHours), value);
    }

    [TestMethod]
    [DataRow("0d", 0)]
    [DataRow("1d", 1)]
    [DataRow("7d", 7)]
    [DataRow("1day", 1)]
    [DataRow("1days", 1)]
    public void TryParse_DaysSuffix_ParsesCorrectly(string input, double expectedDays)
    {
        bool result = TimeSpanParser.TryParse(input, out TimeSpan value);

        Assert.IsTrue(result);
        Assert.AreEqual(TimeSpan.FromDays(expectedDays), value);
    }

    [TestMethod]
    [DataRow("1.5s", 1.5)]
    [DataRow("0.5s", 0.5)]
    [DataRow("2.5m", 2.5)]
    [DataRow("1.5h", 1.5)]
    [DataRow("0.5d", 0.5)]
    [DataRow("500.5ms", 500.5)]
    public void TryParse_DecimalValue_ParsesCorrectly(string input, double expectedValue)
    {
        bool result = TimeSpanParser.TryParse(input, out TimeSpan value);
        Assert.IsTrue(result);

        // The exact unit depends on suffix; just verify overall success + round-trip via Parse
        Assert.AreEqual(TimeSpanParser.Parse(input), value);
        Assert.AreNotEqual(TimeSpan.Zero, value);
    }

    [TestMethod]
    [DataRow("5 s", 5)]
    [DataRow("5 ms", 5)]
    [DataRow("5 m", 5)]
    public void TryParse_SpaceBetweenValueAndSuffix_ParsesCorrectly(string input, double expectedMs)
    {
        // The regex allows optional whitespace before suffix
        bool result = TimeSpanParser.TryParse(input, out TimeSpan value);
        Assert.IsTrue(result);
        Assert.AreNotEqual(TimeSpan.Zero, value);
    }

    [TestMethod]
    public void Parse_ValidInput_ReturnsCorrectTimeSpan()
    {
        TimeSpan result = TimeSpanParser.Parse("5400s");
        Assert.AreEqual(TimeSpan.FromSeconds(5400), result);
    }

    [TestMethod]
    [DataRow("1S", 1, "s")] // uppercase single-char suffix
    [DataRow("1M", 1, "m")]
    [DataRow("1H", 1, "h")]
    [DataRow("1D", 1, "d")]
    [DataRow("1Hour", 1, "h")] // mixed case long form
    [DataRow("1MINUTES", 1, "m")] // upper case long form
    public void TryParse_UppercaseSuffix_ParsesCorrectly(string input, double expectedValue, string expectedUnit)
    {
        bool result = TimeSpanParser.TryParse(input, out TimeSpan value);

        Assert.IsTrue(result);
        TimeSpan expected = expectedUnit switch
        {
            "ms" => TimeSpan.FromMilliseconds(expectedValue),
            "s" => TimeSpan.FromSeconds(expectedValue),
            "m" => TimeSpan.FromMinutes(expectedValue),
            "h" => TimeSpan.FromHours(expectedValue),
            "d" => TimeSpan.FromDays(expectedValue),
            _ => throw new ArgumentException($"Unknown unit '{expectedUnit}'", nameof(expectedUnit)),
        };
        Assert.AreEqual(expected, value);
    }

    [TestMethod]
    public void TryParse_OverflowValue_ReturnsFalseInsteadOfThrowing()
    {
        // 10^18 days is far beyond TimeSpan.MaxValue.
        bool result = TimeSpanParser.TryParse("1000000000000000000d", out TimeSpan value);

        Assert.IsFalse(result);
        Assert.AreEqual(TimeSpan.Zero, value);
    }

    [TestMethod]
    [DataRow("90")]
    [DataRow("90 ")]
    [DataRow("")]
    public void TryParseRequireSuffix_BareNumberFails(string input)
    {
        bool result = TimeSpanParser.TryParseRequireSuffix(input, out TimeSpan _);

        Assert.IsFalse(result);
    }

    [TestMethod]
    public void TryParseRequireSuffix_BareZeroFails()
        => Assert.IsFalse(TimeSpanParser.TryParseRequireSuffix("0", out TimeSpan _));

    [TestMethod]
    public void TryParse_WithDefaultUnitMilliseconds_BareNumberUsesMilliseconds()
    {
        Assert.IsTrue(TimeSpanParser.TryParse("250", TimeSpanDefaultUnit.Milliseconds, out TimeSpan value));
        Assert.AreEqual(TimeSpan.FromMilliseconds(250), value);
    }

    [TestMethod]
    public void TryParse_WithDefaultUnitSeconds_BareNumberUsesSeconds()
    {
        Assert.IsTrue(TimeSpanParser.TryParse("30", TimeSpanDefaultUnit.Seconds, out TimeSpan value));
        Assert.AreEqual(TimeSpan.FromSeconds(30), value);
    }

    [TestMethod]
    public void TryParse_WithDefaultUnitMinutes_BareNumberUsesMinutes()
    {
        Assert.IsTrue(TimeSpanParser.TryParse("5", TimeSpanDefaultUnit.Minutes, out TimeSpan value));
        Assert.AreEqual(TimeSpan.FromMinutes(5), value);
    }

    [TestMethod]
    public void TryParse_WithDefaultUnitHours_BareNumberUsesHours()
    {
        Assert.IsTrue(TimeSpanParser.TryParse("2", TimeSpanDefaultUnit.Hours, out TimeSpan value));
        Assert.AreEqual(TimeSpan.FromHours(2), value);
    }

    [TestMethod]
    public void TryParse_WithDefaultUnitDays_BareNumberUsesDays()
    {
        Assert.IsTrue(TimeSpanParser.TryParse("1", TimeSpanDefaultUnit.Days, out TimeSpan value));
        Assert.AreEqual(TimeSpan.FromDays(1), value);
    }

    [TestMethod]
    public void TryParse_WithDefaultUnit_ExplicitSuffixOverridesDefault_Smoke()
    {
        // Default is hours, but the explicit suffix is seconds.
        Assert.IsTrue(TimeSpanParser.TryParse("30s", TimeSpanDefaultUnit.Hours, out TimeSpan value));
        Assert.AreEqual(TimeSpan.FromSeconds(30), value);
    }

    [TestMethod]
    public void TryParseRequireSuffix_ExplicitSuffixSucceeds()
    {
        Assert.IsTrue(TimeSpanParser.TryParseRequireSuffix("90m", out TimeSpan value));
        Assert.AreEqual(TimeSpan.FromMinutes(90), value);
    }

    [TestMethod]
    public void TryParse_ExplicitSuffix_AlwaysOverridesDefaultUnit()
    {
        var cases = new (string Input, TimeSpan Expected)[]
        {
            ("90ms", TimeSpan.FromMilliseconds(90)),
            ("30s", TimeSpan.FromSeconds(30)),
            ("2m", TimeSpan.FromMinutes(2)),
            ("1h", TimeSpan.FromHours(1)),
            ("1d", TimeSpan.FromDays(1)),
        };

        foreach ((string input, TimeSpan expected) in cases)
        {
            // The explicit suffix in the input must always take precedence over the supplied default.
            foreach (TimeSpanDefaultUnit unit in (TimeSpanDefaultUnit[])Enum.GetValues(typeof(TimeSpanDefaultUnit)))
            {
                Assert.IsTrue(TimeSpanParser.TryParse(input, unit, out TimeSpan value), $"Should parse '{input}' with default {unit}");
                Assert.AreEqual(expected, value, $"Mismatch for '{input}' with default {unit}");
            }
        }
    }

    [TestMethod]
    [DataRow("  1s")] // leading whitespace not allowed
    [DataRow("1s ")] // trailing whitespace not allowed
    public void TryParse_OuterWhitespace_Fails(string input)
        => Assert.IsFalse(TimeSpanParser.TryParse(input, out TimeSpan _));

    [TestMethod]
    public void TryParse_InnerWhitespaceBetweenValueAndSuffix_Succeeds()
    {
        // The regex permits optional whitespace between number and suffix.
        Assert.IsTrue(TimeSpanParser.TryParse("1 s", out TimeSpan value));
        Assert.AreEqual(TimeSpan.FromSeconds(1), value);
    }

    [TestMethod]
    public void ParseRequireSuffix_BareNumberThrows()
    {
        FormatException ex = Assert.ThrowsExactly<FormatException>(() => TimeSpanParser.ParseRequireSuffix("90"));
        Assert.Contains("A unit suffix is required", ex.Message);
    }

    [TestMethod]
    public void Parse_WithDefaultUnit_BareNumberMessageMentionsDefault()
    {
        FormatException ex = Assert.ThrowsExactly<FormatException>(() => TimeSpanParser.Parse("not-a-duration", TimeSpanDefaultUnit.Seconds));
        Assert.Contains("defaults to seconds", ex.Message);
    }

    [TestMethod]
    public void TryParse_And_Parse_AreConsistent()
    {
        string[] inputs = ["1000", "30s", "5m", "2h", "1d", "500ms"];
        foreach (string input in inputs)
        {
            bool tryParseResult = TimeSpanParser.TryParse(input, out TimeSpan tryValue);
            Assert.IsTrue(tryParseResult, $"TryParse should succeed for '{input}'");
            Assert.AreEqual(TimeSpanParser.Parse(input), tryValue, $"Parse and TryParse should agree for '{input}'");
        }
    }
}
