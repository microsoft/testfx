// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform;
using Microsoft.Testing.Platform.Extensions.Messages;

namespace Microsoft.Testing.Extensions;

/// <summary>
/// A single captured test result used by the markdown summary reporters (Azure DevOps and GitHub Actions).
/// </summary>
internal readonly struct TestRecord
{
    public TestRecord(string displayName, string fullyQualifiedName, TerminalKind kind, TimeSpan duration)
        : this(displayName, fullyQualifiedName, kind, duration, isFlaky: false, failure: null)
    {
    }

    public TestRecord(string displayName, string fullyQualifiedName, TerminalKind kind, TimeSpan duration, bool isFlaky)
        : this(displayName, fullyQualifiedName, kind, duration, isFlaky, failure: null)
    {
    }

    public TestRecord(string displayName, string fullyQualifiedName, TerminalKind kind, TimeSpan duration, TestFailureDetails? failure)
        : this(displayName, fullyQualifiedName, kind, duration, isFlaky: false, failure)
    {
    }

    public TestRecord(string displayName, string fullyQualifiedName, TerminalKind kind, TimeSpan duration, bool isFlaky, TestFailureDetails? failure = null)
    {
        DisplayName = displayName;
        FullyQualifiedName = fullyQualifiedName;
        Kind = kind;
        Duration = duration;
        IsFlaky = isFlaky;
        Failure = failure;
    }

    public string DisplayName { get; }

    public string FullyQualifiedName { get; }

    public TerminalKind Kind { get; }

    public TimeSpan Duration { get; }

    public bool IsFlaky { get; }

    /// <summary>
    /// Gets the diagnostics captured for a <see cref="TerminalKind.Failed"/> record, or <see langword="null"/> when the
    /// record is not a failure or the reporter does not collect failure diagnostics.
    /// </summary>
    public TestFailureDetails? Failure { get; }
}

/// <summary>
/// The diagnostics captured for a failing test so a summary reporter can render them beyond the test's name.
/// </summary>
internal sealed class TestFailureDetails
{
    public TestFailureDetails(string? message, string? exceptionType, string? stackTrace, string? filePath, int lineNumber)
    {
        Message = message;
        ExceptionType = exceptionType;
        StackTrace = stackTrace;
        FilePath = filePath;
        LineNumber = lineNumber;
    }

    /// <summary>
    /// Gets the failure explanation reported by the test framework, falling back to the exception message.
    /// </summary>
    public string? Message { get; }

    /// <summary>
    /// Gets the full name of the exception type that caused the failure, when the framework supplied an exception.
    /// </summary>
    public string? ExceptionType { get; }

    /// <summary>
    /// Gets the stack trace of the failure, when available.
    /// </summary>
    public string? StackTrace { get; }

    /// <summary>
    /// Gets the workspace-relative, forward-slash normalized source file of the failure, when it could be resolved.
    /// </summary>
    public string? FilePath { get; }

    /// <summary>
    /// Gets the 1-based line number within <see cref="FilePath"/>, or <c>0</c> when only the file is known.
    /// </summary>
    public int LineNumber { get; }

    /// <summary>
    /// Gets a value indicating whether anything worth rendering was captured.
    /// </summary>
    public bool IsEmpty
        => RoslynString.IsNullOrWhiteSpace(Message)
            && RoslynString.IsNullOrWhiteSpace(ExceptionType)
            && RoslynString.IsNullOrWhiteSpace(StackTrace)
            && RoslynString.IsNullOrWhiteSpace(FilePath);
}

/// <summary>
/// The terminal outcome of a test, as understood by the markdown summary reporters.
/// </summary>
internal enum TerminalKind
{
    NotTerminal,
    Passed,
    Failed,
    Skipped,
}

/// <summary>
/// Helpers shared by the markdown summary reporters (Azure DevOps and GitHub Actions).
/// </summary>
internal static class SummaryReporterHelpers
{
    /// <summary>
    /// Maps a <see cref="TestNodeStateProperty"/> to the <see cref="TerminalKind"/> tracked by the summary reporters.
    /// </summary>
    public static TerminalKind GetTerminalKind(TestNodeStateProperty? state)
        => state switch
        {
            PassedTestNodeStateProperty => TerminalKind.Passed,
            FailedTestNodeStateProperty => TerminalKind.Failed,
            ErrorTestNodeStateProperty => TerminalKind.Failed,
            TimeoutTestNodeStateProperty => TerminalKind.Failed,
            SkippedTestNodeStateProperty => TerminalKind.Skipped,
#pragma warning disable CS0618, MTP0001
            CancelledTestNodeStateProperty => TerminalKind.Failed,
#pragma warning restore CS0618, MTP0001
            _ => TerminalKind.NotTerminal,
        };

    /// <summary>
    /// Formats a duration for display in a markdown summary. Durations below one minute always render as
    /// milliseconds (<c>{0}ms</c>) or seconds (<c>{0:0.00}s</c>); the caller supplies the composite format
    /// strings used for the minute (args: minutes, seconds) and hour (args: total hours, minutes, seconds)
    /// buckets so each reporter keeps its own rendering while sharing the branching algorithm.
    /// </summary>
    public static string FormatDuration(TimeSpan duration, string minuteFormat, string hourFormat)
    {
        if (duration < TimeSpan.FromSeconds(1))
        {
            return string.Format(CultureInfo.InvariantCulture, "{0}ms", (int)duration.TotalMilliseconds);
        }

        if (duration < TimeSpan.FromMinutes(1))
        {
            return string.Format(CultureInfo.InvariantCulture, "{0:0.00}s", duration.TotalSeconds);
        }

        if (duration < TimeSpan.FromHours(1))
        {
            return string.Format(CultureInfo.InvariantCulture, minuteFormat, duration.Minutes, duration.Seconds);
        }

        // The custom TimeSpan format `hh` is the *hour component* and wraps at 24 hours, so for >= 1 hour runs
        // we compute the total hours explicitly to keep multi-day sessions accurate.
        long totalHours = (long)Math.Floor(duration.TotalHours);
        return string.Format(CultureInfo.InvariantCulture, hourFormat, totalHours, duration.Minutes, duration.Seconds);
    }
}
