// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text.Json;
using System.Text.Json.Serialization;

using Microsoft.Testing.Extensions.GitHubActionsReport.Resources;
using Microsoft.Testing.Platform;
using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Extensions.TestHost;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Logging;
using Microsoft.Testing.Platform.Services;

namespace Microsoft.Testing.Extensions.GitHubActionsReport;

internal interface IGitHubActionsHistoryService
{
    bool IsEnabled { get; }

    string? HistoryPath { get; }

    int HistoryWindowInDays { get; }

    bool TryGetStats(
        string testId,
        string fullyQualifiedName,
        string displayName,
        out GitHubActionsHistoryStats stats);

    Task WriteAsync(IReadOnlyList<CiRunSummaryModule> modules, CancellationToken cancellationToken);
}

internal sealed class DisabledGitHubActionsHistoryService : IGitHubActionsHistoryService
{
    public static DisabledGitHubActionsHistoryService Instance { get; } = new();

    public bool IsEnabled => false;

    public string? HistoryPath => null;

    public int HistoryWindowInDays => 0;

    public bool TryGetStats(
        string testId,
        string fullyQualifiedName,
        string displayName,
        out GitHubActionsHistoryStats stats)
    {
        stats = default;
        return false;
    }

    public Task WriteAsync(IReadOnlyList<CiRunSummaryModule> modules, CancellationToken cancellationToken)
        => Task.CompletedTask;
}

