// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Hosts;
using Microsoft.Testing.Platform.ServerMode;
using Microsoft.Testing.Platform.Telemetry;

using Moq;

namespace Microsoft.Testing.Platform.UnitTests;

[TestClass]
public sealed class ServerTelemetryTests
{
    private readonly ServerTelemetry _serverTelemetry;
    private readonly Mock<IServerTestHost> _serverTestHost = new(MockBehavior.Loose);

    public ServerTelemetryTests()
        => _serverTelemetry = new(_serverTestHost.Object);

    [TestMethod]
    public async Task LogEvent_ForDiscovery()
    {
        Dictionary<string, object> metadata = new()
        {
            [TelemetryProperties.RequestProperties.TotalDiscoveredTestsPropertyName] = 1L,
            [TelemetryProperties.RequestProperties.IsFilterEnabledPropertyName] = TelemetryProperties.False,
        };

        await _serverTelemetry.LogEventAsync(TelemetryEvents.TestsDiscoveryEventName, metadata);
        _serverTestHost.Verify(s => s.SendTelemetryEventUpdateAsync(new TelemetryEventArgs(TelemetryEvents.TestsDiscoveryEventName, metadata)));
    }

    [TestMethod]
    public async Task LogEvent_ForRun()
    {
        Dictionary<string, object> metadata = new()
        {
            [TelemetryProperties.RequestProperties.TotalRanTestsPropertyName] = 2L,
            [TelemetryProperties.RequestProperties.TotalFailedTestsPropertyName] = 1L,
            [TelemetryProperties.RequestProperties.IsFilterEnabledPropertyName] = TelemetryProperties.True,
        };

        await _serverTelemetry.LogEventAsync(TelemetryEvents.TestsRunEventName, metadata);
        _serverTestHost.Verify(s => s.SendTelemetryEventUpdateAsync(new TelemetryEventArgs(TelemetryEvents.TestsRunEventName, metadata)));
    }
}
