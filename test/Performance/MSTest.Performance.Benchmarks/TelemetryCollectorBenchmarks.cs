// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MSTest.Performance.Benchmarks;

/// <summary>
/// Measures <c>TelemetryCollector.TrackAssertionCall</c>, which runs once per <c>Assert</c>/
/// <c>CollectionAssert</c>/<c>StringAssert</c> call across every test in a suite. This is one of the
/// hottest paths in MSTest.TestFramework, so contention and allocation behavior under concurrent
/// test execution (parallel test methods sharing the process-wide counter dictionary) matters.
/// </summary>
[MemoryDiagnoser]
public class TelemetryCollectorBenchmarks
{
    private const string AssertionName = "Assert.AreEqual";

    /// <summary>
    /// Single-threaded steady-state cost: the counter for <see cref="AssertionName"/> already exists
    /// in the dictionary, so this measures the <c>GetOrAdd</c> lookup + <c>Interlocked.Increment</c> only.
    /// </summary>
    [Benchmark(Baseline = true)]
    public void TrackAssertionCall_SingleThreaded()
        => TelemetryCollector.TrackAssertionCall(AssertionName, isEnabled: true);

    /// <summary>
    /// Contended cost: simulates multiple test methods running in parallel (a common MSTest/MTP
    /// configuration) all recording assertions concurrently, exercising the
    /// <see cref="System.Collections.Concurrent.ConcurrentDictionary{TKey,TValue}"/> under contention.
    /// </summary>
    [Benchmark]
    [Arguments(4)]
    public void TrackAssertionCall_Concurrent(int threadCount)
    {
        var barrier = new Barrier(threadCount);
        var threads = new Thread[threadCount];
        for (int i = 0; i < threadCount; i++)
        {
            threads[i] = new Thread(() =>
            {
                barrier.SignalAndWait();
                for (int j = 0; j < 1000; j++)
                {
                    TelemetryCollector.TrackAssertionCall(AssertionName, isEnabled: true);
                }
            });
            threads[i].Start();
        }

        foreach (Thread thread in threads)
        {
            thread.Join();
        }
    }

    /// <summary>
    /// Opt-out fast path: measures the cost when telemetry is disabled, which should be a single
    /// branch with no dictionary access.
    /// </summary>
    [Benchmark]
    public void TrackAssertionCall_Disabled()
        => TelemetryCollector.TrackAssertionCall(AssertionName, isEnabled: false);
}
