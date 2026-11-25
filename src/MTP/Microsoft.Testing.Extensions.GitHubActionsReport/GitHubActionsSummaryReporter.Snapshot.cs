// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.GitHubActionsReport.Resources;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Services;

namespace Microsoft.Testing.Extensions.GitHubActionsReport;

internal sealed partial class GitHubActionsSummaryReporter
{
    /// <summary>
    /// Takes the recorded tests in a deterministic order and attaches diagnostics to the failures that will
    /// actually be rendered.
    /// </summary>
    /// <remarks>
    /// The order matters twice. It makes which failures get expanded reproducible — dictionary order is arbitrary,
    /// so without it a run could expand a different twenty failures each time — and it makes the direct path's
    /// "first <see cref="MaxFailures"/> encountered" agree with the aggregated path's "first
    /// <see cref="MaxFailures"/> by name", so exactly the failures that need diagnostics are the ones that pay
    /// for them.
    /// </remarks>
    private SummarySnapshot BuildSnapshot()
    {
        List<(string Uid, string Key, TestRecord Record)> entries;
        Dictionary<string, PendingFailure> pending;
        lock (_stateLock)
        {
            entries = [.. _records];
            pending = new Dictionary<string, PendingFailure>(_pendingFailures, StringComparer.Ordinal);
        }

        entries.Sort(static (left, right)
            => CompareForRendering(
                left.Key,
                left.Record.FullyQualifiedName,
                left.Record.DisplayName,
                right.Key,
                right.Record.FullyQualifiedName,
                right.Record.DisplayName));

        var snapshot = new List<TestRecord>(entries.Count);
        List<CiRunSummaryHistoryTest>? historyTests = _historyService.IsEnabled
            ? new(Math.Min(entries.Count, GitHubActionsHistoryStore.MaxTotalSamples))
            : null;
        Dictionary<(string TestId, string FullyQualifiedName, string DisplayName), int>? historyIndices =
            _historyService.IsEnabled ? [] : null;
        Dictionary<(string TestId, string FullyQualifiedName, string DisplayName), CiRunSummaryHistoryTest>? previousFailures =
            _historyService.IsEnabled ? [] : null;
        int expanded = 0;
        foreach ((string Uid, string Key, TestRecord Record) entry in entries)
        {
            TestRecord record = entry.Record;
            if (record.Kind == TerminalKind.Failed
                && expanded < MaxFailures
                && pending.TryGetValue(entry.Key, out PendingFailure failure))
            {
                expanded++;
                TestFailureDetails? failureDetails = CaptureFailureDetails(failure);
                failureDetails?.Attachments = record.Failure?.Attachments;

                if (failureDetails is not null
                    && _historyService.TryGetStats(
                        entry.Uid,
                        record.FullyQualifiedName,
                        record.DisplayName,
                        out GitHubActionsHistoryStats historyStats)
                    && (historyStats.TotalCount > 0 || historyStats.DurationSampleCount > 0))
                {
                    failureDetails = new TestFailureDetails(
                        GitHubActionsAnnotationReporter.FormatHistoryContext(
                            failureDetails.Message ?? GitHubActionsResources.NoFailureMessageFallback,
                            historyStats,
                            _historyService.HistoryWindowInDays,
                            _historyService.HistoryWindowInRuns),
                        failureDetails.ExceptionType,
                        failureDetails.StackTrace,
                        failureDetails.FilePath,
                        failureDetails.LineNumber)
                    {
                        Attachments = failureDetails.Attachments,
                    };
                }

                record = new TestRecord(
                    record.DisplayName,
                    record.FullyQualifiedName,
                    record.Kind,
                    record.Duration,
                    record.IsFlaky,
                    failureDetails);
            }

            snapshot.Add(record);
            if (historyTests is not null && historyIndices is not null && previousFailures is not null)
            {
                var historyTest = new CiRunSummaryHistoryTest
                {
                    TestId = entry.Uid,
                    DisplayName = record.DisplayName,
                    FullyQualifiedName = record.FullyQualifiedName,
                    Outcome = record.Kind switch
                    {
                        TerminalKind.Passed => GitHubActionsHistoryOutcome.Passed,
                        TerminalKind.Failed => GitHubActionsHistoryOutcome.Failed,
                        TerminalKind.Skipped => GitHubActionsHistoryOutcome.Skipped,
                        _ => throw new InvalidOperationException($"Unexpected terminal kind '{record.Kind}'."),
                    },
                    DurationTicks = record.Duration.Ticks,
                    IsFlaky = record.IsFlaky,
                };
                (string TestId, string FullyQualifiedName, string DisplayName) identity = (
                    historyTest.TestId,
                    historyTest.FullyQualifiedName,
                    historyTest.DisplayName);
                if (_historyService.TryGetStats(
                        historyTest.TestId,
                        historyTest.FullyQualifiedName,
                        historyTest.DisplayName,
                        out GitHubActionsHistoryStats stats)
                    && stats.FailCount > 0)
                {
                    historyTest.HistoricalPassCount = stats.PassCount;
                    historyTest.HistoricalFailCount = stats.FailCount;
                    historyTest.HistoryWindowInDays = _historyService.HistoryWindowInDays;
                    if (previousFailures.TryGetValue(identity, out CiRunSummaryHistoryTest? existing))
                    {
                        previousFailures[identity] = MergeHistoryTest(existing, historyTest);
                    }
                    else if (previousFailures.Count < GitHubActionsHistoryStore.MaxTotalSamples)
                    {
                        previousFailures.Add(identity, historyTest);
                    }
                }

                if (historyIndices.TryGetValue(identity, out int existingIndex))
                {
                    historyTests[existingIndex] = MergeHistoryTest(historyTests[existingIndex], historyTest);
                }
                else if (historyTests.Count < GitHubActionsHistoryStore.MaxTotalSamples)
                {
                    historyIndices.Add(identity, historyTests.Count);
                    historyTests.Add(historyTest);
                }
            }
        }

        if (historyTests is not null && historyIndices is not null && previousFailures is not null)
        {
            if (previousFailures.Keys.Any(identity => !historyIndices.ContainsKey(identity)))
            {
                var selected = new List<CiRunSummaryHistoryTest>(GitHubActionsHistoryStore.MaxTotalSamples);
                HashSet<(string TestId, string FullyQualifiedName, string DisplayName)> selectedIdentities = [];
                foreach (CiRunSummaryHistoryTest test in previousFailures.Values
                    .OrderBy(static test => test.Outcome == GitHubActionsHistoryOutcome.Passed ? 0 : 1)
                    .ThenBy(static test => test.FullyQualifiedName, StringComparer.Ordinal)
                    .ThenBy(static test => test.DisplayName, StringComparer.Ordinal)
                    .ThenBy(static test => test.TestId, StringComparer.Ordinal))
                {
                    if (selected.Count == GitHubActionsHistoryStore.MaxTotalSamples)
                    {
                        break;
                    }

                    selected.Add(test);
                    selectedIdentities.Add((test.TestId, test.FullyQualifiedName, test.DisplayName));
                }

                foreach (CiRunSummaryHistoryTest test in historyTests)
                {
                    if (selected.Count == GitHubActionsHistoryStore.MaxTotalSamples)
                    {
                        break;
                    }

                    if (selectedIdentities.Add((test.TestId, test.FullyQualifiedName, test.DisplayName)))
                    {
                        selected.Add(test);
                    }
                }

                selected.Sort(static (left, right) => CompareForRendering(
                    left.TestId, left.FullyQualifiedName, left.DisplayName,
                    right.TestId, right.FullyQualifiedName, right.DisplayName));
                historyTests = selected;
            }

            foreach (CiRunSummaryHistoryTest test in historyTests
                .Where(static test => test.Outcome != GitHubActionsHistoryOutcome.Passed))
            {
                test.HistoricalPassCount = 0;
                test.HistoricalFailCount = 0;
                test.HistoryWindowInDays = 0;
            }
        }

        return new SummarySnapshot(snapshot, historyTests ?? []);
    }

