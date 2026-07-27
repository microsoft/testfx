// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.TrxReport.Resources;
using Microsoft.Testing.Platform.Extensions.ArtifactPostProcessing;

namespace Microsoft.Testing.Extensions.TrxReport.Abstractions;

internal sealed class TrxArtifactPostProcessor : IArtifactPostProcessor
{
    /// <summary>
    /// Subdirectory of the orchestrator-provided output directory that receives the merged report and the
    /// attachment deployment root written beside it.
    /// </summary>
    private const string MergedReportDirectoryName = "merged";

    private static readonly string[] SupportedArtifactKinds = [TrxReportEngine.TrxArtifactKind];
    private static readonly string[] SupportedExtensions = [".trx"];

    public string Uid => "Microsoft.Testing.Extensions.TrxReport.PostProcessor";

    public string Version => ExtensionVersion.DefaultSemVer;

    public string DisplayName => ExtensionResources.TrxArtifactPostProcessorDisplayName;

    public string Description => ExtensionResources.TrxArtifactPostProcessorDescription;

    public IReadOnlyList<string> SupportedKinds => SupportedArtifactKinds;

    public IReadOnlyList<string> SupportedFileExtensionsFallback => SupportedExtensions;

    public Task<bool> IsEnabledAsync() => Task.FromResult(true);

    public async Task<ProcessedArtifact?> ProcessAsync(
        IReadOnlyList<InputArtifact> inputs,
        string outputDirectory,
        CancellationToken cancellationToken)
    {
        if (inputs.Count < 2)
        {
            return null;
        }

        InputArtifact[] orderedInputs =
        [
            .. inputs
                .OrderBy(input => Path.GetFullPath(input.Path), StringComparer.Ordinal)
                .ThenBy(input => input.ExecutionId, StringComparer.Ordinal),
        ];
        string[] inputPaths = [.. orderedInputs.Select(input => input.Path)];
        Guid runId = TrxReportEngine.CreateMergeRunId(inputPaths, [.. orderedInputs.Select(input => input.ExecutionId)]);
        // Nest the merged report in its own subdirectory instead of writing it as a sibling of the inputs.
        // Orchestrators point outputDirectory at the run's results directory, which already holds the
        // per-module reports, and the usual way those get consumed is a non-recursive '*.trx' glob (Arcade's
        // "Publish TRX Test Results" step, the PublishTestResults@2 defaults, ReportGenerator setups, ...).
        // A sibling merged report would be picked up alongside its own inputs and double-count every test.
        // Nesting keeps it out of those globs by construction, and because MergeToFileAsync derives the
        // attachment deployment root from this path, the attachment tree follows the report into the
        // subdirectory and the relative runDeploymentRoot recorded in the TRX keeps resolving.
        string outputPath = Path.Combine(outputDirectory, MergedReportDirectoryName, $"merged-{runId:N}.trx");
        await TrxReportEngine.MergeToFileAsync(
            inputPaths,
            outputPath,
            runId,
            ExtensionResources.TrxMergedRunName,
            cancellationToken).ConfigureAwait(false);

        return new ProcessedArtifact(
            outputPath,
            TrxReportEngine.TrxArtifactKind,
            ExtensionResources.TrxMergedArtifactDisplayName,
            string.Format(CultureInfo.CurrentCulture, ExtensionResources.TrxMergedArtifactDescription, inputs.Count));
    }
}
