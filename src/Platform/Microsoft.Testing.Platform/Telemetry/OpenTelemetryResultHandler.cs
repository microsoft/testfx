// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions.Messages;

namespace Microsoft.Testing.Platform.Telemetry;

internal sealed class OpenTelemetryResultHandler : IDisposable
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
    {
        _totalStartedTests?.Add(1);
        _activeTestCases.Add(1);
        IPlatformActivity? activity = _otelService.StartActivity(
            GetActivityName(testNode),
            parentId: _otelService.TestFrameworkActivity?.Id,
            tags: GetTestInitialInfo(testNode, parentUid));

        if (!_testActivities.TryGetValue(testNode.Uid, out Queue<IPlatformActivity?>? activities))
        {
            activities = new Queue<IPlatformActivity?>();
            _testActivities.Add(testNode.Uid, activities);
        }

        activities.Enqueue(activity);
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
    internal void NotifyRunCompleted(int totalRanTests, int failedTests, int skippedTests, int exitCode)
    {
        // Dispose can legitimately run more than once; recording a second data point would double count the run.
        if (_runCompletedReported)
        {
            return;
        }

        _runCompletedReported = true;
        _runStopwatch.Stop();
        _testRunDuration.Record(
            _runStopwatch.Elapsed.TotalSeconds,
            [
                new(TestingPlatformSemanticConventions.Attributes.TestCaseResultStatus, failedTests > 0 ? TestingPlatformSemanticConventions.TestResultStatus.Failed : TestingPlatformSemanticConventions.TestResultStatus.Passed),
                new(TestingPlatformSemanticConventions.Attributes.TestRunExitCode, exitCode),
                new("test.run.total", totalRanTests),
                new("test.run.skipped", skippedTests),
            ]);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        foreach (Queue<IPlatformActivity?> activities in _testActivities.Values)
        {
            foreach (IPlatformActivity? activity in activities)
            {
                activity?.Dispose();
            }
        }

        _testActivities.Clear();
    }

    /// <summary>
    /// Removes the oldest in-flight entry for the node and keeps <c>test.case.active</c> balanced.
    /// </summary>
    private bool TryDequeueInFlight(TestNode testNode, out IPlatformActivity? activity)
    {
        activity = null;
        if (!_testActivities.TryGetValue(testNode.Uid, out Queue<IPlatformActivity?>? activities) || activities.Count == 0)
        {
            return false;
        }

        activity = activities.Dequeue();
        if (activities.Count == 0)
        {
            _testActivities.Remove(testNode.Uid);
        }

        _activeTestCases.Add(-1);
        return true;
    }

    /// <summary>
    /// The OpenTelemetry conventions ask for the span name to be the test case name rather than an opaque id,
    /// because it is what shows up in trace waterfalls and what backends group on.
    /// </summary>
    private static string GetActivityName(TestNode testNode)
        => RoslynString.IsNullOrWhiteSpace(testNode.DisplayName) ? testNode.Uid.Value : testNode.DisplayName;

    private static string? GetSuiteName(TestNode testNode)
        => testNode.Properties.SingleOrDefault<TestMethodIdentifierProperty>()?.TypeName;

    private IEnumerable<KeyValuePair<string, object?>> GetTestInitialInfo(TestNode testNode, TestNodeUid? parentUid)
    {
        yield return new(TestingPlatformSemanticConventions.Attributes.TestCaseName, testNode.DisplayName);
        yield return new(TestingPlatformSemanticConventions.Attributes.TestCaseId, testNode.Uid.Value);
        if (parentUid is not null)
        {
            yield return new(TestingPlatformSemanticConventions.Attributes.TestCaseParentId, parentUid.Value);
        }

        if (_options.EmitLegacyAttributes)
        {
            yield return new(TestingPlatformSemanticConventions.Attributes.LegacyTestName, testNode.DisplayName);
            yield return new(TestingPlatformSemanticConventions.Attributes.LegacyTestId, testNode.Uid.Value);
            if (parentUid is not null)
            {
                yield return new(TestingPlatformSemanticConventions.Attributes.LegacyTestParentId, parentUid.Value);
            }
        }

        if (testNode.Properties.SingleOrDefault<TestMethodIdentifierProperty>() is { } identifierProperty)
        {
            yield return new(TestingPlatformSemanticConventions.Attributes.CodeFunctionName, $"{identifierProperty.TypeName}.{identifierProperty.MethodName}");
            yield return new(TestingPlatformSemanticConventions.Attributes.CodeNamespace, identifierProperty.Namespace);
            yield return new(TestingPlatformSemanticConventions.Attributes.TestSuiteName, identifierProperty.TypeName);
            yield return new(TestingPlatformSemanticConventions.Attributes.TestAssemblyName, identifierProperty.AssemblyFullName);

            if (_options.EmitLegacyAttributes)
            {
                yield return new(TestingPlatformSemanticConventions.Attributes.LegacyTestMethod, identifierProperty.MethodName);
                yield return new(TestingPlatformSemanticConventions.Attributes.LegacyTestClass, identifierProperty.TypeName);
                yield return new(TestingPlatformSemanticConventions.Attributes.LegacyTestNamespace, identifierProperty.Namespace);
                yield return new(TestingPlatformSemanticConventions.Attributes.LegacyTestAssembly, identifierProperty.AssemblyFullName);
            }
        }

        if (testNode.Properties.SingleOrDefault<TestFileLocationProperty>() is { } testLocationProperty)
        {
            yield return new(TestingPlatformSemanticConventions.Attributes.CodeFilePath, testLocationProperty.FilePath);
            yield return new(TestingPlatformSemanticConventions.Attributes.CodeLineNumber, testLocationProperty.LineSpan.Start.Line);

            if (_options.EmitLegacyAttributes)
            {
                yield return new(TestingPlatformSemanticConventions.Attributes.LegacyTestFilePath, testLocationProperty.FilePath);
                yield return new(TestingPlatformSemanticConventions.Attributes.LegacyTestLineStart, testLocationProperty.LineSpan.Start.Line);
                yield return new(TestingPlatformSemanticConventions.Attributes.LegacyTestLineEnd, testLocationProperty.LineSpan.End.Line);
            }
        }

        foreach (TestMetadataProperty metadata in testNode.Properties.OfType<TestMetadataProperty>())
        {
            yield return new KeyValuePair<string, object?>($"{TestingPlatformSemanticConventions.Attributes.TestMetadataPrefix}{metadata.Key}", metadata.Value);
            if (_options.EmitLegacyAttributes)
            {
                yield return new KeyValuePair<string, object?>($"{TestingPlatformSemanticConventions.Attributes.LegacyTestMetadataPrefix}{metadata.Key}", metadata.Value);
            }
        }
    }

    private void HandleTestResult(TestNode testNode, TestNodeStateProperty stateProperty)
    {
        _totalCompletedTests?.Add(1);

        (string result, Exception? exception, TimeSpan? timeoutTime) = stateProperty switch
        {
            PassedTestNodeStateProperty => (TestingPlatformSemanticConventions.TestResultStatus.Passed, null, null),
            FailedTestNodeStateProperty failed => (TestingPlatformSemanticConventions.TestResultStatus.Failed, failed.Exception, null),
            ErrorTestNodeStateProperty error => (TestingPlatformSemanticConventions.TestResultStatus.Error, error.Exception, null),
            TimeoutTestNodeStateProperty timeout => (TestingPlatformSemanticConventions.TestResultStatus.Timeout, timeout.Exception, timeout.Timeout),
#pragma warning disable CS0618, MTP0001 // Type or member is obsolete
            CancelledTestNodeStateProperty cancelled => (TestingPlatformSemanticConventions.TestResultStatus.Cancelled, cancelled.Exception, null),
#pragma warning restore CS0618, MTP0001 // Type or member is obsolete
            SkippedTestNodeStateProperty => (TestingPlatformSemanticConventions.TestResultStatus.Skipped, null, null),
            _ => (TestingPlatformSemanticConventions.TestResultStatus.Unknown, null, null),
        };

        KeyValuePair<string, object?>[] measurementTags =
        [
            new(TestingPlatformSemanticConventions.Attributes.TestCaseResultStatus, result),
            new(TestingPlatformSemanticConventions.Attributes.TestSuiteName, GetSuiteName(testNode)),
        ];

        _testCaseResultCount.Add(1, measurementTags);

        if (!TryDequeueInFlight(testNode, out IPlatformActivity? activity) || activity is null)
        {
            // Either the framework never reported the test as in-progress, or nothing is listening so no span was
            // created. Either way we still want the duration recorded, otherwise a framework that only publishes
            // final results produces no latency data.
            SetResultDetails(testNode, measurementTags, activity: null);
            return;
        }

        activity.SetTag(TestingPlatformSemanticConventions.Attributes.TestCaseResultStatus, result);
        activity.SetTag(TestingPlatformSemanticConventions.Attributes.TestCaseResultExplanation, _options.Truncate(stateProperty.Explanation));

        if (_options.EmitLegacyAttributes)
        {
            activity.SetTag(TestingPlatformSemanticConventions.Attributes.LegacyTestResult, result);
            activity.SetTag(TestingPlatformSemanticConventions.Attributes.LegacyTestResultExplanation, stateProperty.Explanation);
        }

        if (exception is not null)
        {
            // The OpenTelemetry convention is an "exception" event plus error.type/error.message on the span, and a
            // status of Error so the trace shows up as failed in every backend.
            activity.RecordException(exception);
            activity.SetTag(TestingPlatformSemanticConventions.Attributes.ErrorType, exception.GetType().FullName);
            activity.SetTag(TestingPlatformSemanticConventions.Attributes.ErrorMessage, _options.Truncate(exception.Message));
            activity.SetTag(TestingPlatformSemanticConventions.Attributes.CodeStacktrace, _options.Truncate(exception.StackTrace));

            if (_options.EmitLegacyAttributes)
            {
                activity.SetTag(TestingPlatformSemanticConventions.Attributes.LegacyTestResultExceptionType, exception.GetType().FullName);
                activity.SetTag(TestingPlatformSemanticConventions.Attributes.LegacyTestResultExceptionMessage, exception.Message);
                activity.SetTag(TestingPlatformSemanticConventions.Attributes.LegacyTestResultExceptionStackTrace, exception.StackTrace);
            }
        }
        else
        {
            activity.SetStatus(
                result switch
                {
                    TestingPlatformSemanticConventions.TestResultStatus.Passed => PlatformActivityStatusCode.Ok,
                    TestingPlatformSemanticConventions.TestResultStatus.Skipped or TestingPlatformSemanticConventions.TestResultStatus.Unknown => PlatformActivityStatusCode.Unset,
                    _ => PlatformActivityStatusCode.Error,
                },
                stateProperty.Explanation);
        }

        if (timeoutTime is not null)
        {
            activity.SetTag(TestingPlatformSemanticConventions.Attributes.TestCaseTimeoutMilliseconds, timeoutTime.Value.TotalMilliseconds);
            if (_options.EmitLegacyAttributes)
            {
                activity.SetTag(TestingPlatformSemanticConventions.Attributes.LegacyTestResultTimeout, timeoutTime.Value.TotalMilliseconds);
            }
        }

        SetResultDetails(testNode, measurementTags, activity);
        activity.Dispose();
    }

    private void SetResultDetails(TestNode testNode, KeyValuePair<string, object?>[] measurementTags, IPlatformActivity? activity)
    {
        // Single pass over the property bag: replaces five separate walks
        // (SingleOrDefault<TimingProperty>, OfType<TestMetadataProperty>, SingleOrDefault<StandardOutputProperty>,
        //  SingleOrDefault<StandardErrorProperty>, OfType<FileArtifactProperty>).
        // Eliminates two TProperty[] heap allocations from OfType<T>() and reduces linked-list traversal from 5 to 1.
        TimingProperty? timingProperty = null;
        StandardOutputProperty? standardOutputProperty = null;
        StandardErrorProperty? standardErrorProperty = null;
        int artifactIndex = 0;
        PropertyBag.PropertyBagEnumerator enumerator = testNode.Properties.GetStructEnumerator();
        while (enumerator.MoveNext())
        {
            switch (enumerator.Current)
            {
                case TimingProperty tp:
                    if (timingProperty is not null)
                    {
                        throw new InvalidOperationException($"Found multiple properties of type '{typeof(TimingProperty)}'.");
                    }

                    timingProperty = tp;
                    break;
                case TestMetadataProperty metadataProperty:
                    activity?.SetTag($"{TestingPlatformSemanticConventions.Attributes.TestMetadataPrefix}{metadataProperty.Key}", metadataProperty.Value);
                    if (_options.EmitLegacyAttributes)
                    {
                        activity?.SetTag($"{TestingPlatformSemanticConventions.Attributes.LegacyTestMetadataPrefix}{metadataProperty.Key}", metadataProperty.Value);
                    }

                    break;
                case StandardOutputProperty outputProperty:
                    if (standardOutputProperty is not null)
                    {
                        throw new InvalidOperationException($"Found multiple properties of type '{typeof(StandardOutputProperty)}'.");
                    }

                    standardOutputProperty = outputProperty;
                    break;
                case StandardErrorProperty errorProperty:
                    if (standardErrorProperty is not null)
                    {
                        throw new InvalidOperationException($"Found multiple properties of type '{typeof(StandardErrorProperty)}'.");
                    }

                    standardErrorProperty = errorProperty;
                    break;
                case FileArtifactProperty fileArtifactProperty:
                    activity?.SetTag($"test.artifact.file[{artifactIndex}].path", fileArtifactProperty.FileInfo.FullName);
                    artifactIndex++;
                    break;
            }
        }

        if (timingProperty is not null)
        {
            double totalMilliseconds = timingProperty.GlobalTiming.Duration.TotalMilliseconds;
            _testCaseDuration.Record(timingProperty.GlobalTiming.Duration.TotalSeconds, measurementTags);
            _totalDuration?.Record(totalMilliseconds);
            activity?.SetTag(TestingPlatformSemanticConventions.Attributes.TestCaseDurationMilliseconds, totalMilliseconds);
            if (_options.EmitLegacyAttributes)
            {
                activity?.SetTag(TestingPlatformSemanticConventions.Attributes.LegacyTestDuration, totalMilliseconds);
            }

            foreach (StepTimingInfo step in timingProperty.StepTimings)
            {
                activity?.SetTag($"{TestingPlatformSemanticConventions.Attributes.TestStepPrefix}{step.Id}.duration", step.Timing.Duration.TotalMilliseconds);
                activity?.SetTag($"{TestingPlatformSemanticConventions.Attributes.TestStepPrefix}{step.Id}.description", step.Description);
                if (_options.EmitLegacyAttributes)
                {
                    activity?.SetTag($"test.step{step.Id}.duration.ms", step.Timing.Duration.TotalMilliseconds);
                    activity?.SetTag($"test.step{step.Id}.description", step.Description);
                }
            }
        }

        if (activity is null || !_options.CaptureTestOutput)
        {
            return;
        }

        activity.SetTag(TestingPlatformSemanticConventions.Attributes.TestOutputStdout, _options.Truncate(standardOutputProperty?.StandardOutput) ?? string.Empty);
        activity.SetTag(TestingPlatformSemanticConventions.Attributes.TestOutputStderr, _options.Truncate(standardErrorProperty?.StandardError) ?? string.Empty);

        if (_options.EmitLegacyAttributes)
        {
            activity.SetTag(TestingPlatformSemanticConventions.Attributes.LegacyTestStdout, standardOutputProperty?.StandardOutput ?? string.Empty);
            activity.SetTag(TestingPlatformSemanticConventions.Attributes.LegacyTestStderr, standardErrorProperty?.StandardError ?? string.Empty);
        }
    }
}
