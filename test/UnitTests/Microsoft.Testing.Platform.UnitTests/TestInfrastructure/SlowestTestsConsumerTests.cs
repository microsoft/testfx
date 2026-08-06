// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.TestHost;
using Microsoft.Testing.TestInfrastructure;

namespace Microsoft.Testing.Platform.UnitTests.TestInfrastructure;

[TestClass]
public sealed class SlowestTestsConsumerTests
{
    [TestMethod]
    public async Task OnTestSessionFinishingAsync_WritesIndentedDurationFirstEntries()
    {
        List<string> lines = [];
        var consumer = new SlowestTestsConsumer(lines.Add);

        await consumer.ConsumeAsync(null!, CreatePassedTestNodeUpdate("FasterTest", TimeSpan.FromSeconds(1.25)), CancellationToken.None);
        await consumer.ConsumeAsync(null!, CreatePassedTestNodeUpdate("SlowerTest", TimeSpan.FromSeconds(12)), CancellationToken.None);
        await consumer.OnTestSessionFinishingAsync(null!);

        Assert.HasCount(3, lines);
        Assert.AreEqual("Slowest 10 tests:", lines[0]);
        Assert.AreEqual($"  {12d:F5}s SlowerTest", lines[1]);
        Assert.AreEqual($"  {1.25d:F5}s FasterTest", lines[2]);
    }

    [TestMethod]
    public async Task OnTestSessionFinishingAsync_WritesOnlyTenSlowestEntries()
    {
        List<string> lines = [];
        var consumer = new SlowestTestsConsumer(lines.Add);

        for (int i = 0; i < 11; i++)
        {
            await consumer.ConsumeAsync(null!, CreatePassedTestNodeUpdate($"Test{i}", TimeSpan.FromSeconds(i + 1)), CancellationToken.None);
        }

        await consumer.OnTestSessionFinishingAsync(null!);

        Assert.HasCount(11, lines);
        Assert.AreEqual($"  {11d:F5}s Test10", lines[1]);
        Assert.AreEqual($"  {2d:F5}s Test1", lines[10]);
        Assert.DoesNotContain(static line => line.EndsWith(" Test0", StringComparison.Ordinal), lines);
    }

    private static TestNodeUpdateMessage CreatePassedTestNodeUpdate(string displayName, TimeSpan duration)
    {
        DateTimeOffset startTime = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        return new(
            new SessionUid("session"),
            new TestNode
            {
                Uid = displayName,
                DisplayName = displayName,
                Properties = new PropertyBag(
                    PassedTestNodeStateProperty.CachedInstance,
                    new TimingProperty(new(startTime, startTime + duration, duration))),
            });
    }
}
