// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions.Messages;

namespace Microsoft.Testing.Platform.Telemetry;

internal sealed partial class OpenTelemetryResultHandler : IDisposable
{
    private readonly IPlatformOpenTelemetryService _otelService;
    private readonly PlatformOpenTelemetryOptions _options;

    // Semantic-convention aligned instruments.
    private readonly ICounter<int> _testCaseResultCount;
    private readonly IHistogram<double> _testCaseDuration;
    private readonly IUpDownCounter<int> _activeTestCases;
    private readonly IHistogram<double> _testRunDuration;

    // Legacy instruments, kept so existing dashboards keep working.
    private readonly ICounter<int>? _totalDiscoveredTests;
    private readonly ICounter<int>? _totalStartedTests;
    private readonly ICounter<int>? _totalCompletedTests;
    private readonly ICounter<int>? _totalPassedTests;
    private readonly ICounter<int>? _totalFailedTests;
    private readonly ICounter<int>? _totalSkippedTests;
    private readonly ICounter<int>? _totalUnknownTests;
    private readonly IHistogram<double>? _totalDuration;

    private readonly Stopwatch _runStopwatch = Stopwatch.StartNew();

    // Note: we use a queue per Uid because frameworks are allowed (but discouraged) to produce
    // multiple test nodes that share the same Uid (e.g. NUnit's [Values("one", "one")] or
    // MSTest's "folded" parameterized tests). When that happens we still want to track every
    // in-flight activity and pair them with results in FIFO order, instead of throwing.
    // The queued activity is nullable on purpose: when no tracer is listening StartActivity returns null, and we
    // still need the entry so the in-flight bookkeeping (and therefore test.case.active) stays balanced.
    private readonly Dictionary<TestNodeUid, Queue<IPlatformActivity?>> _testActivities = [];

    // The notifications are normally serialised by the message bus's single-reader consumer loop. They are not on
    // the cancellation path: a cancelled run skips the drain/disable step, so a consumer can still be publishing
    // results while the host disposes us. Guarding the bookkeeping keeps that race from throwing a collection-
    // modified exception out of the telemetry code during shutdown - telemetry must never fail a run.
    // This is a local mitigation; the platform-level sequencing is tracked by
    // https://github.com/microsoft/testfx/issues/10357 and this guard can be revisited once that is fixed.
#if NET9_0_OR_GREATER
    private readonly Lock _syncRoot = new();
#else
    private readonly object _syncRoot = new();
#endif
    private bool _disposed;
    private bool _runCompletedReported;

    public OpenTelemetryResultHandler(IPlatformOpenTelemetryService otelService)
        : this(otelService, PlatformOpenTelemetryOptions.Default)
    {
    }

    public OpenTelemetryResultHandler(IPlatformOpenTelemetryService otelService, PlatformOpenTelemetryOptions options)
    {
        _otelService = otelService;
        _options = options;

        _testCaseResultCount = otelService.CreateCounter<int>(
            TestingPlatformSemanticConventions.Metrics.TestCaseResultCount,
            TestingPlatformSemanticConventions.Units.Count,
            "Number of test cases, dimensioned by result status.");
        _testCaseDuration = otelService.CreateHistogram<double>(
            TestingPlatformSemanticConventions.Metrics.TestCaseDuration,
            TestingPlatformSemanticConventions.Units.Seconds,
            "Duration of a single test case.");
        _activeTestCases = otelService.CreateUpDownCounter<int>(
            TestingPlatformSemanticConventions.Metrics.TestRunActiveCases,
            TestingPlatformSemanticConventions.Units.Count,
            "Number of test cases currently running.");
        _testRunDuration = otelService.CreateHistogram<double>(
            TestingPlatformSemanticConventions.Metrics.TestRunDuration,
            TestingPlatformSemanticConventions.Units.Seconds,
            "Duration of the whole test run.");

        if (options.EmitLegacyAttributes)
        {
            _totalDiscoveredTests = otelService.CreateCounter<int>(TestingPlatformSemanticConventions.Metrics.LegacyTestsDiscovered);
            _totalStartedTests = otelService.CreateCounter<int>(TestingPlatformSemanticConventions.Metrics.LegacyTestsStarted);
            _totalCompletedTests = otelService.CreateCounter<int>(TestingPlatformSemanticConventions.Metrics.LegacyTestsCompleted);
            _totalPassedTests = otelService.CreateCounter<int>(TestingPlatformSemanticConventions.Metrics.LegacyTestsPassed);
            _totalFailedTests = otelService.CreateCounter<int>(TestingPlatformSemanticConventions.Metrics.LegacyTestsFailed);
            _totalSkippedTests = otelService.CreateCounter<int>(TestingPlatformSemanticConventions.Metrics.LegacyTestsSkipped);
            _totalUnknownTests = otelService.CreateCounter<int>(TestingPlatformSemanticConventions.Metrics.LegacyTestsUnknown);
            _totalDuration = otelService.CreateHistogram<double>(TestingPlatformSemanticConventions.Metrics.LegacyTestsDuration);
        }
    }

    public void Dispose()
    {
        List<IPlatformActivity?> orphaned = [];
        lock (_syncRoot)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;

            // Drain into a local list so the spans are closed outside the lock and a concurrent notification
            // cannot mutate the dictionary while we walk it.
            foreach (Queue<IPlatformActivity?> activities in _testActivities.Values)
            {
                orphaned.AddRange(activities);
            }

            if (orphaned.Count > 0)
            {
                _activeTestCases.Add(-orphaned.Count);
            }

            _testActivities.Clear();
        }

        foreach (IPlatformActivity? activity in orphaned)
        {
            activity?.Dispose();
        }
    }
}
