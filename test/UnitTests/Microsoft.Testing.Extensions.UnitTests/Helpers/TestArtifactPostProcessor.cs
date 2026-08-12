// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Testing.Platform.Extensions.ArtifactPostProcessing;

namespace Microsoft.Testing.Extensions.UnitTests.Helpers;

#pragma warning disable TPEXP // Artifact post-processing is experimental.

internal sealed class TestArtifactPostProcessor : IArtifactPostProcessor
{
    private readonly Func<IReadOnlyList<InputArtifact>, string, ArtifactPostProcessingContext, CancellationToken, Task<ProcessedArtifact?>> _processAsync;

    public TestArtifactPostProcessor(
        IReadOnlyList<string> supportedKinds,
        Func<IReadOnlyList<InputArtifact>, string, ArtifactPostProcessingContext, CancellationToken, Task<ProcessedArtifact?>> processAsync,
        string uid = "TestArtifactPostProcessor",
        IReadOnlyList<string>? supportedFileExtensionsFallback = null)
    {
        SupportedKinds = supportedKinds;
        _processAsync = processAsync;
        Uid = uid;
        SupportedFileExtensionsFallback = supportedFileExtensionsFallback ?? [];
    }

    public string Uid { get; }

    public string Version => "Version";

    public string DisplayName => "DisplayName";

    public string Description => "Description";

    public Task<bool> IsEnabledAsync() => Task.FromResult(true);

    public IReadOnlyList<ArtifactPostProcessingMode> SupportedModes { get; } = [ArtifactPostProcessingMode.RetryAttempts];

    public bool SupportsTruncatedRuns => true;

    public IReadOnlyList<string> SupportedKinds { get; }

    public IReadOnlyList<string> SupportedFileExtensionsFallback { get; }

    public Task<ProcessedArtifact?> ProcessAsync(
        IReadOnlyList<InputArtifact> inputs,
        string outputDirectory,
        ArtifactPostProcessingContext context,
        CancellationToken cancellationToken)
        => _processAsync(inputs, outputDirectory, context, cancellationToken);
}
