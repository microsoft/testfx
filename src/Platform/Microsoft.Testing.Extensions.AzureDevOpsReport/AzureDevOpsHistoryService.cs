// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.AzureDevOpsReport.Helpers;
using Microsoft.Testing.Extensions.AzureDevOpsReport.Resources;
using Microsoft.Testing.Extensions.Reporting;
using Microsoft.Testing.Platform;
using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Extensions.TestHost;
using Microsoft.Testing.Platform.Logging;
using Microsoft.Testing.Platform.Services;

namespace Microsoft.Testing.Extensions.AzureDevOpsReport;

internal sealed class AzureDevOpsHistoryService : ITestSessionLifetimeHandler, IAzureDevOpsHistoryService
{
    // NOTE: Inspect one extra run so the reporter can log when Azure DevOps history exceeds the inspection cap.
    private const int MaxRunsToInspect = 500;

    // NOTE: Request the largest practical result page to minimize Azure DevOps round-trips during session start.
    private const int ResultsPageSize = 1000;

    // NOTE: Bound per-run paging so a single large run cannot keep session startup busy indefinitely.
    private const int MaxResultPagesPerRun = 50;

    private static readonly TimeSpan HistoryLoadBudget = TimeSpan.FromSeconds(30);
    private static readonly IReadOnlyDictionary<string, FlakyStats> EmptyStatsByTest = new Dictionary<string, FlakyStats>(StringComparer.OrdinalIgnoreCase);

    private readonly ICommandLineOptions _commandLineOptions;
    private readonly IEnvironment _environment;
    private readonly IClock _clock;
    private readonly IAzureDevOpsHistoryClient _historyClient;
    private readonly ITask _task;
    private readonly ILogger _logger;
    private int _historyWindowInDays;
    private IReadOnlyDictionary<string, FlakyStats> _statsByTest = EmptyStatsByTest;

    public AzureDevOpsHistoryService(
        ICommandLineOptions commandLineOptions,
        IEnvironment environment,
        IClock clock,
        IAzureDevOpsHistoryClient historyClient,
        ITask task,
        ILoggerFactory loggerFactory)
    {
        _commandLineOptions = commandLineOptions;
        _environment = environment;
        _clock = clock;
        _historyClient = historyClient;
        _task = task;
        _logger = loggerFactory.CreateLogger<AzureDevOpsHistoryService>();
    }

    public string Uid => nameof(AzureDevOpsHistoryService);

    public string Version => ExtensionVersion.DefaultSemVer;

    public string DisplayName => AzureDevOpsResources.DisplayName;

    public string Description => AzureDevOpsResources.Description;

    public int HistoryWindowInDays => Volatile.Read(ref _historyWindowInDays);

    public Task<bool> IsEnabledAsync()
        => Task.FromResult(_commandLineOptions.IsOptionSet(AzureDevOpsCommandLineOptions.AzureDevOpsOptionName)
            && _commandLineOptions.IsOptionSet(AzureDevOpsCommandLineOptions.AzureDevOpsFlakyHistory));

