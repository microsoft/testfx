// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under dual-license. See LICENSE.PLATFORMTOOLS.txt file in the project root for full license information.

using Microsoft.Testing.Extensions.Policy.Resources;
using Microsoft.Testing.Platform.Extensions.ArtifactPostProcessing;
using Microsoft.Testing.Platform.Extensions.OutputDevice;
using Microsoft.Testing.Platform.Extensions.RetryFailedTests.Serializers;
using Microsoft.Testing.Platform.Helpers;
using Microsoft.Testing.Platform.Logging;
using Microsoft.Testing.Platform.OutputDevice;
using Microsoft.Testing.Platform.Services;

namespace Microsoft.Testing.Extensions.Policy;

internal static class RetryArtifactProcessor
{
    public static IReadOnlyList<RetryAttemptArtifact> SnapshotAttemptArtifacts(
        IFileSystem fileSystem,
        IReadOnlyList<ArtifactRequest> artifacts,
        int attempt,
        string attemptDirectory,
        string retryRootDirectory)
    {
        var captured = new List<RetryAttemptArtifact>(artifacts.Count);
        string snapshotDirectory = Path.Combine(
            retryRootDirectory,
            "Artifacts",
            attempt.ToString(CultureInfo.InvariantCulture));
        bool snapshotDirectoryCreated = false;

        for (int i = 0; i < artifacts.Count; i++)
        {
            ArtifactRequest artifact = artifacts[i];
            string artifactPath = Path.GetFullPath(artifact.Path);
            if (IsUnderDirectory(artifactPath, attemptDirectory))
            {
                captured.Add(new RetryAttemptArtifact(artifactPath, artifact.Kind, attempt, destinationPath: null));
                continue;
            }

            if (!snapshotDirectoryCreated)
            {
                fileSystem.CreateDirectory(snapshotDirectory);
                snapshotDirectoryCreated = true;
            }

            string fileName = Path.GetFileName(artifactPath);
            string snapshotPath = Path.GetFullPath(Path.Combine(
                snapshotDirectory,
                $"{i.ToString("D4", CultureInfo.InvariantCulture)}-{fileName}"));
            fileSystem.CopyFile(artifactPath, snapshotPath, overwrite: true);
            captured.Add(new RetryAttemptArtifact(snapshotPath, artifact.Kind, attempt, artifactPath));
        }

        return captured;
    }

    public static async Task<IReadOnlyDictionary<string, string>> ProcessAsync(
        IServiceProvider serviceProvider,
        IOutputDeviceDataProducer producer,
        IOutputDevice outputDevice,
        ILogger logger,
        IReadOnlyList<RetryAttemptArtifact> artifacts,
        int attemptCount,
        string outputDirectory,
        CancellationToken cancellationToken)
        => await ProcessAsync(
            serviceProvider,
            producer,
            outputDevice,
            logger,
            artifacts,
            attemptCount,
            runSummary: null,
            outputDirectory,
            cancellationToken).ConfigureAwait(false);

    public static async Task<IReadOnlyDictionary<string, string>> ProcessAsync(
        IServiceProvider serviceProvider,
        IOutputDeviceDataProducer producer,
        IOutputDevice outputDevice,
        ILogger logger,
        IReadOnlyList<RetryAttemptArtifact> artifacts,
        int attemptCount,
        ArtifactPostProcessingRunSummary? runSummary,
        string outputDirectory,
        CancellationToken cancellationToken)
    {
        IArtifactPostProcessor[] processors =
        [
            .. serviceProvider.GetServicesInternal<IArtifactPostProcessor>()
                .Where(processor => processor.SupportedModes.Contains(ArtifactPostProcessingMode.RetryAttempts)),
        ];
        if (processors.Length == 0)
        {
            return new Dictionary<string, string>();
        }

        List<RetryAttemptArtifact> unmatchedArtifacts = [.. artifacts];
        var replacements = new Dictionary<string, string>(GetPathComparer());
        var context = new ArtifactPostProcessingContext(
            ArtifactPostProcessingTruncationReason.None,
            ArtifactPostProcessingMode.RetryAttempts,
            runSummary);

        foreach (IArtifactPostProcessor processor in processors)
        {
            if (attemptCount < 2 && processor is not IArtifactPostProcessorRequiresPostProcessing)
            {
                continue;
            }

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
                await logger.LogWarningAsync($"Retry artifact post-processor '{processor.Uid}' failed: {ex}").ConfigureAwait(false);
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

    public static void PublishExternalArtifacts(
        IFileSystem fileSystem,
        IReadOnlyList<RetryAttemptArtifact> artifacts,
        int finalAttempt,
        IReadOnlyDictionary<string, string> replacements)
    {
        foreach (RetryAttemptArtifact artifact in artifacts)
        {
            if (artifact.Attempt != finalAttempt || artifact.DestinationPath is null)
            {
                continue;
            }

            string sourcePath = replacements.TryGetValue(artifact.Path, out string? replacement)
                ? replacement
                : artifact.Path;
            string? destinationDirectory = Path.GetDirectoryName(artifact.DestinationPath);
            if (destinationDirectory is not null)
            {
                fileSystem.CreateDirectory(destinationDirectory);
            }

            fileSystem.CopyFile(sourcePath, artifact.DestinationPath, overwrite: true);
        }
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

    private static bool IsUnderDirectory(string path, string directory)
    {
        string directoryPrefix = Path.GetFullPath(directory)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            + Path.DirectorySeparatorChar;
        StringComparison comparison = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        return path.StartsWith(directoryPrefix, comparison);
    }
}

internal sealed class RetryAttemptArtifact(string path, string? kind, int attempt, string? destinationPath)
{
    public string Path { get; } = path;

    public string? Kind { get; } = kind;

    public int Attempt { get; } = attempt;

    public string? DestinationPath { get; } = destinationPath;
}
