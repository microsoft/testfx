// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions.Messages;

namespace Microsoft.Testing.Extensions.HtmlReport;

// Projects a TestNodeUpdateMessage into a capped CapturedTestResult so that the
// generator does not retain entire test node payloads (and their potentially huge
// stdout/stderr/stack traces) in memory for the whole session.
internal static class TestResultCapture
{
    public static CapturedTestResult? TryCapture(TestNode node)
    {
        CapturedTestResultCoreData? coreData = TestResultCaptureHelper.TryCaptureCore(node);
        if (!coreData.HasValue)
        {
            return null;
        }

        CapturedTestResultCoreData core = coreData.GetValueOrDefault();
        return new CapturedTestResult
        {
            // Identity fields are test-controlled and can be unbounded (e.g. very long
            // UIDs/display names from generated data), so we also cap them to keep the
            // session-wide result list and generated HTML within a predictable budget.
            Uid = TestResultCaptureHelper.Truncate(node.Uid.Value, TestResultCaptureHelper.MaxIdentityFieldLength)!,
            DisplayName = TestResultCaptureHelper.Truncate(node.DisplayName, TestResultCaptureHelper.MaxIdentityFieldLength)!,
            // HTML does not special-case cancellation — it falls through to the shared
            // helper which maps it to "failed", unlike JUnit which maps it to "cancelled".
            Outcome = TestResultCaptureHelper.ClassifyOutcome(core.State),
            Duration = core.Duration,
            StartTime = core.Properties.Timing?.GlobalTiming.StartTime,
            EndTime = core.Properties.Timing?.GlobalTiming.EndTime,
            ClassName = TestResultCaptureHelper.Truncate(core.ClassName, TestResultCaptureHelper.MaxIdentityFieldLength),
            MethodName = TestResultCaptureHelper.Truncate(core.MethodName, TestResultCaptureHelper.MaxIdentityFieldLength),
            ErrorMessage = TestResultCaptureHelper.Truncate(core.ExceptionDetails.ErrorMessage, TestResultCaptureHelper.MaxMessageLength),
            ExceptionType = core.ExceptionDetails.ExceptionType,
            StackTrace = TestResultCaptureHelper.Truncate(core.ExceptionDetails.StackTrace, TestResultCaptureHelper.MaxStackTraceLength),
            StandardOutput = TestResultCaptureHelper.Truncate(core.Properties.StandardOutput?.StandardOutput, TestResultCaptureHelper.MaxStandardStreamLength),
            StandardError = TestResultCaptureHelper.Truncate(core.Properties.StandardError?.StandardError, TestResultCaptureHelper.MaxStandardStreamLength),
            Traits = core.Properties.Traits,
        };
    }
}
