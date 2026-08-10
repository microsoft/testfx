// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under dual-license. See LICENSE.PLATFORMTOOLS.txt file in the project root for full license information.

using Microsoft.Testing.Extensions.Policy.Resources;
using Microsoft.Testing.Platform.Extensions.ArtifactPostProcessing;
using Microsoft.Testing.Platform.Extensions.OutputDevice;
using Microsoft.Testing.Platform.Logging;
using Microsoft.Testing.Platform.OutputDevice;
using Microsoft.Testing.Platform.Services;

namespace Microsoft.Testing.Extensions.Policy;

internal static class RetryArtifactProcessor
{
    public static async Task<IReadOnlyDictionary<string, string>> ProcessAsync(
        IServiceProvider serviceProvider,
        IOutputDeviceDataProducer producer,
        IOutputDevice outputDevice,
        ILogger logger,
        IReadOnlyList<RetryAttemptArtifact> artifacts,
        int attemptCount,
        string outputDirectory,
        CancellationToken cancellationToken)
    {
        IArtifactPostProcessor[] processors =
        [
            .. serviceProvider.GetServicesInternal<IArtifactPostProcessor>()
                .Where(processor => processor.SupportedModes.Contains(ArtifactPostProcessingMode.RetryAttempts)),
        ];
        if (processors.Length == 0 || attemptCount < 2)
        {
            return new Dictionary<string, string>();
        }

        List<RetryAttemptArtifact> unmatchedArtifacts = [.. artifacts];
        var replacements = new Dictionary<string, string>(GetPathComparer());
        var context = new ArtifactPostProcessingContext(
            ArtifactPostProcessingTruncationReason.None,
            ArtifactPostProcessingMode.RetryAttempts);

        foreach (IArtifactPostProcessor processor in processors)
        {
            RetryAttemptArtifact[] matchingArtifacts =
            [
                .. unmatchedArtifacts.Where(artifact => Matches(processor, artifact)),
            ];
            if (matchingArtifacts.Length == 0)
            {
                continue;
            }

            unmatchedArtifacts.RemoveAll(artifact => matchingArtifacts.Contains(artifact));

            // Retry merging is only authoritative when every physical attempt contributed exactly one artifact of
            // this kind. Missing an attempt would produce a valid-looking but incomplete logical-run report.
            IGrouping<int, RetryAttemptArtifact>[] artifactsByAttempt =
            [
                .. matchingArtifacts.GroupBy(artifact => artifact.Attempt).OrderBy(group => group.Key),
            ];
            if (artifactsByAttempt.Length != attemptCount
                || artifactsByAttempt.Any(group => group.Count() != 1))
            {
                continue;
            }

            InputArtifact[] inputs =
            [
                .. artifactsByAttempt.Select(group =>
                {
                    RetryAttemptArtifact artifact = group.Single();
                    return new InputArtifact(
                        artifact.Path,
                        artifact.Kind,
                        producingTestModule: null,
                        targetFramework: null,
                        architecture: null,
                        executionId: group.Key.ToString(CultureInfo.InvariantCulture));
                }),
            ];

            try
            {
                ProcessedArtifact? output = await processor.ProcessAsync(
                    inputs,
                    outputDirectory,
                    context,
                    cancellationToken).ConfigureAwait(false);
                if (output is null)
                {
                    continue;
                }

                ProcessedArtifact validatedOutput =
                    ArtifactPostProcessingDispatcherTool.ValidateProcessedArtifact(output, outputDirectory, inputs);
                replacements[matchingArtifacts.Single(artifact => artifact.Attempt == attemptCount).Path] =
                    validatedOutput.Path;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                logger.LogWarning($"Retry artifact post-processor '{processor.Uid}' failed: {ex}");
                await outputDevice.DisplayAsync(
                    producer,
                    new WarningMessageOutputDeviceData(string.Format(
                        CultureInfo.CurrentCulture,
                        ExtensionResources.RetryArtifactPostProcessorFailed,
                        processor.Uid,
                        ex.Message)),
                    cancellationToken).ConfigureAwait(false);
            }
        }

        return replacements;
    }

    private static bool Matches(IArtifactPostProcessor processor, RetryAttemptArtifact artifact)
        => artifact.Kind is not null
            ? processor.SupportedKinds.Contains(artifact.Kind, StringComparer.Ordinal)
            : processor.SupportedFileExtensionsFallback.Contains(
                Path.GetExtension(artifact.Path),
                StringComparer.OrdinalIgnoreCase);

    private static StringComparer GetPathComparer()
        => RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? StringComparer.OrdinalIgnoreCase
            : StringComparer.Ordinal;
}

internal sealed class RetryAttemptArtifact(string path, string? kind, int attempt)
{
    public string Path { get; } = path;

    public string? Kind { get; } = kind;

    public int Attempt { get; } = attempt;
}
