// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform;

namespace Microsoft.Testing.Extensions.GitHubActionsReport;

internal static partial class GitHubActionsHistoryStore
{
    public static IReadOnlyDictionary<
        (string TestId, string FullyQualifiedName, string DisplayName),
        GitHubActionsHistoryStats> AggregateStats(
        GitHubActionsHistorySnapshot snapshot,
        GitHubActionsHistoryScope scope)
    {
        var counts = new Dictionary<(string TestId, string FullyQualifiedName, string DisplayName), (
            int PassCount,
            int FailCount,
            int FlakyCount,
            long P95DurationTicks,
            long P99DurationTicks,
            int DurationSampleCount)>();
        foreach (IGrouping<
            (string TestId, string FullyQualifiedName, string DisplayName),
            GitHubActionsHistorySample> testGroup in snapshot.Samples
            .Where(sample => sample.IsInScope(scope))
            .GroupBy(static sample => (
                sample.TestId,
                sample.FullyQualifiedName,
                sample.DisplayName)))
        {
            int passCount = 0;
            int failCount = 0;
            int flakyCount = 0;
            foreach (IGrouping<(string RunId, int RunAttempt, string CommitSha, DateTimeOffset TimestampUtc, TimeSpan TimestampOffset), GitHubActionsHistorySample> runGroup
                in testGroup.GroupBy(static sample => sample.GetRunIdentity()))
            {
                if (runGroup.Any(static sample => sample.Outcome == GitHubActionsHistoryOutcome.Failed))
                {
                    failCount++;
                }
                else if (runGroup.Any(static sample => sample.Outcome == GitHubActionsHistoryOutcome.Passed))
                {
                    passCount++;
                }

                if (runGroup.Any(static sample => sample.IsFlaky))
                {
                    flakyCount++;
                }
            }

            long[] durationTicks =
            [
                .. testGroup
                    .Where(static sample => sample.DurationTicks > 0)
                    .Select(static sample => sample.DurationTicks)
                    .OrderBy(static duration => duration),
            ];
            counts[testGroup.Key] = (
                passCount,
                failCount,
                flakyCount,
                ComputePercentile(durationTicks, 95),
                ComputePercentile(durationTicks, 99),
                durationTicks.Length);
        }

        return counts.ToDictionary(
            static pair => pair.Key,
            static pair => new GitHubActionsHistoryStats(
                pair.Value.PassCount,
                pair.Value.FailCount,
                pair.Value.FlakyCount,
                pair.Value.P95DurationTicks,
                pair.Value.P99DurationTicks,
                pair.Value.DurationSampleCount));
    }

    internal static GitHubActionsHistorySnapshot Merge(
        GitHubActionsHistorySnapshot existing,
        IReadOnlyList<GitHubActionsHistorySample> currentSamples,
        DateTimeOffset generatedAtUtc,
        DateTimeOffset? cutoff = null)
    {
        var samples = new List<GitHubActionsHistorySample>(existing.Samples.Length + currentSamples.Count);
        samples.AddRange(existing.Samples);
        samples.AddRange(currentSamples.Where(sample => sample.TimestampUtc >= (cutoff ?? DateTimeOffset.MinValue)));

        return new GitHubActionsHistorySnapshot
        {
            SchemaVersion = SchemaVersion,
            GeneratedAtUtc = generatedAtUtc,
            Samples =
            [
                .. samples
                    .GroupBy(static sample => (
                        Test: GetTestIdentity(sample),
                        Run: sample.GetRunIdentity()))
                    .Select(static group => group.OrderByDescending(static sample => sample.TimestampUtc).First())
                    .GroupBy(static sample => GetTestIdentity(sample))
                    .SelectMany(static group => group
                        .OrderByDescending(static sample => sample.TimestampUtc)
                        .ThenByDescending(static sample => sample.RunAttempt)
                        .Take(MaxSamplesPerTest))
                    .OrderByDescending(static sample => sample.TimestampUtc)
                    .Take(MaxTotalSamples)
                    .OrderBy(static sample => sample.TimestampUtc)
                    .ThenBy(static sample => sample.FullyQualifiedName, StringComparer.Ordinal)
                    .ThenBy(static sample => sample.TestId, StringComparer.Ordinal),
            ],
        };
    }

    private static void Validate(GitHubActionsHistorySnapshot? snapshot, string path)
    {
        if (snapshot?.SchemaVersion > SchemaVersion)
        {
            throw new NotSupportedException(
                $"GitHub Actions test history snapshot '{path}' uses newer schema version {snapshot.SchemaVersion}.");
        }

        if (snapshot is null
            || snapshot.SchemaVersion != SchemaVersion
            || snapshot.Samples is null
            || snapshot.Samples.Length > MaxTotalSamples
            || snapshot.Samples.Any(static sample =>
                sample is null
                || RoslynString.IsNullOrWhiteSpace(sample.AssemblyName)
                || RoslynString.IsNullOrWhiteSpace(sample.TargetFramework)
                || RoslynString.IsNullOrWhiteSpace(sample.Architecture)
                || RoslynString.IsNullOrWhiteSpace(sample.RunnerOs)
                || RoslynString.IsNullOrWhiteSpace(sample.TestId)
                || RoslynString.IsNullOrWhiteSpace(sample.FullyQualifiedName)
                || RoslynString.IsNullOrWhiteSpace(sample.DisplayName)
                || sample.TimestampUtc == default
                || sample.RunAttempt <= 0
                || sample.DurationTicks < 0
                || sample.Outcome is not (GitHubActionsHistoryOutcome.Passed or GitHubActionsHistoryOutcome.Failed or GitHubActionsHistoryOutcome.Skipped))
            || ExceedsPerTestSampleLimit(snapshot.Samples))
        {
            throw new FormatException($"Invalid GitHub Actions test history snapshot '{path}'.");
        }
    }

    private static bool ExceedsPerTestSampleLimit(IReadOnlyList<GitHubActionsHistorySample> samples)
    {
        var counts = new Dictionary<(
            string AssemblyName,
            string TargetFramework,
            string Architecture,
            string RunnerOs,
            string TestId,
            string FullyQualifiedName,
            string DisplayName), int>();
        foreach (GitHubActionsHistorySample sample in samples)
        {
            if (sample is null)
            {
                continue;
            }

            (string AssemblyName,
                string TargetFramework,
                string Architecture,
                string RunnerOs,
                string TestId,
                string FullyQualifiedName,
                string DisplayName) identity = GetTestIdentity(sample);
            int count = counts.TryGetValue(identity, out int existingCount) ? existingCount + 1 : 1;
            if (count > MaxSamplesPerTest)
            {
                return true;
            }

            counts[identity] = count;
        }

        return false;
    }

    private static long ComputePercentile(IReadOnlyList<long> sortedValues, int percentile)
        => sortedValues.Count == 0
            ? 0
            : sortedValues[Math.Max(0, (int)Math.Ceiling(percentile / 100d * sortedValues.Count) - 1)];

    private static (
        string AssemblyName,
        string TargetFramework,
        string Architecture,
        string RunnerOs,
        string TestId,
        string FullyQualifiedName,
        string DisplayName) GetTestIdentity(GitHubActionsHistorySample sample)
        => (
            sample.AssemblyName,
            sample.TargetFramework,
            sample.Architecture.ToUpperInvariant(),
            sample.RunnerOs.ToUpperInvariant(),
            sample.TestId,
            sample.FullyQualifiedName,
            sample.DisplayName);
}
