// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.GitHubActionsReport.Resources;
using Microsoft.Testing.Platform;
using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Extensions.ArtifactPostProcessing;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Logging;

namespace Microsoft.Testing.Extensions.GitHubActionsReport;

#pragma warning disable RS0051 // Internal processor implementation is not a compatibility contract.

internal sealed class GitHubActionsSummaryArtifactPostProcessor(
    ICommandLineOptions commandLineOptions,
    IEnvironment environment,
    IFileSystem fileSystem,
    ILoggerFactory loggerFactory,
    Func<bool> downstreamRequiredPostProcessingSupported,
    IGitHubActionsHistoryService? historyService = null)
    : IArtifactPostProcessorRequiresPostProcessing
{
    internal const string FragmentArtifactKind = "microsoft.testing.github-actions-summary-fragment";
    internal const string SummaryArtifactKind = "microsoft.testing.github-actions-summary";
    internal const string Provider = "github-actions";
    internal const string ProviderSlug = "github-actions";

    private const string StepSummaryEnvironmentVariable = "GITHUB_STEP_SUMMARY";
    private const int MaxSlowestTests = 10;
    private const int StepSummaryMaxWriteAttempts = 20;
    private static readonly string[] SupportedArtifactKinds = [FragmentArtifactKind];
    private static readonly ArtifactPostProcessingMode[] SupportedPostProcessingModes =
        [ArtifactPostProcessingMode.TestModules, ArtifactPostProcessingMode.RetryAttempts];

    private static readonly TimeSpan StepSummaryRetryDelay = TimeSpan.FromMilliseconds(50);
    private readonly bool _isEnabled =
        GitHubActionsFeature.IsEnabled(commandLineOptions, environment, GitHubActionsCommandLineOptions.GitHubActionsStepSummary)
        || (historyService?.IsEnabled ?? false)
        || commandLineOptions.IsOptionSet(ArtifactPostProcessingDispatcherToolCommandLine.ManifestOptionName);

    private readonly IGitHubActionsHistoryService _historyService =
        historyService ?? DisabledGitHubActionsHistoryService.Instance;

    private readonly bool _includeFailureDetails =
        GitHubActionsFeature.IsKnobEnabled(commandLineOptions, GitHubActionsCommandLineOptions.GitHubActionsFailureDetails);

    public string Uid => "Microsoft.Testing.Extensions.GitHubActionsReport.SummaryPostProcessor";

    public string Version => ExtensionVersion.DefaultSemVer;

    public string DisplayName => GitHubActionsResources.DisplayName;

    public string Description => GitHubActionsResources.Description;

    public IReadOnlyList<ArtifactPostProcessingMode> SupportedModes => SupportedPostProcessingModes;

    public bool SupportsTruncatedRuns => true;

    public IReadOnlyList<string> SupportedKinds => SupportedArtifactKinds;

    public IReadOnlyList<string> SupportedFileExtensionsFallback => [];

    public Task<bool> IsEnabledAsync() => Task.FromResult(_isEnabled);

    public async Task<ProcessedArtifact?> ProcessAsync(
        IReadOnlyList<InputArtifact> inputs,
        string outputDirectory,
        ArtifactPostProcessingContext context,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (context.Mode == ArtifactPostProcessingMode.RetryAttempts && context.RunSummary is null)
        {
            CiRunSummaryAggregate historyAggregate = CiRunSummaryAggregation.ReadAndAggregate(inputs, Provider, context);
            await _historyService.WriteAsync(
                [CreateRetryHistoryModule(historyAggregate.Modules)],
                cancellationToken).ConfigureAwait(false);

            // Retry inputs overlap, so summing their per-attempt fragments would publish plausible but incorrect
            // logical totals. Persist only the mergeable per-test history, then leave the summary artifacts
            // untouched when the orchestrator cannot supply an authoritative passed/failed/skipped split.
            return null;
        }

        CiRunSummaryAggregate aggregate = CiRunSummaryAggregation.ReadAndAggregate(inputs, Provider, context);
        bool downstreamPostProcessingWillPublishSummary =
            context.Mode == ArtifactPostProcessingMode.RetryAttempts
            && downstreamRequiredPostProcessingSupported();
        CiRunSummaryModule? retryMergedModule = context.Mode == ArtifactPostProcessingMode.RetryAttempts
            ? CreateRetryMergedModule(aggregate)
            : null;
        if (context.Mode == ArtifactPostProcessingMode.TestModules)
        {
            await _historyService.WriteAsync(aggregate.Modules, cancellationToken).ConfigureAwait(false);
        }
        else if (!downstreamPostProcessingWillPublishSummary)
        {
            await _historyService.WriteAsync([retryMergedModule!], cancellationToken).ConfigureAwait(false);
        }

        bool summaryEnabled = aggregate.Modules.Any(static module => module.GitHubActionsStepSummaryEnabled is not false);
        if (!summaryEnabled)
        {
            return downstreamPostProcessingWillPublishSummary
                ? await CreateOutputArtifactAsync(aggregate, outputDirectory, summaryOutputPath: null, retryMergedModule).ConfigureAwait(false)
                : null;
        }

        string aggregationId = CiRunSummaryAggregation.CreateAggregationId(inputs);
        string? outputPath = context.Mode == ArtifactPostProcessingMode.RetryAttempts
            ? null
            : CiRunSummaryAggregation.GetMergedOutputPath(outputDirectory, ProviderSlug, aggregationId);
        string? stepSummaryPath = environment.GetEnvironmentVariable(StepSummaryEnvironmentVariable);
        ILogger logger = loggerFactory.CreateLogger<GitHubActionsSummaryArtifactPostProcessor>();

        // Honour the modules' own on-failure preference: only skip the summary when every module asked for it and
        // the run actually passed.
        bool writeOnFailureOnly = aggregate.Modules.Count > 0
            && aggregate.Modules.All(static module => module.WriteOnFailureOnly);
        bool runFailed = aggregate.IsPartial
            || aggregate.FailedTests > 0
            || (aggregate.ExitCode is int exitCode
                ? GitHubActionsExitCode.IndicatesFailure(exitCode)
                : aggregate.Modules.Any(static module => GitHubActionsExitCode.IndicatesFailure(module.ExitCode)));
        StepSummaryWriter? writer = downstreamPostProcessingWillPublishSummary
            || RoslynString.IsNullOrWhiteSpace(stepSummaryPath)
            || (writeOnFailureOnly && !runFailed)
            ? null
            : new StepSummaryWriter(fileSystem, stepSummaryPath!, logger, StepSummaryMaxWriteAttempts, StepSummaryRetryDelay);

        // The artifact is a standalone file, so what other steps wrote to the job summary is none of its business:
        // it is rendered without that contribution. Its own size still degrades it on the same thresholds, which
        // also keeps a pathological run from building a multi-hundred-megabyte string.
        GitHubActionsSummaryReporter.AggregateRenderResult artifact = GitHubActionsSummaryReporter.BuildAggregateMarkdown(aggregate, _includeFailureDetails);
        if (outputPath is not null)
        {
            await CiRunSummaryAggregation.WriteOutputAsync(outputPath, artifact.Markdown).ConfigureAwait(false);
        }

        if (writer is null)
        {
            return await CreateOutputArtifactAsync(aggregate, outputDirectory, outputPath, retryMergedModule).ConfigureAwait(false);
        }

        // Bounding the summary rendering against its own size alone would be useless: it is appended to a file
        // other steps also write to, and it is that file GitHub measures. Seeding the budget with what is already
        // there is what makes the degradation bound the artifact GitHub sees. Measured once — a refused write
        // leaves the file untouched, so the fallback below faces the same length.
        long alreadyWritten = writer.GetSummaryLengthExcludingSection(aggregationId);
        GitHubActionsSummaryReporter.AggregateRenderResult rendered = alreadyWritten == 0
            ? artifact
            : GitHubActionsSummaryReporter.BuildAggregateMarkdown(aggregate, _includeFailureDetails, condenseAllModules: false, alreadyWritten);

        // Losing whole project sections is a different loss from losing their diagnostics, so say whichever
        // actually happened. Shortening implies the budget was already exhausted, so it takes precedence. The
        // note describes what the *summary* lost, so it is derived from the summary's rendering, not the artifact's.
        // "Fully reported" spans the whole file, not just this run: the writer is evaluated under its own lock and
        // hands back the number of per-project sections already there, which a job that mixes this aggregated path
        // with the direct one will have.
        Func<int, string>? leadingNotice = rendered.CondensedModules > 0 || rendered.UnlistedModules > 0
            ? existingSections => GitHubActionsSummaryReporter.BuildTruncationNotice(existingSections + rendered.FullyReportedModules(aggregate.Modules.Count))
            : rendered.ModulesWithOmittedDetails > 0
                ? _ => GitHubActionsSummaryReporter.BuildAggregateTruncationNotice(rendered.ModulesWithOmittedDetails, aggregate.Modules.Count)
                : null;

        // The step summary is shared with every other step in the job, so the rendered file can exceed GitHub's
        // cap even though this section was budgeted. The writer refuses rather than replacing the file with one
        // GitHub would discard in full; fall back to a verdict line per test project, which is the smallest
        // report this can produce.
        bool? writeResult = await TryUpsertStepSummaryAsync(
            writer,
            aggregationId,
            rendered.Markdown,
            leadingNotice,
            logger,
            cancellationToken).ConfigureAwait(false);
        if (writeResult is false)
        {
            // Every listed module is a verdict line in this rendering, so no test project has a full section.
            GitHubActionsSummaryReporter.AggregateRenderResult condensed = GitHubActionsSummaryReporter.BuildAggregateMarkdown(
                aggregate,
                includeFailureDetails: false,
                condenseAllModules: true,
                alreadyWritten);

            bool? condensedWriteResult = await TryUpsertStepSummaryAsync(
                writer,
                aggregationId,
                condensed.Markdown,
                // Nothing this run contributes is a full section now, but sections the direct path already wrote
                // still are, so they remain the count the note reports.
                static existingSections => GitHubActionsSummaryReporter.BuildTruncationNotice(existingSections),
                logger,
                cancellationToken).ConfigureAwait(false);
            if (condensedWriteResult is false && logger.IsEnabled(LogLevel.Warning))
            {
                // Both renderings were refused, so this run contributed nothing to the job summary. Saying so is
                // the whole point of the refusal: the alternative is a summary that is silently missing a
                // section, indistinguishable from a run that produced none.
                await logger.LogWarningAsync(string.Format(
                    CultureInfo.InvariantCulture,
                    GitHubActionsResources.StepSummaryLimitExceededWarning,
                    (writer.GetSummaryLength() ?? 0).ToString(CultureInfo.InvariantCulture),
                    GitHubActionsFailureDetails.EffectiveStepSummaryLimit.ToString(CultureInfo.InvariantCulture))).ConfigureAwait(false);
            }
        }

        return await CreateOutputArtifactAsync(aggregate, outputDirectory, outputPath, retryMergedModule).ConfigureAwait(false);
    }

    private static async Task<ProcessedArtifact> CreateOutputArtifactAsync(
        CiRunSummaryAggregate aggregate,
        string outputDirectory,
        string? summaryOutputPath,
        CiRunSummaryModule? retryMergedModule)
    {
        if (aggregate.Context.Mode == ArtifactPostProcessingMode.RetryAttempts)
        {
            string fragmentPath = await CiRunSummaryAggregation.WriteFragmentAsync(
                outputDirectory,
                Provider,
                ProviderSlug,
                retryMergedModule!).ConfigureAwait(false);
            return new ProcessedArtifact(
                fragmentPath,
                FragmentArtifactKind,
                GitHubActionsResources.DisplayName,
                GitHubActionsResources.Description);
        }

        return new ProcessedArtifact(
            summaryOutputPath!,
            SummaryArtifactKind,
            GitHubActionsResources.DisplayName,
            GitHubActionsResources.Description);
    }

    private static CiRunSummaryModule CreateRetryMergedModule(CiRunSummaryAggregate aggregate)
    {
        CiRunSummaryModule first = aggregate.Modules[0];
        CiRunSummaryModule last = aggregate.Modules[^1];
        return new CiRunSummaryModule
        {
            AssemblyName = first.AssemblyName,
            ModulePath = first.ModulePath,
            TargetFramework = first.TargetFramework,
            Architecture = first.Architecture,
            ExecutionId = first.ExecutionId,
            SessionUid = last.SessionUid,
            RequestedOutputPath = first.RequestedOutputPath,
            WriteOnFailureOnly = aggregate.Modules.All(static module => module.WriteOnFailureOnly),
            GitHubActionsStepSummaryEnabled = aggregate.Modules.Any(static module => module.GitHubActionsStepSummaryEnabled is not false),
            GitHubActionsHistoryPath = GetConsistentHistoryPath(aggregate.Modules),
            GitHubActionsHistoryWindowInDays = GetConsistentHistoryWindow(aggregate.Modules),
            AttemptNumber = 1,
            ExitCode = aggregate.ExitCode ?? last.ExitCode,
            TotalTests = aggregate.TotalTests,
            PassedTests = aggregate.PassedTests,
            FailedTests = aggregate.FailedTests,
            SkippedTests = aggregate.SkippedTests,
            TestDurationTicks = aggregate.Modules.Sum(static module => module.TestDurationTicks),
            Failures = last.Failures,
            FlakyTests = [.. aggregate.FlakyTests],
            SlowestTests =
            [
                .. aggregate.Modules
                    .SelectMany(static module => module.SlowestTests)
                    .GroupBy(static test => test.FullyQualifiedName, StringComparer.Ordinal)
                    .Select(static group => group.OrderByDescending(test => test.DurationTicks).First())
                    .OrderByDescending(static test => test.DurationTicks)
                    .ThenBy(static test => test.FullyQualifiedName, StringComparer.Ordinal)
                    .Take(MaxSlowestTests),
            ],
            HistoryTests = MergeRetryHistoryTests(aggregate.Modules),
            TopFailingClasses = last.TopFailingClasses,
            Coverage = aggregate.Coverage,
            GitHubActionsStepSummarySections = GitHubActionsStepSummarySectionsParser.ToPersistedValues(
                GitHubActionsStepSummarySectionsParser.GetAggregateSections(aggregate.Modules)),
        };
    }

    private static CiRunSummaryModule CreateRetryHistoryModule(IReadOnlyList<CiRunSummaryModule> modules)
    {
        CiRunSummaryModule first = modules[0];
        CiRunSummaryModule last = modules[^1];
        return new CiRunSummaryModule
        {
            AssemblyName = first.AssemblyName,
            ModulePath = first.ModulePath,
            TargetFramework = first.TargetFramework,
            Architecture = first.Architecture,
            ExecutionId = first.ExecutionId,
            SessionUid = last.SessionUid,
            GitHubActionsHistoryPath = GetConsistentHistoryPath(modules),
            GitHubActionsHistoryWindowInDays = GetConsistentHistoryWindow(modules),
            HistoryTests = MergeRetryHistoryTests(modules),
        };
    }

    private static string? GetConsistentHistoryPath(IReadOnlyList<CiRunSummaryModule> modules)
    {
        string[] paths =
        [
            .. modules
                .Select(static module => module.GitHubActionsHistoryPath)
                .OfType<string>()
                .Distinct(StringComparer.Ordinal),
        ];
        return paths is [string path] ? path : null;
    }

    private static int GetConsistentHistoryWindow(IReadOnlyList<CiRunSummaryModule> modules)
        => modules
            .Where(static module => module.GitHubActionsHistoryPath is not null)
            .Select(static module => module.GitHubActionsHistoryWindowInDays)
            .DefaultIfEmpty()
            .Max();

    private static CiRunSummaryHistoryTest[] MergeRetryHistoryTests(IReadOnlyList<CiRunSummaryModule> modules)
    {
        var latestByTest = new Dictionary<(string TestId, string FullyQualifiedName, string DisplayName), CiRunSummaryHistoryTest>();
        var previouslyFailed = new HashSet<(string TestId, string FullyQualifiedName, string DisplayName)>();
        foreach (CiRunSummaryModule module in modules.OrderBy(static module => module.AttemptNumber))
        {
            foreach (CiRunSummaryHistoryTest test in module.HistoryTests)
            {
                (string TestId, string FullyQualifiedName, string DisplayName) identity =
                    (test.TestId, test.FullyQualifiedName, test.DisplayName);
                if (latestByTest.Count >= GitHubActionsHistoryStore.MaxTotalSamples && !latestByTest.ContainsKey(identity))
                {
                    continue;
                }

                bool recovered = test.Outcome == GitHubActionsHistoryOutcome.Passed && previouslyFailed.Contains(identity);
                latestByTest[identity] = new CiRunSummaryHistoryTest
                {
                    TestId = test.TestId,
                    DisplayName = test.DisplayName,
                    FullyQualifiedName = test.FullyQualifiedName,
                    Outcome = test.Outcome,
                    DurationTicks = test.DurationTicks,
                    IsFlaky = test.IsFlaky || recovered,
                };

                if (test.Outcome == GitHubActionsHistoryOutcome.Failed)
                {
                    previouslyFailed.Add(identity);
                }
            }
        }

        return
        [
            .. latestByTest.Values
                .OrderBy(static test => test.FullyQualifiedName, StringComparer.Ordinal)
                .ThenBy(static test => test.DisplayName, StringComparer.Ordinal)
                .ThenBy(static test => test.TestId, StringComparer.Ordinal),
        ];
    }

    private static async Task<bool?> TryUpsertStepSummaryAsync(
        StepSummaryWriter writer,
        string aggregationId,
        string markdown,
        Func<int, string>? leadingNotice,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        try
        {
            return await writer.UpsertStepSummaryWithRetryAsync(
                aggregationId,
                markdown,
                cancellationToken,
                leadingNotice,
                GitHubActionsFailureDetails.EffectiveStepSummaryLimit).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            if (logger.IsEnabled(LogLevel.Warning))
            {
                await logger.LogWarningAsync(string.Format(CultureInfo.InvariantCulture, GitHubActionsResources.StepSummaryWriteFailedWarning, writer.Path, ex.Message)).ConfigureAwait(false);
            }

            return null;
        }
    }
}

#pragma warning restore RS0051
