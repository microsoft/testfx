// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Extensions.CtrfReport.Resources;
using Microsoft.Testing.Platform.Extensions.ArtifactPostProcessing;

namespace Microsoft.Testing.Extensions.CtrfReport;

internal sealed class CtrfArtifactPostProcessor : IArtifactPostProcessor
{
    private const string MergedReportDirectoryName = "merged";

    private static readonly string[] SupportedArtifactKinds = [CtrfReportGenerator.CtrfArtifactKind];
    private static readonly string[] NoSupportedExtensions = [];
    private static readonly ArtifactPostProcessingMode[] SupportedPostProcessingModes =
        [ArtifactPostProcessingMode.TestModules, ArtifactPostProcessingMode.RetryAttempts];

    public string Uid => "Microsoft.Testing.Extensions.CtrfReport.PostProcessor";

    public string Version => ExtensionVersion.DefaultSemVer;

    public string DisplayName => ExtensionResources.CtrfArtifactPostProcessorDisplayName;

    public string Description => ExtensionResources.CtrfArtifactPostProcessorDescription;

    public IReadOnlyList<ArtifactPostProcessingMode> SupportedModes => SupportedPostProcessingModes;

    // CTRF has no marker that lets this merger label an incomplete input set as a partial run.
    public bool SupportsTruncatedRuns => false;

    public IReadOnlyList<string> SupportedKinds => SupportedArtifactKinds;

    // JSON is shared by many report formats, so accepting untagged .json artifacts would be unsafe.
    public IReadOnlyList<string> SupportedFileExtensionsFallback => NoSupportedExtensions;

    public Task<bool> IsEnabledAsync() => Task.FromResult(true);

    public async Task<ProcessedArtifact?> ProcessAsync(
        IReadOnlyList<InputArtifact> inputs,
        string outputDirectory,
        ArtifactPostProcessingContext context,
        CancellationToken cancellationToken)
    {
        if (inputs.Count < 2)
        {
            return null;
        }

        InputArtifact[] orderedInputs = context.Mode == ArtifactPostProcessingMode.RetryAttempts
            ? [.. inputs]
            :
            [
                .. ArtifactPostProcessingHelper.OrderInputs(inputs, includeModuleMetadata: false),
            ];
        string[] inputPaths = [.. orderedInputs.Select(input => input.Path)];
        string[] identityInputs =
        [
            context.Mode.ToString(),
            .. orderedInputs.Select(input => $"{Path.GetFullPath(input.Path)}\0{input.ExecutionId}"),
        ];
        Guid artifactId = CtrfReportMerger.CreateDeterministicId(identityInputs);
        string mergedDirectory = Path.Combine(outputDirectory, MergedReportDirectoryName);
        try
        {
            Directory.CreateDirectory(mergedDirectory);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }

        if (ArtifactPostProcessingHelper.IsReparsePoint(mergedDirectory))
        {
            return null;
        }

        string outputPath = Path.Combine(mergedDirectory, $"merged-{artifactId:N}.ctrf.json");
        CtrfMergeMode mergeMode = context.Mode == ArtifactPostProcessingMode.RetryAttempts
            ? CtrfMergeMode.CollapseRetryAttempts
            : CtrfMergeMode.Concatenate;
        await CtrfReportMerger.MergeAllToFileAsync(inputPaths, outputPath, mergeMode, cancellationToken).ConfigureAwait(false);

        return new ProcessedArtifact(
            outputPath,
            CtrfReportGenerator.CtrfArtifactKind,
            ExtensionResources.CtrfMergedArtifactDisplayName,
            string.Format(CultureInfo.CurrentCulture, ExtensionResources.CtrfMergedArtifactDescription, inputs.Count));
    }
}
