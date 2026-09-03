// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.Extensions.ArtifactPostProcessing;

/// <summary>
/// Processes artifacts of one or more well-known kinds after test execution completes.
/// </summary>
/// <remarks>
/// This API is experimental. It may change, break, or be removed at any time without notice.
/// </remarks>
[Experimental("TPEXP", UrlFormat = "https://aka.ms/testingplatform/diagnostics#{0}")]
public interface IArtifactPostProcessor : IExtension
{
    /// <summary>
    /// Gets the post-processing modes supported by this processor.
    /// </summary>
    IReadOnlyList<ArtifactPostProcessingMode> SupportedModes { get; }

    /// <summary>
    /// Gets a value indicating whether this processor can process artifacts observed before a run was truncated.
    /// </summary>
    /// <remarks>
    /// This capability only indicates support for an incomplete set of complete artifacts. It does not indicate
    /// that the processor can consume malformed or partially written files.
    /// </remarks>
    bool SupportsTruncatedRuns { get; }

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
    /// <param name="context">The context describing the test run that produced the artifacts.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>
    /// The processed artifact representing all <paramref name="inputs"/>, or <see langword="null"/> when no
    /// processing is needed.
    /// </returns>
    /// <remarks>
    /// Implementations must be deterministic and idempotent because orchestrators may retry transient failures.
    /// A non-<see langword="null"/> result must represent every supplied input. Implementations that cannot produce
    /// one artifact representing the complete input set must return <see langword="null"/> or throw.
    /// When a processor declares both kinds and legacy file extensions, <paramref name="inputs"/> can contain the
    /// union of producer-kind matches and untagged extension-fallback matches.
    /// </remarks>
    Task<ProcessedArtifact?> ProcessAsync(
        IReadOnlyList<InputArtifact> inputs,
        string outputDirectory,
        ArtifactPostProcessingContext context,
        CancellationToken cancellationToken);
}

/// <summary>
/// Marks an artifact post-processor whose inputs are internal coordination artifacts that must always be
/// post-processed, including when only one matching input exists.
/// </summary>
/// <remarks>
/// Orchestrators that support this capability advertise that support in the handshake response before a test
/// session starts. Producers must preserve their standalone behavior when the capability is unavailable.
/// <para>
/// This API is experimental. It may change, break, or be removed at any time without notice.
/// </para>
/// </remarks>
[Experimental("TPEXP", UrlFormat = "https://aka.ms/testingplatform/diagnostics#{0}")]
public interface IArtifactPostProcessorRequiresPostProcessing : IArtifactPostProcessor;

/// <summary>
/// Describes the test run that produced artifacts supplied to an <see cref="IArtifactPostProcessor"/>.
/// </summary>
/// <remarks>
/// This API is experimental. It may change, break, or be removed at any time without notice.
/// </remarks>
[Experimental("TPEXP", UrlFormat = "https://aka.ms/testingplatform/diagnostics#{0}")]
public sealed class ArtifactPostProcessingContext
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ArtifactPostProcessingContext"/> class.
    /// </summary>
    /// <param name="truncationReason">The reason the run was truncated, or <see cref="ArtifactPostProcessingTruncationReason.None"/> for a complete run.</param>
    public ArtifactPostProcessingContext(ArtifactPostProcessingTruncationReason truncationReason)
        : this(truncationReason, ArtifactPostProcessingMode.TestModules, runSummary: null)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ArtifactPostProcessingContext"/> class.
    /// </summary>
    /// <param name="truncationReason">The reason the run was truncated, or <see cref="ArtifactPostProcessingTruncationReason.None"/> for a complete run.</param>
    /// <param name="mode">The operation that supplied the artifacts.</param>
    public ArtifactPostProcessingContext(
        ArtifactPostProcessingTruncationReason truncationReason,
        ArtifactPostProcessingMode mode)
        : this(truncationReason, mode, runSummary: null)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ArtifactPostProcessingContext"/> class.
    /// </summary>
    /// <param name="truncationReason">The reason the run was truncated, or <see cref="ArtifactPostProcessingTruncationReason.None"/> for a complete run.</param>
    /// <param name="runSummary">The authoritative orchestrator summary, or <see langword="null"/> when unavailable.</param>
    public ArtifactPostProcessingContext(
        ArtifactPostProcessingTruncationReason truncationReason,
        ArtifactPostProcessingRunSummary? runSummary)
        : this(truncationReason, ArtifactPostProcessingMode.TestModules, runSummary)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ArtifactPostProcessingContext"/> class.
    /// </summary>
    /// <param name="truncationReason">The reason the run was truncated, or <see cref="ArtifactPostProcessingTruncationReason.None"/> for a complete run.</param>
    /// <param name="mode">The operation that supplied the artifacts.</param>
    /// <param name="runSummary">The authoritative orchestrator summary, or <see langword="null"/> when unavailable.</param>
    public ArtifactPostProcessingContext(
        ArtifactPostProcessingTruncationReason truncationReason,
        ArtifactPostProcessingMode mode,
        ArtifactPostProcessingRunSummary? runSummary)
    {
        TruncationReason = truncationReason;
        Mode = mode;
        RunSummary = runSummary;
    }

    /// <summary>
    /// Gets a value indicating whether the test run was truncated.
    /// </summary>
    public bool IsTruncated => TruncationReason != ArtifactPostProcessingTruncationReason.None;

    /// <summary>
    /// Gets the operation that supplied the artifacts.
    /// </summary>
    public ArtifactPostProcessingMode Mode { get; }

    /// <summary>
    /// Gets the reason the test run was truncated.
    /// </summary>
    public ArtifactPostProcessingTruncationReason TruncationReason { get; }

    /// <summary>
    /// Gets the authoritative orchestrator summary, or <see langword="null"/> when the invoking orchestrator
    /// does not provide run-level totals, duration, and exit verdict.
    /// </summary>
    public ArtifactPostProcessingRunSummary? RunSummary { get; }
}

