// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions.Messages;

namespace Microsoft.Testing.Platform.Telemetry;

internal sealed partial class OpenTelemetryResultHandler
{
    private const string ExceptionEventName = "exception";
    private const string ExceptionTypeTag = "exception.type";
    private const string ExceptionMessageTag = "exception.message";
    private const string ExceptionStackTraceTag = "exception.stacktrace";

    private void HandleTestResult(TestNode testNode, TestNodeStateProperty stateProperty)
    {
        _totalCompletedTests?.Add(1);

        (string result, Exception? exception, TimeSpan? timeoutTime) = stateProperty switch
        {
            PassedTestNodeStateProperty => (TestingPlatformSemanticConventions.TestResultStatus.Pass, null, null),
            FailedTestNodeStateProperty failed => (TestingPlatformSemanticConventions.TestResultStatus.Fail, failed.Exception, null),
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

        string? truncatedExplanation = _options.Truncate(stateProperty.Explanation);
        activity.SetTag(TestingPlatformSemanticConventions.Attributes.TestCaseResultStatus, result);
        activity.SetTag(TestingPlatformSemanticConventions.Attributes.TestCaseResultExplanation, truncatedExplanation);

        if (_options.EmitLegacyAttributes)
        {
            // The legacy attribute keeps its original "passed"/"failed" spellings; the semantic-convention
            // attribute uses the upstream "pass"/"fail" enum.
            activity.SetTag(TestingPlatformSemanticConventions.Attributes.LegacyTestResult, TestingPlatformSemanticConventions.TestResultStatus.ToLegacy(result));
            // Truncated as well: emitting the legacy twin untruncated would defeat the size limit, since legacy
            // attributes are on by default.
            activity.SetTag(TestingPlatformSemanticConventions.Attributes.LegacyTestResultExplanation, truncatedExplanation);
        }

        if (exception is not null)
        {
            // The OpenTelemetry convention is an "exception" event carrying the type/message/stack trace, plus
            // error.type and a status of Error on the span so it shows up as failed in every backend.
            // error.message is deliberately not set: it is deprecated upstream and NOT RECOMMENDED on spans
            // because of its unbounded cardinality - the message is on the event instead.
            string? exceptionTypeName = exception.GetType().FullName;
            string? truncatedMessage = _options.Truncate(exception.Message);
            string? truncatedStackTrace = _options.Truncate(exception.StackTrace);
            string? truncatedExceptionStackTrace = _options.Truncate(exception.ToString());

            activity.AddEvent(
                ExceptionEventName,
                [
                    new(ExceptionTypeTag, exceptionTypeName),
                    new(ExceptionMessageTag, truncatedMessage),
                    new(ExceptionStackTraceTag, truncatedExceptionStackTrace),
                ]);
            activity.SetStatus(PlatformActivityStatusCode.Error, truncatedMessage);
            activity.SetTag(TestingPlatformSemanticConventions.Attributes.ErrorType, exceptionTypeName);
            activity.SetTag(TestingPlatformSemanticConventions.Attributes.CodeStacktrace, truncatedStackTrace);

            if (_options.EmitLegacyAttributes)
            {
                activity.SetTag(TestingPlatformSemanticConventions.Attributes.LegacyTestResultExceptionType, exceptionTypeName);
                activity.SetTag(TestingPlatformSemanticConventions.Attributes.LegacyTestResultExceptionMessage, truncatedMessage);
                activity.SetTag(TestingPlatformSemanticConventions.Attributes.LegacyTestResultExceptionStackTrace, truncatedStackTrace);
            }
        }
        else
        {
            activity.SetStatus(
                result switch
                {
                    TestingPlatformSemanticConventions.TestResultStatus.Pass => PlatformActivityStatusCode.Ok,
                    TestingPlatformSemanticConventions.TestResultStatus.Skipped or TestingPlatformSemanticConventions.TestResultStatus.Unknown => PlatformActivityStatusCode.Unset,
                    _ => PlatformActivityStatusCode.Error,
                },
                truncatedExplanation);
        }

        if (timeoutTime is not null)
        {
            activity.SetTag(TestingPlatformSemanticConventions.Attributes.TestCaseTimeoutMilliseconds, timeoutTime.Value.TotalMilliseconds);
            if (_options.EmitLegacyAttributes)
            {
                activity.SetTag(TestingPlatformSemanticConventions.Attributes.LegacyTestResultTimeout, timeoutTime.Value.TotalMilliseconds);
            }
        }

        try
        {
            SetResultDetails(testNode, measurementTags, activity);
        }
        finally
        {
            // The span was already dequeued, so it must be closed even if collecting the details throws on a
            // malformed property bag; otherwise it would stay open until the handler is disposed.
            activity.Dispose();
        }
    }

    /// <summary>
    /// Collects the timing, output and artifact details of a completed test in a single pass over the property bag.
    /// </summary>
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

        // Truncated for both the semantic-convention and the legacy names: test output routinely runs to megabytes
        // and can contain secrets.
        string standardOutput = _options.Truncate(standardOutputProperty?.StandardOutput) ?? string.Empty;
        string standardError = _options.Truncate(standardErrorProperty?.StandardError) ?? string.Empty;

        activity.SetTag(TestingPlatformSemanticConventions.Attributes.TestOutputStdout, standardOutput);
        activity.SetTag(TestingPlatformSemanticConventions.Attributes.TestOutputStderr, standardError);

        if (_options.EmitLegacyAttributes)
        {
            activity.SetTag(TestingPlatformSemanticConventions.Attributes.LegacyTestStdout, standardOutput);
            activity.SetTag(TestingPlatformSemanticConventions.Attributes.LegacyTestStderr, standardError);
        }
    }
}
