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
    ILoggerFactory loggerFactory)
    : IArtifactPostProcessorRequiresPostProcessing
{
    internal const string FragmentArtifactKind = "microsoft.testing.github-actions-summary-fragment";
    internal const string SummaryArtifactKind = "microsoft.testing.github-actions-summary";
    internal const string Provider = "github-actions";
    internal const string ProviderSlug = "github-actions";

    private const string StepSummaryEnvironmentVariable = "GITHUB_STEP_SUMMARY";
    private const int StepSummaryMaxWriteAttempts = 20;
    private static readonly string[] SupportedArtifactKinds = [FragmentArtifactKind];
    private static readonly ArtifactPostProcessingMode[] SupportedPostProcessingModes = [ArtifactPostProcessingMode.TestModules];
    private static readonly TimeSpan StepSummaryRetryDelay = TimeSpan.FromMilliseconds(50);
    private readonly bool _isEnabled =
        GitHubActionsFeature.IsEnabled(commandLineOptions, environment, GitHubActionsCommandLineOptions.GitHubActionsStepSummary)
        || commandLineOptions.IsOptionSet(ArtifactPostProcessingDispatcherToolCommandLine.ManifestOptionName);

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

        CiRunSummaryAggregate aggregate = CiRunSummaryAggregation.ReadAndAggregate(inputs, Provider, context);
        string aggregationId = CiRunSummaryAggregation.CreateAggregationId(inputs);
        string markdown = GitHubActionsSummaryReporter.BuildAggregateMarkdown(aggregate, _includeFailureDetails, out int modulesWithOmittedDetails, out int condensedModules, out int unlistedModules);
        string outputPath = CiRunSummaryAggregation.GetMergedOutputPath(outputDirectory, ProviderSlug, aggregationId);
        await CiRunSummaryAggregation.WriteOutputAsync(outputPath, markdown).ConfigureAwait(false);

        // Losing whole project sections is a different loss from losing their diagnostics, so say whichever
        // actually happened. Shortening implies the budget was already exhausted, so it takes precedence. The
        // count is of projects that got a *full* section, so it excludes the ones reduced to a verdict line and
        // the ones that did not fit at all.
        string? leadingNotice = condensedModules > 0 || unlistedModules > 0
            ? GitHubActionsSummaryReporter.BuildTruncationNotice(aggregate.Modules.Count - condensedModules - unlistedModules)
            : modulesWithOmittedDetails > 0
                ? GitHubActionsSummaryReporter.BuildAggregateTruncationNotice(modulesWithOmittedDetails, aggregate.Modules.Count)
                : null;

        string? stepSummaryPath = environment.GetEnvironmentVariable(StepSummaryEnvironmentVariable);
        if (!RoslynString.IsNullOrWhiteSpace(stepSummaryPath))
        {
            // The step summary is shared with every other step in the job, so the rendered file can exceed
            // GitHub's cap even though this section was budgeted. The writer refuses rather than replacing the
            // file with one GitHub would discard in full; fall back to a verdict line per test project, which is
            // the smallest report this can produce.
            if (!await GitHubActionsSummaryReporter.UpsertStepSummaryWithRetryAsync(
                fileSystem,
                stepSummaryPath!,
                aggregationId,
                markdown,
                StepSummaryMaxWriteAttempts,
                StepSummaryRetryDelay,
                cancellationToken,
                leadingNotice,
                GitHubActionsFailureDetails.EffectiveStepSummaryLimit).ConfigureAwait(false))
            {
                // Every listed module is a verdict line in this rendering, so no test project has a full section.
                string condensed = GitHubActionsSummaryReporter.BuildAggregateMarkdown(aggregate, includeFailureDetails: false, out _, out _, out _, condenseAllModules: true);
                if (!await GitHubActionsSummaryReporter.UpsertStepSummaryWithRetryAsync(
                    fileSystem,
                    stepSummaryPath!,
                    aggregationId,
                    condensed,
                    StepSummaryMaxWriteAttempts,
                    StepSummaryRetryDelay,
                    cancellationToken,
                    GitHubActionsSummaryReporter.BuildTruncationNotice(0),
                    GitHubActionsFailureDetails.EffectiveStepSummaryLimit).ConfigureAwait(false))
                {
                    // Both renderings were refused, so this run contributed nothing to the job summary. Saying so
                    // is the whole point of the refusal: the alternative is a summary that is silently missing a
                    // section, indistinguishable from a run that produced none.
                    ILogger logger = loggerFactory.CreateLogger<GitHubActionsSummaryArtifactPostProcessor>();
                    if (logger.IsEnabled(LogLevel.Warning))
                    {
                        logger.LogWarning(string.Format(
                            CultureInfo.InvariantCulture,
                            GitHubActionsResources.StepSummaryLimitExceededWarning,
                            GetSummaryLength(stepSummaryPath!).ToString(CultureInfo.InvariantCulture),
                            GitHubActionsFailureDetails.EffectiveStepSummaryLimit.ToString(CultureInfo.InvariantCulture)));
                    }
                }
            }
        }

        return new ProcessedArtifact(
            outputPath,
            SummaryArtifactKind,
            GitHubActionsResources.DisplayName,
            GitHubActionsResources.Description);
    }

    /// <summary>
    /// Measures the shared summary file so the warning can quote its actual size. Best-effort: the size is only
    /// there to explain the refusal, so a file that cannot be measured reports zero rather than failing the run.
    /// </summary>
    private long GetSummaryLength(string path)
    {
        try
        {
            if (!fileSystem.ExistFile(path))
            {
                return 0;
            }

            using IFileStream stream = fileSystem.NewFileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            return stream.Stream.Length;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            return 0;
        }
    }
}

#pragma warning restore RS0051
