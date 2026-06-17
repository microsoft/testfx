// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Extensions.JUnitReport;

internal sealed class CapturedTestResult : CapturedTestResultBase
{
    // Raw, untruncated UID. Used internally to look up parents in the parent-chain
    // dictionary so that truncation of a long UID does not break the chain lookup.
    public required string RawUid { get; init; }

    // Raw, untruncated parent UID for the same reason.
    public string? ParentRawUid { get; init; }

    public required string Outcome { get; init; }
}
