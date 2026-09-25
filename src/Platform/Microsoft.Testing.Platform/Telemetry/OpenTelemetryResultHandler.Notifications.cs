// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions.Messages;

namespace Microsoft.Testing.Platform.Telemetry;

internal sealed partial class OpenTelemetryResultHandler
{
    internal void NotifyDiscovered()
        => _totalDiscoveredTests?.Add(1);

    internal void NotifyPassed(TestNode testNode, TestNodeStateProperty stateProperty)
    {
        _totalPassedTests?.Add(1);
        HandleTestResult(testNode, stateProperty);
    }

    internal void NotifyFailed(TestNode testNode, TestNodeStateProperty stateProperty)
    {
        _totalFailedTests?.Add(1);
        HandleTestResult(testNode, stateProperty);
    }

    internal void NotifySkipped(TestNode testNode, TestNodeStateProperty stateProperty)
    {
        _totalSkippedTests?.Add(1);
        HandleTestResult(testNode, stateProperty);
    }

    internal void NotifyInProgress(TestNode testNode, TestNodeUid? parentUid)
        => NotifyInProgress(testNode, parentUid, executionActivityContext: null);

    internal void NotifyInProgress(TestNode testNode, TestNodeUid? parentUid, PlatformActivityContext? executionActivityContext)
    {
        _totalStartedTests?.Add(1);
        string activityName = GetActivityName(testNode);
        string? parentId = _otelService.TestFrameworkActivity?.Id;
        IEnumerable<KeyValuePair<string, object?>> tags = GetTestInitialInfo(testNode, parentUid);
        IPlatformActivity? activity =
            executionActivityContext is not null
            && _otelService is IPlatformOpenTelemetryServiceWithActivityLinks linkService
                ? linkService.StartActivityWithLink(activityName, tags, parentId, executionActivityContext)
                : _otelService.StartActivity(activityName, tags, parentId);

        lock (_syncRoot)
        {
            if (_disposed)
            {
                // A result arrived after shutdown started; close its span rather than tracking it.
                activity?.Dispose();
                return;
            }

            _activeTestCases.Add(1);
            TestActivityKey key = GetActivityKey(testNode);
            if (!_testActivities.TryGetValue(key, out Queue<IPlatformActivity?>? activities))
            {
                activities = new Queue<IPlatformActivity?>();
                _testActivities.Add(key, activities);
            }

            activities.Enqueue(activity);
        }
    }

    internal void NotifyExecutionCompleted(TestNode testNode)
    {
        _totalCompletedTests?.Add(1);
        if (!TryDequeueInFlight(testNode, out IPlatformActivity? activity))
        {
            return;
        }

        activity?.Dispose();
    }

    internal void NotifyUnknown()
        => _totalUnknownTests?.Add(1);

    /// <summary>
    /// Records the run-level metrics. Called once, when the run verdict is known.
    /// </summary>
    /// <param name="totalRanTests">Number of tests that ran.</param>
    /// <param name="failedTests">Number of tests that failed.</param>
    /// <param name="skippedTests">Number of tests that were skipped.</param>
    /// <param name="exitCode">The process exit code the run resolved to.</param>
    /// <param name="runActivity">The root span of the run, if any. The counts go here rather than on the histogram
    /// because they are unbounded values: as metric dimensions they would create a new time series per distinct
    /// count, while on a span they are free.</param>
    internal void NotifyRunCompleted(int totalRanTests, int failedTests, int skippedTests, int exitCode, IPlatformActivity? runActivity = null)
    {
        // Dispose can legitimately run more than once; recording a second data point would double count the run.
        lock (_syncRoot)
        {
            if (_runCompletedReported)
            {
                return;
            }

            _runCompletedReported = true;
        }

        _runStopwatch.Stop();
        _testRunDuration.Record(
            _runStopwatch.Elapsed.TotalSeconds,
            [
                new(TestingPlatformSemanticConventions.Attributes.TestRunResultStatus, GetRunResultStatus(failedTests, exitCode)),
                new(TestingPlatformSemanticConventions.Attributes.TestRunExitCode, exitCode),
            ]);

        runActivity?.SetTag(TestingPlatformSemanticConventions.Attributes.TestRunTotalCount, totalRanTests);
        runActivity?.SetTag(TestingPlatformSemanticConventions.Attributes.TestRunFailedCount, failedTests);
        runActivity?.SetTag(TestingPlatformSemanticConventions.Attributes.TestRunSkippedCount, skippedTests);
    }

    /// <summary>
    /// Removes the oldest in-flight entry for the node and keeps <c>test.case.active</c> balanced.
    /// </summary>
    private bool TryDequeueInFlight(TestNode testNode, out IPlatformActivity? activity)
    {
        lock (_syncRoot)
        {
            activity = null;
            TestActivityKey key = GetActivityKey(testNode);
            if (!TryDequeueInFlight(key, out activity)
                && (key.AttemptNumber is null
                    || !TryDequeueInFlight(new TestActivityKey(testNode.Uid, AttemptNumber: null), out activity)))
            {
                return false;
            }

            _activeTestCases.Add(-1);
            return true;
        }
    }

    private bool TryDequeueInFlight(TestActivityKey key, out IPlatformActivity? activity)
    {
        activity = null;
        if (!_testActivities.TryGetValue(key, out Queue<IPlatformActivity?>? activities) || activities.Count == 0)
        {
            return false;
        }

        activity = activities.Dequeue();
        if (activities.Count == 0)
        {
            _testActivities.Remove(key);
        }

        return true;
    }

    private static TestActivityKey GetActivityKey(TestNode testNode)
        => new(testNode.Uid, testNode.Properties.SingleOrDefault<RetryAttemptProperty>()?.AttemptNumber);
}
