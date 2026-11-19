// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text.Json.Serialization;

using Microsoft.Testing.Platform;

namespace Microsoft.Testing.Extensions.GitHubActionsReport;

internal readonly struct GitHubActionsHistoryStats
{
    public GitHubActionsHistoryStats(
        int passCount,
        int failCount,
        int flakyCount = 0,
        long p95DurationTicks = 0,
        long p99DurationTicks = 0,
        int durationSampleCount = 0)
    {
        PassCount = passCount;
        FailCount = failCount;
        FlakyCount = flakyCount;
        P95Duration = TimeSpan.FromTicks(p95DurationTicks);
        P99Duration = TimeSpan.FromTicks(p99DurationTicks);
        DurationSampleCount = durationSampleCount;
    }

    public int PassCount { get; }

    public int FailCount { get; }

    public int FlakyCount { get; }

    public TimeSpan P95Duration { get; }

    public TimeSpan P99Duration { get; }

    public int DurationSampleCount { get; }

    public int TotalCount => PassCount + FailCount;

    public double FailureRate => TotalCount == 0 ? 0 : (double)FailCount / TotalCount;
}

internal readonly struct GitHubActionsHistoryScope(
    string assemblyName,
    string targetFramework,
    string architecture,
    string runnerOs)
{
    public string AssemblyName { get; } = assemblyName;

    public string TargetFramework { get; } = targetFramework;

    public string Architecture { get; } = architecture;

    public string RunnerOs { get; } = runnerOs;
}

internal sealed class GitHubActionsHistorySnapshot
{
    public int SchemaVersion { get; set; } = GitHubActionsHistoryStore.SchemaVersion;

    public DateTimeOffset GeneratedAtUtc { get; set; }

    public GitHubActionsHistorySample[] Samples { get; set; } = [];
}

internal sealed class GitHubActionsHistorySample
{
    public string RunId { get; set; } = string.Empty;

    public int RunAttempt { get; set; } = 1;

    public DateTimeOffset TimestampUtc { get; set; }

    public string CommitSha { get; set; } = string.Empty;

    public string RefName { get; set; } = string.Empty;

    public string RunnerOs { get; set; } = string.Empty;

    public string AssemblyName { get; set; } = string.Empty;

    public string TargetFramework { get; set; } = string.Empty;

    public string Architecture { get; set; } = string.Empty;

    public string TestId { get; set; } = string.Empty;

    public string FullyQualifiedName { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public string Outcome { get; set; } = string.Empty;

    public long DurationTicks { get; set; }

    public bool IsFlaky { get; set; }

    internal (string RunId, int RunAttempt, string CommitSha, DateTimeOffset TimestampUtc, TimeSpan TimestampOffset) GetRunIdentity()
        => RoslynString.IsNullOrWhiteSpace(RunId)
            ? (string.Empty, RunAttempt, CommitSha, TimestampUtc, TimestampUtc.Offset)
            : (RunId, RunAttempt, string.Empty, default, default);

    internal bool IsInScope(GitHubActionsHistoryScope scope)
        => string.Equals(AssemblyName, scope.AssemblyName, StringComparison.Ordinal)
            && string.Equals(TargetFramework, scope.TargetFramework, StringComparison.Ordinal)
            && string.Equals(Architecture, scope.Architecture, StringComparison.OrdinalIgnoreCase)
            && string.Equals(RunnerOs, scope.RunnerOs, StringComparison.OrdinalIgnoreCase);
}

internal static class GitHubActionsHistoryOutcome
{
    public const string Passed = "passed";
    public const string Failed = "failed";
    public const string Skipped = "skipped";
}

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(GitHubActionsHistorySnapshot))]
internal sealed partial class GitHubActionsHistoryJsonContext : JsonSerializerContext;
