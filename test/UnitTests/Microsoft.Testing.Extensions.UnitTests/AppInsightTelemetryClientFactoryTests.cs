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

        ITelemetryClient first = factory.Create("session-id", "osVersion");
        ITelemetryClient second = factory.Create("session-id", "osVersion");

        Assert.IsInstanceOfType<AppInsightTelemetryClient>(first);
        Assert.IsInstanceOfType<AppInsightTelemetryClient>(second);
        Assert.AreNotSame(first, second);
    }

    [TestMethod]
    public void Create_WithNullSessionId_DoesNotThrow()
    {
        AppInsightTelemetryClientFactory factory = new();

        ITelemetryClient client = factory.Create(currentSessionId: null, osVersion: "osVersion");

        Assert.IsInstanceOfType<AppInsightTelemetryClient>(client);
    }
}

[TestClass]
public sealed class AppInsightsTelemetryProviderExtensionsTests
{
    [TestMethod]
    public void AddAppInsightsTelemetryProvider_WithNonTestApplicationBuilder_ThrowsArgumentException()
    {
        // The extension only supports the concrete TestApplicationBuilder implementation; any other
        // ITestApplicationBuilder (e.g. a test double) must be rejected rather than silently ignored.
        Mock<ITestApplicationBuilder> builder = new();

        Assert.ThrowsExactly<ArgumentException>(() => builder.Object.AddAppInsightsTelemetryProvider());
    }

    [TestMethod]
    public void AddAppInsightsTelemetryProvider_WithNullBuilder_ThrowsArgumentException()
        => Assert.ThrowsExactly<ArgumentException>(() => ((ITestApplicationBuilder)null!).AddAppInsightsTelemetryProvider());
}
