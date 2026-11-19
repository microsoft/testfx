// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Platform.IPC.Models;

// Each entry replaces the measurement with the same execution/instance/session/producer/scope/metric
// identity. Entries from different applications must never be added to invent a merged total.
internal sealed record CoverageSummaryMessage(
    string? SessionUid,
    string? ProducerId,
    int? ScopeLevel,
    string? ScopeName,
    int? Metric,
    string? CustomMetricName,
    long? CoveredCount,
    long? CoverableCount);

internal sealed record CoverageSummaryMessages(string? ExecutionId, string? InstanceId, CoverageSummaryMessage[] Summaries) : IRequest;
