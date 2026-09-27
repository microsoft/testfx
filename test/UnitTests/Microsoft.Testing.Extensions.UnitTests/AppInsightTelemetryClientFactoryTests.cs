// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.ApplicationInsights;
using Microsoft.Testing.Extensions.Telemetry;
using Microsoft.Testing.Platform.Builder;

using Moq;

namespace Microsoft.Testing.Extensions.UnitTests;

[TestClass]
public sealed class AppInsightTelemetryClientFactoryTests
{
    private static readonly FieldInfo TelemetryClientField =
        typeof(AppInsightTelemetryClient).GetField("_telemetryClient", BindingFlags.Instance | BindingFlags.NonPublic)
        ?? throw new InvalidOperationException("Could not resolve AppInsightTelemetryClient._telemetryClient.");

    [TestMethod]
    public void Create_ReturnsNewAppInsightTelemetryClientInstanceEachCall()
    {
        AppInsightTelemetryClientFactory factory = new();

        ITelemetryClient firstClient = factory.Create("sessionId", "osVersion");
        ITelemetryClient secondClient = factory.Create("sessionId", "osVersion");

        Assert.IsInstanceOfType<AppInsightTelemetryClient>(firstClient);
        Assert.IsInstanceOfType<AppInsightTelemetryClient>(secondClient);
        Assert.AreNotSame(firstClient, secondClient);
    }

    [TestMethod]
    public void Create_WithNullSessionId_PreservesTelemetryContextValues()
    {
        AppInsightTelemetryClientFactory factory = new();

        AppInsightTelemetryClient client = Assert.IsInstanceOfType<AppInsightTelemetryClient>(factory.Create(null, "osVersion"));
        var telemetryClient = (TelemetryClient)TelemetryClientField.GetValue(client)!;

        Assert.IsNull(telemetryClient.Context.Session.Id);
        Assert.AreEqual("osVersion", telemetryClient.Context.Device.OperatingSystem);
    }

    [TestMethod]
    public void AddAppInsightsTelemetryProvider_WithNonTestApplicationBuilder_ThrowsArgumentException()
    {
        ITestApplicationBuilder builder = new Mock<ITestApplicationBuilder>().Object;

        Assert.ThrowsExactly<ArgumentException>(builder.AddAppInsightsTelemetryProvider);
    }

    [TestMethod]
    public void AddAppInsightsTelemetryProvider_WithNullBuilder_IsRejectedAsInvalidBuilder()
    {
        ITestApplicationBuilder builder = null!;

        // Null does not match TestApplicationBuilder, so it follows the invalid-builder path.
        Assert.ThrowsExactly<ArgumentException>(builder.AddAppInsightsTelemetryProvider);
    }
}
