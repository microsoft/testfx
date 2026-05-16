// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Extensions.TrxReport.Abstractions.Streaming;

// Projected from TestNodeStateProperty. Only the four buckets the TRX renderer cares about
// (skipped count, passed count, timed-out count, all other failures count).
internal enum TrxTestOutcome : byte
{
    Passed = 1,
    Skipped = 2,
    Failed = 3,
    Timeout = 4,
}

internal sealed class TrxTestResult
{
    public required string Uid { get; init; }

    public required string DisplayName { get; init; }

    public required TrxTestOutcome Outcome { get; init; }

    public DateTimeOffset? StartTime { get; init; }

    public DateTimeOffset? EndTime { get; init; }

    public TimeSpan? Duration { get; init; }

    public string? TrxTestDefinitionName { get; init; }

    public string? TrxFullyQualifiedTypeName { get; init; }

    public TrxTestMethodIdentifier? TestMethodIdentifier { get; init; }

    public string? ExceptionMessage { get; init; }

    public string? ExceptionStackTrace { get; init; }

    public IReadOnlyList<TrxStreamMessage>? Messages { get; init; }

    public IReadOnlyList<string>? Categories { get; init; }

    public IReadOnlyList<TrxTestMetadata>? Metadata { get; init; }

    public IReadOnlyList<TrxTestFileArtifact>? FileArtifacts { get; init; }
}

internal sealed class TrxTestMethodIdentifier
{
    public required string Namespace { get; init; }

    public required string TypeName { get; init; }

    public required string MethodName { get; init; }
}

internal enum TrxStreamMessageKind : byte
{
    StandardOutput = 1,
    StandardError = 2,
    DebugOrTrace = 3,
}

internal sealed class TrxStreamMessage
{
    public required TrxStreamMessageKind Kind { get; init; }

    public string? Message { get; init; }
}

internal sealed class TrxTestMetadata
{
    public required string Key { get; init; }

    public required string Value { get; init; }
}

internal sealed class TrxTestFileArtifact
{
    public required string FullPath { get; init; }
}
