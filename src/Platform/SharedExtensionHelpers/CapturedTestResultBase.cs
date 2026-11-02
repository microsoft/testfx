// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions.Messages;

namespace Microsoft.Testing.Extensions;

// Minimal projection of a terminal test result, shared by the HtmlReport, JUnitReport and
// CtrfReport consumers (which capture from either a TestNode or a TestNodeUpdateMessage).
// The consumer projects each result into this DTO immediately so that we don't retain
// entire test nodes (and their potentially huge stdout/stderr/stack trace strings) in
// memory for the whole session. The variable-length text fields declared here are
// truncated by the capturing code before construction; derived DTOs may add further
// fields that follow their own truncation rules, so do not assume every field is capped.
internal abstract class CapturedTestResultBase
{
    public string Uid { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public TimeSpan Duration { get; set; }

    public DateTimeOffset? StartTime { get; set; }

    public DateTimeOffset? EndTime { get; set; }

    public string? ClassName { get; set; }

    public string? MethodName { get; set; }

    public string? ErrorMessage { get; set; }

    public string? ExceptionType { get; set; }

    public string? StackTrace { get; set; }

    public string? StandardOutput { get; set; }

    public string? StandardError { get; set; }

    public IReadOnlyList<KeyValuePair<string, string>>? Traits { get; set; }

    // Populates the fields declared on this base type from the captured core data. This is
    // the single truncation-and-assignment path shared by every report format so that a
    // change to a truncation-length constant (or a newly added base field) is applied
    // consistently across HtmlReport, JUnitReport and CtrfReport. Format-specific fields
    // (e.g. Outcome/Status) and any per-format overrides of a base field are set by the
    // caller after this method returns.
    internal void PopulateBaseFields(TestNode node, in CapturedTestResultCoreData core)
    {
        // Identity fields are test-controlled and can be unbounded (e.g. very long
        // UIDs/display names from generated data), so we cap them to keep the
        // session-wide result list and generated report within a predictable budget.
        Uid = TestResultCaptureHelper.Truncate(node.Uid.Value, TestResultCaptureHelper.MaxIdentityFieldLength)!;
        DisplayName = TestResultCaptureHelper.Truncate(node.DisplayName, TestResultCaptureHelper.MaxIdentityFieldLength)!;
        Duration = core.Duration;
        StartTime = core.Properties.Timing?.GlobalTiming.StartTime;
        EndTime = core.Properties.Timing?.GlobalTiming.EndTime;
        ClassName = TestResultCaptureHelper.Truncate(core.ClassName, TestResultCaptureHelper.MaxIdentityFieldLength);
        MethodName = TestResultCaptureHelper.Truncate(core.MethodName, TestResultCaptureHelper.MaxIdentityFieldLength);
        ErrorMessage = TestResultCaptureHelper.Truncate(core.ExceptionDetails.ErrorMessage, TestResultCaptureHelper.MaxMessageLength);
        ExceptionType = core.ExceptionDetails.ExceptionType;
        StackTrace = TestResultCaptureHelper.Truncate(core.ExceptionDetails.StackTrace, TestResultCaptureHelper.MaxStackTraceLength);
        StandardOutput = TestResultCaptureHelper.Truncate(core.Properties.StandardOutput?.StandardOutput, TestResultCaptureHelper.MaxStandardStreamLength);
        StandardError = TestResultCaptureHelper.Truncate(core.Properties.StandardError?.StandardError, TestResultCaptureHelper.MaxStandardStreamLength);
        Traits = core.Properties.Traits;
    }
}
