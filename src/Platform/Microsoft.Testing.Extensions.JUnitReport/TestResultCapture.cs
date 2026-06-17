// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions.Messages;

namespace Microsoft.Testing.Extensions.JUnitReport;

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

    // The display name of a test node is also captured for non-terminal (Discovered /
    // InProgress) messages so that the engine can reconstruct the parent chain for
    // every terminal test. This DTO is intentionally tiny because every node in the
    // tree contributes one entry to the parent-chain dictionary.
    internal readonly record struct ParentChainEntry(string DisplayName, string? ParentRawUid);

    public static ParentChainEntry GetParentChainEntry(TestNodeUpdateMessage update)
        => new(
            // Display names are test-controlled and may be very long, so cap them to
            // bound the size of the parent-chain dictionary and the generated XML.
            Truncate(update.TestNode.DisplayName, MaxIdentityFieldLength)!,
            // The parent UID is also test-controlled. Cap it to the same identity
            // budget used everywhere else; lookups against `_parentChain` (also
            // keyed by a truncated UID) stay consistent.
            Truncate(update.ParentTestNodeUid?.Value, MaxIdentityFieldLength));

    public static CapturedTestResult? TryCapture(TestNodeUpdateMessage update)
    {
        TestNode node = update.TestNode;
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
            // session-wide result list and generated XML within a predictable budget.
            // RawUid and ParentRawUid are used as keys/edges in the parent-chain
            // dictionary, so they must be capped with the same budget on both sides
            // to keep lookups consistent.
            Uid = Truncate(node.Uid.Value, MaxIdentityFieldLength)!,
            RawUid = Truncate(node.Uid.Value, MaxIdentityFieldLength)!,
            ParentRawUid = Truncate(update.ParentTestNodeUid?.Value, MaxIdentityFieldLength),
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
    {
#pragma warning disable CS0618, MTP0001 // CancelledTestNodeStateProperty is obsolete
        // Cancellation is an interruption, not an assertion failure. The RFC maps it
        // to <error> in the generated XML; classifying it as its own bucket here
        // keeps that mapping local to JUnit. Keep the cancellation decision out of
        // the shared helper so each report format preserves its existing behavior.
        if (state is CancelledTestNodeStateProperty)
        {
            return "cancelled";
        }
#pragma warning restore CS0618, MTP0001

        return TestResultCaptureHelper.ClassifyOutcome(state);
    }

    internal static string? Truncate(string? value, int maxLength)
        => TestResultCaptureHelper.Truncate(value, maxLength);
}
