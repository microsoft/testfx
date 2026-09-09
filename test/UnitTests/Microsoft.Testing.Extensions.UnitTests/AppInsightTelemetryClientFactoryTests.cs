// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.Telemetry;
using Microsoft.Testing.Platform.Builder;

using Moq;

namespace Microsoft.Testing.Extensions.UnitTests;

[TestClass]
public sealed class AppInsightTelemetryClientFactoryTests
{
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
    public void Create_WithNullSessionId_DoesNotThrow()
    {
        AppInsightTelemetryClientFactory factory = new();

        ITelemetryClient client = factory.Create(null, "osVersion");

        Assert.IsInstanceOfType<AppInsightTelemetryClient>(client);
    }

    [TestMethod]
    public void AddAppInsightsTelemetryProvider_WithNonTestApplicationBuilder_ThrowsArgumentException()
    {
        ITestApplicationBuilder builder = new Mock<ITestApplicationBuilder>().Object;

        Assert.ThrowsExactly<ArgumentException>(builder.AddAppInsightsTelemetryProvider);
    }

    [TestMethod]
    public void AddAppInsightsTelemetryProvider_WithNullBuilder_ThrowsArgumentException()
    {
        ITestApplicationBuilder builder = null!;

        Assert.ThrowsExactly<ArgumentException>(builder.AddAppInsightsTelemetryProvider);
    }
}
