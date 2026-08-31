// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Text.Json.Serialization;

namespace Microsoft.Testing.Extensions.AzureDevOpsReport;

internal sealed record AzureDevOpsCoordinatedRun(
    int RunId,
    bool IsOwner,
    int BuildId,
    string ResultsDirectory,
    string RunIdFilePath,
    string OwnerFilePath,
    string ParticipantFilePath)
{
    /// <summary>
    /// Gets a value indicating whether the run was created by an ancestor process, which also owns closing it.
    /// </summary>
    /// <remarks>
    /// Declared as a property rather than a positional parameter so that adding it does not change the
    /// record's constructor and deconstructor signatures. An inherited run has no coordination files of
    /// its own: the ancestor decides when the run ends, so there is no owner to elect and no participant
    /// set to drain.
    /// </remarks>
    public bool IsInherited { get; init; }
}

internal sealed record AzureDevOpsRunIdFile(
    [property: JsonPropertyName("runId")] int RunId,
    [property: JsonPropertyName("buildId")] int BuildId,
    [property: JsonPropertyName("collectionUri")] string CollectionUri,
    [property: JsonPropertyName("project")] string Project,
    [property: JsonPropertyName("expiresAt")] DateTimeOffset ExpiresAt);

internal sealed record AzureDevOpsLeaseFile(
    [property: JsonPropertyName("processId")] int ProcessId,
    [property: JsonPropertyName("buildId")] int BuildId,
    [property: JsonPropertyName("expiresAt")] DateTimeOffset ExpiresAt);
