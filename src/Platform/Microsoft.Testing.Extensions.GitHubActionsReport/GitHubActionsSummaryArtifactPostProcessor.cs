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
        string markdown = GitHubActionsSummaryReporter.BuildAggregateMarkdown(aggregate);
        string outputPath = CiRunSummaryAggregation.GetMergedOutputPath(outputDirectory, ProviderSlug, aggregationId);
        await CiRunSummaryAggregation.WriteOutputAsync(outputPath, markdown).ConfigureAwait(false);

        string? stepSummaryPath = environment.GetEnvironmentVariable(StepSummaryEnvironmentVariable);
        bool writeOnFailureOnly = aggregate.Modules.Count > 0
            && aggregate.Modules.All(static module => module.WriteOnFailureOnly);
        bool runFailed = aggregate.IsPartial
            || aggregate.FailedTests > 0
            || (aggregate.ExitCode is int exitCode
                ? GitHubActionsExitCode.IndicatesFailure(exitCode)
                : aggregate.Modules.Any(static module => GitHubActionsExitCode.IndicatesFailure(module.ExitCode)));
        if (!RoslynString.IsNullOrWhiteSpace(stepSummaryPath)
            && (!writeOnFailureOnly || runFailed))
        {
            await GitHubActionsSummaryReporter.UpsertStepSummaryWithRetryAsync(
                fileSystem,
                stepSummaryPath!,
                aggregationId,
                markdown,
                StepSummaryMaxWriteAttempts,
                StepSummaryRetryDelay,
                cancellationToken).ConfigureAwait(false);
        }

        return new ProcessedArtifact(
            outputPath,
            SummaryArtifactKind,
            GitHubActionsResources.DisplayName,
            GitHubActionsResources.Description);
    }
}

#pragma warning restore RS0051