internal sealed class GitHubActionsHistoryService :
    IGitHubActionsHistoryService,
    ITestSessionLifetimeHandler
{
    private static readonly IReadOnlyDictionary<
        (string TestId, string FullyQualifiedName, string DisplayName),
        GitHubActionsHistoryStats> EmptyStats =
            new Dictionary<(string TestId, string FullyQualifiedName, string DisplayName), GitHubActionsHistoryStats>();

    private readonly IEnvironment _environment;
    private readonly IClock _clock;
    private readonly ILogger _logger;
    private readonly GitHubActionsHistoryScope _scope;
    private IReadOnlyDictionary<
        (string TestId, string FullyQualifiedName, string DisplayName),
        GitHubActionsHistoryStats> _statsByTest = EmptyStats;

    public GitHubActionsHistoryService(
        ICommandLineOptions commandLine,
        IEnvironment environment,
        IClock clock,
        ILoggerFactory loggerFactory,
        GitHubActionsHistoryScope scope)
    {
        _environment = environment;
        _clock = clock;
        _logger = loggerFactory.CreateLogger<GitHubActionsHistoryService>();
        _scope = scope;
        bool hasHistoryPath = commandLine.TryGetOptionArgumentList(
            GitHubActionsCommandLineOptions.GitHubActionsHistory,
            out string[]? pathArguments)
            && pathArguments is [string path]
            && !RoslynString.IsNullOrWhiteSpace(path);
        IsEnabled = GitHubActionsFeature.IsMasterEnabled(commandLine, environment)
            && hasHistoryPath;
        HistoryPath = IsEnabled ? Path.GetFullPath(pathArguments![0]) : null;
        HistoryWindowInDays = GetHistoryWindowInDays(commandLine);
    }

    public bool IsEnabled { get; }

    public string? HistoryPath { get; }

    public int HistoryWindowInDays { get; }

    public string Uid => nameof(GitHubActionsHistoryService);

    public string Version => ExtensionVersion.DefaultSemVer;

    public string DisplayName => GitHubActionsResources.DisplayName;

    public string Description => GitHubActionsResources.Description;

    public Task<bool> IsEnabledAsync() => Task.FromResult(IsEnabled);

    public async Task OnTestSessionStartingAsync(ITestSessionContext testSessionContext)
    {
        if (!IsEnabled)
        {
            return;
        }

        try
        {
            GitHubActionsHistorySnapshot snapshot = await GitHubActionsHistoryStore.ReadAsync(
                HistoryPath!,
                _clock.UtcNow.AddDays(-HistoryWindowInDays),
                testSessionContext.CancellationToken).ConfigureAwait(false);
            string? currentRunId = _environment.GetEnvironmentVariable("GITHUB_RUN_ID");
            int currentRunAttempt = GetRunAttempt(_environment);
            if (!RoslynString.IsNullOrWhiteSpace(currentRunId))
            {
                snapshot.Samples =
                [
                    .. snapshot.Samples.Where(sample =>
                        !string.Equals(sample.RunId, currentRunId, StringComparison.Ordinal)
                        || sample.RunAttempt != currentRunAttempt),
                ];
            }

            Volatile.Write(ref _statsByTest, GitHubActionsHistoryStore.AggregateStats(snapshot, _scope));
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            Volatile.Write(ref _statsByTest, EmptyStats);
            await _logger.LogWarningAsync(string.Format(
                CultureInfo.InvariantCulture,
                GitHubActionsResources.HistoryLoadFailedWarning,
                HistoryPath,
                ex.Message)).ConfigureAwait(false);
        }
    }

    public Task OnTestSessionFinishingAsync(ITestSessionContext testSessionContext)
        => Task.CompletedTask;

    public bool TryGetStats(
        string testId,
        string fullyQualifiedName,
        string displayName,
        out GitHubActionsHistoryStats stats)
    {
        if (RoslynString.IsNullOrWhiteSpace(testId)
            || RoslynString.IsNullOrWhiteSpace(fullyQualifiedName)
            || RoslynString.IsNullOrWhiteSpace(displayName))
        {
            stats = default;
            return false;
        }

        return Volatile.Read(ref _statsByTest).TryGetValue(
            (testId, fullyQualifiedName, displayName),
            out stats);
    }

    public async Task WriteAsync(IReadOnlyList<CiRunSummaryModule> modules, CancellationToken cancellationToken)
    {
        CiRunSummaryModule[] configuredModules =
        [
            .. modules.Where(static module => module.GitHubActionsHistoryPath is not null),
        ];
        if (configuredModules.Length == 0)
        {
            return;
        }

        DateTimeOffset now = _clock.UtcNow;
        string runId = _environment.GetEnvironmentVariable("GITHUB_RUN_ID") ?? string.Empty;
        int runAttempt = GetRunAttempt(_environment);
        string commitSha = _environment.GetEnvironmentVariable("GITHUB_SHA") ?? string.Empty;
        string refName = _environment.GetEnvironmentVariable("GITHUB_REF_NAME") ?? string.Empty;
        string runnerOs = _environment.GetEnvironmentVariable("RUNNER_OS") ?? string.Empty;
        foreach (IGrouping<string, CiRunSummaryModule> configuredGroup in configuredModules.GroupBy(
            static module => module.GitHubActionsHistoryPath!,
            StringComparer.Ordinal))
        {
            GitHubActionsHistorySample[] samples =
            [
                .. configuredGroup
                    .SelectMany(module => module.HistoryTests.Select(test => (Module: module, Test: test)))
                    .Take(GitHubActionsHistoryStore.MaxTotalSamples)
                    .Select(item => new GitHubActionsHistorySample
                    {
                        RunId = runId,
                        RunAttempt = runAttempt,
                        TimestampUtc = now,
                        CommitSha = commitSha,
                        RefName = refName,
                        RunnerOs = runnerOs,
                        AssemblyName = item.Module.AssemblyName,
                        TargetFramework = item.Module.TargetFramework,
                        Architecture = item.Module.Architecture,
                        TestId = item.Test.TestId,
                        FullyQualifiedName = item.Test.FullyQualifiedName,
                        DisplayName = item.Test.DisplayName,
                        Outcome = item.Test.Outcome,
                        DurationTicks = item.Test.DurationTicks,
                        IsFlaky = item.Test.IsFlaky,
                    }),
            ];

            try
            {
                await GitHubActionsHistoryStore.WriteMergedAsync(
                    configuredGroup.Key,
                    configuredGroup.Max(static module => module.GitHubActionsHistoryWindowInDays),
                    now,
                    samples,
                    cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                await _logger.LogWarningAsync(string.Format(
                    CultureInfo.InvariantCulture,
                    GitHubActionsResources.HistoryWriteFailedWarning,
                    configuredGroup.Key,
                    ex.Message)).ConfigureAwait(false);
            }
        }
    }

    private static int GetRunAttempt(IEnvironment environment)
        => int.TryParse(
            environment.GetEnvironmentVariable("GITHUB_RUN_ATTEMPT"),
            NumberStyles.None,
            CultureInfo.InvariantCulture,
            out int parsedRunAttempt)
            && parsedRunAttempt > 0
                ? parsedRunAttempt
                : 1;

    private static int GetHistoryWindowInDays(ICommandLineOptions commandLine)
        => commandLine.TryGetOptionArgumentList(
                GitHubActionsCommandLineOptions.GitHubActionsHistoryWindow,
                out string[]? arguments)
            && arguments is [string value]
            && int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out int windowInDays)
                ? windowInDays
                : GitHubActionsCommandLineOptions.HistoryWindowDefaultDays;
}

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

internal static class GitHubActionsHistoryStore
{
    internal const int SchemaVersion = 1;
    internal const int MaxSamplesPerTest = 1000;
    internal const int MaxTotalSamples = 10_000;
    internal const long MaxSnapshotBytes = 16 * 1024 * 1024;

    private static readonly TimeSpan LockWaitBudget = TimeSpan.FromSeconds(30);

