// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Security.Cryptography;

using Microsoft.Testing.Extensions.JUnitReport.Resources;
using Microsoft.Testing.Platform.Extensions.ArtifactPostProcessing;

namespace Microsoft.Testing.Extensions.JUnitReport;

internal sealed class JUnitArtifactPostProcessor : IArtifactPostProcessor
{
    private const string MergedReportDirectoryName = "merged";

    private static readonly string[] SupportedArtifactKinds = [JUnitReportGenerator.JUnitArtifactKind];

    public string Uid => "Microsoft.Testing.Extensions.JUnitReport.PostProcessor";

    public string Version => ExtensionVersion.DefaultSemVer;

    public string DisplayName => ExtensionResources.JUnitArtifactPostProcessorDisplayName;

    public string Description => ExtensionResources.JUnitArtifactPostProcessorDescription;

    public bool SupportsTruncatedRuns => false;

    public IReadOnlyList<string> SupportedKinds => SupportedArtifactKinds;

    // JUnit uses the generic .xml extension, so extension-only matching would claim unrelated XML artifacts.
    public IReadOnlyList<string> SupportedFileExtensionsFallback => [];

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
                .ThenBy(input => input.ProducingTestModule, StringComparer.Ordinal)
                .ThenBy(input => input.TargetFramework, StringComparer.Ordinal)
                .ThenBy(input => input.Architecture, StringComparer.Ordinal)
                .ThenBy(input => input.ExecutionId, StringComparer.Ordinal),
        ];

        string mergedDirectory = Path.Combine(outputDirectory, MergedReportDirectoryName);
        Directory.CreateDirectory(mergedDirectory);
        if (IsReparsePoint(mergedDirectory))
        {
            return null;
        }

        string mergeId = CreateMergeId(orderedInputs);
        string outputPath = Path.Combine(mergedDirectory, $"merged-{mergeId}.xml");
        await JUnitReportMerger.MergeToFileAsync(
            [.. orderedInputs.Select(input => input.Path)],
            outputPath,
            ExtensionResources.JUnitMergedReportName,
            cancellationToken).ConfigureAwait(false);

        return new ProcessedArtifact(
            outputPath,
            JUnitReportGenerator.JUnitArtifactKind,
            ExtensionResources.JUnitMergedArtifactDisplayName,
            string.Format(CultureInfo.CurrentCulture, ExtensionResources.JUnitMergedArtifactDescription, inputs.Count));
    }

    internal static string CreateMergeId(IReadOnlyList<InputArtifact> inputs)
    {
        var identity = new StringBuilder();
        foreach (InputArtifact input in inputs
            .OrderBy(input => Path.GetFullPath(input.Path), StringComparer.Ordinal)
            .ThenBy(input => input.ProducingTestModule, StringComparer.Ordinal)
            .ThenBy(input => input.TargetFramework, StringComparer.Ordinal)
            .ThenBy(input => input.Architecture, StringComparer.Ordinal)
            .ThenBy(input => input.ExecutionId, StringComparer.Ordinal))
        {
            AppendIdentityPart(identity, Path.GetFullPath(input.Path));
            AppendIdentityPart(identity, input.ProducingTestModule);
            AppendIdentityPart(identity, input.TargetFramework);
            AppendIdentityPart(identity, input.Architecture);
            AppendIdentityPart(identity, input.ExecutionId);
        }

        using var sha256 = SHA256.Create();
        byte[] hash = sha256.ComputeHash(Encoding.UTF8.GetBytes(identity.ToString()));
        var result = new StringBuilder(32);
        for (int i = 0; i < 16; i++)
        {
            result.Append(hash[i].ToString("x2", CultureInfo.InvariantCulture));
        }

        return result.ToString();
    }

    private static void AppendIdentityPart(StringBuilder builder, string? value)
    {
        if (value is null)
        {
            builder.Append("-1:");
            return;
        }

        builder.Append(value.Length.ToString(CultureInfo.InvariantCulture));
        builder.Append(':');
        builder.Append(value);
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
