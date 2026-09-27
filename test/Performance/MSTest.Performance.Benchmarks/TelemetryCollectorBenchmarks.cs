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
    private const string TelemetryOptOutEnvironmentVariable = "TESTINGPLATFORM_TELEMETRY_OPTOUT";

    private readonly Barrier _startBarrier = new(ConcurrentThreadCount + 1);
    private readonly Barrier _completedBarrier = new(ConcurrentThreadCount + 1);
    private Thread[] _workerThreads = null!;
    private bool _isDisposed;
    private bool _restoreTelemetryOptOut;
    private string? _telemetryOptOut;
    private bool _workersStarted;
    private volatile bool _stopWorkers;

    [GlobalSetup(Target = nameof(TrackAssertionCall_SingleThreaded))]
    public static void SetupSingleThreaded() => ResetAndPrimeCounter();

    [GlobalSetup(Target = nameof(TrackAssertionCall_Concurrent))]
    public void SetupConcurrent()
    {
        ResetAndPrimeCounter();

        _workerThreads = new Thread[ConcurrentThreadCount];
        for (int i = 0; i < _workerThreads.Length; i++)
        {
            _workerThreads[i] = new Thread(TrackAssertionCalls)
            {
                IsBackground = true,
                Name = "TelemetryCollectorBenchmarkWorker",
            };
            _workerThreads[i].Start();
        }

        _workersStarted = true;
    }

    [GlobalSetup(Target = nameof(TrackAssertionCall_Disabled))]
    public void SetupDisabled()
    {
        TelemetryCollector.DrainAssertionCallCounts();
        _telemetryOptOut = Environment.GetEnvironmentVariable(TelemetryOptOutEnvironmentVariable);
        _restoreTelemetryOptOut = true;
        Environment.SetEnvironmentVariable(TelemetryOptOutEnvironmentVariable, "1");
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
        if (_restoreTelemetryOptOut)
        {
            Environment.SetEnvironmentVariable(TelemetryOptOutEnvironmentVariable, _telemetryOptOut);
        }

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
        => TelemetryCollector.TrackAssertionCall(AssertionName);

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

    private static void ResetAndPrimeCounter()
    {
        TelemetryCollector.DrainAssertionCallCounts();
        TelemetryCollector.TrackAssertionCall(AssertionName, isEnabled: true);
    }
}
