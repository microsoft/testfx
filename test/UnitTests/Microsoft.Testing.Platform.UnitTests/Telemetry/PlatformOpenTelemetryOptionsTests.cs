// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Telemetry;

using Moq;

namespace Microsoft.Testing.Platform.UnitTests;

[TestClass]
public sealed class PlatformOpenTelemetryOptionsTests
{
    private static IEnvironment CreateEnvironment(string? captureOutput = null, string? limit = null)
    {
        Mock<IEnvironment> environment = new();
        environment.Setup(e => e.GetEnvironmentVariable(EnvironmentVariableConstants.TESTINGPLATFORM_OTEL_CAPTURE_TEST_OUTPUT)).Returns(captureOutput);
        environment.Setup(e => e.GetEnvironmentVariable(EnvironmentVariableConstants.TESTINGPLATFORM_OTEL_ATTRIBUTE_VALUE_LENGTH_LIMIT)).Returns(limit);
        return environment.Object;
    }

    [TestMethod]
    [DataRow("0")]
    [DataRow("-3")]
    [DataRow("abc")]
    [DataRow(null)]
    public void FromEnvironment_NonPositiveOrInvalidLimit_UsesDefault(string? limit)
        => Assert.AreEqual(
            PlatformOpenTelemetryOptions.DefaultAttributeValueLengthLimit,
            PlatformOpenTelemetryOptions.FromEnvironment(CreateEnvironment(limit: limit)).AttributeValueLengthLimit);

    [TestMethod]
    public void FromEnvironment_PositiveLimit_IsUsed()
        => Assert.AreEqual(1, PlatformOpenTelemetryOptions.FromEnvironment(CreateEnvironment(limit: "1")).AttributeValueLengthLimit);

    [TestMethod]
    public void Truncate_ValueAtLimitIsKeptAndLongerValueGetsEllipsis()
    {
        var options = PlatformOpenTelemetryOptions.FromEnvironment(CreateEnvironment(limit: "5"));

        Assert.AreEqual("12345", options.Truncate("12345"));
        Assert.AreEqual("12345…", options.Truncate("123456"));
        Assert.IsNull(options.Truncate(null));
    }

    [TestMethod]
    [DataRow("1")]
    [DataRow("true")]
    [DataRow("True")]
    [DataRow("TRUE")]
    public void FromEnvironment_TruthyCaptureValue_CapturesOutput(string value)
        => Assert.IsTrue(PlatformOpenTelemetryOptions.FromEnvironment(CreateEnvironment(captureOutput: value)).CaptureTestOutput);

    [TestMethod]
    [DataRow("0")]
    [DataRow("false")]
    [DataRow("False")]
    [DataRow("FALSE")]
    public void FromEnvironment_FalsyCaptureValue_DoesNotCaptureOutput(string value)
        => Assert.IsFalse(PlatformOpenTelemetryOptions.FromEnvironment(CreateEnvironment(captureOutput: value)).CaptureTestOutput);

    [TestMethod]
    [DataRow(null)]
    [DataRow("garbage")]
    public void FromEnvironment_UnsetOrUnknownCaptureValue_DefaultsToCapturing(string? value)
        => Assert.IsTrue(PlatformOpenTelemetryOptions.FromEnvironment(CreateEnvironment(captureOutput: value)).CaptureTestOutput);
}
