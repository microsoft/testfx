// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Globalization;

using Microsoft.Testing.Internal.Framework;
using Microsoft.Testing.TestInfrastructure;

namespace Microsoft.Testing.Platform.UnitTests.ConsoleOutputDevice;

[TestGroup]
public sealed class ConsoleOutputDeviceTests : TestBase
{
    public ConsoleOutputDeviceTests(ITestExecutionContext testExecutionContext)
        : base(testExecutionContext)
    {
    }

    [Arguments("2d 01h 00m 00s 000ms", "49 00 00 000")]
    [Arguments("1d 01h 00m 00s 000ms", "25 00 00 000")]
    [Arguments("10h 00m 00s 000ms", "10 00 00 000")]
    [Arguments("1m 00s 000ms", "00 01 00 000")]
    [Arguments("11m 00s 000ms", "00 11 00 000")]
    [Arguments("11m 01s 100ms", "00 11 01 100")]
    [Arguments("59m 01s 100ms", "00 59 01 100")]
    [Arguments("1s 100ms", "00 00 01 100")]
    [Arguments("1s 000ms", "00 00 01 000")]
    [Arguments("11s 100ms", "00 00 11 100")]
    [Arguments("100ms", "00 00 00 100")]
    [Arguments("10ms", "00 00 00 010")]
    [Arguments("1ms", "00 00 00 001")]
    [Arguments("0ms", "00 00 00 000")]
    public void ToHumanReadableDurationTests(string expectedString, string time)
    {
        string[] timePart = time.Split(' ');
        Assert.AreEqual(expectedString, OutputDevice.ConsoleOutputDevice.ToHumanReadableDuration(
            new TimeSpan(
                0,
                int.Parse(timePart[0], CultureInfo.InvariantCulture),
                int.Parse(timePart[1], CultureInfo.InvariantCulture),
                int.Parse(timePart[2], CultureInfo.InvariantCulture),
                int.Parse(timePart[3], CultureInfo.InvariantCulture)).TotalMilliseconds));
    }

    [Arguments(null)]
    [Arguments(-1)]
    public void ToHumanReadableDuration_InvalidInput_ShouldReturnNull(double? durationInMs)
        => Assert.IsNull(OutputDevice.ConsoleOutputDevice.ToHumanReadableDuration(durationInMs));
}
