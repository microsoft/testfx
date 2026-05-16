// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Messages;

namespace Microsoft.Testing.Extensions.HtmlReport;

// Projects a TestNodeUpdateMessage into a capped CapturedTestResult so that the
// generator does not retain entire test node payloads (and their potentially huge
// stdout/stderr/stack traces) in memory for the whole session.
internal static class TestResultCapture
{
    internal const int MaxStandardStreamLength = 32 * 1024;
    internal const int MaxStackTraceLength = 32 * 1024;
    internal const int MaxMessageLength = 16 * 1024;

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

        (string? className, string? methodName) = GetClassAndMethodName(node);

        string? errorMessage = state.Explanation;
        string? stackTrace = null;
        string? exceptionType = null;
        Exception? exception = state switch
        {
            FailedTestNodeStateProperty f => f.Exception,
            ErrorTestNodeStateProperty e => e.Exception,
            TimeoutTestNodeStateProperty t => t.Exception,
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
        List<KeyValuePair<string, string>>? traits = null;
        foreach (IProperty p in node.Properties)
        {
            if (p is TestMetadataProperty meta)
            {
                traits ??= [];
                traits.Add(new KeyValuePair<string, string>(meta.Key, meta.Value));
            }
        }

        return new CapturedTestResult
        {
            Uid = node.Uid.Value,
            DisplayName = node.DisplayName,
            Outcome = outcome,
            Duration = duration,
            StartTime = timing?.GlobalTiming.StartTime,
            EndTime = timing?.GlobalTiming.EndTime,
            ClassName = className,
            MethodName = methodName,
            ErrorMessage = Truncate(errorMessage, MaxMessageLength),
            ExceptionType = exceptionType,
            StackTrace = Truncate(stackTrace, MaxStackTraceLength),
            StandardOutput = Truncate(stdout, MaxStandardStreamLength),
            StandardError = Truncate(stderr, MaxStandardStreamLength),
            Traits = traits,
        };
    }

    private static string ClassifyOutcome(TestNodeStateProperty state)
        => state switch
        {
            PassedTestNodeStateProperty => "passed",
            SkippedTestNodeStateProperty => "skipped",
            TimeoutTestNodeStateProperty => "timedOut",
            ErrorTestNodeStateProperty => "errored",
            FailedTestNodeStateProperty => "failed",
            _ when Array.IndexOf(TestNodePropertiesCategories.WellKnownTestNodeTestRunOutcomeFailedProperties, state.GetType()) >= 0
                => "failed",
            _ => throw ApplicationStateGuard.Unreachable(),
        };

    private static (string? ClassName, string? MethodName) GetClassAndMethodName(TestNode node)
    {
        TestMethodIdentifierProperty? identifier = node.Properties.SingleOrDefault<TestMethodIdentifierProperty>();
        if (identifier is null)
        {
            return (null, null);
        }

        string className = RoslynString.IsNullOrEmpty(identifier.Namespace)
            ? identifier.TypeName
            : $"{identifier.Namespace}.{identifier.TypeName}";

        return (className, identifier.MethodName);
    }

    internal static string? Truncate(string? value, int maxLength)
        => value is null || value.Length <= maxLength
            ? value
            : value.Substring(0, maxLength)
                + $"\n…[truncated, original length: {value.Length.ToString(CultureInfo.InvariantCulture)}]";
}
