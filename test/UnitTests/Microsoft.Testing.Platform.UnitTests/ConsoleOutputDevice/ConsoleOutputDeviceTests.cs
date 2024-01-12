// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Framework;
using Microsoft.Testing.TestInfrastructure;

namespace Microsoft.Testing.Platform.UnitTests.ConsoleOutputDevice;

[TestGroup]
public sealed class ConsoleOutputDeviceTests : TestBase
{
    public ConsoleOutputDeviceTests(ITestExecutionContext testExecutionContext)
        : base(testExecutionContext)
    {
    }

    [Arguments("1h 19m 51s 600ms", 4791600)]
    [Arguments("1s 330ms", 1330)]
    [Arguments("1m 19s 800ms", 79800)]
    [Arguments("130ms", 130)]
    [Arguments("20ms", 20)]
    [Arguments("1s 300ms", 1300)]
    [Arguments("1s 310ms", 1310)]
    [Arguments("1ms", 1)]
    [Arguments("0ms", 0)]
    public void ToHumanReadableDurationFormatTests(string expectedString, double timeSpan)
    {
        Assert.AreEqual(expectedString, OutputDevice.ConsoleOutputDevice.ToHumanReadableDuration(timeSpan));
    }
}
