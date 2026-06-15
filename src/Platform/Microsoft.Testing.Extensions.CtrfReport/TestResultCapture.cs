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

        // Keep the O(1) state lookup above as a fast path for non-terminal messages. Terminal
        // results collect all remaining required properties in one pass — replacing
        // 5 × SingleOrDefault<T>() + 1 × foreach/GetEnumerator() with one zero-allocation
        // GetStructEnumerator() pass. Singleton-typed properties use the local GetSingleOrDefaultValue
        // helper to preserve the throw-on-duplicate invariant that SingleOrDefault<T>() provided;
        // TestMetadataProperty is intentionally multi-valued and accumulates into a list.
        TimingProperty? timing = null;
        TestMethodIdentifierProperty? identifier = null;
        StandardOutputProperty? standardOutput = null;
        StandardErrorProperty? standardError = null;
        TestFileLocationProperty? location = null;
        List<KeyValuePair<string, string>>? traits = null;

        PropertyBag.PropertyBagEnumerator enumerator = node.Properties.GetStructEnumerator();
        while (enumerator.MoveNext())
        {
            switch (enumerator.Current)
            {
                case TimingProperty t: timing = GetSingleOrDefaultValue(timing, t); break;
                case TestMethodIdentifierProperty m: identifier = GetSingleOrDefaultValue(identifier, m); break;
                case StandardOutputProperty so: standardOutput = GetSingleOrDefaultValue(standardOutput, so); break;
                case StandardErrorProperty se: standardError = GetSingleOrDefaultValue(standardError, se); break;
                case TestFileLocationProperty loc: location = GetSingleOrDefaultValue(location, loc); break;
                case TestMetadataProperty meta:
                    // Trait keys and values are test-controlled so we truncate them to
                    // bound the size of the in-memory result list and generated report.
                    traits ??= [];
                    traits.Add(new KeyValuePair<string, string>(
                        Truncate(meta.Key, MaxTraitFieldLength)!,
                        Truncate(meta.Value, MaxTraitFieldLength)!));
                    break;
            }
        }

        static TProperty GetSingleOrDefaultValue<TProperty>(TProperty? existingProperty, TProperty property)
            where TProperty : class, IProperty
            => existingProperty is not null
                ? throw new InvalidOperationException($"Found multiple properties of type '{typeof(TProperty)}'.")
                : property;

        (string status, string? rawStatus) = ClassifyStatus(state);
        TimeSpan duration = timing?.GlobalTiming.Duration ?? TimeSpan.Zero;
        (string? ns, string? className, string? methodName) = GetClassAndMethodName(identifier);

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
            StandardOutput = Truncate(standardOutput?.StandardOutput, MaxStandardStreamLength),
            StandardError = Truncate(standardError?.StandardError, MaxStandardStreamLength),
            FilePath = Truncate(location?.FilePath, MaxIdentityFieldLength),
            Line = location?.LineSpan.Start.Line,
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
