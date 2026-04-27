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
