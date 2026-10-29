// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using AwesomeAssertions;

using Microsoft.VisualStudio.TestTools.UnitTesting;

using TestFramework.ForTestingMSTest;

namespace Microsoft.VisualStudio.TestPlatform.TestFramework.UnitTests;

public sealed class TelemetryCollectorTests : TestContainer
{
    public void TrackAssertionCall_AccumulatesCounts()
    {
        TelemetryCollector.DrainAssertionCallCounts();

        TelemetryCollector.TrackAssertionCall("TelemetryCollectorTests.First", isEnabled: true);
        TelemetryCollector.TrackAssertionCall("TelemetryCollectorTests.First", isEnabled: true);
        TelemetryCollector.TrackAssertionCall("TelemetryCollectorTests.Second", isEnabled: true);

        Dictionary<string, long> counts = TelemetryCollector.DrainAssertionCallCounts();
        counts["TelemetryCollectorTests.First"].Should().Be(2);
        counts["TelemetryCollectorTests.Second"].Should().Be(1);
    }

    public void TrackAssertionCall_ConcurrentCallsDoNotLoseUpdates()
    {
        const int CallCount = 100_000;
        TelemetryCollector.DrainAssertionCallCounts();

        Parallel.For(
            0,
            CallCount,
            static _ => TelemetryCollector.TrackAssertionCall("TelemetryCollectorTests.Concurrent", isEnabled: true));

        Dictionary<string, long> counts = TelemetryCollector.DrainAssertionCallCounts();
        counts["TelemetryCollectorTests.Concurrent"].Should().Be(CallCount);
    }

    public void DrainAssertionCallCounts_ResetsCounts()
    {
        TelemetryCollector.DrainAssertionCallCounts();
        TelemetryCollector.TrackAssertionCall("TelemetryCollectorTests.Reset", isEnabled: true);

        Dictionary<string, long> first = TelemetryCollector.DrainAssertionCallCounts();
        Dictionary<string, long> second = TelemetryCollector.DrainAssertionCallCounts();

        first["TelemetryCollectorTests.Reset"].Should().Be(1);
        second.Should().NotContainKey("TelemetryCollectorTests.Reset");
    }

    public void ConcurrentDrains_PartitionCompletedUpdatesWithoutDuplication()
    {
        const int CallCount = 100_000;
        TelemetryCollector.DrainAssertionCallCounts();
        for (int i = 0; i < CallCount; i++)
        {
            TelemetryCollector.TrackAssertionCall("TelemetryCollectorTests.ConcurrentDrain", isEnabled: true);
        }

        using var start = new ManualResetEventSlim();
        Task<Dictionary<string, long>> firstDrain = Task.Run(() =>
        {
            start.Wait();
            return TelemetryCollector.DrainAssertionCallCounts();
        });
        Task<Dictionary<string, long>> secondDrain = Task.Run(() =>
        {
            start.Wait();
            return TelemetryCollector.DrainAssertionCallCounts();
        });

        start.Set();
        Task.WaitAll(firstDrain, secondDrain);

        firstDrain.Result.TryGetValue("TelemetryCollectorTests.ConcurrentDrain", out long firstCount);
        secondDrain.Result.TryGetValue("TelemetryCollectorTests.ConcurrentDrain", out long secondCount);
        (firstCount + secondCount).Should().Be(CallCount);
    }

    public void TrackingWhileDraining_DoesNotDuplicateUpdates()
    {
        const int WorkerCount = 4;
        const int CallsPerWorker = 25_000;
        const int ExpectedCallCount = WorkerCount * CallsPerWorker;
        TelemetryCollector.DrainAssertionCallCounts();

        long drainedCount = 0;
        var workers = new Task[WorkerCount];
        for (int worker = 0; worker < workers.Length; worker++)
        {
            workers[worker] = Task.Run(() =>
            {
                for (int i = 0; i < CallsPerWorker; i++)
                {
                    TelemetryCollector.TrackAssertionCall("TelemetryCollectorTests.RacingDrain", isEnabled: true);
                }
            });
        }

        while (!Task.WaitAll(workers, millisecondsTimeout: 0))
        {
            Dictionary<string, long> counts = TelemetryCollector.DrainAssertionCallCounts();
            if (counts.TryGetValue("TelemetryCollectorTests.RacingDrain", out long count))
            {
                drainedCount += count;
            }
        }

        Dictionary<string, long> finalCounts = TelemetryCollector.DrainAssertionCallCounts();
        if (finalCounts.TryGetValue("TelemetryCollectorTests.RacingDrain", out long finalCount))
        {
            drainedCount += finalCount;
        }

        drainedCount.Should().BeLessThanOrEqualTo(ExpectedCallCount);
    }

    public void TrackAssertionCall_WhenDisabled_DoesNotRecordCount()
    {
        TelemetryCollector.DrainAssertionCallCounts();

        TelemetryCollector.TrackAssertionCall("TelemetryCollectorTests.Disabled", isEnabled: false);

        TelemetryCollector.DrainAssertionCallCounts().Should().NotContainKey("TelemetryCollectorTests.Disabled");
    }

#if NET8_0_OR_GREATER
    public void TrackAssertionCall_DoesNotAllocateAfterCounterExists()
    {
        const int WarmupCallCount = 1_000;
        const int CallsPerMeasurement = 10_000;
        const int MeasurementCount = 3;
        TelemetryCollector.DrainAssertionCallCounts();
        for (int i = 0; i < WarmupCallCount; i++)
        {
            TelemetryCollector.TrackAssertionCall("TelemetryCollectorTests.Allocations", isEnabled: true);
        }

        // One-time runtime or profiler initialization must not count as a steady-state allocation.
        long[] allocatedBytes = new long[MeasurementCount];
        for (int measurement = 0; measurement < MeasurementCount; measurement++)
        {
            long before = GC.GetAllocatedBytesForCurrentThread();
            for (int i = 0; i < CallsPerMeasurement; i++)
            {
                TelemetryCollector.TrackAssertionCall("TelemetryCollectorTests.Allocations", isEnabled: true);
            }

            allocatedBytes[measurement] = GC.GetAllocatedBytesForCurrentThread() - before;
        }

        allocatedBytes.Should().Contain(0L);
        TelemetryCollector.DrainAssertionCallCounts()["TelemetryCollectorTests.Allocations"].Should().Be(WarmupCallCount + (MeasurementCount * CallsPerMeasurement));
    }
#endif
}
