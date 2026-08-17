// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.AzureDevOpsReport.Resources;
using Microsoft.Testing.Extensions.Reporting;
using Microsoft.Testing.Platform;
using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Extensions.ArtifactPostProcessing;
using Microsoft.Testing.Platform.Extensions.OutputDevice;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.OutputDevice;

namespace Microsoft.Testing.Extensions.AzureDevOpsReport;

#pragma warning disable RS0051 // Internal processor implementation is not a compatibility contract.

internal sealed class AzureDevOpsSummaryArtifactPostProcessor(
    ICommandLineOptions commandLineOptions,
    IEnvironment environment,
    IOutputDevice outputDevice)
    : IArtifactPostProcessorRequiresPostProcessing, IOutputDeviceDataProducer
{
    internal const string FragmentArtifactKind = "microsoft.testing.azure-devops-summary-fragment";
    internal const string SummaryArtifactKind = "microsoft.testing.azure-devops-summary";
    internal const string Provider = "azure-devops";
    internal const string ProviderSlug = "azure-devops";

    private static readonly string[] SupportedArtifactKinds = [FragmentArtifactKind];
    private static readonly ArtifactPostProcessingMode[] SupportedPostProcessingModes = [ArtifactPostProcessingMode.TestModules];
    private readonly bool _isEnabled =
        (commandLineOptions.IsOptionSet(AzureDevOpsCommandLineOptions.AzureDevOpsSummary)
            && AzureDevOpsConstants.IsRunningInAzureDevOps(environment))
        || commandLineOptions.IsOptionSet(ArtifactPostProcessingDispatcherToolCommandLine.ManifestOptionName);

    public string Uid => "Microsoft.Testing.Extensions.AzureDevOpsReport.SummaryPostProcessor";

    public string Version => ExtensionVersion.DefaultSemVer;

    public string DisplayName => AzureDevOpsResources.DisplayName;

    public string Description => AzureDevOpsResources.Description;

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
        string markdown = AzureDevOpsSummaryReporter.BuildAggregateMarkdown(aggregate);
        string outputPath = CiRunSummaryAggregation.GetMergedOutputPath(outputDirectory, ProviderSlug, aggregationId);
        await CiRunSummaryAggregation.WriteOutputAsync(outputPath, markdown).ConfigureAwait(false);

        string[] requestedOutputPaths =
        [
            .. aggregate.Modules
                .Select(module => module.RequestedOutputPath)
                .Where(path => !RoslynString.IsNullOrWhiteSpace(path))
                .Distinct(PathComparer),
        ];
        string uploadPath = requestedOutputPaths is [string requestedOutputPath] ? requestedOutputPath : outputPath;
        if (!string.Equals(Path.GetFullPath(uploadPath), Path.GetFullPath(outputPath), PathComparison))
        {
            await CiRunSummaryAggregation.WriteOutputAsync(uploadPath, markdown).ConfigureAwait(false);
        }

        string uploadMarkerPath = outputPath + ".uploaded";
        if (!File.Exists(uploadMarkerPath))
        {
            string line = $"##vso[task.uploadsummary]{AzDoEscaper.Escape(uploadPath)}";
            await outputDevice.DisplayAsync(this, new AzureDevOpsCommandOutputDeviceData(line), cancellationToken).ConfigureAwait(false);
            await CiRunSummaryAggregation.WriteOutputAsync(uploadMarkerPath, aggregationId).ConfigureAwait(false);
        }

        return new ProcessedArtifact(
            outputPath,
            SummaryArtifactKind,
            AzureDevOpsResources.DisplayName,
            AzureDevOpsResources.Description);
    }

    private static StringComparison PathComparison
        => RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

    private static StringComparer PathComparer
        => RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? StringComparer.OrdinalIgnoreCase
            : StringComparer.Ordinal;
}

#pragma warning restore RS0051
