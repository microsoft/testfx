// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Extensions.AzureDevOpsReport;

internal sealed partial class AzureDevOpsSummaryReporter
{
    private CiRunSummaryHistoryTest? CreateHistoryTest(
        string uid,
        string displayName,
        string fullyQualifiedName,
        TerminalKind kind,
        TimeSpan duration,
        bool isFlaky)
    {
        FlakyStats flakyStats = default;
        DurationHistoryStats durationStats = default;
        bool hasFlakyStats = _historyService is not null
            && _historyService.TryGetStats(fullyQualifiedName, out flakyStats)
            && flakyStats.TotalCount > 0;
        bool hasDurationStats = _historyService is not null
            && _historyService.TryGetDurationStats(fullyQualifiedName, out durationStats)
            && durationStats.SampleCount > 0;
        return !hasFlakyStats && !hasDurationStats
            ? null
            : new CiRunSummaryHistoryTest
            {
                TestId = uid,
                DisplayName = displayName,
                FullyQualifiedName = fullyQualifiedName,
                Outcome = kind switch
                {
                    TerminalKind.Passed => "passed",
                    TerminalKind.Failed => "failed",
                    TerminalKind.Skipped => "skipped",
                    _ => throw new InvalidOperationException($"Unexpected terminal kind '{kind}'."),
                },
                DurationTicks = duration.Ticks,
                IsFlaky = isFlaky,
                HistoricalPassCount = hasFlakyStats ? flakyStats.PassCount : 0,
                HistoricalFailCount = hasFlakyStats ? flakyStats.FailCount : 0,
                HistoryWindowInDays = _historyService?.HistoryWindowInDays ?? 0,
                DurationSampleCount = hasDurationStats ? durationStats.SampleCount : 0,
                P95DurationMilliseconds = hasDurationStats ? durationStats.P95Milliseconds : 0,
                P99DurationMilliseconds = hasDurationStats ? durationStats.P99Milliseconds : 0,
            };
    }

    private void ClearFlakyRows(string uid)
    {
        if (!_flakyRowIndicesByUid.TryGetValue(uid, out List<int>? indices))
        {
            return;
        }

        _flakyRowIndicesByUid.Remove(uid);
        foreach (int index in indices)
        {
            SummaryRow row = _rows[index];
            TestRecord record = row.Record;
            row.Record = new TestRecord(
                record.DisplayName,
                record.FullyQualifiedName,
                record.Kind,
                record.Duration,
                isFlaky: false,
                record.Failure,
                record.SkipReason);
            row.HistoryTest?.IsFlaky = false;
        }
    }
}
