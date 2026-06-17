// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.Testing.Extensions.JUnitReport;

internal sealed class CapturedTestResult : CapturedTestResultBase
{
    // Raw UID used internally as the key to look up parents in the parent-chain
    // dictionary. It is capped with MaxIdentityFieldLength (same budget as Uid) so that
    // both sides of a lookup are truncated consistently and the chain lookup still matches.
    public required string RawUid { get; init; }

    // Raw parent UID (edge into the parent-chain dictionary) for the same reason; also
    // capped with MaxIdentityFieldLength so it matches the corresponding RawUid key.
    public string? ParentRawUid { get; init; }

    public required string Outcome { get; init; }
}
