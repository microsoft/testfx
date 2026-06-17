// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Extensions.CtrfReport;

internal sealed class CapturedTestResult : CapturedTestResultBase
{
    // CTRF status (passed/failed/skipped/pending/other) — already normalized.
    public required string Status { get; init; }

    // Preserves the original MTP outcome when it doesn't map 1:1 to CTRF
    // (e.g. "timedOut", "errored", "cancelled"). Surfaced via CTRF `rawStatus`.
    public string? RawStatus { get; init; }

    public string? Namespace { get; init; }

    public string? FilePath { get; init; }

    public int? Line { get; init; }
}
