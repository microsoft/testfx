// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Helpers;

using Moq;

namespace Microsoft.Testing.Platform.UnitTests;

[TestClass]
public sealed class DeadlineHelperTests
{
    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("   ")]
    [DataRow("not-a-date")]
    [DataRow("12345")]
    [DataRow("01/02/2030 03:04:05")]
    [DataRow("2030/01/02T03:04:05Z")]
    [DataRow("January 2, 2030 03:04:05Z")]
    [DataRow("2030-13-01T00:00:00Z")] // invalid month
    [DataRow("2030-01-01T00:00:00")]
    [DataRow("2030-01-01T00:00:00.1234567")]
    public void TryGetDeadline_WhenUnsetOrMalformed_ReturnsFalse(string? raw)
    {
        IEnvironment environment = CreateEnvironment(EnvironmentVariableConstants.TESTINGPLATFORM_DEADLINE, raw);

        bool result = DeadlineHelper.TryGetDeadline(environment, out DateTimeOffset deadlineUtc);

        Assert.IsFalse(result);
        Assert.AreEqual(default, deadlineUtc);
    }

    [TestMethod]
    public void TryGetDeadline_WhenUtcInstant_ReturnsInstantInUtc()
    {
        IEnvironment environment = CreateEnvironment(EnvironmentVariableConstants.TESTINGPLATFORM_DEADLINE, "2030-01-01T00:00:00Z");

        bool result = DeadlineHelper.TryGetDeadline(environment, out DateTimeOffset deadlineUtc);

        Assert.IsTrue(result);
        Assert.AreEqual(new DateTimeOffset(2030, 1, 1, 0, 0, 0, TimeSpan.Zero), deadlineUtc);
        Assert.AreEqual(TimeSpan.Zero, deadlineUtc.Offset);
    }

    [TestMethod]
    public void TryGetDeadline_WhenInstantHasOffset_ConvertsToUtc()
    {
        IEnvironment environment = CreateEnvironment(EnvironmentVariableConstants.TESTINGPLATFORM_DEADLINE, "2030-01-01T00:00:00+02:00");

        bool result = DeadlineHelper.TryGetDeadline(environment, out DateTimeOffset deadlineUtc);

        Assert.IsTrue(result);
        // 00:00 at +02:00 is 22:00 the previous day in UTC.
        Assert.AreEqual(new DateTimeOffset(2029, 12, 31, 22, 0, 0, TimeSpan.Zero), deadlineUtc);
        Assert.AreEqual(TimeSpan.Zero, deadlineUtc.Offset);
    }

    [TestMethod]
    [DataRow("2030-01-01T00:00:00.1Z", 1_000_000L)]
    [DataRow("2030-01-01T00:00:00.1234567Z", 1_234_567L)]
    public void TryGetDeadline_WhenInstantHasFractionalSeconds_ReturnsInstantInUtc(string raw, long fractionalTicks)
    {
        IEnvironment environment = CreateEnvironment(EnvironmentVariableConstants.TESTINGPLATFORM_DEADLINE, raw);

        bool result = DeadlineHelper.TryGetDeadline(environment, out DateTimeOffset deadlineUtc);

        Assert.IsTrue(result);
        Assert.AreEqual(new DateTimeOffset(2030, 1, 1, 0, 0, 0, TimeSpan.Zero).AddTicks(fractionalTicks), deadlineUtc);
        Assert.AreEqual(TimeSpan.Zero, deadlineUtc.Offset);
    }

    [TestMethod]
    [DataRow("45", 45)]
    [DataRow("45s", 45)]
    [DataRow("2m", 120)]
    [DataRow("0", 0)]
    public void GetStopMargin_WhenParsable_ReturnsParsedValue(string raw, int expectedSeconds)
    {
        IEnvironment environment = CreateEnvironment(EnvironmentVariableConstants.TESTINGPLATFORM_DEADLINE_STOP_MARGIN, raw);

        Assert.AreEqual(TimeSpan.FromSeconds(expectedSeconds), DeadlineHelper.GetStopMargin(environment));
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("   ")]
    [DataRow("abc")]
    [DataRow("-1s")] // negative is not accepted by the parser, so the default is used
    public void GetStopMargin_WhenUnsetOrUnparsable_ReturnsDefault(string? raw)
    {
        IEnvironment environment = CreateEnvironment(EnvironmentVariableConstants.TESTINGPLATFORM_DEADLINE_STOP_MARGIN, raw);

        Assert.AreEqual(TimeSpan.FromSeconds(60), DeadlineHelper.GetStopMargin(environment));
    }

    [TestMethod]
    [DataRow("15", 15)]
    [DataRow("15s", 15)]
    [DataRow("1m", 60)]
    public void GetDumpMargin_WhenParsable_ReturnsParsedValue(string raw, int expectedSeconds)
    {
        IEnvironment environment = CreateEnvironment(EnvironmentVariableConstants.TESTINGPLATFORM_DEADLINE_DUMP_MARGIN, raw);

        Assert.AreEqual(TimeSpan.FromSeconds(expectedSeconds), DeadlineHelper.GetDumpMargin(environment));
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("nonsense")]
    public void GetDumpMargin_WhenUnsetOrUnparsable_ReturnsDefault(string? raw)
    {
        IEnvironment environment = CreateEnvironment(EnvironmentVariableConstants.TESTINGPLATFORM_DEADLINE_DUMP_MARGIN, raw);

        Assert.AreEqual(TimeSpan.FromSeconds(30), DeadlineHelper.GetDumpMargin(environment));
    }

    [TestMethod]
    public void GetTimerDueTime_WhenDeadlineExceedsTimerLimit_ReturnsBoundedIntervals()
    {
        DateTimeOffset now = new(2030, 1, 1, 12, 0, 0, TimeSpan.Zero);
        DateTimeOffset deadline = now + TimeSpan.FromDays(60);
        var maxTimerDueTime = TimeSpan.FromMilliseconds(uint.MaxValue - 1);

        TimeSpan firstInterval = DeadlineHelper.GetTimerDueTime(deadline, now);
        DateTimeOffset afterFirstInterval = now + firstInterval;
        TimeSpan secondInterval = DeadlineHelper.GetTimerDueTime(deadline, afterFirstInterval);

        Assert.AreEqual(maxTimerDueTime, firstInterval);
        Assert.IsGreaterThan(TimeSpan.Zero, secondInterval);
        Assert.IsGreaterThan(secondInterval, firstInterval);
        Assert.AreEqual(TimeSpan.Zero, DeadlineHelper.GetTimerDueTime(deadline, afterFirstInterval + secondInterval));
        Assert.AreEqual(TimeSpan.Zero, DeadlineHelper.GetTimerDueTime(deadline, deadline));
        Assert.AreEqual(TimeSpan.Zero, DeadlineHelper.GetTimerDueTime(deadline, deadline.AddSeconds(1)));
    }

    [TestMethod]
    public void SubtractSaturating_WhenNoUnderflow_SubtractsMargin()
    {
        var instant = new DateTimeOffset(2030, 1, 1, 0, 0, 0, TimeSpan.Zero);

        DateTimeOffset result = DeadlineHelper.SubtractSaturating(instant, TimeSpan.FromSeconds(60));

        Assert.AreEqual(new DateTimeOffset(2029, 12, 31, 23, 59, 0, TimeSpan.Zero), result);
    }

    [TestMethod]
    public void SubtractSaturating_WhenMarginIsZero_ReturnsInstant()
    {
        var instant = new DateTimeOffset(2030, 1, 1, 0, 0, 0, TimeSpan.Zero);

        Assert.AreEqual(instant, DeadlineHelper.SubtractSaturating(instant, TimeSpan.Zero));
    }

    [TestMethod]
    public void SubtractSaturating_WhenMarginWouldUnderflow_ClampsToMinValue()
    {
        DateTimeOffset instant = DateTimeOffset.MinValue.AddSeconds(10);

        DateTimeOffset result = DeadlineHelper.SubtractSaturating(instant, TimeSpan.FromSeconds(60));

        Assert.AreEqual(DateTimeOffset.MinValue, result);
    }

    [TestMethod]
    public void SubtractSaturating_WhenMarginEqualsAvailableRange_ReturnsMinValue()
    {
        DateTimeOffset instant = DateTimeOffset.MinValue.AddSeconds(60);

        // margin (60s) is not greater than the available range (60s), so the exact subtraction is used
        // and lands precisely on MinValue.
        DateTimeOffset result = DeadlineHelper.SubtractSaturating(instant, TimeSpan.FromSeconds(60));

        Assert.AreEqual(DateTimeOffset.MinValue, result);
    }

    private static IEnvironment CreateEnvironment(string variableName, string? value)
    {
        Mock<IEnvironment> environment = new();
        _ = environment.Setup(x => x.GetEnvironmentVariable(It.IsAny<string>())).Returns((string?)null);

        // A null value models "variable unset"; the default mock already returns null, so only wire up
        // an explicit (possibly empty/whitespace) value.
        if (value is not null)
        {
            _ = environment.Setup(x => x.GetEnvironmentVariable(variableName)).Returns(value);
        }

        return environment.Object;
    }
}
