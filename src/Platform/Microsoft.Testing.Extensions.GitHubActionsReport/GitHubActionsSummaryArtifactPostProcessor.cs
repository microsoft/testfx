// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.GitHubActionsReport.Resources;
using Microsoft.Testing.Platform;
using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Extensions.ArtifactPostProcessing;
using Microsoft.Testing.Platform.Helpers;

namespace Microsoft.Testing.Extensions.GitHubActionsReport;

#pragma warning disable RS0051 // Internal processor implementation is not a compatibility contract.

internal sealed class GitHubActionsSummaryArtifactPostProcessor(
    ICommandLineOptions commandLineOptions,
    IEnvironment environment,
    IFileSystem fileSystem)
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
        string markdown = GitHubActionsSummaryReporter.BuildAggregateMarkdown(aggregate, _includeFailureDetails, out int modulesWithOmittedDetails, out int condensedModules);
        string outputPath = CiRunSummaryAggregation.GetMergedOutputPath(outputDirectory, ProviderSlug, aggregationId);
        await CiRunSummaryAggregation.WriteOutputAsync(outputPath, markdown).ConfigureAwait(false);

        // Losing whole project sections is a different loss from losing their diagnostics, so say whichever
        // actually happened. Condensing implies the budget was already exhausted, so it takes precedence.
        string? leadingNotice = condensedModules > 0
            ? GitHubActionsSummaryReporter.BuildTruncationNotice(aggregate.Modules.Count - condensedModules)
            : modulesWithOmittedDetails > 0
                ? GitHubActionsSummaryReporter.BuildAggregateTruncationNotice(modulesWithOmittedDetails, aggregate.Modules.Count)
                : null;

        string? stepSummaryPath = environment.GetEnvironmentVariable(StepSummaryEnvironmentVariable);
        if (!RoslynString.IsNullOrWhiteSpace(stepSummaryPath))
        {
            // The step summary is shared with every other step in the job, so the rendered file can exceed
            // GitHub's cap even though this section was budgeted. The writer refuses rather than replacing the
            // file with one GitHub would discard in full; fall back to a verdict line per module, which is the
            // smallest report that still names every test project.
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
                string condensed = GitHubActionsSummaryReporter.BuildAggregateMarkdown(aggregate, includeFailureDetails: false, out _, out _, condenseAllModules: true);
                await GitHubActionsSummaryReporter.UpsertStepSummaryWithRetryAsync(
                    fileSystem,
                    stepSummaryPath!,
                    aggregationId,
                    condensed,
                    StepSummaryMaxWriteAttempts,
                    StepSummaryRetryDelay,
                    cancellationToken,
                    GitHubActionsSummaryReporter.BuildTruncationNotice(0),
                    GitHubActionsFailureDetails.EffectiveStepSummaryLimit).ConfigureAwait(false);
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
