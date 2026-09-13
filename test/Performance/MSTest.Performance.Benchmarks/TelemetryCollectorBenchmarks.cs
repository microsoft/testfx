// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MSTest.Performance.Benchmarks;

/// <summary>
/// Measures assertion telemetry collection, which runs once per Assert, CollectionAssert, and StringAssert call.
/// </summary>
[MemoryDiagnoser]
public class TelemetryCollectorBenchmarks : IDisposable
{
    private const string AssertionName = "Assert.AreEqual";
    private const int ConcurrentThreadCount = 4;
    private const int CallsPerThread = 1_000;
    private const int ConcurrentOperationsPerInvoke = ConcurrentThreadCount * CallsPerThread;

    private readonly Barrier _startBarrier = new(ConcurrentThreadCount + 1);
    private readonly Barrier _completedBarrier = new(ConcurrentThreadCount + 1);
    private Thread[] _workerThreads = null!;
    private bool _isDisposed;
    private bool _workersStarted;
    private volatile bool _stopWorkers;

    [GlobalSetup]
    public void Setup()
    {
        TelemetryCollector.DrainAssertionCallCounts();
        TelemetryCollector.TrackAssertionCall(AssertionName, isEnabled: true);

        _workerThreads = new Thread[ConcurrentThreadCount];
        for (int i = 0; i < _workerThreads.Length; i++)
        {
            _workerThreads[i] = new Thread(TrackAssertionCalls)
            {
                IsBackground = true,
            };
            _workerThreads[i].Start();
        }

        _workersStarted = true;
    }

    [GlobalCleanup]
    public void Cleanup() => Dispose();

    public void Dispose()
    {
        if (_isDisposed)
        {
            return;
        }

        _isDisposed = true;
        if (_workersStarted)
        {
            _stopWorkers = true;
            _startBarrier.SignalAndWait();

            foreach (Thread workerThread in _workerThreads)
            {
                workerThread.Join();
            }
        }

        _startBarrier.Dispose();
        _completedBarrier.Dispose();
        TelemetryCollector.DrainAssertionCallCounts();
        GC.SuppressFinalize(this);
    }

    [Benchmark(Baseline = true)]
    public void TrackAssertionCall_SingleThreaded()
        => TelemetryCollector.TrackAssertionCall(AssertionName, isEnabled: true);

    [Benchmark(OperationsPerInvoke = ConcurrentOperationsPerInvoke)]
    public void TrackAssertionCall_Concurrent()
    {
        _startBarrier.SignalAndWait();
        _completedBarrier.SignalAndWait();
    }

    [Benchmark]
    public void TrackAssertionCall_Disabled()
        => TelemetryCollector.TrackAssertionCall(AssertionName, isEnabled: false);

    private void TrackAssertionCalls()
    {
        while (true)
        {
            _startBarrier.SignalAndWait();
            if (_stopWorkers)
            {
                return;
            }

            for (int i = 0; i < CallsPerThread; i++)
            {
                TelemetryCollector.TrackAssertionCall(AssertionName, isEnabled: true);
            }

            _completedBarrier.SignalAndWait();
        }
    }
}
