// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions.Messages;

namespace Microsoft.Testing.Extensions.HtmlReport;

// Projects a TestNodeUpdateMessage into a capped CapturedTestResult so that the
// generator does not retain entire test node payloads (and their potentially huge
// stdout/stderr/stack traces) in memory for the whole session.
internal static class TestResultCapture
{
    internal const int MaxStandardStreamLength = TestResultCaptureHelper.MaxStandardStreamLength;
    internal const int MaxStackTraceLength = TestResultCaptureHelper.MaxStackTraceLength;
    internal const int MaxMessageLength = TestResultCaptureHelper.MaxMessageLength;
    internal const int MaxIdentityFieldLength = TestResultCaptureHelper.MaxIdentityFieldLength;
    internal const int MaxTraitFieldLength = TestResultCaptureHelper.MaxTraitFieldLength;

    public static CapturedTestResult? TryCapture(TestNode node)
    {
        TestNodeStateProperty? state = node.Properties.SingleOrDefault<TestNodeStateProperty>();
        if (state is null or DiscoveredTestNodeStateProperty or InProgressTestNodeStateProperty)
        {
            return null;
        }

        CapturedTestResultProperties properties = TestResultCaptureHelper.ExtractProperties(node.Properties);
        string outcome = ClassifyOutcome(state);
        TimeSpan duration = properties.Timing?.GlobalTiming.Duration ?? TimeSpan.Zero;
        (string? className, string? methodName) = TestResultCaptureHelper.GetClassAndMethodName(properties.Identifier);
        CapturedExceptionDetails exceptionDetails = TestResultCaptureHelper.ExtractExceptionDetails(state);

        return new CapturedTestResult
        {
            // Identity fields are test-controlled and can be unbounded (e.g. very long
            // UIDs/display names from generated data), so we also cap them to keep the
            // session-wide result list and generated HTML within a predictable budget.
            Uid = Truncate(node.Uid.Value, MaxIdentityFieldLength)!,
            DisplayName = Truncate(node.DisplayName, MaxIdentityFieldLength)!,
            Outcome = outcome,
            Duration = duration,
            StartTime = properties.Timing?.GlobalTiming.StartTime,
            EndTime = properties.Timing?.GlobalTiming.EndTime,
            ClassName = Truncate(className, MaxIdentityFieldLength),
            MethodName = Truncate(methodName, MaxIdentityFieldLength),
            ErrorMessage = Truncate(exceptionDetails.ErrorMessage, MaxMessageLength),
            ExceptionType = exceptionDetails.ExceptionType,
            StackTrace = Truncate(exceptionDetails.StackTrace, MaxStackTraceLength),
            StandardOutput = Truncate(properties.StandardOutput?.StandardOutput, MaxStandardStreamLength),
            StandardError = Truncate(properties.StandardError?.StandardError, MaxStandardStreamLength),
            Traits = properties.Traits,
        };
    }

    private static string ClassifyOutcome(TestNodeStateProperty state)
        // Cancellation is intentionally handled in report-specific wrappers. HTML has
        // historically let cancellation fall through to the failed outcome category, so
        // this wrapper simply delegates to the shared helper with no special-casing.
        => TestResultCaptureHelper.ClassifyOutcome(state);

    internal static string? Truncate(string? value, int maxLength)
        => TestResultCaptureHelper.Truncate(value, maxLength);
}
