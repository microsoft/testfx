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
        string outputPath = CiRunSummaryAggregation.GetMergedOutputPath(outputDirectory, ProviderSlug, aggregationId);
        string? stepSummaryPath = environment.GetEnvironmentVariable(StepSummaryEnvironmentVariable);
        ILogger logger = loggerFactory.CreateLogger<GitHubActionsSummaryArtifactPostProcessor>();
        StepSummaryWriter? writer = RoslynString.IsNullOrWhiteSpace(stepSummaryPath)
            ? null
            : new StepSummaryWriter(fileSystem, stepSummaryPath!, logger, StepSummaryMaxWriteAttempts, StepSummaryRetryDelay);

        // Bounding this rendering against its own size alone would be useless: it is appended to a file other
        // steps also write to, and it is that file GitHub measures. Seeding the budget with what is already there
        // is what makes the degradation actually bound the artifact GitHub sees.
        long alreadyWritten = writer?.GetSummaryLength() ?? 0;

        GitHubActionsSummaryReporter.AggregateRenderResult rendered = GitHubActionsSummaryReporter.BuildAggregateMarkdown(
            aggregate,
            _includeFailureDetails,
            condenseAllModules: false,
            alreadyWritten);

        await CiRunSummaryAggregation.WriteOutputAsync(outputPath, rendered.Markdown).ConfigureAwait(false);

        // Losing whole project sections is a different loss from losing their diagnostics, so say whichever
        // actually happened. Shortening implies the budget was already exhausted, so it takes precedence.
        string? leadingNotice = rendered.CondensedModules > 0 || rendered.UnlistedModules > 0
            ? GitHubActionsSummaryReporter.BuildTruncationNotice(rendered.FullyReportedModules(aggregate.Modules.Count))
            : rendered.ModulesWithOmittedDetails > 0
                ? GitHubActionsSummaryReporter.BuildAggregateTruncationNotice(rendered.ModulesWithOmittedDetails, aggregate.Modules.Count)
                : null;

        if (writer is not null)
        {
            // The step summary is shared with every other step in the job, so the rendered file can exceed
            // GitHub's cap even though this section was budgeted. The writer refuses rather than replacing the
            // file with one GitHub would discard in full; fall back to a verdict line per test project, which is
            // the smallest report this can produce.
            if (!await writer.UpsertStepSummaryWithRetryAsync(
                aggregationId,
                rendered.Markdown,
                cancellationToken,
                leadingNotice,
                GitHubActionsFailureDetails.EffectiveStepSummaryLimit).ConfigureAwait(false))
            {
                // Every listed module is a verdict line in this rendering, so no test project has a full section.
                GitHubActionsSummaryReporter.AggregateRenderResult condensed = GitHubActionsSummaryReporter.BuildAggregateMarkdown(
                    aggregate,
                    includeFailureDetails: false,
                    condenseAllModules: true,
                    alreadyWritten);

                if (!await writer.UpsertStepSummaryWithRetryAsync(
                    aggregationId,
                    condensed.Markdown,
                    cancellationToken,
                    GitHubActionsSummaryReporter.BuildTruncationNotice(0),
                    GitHubActionsFailureDetails.EffectiveStepSummaryLimit).ConfigureAwait(false))
                {
                    // Both renderings were refused, so this run contributed nothing to the job summary. Saying so
                    // is the whole point of the refusal: the alternative is a summary that is silently missing a
                    // section, indistinguishable from a run that produced none.
                    if (logger.IsEnabled(LogLevel.Warning))
                    {
                        logger.LogWarning(string.Format(
                            CultureInfo.InvariantCulture,
                            GitHubActionsResources.StepSummaryLimitExceededWarning,
                            (writer.GetSummaryLength() ?? 0).ToString(CultureInfo.InvariantCulture),
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
}

#pragma warning restore RS0051
