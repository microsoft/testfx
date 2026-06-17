// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Extensions;

// Minimal projection of a terminal test result, shared by the HtmlReport, JUnitReport and
// CtrfReport consumers (which capture from either a TestNode or a TestNodeUpdateMessage).
// The consumer projects each result into this DTO immediately so that we don't retain
// entire test nodes (and their potentially huge stdout/stderr/stack trace strings) in
// memory for the whole session. The variable-length text fields declared here are
// truncated by the capturing code before construction; derived DTOs may add further
// fields that follow their own truncation rules, so do not assume every field is capped.
internal abstract class CapturedTestResultBase
{
    public required string Uid { get; init; }

    public required string DisplayName { get; init; }

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
