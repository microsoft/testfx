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

    public string Uid => "Microsoft.Testing.Extensions.CtrfReport.PostProcessor";

    public string Version => ExtensionVersion.DefaultSemVer;

    public string DisplayName => ExtensionResources.CtrfArtifactPostProcessorDisplayName;

    public string Description => ExtensionResources.CtrfArtifactPostProcessorDescription;

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

        InputArtifact[] orderedInputs =
        [
            .. inputs
                .OrderBy(input => Path.GetFullPath(input.Path), StringComparer.Ordinal)
                .ThenBy(input => input.ExecutionId, StringComparer.Ordinal),
        ];
        string[] inputPaths = [.. orderedInputs.Select(input => input.Path)];
        Guid artifactId = CreateArtifactId(orderedInputs);
        string mergedDirectory = Path.Combine(outputDirectory, MergedReportDirectoryName);
        Directory.CreateDirectory(mergedDirectory);
        if (IsReparsePoint(mergedDirectory))
        {
            return null;
        }

        string outputPath = Path.Combine(mergedDirectory, $"merged-{artifactId:N}.ctrf.json");
        await CtrfReportMerger.MergeAllToFileAsync(inputPaths, outputPath, cancellationToken).ConfigureAwait(false);

        return new ProcessedArtifact(
            outputPath,
            CtrfReportGenerator.CtrfArtifactKind,
            ExtensionResources.CtrfMergedArtifactDisplayName,
            string.Format(CultureInfo.CurrentCulture, ExtensionResources.CtrfMergedArtifactDescription, inputs.Count));
    }

    private static Guid CreateArtifactId(IReadOnlyList<InputArtifact> inputs)
    {
        const ulong fnvPrime = 1099511628211UL;
        ulong hashLow = 14695981039346656037UL;
        ulong hashHigh = 0x9E3779B97F4A7C15UL;

        foreach (InputArtifact input in inputs)
        {
            string value = $"{Path.GetFullPath(input.Path)}\0{input.ExecutionId}";
            foreach (char c in value)
            {
                hashLow = (hashLow ^ c) * fnvPrime;
                hashHigh = (hashHigh ^ c) * fnvPrime;
            }

            hashLow = (hashLow ^ (ulong)value.Length) * fnvPrime;
            hashHigh = (hashHigh ^ ((ulong)value.Length + 1UL)) * fnvPrime;
        }

        byte[] bytes = new byte[16];
        for (int i = 0; i < 8; i++)
        {
            bytes[i] = (byte)(hashLow >> (i * 8));
            bytes[i + 8] = (byte)(hashHigh >> (i * 8));
        }

        return new Guid(bytes);
    }

    private static bool IsReparsePoint(string path)
    {
        try
        {
            return (File.GetAttributes(path) & FileAttributes.ReparsePoint) == FileAttributes.ReparsePoint;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return true;
        }
    }
}
