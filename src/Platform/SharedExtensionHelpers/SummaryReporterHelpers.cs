// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions.Messages;

namespace Microsoft.Testing.Extensions;

/// <summary>
/// A single captured test result used by the markdown summary reporters (Azure DevOps and GitHub Actions).
/// </summary>
internal readonly struct TestRecord
{
    public TestRecord(string displayName, string fullyQualifiedName, TerminalKind kind, TimeSpan duration)
    {
        DisplayName = displayName;
        FullyQualifiedName = fullyQualifiedName;
        Kind = kind;
        Duration = duration;
    }

    public string DisplayName { get; }

    public string FullyQualifiedName { get; }

    public TerminalKind Kind { get; }

    public TimeSpan Duration { get; }
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
