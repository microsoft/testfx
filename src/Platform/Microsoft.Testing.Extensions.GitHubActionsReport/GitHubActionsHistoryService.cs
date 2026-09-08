// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

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
