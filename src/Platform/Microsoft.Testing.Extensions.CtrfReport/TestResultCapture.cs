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

        CapturedTestResultProperties properties = TestResultCaptureHelper.ExtractProperties(node.Properties, includeLocation: true);
        (string status, string? rawStatus) = ClassifyStatus(state);
        TimeSpan duration = properties.Timing?.GlobalTiming.Duration ?? TimeSpan.Zero;
        (string? ns, string? className, string? methodName) = GetClassAndMethodName(properties.Identifier);
        CapturedExceptionDetails exceptionDetails = TestResultCaptureHelper.ExtractExceptionDetails(state);

        return new CapturedTestResult
        {
            // Identity fields are test-controlled and can be unbounded (e.g. very long
            // UIDs/display names from generated data), so we also cap them to keep the
            // session-wide result list and generated report within a predictable budget.
            Uid = Truncate(node.Uid.Value, MaxIdentityFieldLength)!,
            DisplayName = Truncate(node.DisplayName, MaxIdentityFieldLength)!,
            Status = status,
            RawStatus = rawStatus,
            Duration = duration,
            StartTime = properties.Timing?.GlobalTiming.StartTime,
            EndTime = properties.Timing?.GlobalTiming.EndTime,
            Namespace = Truncate(ns, MaxIdentityFieldLength),
            ClassName = Truncate(className, MaxIdentityFieldLength),
            MethodName = Truncate(methodName, MaxIdentityFieldLength),
            ErrorMessage = Truncate(exceptionDetails.ErrorMessage, MaxMessageLength),
            ExceptionType = exceptionDetails.ExceptionType,
            StackTrace = Truncate(exceptionDetails.StackTrace, MaxStackTraceLength),
            StandardOutput = Truncate(properties.StandardOutput?.StandardOutput, MaxStandardStreamLength),
            StandardError = Truncate(properties.StandardError?.StandardError, MaxStandardStreamLength),
            FilePath = Truncate(properties.Location?.FilePath, MaxIdentityFieldLength),
            Line = properties.Location?.LineSpan.Start.Line,
            Traits = properties.Traits,
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

    private static (string? Namespace, string? ClassName, string? MethodName) GetClassAndMethodName(TestMethodIdentifierProperty? identifier)
    {
        if (identifier is null)
        {
            return (null, null, null);
        }

        string? ns = RoslynString.IsNullOrEmpty(identifier.Namespace) ? null : identifier.Namespace;
        return (ns, identifier.TypeName, identifier.MethodName);
    }

    internal static string? Truncate(string? value, int maxLength)
        => TestResultCaptureHelper.Truncate(value, maxLength);
}
