// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text.Json.Serialization;

namespace Microsoft.Testing.Extensions.AzureDevOpsReport;

/// <summary>A test already published to the run, with every attempt seen so far.</summary>
internal sealed record AzureDevOpsPublishedResult(
    string Storage,
    string Name,
    string Title,
    int Id,
    IReadOnlyList<AzureDevOpsTestSubResult> Attempts)
{
    public int LastPublishedSubResultSequenceId { get; init; }

    public long? TotalDurationInMs { get; init; }

    public DateTimeOffset? StartedDate { get; init; }

    public DateTimeOffset? CompletedDate { get; init; }
}

/// <summary>
/// On-disk shape of one map entry.
/// </summary>
/// <remarks>
/// Carries its key as ordinary fields rather than being a JSON object keyed by test name, because a test
/// uid can contain any character and would otherwise need escaping. Every member is nullable because the
/// file is untrusted input.
/// </remarks>
internal sealed record AzureDevOpsResultMapEntry(
    [property: JsonPropertyName("storage")] string? Storage,
    [property: JsonPropertyName("name")] string? Name,
    [property: JsonPropertyName("title")] string? Title,
    [property: JsonPropertyName("id")] int Id,
    [property: JsonPropertyName("attempts")] IReadOnlyList<AzureDevOpsTestSubResult>? Attempts)
{
    [JsonPropertyName("lastPublishedSubResultSequenceId")]
    public int? LastPublishedSubResultSequenceId { get; init; }

    [JsonPropertyName("totalDurationInMs")]
    public long? TotalDurationInMs { get; init; }

    [JsonPropertyName("startedDate")]
    public DateTimeOffset? StartedDate { get; init; }

    [JsonPropertyName("completedDate")]
    public DateTimeOffset? CompletedDate { get; init; }
}

/// <summary>
/// On-disk shape of the result map.
/// </summary>
/// <remarks>
/// Unknown members are ignored on read, so additive fields remain forward-compatible. The build and run
/// ids are required discriminators: a map that predates either one is deliberately ignored, because result
/// ids are only meaningful within the run that created them.
/// </remarks>
internal sealed record AzureDevOpsResultMapFile(
    [property: JsonPropertyName("buildId")] int BuildId,
    [property: JsonPropertyName("runId")] int RunId,
    [property: JsonPropertyName("results")] IReadOnlyList<AzureDevOpsResultMapEntry>? Results);
