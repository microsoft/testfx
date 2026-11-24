// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Telemetry;

namespace Microsoft.Testing.Platform.UnitTests;

[TestClass]
public sealed class NopTelemetryServiceTests
{
    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task LogEventAsync_WhenEnabled_CompletesWithoutThrowing()
    {
        Task task = new NopTelemetryService(enabled: true).LogEventAsync("event", new Dictionary<string, object>(), TestContext.CancellationToken);

        await task;

        Assert.IsTrue(task.IsCompleted);
    }

    [TestMethod]
    public async Task LogEventAsync_WhenDisabled_ThrowsInvalidOperationException()
    {
        InvalidOperationException exception = await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => new NopTelemetryService(enabled: false).LogEventAsync("event", new Dictionary<string, object>(), TestContext.CancellationToken));

        Assert.IsFalse(string.IsNullOrWhiteSpace(exception.Message));
    }
}