    public static async Task<GitHubActionsHistorySnapshot> ReadAsync(
        string path,
        DateTimeOffset cutoff,
        CancellationToken cancellationToken)
    {
        if (!File.Exists(path))
        {
            return new GitHubActionsHistorySnapshot();
        }

        string fullPath = Path.GetFullPath(path);
        string lockPath = fullPath + ".lock";
        var stopwatch = Stopwatch.StartNew();
        for (int attempt = 1; ; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                using FileStream lockStream = new(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
                return ReadCore(fullPath, cutoff);
            }
            catch (Exception ex) when (IsRetryableFileException(ex) && stopwatch.Elapsed < LockWaitBudget)
            {
                await Task.Delay(GetRetryDelay(attempt), cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private static GitHubActionsHistorySnapshot ReadCore(string path, DateTimeOffset cutoff)
    {
        if (!File.Exists(path))
        {
            return new GitHubActionsHistorySnapshot();
        }

        using FileStream stream = File.OpenRead(path);
        if (stream.Length > MaxSnapshotBytes)
        {
            throw new FormatException(
                $"GitHub Actions test history snapshot '{path}' exceeds the {MaxSnapshotBytes.ToString(CultureInfo.InvariantCulture)}-byte limit.");
        }

        GitHubActionsHistorySnapshot? snapshot = JsonSerializer.Deserialize(stream, GitHubActionsHistoryJsonContext.Default.GitHubActionsHistorySnapshot);
        Validate(snapshot, path);
        snapshot!.Samples =
        [
            .. snapshot.Samples
                .Where(sample => sample.TimestampUtc >= cutoff)
                .OrderBy(static sample => sample.TimestampUtc)
                .ThenBy(static sample => sample.FullyQualifiedName, StringComparer.Ordinal)
                .ThenBy(static sample => sample.TestId, StringComparer.Ordinal),
        ];
        return snapshot;
    }

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

    public static async Task WriteMergedAsync(
        string path,
        int historyWindowInDays,
        DateTimeOffset now,
        IReadOnlyList<GitHubActionsHistorySample> currentSamples,
        CancellationToken cancellationToken)
    {
        string fullPath = Path.GetFullPath(path);
        string? directory = Path.GetDirectoryName(fullPath);
        if (directory is not null)
        {
            Directory.CreateDirectory(directory);
        }

        string lockPath = fullPath + ".lock";
        var stopwatch = Stopwatch.StartNew();
        for (int attempt = 1; ; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                using FileStream lockStream = new(lockPath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
                GitHubActionsHistorySnapshot existing;
                try
                {
                    existing = ReadCore(fullPath, now.AddDays(-historyWindowInDays));
                }
                catch (Exception ex) when (ex is FormatException or JsonException)
                {
                    existing = new GitHubActionsHistorySnapshot();
                }

                GitHubActionsHistorySnapshot merged = Merge(
                    existing,
                    currentSamples,
                    now,
                    now.AddDays(-historyWindowInDays));
                await WriteAtomicAsync(fullPath, merged, cancellationToken).ConfigureAwait(false);
                return;
            }
            catch (Exception ex) when (IsRetryableFileException(ex) && stopwatch.Elapsed < LockWaitBudget)
            {
                await Task.Delay(GetRetryDelay(attempt), cancellationToken).ConfigureAwait(false);
            }
        }
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

    private static async Task WriteAtomicAsync(
        string path,
        GitHubActionsHistorySnapshot snapshot,
        CancellationToken cancellationToken)
    {
        string tempPath = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var stream = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.Read))
            {
                await JsonSerializer.SerializeAsync(
                    stream,
                    snapshot,
                    GitHubActionsHistoryJsonContext.Default.GitHubActionsHistorySnapshot,
                    cancellationToken).ConfigureAwait(false);
            }

            long snapshotLength = new FileInfo(tempPath).Length;
            if (snapshotLength > MaxSnapshotBytes)
            {
                throw new FormatException(
                    $"GitHub Actions test history snapshot would be {snapshotLength.ToString(CultureInfo.InvariantCulture)} bytes, exceeding the {MaxSnapshotBytes.ToString(CultureInfo.InvariantCulture)}-byte limit.");
            }

            if (File.Exists(path))
            {
                File.Replace(tempPath, path, destinationBackupFileName: null);
            }
            else
            {
                File.Move(tempPath, path);
            }
        }
        finally
        {
            try
            {
                File.Delete(tempPath);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // Best-effort cleanup must not hide a successful write or its primary failure.
            }
        }
    }

    private static bool IsRetryableFileException(Exception exception)
        => exception is IOException or UnauthorizedAccessException;

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

    private static TimeSpan GetRetryDelay(int attempt)
        => TimeSpan.FromMilliseconds(Math.Min(1000, 50 * Math.Pow(2, Math.Min(attempt - 1, 5))));
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