/// <summary>
/// Describes authoritative run-level values supplied by the outer test orchestrator.
/// </summary>
/// <remarks>
/// This API is experimental. It may change, break, or be removed at any time without notice.
/// </remarks>
[Experimental("TPEXP", UrlFormat = "https://aka.ms/testingplatform/diagnostics#{0}")]
public sealed class ArtifactPostProcessingRunSummary
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ArtifactPostProcessingRunSummary"/> class.
    /// </summary>
    public ArtifactPostProcessingRunSummary(
        long totalTests,
        long passedTests,
        long failedTests,
        long skippedTests,
        TimeSpan duration,
        int exitCode,
        int testModuleCount)
    {
        if (totalTests < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(totalTests));
        }

        if (passedTests < 0 || passedTests > totalTests)
        {
            throw new ArgumentOutOfRangeException(nameof(passedTests));
        }

        if (failedTests < 0 || failedTests > totalTests - passedTests)
        {
            throw new ArgumentOutOfRangeException(nameof(failedTests));
        }

        if (skippedTests < 0 || skippedTests != totalTests - passedTests - failedTests)
        {
            throw new ArgumentOutOfRangeException(nameof(skippedTests));
        }

        if (duration < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(duration));
        }

        if (testModuleCount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(testModuleCount));
        }

        TotalTests = totalTests;
        PassedTests = passedTests;
        FailedTests = failedTests;
        SkippedTests = skippedTests;
        Duration = duration;
        ExitCode = exitCode;
        TestModuleCount = testModuleCount;
    }

    /// <summary>
    /// Gets the total number of tests reported by the orchestrator.
    /// </summary>
    public long TotalTests { get; }

    /// <summary>
    /// Gets the number of passed tests reported by the orchestrator.
    /// </summary>
    public long PassedTests { get; }

    /// <summary>
    /// Gets the number of failed tests reported by the orchestrator.
    /// </summary>
    public long FailedTests { get; }

    /// <summary>
    /// Gets the number of skipped tests reported by the orchestrator.
    /// </summary>
    public long SkippedTests { get; }

    /// <summary>
    /// Gets the outer orchestrator wall-clock duration.
    /// </summary>
    public TimeSpan Duration { get; }

    /// <summary>
    /// Gets the outer orchestrator exit code.
    /// </summary>
    public int ExitCode { get; }

    /// <summary>
    /// Gets the number of test modules included in the orchestrator summary.
    /// </summary>
    public int TestModuleCount { get; }
}

/// <summary>
/// Specifies how the supplied artifacts relate to one another.
/// </summary>
/// <remarks>
/// This API is experimental. It may change, break, or be removed at any time without notice.
/// </remarks>
[Experimental("TPEXP", UrlFormat = "https://aka.ms/testingplatform/diagnostics#{0}")]
public enum ArtifactPostProcessingMode
{
    /// <summary>
    /// Artifacts were produced by distinct test modules in one test run.
    /// </summary>
    TestModules,

    /// <summary>
    /// Artifacts were produced by successive attempts of the same logical tests.
    /// Inputs are ordered from the initial execution to the final attempt.
    /// </summary>
    RetryAttempts,
}

/// <summary>
/// Specifies why a test run was truncated before every scheduled test module completed.
/// </summary>
/// <remarks>
/// This API is experimental. It may change, break, or be removed at any time without notice.
/// </remarks>
[Experimental("TPEXP", UrlFormat = "https://aka.ms/testingplatform/diagnostics#{0}")]
public enum ArtifactPostProcessingTruncationReason
{
    /// <summary>
    /// The test run completed without policy-driven truncation.
    /// </summary>
    None,

    /// <summary>
    /// The test run reached the configured maximum number of failed tests.
    /// </summary>
    MaximumFailedTests,

    /// <summary>
    /// The test run reached its configured timeout.
    /// </summary>
    Timeout,
}

/// <summary>
/// Describes an artifact supplied to an <see cref="IArtifactPostProcessor"/>.
/// </summary>
/// <remarks>
/// This API is experimental. It may change, break, or be removed at any time without notice.
/// </remarks>
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
/// <remarks>
/// This API is experimental. It may change, break, or be removed at any time without notice.
/// </remarks>
[Experimental("TPEXP", UrlFormat = "https://aka.ms/testingplatform/diagnostics#{0}")]
public sealed class ProcessedArtifact
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ProcessedArtifact"/> class.
    /// </summary>
    public ProcessedArtifact(string path, string kind, string displayName, string? description)
    {
        Path = path ?? throw new ArgumentNullException(nameof(path));
        Kind = kind is null
            ? throw new ArgumentNullException(nameof(kind))
            : RoslynString.IsNullOrWhiteSpace(kind)
                ? throw new ArgumentException(null, nameof(kind))
                : kind;
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
