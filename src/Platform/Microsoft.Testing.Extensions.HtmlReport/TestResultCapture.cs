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
        TryCaptureResult? coreResult = TestResultCaptureHelper.TryCaptureCore(node);
        if (coreResult is null)
        {
            return null;
        }

        TryCaptureResult core = coreResult.Value;
        return new CapturedTestResult
        {
            // Identity fields are test-controlled and can be unbounded (e.g. very long
            // UIDs/display names from generated data), so we also cap them to keep the
            // session-wide result list and generated HTML within a predictable budget.
            Uid = core.Uid,
            DisplayName = core.DisplayName,
            // Cancellation is intentionally handled in report-specific wrappers. HTML has
            // historically let cancellation fall through to the failed outcome category, so
            // this simply delegates to the shared helper with no special-casing.
            Outcome = TestResultCaptureHelper.ClassifyOutcome(core.State),
            Duration = core.Duration,
            StartTime = core.StartTime,
            EndTime = core.EndTime,
            ClassName = core.ClassName,
            MethodName = core.MethodName,
            ErrorMessage = core.ErrorMessage,
            ExceptionType = core.ExceptionType,
            StackTrace = core.StackTrace,
            StandardOutput = core.StandardOutput,
            StandardError = core.StandardError,
            Traits = core.Traits,
        };
    }
}
