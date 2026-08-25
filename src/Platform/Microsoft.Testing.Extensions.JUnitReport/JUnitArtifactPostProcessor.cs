// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Security.Cryptography;

using Microsoft.Testing.Extensions.JUnitReport.Resources;
using Microsoft.Testing.Platform.Extensions.ArtifactPostProcessing;
using Microsoft.Testing.Platform.Helpers;

namespace Microsoft.Testing.Extensions.JUnitReport;

internal sealed class JUnitArtifactPostProcessor : IArtifactPostProcessor
{
    private const string MergedReportDirectoryName = "merged";

    private static readonly string[] SupportedArtifactKinds = [JUnitReportGenerator.JUnitArtifactKind];
    private static readonly ArtifactPostProcessingMode[] SupportedPostProcessingModes =
        [ArtifactPostProcessingMode.TestModules, ArtifactPostProcessingMode.RetryAttempts];

    public string Uid => "Microsoft.Testing.Extensions.JUnitReport.PostProcessor";

    public string Version => ExtensionVersion.DefaultSemVer;

    public string DisplayName => ExtensionResources.JUnitArtifactPostProcessorDisplayName;

    public string Description => ExtensionResources.JUnitArtifactPostProcessorDescription;

    public IReadOnlyList<ArtifactPostProcessingMode> SupportedModes => SupportedPostProcessingModes;

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

        InputArtifact[] orderedInputs = context.Mode == ArtifactPostProcessingMode.RetryAttempts
            ? [.. inputs]
            : [.. ArtifactPostProcessingHelper.OrderInputs(inputs, includeModuleMetadata: true)];

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

        string mergeId = CreateMergeIdFromOrderedInputs(orderedInputs, context.Mode);
        string outputPath = Path.Combine(mergedDirectory, $"merged-{mergeId}.xml");
        JUnitMergeMode mergeMode = context.Mode == ArtifactPostProcessingMode.RetryAttempts
            ? JUnitMergeMode.CollapseRetryAttempts
            : JUnitMergeMode.Concatenate;
        await JUnitReportMerger.MergeToFileAsync(
            [.. orderedInputs.Select(input => input.Path)],
            outputPath,
            ExtensionResources.JUnitMergedReportName,
            mergeMode,
            cancellationToken).ConfigureAwait(false);

        return new ProcessedArtifact(
            outputPath,
            JUnitReportGenerator.JUnitArtifactKind,
            ExtensionResources.JUnitMergedArtifactDisplayName,
            string.Format(CultureInfo.CurrentCulture, ExtensionResources.JUnitMergedArtifactDescription, inputs.Count));
    }

    internal static string CreateMergeId(IReadOnlyList<InputArtifact> inputs)
        => CreateMergeIdFromOrderedInputs(
            ArtifactPostProcessingHelper.OrderInputs(inputs, includeModuleMetadata: true),
            ArtifactPostProcessingMode.TestModules);

    private static string CreateMergeIdFromOrderedInputs(
        IEnumerable<InputArtifact> orderedInputs,
        ArtifactPostProcessingMode mode)
    {
        var identity = new StringBuilder(mode.ToString());
        foreach (InputArtifact input in orderedInputs)
        {
            IdentityKeyBuilder.AppendLengthPrefixedComponent(identity, Path.GetFullPath(input.Path));
            IdentityKeyBuilder.AppendLengthPrefixedComponent(identity, input.ProducingTestModule);
            IdentityKeyBuilder.AppendLengthPrefixedComponent(identity, input.TargetFramework);
            IdentityKeyBuilder.AppendLengthPrefixedComponent(identity, input.Architecture);
            IdentityKeyBuilder.AppendLengthPrefixedComponent(identity, input.ExecutionId);
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
}
