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
    internal const int MaxStandardStreamLength = 32 * 1024;
    internal const int MaxStackTraceLength = 32 * 1024;
    internal const int MaxMessageLength = 16 * 1024;
    internal const int MaxIdentityFieldLength = 4 * 1024;
    internal const int MaxTraitFieldLength = 1024;

    public static CapturedTestResult? TryCapture(TestNode node)
    {
        TestNodeStateProperty? state = node.Properties.SingleOrDefault<TestNodeStateProperty>();
        if (state is null or DiscoveredTestNodeStateProperty or InProgressTestNodeStateProperty)
        {
            return null;
        }

        (string status, string? rawStatus) = ClassifyStatus(state);

        TimingProperty? timing = node.Properties.SingleOrDefault<TimingProperty>();
        TimeSpan duration = timing?.GlobalTiming.Duration ?? TimeSpan.Zero;

        (string? ns, string? className, string? methodName) = GetClassAndMethodName(node);

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

        TestFileLocationProperty? location = node.Properties.SingleOrDefault<TestFileLocationProperty>();
        string? filePath = location?.FilePath;
        int? line = location?.LineSpan.Start.Line;

        // Collect traits without using LINQ to avoid an enumerator allocation per node.
        // Trait keys and values are also test-controlled so we truncate them as well to
        // bound the size of the in-memory result list and generated report.
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
            // session-wide result list and generated report within a predictable budget.
            Uid = Truncate(node.Uid.Value, MaxIdentityFieldLength)!,
            DisplayName = Truncate(node.DisplayName, MaxIdentityFieldLength)!,
            Status = status,
            RawStatus = rawStatus,
            Duration = duration,
            StartTime = timing?.GlobalTiming.StartTime,
            EndTime = timing?.GlobalTiming.EndTime,
            Namespace = Truncate(ns, MaxIdentityFieldLength),
            ClassName = Truncate(className, MaxIdentityFieldLength),
            MethodName = Truncate(methodName, MaxIdentityFieldLength),
            ErrorMessage = Truncate(errorMessage, MaxMessageLength),
            ExceptionType = exceptionType,
            StackTrace = Truncate(stackTrace, MaxStackTraceLength),
            StandardOutput = Truncate(stdout, MaxStandardStreamLength),
            StandardError = Truncate(stderr, MaxStandardStreamLength),
            FilePath = Truncate(filePath, MaxIdentityFieldLength),
            Line = line,
            Traits = traits,
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

    private static (string? Namespace, string? ClassName, string? MethodName) GetClassAndMethodName(TestNode node)
    {
        TestMethodIdentifierProperty? identifier = node.Properties.SingleOrDefault<TestMethodIdentifierProperty>();
        if (identifier is null)
        {
            return (null, null, null);
        }

        string? ns = RoslynString.IsNullOrEmpty(identifier.Namespace) ? null : identifier.Namespace;
        return (ns, identifier.TypeName, identifier.MethodName);
    }

    internal static string? Truncate(string? value, int maxLength)
        => value is null || value.Length <= maxLength
            ? value
            : value.Substring(0, maxLength)
                + $"\n…[truncated, original length: {value.Length.ToString(CultureInfo.InvariantCulture)}]";
}