    public async Task OnTestSessionStartingAsync(ITestSessionContext testSessionContext)
    {
        if (!_commandLineOptions.IsOptionSet(AzureDevOpsCommandLineOptions.AzureDevOpsOptionName)
            || !TryGetHistoryWindowInDays(out int historyWindowInDays))
        {
            return;
        }

        if (!string.Equals(_environment.GetEnvironmentVariable("TF_BUILD"), "true", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (!TryCreateQuery(historyWindowInDays, out AzureDevOpsHistoryQuery? query))
        {
            _logger.LogWarning(AzureDevOpsResources.FlakyHistoryMissingEnvironmentWarning);
            return;
        }

        try
        {
            CancellationTokenSource? budgetCancellationTokenSource = null;

            try
            {
                using var loadCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(testSessionContext.CancellationToken);
                budgetCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(testSessionContext.CancellationToken);
                Task loadTask = LoadHistoryAsync(query, historyWindowInDays, loadCancellationTokenSource.Token);
                Task budgetTask = _task.Delay(HistoryLoadBudget, budgetCancellationTokenSource.Token);

                if (await Task.WhenAny(loadTask, budgetTask).ConfigureAwait(false) != loadTask)
                {
                    testSessionContext.CancellationToken.ThrowIfCancellationRequested();
#pragma warning disable VSTHRD103 // CancelAsync is unavailable on all target frameworks.
                    loadCancellationTokenSource.Cancel();
#pragma warning restore VSTHRD103
                    ResetHistoryState();
                    _logger.LogInformation(string.Format(CultureInfo.InvariantCulture, AzureDevOpsResources.FlakyHistoryLoadTimedOutInfo, (int)HistoryLoadBudget.TotalSeconds));

                    try
                    {
                        await loadTask.ConfigureAwait(false);
                    }
                    catch (OperationCanceledException) when (!testSessionContext.CancellationToken.IsCancellationRequested)
                    {
                    }

                    return;
                }

#pragma warning disable VSTHRD103 // CancelAsync is unavailable on all target frameworks.
                budgetCancellationTokenSource.Cancel();
#pragma warning restore VSTHRD103
                await loadTask.ConfigureAwait(false);
            }
            finally
            {
                budgetCancellationTokenSource?.Dispose();
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            ResetHistoryState();
            _logger.LogWarning(string.Format(CultureInfo.InvariantCulture, AzureDevOpsResources.FlakyHistoryLoadFailedWarning, ex.Message));
        }
    }

    public Task OnTestSessionFinishingAsync(ITestSessionContext testSessionContext)
        => Task.CompletedTask;

    public bool TryGetStats(string testName, out FlakyStats stats)
    {
        if (RoslynString.IsNullOrWhiteSpace(testName))
        {
            stats = default;
            return false;
        }

        // NOTE: Callers must only query stats after test-session start completes; the published snapshot is empty until then.
        return Volatile.Read(ref _statsByTest).TryGetValue(testName, out stats);
    }

    public bool IsLikelyFlaky(string testName, double threshold)
        => TryGetStats(testName, out FlakyStats stats)
            && stats.TotalCount > 0
            && stats.FailureRate >= threshold;

    private async Task LoadHistoryAsync(AzureDevOpsHistoryQuery query, int historyWindowInDays, CancellationToken cancellationToken)
    {
        IReadOnlyList<AzureDevOpsTestRun> runs = await _historyClient.GetRunsAsync(query, MaxRunsToInspect + 1, cancellationToken).ConfigureAwait(false);
        if (runs.Count > MaxRunsToInspect)
        {
            _logger.LogInformation(string.Format(CultureInfo.InvariantCulture, AzureDevOpsResources.FlakyHistoryRunsCappedInfo, runs.Count, MaxRunsToInspect));
            runs = [.. runs.Take(MaxRunsToInspect)];
        }

        var counts = new Dictionary<string, (int PassCount, int FailCount)>(StringComparer.OrdinalIgnoreCase);
        foreach (AzureDevOpsTestRun run in runs)
        {
            await AggregateRunResultsAsync(query, run, counts, cancellationToken).ConfigureAwait(false);
        }

        PublishHistoryStats(historyWindowInDays, counts);
    }

    private async Task AggregateRunResultsAsync(
        AzureDevOpsHistoryQuery query,
        AzureDevOpsTestRun run,
        Dictionary<string, (int PassCount, int FailCount)> counts,
        CancellationToken cancellationToken)
    {
        string? continuationToken = null;
        string? previousContinuationToken = null;
        bool hasSeenContinuationToken = false;
        int skip = 0;

        for (int pageNumber = 0; pageNumber < MaxResultPagesPerRun; pageNumber++)
        {
            AzureDevOpsTestResultsPage page = await _historyClient.GetResultsAsync(query, run.Url, skip, ResultsPageSize, continuationToken, cancellationToken).ConfigureAwait(false);
            foreach (AzureDevOpsTestResult result in page.Results)
            {
                if (!counts.TryGetValue(result.AutomatedTestName, out (int PassCount, int FailCount) currentCount))
                {
                    currentCount = default;
                }

                counts[result.AutomatedTestName] = result.Outcome switch
                {
                    "Passed" => (currentCount.PassCount + 1, currentCount.FailCount),
                    "Failed" => (currentCount.PassCount, currentCount.FailCount + 1),
                    _ => currentCount,
                };
            }

            if (page.Results.Count == 0)
            {
                return;
            }

            if (page.ContinuationToken is null)
            {
                if (hasSeenContinuationToken || page.Results.Count < ResultsPageSize)
                {
                    return;
                }

                skip += page.Results.Count;
                continue;
            }

            if (page.ContinuationToken == previousContinuationToken)
            {
                _logger.LogWarning(string.Format(CultureInfo.InvariantCulture, AzureDevOpsResources.FlakyHistoryResultsPagingStoppedWarning, run.Url, MaxResultPagesPerRun));
                return;
            }

            hasSeenContinuationToken = true;
            previousContinuationToken = page.ContinuationToken;
            continuationToken = page.ContinuationToken;
        }

        _logger.LogWarning(string.Format(CultureInfo.InvariantCulture, AzureDevOpsResources.FlakyHistoryResultsPagingStoppedWarning, run.Url, MaxResultPagesPerRun));
    }

    private void PublishHistoryStats(int historyWindowInDays, Dictionary<string, (int PassCount, int FailCount)> counts)
    {
        Volatile.Write(ref _historyWindowInDays, historyWindowInDays);
        IReadOnlyDictionary<string, FlakyStats> publishedStats = counts.ToDictionary(
            static keyValuePair => keyValuePair.Key,
            static keyValuePair => new FlakyStats(keyValuePair.Value.PassCount, keyValuePair.Value.FailCount),
            StringComparer.OrdinalIgnoreCase);

        // NOTE: Volatile.Write on a reference atomically publishes the fully materialized dictionary to concurrent readers.
        Volatile.Write(ref _statsByTest, publishedStats);
    }

    private void ResetHistoryState()
    {
        Volatile.Write(ref _historyWindowInDays, 0);

        // NOTE: Volatile.Write on a reference atomically publishes the empty snapshot when history loading is skipped or fails.
        Volatile.Write(ref _statsByTest, EmptyStatsByTest);
    }

    private bool TryGetHistoryWindowInDays(out int historyWindowInDays)
    {
        historyWindowInDays = 0;
        return _commandLineOptions.TryGetOptionArgumentList(AzureDevOpsCommandLineOptions.AzureDevOpsFlakyHistory, out string[]? arguments)
            && arguments is [string value]
            && int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out historyWindowInDays);
    }

    private bool TryCreateQuery(int historyWindowInDays, [NotNullWhen(true)] out AzureDevOpsHistoryQuery? query)
    {
        string? collectionUri = _environment.GetEnvironmentVariable("SYSTEM_COLLECTIONURI");
        string? teamProject = _environment.GetEnvironmentVariable("SYSTEM_TEAMPROJECT");
        string? accessToken = _environment.GetEnvironmentVariable("SYSTEM_ACCESSTOKEN");
        string? buildDefinitionId = _environment.GetEnvironmentVariable("BUILD_DEFINITIONID");
        if (RoslynString.IsNullOrWhiteSpace(collectionUri)
            || RoslynString.IsNullOrWhiteSpace(teamProject)
            || RoslynString.IsNullOrWhiteSpace(accessToken)
            || RoslynString.IsNullOrWhiteSpace(buildDefinitionId))
        {
            query = null;
            return false;
        }

        DateTimeOffset now = _clock.UtcNow;
        query = new AzureDevOpsHistoryQuery(collectionUri, teamProject, accessToken, buildDefinitionId, now.AddDays(-historyWindowInDays), now);
        return true;
    }
}
