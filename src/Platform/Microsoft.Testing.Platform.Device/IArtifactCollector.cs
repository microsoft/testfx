// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Device;

/// <summary>
/// Collects test artifacts from a device.
/// </summary>
public interface IArtifactCollector
{
    /// <summary>
    /// Collects test artifacts (TRX, coverage, logs) from the device.
    /// </summary>
    /// <param name="device">Source device.</param>
    /// <param name="appId">Application identifier.</param>
    /// <param name="outputDirectory">Local directory to save artifacts to.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Collection of retrieved artifacts.</returns>
    Task<ArtifactCollection> CollectArtifactsAsync(
        DeviceInfo device,
        string appId,
        string outputDirectory,
        CancellationToken cancellationToken);
}

/// <summary>
/// Collection of test artifacts.
/// </summary>
/// <param name="TrxFiles">TRX test result files.</param>
/// <param name="CoverageFiles">Code coverage files.</param>
/// <param name="LogFiles">Log files.</param>
/// <param name="OtherFiles">Other artifact files.</param>
public record ArtifactCollection(
    IReadOnlyList<string> TrxFiles,
    IReadOnlyList<string> CoverageFiles,
    IReadOnlyList<string> LogFiles,
    IReadOnlyList<string> OtherFiles);
