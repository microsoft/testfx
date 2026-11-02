// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform;

namespace Microsoft.Testing.Extensions;

#pragma warning disable RS0051 // Shared implementation details are compiled into multiple extension assemblies.

internal static partial class CiRunSummaryAggregation
{
    private const int MaxFailures = 20;
    private const int MaxSlowestTests = 10;
    private const int MaxTopFailingClasses = 5;
    private const int MaxSkipReasons = 10;
    private const int MaxSkipReasonLength = 512;

    public static CiRunSummaryModule CreateModule(
        IReadOnlyList<TestRecord> records,
        string assemblyName,
        string modulePath,
        string targetFramework,
        string architecture,
        string? executionId,
        string sessionUid,
        int attemptNumber,
        int exitCode,
        string? requestedOutputPath = null,
        CiCoverageSummaryData? coverage = null,
        bool writeOnFailureOnly = false)
    {
        long passed = 0;
        long failed = 0;
        long skipped = 0;
        long durationTicks = 0;
        var failures = new List<TestRecord>();
        var failingByClass = new Dictionary<string, long>(StringComparer.Ordinal);

        foreach (TestRecord record in records)
        {
            durationTicks = checked(durationTicks + record.Duration.Ticks);
            switch (record.Kind)
            {
                case TerminalKind.Passed:
                    passed++;
                    break;
                case TerminalKind.Failed:
                    failed++;
                    failures.Add(record);
                    string className = GetClassName(record.FullyQualifiedName);
                    failingByClass[className] = failingByClass.TryGetValue(className, out long count) ? count + 1 : 1;
                    break;
                case TerminalKind.Skipped:
                    skipped++;
                    break;
            }
        }

        return new CiRunSummaryModule
        {
            AssemblyName = assemblyName,
            ModulePath = modulePath,
            TargetFramework = targetFramework,
            Architecture = architecture,
            OperatingSystem = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                ? "Windows"
                : RuntimeInformation.IsOSPlatform(OSPlatform.Linux)
                    ? "Linux"
                    : RuntimeInformation.IsOSPlatform(OSPlatform.OSX) ? "macOS" : "Unknown",
            ExecutionId = executionId ?? string.Empty,
            SessionUid = sessionUid,
            RequestedOutputPath = requestedOutputPath ?? string.Empty,
            WriteOnFailureOnly = writeOnFailureOnly,
            AttemptNumber = attemptNumber,
            ExitCode = exitCode,
            TotalTests = checked(passed + failed + skipped),
            PassedTests = passed,
            FailedTests = failed,
            SkippedTests = skipped,
            TestDurationTicks = durationTicks,
            Failures =
            [
                .. failures
                    .OrderBy(record => record.FullyQualifiedName, StringComparer.Ordinal)
                    .ThenBy(record => record.DisplayName, StringComparer.Ordinal)
                    .Take(MaxFailures)
                    .Select(record => ToSummaryTest(record, includeFailureDetails: true)),
            ],
            FlakyTests =
            [
                .. records
                    .Where(static record => record.IsFlaky)
                    .OrderBy(static record => record.FullyQualifiedName, StringComparer.Ordinal)
                    .ThenBy(static record => record.DisplayName, StringComparer.Ordinal)
                    .Select(record => ToSummaryTest(record, includeFailureDetails: false)),
            ],
            SlowestTests =
            [
                .. records
                    .Where(record => record.Duration > TimeSpan.Zero)
                    .OrderByDescending(record => record.Duration)
                    .ThenBy(record => record.FullyQualifiedName, StringComparer.Ordinal)
                    .Take(MaxSlowestTests)
                    .Select(record => ToSummaryTest(record, includeFailureDetails: false)),
            ],
            SkipReasons =
            [
                .. records
                    .Where(static record => record.Kind == TerminalKind.Skipped)
                    .GroupBy(static record => NormalizeSkipReason(record.SkipReason), StringComparer.Ordinal)
                    .OrderByDescending(static group => group.LongCount())
                    .ThenBy(static group => group.Key, StringComparer.Ordinal)
                    .Take(MaxSkipReasons)
                    .Select(static group => new CiRunSummarySkipReason { Reason = group.Key, Count = group.LongCount() }),
            ],
            TopFailingClasses =
            [
                .. failingByClass
                    .OrderByDescending(pair => pair.Value)
                    .ThenBy(pair => pair.Key, StringComparer.Ordinal)
                    .Take(MaxTopFailingClasses)
                    .Select(pair => new CiRunSummaryFailingClass { ClassName = pair.Key, FailureCount = pair.Value }),
            ],
            Coverage = coverage ?? new CiCoverageSummaryData(),
        };
    }

    private static CiRunSummaryTest ToSummaryTest(TestRecord record, bool includeFailureDetails)
    {
        var test = new CiRunSummaryTest
        {
            DisplayName = record.DisplayName,
            FullyQualifiedName = record.FullyQualifiedName,
            DurationTicks = record.Duration.Ticks,
            Outcome = record.Kind switch
            {
                TerminalKind.Passed => "passed",
                TerminalKind.Failed => "failed",
                TerminalKind.Skipped => "skipped",
                _ => null,
            },
        };

        // Diagnostics are only meaningful on the failures list; carrying them on the slowest-tests entries would
        // duplicate potentially large stack traces inside every fragment for no rendering benefit.
        if (includeFailureDetails && record.Failure is { IsEmpty: false } failure)
        {
            test.ErrorMessage = failure.Message;
            test.ErrorType = failure.ExceptionType;
            test.StackTrace = failure.StackTrace;
            test.FilePath = failure.FilePath;
            test.LineNumber = failure.LineNumber > 0 ? failure.LineNumber : null;
        }

        return test;
    }

    private static string NormalizeSkipReason(string? reason)
    {
        if (RoslynString.IsNullOrWhiteSpace(reason))
        {
            return "Reason not provided";
        }

        string normalized = reason.Replace('\r', ' ').Replace('\n', ' ').Trim();
        return normalized.Length > MaxSkipReasonLength
            ? normalized.Substring(0, MaxSkipReasonLength - 3) + "..."
            : normalized;
    }

    private static string GetClassName(string fullyQualifiedName)
    {
        if (RoslynString.IsNullOrEmpty(fullyQualifiedName))
        {
            return "(unknown)";
        }

        int lastDot = fullyQualifiedName.LastIndexOf('.');
        return lastDot <= 0 ? "(unknown)" : fullyQualifiedName.Substring(0, lastDot);
    }
}

#pragma warning restore RS0051