    private static CiRunSummaryHistoryTest MergeHistoryTest(
        CiRunSummaryHistoryTest existing,
        CiRunSummaryHistoryTest current)
        => new()
        {
            TestId = existing.TestId,
            FullyQualifiedName = existing.FullyQualifiedName,
            DisplayName = existing.DisplayName,
            Outcome = existing.Outcome == GitHubActionsHistoryOutcome.Failed
                || current.Outcome == GitHubActionsHistoryOutcome.Failed
                    ? GitHubActionsHistoryOutcome.Failed
                    : existing.Outcome == GitHubActionsHistoryOutcome.Passed
                        || current.Outcome == GitHubActionsHistoryOutcome.Passed
                            ? GitHubActionsHistoryOutcome.Passed
                            : GitHubActionsHistoryOutcome.Skipped,
            DurationTicks = Math.Max(existing.DurationTicks, current.DurationTicks),
            IsFlaky = existing.IsFlaky || current.IsFlaky,
            HistoricalPassCount = Math.Max(existing.HistoricalPassCount, current.HistoricalPassCount),
            HistoricalFailCount = Math.Max(existing.HistoricalFailCount, current.HistoricalFailCount),
            HistoryWindowInDays = Math.Max(existing.HistoryWindowInDays, current.HistoryWindowInDays),
        };

    private CiRunSummaryModule CreateModule(
        SummarySnapshot snapshot,
        string assemblyName,
        ITestSessionContext testSessionContext,
        CiCoverageSummaryData coverage)
    {
        CiRunSummaryModule module = CiRunSummaryAggregation.CreateModule(
            snapshot.Records,
            assemblyName,
            _testApplicationModuleInfo.GetCurrentTestApplicationFullPath(),
            _targetFrameworkMoniker.Value,
            RuntimeInformation.ProcessArchitecture.ToString().ToLowerInvariant(),
            _environment.GetEnvironmentVariable(EnvironmentVariableConstants.TESTINGPLATFORM_DOTNETTEST_EXECUTIONID),
            testSessionContext.SessionUid.Value,
            GetAttemptNumber(),
            _testApplicationProcessExitCode.GetProcessExitCode(),
            coverage: coverage,
            writeOnFailureOnly: _writeOnFailureOnly);
        module.HistoryTests = [.. snapshot.HistoryTests];
        module.GitHubActionsStepSummaryEnabled = _isSummaryEnabled;
        module.GitHubActionsHistoryPath = _historyService.HistoryPath;
        module.GitHubActionsHistoryWindowInDays = _historyService.IsEnabled
            ? _historyService.HistoryWindowInDays
            : 0;
        module.GitHubActionsStepSummarySections = GitHubActionsStepSummarySectionsParser.ToPersistedValues(_sections);
        return module;
    }

    private sealed class SummarySnapshot(
        IReadOnlyList<TestRecord> records,
        IReadOnlyList<CiRunSummaryHistoryTest> historyTests)
    {
        public IReadOnlyList<TestRecord> Records { get; } = records;

        public IReadOnlyList<CiRunSummaryHistoryTest> HistoryTests { get; } = historyTests;
    }
}
