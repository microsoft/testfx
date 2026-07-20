// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Extensions.ArtifactPostProcessing;

/// <summary>
/// Processes artifacts of one or more well-known kinds after test execution completes.
/// </summary>
[Experimental("TPEXP", UrlFormat = "https://aka.ms/testingplatform/diagnostics#{0}")]
public interface IArtifactPostProcessor : IExtension
{
    /// <summary>
    /// Gets the producer-asserted artifact kinds supported by this processor.
    /// </summary>
    IReadOnlyList<string> SupportedKinds { get; }

    /// <summary>
    /// Gets the lowercase file extensions, including the leading dot, used to match artifacts
    /// produced by older hosts that do not provide a kind.
    /// </summary>
    IReadOnlyList<string> SupportedFileExtensionsFallback { get; }

    /// <summary>
    /// Processes matching artifacts and writes at most one result under <paramref name="outputDirectory"/>.
    /// </summary>
    /// <param name="inputs">The input artifacts. Implementations must treat them as read-only.</param>
    /// <param name="outputDirectory">The directory under which the processed artifact must be written.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The processed artifact, or <see langword="null"/> when no processing is needed.</returns>
    Task<ProcessedArtifact?> ProcessAsync(
        IReadOnlyList<InputArtifact> inputs,
        string outputDirectory,
        CancellationToken cancellationToken);
}

/// <summary>
/// Describes an artifact supplied to an <see cref="IArtifactPostProcessor"/>.
/// </summary>
[Experimental("TPEXP", UrlFormat = "https://aka.ms/testingplatform/diagnostics#{0}")]
public sealed class InputArtifact
{
    /// <summary>
    /// Initializes a new instance of the <see cref="InputArtifact"/> class.
    /// </summary>
    public InputArtifact(
        string path,
        string? kind,
        string? producingTestModule,
        string? targetFramework,
        string? architecture,
        string? executionId)
    {
        Path = path ?? throw new ArgumentNullException(nameof(path));
        Kind = kind;
        ProducingTestModule = producingTestModule;
        TargetFramework = targetFramework;
        Architecture = architecture;
        ExecutionId = executionId;
    }

    /// <summary>
    /// Gets the artifact path.
    /// </summary>
    public string Path { get; }

    /// <summary>
    /// Gets the producer-asserted artifact kind, or <see langword="null"/> when unavailable.
    /// </summary>
    public string? Kind { get; }

    /// <summary>
    /// Gets the path of the test module that produced the artifact.
    /// </summary>
    public string? ProducingTestModule { get; }

    /// <summary>
    /// Gets the target framework of the producing test module.
    /// </summary>
    public string? TargetFramework { get; }

    /// <summary>
    /// Gets the process architecture of the producing test module.
    /// </summary>
    public string? Architecture { get; }

    /// <summary>
    /// Gets the execution identifier associated with the artifact.
    /// </summary>
    public string? ExecutionId { get; }
}

/// <summary>
/// Describes an artifact produced by an <see cref="IArtifactPostProcessor"/>.
/// </summary>
[Experimental("TPEXP", UrlFormat = "https://aka.ms/testingplatform/diagnostics#{0}")]
public sealed class ProcessedArtifact
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ProcessedArtifact"/> class.
    /// </summary>
    public ProcessedArtifact(string path, string kind, string displayName, string? description)
    {
        Path = path ?? throw new ArgumentNullException(nameof(path));
        Kind = kind ?? throw new ArgumentNullException(nameof(kind));
        DisplayName = displayName ?? throw new ArgumentNullException(nameof(displayName));
        Description = description;
    }

    /// <summary>
    /// Gets the processed artifact path.
    /// </summary>
    public string Path { get; }

    /// <summary>
    /// Gets the producer-asserted artifact kind.
    /// </summary>
    public string Kind { get; }

    /// <summary>
    /// Gets the artifact display name.
    /// </summary>
    public string DisplayName { get; }

    /// <summary>
    /// Gets the artifact description.
    /// </summary>
    public string? Description { get; }
}
