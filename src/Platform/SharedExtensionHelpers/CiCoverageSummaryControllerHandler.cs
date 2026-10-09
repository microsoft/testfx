// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text.Json;

using Microsoft.Testing.Platform.CommandLine;
using Microsoft.Testing.Platform.Configurations;
using Microsoft.Testing.Platform.Extensions.ArtifactPostProcessing;
using Microsoft.Testing.Platform.Extensions.Messages;
using Microsoft.Testing.Platform.Extensions.OutputDevice;
using Microsoft.Testing.Platform.Logging;
using Microsoft.Testing.Platform.OutputDevice;
using Microsoft.Testing.Platform.Services;
using Microsoft.Testing.Platform.TestHostControllers;

namespace Microsoft.Testing.Extensions;

#pragma warning disable RS0051 // Shared implementation details are compiled into multiple extension assemblies.

internal sealed class CiCoverageSummaryControllerHandler(
    string provider,
    string fragmentKind,
    ICommandLineOptions commandLineOptions,
    IConfiguration configuration,
    ITestCoverageResult coverageResult,
    IOutputDevice outputDevice,
    ILogger logger,
    Func<IArtifactPostProcessor> getProcessor,
    Func<bool> deferToDownstream,
    Func<string, Exception, string> formatWarning)
    : ITestHostControllerRunCompletionHandler, IOutputDeviceDataProducer
{
    public string Uid => $"{provider}.CoverageSummaryFinalizer";

    public string Version => ExtensionVersion.DefaultSemVer;

    public string DisplayName => provider;

    public string Description => "Finalizes CI coverage summaries after controller collection.";

    public Task<bool> IsEnabledAsync()
        => Task.FromResult(commandLineOptions.IsOptionSet(PlatformCommandLineProvider.CoverageThresholdLineOptionKey)
            || commandLineOptions.IsOptionSet(PlatformCommandLineProvider.CoverageThresholdBranchOptionKey));

    public async Task OnRunCompletedAsync(int exitCode, IReadOnlyList<SessionFileArtifact> artifacts, CancellationToken cancellationToken)
    {
        InputArtifact[] inputs = [.. artifacts
            .Where(artifact => string.Equals(artifact.Kind, fragmentKind, StringComparison.Ordinal))
            .Select(artifact => new InputArtifact(artifact.FileInfo.FullName, fragmentKind, null, null, null, null))];
        if (inputs.Length == 0 || !coverageResult.Thresholds.Any(threshold => threshold.ProducerId == nameof(CoverageThresholdPolicy)))
        {
            return;
        }

        string resultsDirectory = configuration.GetTestResultDirectory();
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var context = new ArtifactPostProcessingContext(ArtifactPostProcessingTruncationReason.None);
            foreach (InputArtifact input in inputs)
            {
                CiRunSummaryModule module = CiRunSummaryAggregation.ReadAndAggregate([input], provider, context).Modules.Single();
                module.Coverage = CiCoverageSummary.Create(coverageResult, sessionUid: null);
                module.ExitCode = exitCode;
                await CiRunSummaryAggregation.WriteFragmentAsync(resultsDirectory, provider, provider, module).ConfigureAwait(false);
            }

            // The downstream orchestrator already has these artifact paths. Updating the fragments
            // before completion preserves its ownership of multi-module reporting.
            if (!deferToDownstream())
            {
                await getProcessor().ProcessAsync(inputs, resultsDirectory, context, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or FormatException or JsonException)
        {
            string warning = formatWarning(resultsDirectory, ex);
            await logger.LogWarningAsync(warning).ConfigureAwait(false);
            await outputDevice.DisplayAsync(this, new WarningMessageOutputDeviceData(warning), cancellationToken).ConfigureAwait(false);
        }
    }
}

#pragma warning restore RS0051
