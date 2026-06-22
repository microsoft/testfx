// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Messages;

namespace Microsoft.Testing.Extensions.CtrfReport;

// Projects a TestNodeUpdateMessage into a capped CapturedTestResult so that the
// generator does not retain entire test node payloads (and their potentially huge
// stdout/stderr/stack traces) in memory for the whole session.
internal static class TestResultCapture
{
    public static CapturedTestResult? TryCapture(TestNode node)
    {
        TryCaptureResult? coreResult = TestResultCaptureHelper.TryCaptureCore(node, includeLocation: true);
        if (coreResult is null)
        {
            return null;
        }

        TryCaptureResult core = coreResult.Value;
        (string status, string? rawStatus) = ClassifyStatus(core.State);

        // CTRF keeps Namespace and TypeName separate rather than combining them into
        // a single ClassName string, so we decompose the identifier here instead of
        // using the pre-combined ClassName from TryCaptureResult.
        TestMethodIdentifierProperty? identifier = core.Properties.Identifier;

        return new CapturedTestResult
        {
            // Identity fields are test-controlled and can be unbounded (e.g. very long
            // UIDs/display names from generated data), so we also cap them to keep the
            // session-wide result list and generated report within a predictable budget.
            Uid = core.Uid,
            DisplayName = core.DisplayName,
            Status = status,
            RawStatus = rawStatus,
            Duration = core.Duration,
            StartTime = core.StartTime,
            EndTime = core.EndTime,
            Namespace = TestResultCaptureHelper.Truncate(
                identifier is not null && !RoslynString.IsNullOrEmpty(identifier.Namespace) ? identifier.Namespace : null,
                TestResultCaptureHelper.MaxIdentityFieldLength),
            ClassName = TestResultCaptureHelper.Truncate(identifier?.TypeName, TestResultCaptureHelper.MaxIdentityFieldLength),
            MethodName = core.MethodName,
            ErrorMessage = core.ErrorMessage,
            ExceptionType = core.ExceptionType,
            StackTrace = core.StackTrace,
            StandardOutput = core.StandardOutput,
            StandardError = core.StandardError,
            FilePath = TestResultCaptureHelper.Truncate(core.Properties.Location?.FilePath, TestResultCaptureHelper.MaxIdentityFieldLength),
            Line = core.Properties.Location?.LineSpan.Start.Line,
            Traits = core.Traits,
        };
    }

    // CTRF status enum: passed, failed, skipped, pending, other.
    // Returns the CTRF status plus an optional `rawStatus` preserving the original
    // MTP outcome when it doesn't map 1:1 (timedOut, errored, cancelled).
    private static (string Status, string? RawStatus) ClassifyStatus(TestNodeStateProperty state)
        => state switch
        {
            PassedTestNodeStateProperty => ("passed", null),
            SkippedTestNodeStateProperty => ("skipped", null),
            TimeoutTestNodeStateProperty => ("failed", "timedOut"),
            ErrorTestNodeStateProperty => ("failed", "errored"),
            FailedTestNodeStateProperty => ("failed", null),
#pragma warning disable CS0618, MTP0001 // CancelledTestNodeStateProperty is obsolete
            CancelledTestNodeStateProperty => ("other", "cancelled"),
#pragma warning restore CS0618, MTP0001
            _ when Array.IndexOf(TestNodePropertiesCategories.WellKnownTestNodeTestRunOutcomeFailedProperties, state.GetType()) >= 0
                => ("failed", null),
            _ => throw ApplicationStateGuard.Unreachable(),
        };
}
