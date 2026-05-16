// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Extensions.HtmlReport;

// Minimal capped-size projection of a TestNodeUpdateMessage. The consumer projects
// each message into this DTO immediately so that we don't retain entire test nodes
// (and their potentially huge stdout/stderr/stack trace strings) in memory for the
// whole session. All variable-length text fields are already truncated at this point
// so the engine doesn't need to truncate again.
internal sealed class CapturedTestResult
{
    public required string Uid { get; init; }

    public required string DisplayName { get; init; }

    public required string Outcome { get; init; }

    public required TimeSpan Duration { get; init; }

    public DateTimeOffset? StartTime { get; init; }

    public DateTimeOffset? EndTime { get; init; }

    public string? ClassName { get; init; }

    public string? MethodName { get; init; }

    public string? ErrorMessage { get; init; }

    public string? ExceptionType { get; init; }

    public string? StackTrace { get; init; }

    public string? StandardOutput { get; init; }

    public string? StandardError { get; init; }

    public IReadOnlyList<KeyValuePair<string, string>>? Traits { get; init; }
}
