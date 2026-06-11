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

        string outcome = ClassifyOutcome(state);

        TimingProperty? timing = node.Properties.SingleOrDefault<TimingProperty>();
        TimeSpan duration = timing?.GlobalTiming.Duration ?? TimeSpan.Zero;

        TestMethodIdentifierProperty? identifier = node.Properties.SingleOrDefault<TestMethodIdentifierProperty>();
        (string? className, string? methodName) = TestResultCaptureHelper.GetClassAndMethodName(identifier);

        string? errorMessage = state.Explanation;
        string? stackTrace = null;
        string? exceptionType = null;
        Exception? exception = state switch
        {
            FailedTestNodeStateProperty f => f.Exception,
            ErrorTestNodeStateProperty e => e.Exception,
            TimeoutTestNodeStateProperty t => t.Exception,
#pragma warning disable CS0618, MTP0001 // CancelledTestNodeStateProperty is obsolete
            CancelledTestNodeStateProperty c => c.Exception,
#pragma warning restore CS0618, MTP0001
            _ => null,
        };

        if (exception is not null)
        {
            errorMessage ??= exception.Message;
            stackTrace = exception.StackTrace;
            exceptionType = exception.GetType().FullName;
        }

        string? stdout = node.Properties.SingleOrDefault<StandardOutputProperty>()?.StandardOutput;
        string? stderr = node.Properties.SingleOrDefault<StandardErrorProperty>()?.StandardError;

        // Collect traits without using LINQ to avoid an enumerator allocation per node.
        // Trait keys and values are also test-controlled so we truncate them as well to
        // bound the size of the in-memory result list and generated HTML.
        List<KeyValuePair<string, string>>? traits = null;
        foreach (IProperty p in node.Properties)
        {
            if (p is TestMetadataProperty meta)
            {
                traits ??= [];
                traits.Add(new KeyValuePair<string, string>(
                    Truncate(meta.Key, MaxTraitFieldLength)!,
                    Truncate(meta.Value, MaxTraitFieldLength)!));
            }
        }

        return new CapturedTestResult
        {
            // Identity fields are test-controlled and can be unbounded (e.g. very long
            // UIDs/display names from generated data), so we also cap them to keep the
            // session-wide result list and generated HTML within a predictable budget.
            Uid = Truncate(node.Uid.Value, MaxIdentityFieldLength)!,
            DisplayName = Truncate(node.DisplayName, MaxIdentityFieldLength)!,
            Outcome = outcome,
            Duration = duration,
            StartTime = timing?.GlobalTiming.StartTime,
            EndTime = timing?.GlobalTiming.EndTime,
            ClassName = Truncate(className, MaxIdentityFieldLength),
            MethodName = Truncate(methodName, MaxIdentityFieldLength),
            ErrorMessage = Truncate(errorMessage, MaxMessageLength),
            ExceptionType = exceptionType,
            StackTrace = Truncate(stackTrace, MaxStackTraceLength),
            StandardOutput = Truncate(stdout, MaxStandardStreamLength),
            StandardError = Truncate(stderr, MaxStandardStreamLength),
            Traits = traits,
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
